#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S2", D-E9b-2/D-E9b-11) - production-site pins for
/// <see cref="AlundraBackdropStage.PushFrame"/>, replacing the retired <c>BackdropAnimationReplayProductionTests</c>
/// (the animation-counter pin it used to carry now lives engine-side, S0). Reuses the real map 389
/// headless montage (<see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/>) with the
/// SAME "recipe B3" accommodation that montage always needs: <c>_clearColorApplied</c> posed by
/// reflection before the first <c>proxy.Update</c> (<see cref="AlundraBackdropStage.ApplyOriginalBackgroundClearColorOnce"/>
/// would otherwise dereference <c>world.Game.GameManager.ViewManager.Views</c> on this uninitialized
/// <c>CasaEngineGame</c>), plus a priming <c>proxy.Update(0.02f)</c> (the sticky first-frame floor,
/// <c>AlundraLogicClock</c>'s own first-call memo) so the FOLLOWING frame's tick count is exactly
/// <c>elapsedTime * 50</c>. A real <see cref="ScrollingLayerService"/> (no
/// <see cref="CasaEngine.Framework.Application.Components.ScrollingLayerComponent"/> - nothing here
/// calls <see cref="ScrollingLayerService.Advance"/>) is injected via
/// <see cref="AlundraBackdropStage.AttachService"/> BEFORE that first <c>Update</c> - <c>InitializeWithWorld</c>
/// itself already attached a null service (this montage's <c>Game</c> is uninitialized, so its
/// <c>ScrollingLayerComponent</c> is null too) and logged the one-warning "arrêt" case harmlessly:
/// <see cref="AlundraBackdropStage.PushFrame"/> depends on neither the loaded layers nor <c>HasContent</c>,
/// so the later re-attach is all that matters for these pins.
/// </summary>
public sealed class BackdropPushProductionTests : IDisposable
{
    public BackdropPushProductionTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy
        // and drives proxy.Update over the real map 389 montage - reset the session carriers, same as
        // every other class exercising this montage (AlundraWorldProxyGlobalFreezeTests, ...).
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    /// <summary>
    /// (a) The push contract itself: a priming frame arms and leaves <c>PendingTicks</c> unconsumed
    /// (nothing here ever calls <see cref="ScrollingLayerService.Advance"/>) - the NEXT <c>SetFrame</c>
    /// OVERWRITES it (D-E9b-3), so <c>Update(0f)</c> (0 ticks) drives <c>PendingTicks</c> straight to 0
    /// while still incrementing <see cref="ScrollingLayerService.FramesPushed"/>, and the following
    /// <c>Update(0.04f)</c> (exactly 2 ticks) drives it to 2. <c>LastPushedScrollX/Y</c> and
    /// <c>CameraTarget</c> are read back against the SAME conversion/fallback
    /// <see cref="AlundraBackdropStage.PushFrame"/> itself applies to the resolved camera's <c>Target</c>
    /// (this montage has no camera entity, so the resolved camera - and therefore <c>Target</c> - is the
    /// fallback <see cref="Vector3.Zero"/>, same as production on a world with no camera).
    /// </summary>
    [Fact]
    public void Update_PushesFrame_AtTheProductionCallSite_PendingTicksReflectsTheLatestPush()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var service = new ScrollingLayerService();
        stage.AttachService(service);
        SetPrivateField(stage, "_clearColorApplied", true);

        proxy.Update(0.02f); // priming frame - sticky first-frame floor, resets the clock exactly.
        var framesPushedAfterPriming = service.FramesPushed;

        proxy.Update(0f);
        Assert.True(service.FramesPushed > framesPushedAfterPriming);
        Assert.Equal(0, service.PendingTicks);

        proxy.Update(0.04f);
        Assert.Equal(2, service.PendingTicks);

        var target = proxy._cameraDirector.ResolvedCamera?.Target ?? Vector3.Zero;
        var expectedScroll = AlundraCameraMath.ToOriginalScrollSpace(target);
        Assert.Equal(expectedScroll.X, service.LastPushedScrollX);
        Assert.Equal(expectedScroll.Y, service.LastPushedScrollY);
        Assert.Equal(target, service.CameraTarget);
    }

    /// <summary>
    /// (b) The freeze-gate pin (D-E9b-2's own "hors porte de gel" clause): posing
    /// <see cref="AlundraGameState.PlayerControlBits.MenuOpen"/> (part of <c>GameplayBlockedMask</c>)
    /// BEFORE the very same <c>Update(0.04f)</c> as the test above must NOT change the result -
    /// <c>PendingTicks</c> is still 2, proving <see cref="AlundraBackdropStage.PushFrame"/>'s call site
    /// sits outside the gameplay freeze gate (a MenuOpen dialogue box does not also freeze the backdrop
    /// push, exactly like the camera follow - <see cref="AlundraWorldProxyGlobalFreezeTests"/>'s own
    /// "mettre le suivi caméra dedans" pin).
    /// </summary>
    [Fact]
    public void Update_PushesFrame_WhileGameplayBlockedMaskIsPosed_PendingTicksStillReflectsThePush()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var service = new ScrollingLayerService();
        stage.AttachService(service);
        SetPrivateField(stage, "_clearColorApplied", true);

        proxy.Update(0.02f); // priming frame.
        proxy.Update(0f); // resets PendingTicks to 0 (see the sibling test).

        proxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;

        proxy.Update(0.04f);

        Assert.Equal(2, service.PendingTicks);
    }

    /// <summary>
    /// (c) A world with no <c>Game</c> at all (<c>BuildHeadlessWorld</c>'s own shape - no live
    /// <c>GraphicsDevice</c>-backed collaborator anywhere) - the push still runs:
    /// <see cref="AlundraBackdropStage.PushFrame"/> depends on neither <c>world.Game</c> nor
    /// <c>HasContent</c>, only on the resolved camera (fallback <see cref="Vector3.Zero"/> here, same
    /// reasoning as the tests above) and the attached service.
    /// </summary>
    [Fact]
    public void Update_PushesFrame_OnAWorldWithNoGame()
    {
        var world = new World { Name = "TestWorld" };
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no "tileMap" entity -> early return, harmless here.

        var stage = proxy._backdropStage;
        var service = new ScrollingLayerService();
        stage.AttachService(service);

        proxy.Update(0.02f);

        Assert.True(service.FramesPushed > 0);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
