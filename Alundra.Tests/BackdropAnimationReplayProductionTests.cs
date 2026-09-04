#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// D-E9-5 (docs/plan-e9-backdrops-residus.md §2/§3 slice B3), production-site pin:
/// <see cref="BackdropRenderer.AdvanceAnimation"/> really runs, once per logic tick, at
/// <see cref="AlundraWorldProxy.Update"/>'s own call site (AlundraWorldProxy.cs ~1520), even though
/// <see cref="AlundraBackdropStage.UpdateAndDrawBackdrop"/> has two early returns that would otherwise
/// gate everything else in that method - the whole point of D-E9-5's "advance is the FIRST instruction"
/// rule (§1.4).
///
/// Reuses the real map 389 headless montage
/// (<see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/> - a "tileMap" entity with real
/// <see cref="CasaEngine.Framework.Assets.TileMap.TileMapData"/>/<see cref="CasaEngine.Framework.Assets.TileMap.TileSetData"/>,
/// an uninitialized <see cref="CasaEngine.Framework.Application.CasaEngineGame"/> whose <c>GameManager</c>
/// is null - see that class' own doc). On this exact montage, <c>proxy.Update</c> would otherwise throw a
/// <see cref="NullReferenceException"/> at <c>AlundraBackdropStage.cs:90</c>
/// (<see cref="AlundraBackdropStage.ApplyOriginalBackgroundClearColorOnce"/> dereferencing
/// <c>world.Game.GameManager.ViewManager.Views</c>) BEFORE ever reaching
/// <see cref="AlundraBackdropStage.UpdateAndDrawBackdrop"/> - the plan's own accommodation (a) is posing
/// <c>_clearColorApplied = true</c> by reflection before the first <c>proxy.Update</c>, so that guard
/// clears without needing a real <c>GameManager</c>/<c>ViewManager</c>.
///
/// The stage itself is reached through <see cref="AlundraWorldProxy._backdropStage"/> directly (internal,
/// and this assembly has <c>InternalsVisibleTo</c> - no reflection needed for that hop); only the private
/// <c>_backdropRenderer</c>/<c>_clearColorApplied</c> fields inside <see cref="AlundraBackdropStage"/> and
/// the private <c>LayerRuntime</c> nested type inside <see cref="BackdropRenderer"/> need reflection - the
/// exact same seams <see cref="BackdropRendererTests"/> already uses.
/// </summary>
public sealed class BackdropAnimationReplayProductionTests : IDisposable
{
    public BackdropAnimationReplayProductionTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy
        // and drives a full proxy.Update over the real map 389 montage - the same four session carriers
        // AlundraWorldProxyGlobalFreezeTests/AlundraScreenFadeCameraWiringTests reset for that montage.
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
    /// Recipe imposed by the montage (§1.4/§3 slice B3 acceptance): (a) pose <c>_clearColorApplied</c>
    /// before the first <c>proxy.Update</c>, same reflection hop as the renderer substitution below;
    /// (b) one priming frame <c>proxy.Update(0.02f)</c> first (the sticky first-frame floor,
    /// <c>Math.Max(ticks, 1)</c>, would otherwise inflate the very first measurement), THEN measure as
    /// counter DELTAS: <c>proxy.Update(0f)</c> advances 0 ticks -&gt; delta 0; <c>proxy.Update(0.04f)</c>
    /// advances exactly 2 ticks (0.04f is exactly 2*0.02f in binary, and the accumulator was reset exactly
    /// by the priming frame - <c>AlundraLogicClock.cs:62-69</c>) -&gt; delta 2.
    ///
    /// The synthetic layer uses <c>AnimTimer = 0</c>: with the ">AnimTimer" threshold at 0, EVERY tick
    /// crosses it (<c>++AnimFrameTimer</c> is always &gt;= 1 &gt; 0), so the counter advances by exactly
    /// one step per tick regardless of starting parity - making "delta of ticks" and "delta of counter"
    /// the same number, with <c>Frames.Length = 4</c> large enough that neither delta below (0, then 2)
    /// ever wraps.
    /// </summary>
    [Fact]
    public void Update_AdvancesTheBackdropAnimationCounter_ByExactlyTicksThisFrame_AtTheProductionCallSite()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        var stage = proxy._backdropStage;

