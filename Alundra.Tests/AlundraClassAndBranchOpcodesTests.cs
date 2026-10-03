#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.l1 L1-1 (docs/plan-e19-opcodes.md, section 1.2m.1): the class opcodes 0x28/0x29/0x2A/0x2B, the riding predicate 0x3F,
/// the restart 0x4A, the direction branches 0x57/0x58 and the deactivation 0x5D, through the real interpreter on synthetic
/// bytecode. Every value is the binary's (handlers 0x8003DC24-0x8003DC6C, 0x8003E734, 0x8003E9EC, 0x8003EE28/0x8003EE5C,
/// 0x8003F144).
/// </summary>
public sealed class AlundraClassAndBranchOpcodesTests
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
        AlundraEntityScriptProxy entity,
        IEntityWorldContext? context = null,
        int result = ResultBefore,
        int codeIndex = 0,
        int parameter0 = 0,
        List<EventTraceRecord>? trace = null)
    {
        var document = Document(codes);
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);
        if (trace != null)
        {
            runner.TraceSink = trace.Add;
        }

        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = result, CodeIndex = codeIndex };
        state.Parameters[0] = parameter0;
        runner.RunOneScriptCall(entity, state);
        return state;
    }

    // ---------------------------------------------------------------------------------------
    // 0x28 / 0x29 / 0x2A / 0x2B - the class bits
    // ---------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy EntityWithController(uint flags, uint mask)
    {
        var entity = NewEntity();
        entity.Flags = flags;
        entity.Controller = new CharacterControllerComponent();
        entity.Controller.Settings.WalkabilityMask = mask;
        return entity;
    }

    [Fact]
    public void SetClassA_0x2A_SetsTheBit_AndTheControllerMaskFollows()
    {
        var entity = EntityWithController(0x108, 0x40);

        var state = Run(new[] { 0x2A, 0xFF }, entity);

        Assert.Equal(0x109u, entity.Flags);
        Assert.Equal(0x1041u, entity.Controller!.Settings.WalkabilityMask); // ClassB (0x08) still set: 0x40 | 0x01 | 0x1000.
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(ResultBefore, state.Result);
    }

    [Fact]
    public void ClearClassA_0x2B_ClearsTheBit_AndTheControllerMaskFollows()
    {
        var entity = EntityWithController(0x101, 0x1040);

        var state = Run(new[] { 0x2B, 0xFF }, entity);

        Assert.Equal(0x100u, entity.Flags);
        Assert.Equal(0x40u, entity.Controller!.Settings.WalkabilityMask);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void SetClassB_0x28_SetsTheBit_AndTheControllerMaskFollows()
    {
        var entity = EntityWithController(0x100, 0x40);

        var state = Run(new[] { 0x28, 0xFF }, entity);

        Assert.Equal(0x108u, entity.Flags);
        Assert.Equal(0x41u, entity.Controller!.Settings.WalkabilityMask);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void ClearClassB_0x29_ClearsTheBit_AndTheControllerMaskFollows()
    {
        var entity = EntityWithController(0x109, 0x1041);

        var state = Run(new[] { 0x29, 0xFF }, entity);

        Assert.Equal(0x101u, entity.Flags);
        Assert.Equal(0x1040u, entity.Controller!.Settings.WalkabilityMask);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void TheClassOpcodes_WithoutAController_OnlyTouchTheFlags()
    {
        var entity = NewEntity();
        entity.Flags = 0x100;

        Run(new[] { 0x2A, 0x28, 0xFF }, entity);

        Assert.Equal(0x109u, entity.Flags);
    }

    // ---------------------------------------------------------------------------------------
    // 0x3F - is any entity riding me (hero included, Normal or Deactivated, not blocked)
    // ---------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy Rider(AlundraEntityScriptProxy carried, EntityStatus status)
        => new() { Status = status, RidingEntity = carried.LogicContextEntity };

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_ANormalRider_GivesOne()
    {
        var entity = NewEntity();
        var context = new Context();
        context.Spawned.Add(Rider(entity, EntityStatus.Normal));

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 0);

        Assert.Equal(1, state.Result);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_ADeactivatedRider_GivesOne()
    {
        var entity = NewEntity();
        var context = new Context();
        context.Spawned.Add(Rider(entity, EntityStatus.Deactivated));

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 0);

        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_ALoadedRider_GivesZero_TheBinaryNotTheDecompilation()
    {
        var entity = NewEntity();
        var context = new Context();
        context.Spawned.Add(Rider(entity, EntityStatus.Loaded));

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 1);

        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_TheHeroCarried_GivesOne()
    {
        var entity = NewEntity();
        var context = new Context { PlayerEntity = Rider(entity, EntityStatus.Normal) };
        context.PlayerEntity.IsPlayer = true;

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 0);

        Assert.Equal(1, state.Result);
    }

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_ABlockedRider_GivesZero()
    {
        var entity = NewEntity();
        var rider = Rider(entity, EntityStatus.Normal);
        rider.BlockedByEntity = new Entity { Name = "blocker" };
        var context = new Context();
        context.Spawned.Add(rider);

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 1);

        Assert.Equal(0, state.Result);
    }

    [Fact]
    public void IsAnyEntityRidingMe_0x3F_NobodyRiding_GivesZero()
    {
        var entity = NewEntity();
        var context = new Context();
        context.Spawned.Add(new AlundraEntityScriptProxy { Status = EntityStatus.Normal, RidingEntity = new Entity { Name = "other" } });

        var state = Run(new[] { 0x3F, 0xFF }, entity, context, result: 1);

        Assert.Equal(0, state.Result);
        Assert.Equal(1, state.CodeIndex);
    }

    // ---------------------------------------------------------------------------------------
    // 0x4A - if true restart
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void IfTrueRestart_0x4A_ResultNonZero_JumpsBackToTheProgramStart()
    {
        var trace = new List<EventTraceRecord>();

        var state = Run(new[] { 0x00, 0x4A, 0xFF }, NewEntity(), result: 1, codeIndex: 1, parameter0: 0, trace: trace);

        Assert.Equal(1, state.CodeIndex);
        Assert.Contains(trace, record => record.Opcode == 0x00 && record.CodeIndex == 0);
    }

    [Fact]
    public void IfTrueRestart_0x4A_ResultZero_Advances()
    {
        var state = Run(new[] { 0x00, 0x4A, 0xFF }, NewEntity(), result: 0, codeIndex: 1, parameter0: 0);

        Assert.Equal(2, state.CodeIndex);
    }

    // ---------------------------------------------------------------------------------------
    // 0x57 / 0x58 - goto by animation direction, never a fall-through
    // ---------------------------------------------------------------------------------------

    private static readonly int[] DirectionalBranch =
    {
        0x58, 9, 0, 12, 0, 15, 0, 18, 0, 0x1A, 1, 0xFF, 0x1A, 2, 0xFF, 0x1A, 3, 0xFF, 0x1A, 4, 0xFF,
    };

    [Theory]
    [InlineData(0, 1u, 11)]
    [InlineData(1, 2u, 14)]
    [InlineData(2, 3u, 17)]
    [InlineData(3, 4u, 20)]
    public void DirectionalBranch_0x58_JumpsToTheTargetOfTheEntitysDirection(int direction, uint animation, int codeIndex)
    {
        var entity = NewEntity();
        entity.AnimationDirection = direction;

        var state = Run(DirectionalBranch, entity);

        Assert.Equal(animation, entity.TargetAnimationId);
        Assert.Equal(codeIndex, state.CodeIndex);
    }

    [Fact]
    public void DirectionalBranch_0x58_ANegativeOffsetJumpsBackwards()
    {
        var codes = new[] { 0xFF, 0x58, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        var state = Run(codes, NewEntity(), codeIndex: 1);

        Assert.Equal(0, state.CodeIndex);
    }

    [Fact]
    public void GotoByHeroDirection_0x57_ReadsTheHerosDirection_NotTheEntitys()
    {
        var entity = NewEntity();
        entity.AnimationDirection = 0;
        var hero = new AlundraEntityScriptProxy { IsPlayer = true, Status = EntityStatus.Normal, AnimationDirection = 2 };
        var context = new Context { PlayerEntity = hero };
        var codes = (int[])DirectionalBranch.Clone();
        codes[0] = 0x57;

        var state = Run(codes, entity, context);

        Assert.Equal(3u, entity.TargetAnimationId);
        Assert.Equal(17, state.CodeIndex);
    }

    // ---------------------------------------------------------------------------------------
    // 0x5D - every match goes to state 3
    // ---------------------------------------------------------------------------------------

    private static AlundraEntityScriptProxy Npc(int refId, EntityStatus status = EntityStatus.Normal)
        => new() { EntityRefId = refId, Status = status };

    [Fact]
    public void DeactivateMatching_0x5D_OneMatch_BecomesDeactivated()
    {
        var npc = Npc(10);
        var context = new Context();
        context.Spawned.Add(npc);

        var state = Run(new[] { 0x5D, 10, 0xFF }, NewEntity(), context);

        Assert.Equal(EntityStatus.Deactivated, npc.Status);
        Assert.Equal(2, state.CodeIndex);
    }

    [Fact]
    public void DeactivateMatching_0x5D_TwoMatches_BothBecomeDeactivated_AndOthersStay()
    {
        var first = Npc(10);
        var second = Npc(10);
        var other = Npc(11);
        var context = new Context();
        context.Spawned.AddRange(new[] { first, second, other });

        Run(new[] { 0x5D, 10, 0xFF }, NewEntity(), context);

        Assert.Equal(EntityStatus.Deactivated, first.Status);
        Assert.Equal(EntityStatus.Deactivated, second.Status);
        Assert.Equal(EntityStatus.Normal, other.Status);
    }

    [Fact]
    public void DeactivateMatching_0x5D_NoMatch_ChangesNothing()
    {
        var other = Npc(11);
        var context = new Context();
        context.Spawned.Add(other);

        var state = Run(new[] { 0x5D, 10, 0xFF }, NewEntity(), context);

        Assert.Equal(EntityStatus.Normal, other.Status);
        Assert.Equal(2, state.CodeIndex);
    }
}
