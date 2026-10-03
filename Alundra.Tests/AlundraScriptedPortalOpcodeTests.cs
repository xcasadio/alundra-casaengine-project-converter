#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.l1 L1-1 (docs/plan-e19-opcodes.md, section 1.2m.1): opcode 0x52 (<c>0x8003EB20</c>), the portal under the hero taken by
/// a script. The warp keeps the hero's CURRENT animation and direction (the binary hands
/// <c>HandleWarpTransition</c> the hero's <c>TargetAnimationId</c> and <c>TargetDirection</c>); the walk onto a portal keeps
/// animation 0x36 (the existing director tests).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraScriptedPortalOpcodeTests : IDisposable
{
    public AlundraScriptedPortalOpcodeTests() => ResetSessionState();

    public void Dispose() => ResetSessionState();

    private static void ResetSessionState()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
    }

    private sealed class Context : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    private sealed class PortalHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; set; } = null!;
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public IReadOnlyList<AlundraPortalRecord> Portals { get; init; } = Array.Empty<AlundraPortalRecord>();
        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId) { }
        public int LogicTicksThisFrame(float elapsedTime) => 0;
    }

    private static AlundraPortalRecord PortalAround(int tileX, int tileY) => new()
    {
        Index = 0,
        X1 = tileX - 1,
        Y1 = tileY - 1,
        X2 = tileX + 1,
        Y2 = tileY + 1,
        DestMapId = 390,
        DestTileX = 10,
        DestTileY = 40,
        ZLevel = 0,
        Flags = 0x5001,
    };

    private static AlundraEntityScriptProxy Hero(int tileX, int tileY) => new()
    {
        IsPlayer = true,
        TileX = tileX,
        TileY = tileY,
        PosX = (tileX * 24 + 12) << 16,
        PosY = (tileY * 16 + 8) << 16,
        TargetAnimationId = 44,
        TargetDirection = 2,
    };

    private static (EventProgramState State, AlundraEventProgramRunner Runner) Run(
        AlundraEntityScriptProxy hero, IReadOnlyList<AlundraPortalRecord> portals, AlundraGameState gameState, int result, bool withHost = true)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x52, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, gameState, new Context { PlayerEntity = hero });
        if (withHost)
        {
            hero.ScriptHost = new PortalHost { Runner = runner, Portals = portals };
        }

        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = result };
        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);
        return (state, runner);
    }

    [Fact]
    public void UsePortalUnderHero_0x52_StartsTheDeparture_WithTheHerosCurrentAnimationAndDirection()
    {
        var hero = Hero(30, 20);

        var (state, _) = Run(hero, new[] { PortalAround(30, 20) }, new AlundraGameState(), result: 0);

        Assert.Equal(1, state.Result);
        Assert.Equal(1, state.CodeIndex);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        var record = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(44u, record.AnimationId);
        Assert.Equal(2u, record.DirectionId);
    }

    [Fact]
    public void UsePortalUnderHero_0x52_NoPortalUnderTheHero_GivesZero_AndNoDeparture()
    {
        var hero = Hero(5, 5);

        var (state, _) = Run(hero, new[] { PortalAround(30, 20) }, new AlundraGameState(), result: 1);

        Assert.Equal(0, state.Result);
        Assert.Equal(1, state.CodeIndex);
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);
    }

    [Fact]
    public void UsePortalUnderHero_0x52_WarpDisabled_StillGivesOne_ButNoDepartureStarts()
    {
        var hero = Hero(30, 20);

        var (state, _) = Run(hero, new[] { PortalAround(30, 20) }, new AlundraGameState { IsWarpDisabled = true }, result: 0);

        Assert.Equal(1, state.Result);
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);
    }

    [Fact]
    public void UsePortalUnderHero_0x52_WithoutAHost_GivesZero_Degraded()
    {
        var hero = Hero(30, 20);

        var (state, _) = Run(hero, new[] { PortalAround(30, 20) }, new AlundraGameState(), result: 1, withHost: false);

        Assert.Equal(0, state.Result);
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
    }

    [Fact]
    public void BeginDeparture_ByDefault_KeepsTheLoadingAnimation_0x36()
    {
        var hero = Hero(30, 20);

        AlundraWarpDirector.Instance.BeginDeparture(PortalAround(30, 20), 0x10, hero, new AlundraGameState());

        var record = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(0x36u, record.AnimationId);
        Assert.Equal(0x10u, record.DirectionId);
    }
}
