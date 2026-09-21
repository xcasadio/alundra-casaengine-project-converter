#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.d D2 (docs/plan-e13d-inventaire.md, D-E13D-15): <see cref="AlundraGameplayFreeze"/> - the engine's
/// own gravity integration and sprite animation frozen while GameplayBlockedMask is posed, and given back
/// exactly. The production call sites (a real hero, a real ladder, a real frame with zero logic ticks) are
/// in <see cref="AlundraLadderClimbTests"/>; the world-level wiring is the last test here.
/// </summary>
public sealed class AlundraGameplayFreezeTests : IDisposable
{
    public AlundraGameplayFreezeTests()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static readonly CharacterControllerStateSnapshot MidJump = new(
        Position: Vector3.Zero,
        Orientation: Quaternion.Identity,
        ControlMode: CharacterControlMode.Player,
        MovementState: CharacterMovementState.Jumping,
        Velocity: new Vector3(3f, -2f, 150f),
        MoveIntent: new Vector2(1f, 0f),
        JumpRequested: false,
        JumpBufferRemainingSeconds: 0.05f,
        CoyoteTimeRemainingSeconds: 0.08f,
        DashRequested: false,
        DashRequestedDirection: Vector2.Zero,
        DashDirection: Vector2.Zero,
        DashRemainingSeconds: 0f,
        DashCooldownRemainingSeconds: 0.3f,
        IsGrounded: false,
        GroundNormal: Vector3.UnitZ,
        GroundSlopeAngle: 0f,
        LastRequestedDisplacement: new Vector3(0f, 0f, 3f),
        LastActualDisplacement: new Vector3(0f, 0f, 3f));

    private static AlundraEntityScriptProxy NewProxyWithController(CharacterControllerStateSnapshot state)
    {
        var controller = new CharacterControllerComponent();
        controller.RestoreStateSnapshot(state);
        return new AlundraEntityScriptProxy { Controller = controller };
    }

