#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.l2 L2-1 (docs/plan-e19-opcodes.md, section 1.2m.2): opcode 0x9F, "is the chest opened", through the real interpreter.
/// Binary 0x80040C80: the first match of the search v1 decides (its ContentsGameFlag non-zero and set), and the handler ALWAYS
/// returns its size (2): it never suspends - the wait is the shape of the scripts (<c>00; 9F; 04</c>).
/// </summary>
public sealed class AlundraChestOpcodeTests
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
    }

    private static AlundraEntityScriptProxy Chest(int refId, int contentsGameFlag)
        => new() { EntityRefId = refId, Status = EntityStatus.Normal, ContentsGameFlag = contentsGameFlag };

    private static EventProgramState Run(AlundraGameState gameState, Context context)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x9F, 4, 0x1A, 9, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, gameState, context);
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = ResultBefore };
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId); // 0x1A 9 ran in the same call: 0x9F never suspends.
        return state;
    }

    [Fact]
    public void ChestOpened_FlagSet_AnswersOne()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(1192, 1u << (1192 & 31));
        var context = new Context();
        context.Spawned.Add(Chest(4, 1192));

        Assert.Equal(1, Run(gameState, context).Result);
    }

    [Fact]
    public void ChestOpened_FlagClear_AnswersZero()
    {
        var context = new Context();
        context.Spawned.Add(Chest(4, 1192));

        Assert.Equal(0, Run(new AlundraGameState(), context).Result);
    }

    [Fact]
    public void ChestOpened_NoContentsFlag_AnswersZero()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(0, 1);
        var context = new Context();
        context.Spawned.Add(Chest(4, 0));

        Assert.Equal(0, Run(gameState, context).Result);
    }

    [Fact]
    public void ChestOpened_NoMatch_AnswersZero()
    {
        var context = new Context();
        context.Spawned.Add(Chest(5, 1192));

        Assert.Equal(0, Run(new AlundraGameState(), context).Result);
    }

    [Fact]
    public void ChestOpened_TemporaryFlag_ReadsTheTemporaryBank()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(0x8005, 1u << 5);
        var context = new Context();
        context.Spawned.Add(Chest(4, 0x8005));

        Assert.Equal(1, Run(gameState, context).Result);
    }

    [Fact]
    public void ChestOpened_TwoMatches_OnlyTheFirstDecides()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(1192, 1u << (1192 & 31)); // only the second chest's flag is set.
        var context = new Context();
        context.Spawned.Add(Chest(4, 1193));
        context.Spawned.Add(Chest(4, 1192));

        Assert.Equal(0, Run(gameState, context).Result);

        // The reverse order: the first one's flag is set, the second's is not.
        var reversed = new Context();
        reversed.Spawned.Add(Chest(4, 1192));
        reversed.Spawned.Add(Chest(4, 1193));

        Assert.Equal(1, Run(gameState, reversed).Result);
    }
}