        // Substitute a fully controlled BackdropRenderer (independent of whatever Load resolved from the
        // real 389 companion) carrying one synthetic animated layer, and pose _clearColorApplied so
        // ApplyOriginalBackgroundClearColorOnce's guard clears without a real GameManager/ViewManager.
        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: 4, animTimer: 0);
        SetPrivateField(stage, "_backdropRenderer", renderer);
        SetPrivateField(stage, "_clearColorApplied", true);

        proxy.Update(0.02f); // (b) priming frame - sticky first-frame floor, resets the clock exactly.
        var counterAfterPriming = (int)GetLayerAnimFrameCounter(layer);

        proxy.Update(0f);
        var counterAfterZero = (int)GetLayerAnimFrameCounter(layer);
        Assert.Equal(0, counterAfterZero - counterAfterPriming); // ticksThisFrame = 0 -> no advance.

        proxy.Update(0.04f);
        var counterAfterTwoTicks = (int)GetLayerAnimFrameCounter(layer);
        Assert.Equal(2, counterAfterTwoTicks - counterAfterZero); // ticksThisFrame = 2 -> delta 2.
    }

    /// <summary>
    /// Sibling pin, same measurement, but on a montage where the FIRST guard
    /// (<c>!_backdropRenderer.HasContent || world?.Game == null</c>) is the one that fires: a headless
    /// world with NO <c>Game</c> at all (the same <c>BuildHeadlessWorld()</c> shape
    /// <see cref="AlundraWorldProxyGlobalFreezeTests.Update_CameraFollow_KeepsTrackingFollowedEntity_WhileGameplayBlockedMaskIsPosed"/>
    /// already drives <c>proxy.Update</c> through). <see cref="AlundraBackdropStage.ApplyOriginalBackgroundClearColorOnce"/>
    /// also returns immediately on <c>world?.Game == null</c>, so <c>_clearColorApplied</c> does not
    /// matter here - posed anyway for symmetry with the other pin, never read on this path.
    ///
    /// This is the discriminating montage for "advance moved between the two guards": with that
    /// mutation, the advance sits AFTER the first guard's <c>return</c>, so on THIS world it never runs
    /// at all - the existing sibling test cannot see that placement because its own real-389 montage
    /// never trips the first guard (<c>HasContent</c> is true and <c>world.Game</c> is non-null there).
    /// </summary>
    [Fact]
    public void Update_AdvancesTheBackdropAnimationCounter_OnAWorldWithNoGame_WhereTheFirstGuardFires()
    {
        var world = new World { Name = "TestWorld" }; // no "tileMap" entity, no Game - BuildHeadlessWorld() shape.

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no "tileMap" entity -> early return, harmless here.

        var stage = proxy._backdropStage;

        var renderer = new BackdropRenderer();
        var layer = AddSyntheticAnimatedLayer(renderer, frameCount: 4, animTimer: 0);
        SetPrivateField(stage, "_backdropRenderer", renderer);
        SetPrivateField(stage, "_clearColorApplied", true); // symmetry only - never read on this path (world.Game is null).

        proxy.Update(0.02f); // priming frame - sticky first-frame floor.
        var counterAfterPriming = (int)GetLayerAnimFrameCounter(layer);

        proxy.Update(0f);
        var counterAfterZero = (int)GetLayerAnimFrameCounter(layer);
        Assert.Equal(0, counterAfterZero - counterAfterPriming); // ticksThisFrame = 0 -> no advance.

        proxy.Update(0.04f);
        var counterAfterTwoTicks = (int)GetLayerAnimFrameCounter(layer);
        Assert.Equal(2, counterAfterTwoTicks - counterAfterZero); // ticksThisFrame = 2 -> delta 2.
    }

    private static object AddSyntheticAnimatedLayer(BackdropRenderer renderer, int frameCount, int animTimer)
    {
        var frames = new Texture2D[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = (Texture2D)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        }

        var scrollar = new BackdropScrollarData
        {
            FactorXNum = 1, FactorXDenom = 1,
            FactorYNum = 1, FactorYDenom = 1,
            ScrollXSpeed = 0, ScrollXPeriod = 0,
            ScrollYSpeed = 0, ScrollYPeriod = 0,
        };

        var sortKey = new CasaEngine.Framework.Rendering.Depth.RenderSortKey2D(
            (int)CasaEngine.Framework.Rendering.Depth.RenderPass2D.Background, 0, 0, 0, 0, 0, 0);

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layer = Activator.CreateInstance(
            layerRuntimeType!, scrollar, frames, animTimer, sortKey, Color.White,
            CasaEngine.Framework.Rendering.Depth.SpriteBlendMode.Opaque)!;

        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;
        layers.Add(layer);

        return layer;
    }

    private static object GetLayerAnimFrameCounter(object layer)
    {
        var property = layer.GetType().GetProperty("AnimFrameCounter", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property!.GetValue(layer)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
