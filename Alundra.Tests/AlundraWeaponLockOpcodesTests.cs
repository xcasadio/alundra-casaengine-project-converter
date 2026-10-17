#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using static Alundra.Tests.AlundraStoryChainOpcodeAudit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;

namespace Alundra.Tests;

/// <summary>
/// E20.f0 T1 and T2 (docs/plan-e20-heros.md section 1.8): the scripted weapon opcodes 0xC0 (Script_192_0C0 @ 0x80041CDC, size 2) and 0xC1
/// (Script_193_0C1 @ 0x80041D18, size 1), and the whole-value gates of the inventory (0x8002BB68), the warp (0x8002F1C4) that the bit 0x80 shuts.
/// Expected values are written from the disassembly of ALUN_CD.EXE (France), never read back from the code under test:
/// 0xC0 = SetPlayerWeaponId(v1 + 1) then [0x800DC4B8] |= 0x80, returns 2; 0xC1 = [0x800DC4B8] &amp;= ~0x80, returns 1; Result untouched.
/// </summary>
public sealed class AlundraWeaponLockOpcodesTests
{
    private const int ResultBefore = 7;
    private const uint Lock = AlundraGameState.PlayerControlBits.ForcedWeapon;

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

    private static readonly AlundraItemTables RealTables = new(SaveGameDirectorTestSupport.FindProjectRoot());

    private static AlundraEntityScriptProxy NewEntity()
        => new() { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };

    /// <summary>Runs the instruction bytes followed by <c>0x1A 9 0xFF</c>: animation 9 proves the opcode consumed exactly its size.</summary>
    private static (EventProgramState State, EventTraceRecord First) Run(AlundraGameState gameState, AlundraItemTables? tables, params int[] instruction)
    {
        var codes = instruction.Concat(new[] { 0x1A, 9, 0xFF }).ToArray();
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new int[6], Codes = codes };
        var runner = new AlundraEventProgramRunner(document, gameState, new Context { Tables = tables });
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = ResultBefore };
        var entity = NewEntity();
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId);
        return (state, trace.First(t => t.CodeIndex == 0));
    }

    private static AlundraGameState Owning(params int[] items)
    {
        var gameState = new AlundraGameState();
        foreach (var item in items)
        {
            gameState.NumberOfItems[item * 2 + 1] = 1;
        }

        return gameState;
    }

    // ---------------------------------------------------------------------------------------
    // The real bytes of the four sites (docs/plan-e20-annexe/e20-armes/f-check.md rows 37 and 39)
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(472, 327, 0xC0, 0)]
    [InlineData(472, 378, 0xC1, -1)]
    [InlineData(473, 198, 0xC0, 1)]
    [InlineData(473, 289, 0xC1, -1)]
    public void TheRealBytesOfMaps472And473_HoldTheOpcodeAndItsOperand(int map, int pc, int opcode, int operand)
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), map);

        Assert.Equal(opcode, document.Codes[pc]);
        if (operand >= 0)
        {
            Assert.Equal(operand, document.Codes[pc + 1]);
        }
    }

    [Fact]
    public void TheRealBytesOf472Site327_EquipSlot1_AndPoseBit0x80_OnTheRealTables()
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), 472);
        var gameState = Owning(1);

        var (state, first) = Run(gameState, RealTables, document.Codes[327], document.Codes[328]);

        Assert.Equal((short)1, gameState.PlayerStats.WeaponId);
        Assert.Equal(1u, AlundraPlayerManager.GetItemIdFromCurrentWeapon(gameState, RealTables));
        Assert.Equal(0x80u, gameState.PlayerControlFlags);
        Assert.Equal(ResultBefore, state.Result);
        Assert.Equal((0xC0, 2), (first.Opcode, first.Size));
    }

    [Fact]
    public void TheRealBytesOf473Site198_EquipSlot2_TheHuntersBow_AndPoseBit0x80()
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), 473);
        var gameState = Owning(1, 5);

        var (state, first) = Run(gameState, RealTables, document.Codes[198], document.Codes[199]);

        Assert.Equal((short)2, gameState.PlayerStats.WeaponId);
        Assert.Equal(5u, AlundraPlayerManager.GetItemIdFromCurrentWeapon(gameState, RealTables));
        Assert.Equal(0x80u, gameState.PlayerControlFlags);
        Assert.Equal(ResultBefore, state.Result);
        Assert.Equal((0xC0, 2), (first.Opcode, first.Size));
    }

    [Theory]
    [InlineData(472, 378)]
    [InlineData(473, 289)]
    public void TheRealBytesOfTheTwo0xC1Sites_ClearBit0x80_AndNothingElse(int map, int pc)
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), map);
        var gameState = new AlundraGameState { PlayerControlFlags = 0x80 | 0x04 };

        var (state, first) = Run(gameState, RealTables, document.Codes[pc]);

        Assert.Equal(0x04u, gameState.PlayerControlFlags);
        Assert.Equal(ResultBefore, state.Result);
        Assert.Equal((0xC1, 1), (first.Opcode, first.Size));
    }

    [Theory]
    [InlineData(472, 5)]
    [InlineData(473, 1)]
    public void TheWholeProgramB_OfTheTwoMaps_HasNoSkippedOpcode(int map, int program)
    {
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), map);

        var reached = WalkMap(map, document, new[] { ('B', program) });

        Assert.Contains(reached, r => r.Opcode == 0xC0);
        Assert.Contains(reached, r => r.Opcode == 0xC1);
        Assert.Empty(reached.Where(r => !IsPorted(r.Opcode)).Select(r => $"0x{r.Opcode:X2} @{r.Site.Pc}").Distinct());
    }

    // ---------------------------------------------------------------------------------------
    // The port itself
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Opcode0xC0_AddsOneToTheOperand_KeepsTheOtherBits_AndReturns2()
    {
        // Operand 5 -> slot 6, the Magic Staff (item 7); the bit 0x04 posed before stays.
        var gameState = Owning(7);
        gameState.PlayerControlFlags = 0x04;

        var (state, first) = Run(gameState, RealTables, 0xC0, 5);

        Assert.Equal((short)6, gameState.PlayerStats.WeaponId);
        Assert.Equal(7u, AlundraPlayerManager.GetItemIdFromCurrentWeapon(gameState, RealTables));
        Assert.Equal(0x84u, gameState.PlayerControlFlags);
        Assert.Equal(ResultBefore, state.Result);
        Assert.Equal(2, first.Size);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(255)]
    public void Opcode0xC0_AnOperandPastSlot6_IsRejectedBySetPlayerWeaponId_ButTheBitIsStillPosed(int operand)
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.WeaponId = 3;

        Run(gameState, RealTables, 0xC0, operand);

        Assert.Equal((short)3, gameState.PlayerStats.WeaponId);
        Assert.Equal(0x80u, gameState.PlayerControlFlags);
    }

    [Fact]
    public void Opcode0xC0_WithoutItemTables_StillPosesTheBit_AndConsumesItsSize()
    {
        var gameState = new AlundraGameState();

        var (_, first) = Run(gameState, null, 0xC0, 0);

        Assert.Equal(0x80u, gameState.PlayerControlFlags);
        Assert.Equal(2, first.Size);
    }

    [Fact]
    public void Opcode0xC1_OnAClearBit_ChangesNothing()
    {
        var gameState = new AlundraGameState { PlayerControlFlags = 0x10 };

        var (_, first) = Run(gameState, RealTables, 0xC1);

        Assert.Equal(0x10u, gameState.PlayerControlFlags);
        Assert.Equal(1, first.Size);
    }
}
