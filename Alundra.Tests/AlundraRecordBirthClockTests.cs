#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// The oracle of E20.e0a4 (docs/plan-e20-heros.md section 1.16.4, <c>docs/plan-e20-annexe/e20-e0a4/oracle/out/</c>) read BY KEY, never pasted: what an entity born from a RECORD by a script (the real record spawner
/// <c>0x8003A1B8</c>) does after its birth tick S. <c>rec_birth_clock.json</c> (rows <c>REC-H2</c>, <c>REC-SW*</c>: births in the programs pass of another entity; keys <c>call_no</c>, <c>end_AC</c>, <c>k</c> = tick - S) and
/// <c>rec_tick.json</c> (row <c>REC-H1</c>, <c>creation_end</c>: the box and the byte <c>animflags_B4</c> at the end of the creation).
/// </summary>
internal static class RecordBirthOracle
{
    private static JsonElement Row(string file, string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BirthClockOracle.Find("docs", "plan-e20-annexe", "e20-e0a4", "oracle", "out", file)));
        return document.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("label").GetString() == label).Clone();
    }

    private static IEnumerable<JsonElement> Ticks(string label) => Row("rec_birth_clock.json", label).GetProperty("ticks").EnumerateArray();

    /// <summary>The first tick (relative to S) at which the end-of-animation flag of the row is raised.</summary>
    internal static int FirstEnd(string label) => Ticks(label).First(t => t.GetProperty("end_AC").GetInt32() == 1).GetProperty("k").GetInt32();

    /// <summary>The number of the call of the animation pass (the cursor of the box of the DLL, <c>BoxTicks</c>) at S + <paramref name="k"/>: 0 before the first call.</summary>
    internal static int CallNo(string label, int k) => Ticks(label).First(t => t.GetProperty("k").GetInt32() == k).GetProperty("call_no").GetInt32();

    /// <summary>The box at the end of the creation (<c>0x80039F30</c>): the offset from the entity and the sizes, raw 16.16 (the sizes are <c>(n &lt;&lt; 16) - 1</c>), or null when the oracle says nothing.</summary>
    internal static (long Dx, long Dy, long Dz, long W, long D, long H) CreationBox(string label)
    {
        var t = Row("rec_tick.json", label).GetProperty("creation_end");
        return (t.GetProperty("hbx").GetInt64() - t.GetProperty("px").GetInt64(), t.GetProperty("hby").GetInt64() - t.GetProperty("py").GetInt64(), t.GetProperty("hbz").GetInt64() - t.GetProperty("pz").GetInt64(),
            t.GetProperty("bw").GetInt64(), t.GetProperty("bd").GetInt64(), t.GetProperty("bh").GetInt64());
    }

    /// <summary>The byte <c>+0xB4</c> (<c>AnimFlags</c>) the binary holds at the end of the creation.</summary>
    internal static int CreationAnimFlags(string label) => (int)Row("rec_tick.json", label).GetProperty("creation_end").GetProperty("animflags_B4").GetInt64();
}

