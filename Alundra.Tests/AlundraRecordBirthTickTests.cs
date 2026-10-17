#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// The oracle of E20.e0a4 phase B (docs/plan-e20-heros.md section 1.16.4, <c>docs/plan-e20-annexe/e20-e0a4/oracle/out/rec_tick.json</c>) read BY KEY: the box the binary holds at the end of every tick of an entity born from a record
/// by a script (row <c>REC-ME</c>, <c>REC-ME-8A</c>: births in the pass of the map events; keys <c>ticks[k].passes.hits</c>: <c>hbx - px</c>, <c>hby - py</c>, <c>hbz - pz</c>, <c>bw</c>, <c>bd</c>, <c>bh</c>).
/// </summary>
internal static class RecordBirthTickOracle
{
    private static readonly Lazy<JsonDocument> File_ = new(() => JsonDocument.Parse(File.ReadAllText(BirthClockOracle.Find("docs", "plan-e20-annexe", "e20-e0a4", "oracle", "out", "rec_tick.json"))));

    private static JsonElement Row(string label) => File_.Value.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("label").GetString() == label);

    /// <summary>The box at the hit pass of the tick S + <paramref name="k"/>: the offset from the entity and the sizes, raw 16.16 (the sizes are <c>(n &lt;&lt; 16) - 1</c>).</summary>
    internal static (long Dx, long Dy, long Dz, long W, long D, long H) BoxAtHit(string label, int k)
    {
        var t = Row(label).GetProperty("ticks").EnumerateArray().First(x => x.GetProperty("k").GetInt32() == k).GetProperty("passes").GetProperty("hits");
        return (t.GetProperty("hbx").GetInt64() - t.GetProperty("px").GetInt64(), t.GetProperty("hby").GetInt64() - t.GetProperty("py").GetInt64(), t.GetProperty("hbz").GetInt64() - t.GetProperty("pz").GetInt64(),
            t.GetProperty("bw").GetInt64(), t.GetProperty("bd").GetInt64(), t.GetProperty("bh").GetInt64());
    }
}

