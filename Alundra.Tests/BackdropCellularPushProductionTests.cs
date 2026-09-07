#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// D-E9d - production-site pin for <see cref="AlundraBackdropStage.PushFrame"/> feeding BOTH the
/// sibling scrolling-layer service AND the cellular-layer service from the SAME call site (no second
/// push site was added to <see cref="AlundraWorldProxy.Update"/>). Reuses the real map 389 headless
/// montage exactly like <see cref="BackdropPushProductionTests"/> - see that class's own doc for the
/// "recipe B3" accommodation (posing <c>_clearColorApplied</c>, priming <c>Update</c>) this montage
/// always needs.
/// </summary>
public sealed class BackdropCellularPushProductionTests : IDisposable
{
    public BackdropCellularPushProductionTests()
    {
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
    /// The ONE production push site (<c>AlundraWorldProxy.Update</c> -&gt;
    /// <see cref="AlundraBackdropStage.PushFrame"/>) feeds a real <see cref="CellularLayerService"/>
    /// exactly like it already feeds a real <see cref="ScrollingLayerService"/> - same
    /// <c>FramesPushed</c>/<c>PendingTicks</c> progression on both, same camera target, proving there is
    /// no separate call site for the cellular mechanism.
    /// </summary>
    [Fact]
    public void Update_PushesFrame_ToBothServices_AtTheSameProductionCallSite()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrollingService = new ScrollingLayerService();
        var cellularService = new CellularLayerService();
        stage.AttachService(scrollingService);
        stage.AttachCellularService(cellularService);
        SetPrivateField(stage, "_clearColorApplied", true);

        proxy.Update(0.02f); // priming frame - sticky first-frame floor, resets the clock exactly.
        var scrollingFramesAfterPriming = scrollingService.FramesPushed;
        var cellularFramesAfterPriming = cellularService.FramesPushed;

        proxy.Update(0f);
        Assert.True(scrollingService.FramesPushed > scrollingFramesAfterPriming);
        Assert.True(cellularService.FramesPushed > cellularFramesAfterPriming);
        Assert.Equal(0, scrollingService.PendingTicks);
        Assert.Equal(0, cellularService.PendingTicks);

        proxy.Update(0.04f);
        Assert.Equal(2, scrollingService.PendingTicks);
        Assert.Equal(2, cellularService.PendingTicks); // SAME tick count as the sibling service.

        var target = proxy._cameraDirector.ResolvedCamera?.Target ?? Vector3.Zero;
        Assert.Equal(target, scrollingService.CameraTarget);
        Assert.Equal(target, cellularService.CameraTarget); // SAME camera target as the sibling service.

        var expectedScroll = AlundraCameraMath.ToOriginalScrollSpace(target);
        Assert.Equal(expectedScroll.X, cellularService.LastPushedCameraX);
        Assert.Equal(expectedScroll.Y, cellularService.LastPushedCameraY);
    }

    /// <summary>A <see langword="null"/> cellular service (never attached) is a no-op for that side only
    /// - the sibling scrolling-layer push still runs unaffected.</summary>
    [Fact]
    public void Update_PushesFrame_WithNoCellularServiceAttached_ScrollingServiceStillPushed()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;
        var scrollingService = new ScrollingLayerService();
        stage.AttachService(scrollingService);
        // AttachCellularService deliberately never called - _cellularService stays null.
        SetPrivateField(stage, "_clearColorApplied", true);

        var exception = Record.Exception(() => proxy.Update(0.02f));

        Assert.Null(exception);
        Assert.True(scrollingService.FramesPushed > 0);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
