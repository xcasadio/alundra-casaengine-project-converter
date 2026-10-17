#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Combat;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b K4/K5 (docs/plan-e20-heros.md section 1.6): the opcodes <c>0x71</c>, <c>0x86</c>, <c>0x87</c> and <c>0x95</c> through the real interpreter, case by case against the oracle
/// <c>docs/plan-e20-annexe/e20-c1b/oracle/out/K3.txt</c> (the real handlers of <c>ALUN_CD.EXE</c> (France) on a world of a hero, two Beannoids touched by his sword, an armed Beannoid touching another and an untouched one;
/// every expected value below is a line of that file), then inside real programs D run by the interpreter on the state shared by A, D, E and F. The world is rebuilt by hand with the oracle's flags, attacks and
/// counters; its boxes are mine, placed to give the geometry of each line (the oracle's own boundary lines move the hero's box by one raw unit around a rectangle, and so do the ones below).
/// </summary>
public sealed class AlundraHitOpcodesTests
{
    private const int Sentinel = 0x5555;
    private const int Raw = 1 << 16;

    private sealed class Context : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    /// <summary>The world of K3: the hero (slot 0), T and T2 (map records 0 and 1, touched by the hero's sword, attribute 0x81), A (record 2, armed, attribute 0x04), V (record 3, touched by A), U (record 4, untouched).</summary>
    private sealed class K3World
    {
        public AlundraEntityScriptProxy Hero { get; } = Make(-1, 0x3118C, 0x81, 1);
        public AlundraEntityScriptProxy T { get; } = Make(0, 0x103, 0x01, 9);
        public AlundraEntityScriptProxy T2 { get; } = Make(1, 0x103, 0x01, 9);
        public AlundraEntityScriptProxy A { get; } = Make(2, 0x107, 0x04, 2);
        public AlundraEntityScriptProxy V { get; } = Make(3, 0x103, 0x01, 9);
        public AlundraEntityScriptProxy U { get; } = Make(4, 0x103, 0x01, 9);
        public Context Context { get; } = new();

        public K3World()
        {
            Context.PlayerEntity = Hero;
            Context.Spawned.AddRange(new[] { Hero, T, T2, A, V, U });
            T.TouchingEntity = Hero.SelfEntity;
            T2.TouchingEntity = Hero.SelfEntity;
            V.TouchingEntity = A.SelfEntity;
            Hero.HitCounter = 2;
            A.HitCounter = 1;

            // The hero's swing box at the end of tick 5: (233, 88) px, 29 x 15 x 15 px, on the ground (binary Z 1 raw, so the DLL's 0).
            SetBox(Hero, 233 * Raw, 88 * Raw, 0, 29, 15, 15);
            SetBox(T, 252 * Raw, 96 * Raw, 0, 29, 15, 15);
            SetBox(T2, 264 * Raw, 104 * Raw, 0, 29, 15, 15);
            SetBox(A, 348 * Raw, 88 * Raw, 0, 29, 15, 15);
            SetBox(V, 360 * Raw, 96 * Raw, 0, 29, 15, 15);
            SetBox(U, 252 * Raw, 168 * Raw, 0, 29, 15, 15);
        }

        public IEnumerable<AlundraEntityScriptProxy> All => new[] { Hero, T, T2, A, V, U };
    }

