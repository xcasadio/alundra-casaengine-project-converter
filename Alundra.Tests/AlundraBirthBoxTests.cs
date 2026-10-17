#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a5 phase B (T2, docs/plan-e20-heros.md section 1.16.5, D-E0A5-3, ADR-0071): the frame box of an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> is resolved BEFORE the engine integrated it (its sprite
/// has no <c>Animations</c> yet), from the animation data of the prefab, and is never frozen at null. Read by key from the oracle (<c>birth_clock.json</c> rows <c>SW0</c>, <c>WAVE</c>; <c>wave_steel_*.json</c> of E20.f3; <c>v1.json</c>
/// and <c>v2.json</c> of E20.e0a, <c>p.has_box</c>), never pasted. S is the first tick of the entity. Labels: [R] red on the code of phase A (predicted), [V] green by construction.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraBirthBoxTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public AlundraBirthBoxTests(ITestOutputHelper output)
    {
        _output = output;
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
    }

    public void Dispose() => AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

    /// <summary>The box of the binary at one tick: the offset of the stored box from the entity (<c>hbx - px</c>, <c>hby - py</c>, <c>hbz - pz</c>) and its sizes (<c>bw</c>, <c>bd</c>, <c>bh</c>, the raw <c>(n &lt;&lt; 16) - 1</c>).</summary>
    private readonly record struct BoxRow(long Dx, long Dy, long Dz, long W, long D, long H);

    private static BoxRow Of(JsonElement t)
        => new(t.GetProperty("hbx").GetInt64() - t.GetProperty("px").GetInt64(), t.GetProperty("hby").GetInt64() - t.GetProperty("py").GetInt64(), t.GetProperty("hbz").GetInt64() - t.GetProperty("pz").GetInt64(),
            t.GetProperty("bw").GetInt64(), t.GetProperty("bd").GetInt64(), t.GetProperty("bh").GetInt64());

    /// <summary>The boxes of a row of <c>birth_clock.json</c> by tick relative to S, for the ticks the entity is alive in the oracle (status 2).</summary>
    private static SortedDictionary<int, BoxRow> OracleRow(string label)
        => new(BirthClockOracle.Ticks(label).Where(t => t.GetProperty("status").GetInt32() == 2).ToDictionary(t => t.GetProperty("k").GetInt32(), Of));

    /// <summary>The boxes of the wave of a steel sword from the E20.f3 oracle file (slot 1), by tick relative to S, while it is alive (status 2).</summary>
    private static SortedDictionary<int, BoxRow> OracleWave(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BirthClockOracle.Find("docs", "plan-e20-annexe", "e20-f3", "oracle", "out", file)));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray()
            .Where(r => r.GetProperty("slots").TryGetProperty("1", out var one) && one.ValueKind == JsonValueKind.Object)
            .Select(r => (Tick: r.GetProperty("tick").GetInt32(), Slot: r.GetProperty("slots").GetProperty("1").Clone()))
            .ToList();
        var first = rows[0].Tick;
        return new SortedDictionary<int, BoxRow>(rows.Where(r => r.Slot.GetProperty("status").GetInt32() == 2).ToDictionary(r => r.Tick - first, r => Of(r.Slot)));
    }

    /// <summary>What the entity holds as its box at the hit point of a tick (stored box minus position, then the sizes), or null when it has none.</summary>
    private static BoxRow? DllBox(AlundraEntityScriptProxy born)
        => born.HasFrameBox
            ? new BoxRow((long)born.HitBoxX - born.PosX, (long)born.HitBoxY - born.PosY, (long)born.HitBoxZ - born.PosZ, born.CollisionWidth, born.CollisionDepth, born.CollisionHeight)
            : null;

    private static string WaveFile(int direction)
        => direction switch { 0x00 => "wave_steel_south.json", 0x08 => "wave_steel_west.json", 0x10 => "wave_steel_north.json", _ => "wave_steel_east.json" };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b1) The box of the wave at every tick the oracle has it alive: the same offsets and sizes (36 x 24 x 31 and its moving offset), the position and the ground constant, in the four directions of the steel sword, at 1, 2 and 3
    // ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x18, 1, 1)]
    [InlineData(0x18, 2, 1)]
    [InlineData(0x18, 3, 1)]
    [InlineData(0x18, 3, 2)]
    [InlineData(0x00, 1, 1)]
    [InlineData(0x00, 2, 2)]
    [InlineData(0x00, 3, 3)]
    [InlineData(0x08, 1, 1)]
    [InlineData(0x08, 2, 1)]
    [InlineData(0x08, 3, 2)]
    [InlineData(0x10, 1, 1)]
    [InlineData(0x10, 2, 2)]
    [InlineData(0x10, 3, 1)]
    public void B1_R_TheBoxOfTheWave_IsTheOneOfTheBinaryAtEveryTick_InTheFourDirectionsOfTheSteelSword(int direction, int ticksPerFrame, int position)
    {
        var oracle = OracleWave(WaveFile(direction));
        using var run = new AlundraCreationSwitchClockTests.Birth($"b1-{direction}-{ticksPerFrame}-{position}", 27, direction);
        var boxes = new SortedDictionary<int, BoxRow?>();
        var positions = new SortedDictionary<int, (int X, int Y, int Z)>();
        run.AtHit = (born, k) =>
        {
            boxes[k] = DllBox(born);
            positions[k] = (born.PosX, born.PosY, born.PosZ);
        };
        run.RunAll(ticksPerFrame, position, oracle.Keys.Max());
        Assert.NotNull(run.Born);

        var different = oracle.Keys.Where(k => !Equals(boxes.GetValueOrDefault(k), (BoxRow?)oracle[k])).Select(k => $"S+{k}: dll {boxes.GetValueOrDefault(k)?.ToString() ?? "no box"}, oracle {oracle[k]}").ToList();
        var constant = oracle.Keys.Select(k => positions[k]).Distinct().Count();
        Assert.True(different.Count == 0 && constant == 1, $"{different.Count} ticks differ, {constant} positions; first: {string.Join(" | ", different.Take(3))}");
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b2) The box of the creation (0x80039F30): HasFrameBox right after the creation is the has_box of the oracle, for the 151 prefabs of the global page and the numbers of the map page valid on map 14 that B2 of
    // AlundraSpawnBySpriteIndexInterfaceTests replays; the vectors at 0 stay false in the same assertion.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Dictionary<string, bool> HasBoxByVector(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AlundraSpawnIndexOracle.OutputFolder(), file)));
        return document.RootElement.EnumerateArray().Where(v => v.TryGetProperty("p", out _))
            .ToDictionary(v => v.GetProperty("id").GetString()!, v => v.GetProperty("p").GetProperty("has_box").GetInt64() != 0);
    }

    [Fact]
    public void B2_R_HasFrameBoxAtTheCreation_IsTheHasBoxOfTheOracle_OnTheVectorsOfTheInterfaceTest_AndTheVectorsAtZeroStayFalse()
    {
        using var arc = AlundraCreationSwitchClockTests.Arc("b2");
        var onMap14 = AlundraSpawnIndexOracle.ValidIndicesByMap()[14].ToHashSet();
        var vectors = AlundraSpawnIndexOracle.Vectors("v1").Concat(AlundraSpawnIndexOracle.Vectors("v2").Where(v => onMap14.Contains(v.Index))).ToList();
        var oracle = HasBoxByVector("v1.json").Concat(HasBoxByVector("v2.json")).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Value);
        var expected = new List<(string Id, bool HasBox)>();
        var seen = new List<(string Id, bool HasBox)>();
        foreach (var v in vectors)
        {
            var spawned = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, v.IsMapPage, v.Index, v.X << 16, v.Y << 16, v.Z - 1, v.Direction);
            Assert.NotNull(spawned);
            expected.Add((v.Id, oracle[v.Id]));
            seen.Add((v.Id, spawned!.HasFrameBox));
            spawned.Status = EntityStatus.FlagToDestroy;
            arc.Proxy.RecycleDestroyedEntities();
        }

        Assert.Contains(expected, e => e.HasBox);
        Assert.Contains(expected, e => !e.HasBox);
        Assert.Equal(expected, seen);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b3) Before the integration the sprite has no animation (PA1: the list is not touched) and the entity has its box at S; after the integration the box of S+1 is the one of the image 1. Without resources (a host that
    // has no asset manager), asked once, no exception, and once the engine integrated the entity the box is read from the sprite: the timeline was not frozen at null.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_R_BeforeTheIntegrationTheSpriteHasNoAnimationAndTheEntityHasItsBox_AfterItTheBoxOfSPlus1IsTheImage1_AndWithoutResourcesTheTimelineIsNotFrozenAtNull()
    {
        var oracle = OracleRow("WAVE");
        using var run = new AlundraCreationSwitchClockTests.Birth("b3", 27, 0x18);
        (int Animations, bool HasBox, bool WorldNull)? atS = null;
        BoxRow? atSPlus1 = null;
        run.AtHit = (born, k) =>
        {
            if (k == 0)
            {
                var entity = ArcRun.EntityOf(born)!;
                atS = (entity.GetComponent<AnimatedSpriteComponent>()!.Animations.Count, born.HasFrameBox, entity.World == null);
            }
            else if (k == 1)
            {
                atSPlus1 = DllBox(born);
            }
        };
        run.RunAll(1, 1, 2);

        // Without resources: the host answers nothing.
        var fake = new NoAssetsHost();
        using var arc = AlundraCreationSwitchClockTests.Arc("b3-nores");
        var born = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 27, 100 << 16, 100 << 16, 0, 0x18)!;
        var realHost = born.ScriptHost;
        born.ScriptHost = fake;
        born.LoadAnimationSwitch(born.BoxAnimationId, born.BoxDirection, fromClock: false);
        var thrown = false;
        var before = new List<bool>();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                before.Add(born.EvaluateFrameBox());
            }
        }
        catch (Exception)
        {
            thrown = true;
        }

        born.ScriptHost = realHost;
        arc.OneFrame();
        var after = born.EvaluateFrameBox();

        Assert.Equal(((0, true, true), oracle[1], false, false, 1, false, true), (atS.GetValueOrDefault(), atSPlus1, thrown, before.Any(b => b), fake.Calls, ArcRun.EntityOf(born)!.World == null, after));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b4) A switch asked before the integration (SW0: the animation 1 at S): the box of the animation 1 at S, from the animation data of the prefab (x 88 for an entity at 100: -12 px; 23 x 23 x 15), and the following ticks.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_R_ASwitchBeforeTheIntegration_GivesTheBoxOfTheAnimation1AtS_AndAtTheTicksThatFollow()
    {
        var oracle = OracleRow("SW0");
        using var run = new AlundraCreationSwitchClockTests.Birth("b4", 27, 0x18);
        run.AtLists = (born, k) =>
        {
            if (k == 0)
            {
                born.TargetAnimationId = 1;
            }
        };
        var boxes = new SortedDictionary<int, BoxRow?>();
        run.AtHit = (born, k) => boxes[k] = DllBox(born);
        run.RunAll(1, 1, 8);
        var keys = oracle.Keys.Where(k => k <= 8).ToList();
        Assert.Equal(keys.Select(k => (k, (BoxRow?)oracle[k])).ToList(), keys.Select(k => (k, boxes.GetValueOrDefault(k))).ToList());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b5) After the validation the cursor of the box stands for the logical tick of the sprite at the end of every frame, for 30 ticks, at 1, 2 and 3 ticks per frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    [InlineData(3, 3)]
    public void B5_R_AfterTheValidation_TheCursorOfTheBox_IsTheLogicalTickOfTheSprite_AtTheEndOfEveryFrame(int ticksPerFrame, int position)
    {
        using var run = new AlundraCreationSwitchClockTests.Birth($"b5-{ticksPerFrame}-{position}", 27, 0x18);
        run.Settle(position);
        AlundraCreationSwitchClockTests.Frame(run.Arc, ticksPerFrame); // the frame of the birth: the sprite is integrated at the head of the next one
        Assert.NotNull(run.Born);
        var sprite = ArcRun.EntityOf(run.Born!)!.GetComponent<AnimatedSpriteComponent>()!;
        var cursor = new List<int?>();
        var logical = new List<int?>();
        while (run.Tick < run.S + 30)
        {
            AlundraCreationSwitchClockTests.Frame(run.Arc, ticksPerFrame);
            cursor.Add(run.Born!.CursorLogicalTickForTests());
            logical.Add(sprite.LogicalTick);
        }

        Assert.True(cursor.Count >= 8);
        Assert.Equal(logical, cursor);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [V] (b6) Zero change: an entity of the MAP LOAD keeps its timeline frozen at null at its creation and has no box (the host is not asked); and a host with no resources is asked at most once, with no exception. Since E20.e0a4
    // (D-E0A4-6) the pin is on an entity of the load: an entity born from a record by a script carries the rule of the interface (the box exists at the creation, the host is asked once to resolve it); the same entity is seen
    // in the same assertion.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B6_V_AnEntityOfTheMapLoad_HasNoBoxAtItsCreation_AndTheHostIsNeverAsked_AndAHostWithoutResourcesIsAskedAtMostOnce()
    {
        using var arc = AlundraCreationSwitchClockTests.Arc("b6");
        var asked = 0;
        arc.Proxy.ResolveAnimationDataForTests = () => asked++;
        var loaded = arc.Entities.First(e => e.EntityRefId >= 0);
        var record = loaded.EntityRefId;
        var born = loaded;
        var results = new List<bool>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(born.EvaluateFrameBox());
        }

        var askedByTheRecord = asked;

        // The same record born by a script (E20.e0a4): the rule of the interface, the host asked once by the creation.
        var scripted = arc.Proxy.SpawnEntityByRecordId(arc.Hero, record)!;
        var askedByTheScript = asked - askedByTheRecord;

        // The interface's entity, with a host that has no resources: asked at most once, no exception, no box before the integration.
        var viaInterface = ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 27, 100 << 16, 100 << 16, 0, 0x18)!;
        var fake = new NoAssetsHost();
        viaInterface.ScriptHost = fake;
        viaInterface.LoadAnimationSwitch(viaInterface.BoxAnimationId, viaInterface.BoxDirection, fromClock: false);
        for (var i = 0; i < 4; i++)
        {
            viaInterface.EvaluateFrameBox();
        }

        Assert.Equal(
            (false, false, false, null, 0, true, false, (true, true, 1)),
            (results.Any(r => r), born.HasFrameBox, born.SwitchedAtCreation, born.CursorLogicalTickForTests(), askedByTheRecord, fake.Calls <= 1, viaInterface.HasFrameBox, (scripted.HasFrameBox, scripted.SwitchedAtCreation, askedByTheScript)));
    }

    /// <summary>A host with no asset manager: it answers nothing to the resolver and counts the questions.</summary>
    private sealed class NoAssetsHost : IAlundraScriptHost
    {
        public int Calls;
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;

        Animation2dData? IAlundraScriptHost.ResolveAnimationData(IReadOnlyList<Guid> animationAssetIds, uint animationId, int direction)
        {
            Calls++;
            return null;
        }
    }

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }
}