/// <summary>
/// E20.e0a4 phase A (T1, docs/plan-e20-heros.md section 1.16.4, D-E0A4-1, D-E0A4-2, D-E0A4-4): an entity born from a record by a script (the opcode <c>0x2D</c> through the real runner, <c>SpawnEntityByRecordId</c>) has, from its
/// creation, the first switch of the animation 0 that the binary makes (<c>SwitchedAtCreation</c>: the first clock tick counts, the box exists before the engine integrated the entity) and the <c>AnimFlags</c> of that animation.
/// The real world of map 14 (<see cref="ArcRun"/>, real prefabs). The subject is a test record of the global sprite 27 (the wave of the sword: no program, an animation of 25 ticks), added to the record table of the proxy; the
/// births are made at the pass <c>programs</c> of <see cref="AlundraWorldProxy.TickTraceForTests"/> (inside the entity block: a birth in the programs of another entity, the loop 2 of the binary, at one tick per frame). Labels: [R] red on the HEAD
/// (predicted), [V] green by construction, [C] control (its colour on the HEAD is recorded, no stop value; not part of any proof).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraRecordBirthClockTests : IDisposable
{
    /// <summary>The index of the test record (a byte: the operand of <c>0x2D</c>); the records of the world of map 14 are 0 to 24.</summary>
    internal const int TestRecord = 200;

    public AlundraRecordBirthClockTests()
    {
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
    }

    public void Dispose() => AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

    /// <summary>Adds to the record table of <paramref name="arc"/> a record of the global sprite <paramref name="sprite"/> at the place of the oracle (132, 88, 16 px), facing <paramref name="spriteDirection"/> (3: the direction word 0x18).</summary>
    internal static void RegisterRecord(ArcRun arc, int id, int sprite, int spriteDirection = 3)
    {
        Assert.True(arc.Proxy.SpriteRecordCatalog.TryGetPrefab(false, sprite, out var prefab), $"no prefab for the global sprite {sprite}");
        var record = new TileMapObjectData { Id = id, Name = $"Entity_{id}" };
        record.CustomProperties["Index"] = id.ToString();
        record.CustomProperties["SpriteTableIndex"] = sprite.ToString();
        record.CustomProperties["SpriteDirection"] = spriteDirection.ToString();
        record.CustomProperties["XPos"] = "10";   // (10 * 12 + 12) = 132 px
        record.CustomProperties["YPos"] = "10";   // (10 * 8 + 8) = 88 px
        record.CustomProperties["Height"] = "2";  // 2 << 19 = 16 px
        record.CustomProperties["PrefabAssetId"] = prefab.ToString();
        ((Dictionary<int, TileMapObjectData>)typeof(AlundraWorldProxy).GetField("_entityRecordsByIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!)[id] = record;
    }

    /// <summary>Runs the real opcode <c>0x2D [record]</c> through the real runner of the world, the hero being the entity that runs the program; returns the entity it made (null when the spawn failed).</summary>
    internal static AlundraEntityScriptProxy? Op2D(ArcRun arc, int record)
    {
        var document = new EventProgramDocument { MapIndex = 14, EventCodesATable = new[] { 0, 0 }, Codes = new[] { 0x2D, record, 0xFF } };
        var runner = new AlundraEventProgramRunner(document, ArcRun.State, (IEntityWorldContext)arc.Proxy);
        runner.RunOneScriptCall(arc.Hero, new EventProgramState { Codes = document.CodesAsBytes() });
        return arc.EntityByRecord(record);
    }

    /// <summary>The box of the DLL as the oracle's offsets and sizes, or null when the entity has none.</summary>
    internal static (long Dx, long Dy, long Dz, long W, long D, long H)? DllBox(AlundraEntityScriptProxy born)
        => born.HasFrameBox
            ? ((long)born.HitBoxX - born.PosX, (long)born.HitBoxY - born.PosY, (long)born.HitBoxZ - born.PosZ, born.CollisionWidth, born.CollisionDepth, born.CollisionHeight)
            : null;

    /// <summary>
    /// One birth by <c>0x2D</c> in the real world: the entity of the test record born at the tick <see cref="S"/> at the pass <see cref="BirthPass"/>, sampled at the hit point of every tick from the birth on (one tick per frame).
    /// </summary>
    internal sealed class RecordBirth : IDisposable
    {
        public readonly ArcRun Arc;
        public readonly string BirthPass;
        public AlundraEntityScriptProxy? Born;
        public int Tick;
        public int S = int.MaxValue;
        public readonly SortedDictionary<int, AlundraCreationSwitchClockTests.Sample> After = new();

        /// <summary>Told at the head of the lists pass of every tick from the birth on (before the clock), with the entity and the tick relative to S: the scripts that ask for another animation or direction.</summary>
        public Action<AlundraEntityScriptProxy, int>? AtLists;

        /// <summary>Told right after the birth, with the entity (the test lowers the flag there).</summary>
        public Action<AlundraEntityScriptProxy>? AtBirth;

        public RecordBirth(string name, string birthPass = "programs", int sprite = 27)
        {
            Arc = AlundraCreationSwitchClockTests.Arc(name);
            BirthPass = birthPass;
            RegisterRecord(Arc, TestRecord, sprite);
            Arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "pad")
                {
                    Tick++;
                }

                if (pass == BirthPass && Tick == S && Born == null)
                {
                    Born = Op2D(Arc, TestRecord);
                    if (Born != null)
                    {
                        AtBirth?.Invoke(Born);
                    }
                }

                if (Born == null)
                {
                    return;
                }

                if (pass == "lists")
                {
                    AtLists?.Invoke(Born, Tick - S);
                }
                else if (pass == "hit")
                {
                    After[Tick - S] = new AlundraCreationSwitchClockTests.Sample(Born.BoxTicks, Born.ForceResetAnimationFlag, Born.AnimCompleteCounter, Born.Status, Born.AnimationSwitchTickOwed,
                        Born.AnimationSwitchTicksReserved, Born.CurrentAnimationId, Born.TargetAnimationId, Born.IsZForceApplied);
                }
            };
        }

        /// <summary>One tick settles the world (the first frame has a tick of its own); S is then the first tick of the next frame (one tick per frame).</summary>
        public void Settle()
        {
            AlundraCreationSwitchClockTests.Frame(Arc, 1);
            S = Tick + 1;
        }

        /// <summary>Runs frames of one tick until the tick S + <paramref name="last"/> has run.</summary>
        public void Run(int last)
        {
            while (Tick < S + last)
            {
                AlundraCreationSwitchClockTests.Frame(Arc, 1);
            }
        }

        public void RunAll(int last)
        {
            Settle();
            Run(last);
        }

        /// <summary>The first tick (relative to S) at which the end-of-animation flag is raised, or -1.</summary>
        public int FirstFlag() => After.Where(a => a.Value.Flag != 0).Select(a => a.Key).DefaultIfEmpty(-1).First();

        public void Dispose()
        {
            Arc.Proxy.TickTraceForTests = null;
            Arc.Dispose();
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a1) The box of the creation (0x80039F30): an entity born by 0x2D has its box right after the creation, before the engine integrated it (no world, no animation in its sprite): the offsets and the sizes of the
    // oracle (REC-H1, creation_end: (-18, -12, 0) px, 36 x 24 x 31).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A1_R_TheBoxOfTheCreation_OfAnEntityBornBy0x2D_IsTheOneOfTheBinary_BeforeTheIntegration()
    {
        using var arc = AlundraCreationSwitchClockTests.Arc("rec-a1");
        RegisterRecord(arc, TestRecord, 27);
        var born = Op2D(arc, TestRecord);
        Assert.NotNull(born);
        var entity = ArcRun.EntityOf(born!)!;
        var animations = entity.GetComponent<AnimatedSpriteComponent>()!.Animations.Count;
        Assert.Equal((RecordBirthOracle.CreationBox("REC-H1"), 0, true), (DllBox(born!), animations, entity.World == null));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a2) Born in the programs pass of another entity (the loop 2), at one tick per frame: the first tick of the clock is S+1 and it counts: the cursor of the box is the call number of the oracle (1, 2, 3 at S+1, S+2,
    // S+3); the end flag comes at S+26 (REC-H2, end_AC). In the same assertion, the same birth with the flag lowered right after the creation (the HEAD): S+27.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A2_R_BornInTheProgramsOfAnotherEntity_TheFirstClockTickCounts_TheCursorIs1Then2Then3_TheEndFlagComesAtSPlus26_AndOneTickLaterWithoutTheFlag()
    {
        using var counted = new RecordBirth("rec-a2");
        counted.RunAll(34);
        Assert.NotNull(counted.Born);

        using var lowered = new RecordBirth("rec-a2-low");
        lowered.AtBirth = born => born.SwitchedAtCreation = false;
        lowered.RunAll(34);
        Assert.NotNull(lowered.Born);

        var end = RecordBirthOracle.FirstEnd("REC-H2");
        var expectedCursors = string.Join(",", Enumerable.Range(1, 3).Select(k => RecordBirthOracle.CallNo("REC-H2", k)));
        Assert.Equal(
            (expectedCursors, end, end + 1),
            (string.Join(",", Enumerable.Range(1, 3).Select(k => counted.After[k].Cursor)), counted.FirstFlag(), lowered.FirstFlag()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [C] (a3) Controls: a switch asked inside the window of the first tick (before the clock of the tick S+k), one tick per frame; the first end flag of the rows REC-SW0, -SW1, -SW2, -SW5, -SW1-2, -SWD1, -SWD2. The logic of
    // the window is the one of E20.e0a5, already guarded by its tests and its mutants: these cases only show that a record birth in the programs pass obeys it. Their colour on the HEAD is recorded without stop value and they
    // are in no proof.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (int K, uint Animation, int? Direction)[] SwitchesOf(string label)
        => label switch
        {
            "REC-SW0" => new[] { (0, 1u, (int?)null) },
            "REC-SW1" => new[] { (1, 1u, (int?)null) },
            "REC-SW2" => new[] { (2, 1u, (int?)null) },
            "REC-SW5" => new[] { (5, 1u, (int?)null) },
            "REC-SW1-2" => new[] { (1, 1u, (int?)null), (2, 0u, (int?)null) },
            "REC-SWD1" => new[] { (1, 0u, (int?)16) },
            "REC-SWD2" => new[] { (2, 0u, (int?)16) },
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, null),
        };

    [Theory]
    [InlineData("REC-SW0")]
    [InlineData("REC-SW1")]
    [InlineData("REC-SW2")]
    [InlineData("REC-SW5")]
    [InlineData("REC-SW1-2")]
    [InlineData("REC-SWD1")]
    [InlineData("REC-SWD2")]
    public void A3_C_BornInTheProgramsOfAnotherEntity_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt_AtOneTickPerFrame(string label)
    {
        var switches = SwitchesOf(label);
        using var run = new RecordBirth($"rec-a3-{label}");
        run.AtLists = (born, k) =>
        {
            foreach (var (at, animation, direction) in switches)
            {
                if (k == at)
                {
                    born.TargetAnimationId = animation;
                    if (direction is { } d)
                    {
                        born.TargetDirection = (uint)d;
                    }
                }
            }
        };
        run.RunAll(38);
        Assert.NotNull(run.Born);
        Assert.Equal(RecordBirthOracle.FirstEnd(label), run.FirstFlag());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a4) AnimFlags: the byte 0xD of the animation set of the creation animation, loaded at the creation (0x80039EA8) for every entity: the sprite 27 (193, the oracle's animflags_B4 of the end of the creation) and the
    // real record 3 of map 36 (sprite 313, the byte of its animation 0 is 0x40, born by 0x2D in the real site s36c4_rec3). The HEAD leaves 0.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A4_R_AnimFlagsOfTheCreationAnimation_IsLoadedAtTheCreation_ForTheSprite27AndForTheRealRecord3OfMap36()
    {
        (int AnimFlags, bool Flag, bool CloneFlag) OfTheTestRecord()
        {
            using var arc = AlundraCreationSwitchClockTests.Arc("rec-a4");
            RegisterRecord(arc, TestRecord, 27);
            var born = Op2D(arc, TestRecord)!;
            return (born.AnimFlags, born.SwitchedAtCreation, ((AlundraEntityScriptProxy)born.Clone()).SwitchedAtCreation); // a clone is never a birth: it does not carry the flag.
        }

        int OfTheRealRecord()
        {
            var root = SaveGameDirectorTestSupport.FindProjectRoot();
            var worldDir = Directory.GetDirectories(Path.Combine(root, "Maps")).SelectMany(Directory.GetDirectories)
                .First(d => Path.GetFileName(d).EndsWith("-36", StringComparison.Ordinal));
            using var arc = new ArcRun(new ArcSpec("E20e0a4-a4-36", Path.GetFileName(Path.GetDirectoryName(worldDir)!), Path.GetFileName(worldDir), Array.Empty<int>(), 8, 6, 1, 100, RealController: true, Prefabs: true,
                Arrival: new ArcArrival((8 * 24 + 12) << 16, (6 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0)));
            var born = Op2D(arc, 3);
            Assert.NotNull(born);
            Assert.Equal(313, born!.SpriteType);
            return born.AnimFlags;
        }

        Assert.Equal(((RecordBirthOracle.CreationAnimFlags("REC-H1"), true, false), 0x40), (OfTheTestRecord(), OfTheRealRecord()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [V] (a5) Zero change outside the births by script: the entities of the map load and the hero carry neither the flag nor a loaded AnimFlags (the deviation of E20.e0a5, Q-E0A5-1 and D-E0A5-5, is pinned on an entity of
    // the map load by A6_V of AlundraCreationSwitchClockTests and B6_V of AlundraBirthBoxTests; the entity born by the interface and the hero are guarded by their own tests, the gull by
    // AlundraNpcCharacterControllerMoverTests). The positive side (a birth by script carries the flag, its clone does not) is in (a4).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A5_V_TheMapLoadAndTheHero_CarryNeitherTheFlagNorALoadedAnimFlags()
    {
        using var arc = AlundraCreationSwitchClockTests.Arc("rec-a5");
        var loaded = arc.Entities.Where(e => e.EntityRefId >= 0).ToList();
        Assert.NotEmpty(loaded);
        Assert.Equal((0, false, 0), (loaded.Count(e => e.SwitchedAtCreation || e.AnimFlags != 0), arc.Hero.SwitchedAtCreation, arc.Hero.AnimFlags));
    }
}