    private static AlundraEntityScriptProxy Make(int recordId, uint flags, int attribute, int power)
    {
        var entity = new Entity { Name = "e" + recordId, GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.LogicContextEntity = entity;
        proxy.EntityRefId = recordId;
        proxy.Status = EntityStatus.Normal;
        proxy.Flags = flags;
        proxy.CurrentAttack = new AlundraBalanceAttack(attribute, power);
        proxy.HasFrameBox = true;
        return proxy;
    }

    private static void SetBox(AlundraEntityScriptProxy entity, int x, int y, int z, int width, int depth, int height)
    {
        entity.HitBoxX = x;
        entity.HitBoxY = y;
        entity.HitBoxZ = z;
        entity.CollisionWidth = (width << 16) - 1;
        entity.CollisionDepth = (depth << 16) - 1;
        entity.CollisionHeight = (height << 16) - 1;
    }

    private static EventProgramDocument Document(params int[] codes) => new() { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = codes };

    /// <summary>Runs <paramref name="code"/> (then the end) for <paramref name="self"/>; returns the state (its size is <c>CodeIndex</c>, its answer <c>Result</c>, which starts at a sentinel).</summary>
    private static EventProgramState Run(K3World world, AlundraEntityScriptProxy self, params int[] code)
    {
        var document = Document(code.Append(0xFF).ToArray());
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), world.Context);
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = Sentinel };
        runner.RunOneScriptCall(self, state);
        return state;
    }

    // -----------------------------------------------------------------------------------------
    // 0x71
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Op71_ResultIsTheHitCounterOfTheLogicEntity_Size1()
    {
        var world = new K3World();
        var expected = new[] { (world.Hero, 2), (world.T, 0), (world.T2, 0), (world.A, 1), (world.V, 0), (world.U, 0) }; // K3: "0x71 HitCounter of hero/T/T2/A/V/U: 2 / 0 / 0 / 1 / 0 / 0"
        foreach (var (self, counter) in expected)
        {
            var state = Run(world, self, 0x71);
            Assert.Equal(counter, state.Result);
            Assert.Equal(1, state.CodeIndex);
        }
    }

    // -----------------------------------------------------------------------------------------
    // 0x86: a SET of +0x1CC, Result untouched
    // -----------------------------------------------------------------------------------------

    private static string Counters(K3World world) => string.Join(" ", world.All.Select(e => e.DamagedTickCounter.ToString("X")));

    [Fact]
    public void Op86_SetsTheCounterOfEveryMatch_NotAnAdd_AndLeavesResultAlone()
    {
        var world = new K3World();
        var state = Run(world, world.T, 0x86, 0, 0x2C, 0x01); // search record 0 (T), value 0x012C
        Assert.Equal("0 12C 0 0 0 0", Counters(world));
        Assert.Equal(Sentinel, state.Result);
        Assert.Equal(4, state.CodeIndex);

        world = new K3World();
        world.U.EntityRefId = 0; // record 0 shared by T and U
        Run(world, world.T, 0x86, 0, 0x34, 0x12);
        Assert.Equal("0 1234 0 0 0 1234", Counters(world));

        world = new K3World();
        Run(world, world.T, 0x86, 5, 0x34, 0x12); // record 5: no entity
        Assert.Equal("0 0 0 0 0 0", Counters(world));

        world = new K3World();
        world.T.DamagedTickCounter = 0x0100;
        Run(world, world.T, 0x86, 0, 2, 0); // SET, not add: 2, not 0x102
        Assert.Equal("0 2 0 0 0 0", Counters(world));

        world = new K3World();
        Run(world, world.T, 0x86, 0, 0xFF, 0xFF);
        Assert.Equal("0 FFFF 0 0 0 0", Counters(world));

        world = new K3World();
        world.T.DamagedTickCounter = 0x55;
        Run(world, world.T, 0x86, 0, 0, 0); // a value 0 clears
        Assert.Equal("0 0 0 0 0 0", Counters(world));
    }

    [Fact]
    public void Op86_TheSpecialSearches_Self0x80_Hero0x81_Everyone0x82()
    {
        var world = new K3World();
        Run(world, world.A, 0x86, 0x80, 5, 0); // owner A: "self" is A
        Assert.Equal("0 0 0 5 0 0", Counters(world));

        // K3: "search 0x80 (self) with owner A, self T -> T=5": the logic entity (the program's a0) is T while A runs it.
        world = new K3World();
        world.A.LogicEntity = world.T;
        Run(world, world.A, 0x86, 0x80, 5, 0);
        Assert.Equal("0 5 0 0 0 0", Counters(world));

        world = new K3World();
        Run(world, world.T, 0x86, 0x81, 7, 0);
        Assert.Equal("7 0 0 0 0 0", Counters(world));

        world = new K3World();
        Run(world, world.T, 0x86, 0x82, 9, 0);
        Assert.Equal("9 9 9 9 9 9", Counters(world));
    }

    [Fact]
    public void Op86_ADamagedTarget_IsSkippedByTheHitPass_UntilTheCountersPassRunsItDown()
    {
        var world = new K3World();
        Run(world, world.T, 0x86, 0, 3, 0);
        Assert.Equal(3, world.T.DamagedTickCounter); // the hit pass skips a target whose +0x1CC is not 0 (AlundraHitPass); its own tests pin that rule.
    }

    // -----------------------------------------------------------------------------------------
    // 0x87
    // -----------------------------------------------------------------------------------------

    private static int Op87(K3World world, AlundraEntityScriptProxy self, int search, int typeIndex)
    {
        var state = Run(world, self, 0x87, search, typeIndex);
        Assert.Equal(3, state.CodeIndex);
        return state.Result;
    }

    [Fact]
    public void Op87_TouchedByTheHerosSword_OnlyTheSwordTypeIndex0Matches()
    {
        var world = new K3World();
        // K3: "T (hero sword, nibble 1) typeIdx 0..7: 1 0 0 0 0 0 0 0".
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0 }, Enumerable.Range(0, 8).Select(index => Op87(world, world.U, 0, index)).ToArray());
        Assert.Equal(1, Op87(world, world.U, 1, 0)); // T2
        Assert.Equal(1, Op87(world, world.U, 3, 3)); // V, touched by A (nibble 4)
        Assert.Equal(0, Op87(world, world.U, 3, 0));
        Assert.Equal(0, Op87(world, world.T, 4, 0)); // U, untouched
        Assert.Equal(0, Op87(world, world.T, 4, 3));
    }

    [Fact]
    public void Op87_SearchSelf0x80_ReadsTheLeverCase()
    {
        var world = new K3World();
        Assert.Equal(1, Op87(world, world.T, 0x80, 0));
        Assert.Equal(0, Op87(world, world.T, 0x80, 3));
        Assert.Equal(1, Op87(world, world.V, 0x80, 3));
    }

    [Fact]
    public void Op87_ASharedRecordId_AnyMatchQualifies_AndNoEntityGivesZero()
    {
        var world = new K3World();
        world.U.EntityRefId = 0; // record 0 shared by T and U
        Assert.Equal(1, Op87(world, world.A, 0, 0));

        world = new K3World();
        world.U.EntityRefId = 3; // record 3 shared by V and U
        Assert.Equal(1, Op87(world, world.A, 3, 3));

        world = new K3World();
        Assert.Equal(0, Op87(world, world.A, 5, 0)); // record 5: no entity
    }

    [Fact]
    public void Op87_TheAttackerWithoutABoxOrAnAttackOrAnAttribute_AndTheTargetNotTouched_GiveZero()
    {
        var world = new K3World();
        world.Hero.HasFrameBox = false;
        Assert.Equal(0, Op87(world, world.U, 0, 0));

        world = new K3World();
        world.Hero.CurrentAttack = null;
        Assert.Equal(0, Op87(world, world.U, 0, 0));

        world = new K3World();
        world.Hero.CurrentAttack = new AlundraBalanceAttack(0, 1);
        Assert.Equal(0, Op87(world, world.U, 0, 0));

        world = new K3World();
        world.T.TouchingEntity = null;
        Assert.Equal(0, Op87(world, world.U, 0, 0));
    }

    [Fact]
    public void Op87_TheTableOf12Bytes_OnlyTheIndex10CanMatchAfterTheEight()
    {
        var world = new K3World();
        // K3: V touched by A (nibble 4): typeIdx 8..11 -> 0 0 0 0.
        Assert.Equal(new[] { 0, 0, 0, 0 }, Enumerable.Range(8, 4).Select(index => Op87(world, world.U, 3, index)).ToArray());

        world.A.CurrentAttack = new AlundraBalanceAttack(0x03, 2); // nibble 3: typeIdx 2 and 10 (bytes 03 and 03).
        Assert.Equal(1, Op87(world, world.U, 3, 2));
        Assert.Equal(new[] { 0, 0, 1, 0 }, Enumerable.Range(8, 4).Select(index => Op87(world, world.U, 3, index)).ToArray());

        world.A.CurrentAttack = new AlundraBalanceAttack(0x05, 2); // nibble 5: typeIdx 4 and 5.
        Assert.Equal(1, Op87(world, world.U, 3, 4));
        Assert.Equal(1, Op87(world, world.U, 3, 5));

        Assert.Equal(0, Op87(world, world.U, 3, 12)); // past the table of 12: no site in the corpus, never a match here.
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 5, 6, 7, 0x58, 0xD1, 3, 0x80 }, AlundraHitOpcodes.TypeNibbles);
    }

    // -----------------------------------------------------------------------------------------
    // 0x95
    // -----------------------------------------------------------------------------------------

    private static int Op95(K3World world, int x, int y, int z, int wx, int hy, int mask, int typeIndex, int unused = 0)
    {
        var state = Run(world, world.U, 0x95, x, y, z, wx, hy, unused, mask & 0xFF, mask >> 8, typeIndex);
        Assert.Equal(10, state.CodeIndex);
        return state.Result;
    }

    [Fact]
    public void Op95_TheRectangleAroundTheHerosSwing_AndTheFilters()
    {
        var world = new K3World();
        Assert.Equal(1, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0)); // IN: hero-only mask, the sword type
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 3)); // typeIdx 3 (nibble 4): no attacker of that kind there
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0040, 0)); // no entity has the bit
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0000, 0)); // a zero mask answers 0 at once
        Assert.Equal(0, Op95(world, 11, 5, 0, 2, 2, 0x0008, 0)); // entirely right of the box
        Assert.Equal(0, Op95(world, 0, 5, 0, 2, 2, 0x0008, 0)); // entirely left
        Assert.Equal(0, Op95(world, 9, 7, 0, 2, 2, 0x0008, 0)); // below
        Assert.Equal(0, Op95(world, 9, 0, 0, 2, 1, 0x0008, 0)); // above, one unit high
        Assert.Equal(0, Op95(world, 9, 5, 3, 2, 2, 0x0008, 0)); // from z unit 3 (48 px): the box (z 0..15 px) is below it
        Assert.Equal(1, Op95(world, 9, 5, 0, 2, 2, 0xFFFF, 0)); // all bits: the other attackers qualify as well
        Assert.Equal(1, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0, unused: 0x55)); // the byte 6 is not used
    }

    [Fact]
    public void Op95_TheRectangleAroundTheArmedBeannoid_MaskAndTypeSelectWhoCounts()
    {
        var world = new K3World();
        Assert.Equal(1, Op95(world, 14, 5, 0, 2, 2, 0x0004, 3)); // A is nibble 4, flags 0x107 & 4
        Assert.Equal(0, Op95(world, 14, 5, 0, 2, 2, 0x0004, 0)); // the sword nibble: V (flags 0x103 & 4 = 0) does not qualify, the hero is elsewhere
        Assert.Equal(1, Op95(world, 14, 5, 0, 2, 2, 0x0100, 0)); // V and A share the bit 8; V is nibble 1

        // typeIdx 8..11 with A at nibble 3 and mask 4: only the index 2 (byte 3) and 10 (byte 3) match.
        world.A.CurrentAttack = new AlundraBalanceAttack(0x03, 2);
        Assert.Equal(new[] { 1, 0, 0, 0, 1, 0, 0 }, new[] { 2, 3, 8, 9, 10, 11, 12 }.Select(index => Op95(world, 14, 5, 0, 2, 2, 0x0004, index)).ToArray());

        world.A.CurrentAttack = new AlundraBalanceAttack(0x04, 2); // the real nibble 4: 8..11 -> 0 0 0 0
        Assert.Equal(new[] { 0, 0, 0, 0 }, Enumerable.Range(8, 4).Select(index => Op95(world, 14, 5, 0, 2, 2, 0x0004, index)).ToArray());
    }

    [Fact]
    public void Op95_TheStatusTheBlockerTheBoxAndTheAttackOfTheAttackerAreFilters()
    {
        var world = new K3World();
        world.Hero.Status = EntityStatus.Loaded; // not 2 or 3
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));

        world = new K3World();
        world.Hero.BlockedByEntity = world.T.SelfEntity; // +0x20 != 0
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));

        world = new K3World();
        world.Hero.HasFrameBox = false;
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));

        world = new K3World();
        world.Hero.CurrentAttack = null;
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));

        world = new K3World();
        world.Hero.CurrentAttack = new AlundraBalanceAttack(0, 1);
        Assert.Equal(0, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));

        world = new K3World();
        world.Hero.Status = EntityStatus.Deactivated; // 3 counts
        Assert.Equal(1, Op95(world, 9, 5, 0, 2, 2, 0x0008, 0));
    }

    /// <summary>The boundary lines of K3 on the rectangle (10, 6, 0) 1 x 1 units: X [240, 264) px, Y [96, 112) px, Z [0, 24) px (binary), the hero's box moved one raw unit around the edge on one axis, inside on the others.</summary>
    [Theory]
    [InlineData(0, 0, 1)] // d = 0
    [InlineData(0, 1, 1)] // d = size - 1
    [InlineData(0, 2, 0)] // d = size
    [InlineData(0, 3, 1)] // d = -asize
    [InlineData(0, 4, 0)] // d = -asize - 1
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(1, 3, 1)]
    [InlineData(1, 4, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(2, 2, 0)]
    [InlineData(2, 3, 1)]
    [InlineData(2, 4, 0)]
    public void Op95_TheBoundariesOfEachAxis_AreTheOraclesToTheRawUnit(int axis, int line, int expected)
    {
        var world = new K3World();
        var origin = new[] { 240 * Raw, 96 * Raw, 0 };
        var size = new[] { 24 * Raw, 16 * Raw, 24 * Raw }; // the Z size is the X size
        var attackerSize = new[] { 1900543, 983039, 983039 };
        var box = new[] { origin[0] + 1000, origin[1] + 1000, origin[2] + 1000 }; // inside on the axes that are not the one under test
        box[axis] = origin[axis] + line switch { 0 => 0, 1 => size[axis] - 1, 2 => size[axis], 3 => -attackerSize[axis], _ => -attackerSize[axis] - 1 };
        // The binary's Z is the DLL's plus one raw unit (ADR-0026): the box handed to the interpreter is the DLL's.
        SetBox(world.Hero, box[0], box[1], box[2] - 1, 29, 15, 15);
        world.Hero.CollisionWidth = attackerSize[0];
        world.Hero.CollisionDepth = attackerSize[1];
        world.Hero.CollisionHeight = attackerSize[2];

        Assert.Equal(expected, Op95(world, 10, 6, 0, 1, 1, 0x0008, 0));
    }

    // -----------------------------------------------------------------------------------------
    // Inside real programs D, run by the interpreter on the state shared by A, D, E and F.
    // -----------------------------------------------------------------------------------------


    private static AlundraEventProgramRunner RunnerFor(K3World world, AlundraGameState state, int[] aTable, int[] dTable, int[] codes)
    {
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = aTable, EventCodesDTable = dTable, Codes = codes };
        return new AlundraEventProgramRunner(document, state, world.Context);
    }

    /// <summary>A program D: <c>0x87 [128, 0]</c> (am I touched by a sword), the jump when <c>Result</c> is 0 (<c>0x04</c>, to the end), the flag 9 on (<c>0x05</c>), the end.</summary>
    private static readonly int[] SwordSwitchProgram = { 0x87, 0x80, 0x00, 0x04, 6, 0, 0x05, 9, 0, 0xFF };

    [Fact]
    public void Op87_InAProgramD_DecidesOnItsOwnAnswer_NotOnTheResultAProgramALeftInTheSharedState()
    {
        var world = new K3World();
        var gameState = new AlundraGameState();
        // A program A at code 0 (0x2C on a search that finds nothing: Result := 1), the program D above at code 3.
        var codes = new[] { 0x2C, 5, 0xFF }.Concat(SwordSwitchProgram).ToArray();
        var runner = RunnerFor(world, gameState, new[] { 0, 0 }, new[] { 0, 3 }, codes);
        foreach (var entity in new[] { world.T, world.U })
        {
            entity.ProgramIndexes[ScriptHelper.ProgramALoad] = 0x81;
            entity.ProgramIndexes[ScriptHelper.ProgramDTouch] = 0x81;
        }

        // 1. The program A of the untouched U leaves Result = 1 in the state it shares with every D, E and F.
        runner.RunScript(world.U, ScriptHelper.ProgramALoad);
        Assert.Equal(1, runner.SharedProgramStateForTests.Result);

        // 2. The program D of the untouched U asks "am I touched by a sword": no. The stale 1 must not decide, so the flag stays off.
        runner.RunScript(world.U, ScriptHelper.ProgramDTouch);
        Assert.Equal(0, runner.SharedProgramStateForTests.Result);
        Assert.Equal(0u, gameState.GetFlag(9) & 1u << 9);

        // 3. A stale 1 again, then the program D of T, touched by the hero's sword: yes, and the flag goes on.
        runner.RunScript(world.U, ScriptHelper.ProgramALoad);
        Assert.Equal(1, runner.SharedProgramStateForTests.Result);
        runner.SharedProgramStateForTests.Result = 0;
        runner.RunScript(world.T, ScriptHelper.ProgramDTouch);
        Assert.Equal(1, runner.SharedProgramStateForTests.Result);
        Assert.NotEqual(0u, gameState.GetFlag(9) & 1u << 9);
    }

    [Fact]
    public void Op95_InAProgramD_ReadsTheStoredBox_AndTheStateItLeavesIsTheNextProgramsInput()
    {
        var world = new K3World();
        var gameState = new AlundraGameState();
        // A program D at 0: 0x95 [9, 5, 0, 2, 2, _, mask 0x0008, sword]; the jump when false; the flag 11 on; the end.
        var codes = new[] { 0x95, 9, 5, 0, 2, 2, 0, 0x08, 0x00, 0x00, 0x04, 6, 0, 0x05, 11, 0, 0xFF };
        var runner = RunnerFor(world, gameState, new[] { 0, 0 }, new[] { 0, 0 }, codes);
        world.U.ProgramIndexes[ScriptHelper.ProgramDTouch] = 0x81;
        runner.SharedProgramStateForTests.Result = 7; // stale

        runner.RunScript(world.U, ScriptHelper.ProgramDTouch);
        Assert.Equal(1, runner.SharedProgramStateForTests.Result);
        Assert.NotEqual(0u, gameState.GetFlag(11) & 1u << 11);

        // The hero's stored box moves out of the rectangle: the same program answers no, and the shared state a following program reads says so.
        world.Hero.HitBoxX += 100 * Raw;
        gameState.SetFlag(11, ~(1u << 11));
        runner.RunScript(world.U, ScriptHelper.ProgramDTouch);
        Assert.Equal(0, runner.SharedProgramStateForTests.Result);
        Assert.Equal(0u, gameState.GetFlag(11) & 1u << 11);
    }
}