    private static float ExternalVerticalLatch(CharacterControllerComponent controller)
    {
        var field = typeof(CharacterControllerComponent).GetField(
            "_externalVerticalDisplacement", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (float)field!.GetValue(controller)!;
    }

    [Fact]
    public void Freeze_DisablesTheController_ThawGivesBackItsWholeState()
    {
        var proxy = NewProxyWithController(MidJump);
        var before = proxy.Controller!.CaptureStateSnapshot();

        AlundraGameplayFreeze.Freeze(proxy);

        // SetControlMode(Disabled) goes through Stop(), which wipes the motion state - the reason for the
        // snapshot (plan §1.2).
        Assert.Equal(CharacterControlMode.Disabled, proxy.Controller.ControlMode);
        Assert.Equal(Vector3.Zero, proxy.Controller.Velocity);

        AlundraGameplayFreeze.Thaw(proxy);

        Assert.Equal(before, proxy.Controller.CaptureStateSnapshot());
        Assert.Equal(CharacterMovementState.Jumping, proxy.Controller.MovementState);
        Assert.Equal(new Vector3(3f, -2f, 150f), proxy.Controller.Velocity);
    }

    [Fact]
    public void Apply_FreezesOnce_ASecondBlockedFrameDoesNotRecaptureTheWipedState()
    {
        var proxy = NewProxyWithController(MidJump);

        AlundraGameplayFreeze.Apply(proxy, gameplayBlocked: true);
        AlundraGameplayFreeze.Apply(proxy, gameplayBlocked: true);
        AlundraGameplayFreeze.Apply(proxy, gameplayBlocked: false);

        Assert.Equal(new Vector3(3f, -2f, 150f), proxy.Controller!.Velocity);
        Assert.False(proxy.FreezeState.IsFrozen);
    }

    [Fact]
    public void Apply_NeverBlocked_TouchesNothing()
    {
        var proxy = NewProxyWithController(MidJump);

        AlundraGameplayFreeze.Apply(proxy, gameplayBlocked: false);

        Assert.Equal(CharacterControlMode.Player, proxy.Controller!.ControlMode);
        Assert.Equal(new Vector3(3f, -2f, 150f), proxy.Controller.Velocity);
    }

    [Fact]
    public void Thaw_ControllerDrivenNpc_DeclaresItsOwnVerticalForceAgain()
    {
        // AlundraEntityScriptProxy.Update declares FinalForceZ / 65536 once per tick; Stop() and
        // RestoreStateSnapshot both zero the latch, and a thaw frame may carry no tick.
        var proxy = NewProxyWithController(MidJump);
        proxy.Controller!.IsVerticalOwnedExternally = true;
        proxy.FinalForceZ = 2 * 65536;
        proxy.Controller.SetExternalVerticalDisplacement(2f);

        AlundraGameplayFreeze.Freeze(proxy);
        Assert.Equal(0f, ExternalVerticalLatch(proxy.Controller));

        AlundraGameplayFreeze.Thaw(proxy);
        Assert.Equal(2f, ExternalVerticalLatch(proxy.Controller));
    }

    [Theory]
    [InlineData(AlundraPlayerManager.ClimbingAnimationId, AlundraScriptedMotion.ClimbingExternalDisplacementSentinel)]
    [InlineData(AlundraPlayerManager.ClimbStillAnimationId, AlundraScriptedMotion.ClimbingExternalDisplacementSentinel)]
    [InlineData(0u, 0f)]
    public void Thaw_Hero_DeclaresTheClimbingSentinelAgain_OnlyWhileClimbing(uint animationId, float expectedLatch)
    {
        var proxy = NewProxyWithController(MidJump);
        proxy.IsPlayer = true;
        proxy.TargetAnimationId = animationId;
        proxy.Controller!.IsVerticalOwnedExternally = true;
        proxy.Controller.SetExternalVerticalDisplacement(AlundraScriptedMotion.ClimbingExternalDisplacementSentinel);

        AlundraGameplayFreeze.Freeze(proxy);
        AlundraGameplayFreeze.Thaw(proxy);

        Assert.Equal(expectedLatch, ExternalVerticalLatch(proxy.Controller));
    }

    private static (AlundraEntityScriptProxy Proxy, AnimatedSpriteComponent Sprite) NewProxyWithSprite()
    {
        var root = new TransformComponent();
        var sprite = new AnimatedSpriteComponent();
        root.AddChildComponent(sprite);
        var entity = new Entity
        {
            Name = "FreezeTestEntity",
            RootComponent = root,
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        return (proxy, sprite);
    }

    [Fact]
    public void Freeze_PausesTheSpriteAnimation_ThawResumesIt()
    {
        var (proxy, sprite) = NewProxyWithSprite();

        AlundraGameplayFreeze.Freeze(proxy);
        Assert.True(sprite.IsPlaybackPaused);

        AlundraGameplayFreeze.Thaw(proxy);
        Assert.False(sprite.IsPlaybackPaused);
    }

    [Fact]
    public void Thaw_AnimationPausedBeforeTheFreeze_StaysPaused()
    {
        var (proxy, sprite) = NewProxyWithSprite();
        sprite.IsPlaybackPaused = true;

        AlundraGameplayFreeze.Freeze(proxy);
        AlundraGameplayFreeze.Thaw(proxy);

        Assert.True(sprite.IsPlaybackPaused);
    }

    /// <summary>The production site: the end of <see cref="AlundraWorldProxy.Update"/> freezes every
    /// spawned entity while MenuOpen is posed, and thaws it once MenuOpen is lifted. Same headless montage as
    /// <see cref="AlundraWorldProxyGlobalFreezeTests"/>'s own camera test; the spawned entity is placed in
    /// the world proxy's own spawned list, where every real spawn path puts it.</summary>
    [Fact]
    public void WorldUpdate_MenuOpen_FreezesASpawnedEntity_LiftingItThawsIt()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);

        var (proxy, sprite) = NewProxyWithSprite();
        var controller = new CharacterControllerComponent();
        controller.RestoreStateSnapshot(MidJump);
        proxy.Controller = controller;
        var spawnedField = typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(spawnedField);
        ((List<Entity>)spawnedField!.GetValue(worldProxy)!).Add(sprite.Owner!);

        worldProxy.Update(0.02f);
        Assert.False(proxy.FreezeState.IsFrozen);

        worldProxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        worldProxy.Update(0.02f);
        Assert.True(proxy.FreezeState.IsFrozen);
        Assert.Equal(CharacterControlMode.Disabled, controller.ControlMode);
        Assert.True(sprite.IsPlaybackPaused);

        worldProxy.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        worldProxy.Update(0.02f);
        Assert.False(proxy.FreezeState.IsFrozen);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(new Vector3(3f, -2f, 150f), controller.Velocity);
        Assert.False(sprite.IsPlaybackPaused);
    }
}
