#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.l2 L2-1 (docs/plan-e19-opcodes.md, section 1.2m.2): the item, money and chest-flag opcodes 0x82, 0x83, 0x84, 0x98, 0x99,
/// 0x9A, through the real interpreter on synthetic bytecode (<c>[op, ..., 0x1A, 9, 0xFF]</c>: animation 9 proves the opcode
/// did not suspend). Every value is the binary's (handlers 0x8003FE7C, 0x8003FEC8, 0x8003FF34, 0x80040A58, 0x80040A8C,
/// 0x80040B00; HandleMapTriggerCommand 0x80034108, AddOneItemIfUnlocked 0x8004E530, UseItem 0x8004E5C4, SetMoney 0x8004DF80).
/// </summary>
public sealed class AlundraItemAndMoneyOpcodesTests
{
    private const int ResultBefore = 7;

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
        public AlundraItemTables? Tables { get; set; }
        public AlundraItemTables? ItemTables => Tables;
    }

    private static readonly AlundraItemTables RealTables = LoadTables();

    /// <summary>The fixture's real rows plus the five items these tests touch, with the exported
    /// <c>alundra-project/Data/items-properties.json</c> values (key 61 max 99, 36 max 9, 41 max 1, 38 max 1, 88 max 1).</summary>
    private static AlundraItemTables LoadTables()
    {
        var rows = ItemTablesFixture.RealProperties();
        rows[36] = new[] { 12, 0, 0, 9, 66 };
        rows[38] = new[] { 15, 0, 0, 1, 68 };
        rows[41] = new[] { 14, 0, 0, 1, 71 };
        rows[61] = new[] { 0, 0, 0, 99, 91 };
        rows[88] = new[] { 28, 0, 0, 1, 118 };
        var projectPath = ItemTablesFixture.Write(rows, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        try
        {
            return new AlundraItemTables(projectPath);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    private static EventProgramDocument Document(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private static AlundraEntityScriptProxy NewEntity()
        => new() { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };

    private static EventProgramState Run(
        int[] codes,
        AlundraGameState gameState,
        IEntityWorldContext? context = null,
        List<EventTraceRecord>? trace = null)
    {
        var document = Document(codes);
        var runner = new AlundraEventProgramRunner(document, gameState, context ?? new Context { Tables = RealTables });
        if (trace != null)
        {
            runner.TraceSink = trace.Add;
        }

        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = ResultBefore };
        var entity = NewEntity();
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId); // 0x1A 9 ran in the same call: the opcode did not suspend.
        return state;
    }

    private static AlundraGameState StateWithMoney(int money)
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Money = (short)money;
        return gameState;
    }

    private static int Count(AlundraGameState gameState, int itemId) => gameState.NumberOfItems[itemId * 2 + 1];

    // ---------------------------------------------------------------------------------------
    // The helpers
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AddMoney_ComputesOn32Bits_ThenClampsTo0And9999()
    {
        var gameState = StateWithMoney(9990);

        Assert.Equal(9999, AlundraPlayerManager.AddMoney(gameState, 65535));
        Assert.Equal(9999, gameState.PlayerStats.Money);

        gameState.PlayerStats.Money = 5;
        Assert.Equal(15, AlundraPlayerManager.AddMoney(gameState, 10));
        Assert.Equal(0, AlundraPlayerManager.AddMoney(gameState, -100));
    }

    [Fact]
    public void UseItem_InvalidId_Returns0_ZeroCount_ReturnsMinus1_ElseDecrements()
    {
        var gameState = new AlundraGameState();
        gameState.NumberOfItems[88 * 2 + 1] = 2;

        Assert.Equal(0, AlundraPlayerManager.UseItem(gameState, 99));
        Assert.Equal(0, AlundraPlayerManager.UseItem(gameState, -1));
        Assert.Equal(-1, AlundraPlayerManager.UseItem(gameState, 5));
        Assert.Equal(1, AlundraPlayerManager.UseItem(gameState, 88));
        Assert.Equal(1, Count(gameState, 88));
    }

    // ---------------------------------------------------------------------------------------
    // 0x82 - handle map trigger (give item or pickup by id)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Give_0x82_Item36AtCount0_GivesOne()
    {
        var gameState = new AlundraGameState();

        var state = Run(new[] { 0x82, 36, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(1, gameState.NumberOfItems[73]);
    }

    [Fact]
    public void Give_0x82_Item36AtCount8_GivesTheLastOne_ThenRefuses()
    {
        var gameState = new AlundraGameState();
        gameState.NumberOfItems[73] = 8;

        var first = Run(new[] { 0x82, 36, 0x1A, 9, 0xFF }, gameState);
        Assert.Equal(1, first.Result);
        Assert.Equal(9, gameState.NumberOfItems[73]);

        var second = Run(new[] { 0x82, 36, 0x1A, 9, 0xFF }, gameState);
        Assert.Equal(0, second.Result);
        Assert.Equal(9, gameState.NumberOfItems[73]);
    }

    [Fact]
    public void Give_0x82_Item41AtItsMaximum_Refuses_BecauseTheBinaryReturnsTheCountNotTheId()
    {
        var gameState = new AlundraGameState();
        gameState.NumberOfItems[41 * 2 + 1] = 1;

        var state = Run(new[] { 0x82, 41, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(0, state.Result);
        Assert.Equal(1, Count(gameState, 41));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Give_0x82_Id0_Or_AnIdOutOfTheTable_AnswersZero_AndWritesNothing(int id)
    {
        var gameState = new AlundraGameState();

        var state = Run(new[] { 0x82, id, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(0, state.Result);
        Assert.All(gameState.NumberOfItems, count => Assert.Equal(0, count));
        Assert.Equal(0, gameState.PlayerStats.Money);
    }

    [Fact]
    public void Give_0x82_Id0_AnswersZero_EvenWhenTheItem0HasAMaximum()
    {
        // Tables where the item 0 has a maximum of 1: the default branch (AddOneItemIfUnlocked) would give it, so only the explicit "case 0" of the binary
        // (jump table 0x80023234, entry 0 = 0x8003413C: returns 0) answers 0 here. With the real tables (maximum 0) both paths answer 0.
        var rows = ItemTablesFixture.RealProperties();
        rows[0] = new[] { 0, 0, 0, 1, 65535 };
        var projectPath = ItemTablesFixture.Write(rows, ItemTablesFixture.RealDrops(), ItemTablesFixture.RealIcons());
        AlundraItemTables tables;
        try
        {
            tables = new AlundraItemTables(projectPath);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }

        Assert.Equal(1, AlundraPlayerManager.AddOneItemIfUnlocked(new AlundraGameState(), tables, 0)); // the montage discriminates: the default branch would give it.

        var gameState = new AlundraGameState();
        var state = Run(new[] { 0x82, 0, 0x1A, 9, 0xFF }, gameState, new Context { Tables = tables });

        Assert.Equal(0, state.Result);
        Assert.All(gameState.NumberOfItems, count => Assert.Equal(0, count));
        Assert.Equal(0, gameState.PlayerStats.Money);
    }

    [Fact]
    public void Give_0x82_Id0x47_AddsTenGold_ClampedTo9999()
    {
        var gameState = StateWithMoney(9995);

        var state = Run(new[] { 0x82, 0x47, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(9999, gameState.PlayerStats.Money);
    }

    [Fact]
    public void Give_0x82_Id0x48_AddsThirtyGold()
    {
        var gameState = StateWithMoney(0);

        var state = Run(new[] { 0x82, 0x48, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(30, gameState.PlayerStats.Money);
    }

    [Theory]
    [InlineData(0x45, 1)]
    [InlineData(0x46, 5)]
    public void Give_0x82_Id0x45And0x46_AddOneAndFiveGold(int id, int gold)
    {
        var gameState = StateWithMoney(100);

        var state = Run(new[] { 0x82, id, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(100 + gold, gameState.PlayerStats.Money);
    }

    [Fact]
    public void Give_0x82_Id0x4F_RaisesFalconTemp_AndSetsG1450()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.FalconTemp = 49;

        var state = Run(new[] { 0x82, 0x4F, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(50, gameState.PlayerStats.FalconTemp);
        Assert.NotEqual(0u, gameState.GameFlags[45] & 0x400);
    }

    [Fact]
    public void Give_0x82_Id0x4F_AtTheCap_StaysAt50_AndStillAnswersOne()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.FalconTemp = 50;

        var state = Run(new[] { 0x82, 0x4F, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(50, gameState.PlayerStats.FalconTemp);
    }

    [Fact]
    public void Give_0x82_Id0x53_AnswersOne_WritesNothing_AndTracesDegraded()
    {
        var gameState = new AlundraGameState();
        var trace = new List<EventTraceRecord>();

        var state = Run(new[] { 0x82, 0x53, 0x1A, 9, 0xFF }, gameState, trace: trace);

        Assert.Equal(1, state.Result);
        Assert.All(gameState.NumberOfItems, count => Assert.Equal(0, count));
        Assert.Equal(10, gameState.PlayerStats.HpMax);
        Assert.Equal(EventTraceKind.Degraded, trace.First(record => record.Opcode == 0x82).Kind);
    }

    [Fact]
    public void Give_0x82_AnItem_WithoutTables_IsDegraded_ResultZero_NothingWritten()
    {
        var gameState = new AlundraGameState();
        var trace = new List<EventTraceRecord>();

        var state = Run(new[] { 0x82, 36, 0x1A, 9, 0xFF }, gameState, new Context(), trace);

        Assert.Equal(0, state.Result);
        Assert.Equal(0, gameState.NumberOfItems[73]);
        Assert.Equal(EventTraceKind.Degraded, trace.First(record => record.Opcode == 0x82).Kind);
    }

    // ---------------------------------------------------------------------------------------
    // 0x83 / 0x84 - count test and use
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(3, 4, 0)]
    [InlineData(2, 0, 1)]
    public void HasCount_0x83_IsCountAtLeastV2(int count, int v2, int expected)
    {
        var gameState = new AlundraGameState();
        gameState.NumberOfItems[61 * 2 + 1] = (short)count;

        var state = Run(new[] { 0x83, 61, v2, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(expected, state.Result);
        Assert.Equal(count, Count(gameState, 61));
    }

    [Fact]
    public void HasCount_0x83_IdOutOfTheTable_AnswersZero()
    {
        var state = Run(new[] { 0x83, 99, 1, 0x1A, 9, 0xFF }, new AlundraGameState());

        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void Use_0x84_Item88AtCount1_ConsumesIt_AnswersOne()
    {
        var gameState = new AlundraGameState();
        gameState.NumberOfItems[88 * 2 + 1] = 1;

        var state = Run(new[] { 0x84, 88, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.Equal(0, Count(gameState, 88));
    }

    [Fact]
    public void Use_0x84_Item88AtCount0_AnswersZero_AndWritesNothing()
    {
        var gameState = new AlundraGameState();

        var state = Run(new[] { 0x84, 88, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(0, state.Result);
        Assert.Equal(0, Count(gameState, 88));
    }

    [Fact]
    public void Use_0x84_AnInvalidId_AnswersOne_AsTheBinaryDoes_AndWritesNothing()
    {
        var gameState = new AlundraGameState();

        var state = Run(new[] { 0x84, 99, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(1, state.Result);
        Assert.All(gameState.NumberOfItems, count => Assert.Equal(0, count));
    }

    // ---------------------------------------------------------------------------------------
    // 0x98 / 0x99 / 0x9A - money
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AddMoney_0x98_Adds_AndLeavesResultAlone()
    {
        var gameState = StateWithMoney(0);

        var state = Run(new[] { 0x98, 10, 0, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(10, gameState.PlayerStats.Money);
        Assert.Equal(ResultBefore, state.Result);
    }

    [Fact]
    public void AddMoney_0x98_ClampsTo9999_WithoutWrappingTheShort()
    {
        var gameState = StateWithMoney(9990);

        Run(new[] { 0x98, 0xFF, 0xFF, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(9999, gameState.PlayerStats.Money);
    }

    [Theory]
    [InlineData(5, 5, 0, 0, 1)]
    [InlineData(4, 5, 0, 4, 0)]
    [InlineData(2163, 0x73, 0x08, 0, 1)]
    [InlineData(2162, 0x73, 0x08, 2162, 0)]
    [InlineData(9999, 0xFF, 0xFF, 9999, 0)]
    public void Spend_0x99_TakesThePrice_OnlyWhenTheMoneyCoversIt(int money, int lo, int hi, int moneyAfter, int result)
    {
        var gameState = StateWithMoney(money);

        var state = Run(new[] { 0x99, lo, hi, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(result, state.Result);
        Assert.Equal(moneyAfter, gameState.PlayerStats.Money);
    }

    [Theory]
    [InlineData(15, 15, 0, 1)]
    [InlineData(14, 15, 0, 0)]
    [InlineData(9999, 0xFF, 0xFF, 0)]
    public void Enough_0x9A_ComparesWithoutWriting(int money, int lo, int hi, int result)
    {
        var gameState = StateWithMoney(money);

        var state = Run(new[] { 0x9A, lo, hi, 0x1A, 9, 0xFF }, gameState);

        Assert.Equal(result, state.Result);
        Assert.Equal(money, gameState.PlayerStats.Money);
    }
}