/// <summary>
/// E20.e0a4 phase B (T2, docs/plan-e20-heros.md section 1.16.4, D-E0A4-2, D-E0A4-3, D-E0A4-5): an entity born from a record by a script (the real opcodes <c>0x2D</c>, <c>0x8A</c> and <c>0x8B</c> through the real runner) takes part in the tick that
/// sees it born (<c>JoinsBirthTick</c>, posed LAST by the opcode): born in the pass of the map events it turns from the tick S, born in the programs of another entity (the loop 2) from S+1, in the same frame when the frame has
/// another tick. The real world of map 14 (<see cref="ArcRun"/>, real prefabs); the subject is the record of the global sprite 27 added by <see cref="AlundraRecordBirthClockTests.RegisterRecord"/>; the births are made at the
/// pass <c>mapEvents</c> (just before the map events of the tick, as a map-event program does) or <c>programs</c> (head of the loop of the programs, as a program of another entity does) of
/// <see cref="AlundraWorldProxy.TickTraceForTests"/>. Labels: [R] red on the code of phase A (predicted), [V] green by construction.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraRecordBirthTickTests : IDisposable
{
    private const int TestRecord = AlundraRecordBirthClockTests.TestRecord;

    public AlundraRecordBirthTickTests()
    {
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
    }

    public void Dispose() => AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The real opcodes, through the real runner of the world, the hero being the entity that runs the program.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Runs the program <paramref name="codes"/> (ending in 0xFF) and returns the entity it made (null when none).</summary>
    internal static AlundraEntityScriptProxy? RunProgram(ArcRun arc, int mapIndex, params int[] codes)
    {
        var before = arc.Entities.ToList();
        var document = new EventProgramDocument { MapIndex = mapIndex, EventCodesATable = new[] { 0, 0 }, Codes = codes };
        var runner = new AlundraEventProgramRunner(document, ArcRun.State, (IEntityWorldContext)arc.Proxy);
        runner.RunOneScriptCall(arc.Hero, new EventProgramState { Codes = document.CodesAsBytes() });
        return arc.Entities.Except(before).SingleOrDefault();
    }

    internal static AlundraEntityScriptProxy? Op2D(ArcRun arc, int record, int mapIndex = 14) => RunProgram(arc, mapIndex, 0x2D, record, 0xFF);

    /// <summary>0x8A [record, X, Y, Z] in pixels (two bytes each, low then high).</summary>
    internal static AlundraEntityScriptProxy? Op8A(ArcRun arc, int record, int x, int y, int z)
        => RunProgram(arc, 14, 0x8A, record, x & 0xFF, x >> 8, y & 0xFF, y >> 8, z & 0xFF, z >> 8, 0xFF);

    /// <summary>0x8B [search type, record, dx, dy, dz] in pixels (two bytes each, low then high): the new entity at the first match plus the offset.</summary>
    internal static AlundraEntityScriptProxy? Op8B(ArcRun arc, int search, int record, int dx, int dy, int dz)
        => RunProgram(arc, 14, 0x8B, search, record, dx & 0xFF, dx >> 8, dy & 0xFF, dy >> 8, dz & 0xFF, dz >> 8, 0xFF);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The montage: one birth, sampled at the head of every pass of every tick from the birth on.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool Contains(ArcRun arc, string field, AlundraEntityScriptProxy proxy)
        => ((IEnumerable)typeof(AlundraWorldProxy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!).Cast<object>().Any(o => ReferenceEquals(o, proxy));

    /// <summary>What the newborn shows at the head of a pass.</summary>
    internal sealed record Snap(int Frame, EntityStatus Status, int EventTrigger, int FrameCounter, bool InUpdateList, bool InHitList, int Cursor, int Flag, bool Integrated, int ARuns, int Reads,
        (long Dx, long Dy, long Dz, long W, long D, long H)? Box, int PosX, int PosY, int PosZ);

    /// <summary>Counts the runs of the program A of the entities of the sprite <see cref="Sprite"/>, then passes the call on.</summary>
    private sealed class CountingRunner : IEventProgramRunner
    {
        private readonly IEventProgramRunner _inner;
        private readonly int _sprite;
        public int Runs;

        public CountingRunner(IEventProgramRunner inner, int sprite)
        {
            _inner = inner;
            _sprite = sprite;
        }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (entity.SpriteType == _sprite && programSlot == ScriptHelper.ProgramALoad)
            {
                Runs++;
            }

            _inner.RunScript(entity, programSlot);
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
            if (entity.SpriteType == _sprite && entity.EventTrigger == ScriptHelper.ProgramALoad)
            {
                Runs++;
            }

            _inner.RunSpriteEvent(entity);
        }
    }

    internal sealed class ScriptBirth : IDisposable
    {
        public readonly ArcRun Arc;
        public readonly string BirthPass;
        private readonly Func<ArcRun, AlundraEntityScriptProxy?> _make;
        private readonly CountingRunner _counter;
        public AlundraEntityScriptProxy? Born;
        public int Tick;
        public int S = int.MaxValue;
        public int Reads;
        public int ReadsDuringTheBirth = -1;
        public Snap? AtTheBirth;
        public readonly Dictionary<int, int> FrameOfTick = new();
        public readonly List<(int K, string Pass, Snap State)> Samples = new();

        public ScriptBirth(ArcRun arc, string birthPass, Func<ArcRun, AlundraEntityScriptProxy?> make, int sprite = 27)
        {
            Arc = arc;
            BirthPass = birthPass;
            _make = make;
            _counter = new CountingRunner(arc.Proxy.EventProgramRunner, sprite);
            arc.Proxy.EventProgramRunner = _counter;
            arc.Proxy.CollisionFieldFallbackReadForTests = () => Reads++;
            arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "pad")
                {
                    Tick++;
                    FrameOfTick[Tick] = arc.Frame;
                }

                if (pass == BirthPass && Tick == S && Born == null)
                {
                    var reads = Reads;
                    Born = _make(arc);
                    ReadsDuringTheBirth = Reads - reads;
                    if (Born != null)
                    {
                        AtTheBirth = Snapshot();
                    }
                }

                if (Born != null)
                {
                    Samples.Add((Tick - S, pass, Snapshot()));
                }
            };
        }

        /// <summary>The scripted record of the global sprite 27 added to the table of the world, born by <paramref name="make"/> at the pass <paramref name="birthPass"/>.</summary>
        public static ScriptBirth OfTheTestRecord(string name, string birthPass, Func<ArcRun, AlundraEntityScriptProxy?> make)
        {
            var arc = AlundraCreationSwitchClockTests.Arc(name);
            AlundraRecordBirthClockTests.RegisterRecord(arc, TestRecord, 27);
            return new ScriptBirth(arc, birthPass, make);
        }

        private Snap Snapshot()
        {
            var b = Born!;
            return new Snap(Arc.Frame, b.Status, b.EventTrigger, b.FrameCounter, Contains(Arc, "_updateProxies", b), Contains(Arc, "_hitList", b), b.BoxTicks, b.ForceResetAnimationFlag,
                ArcRun.EntityOf(b)!.World != null, _counter.Runs, Reads, AlundraRecordBirthClockTests.DllBox(b), b.PosX, b.PosY, b.PosZ);
        }

        public Snap At(int k, string pass) => Samples.Last(s => s.K == k && s.Pass == pass).State;

        /// <summary>The first tick (relative to S) at which the end-of-animation flag is raised at the hit point, or -1.</summary>
        public int FirstFlag() => Samples.Where(s => s.Pass == "hit" && s.State.Flag != 0).Select(s => s.K).DefaultIfEmpty(-1).First();

        /// <summary>One tick settles the world (the first frame has a tick of its own); S is then the <paramref name="position"/>-th tick (1-based) of the next frame of the grouping.</summary>
        public void Settle(int position)
        {
            AlundraCreationSwitchClockTests.Frame(Arc, 1);
            S = Tick + position;
        }

        public void RunAll(int ticksPerFrame, int position, int last)
        {
            Settle(position);
            while (Tick < S + last)
            {
                AlundraCreationSwitchClockTests.Frame(Arc, ticksPerFrame);
            }
        }

        public void Dispose()
        {
            Arc.Proxy.TickTraceForTests = null;
            Arc.Proxy.CollisionFieldFallbackReadForTests = null;
            Arc.Dispose();
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b1) Born by a program of the map events (REC-ME): at the tick S the entity is Normal after the programs, its program A ran once (the trigger is back to -1), +0x1C is 1 at the lists pass, it is in the update
    // and hit lists, it has not been integrated by the engine, the cursor of its box is the call number of the binary (1, 2, 3, 4 at S .. S+3), its box is the one of the binary at every tick, the end flag comes at S+25.
    // At 1, 2 and 3 ticks per frame, born at every position of the frame.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    public void B1_R_BornByAProgramOfTheMapEvents_TakesPartInTheTickOfItsBirth_ProgramListsClockPhysicsHits_AtEveryGroupingAndPosition(int ticksPerFrame, int position)
    {
        using var run = ScriptBirth.OfTheTestRecord($"b1-{ticksPerFrame}-{position}", "mapEvents", arc => Op2D(arc, TestRecord));
        run.RunAll(ticksPerFrame, position, 30);
        Assert.NotNull(run.Born);

        var after = run.At(0, "pending");
        var cursors = Enumerable.Range(0, 4).Select(k => run.At(k, "hit").Cursor).ToArray();
        var boxes = Enumerable.Range(0, 10).Select(k => run.At(k, "hit").Box).ToArray();
        Assert.Equal(
            (EntityStatus.Loaded, EntityStatus.Normal, -1, 1, 0, 1, true, true, false,
                string.Join(",", Enumerable.Range(0, 4).Select(k => RecordBirthOracle.CallNo("REC-ME", k))), RecordBirthOracle.FirstEnd("REC-ME"),
                string.Join(",", Enumerable.Range(0, 10).Select(k => RecordBirthTickOracle.BoxAtHit("REC-ME", k)))),
            (run.At(0, "programs").Status, after.Status, after.EventTrigger, after.ARuns, run.At(0, "counters").FrameCounter, run.At(0, "lists").FrameCounter, run.At(0, "clock").InUpdateList && run.At(0, "clock").InHitList,
                run.At(0, "clock").InHitList, run.At(0, "hit").Integrated, string.Join(",", cursors), run.FirstFlag(), string.Join(",", boxes)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b2) Born in the programs of another entity (the loop 2) at the FIRST tick of a frame of 2 or 3 ticks (and at the second of 3): the entity turns from S+1, in the SAME frame (the frame has another tick), its cursor is the
    // call number of the binary (REC-H2: 1, 2, 3 at S+1, S+2, S+3), its end flag comes at S+26.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void B2_R_BornInTheProgramsOfAnotherEntity_TurnsFromSPlus1InTheSameFrame_TheFirstClockTickCounts_TheEndFlagComesAtSPlus26(int ticksPerFrame, int position)
    {
        using var run = ScriptBirth.OfTheTestRecord($"b2-{ticksPerFrame}-{position}", "programs", arc => Op2D(arc, TestRecord));
        run.RunAll(ticksPerFrame, position, 34);
        Assert.NotNull(run.Born);

        var sameFrame = run.FrameOfTick[run.S + 1] == run.FrameOfTick[run.S];
        var cursors = Enumerable.Range(1, 3).Select(k => run.At(k, "hit").Cursor);
        Assert.Equal(
            (true, EntityStatus.Normal, string.Join(",", Enumerable.Range(1, 3).Select(k => RecordBirthOracle.CallNo("REC-H2", k))), RecordBirthOracle.FirstEnd("REC-H2")),
            (sameFrame, run.At(1, "pending").Status, string.Join(",", cursors), run.FirstFlag()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [V] (b3) The box of an entity born by a program of the map events exists at S and at every tick that follows, before and after the engine integrated it (the blind spot of the marker alone: the timeline frozen at null
    // by the creation is reloaded then frozen again by the physics of S before the integration, and the entity would have no box until the next switch): green on the code of phase A, it kills the marker without
    // SwitchedAtCreation. The box at S is the one of the creation of the binary.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void B3_V_TheBoxOfABirthByTheMapEvents_ExistsAtSAndAfterTheIntegration_AndAtSItIsTheBoxOfTheCreationOfTheBinary(int ticksPerFrame, int position)
    {
        using var run = ScriptBirth.OfTheTestRecord($"b3-{ticksPerFrame}-{position}", "mapEvents", arc => Op2D(arc, TestRecord));
        run.RunAll(ticksPerFrame, position, 8);
        Assert.NotNull(run.Born);

        var hasBox = Enumerable.Range(0, 9).Select(k => run.At(k, "hit").Box != null).ToArray();
        var integratedAt = Enumerable.Range(0, 9).Where(k => run.At(k, "hit").Integrated).DefaultIfEmpty(-1).First();
        (long Dx, long Dy, long Dz, long W, long D, long H)? creationBox = RecordBirthOracle.CreationBox("REC-ME");
        Assert.Equal(
            (string.Join(",", Enumerable.Repeat(true, 9)), creationBox, true),
            (string.Join(",", hasBox), run.AtTheBirth!.Box, integratedAt > 0));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b4) 0x8A by a program of the map events (REC-ME-8A), at a site where the written position is UNDER the terrain (X 204, Y 152, Z 4 px; the ground of the cell is 144 px): the position written is not raised to the
    // ground at the creation (the marker is posed after the push of the opcode, which reads no field by the fallback: the binary has no floor at 0x8A, ADR-0026); at S the entity turns (Normal after the programs), the host's
    // field is read during the tick of the birth, and its box at the hit point is the one of the binary after the correction by the physics (box_table, REC-ME-8A, k = 0). Red on the code of phase A: the entity turns one tick later.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const int SiteX = 204;
    private const int SiteY = 152;
    private const int SiteZ = 4;

    [Fact]
    public void B4_R_0x8AByAProgramOfTheMapEvents_TheWrittenPositionUnderTheTerrainIsNotRaisedAtTheCreation_TheEntityTurnsAtS_AndItsBoxIsTheOneOfTheBinary()
    {
        using var run = ScriptBirth.OfTheTestRecord("b4", "mapEvents", arc => Op8A(arc, TestRecord, SiteX, SiteY, SiteZ));
        run.RunAll(1, 1, 3);
        Assert.NotNull(run.Born);

        var atS = run.At(0, "hit");
        var readsAtS = atS.Reads - (run.AtTheBirth!.Reads);
        (long Dx, long Dy, long Dz, long W, long D, long H)? boxOfTheBinary = RecordBirthTickOracle.BoxAtHit("REC-ME-8A", 0);
        Assert.Equal(
            (SiteZ << 16, 0, EntityStatus.Normal, true, boxOfTheBinary),
            (run.AtTheBirth.PosZ, run.ReadsDuringTheBirth, run.At(0, "pending").Status, readsAtS > 0, atS.Box));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b4b) 0x8B by a program of the map events, with a target found (the player, at (204, 104, 0); the offset puts the entity under the terrain, at (204, 152, 4)) and then without a target: the entity turns at S in both
    // cases; with a target the written position is not raised to the ground at the creation and the host's field is not read by the opcode; without a target the entity stays where its record puts it (132, 88, 16 px).
    // Red on the code of phase A: the entity turns one tick later.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void B4b_R_0x8BByAProgramOfTheMapEvents_TurnsAtS_WithAndWithoutATarget_AndTheWrittenPositionIsNotRaisedAtTheCreation(bool targetFound)
    {
        var search = targetFound ? 0x81 : 77; // 0x81: the player; 77: the entities of the record 77, there are none.
        using var run = ScriptBirth.OfTheTestRecord($"b4b-{targetFound}", "mapEvents", arc => Op8B(arc, search, TestRecord, SiteX - 204, SiteY - 104, SiteZ));
        run.RunAll(1, 1, 3);
        Assert.NotNull(run.Born);

        var expectedPosition = targetFound ? (SiteX << 16, SiteY << 16, SiteZ << 16) : (132 << 16, 88 << 16, 16 << 16);
        Assert.Equal(
            (expectedPosition, 0, EntityStatus.Normal),
            ((run.AtTheBirth!.PosX, run.AtTheBirth.PosY, run.AtTheBirth.PosZ), run.ReadsDuringTheBirth, run.At(0, "pending").Status));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [V] (b5) The known deviation D-E0A4-5 ("A deux fois", D-E20-42): the binary plays the program A of an entity born in the loop 2 twice when no slot is free under the highest one (REC-H1: once at S with the
    // status 1, once at S+1); the port plays it ONCE, at S+1, and leaves the trigger at -1. One tick per frame. Named after the deviation; kept by the decision of the author.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B5_V_ADeuxFois_KnownDeviationD_E0A4_5_ABirthInTheLoop2PlaysTheProgramAOnceAtSPlus1_NeverAtS()
    {
        using var run = ScriptBirth.OfTheTestRecord("b5", "programs", arc => Op2D(arc, TestRecord));
        run.RunAll(1, 1, 6);
        Assert.NotNull(run.Born);

        Assert.Equal(
            (-1, 0, 1, 1),
            (run.AtTheBirth!.EventTrigger, run.At(0, "hit").ARuns, run.At(1, "pending").ARuns, run.At(6, "hit").ARuns));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (b6) The key ~0u of AlundraScriptedMotion (the pin P3 that e0a3b reported): an entity born by a script, marked, whose animation 0 has a non-zero speed, takes the speed of its TARGET animation at its first tick of
    // physics, while its current animation is still ~0 (not validated): the real record 1 of map 321 (sprite 469, speed 160). The code of phase A (entity not marked) reads the animation ~0 at its first tick, which
    // has no animation set: speed 0.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B6_R_TheKeyOfTheKinematics_AnEntityBornByAScript_AtItsFirstTickOfPhysics_TakesTheSpeedOfItsTargetAnimation_WhileItsCurrentOneIsNotValidated()
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var worldDir = Directory.GetDirectories(Path.Combine(root, "Maps")).SelectMany(Directory.GetDirectories)
            .First(d => Path.GetFileName(d).EndsWith("-321", StringComparison.Ordinal));
        var arc = new ArcRun(new ArcSpec("E20e0a4-b6-321", Path.GetFileName(Path.GetDirectoryName(worldDir)!), Path.GetFileName(worldDir), Array.Empty<int>(), 8, 6, 1, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((8 * 24 + 12) << 16, (6 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0)));
        using var run = new ScriptBirth(arc, "mapEvents", a => Op2D(a, 1, 321));
        (bool Pending, int Speed, int TargetForceX, int TargetForceY)? firstTick = null;
        var previousTrace = arc.Proxy.TickTraceForTests;
        arc.Proxy.TickTraceForTests = pass =>
        {
            previousTrace?.Invoke(pass);
            if (pass == "hit" && run.Born is { MotionTickCount: > 0 } born && firstTick == null)
            {
                firstTick = (born.CurrentAnimationId == ~0u, born.Speed, born.TargetForceX, born.TargetForceY);
            }
        };

        run.Settle(1);
        while (firstTick == null && run.Tick < run.S + 6)
        {
            AlundraCreationSwitchClockTests.Frame(arc, 1);
        }

        Assert.NotNull(run.Born);
        Assert.NotNull(firstTick);
        var born = run.Born!;
        var targetSpeed = born.AnimSetsByAnim!.TryGetValue((int)born.TargetAnimationId, out var set) ? set.Speed : 0;
        var direction = (int)born.TargetDirection;
        Assert.True(targetSpeed > 0, "the subject must have a non-zero speed on its target animation");
        Assert.Equal(
            (true, targetSpeed, AnimationTables.OffsetXList[direction] * targetSpeed, AnimationTables.OffsetYList[direction] * targetSpeed),
            (firstTick!.Value.Pending, firstTick.Value.Speed, firstTick.Value.TargetForceX, firstTick.Value.TargetForceY));
    }
}
