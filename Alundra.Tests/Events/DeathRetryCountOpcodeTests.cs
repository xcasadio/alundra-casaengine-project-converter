#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.b2 T2 (docs/plan-e20-heros.md section 1.4.1, D-B2-2): opcode 0xC2, "has the player resumed after a death at least N times", through the real interpreter. Binary 0x80041D34 (disassembled): <c>lbu</c> of the one
/// parameter byte and <c>lbu</c> of the counter <c>0x801EBA3E</c> (the byte <c>g_saveData + 0x756</c>, <see cref="GameState.DeathRetryCount"/>), <c>slt counter, parameter</c>: <c>Result = (counter &gt;= parameter) ? 1 : 0</c>, both
/// unsigned, written whatever the previous value; it returns 2 (one parameter byte). The scene of the Snow King (map 7, <c>B[10]</c> @1508) is <c>0xC2 [20]</c> then <c>0x03</c>. The expected values are the plan's (19, 20, 21).
/// </summary>
public sealed class DeathRetryCountOpcodeTests
{
    private sealed class Context : IEntityWorldContext
    {
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities { get; } = new List<EntityScriptProxy>();
        public EntityScriptProxy? PlayerEntity { get; set; }
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    /// <summary>
    /// Runs <c>0xC2 [parameter]</c> then <c>0x1A 9</c> (set animation 9) in one call, with <see cref="EventProgramState.Result"/> preloaded to <paramref name="resultBefore"/>, the value OPPOSITE to the expected answer, so the test is red
    /// whatever stale value the program state holds when the opcode is not run. The animation 9 proves that the opcode consumed exactly two bytes.
    /// </summary>
    private static (int Result, uint Animation) Run(int counter, int parameter, int resultBefore)
    {
        var gameState = new GameState { DeathRetryCount = (byte)counter };
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0xC2, parameter, 0x1A, 9, 0xFF },
        };
        var runner = new EventProgramRunner(document, gameState, new Context());
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = resultBefore };
        var entity = new EntityScriptProxy { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };

        runner.RunOneScriptCall(entity, state);

        return (state.Result, entity.TargetAnimationId);
    }

    [Fact]
    public void Opcode0xC2_Param20_WithTheCounterAt19_AnswersZero_OverAStaleOne()
    {
        var (result, animation) = Run(counter: 19, parameter: 20, resultBefore: 1);

        Assert.Equal(0, result);
        Assert.Equal(9u, animation);
    }

    [Fact]
    public void Opcode0xC2_Param20_WithTheCounterAt20_AnswersOne_OverAStaleZero()
    {
        var (result, animation) = Run(counter: 20, parameter: 20, resultBefore: 0);

        Assert.Equal(1, result);
        Assert.Equal(9u, animation);
    }

    [Fact]
    public void Opcode0xC2_Param20_WithTheCounterAt21_AnswersOne_OverAStaleZero()
    {
        var (result, animation) = Run(counter: 21, parameter: 20, resultBefore: 0);

        Assert.Equal(1, result);
        Assert.Equal(9u, animation);
    }

    /// <summary>Both operands are loaded with <c>lbu</c>: a counter of 200 is 200, not -56, so it is at least 20; and the parameter 255 is not -1.</summary>
    [Fact]
    public void Opcode0xC2_ComparesTwoUnsignedBytes()
    {
        Assert.Equal(1, Run(counter: 200, parameter: 20, resultBefore: 0).Result);
        Assert.Equal(0, Run(counter: 200, parameter: 255, resultBefore: 1).Result);
        Assert.Equal(1, Run(counter: 255, parameter: 255, resultBefore: 0).Result);
        Assert.Equal(1, Run(counter: 0, parameter: 0, resultBefore: 0).Result);
    }

    /// <summary>One parameter byte: the instruction that follows (<c>0x1A 9</c>) runs in the same call, so the pc advanced by exactly 2.</summary>
    [Fact]
    public void Opcode0xC2_TakesOneParameterByte_ThePcAdvancesByTwo()
    {
        var (_, animation) = Run(counter: 20, parameter: 20, resultBefore: 0);

        Assert.Equal(9u, animation);
    }
}
