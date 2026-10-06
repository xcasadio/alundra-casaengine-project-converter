#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.g G3d (docs/plan-e19-opcodes.md, section 1.2o.6, G3D-R1 to R4): the life vessel (opcode <c>0x82</c>, sub-case <c>0x53</c>) through the real interpreter and the effect stage,
/// against <c>docs/plan-e19-g3d-annexe/discovery/lib/g3d-golden.tsv</c> (rows <c>V</c>, <c>S</c>, <c>T</c>, <c>F</c>, <c>P</c>: produced by the real code of the binary in an interpreter), then the native
/// creators of modes 1 and 3 against <c>g3d-modes13.tsv</c>. Every test that draws from <see cref="AlundraRandom"/> lives here, in the collection that serialises its static state.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraNativeEffectTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly int[] VesselProgram = { 0x82, 0x53, 0x1A, 9, 0xFF };

    public AlundraNativeEffectTests()
    {
        AlundraRandom.Reset();
        ResetSingletons();
    }

    public void Dispose()
    {
        AlundraRandom.Reset();
        ResetSingletons();
    }

    private static void ResetSingletons()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The tables of the annex
    // ---------------------------------------------------------------------------------------------------------------------

    private static string FindAnnexFile(string folder, string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-g3d-annexe", "discovery", folder, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AlundraNativeEffectTests: no 'docs/plan-e19-g3d-annexe/discovery/{folder}/{name}' found above '{AppContext.BaseDirectory}'.");
    }

    private static string[][] ReadTable(string name) =>
        File.ReadAllLines(FindAnnexFile("lib", name)).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t')).ToArray();

    private static readonly string[][] Golden = ReadTable("g3d-golden.tsv");

    /// <summary>A row <c>V</c>: the inputs (columns 2 to 9) and the outputs of the real code (columns 11 to 18).</summary>
    private sealed record Scenario(
        string Case, uint Seed, int HeroX, int HeroY, int HeroZ, int Prefill, int[] Free, int Hp, int HpMax,
        int Result, int Size, string Sounds, int HpOut, int HpMaxOut, uint SeedOut, int Created, int Sparks);

    private static Scenario ScenarioOf(string[] r) => new(
        r[1], Convert.ToUInt32(r[2], 16), int.Parse(r[3]), int.Parse(r[4]), int.Parse(r[5]), int.Parse(r[6]),
        r[7] == "-" ? Array.Empty<int>() : r[7].Split(',').Select(int.Parse).ToArray(), int.Parse(r[8]), int.Parse(r[9]),
        int.Parse(r[11]), int.Parse(r[12]), r[13], int.Parse(r[14]), int.Parse(r[15]), Convert.ToUInt32(r[16], 16), int.Parse(r[17]), int.Parse(r[18]));

    private static IReadOnlyList<Scenario> Scenarios { get; } = Golden.Where(r => r[0] == "V").Select(ScenarioOf).ToList();

    public static IEnumerable<object[]> ScenarioCases() => Scenarios.Select(s => new object[] { s.Case });

    private static Scenario ScenarioNamed(string name) => Scenarios.Single(s => s.Case == name);

    // ---------------------------------------------------------------------------------------------------------------------
    // The mounting
    // ---------------------------------------------------------------------------------------------------------------------

    private sealed class RecordingSound : IAlundraSoundPlayer
    {
        public List<int> Played { get; } = new();
        public void PlaySfx(int sfxId) => Played.Add(sfxId);
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() { }
    }

    private sealed class Host : IEntityWorldContext
    {
        public AlundraEffectStage? Stage { get; set; }
        public RecordingSound? Sound { get; set; }
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public AlundraEffectStage? EffectStage => Stage;
        public IAlundraSoundPlayer? SoundPlayer => Sound;
    }

    /// <summary>The bank of map 476 (the global table of the effects holds the effect 14; the map is any one with a companion).</summary>
    private static AlundraEffectBank Bank() => EffectGolden.BankOf(Root, 476);

    private static AlundraEffectStage NewStage()
    {
        var stage = new AlundraEffectStage();
        stage.Load(Bank());
        return stage;
    }

    private static AlundraEntityScriptProxy HeroAt(int x, int y, int z) => new() { PosX = x, PosY = y, PosZ = z };

    private static EventProgramState Exec(Host host, AlundraGameState gameState, int[] code, List<EventTraceRecord>? trace = null)
    {
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new int[6], Codes = code };
        var runner = new AlundraEventProgramRunner(document, gameState, host);
        if (trace != null)
        {
            runner.TraceSink = trace.Add;
        }

        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, LogicContextEntity = new CasaEngine.Framework.Scene.Entities.Entity { Name = "logic" } };
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId); // 0x1A 9 ran in the same call: the vessel did not suspend.
        return state;
    }

    /// <summary>A scenario on the real runner: the reservoir prefilled like the generator did, the stats as given, the vessel run once. Nothing of the stage is applied yet.</summary>
    private sealed class Rig
    {
        public Scenario Scenario { get; }
        public AlundraEffectStage Stage { get; } = NewStage();
        public Host Host { get; } = new();
        public AlundraGameState State { get; } = new();
        public RecordingSound Sound { get; } = new();
        public EventProgramState Program { get; }
        public HashSet<int> Before { get; }

        public Rig(Scenario scenario)
        {
            Scenario = scenario;
            for (var i = 0; i < scenario.Prefill; i++)
            {
                Assert.NotNull(Stage.Reservoir.Create(false, 14, 0, 0, 0, 0));
            }

            foreach (var slot in scenario.Free)
            {
                Stage.Reservoir.Free(Stage.Reservoir.Slots[slot]);
            }

            Before = Stage.Reservoir.ActiveSlots.Select(s => s.Index).ToHashSet();
            Host.Stage = Stage;
            Host.Sound = Sound;
            Host.PlayerEntity = HeroAt(scenario.HeroX, scenario.HeroY, scenario.HeroZ);
            State.PlayerStats.Hp = (short)scenario.Hp;
            State.PlayerStats.HpMax = (short)scenario.HpMax;
            AlundraRandom.RandomSeed = scenario.Seed;
            Program = Exec(Host, State, VesselProgram);
        }

        /// <summary>Applies the queue without any update (the golden <c>S</c> rows).</summary>
        public void Apply() => Stage.RunFrame(0, Array.Empty<bool>());

        public List<AlundraEffectSlot> Created() => Stage.Reservoir.ActiveSlots.Where(s => !Before.Contains(s.Index)).ToList();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Rows V and S: the call itself
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGoldenTable_HoldsThirtyOneScenarios_TwoHundredNinetyCreatedSlots_AndTheOtherRows()
    {
        Assert.Equal(31, Scenarios.Count);
        Assert.Equal(290, Golden.Count(r => r[0] == "S"));
        Assert.Equal(390, Golden.Count(r => r[0] == "T"));
        Assert.Single(Golden, r => r[0] == "F");
        Assert.Equal(11, Golden.Count(r => r[0] == "P"));
    }

    [Theory]
    [MemberData(nameof(ScenarioCases))]
    public void TheVessel_OnEachScenarioOfTheBinary_AnswersOne_PlaysTheSoundAtOnce_AndQueuesTheEffects(string name)
    {
        var s = ScenarioNamed(name);
        var rig = new Rig(s);

        Assert.Equal(s.Result, rig.Program.Result);
        Assert.Equal(new[] { 49 }, rig.Sound.Played.ToArray());
        Assert.Equal("49", s.Sounds);
        Assert.Equal((s.HpOut, s.HpMaxOut), ((int)rig.State.PlayerStats.Hp, (int)rig.State.PlayerStats.HpMax));
        Assert.Equal(0, rig.Stage.Reservoir.ActiveCount - s.Prefill + s.Free.Length); // nothing is written to the reservoir before the stage applies the queue
        Assert.Equal(s.Seed, (uint)AlundraRandom.RandomSeed); // no number is drawn before the stage applies the queue

        rig.Apply();

        var created = rig.Created();
        Assert.Equal(s.Created, created.Count);
        Assert.Equal(s.Sparks, created.Count(slot => slot.ForceZ != 0));
        Assert.Equal(s.SeedOut, (uint)AlundraRandom.RandomSeed);
        Assert.Equal(2, s.Size);
    }

    [Theory]
    [MemberData(nameof(ScenarioCases))]
    public void TheCreatedSlots_RightAfterTheCall_AreTheRowsS(string name)
    {
        var s = ScenarioNamed(name);
        var rig = new Rig(s);
        rig.Apply();

        var expected = Golden.Where(r => r[0] == "S" && r[1] == name)
            .Select(r => (Slot: int.Parse(r[2]), Values: r.Skip(3).Select(int.Parse).ToArray()))
            .OrderBy(r => r.Slot).ToList();
        var actual = rig.Created().OrderBy(slot => slot.Index)
            .Select(slot => (Slot: slot.Index, Values: new[] { slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.ForceZ })).ToList();

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Slot, actual[i].Slot);
            Assert.Equal(expected[i].Values, actual[i].Values);
        }

        Assert.All(rig.Created(), slot =>
        {
            Assert.Equal(-1, slot.RecordIndex);
            Assert.Equal(0, slot.Mode);
            Assert.False(slot.TargetMapTable);
            Assert.Equal((14, 0), (slot.TargetEffect, slot.TargetAnimation));
        });
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Rows T and F: the slots tick by tick
    // ---------------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> TickedCases() => Golden.Where(r => r[0] == "T").Select(r => r[1]).Distinct().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(TickedCases))]
    public void TheSlots_AfterN_Updates_AreTheRowsT_TheRingConvergesAndTheyAreFreedOnTheTwentyNinth(string name)
    {
        var rig = new Rig(ScenarioNamed(name));
        rig.Apply();
        var rows = Golden.Where(r => r[0] == "T" && r[1] == name).ToList();
        Assert.Equal(new[] { 1, 2, 16, 27, 28, 29 }, rows.Select(r => int.Parse(r[2])).Distinct().OrderBy(n => n).ToArray());

        for (var update = 1; update <= 29; update++)
        {
            rig.Stage.RunFrame(1, new[] { false });
            foreach (var row in rows.Where(r => int.Parse(r[2]) == update))
            {
                var slot = rig.Stage.Reservoir.Slots[int.Parse(row[3])];
                if (row[4] == "free")
                {
                    Assert.False(slot.Active);
                }
                else
                {
                    Assert.True(slot.Active);
                    Assert.Equal(new[] { int.Parse(row[4]), int.Parse(row[5]), int.Parse(row[6]) }, new[] { slot.X, slot.Y, slot.Z });
                }
            }
        }
    }

    [Fact]
    public void TheTwelveSlots_AreDrawnOnTicks0To27_AndFreedOnTheTick28_RowF()
    {
        var f = Golden.Single(r => r[0] == "F");
        var lastDrawn = int.Parse(f[2]);
        var freed = int.Parse(f[3]);
        Assert.Equal((27, 28), (lastDrawn, freed));

        var rig = new Rig(ScenarioNamed("c00"));
        rig.Apply();
        var created = rig.Created();
        Assert.Equal(12, created.Count);
        for (var tick = 0; tick <= freed; tick++)
        {
            rig.Stage.RunFrame(1, new[] { false });
            // a slot of tick t is drawn if it is still active after update t (the draw follows the update)
            Assert.Equal(tick <= lastDrawn ? 12 : 0, created.Count(slot => slot.Active));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Rows P: the life, the real code of the chain (vessel, then the end of MovePlayer)
    // ---------------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> LifeCases() => Golden.Where(r => r[0] == "P").Select(r => new object[] { int.Parse(r[1]), int.Parse(r[2]), int.Parse(r[3]), int.Parse(r[4]) });

    [Theory]
    [MemberData(nameof(LifeCases))]
    public void TheLife_IsTheRowsP_TheVesselThenTheEndOfMovePlayer(int hp, int hpMax, int statsHp, int statsHpMax)
    {
        var state = new AlundraGameState();
        state.PlayerStats.Hp = (short)hp;
        state.PlayerStats.HpMax = (short)hpMax;

        Exec(new Host { Stage = NewStage(), Sound = new RecordingSound(), PlayerEntity = HeroAt(0, 0, 0) }, state, VesselProgram);

        Assert.Equal((statsHp, statsHpMax), ((int)state.PlayerStats.Hp, (int)state.PlayerStats.HpMax));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The real runner: the whole vessel
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheVessel_OnTheNewGameStats_Answers1_DoesNotSuspend_RaisesTheLife_PlaysTheSoundBeforeTheEffects_AndCreatesTwelveSlots()
    {
        var stage = NewStage();
        var sound = new RecordingSound();
        var host = new Host { Stage = stage, Sound = sound, PlayerEntity = HeroAt(65273856, 33030144, 0) };
        var gameState = new AlundraGameState();
        Assert.Equal((10, 10), ((int)gameState.PlayerStats.Hp, (int)gameState.PlayerStats.HpMax));
        gameState.PlayerStats.Hp = 4;

        var state = Exec(host, gameState, VesselProgram);

        Assert.Equal(1, state.Result);
        Assert.Equal((11, 11), ((int)gameState.PlayerStats.Hp, (int)gameState.PlayerStats.HpMax));
        Assert.Equal(new[] { 0x31 }, sound.Played.ToArray());
        Assert.Equal(0, stage.Reservoir.ActiveCount); // the sound is played at once, the effects are queued: the sound comes first
        Assert.Equal(0xB017C93Du, (uint)AlundraRandom.RandomSeed);

        stage.RunFrame(1, new[] { false });

        Assert.Equal(12, stage.Reservoir.ActiveCount);
        Assert.Equal(0xC5BF9705u, (uint)AlundraRandom.RandomSeed);
    }

    [Fact]
    public void TheVessel_ReadsTheHeroWhereItIsWhenTheScriptRuns_NotWhenTheStageApplies()
    {
        var stage = NewStage();
        var hero = HeroAt(65273856, 33030144, 0);
        Exec(new Host { Stage = stage, Sound = new RecordingSound(), PlayerEntity = hero }, new AlundraGameState(), VesselProgram);
        hero.PosX = 1;
        hero.PosY = 2;
        hero.PosZ = 3;

        stage.RunFrame(0, Array.Empty<bool>());

        Assert.Equal(65273856, stage.Reservoir.Slots[0].X);
        Assert.Equal(33030144 + (512 << 13), stage.Reservoir.Slots[0].Y);
    }

    [Fact]
    public void TheLifeCap_HpMax49Goes50_At50StaysFifty_AndTheSaveStaysValid()
    {
        foreach (var (hpMaxBefore, hpBefore) in new[] { (49, 20), (50, 50), (50, 3) })
        {
            var gameState = new AlundraGameState();
            gameState.PlayerStats.HpMax = (short)hpMaxBefore;
            gameState.PlayerStats.Hp = (short)hpBefore;
            gameState.PlayerStats.WeaponId = 1; // the New Game weapon: a state the save validation accepts

            Exec(new Host { Stage = NewStage(), Sound = new RecordingSound(), PlayerEntity = HeroAt(0, 0, 0) }, gameState, VesselProgram);

            Assert.Equal((50, 50), ((int)gameState.PlayerStats.Hp, (int)gameState.PlayerStats.HpMax));
            var save = AlundraSaveGame.Capture(gameState, 389, 33, 59, 0);
            Assert.True(save.TryValidate(SaveGameDirectorTestSupport.RealRules(), out var error), error);
        }
    }

    [Fact]
    public void TheHud_CatchesUpToTheNewMaximum_AfterAFewTicks()
    {
        var state = new AlundraGameState();
        var director = AlundraHudDirector.Instance;
        director.AttachToWorld(state);
        state.AddFlag(1813, 0x200000); // "please appear"
        for (var i = 0; i < 18; i++)
        {
            director.Tick();
        }

        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);
        Assert.Equal(10, director.HpMax);

        Exec(new Host { Stage = NewStage(), Sound = new RecordingSound(), PlayerEntity = HeroAt(0, 0, 0) }, state, VesselProgram);

        Assert.Equal(11, state.PlayerStats.HpMax);
        for (var i = 0; i < 40; i++)
        {
            director.Tick();
        }

        Assert.Equal(11, director.HpMax);
        Assert.Equal(11, director.Hp);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The eight sites of the export
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheEightSites_AreTheBytes_82_53_AtTheirPcOfTheirProgram()
    {
        var annex = FindAnnexFile("data", "sites53.out");
        var header = new Regex(@"^=== map (\d+) program ([A-F])\[(\d+)\] pc (\d+)");
        var sites = File.ReadAllLines(annex).Select(l => header.Match(l)).Where(m => m.Success)
            .Select(m => (Map: int.Parse(m.Groups[1].Value), Table: m.Groups[2].Value[0], Program: int.Parse(m.Groups[3].Value), Pc: int.Parse(m.Groups[4].Value))).ToList();
        Assert.Equal(
            new[] { (134, 'C', 4, 1083), (134, 'C', 9, 1331), (134, 'C', 10, 1387), (134, 'C', 11, 1443), (258, 'B', 5, 614), (298, 'B', 5, 668), (302, 'C', 8, 624), (398, 'B', 6, 1202) },
            sites.ToArray());

        foreach (var site in sites)
        {
            var document = MapEventProgramLoader.Load(Root, EffectGolden.WorldNameOf(Root, site.Map)!)!;
            Assert.Equal(new[] { 0x82, 0x53 }, document.Codes.Skip(site.Pc).Take(2).ToArray());

            var table = site.Table == 'B' ? document.EventCodesBTable : document.EventCodesCTable;
            var start = table[site.Program];
            Assert.True(start <= site.Pc);
            Assert.Equal(start, table.Where(entry => entry <= site.Pc).Max()); // no other program of the table starts between the start of the named one and the site
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The degraded cases
    // ---------------------------------------------------------------------------------------------------------------------

    private sealed class DebugLog : ILogger, IDisposable
    {
        public List<string> Debugs { get; } = new();
        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) => Debugs.Add(msg);
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) { }

        public static DebugLog Install()
        {
            var log = new DebugLog();
            Logs.AddLogger(log);
            return log;
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }

    [Fact]
    public void WithoutAHero_TheLifeAndTheSoundAreDone_TheEffectsAreSkipped_AndTraceOneDegradedLogPerRunner()
    {
        using var log = DebugLog.Install();
        var stage = NewStage();
        var sound = new RecordingSound();
        var gameState = new AlundraGameState();
        var trace = new List<EventTraceRecord>();

        var state = Exec(new Host { Stage = stage, Sound = sound, PlayerEntity = null }, gameState, new[] { 0x82, 0x53, 0x82, 0x53, 0x1A, 9, 0xFF }, trace);
        stage.RunFrame(1, new[] { false });

        Assert.Equal(1, state.Result);
        Assert.Equal((12, 12), ((int)gameState.PlayerStats.Hp, (int)gameState.PlayerStats.HpMax));
        Assert.Equal(new[] { 0x31, 0x31 }, sound.Played.ToArray());
        Assert.Equal(0, stage.Reservoir.ActiveCount);
        Assert.Equal(0xB017C93Du, (uint)AlundraRandom.RandomSeed);
        Assert.All(trace.Where(record => record.Opcode == 0x82), record => Assert.Equal(EventTraceKind.Degraded, record.Kind));
        Assert.Equal(2, trace.Count(record => record.Opcode == 0x82));
        Assert.Single(log.Debugs, message => message.Contains("life vessel"));
    }

    [Fact]
    public void WithoutAStage_TheLifeAndTheSoundAreDone_NothingIsDrawn_AndTheTraceIsDegraded()
    {
        var sound = new RecordingSound();
        var gameState = new AlundraGameState();
        var trace = new List<EventTraceRecord>();

        var state = Exec(new Host { Stage = null, Sound = sound, PlayerEntity = HeroAt(0, 0, 0) }, gameState, VesselProgram, trace);

        Assert.Equal(1, state.Result);
        Assert.Equal((11, 11), ((int)gameState.PlayerStats.Hp, (int)gameState.PlayerStats.HpMax));
        Assert.Equal(new[] { 0x31 }, sound.Played.ToArray());
        Assert.Equal(0xB017C93Du, (uint)AlundraRandom.RandomSeed);
        Assert.Equal(EventTraceKind.Degraded, trace.First(record => record.Opcode == 0x82).Kind);
    }

    [Fact]
    public void WithoutASoundPlayer_TheLifeAndTheEffectsAreDone()
    {
        var stage = NewStage();
        var gameState = new AlundraGameState();

        Exec(new Host { Stage = stage, Sound = null, PlayerEntity = HeroAt(0, 0, 0) }, gameState, VesselProgram);
        stage.RunFrame(0, Array.Empty<bool>());

        Assert.Equal(11, gameState.PlayerStats.HpMax);
        Assert.Equal(12, stage.Reservoir.ActiveCount);
    }

    [Fact]
    public void AFullReservoir_StillRaisesTheLifeAndPlaysTheSound_AndDrawsNoNumber()
    {
        var s = ScenarioNamed("c28");
        var rig = new Rig(s);
        rig.Apply();

        Assert.Equal((11, 11), ((int)rig.State.PlayerStats.Hp, (int)rig.State.PlayerStats.HpMax));
        Assert.Empty(rig.Created());
        Assert.Equal(s.Seed, (uint)AlundraRandom.RandomSeed);
        Assert.Equal(new[] { 49 }, rig.Sound.Played.ToArray());
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // G3D-R4: the native creators of modes 1 (attached) and 3 (detached), against g3d-modes13.tsv
    // ---------------------------------------------------------------------------------------------------------------------

    private static readonly string[][] Modes13 = ReadTable("g3d-modes13.tsv");

    public static IEnumerable<object[]> Modes13Cases() => Modes13.Where(r => r[0] == "C").Select(r => new object[] { r[1] });

    private sealed class Anchor : IAlundraEffectAnchor
    {
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int PosZ { get; set; }
        public uint DepthKey { get; set; }
        public int Status { get; set; }

        /// <summary>A row <c>E</c>: <c>E name update status x y z depthkey</c>.</summary>
        public void Set(string[] e)
        {
            Status = int.Parse(e[3]);
            PosX = int.Parse(e[4]);
            PosY = int.Parse(e[5]);
            PosZ = int.Parse(e[6]);
            DepthKey = uint.Parse(e[7]);
        }
    }

    [Fact]
    public void TheModes13Table_HoldsEightScenarios_OfSixUpdates()
    {
        Assert.Equal(8, Modes13.Count(r => r[0] == "C"));
        Assert.Equal(48, Modes13.Count(r => r[0] == "E"));
        Assert.Equal(48, Modes13.Count(r => r[0] == "M"));
        Assert.Equal(3, Modes13.Count(r => r[0] == "C" && r[2] == "detached"));
    }

    [Theory]
    [MemberData(nameof(Modes13Cases))]
    public void TheCreators_AndTheUpdates_OfTheModes1And3_AreTheRowsOfTheBinary(string name)
    {
        var c = Modes13.Single(r => r[0] == "C" && r[1] == name);
        var updates = Modes13.Where(r => r[0] == "E" && r[1] == name).OrderBy(r => int.Parse(r[2])).ToList();
        var results = Modes13.Where(r => r[0] == "M" && r[1] == name).OrderBy(r => int.Parse(r[2])).ToList();
        Assert.Equal(6, updates.Count);
        Assert.Equal(6, results.Count);

        var reservoir = new AlundraEffectReservoir(Bank());
        var anchor = new Anchor();
        anchor.Set(updates[0]);
        var offsets = (X: int.Parse(c[11]), Y: int.Parse(c[12]), Z: int.Parse(c[13]));
        var depthOffset = int.Parse(c[14]);
        var slot = c[2] == "attached"
            ? reservoir.CreateAttached(false, 14, 0, anchor, depthOffset, offsets.X, offsets.Y, offsets.Z)
            : reservoir.CreateDetached(false, 14, 0, anchor, depthOffset, int.Parse(c[5]), int.Parse(c[6]), int.Parse(c[7]));
        Assert.NotNull(slot);
        slot!.ForceX = int.Parse(c[8]); // the native code writes the forces after the creator
        slot.ForceY = int.Parse(c[9]);
        slot.ForceZ = int.Parse(c[10]);

        Assert.Equal(int.Parse(c[3]), slot.Index);
        Assert.Equal(int.Parse(c[4]), slot.Mode);
        Assert.Equal(new[] { int.Parse(c[5]), int.Parse(c[6]), int.Parse(c[7]) }, new[] { slot.X, slot.Y, slot.Z });
        Assert.Equal(new[] { offsets.X, offsets.Y, offsets.Z }, new[] { slot.OffsetX, slot.OffsetY, slot.OffsetZ });
        Assert.Equal(depthOffset, slot.DepthOffset);
        Assert.Same(anchor, slot.Anchor);
        Assert.Equal(-1, slot.RecordIndex);

        for (var u = 0; u < 6; u++)
        {
            anchor.Set(updates[u]);
            reservoir.Tick(false);

            var m = results[u];
            Assert.Equal(int.Parse(m[3]), slot.Mode);
            Assert.Equal(new[] { int.Parse(m[4]), int.Parse(m[5]), int.Parse(m[6]) }, new[] { slot.X, slot.Y, slot.Z });
            Assert.Equal(Convert.ToUInt32(m[7], 16), slot.DepthKey);
            Assert.Equal(m[8] == "1", slot.Active);
        }
    }

    [Fact]
    public void AnAnchorOfStatus0_StillReceivesTheForcesOnTheUpdateThatFreezesIt_ThenNothingMoves()
    {
        var reservoir = new AlundraEffectReservoir(Bank());
        var anchor = new Anchor { Status = 2, PosX = 100, PosY = 200, PosZ = 300, DepthKey = 7 };
        var slot = reservoir.CreateAttached(false, 14, 0, anchor, 0, 1, 2, 3)!;
        slot.ForceX = 10;

        reservoir.Tick(false);
        Assert.Equal((1, 111), (slot.Mode, slot.X)); // anchor + offset (101), then the forces of the fall-through (+ 10)

        anchor.Status = 0;
        anchor.PosX = 5000;
        reservoir.Tick(false);
        Assert.Equal((2, 121), (slot.Mode, slot.X)); // not re-derived from the anchor (5001), the forces still added

        reservoir.Tick(false);
        Assert.Equal((2, 121), (slot.Mode, slot.X)); // mode 2: nothing
    }

    [Fact]
    public void TheCreatorsOfMode0_ResetWhatTheModes1And3Set_AndAFullReservoirCreatesNothing()
    {
        var reservoir = new AlundraEffectReservoir(Bank());
        var anchor = new Anchor { Status = 2 };
        var attached = reservoir.CreateAttached(false, 14, 0, anchor, 5, 1, 2, 3)!;
        reservoir.Free(attached);

        var plain = reservoir.Create(false, 14, 0, 9, 9, 9)!;

        Assert.Same(attached, plain);
        Assert.Equal((0, 0, 0, 0, 0, 0), (plain.Mode, plain.DepthOffset, plain.OffsetX, plain.OffsetY, plain.OffsetZ, plain.Anchor is null ? 0 : 1));

        for (var i = reservoir.ActiveCount; i < AlundraEffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(reservoir.CreateDetached(false, 14, 0, anchor, 0, 0, 0, 0));
        }

        Assert.Null(reservoir.CreateAttached(false, 14, 0, anchor, 0, 0, 0, 0));
        Assert.Null(reservoir.CreateDetached(false, 14, 0, anchor, 0, 0, 0, 0));
    }
}
