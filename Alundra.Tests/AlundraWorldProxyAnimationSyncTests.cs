using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the animation-sync port added to <see cref="AlundraWorldProxy"/>: the target-resolution part
/// of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4
/// (<see cref="AlundraFrameSyncPasses.TryResolveAnimationTarget"/>), the by-suffix animation lookup
/// (<see cref="AlundraFrameSyncPasses.TrySelectAnimationByNameSuffix"/>) and the per-frame driver
/// (<see cref="AlundraFrameSyncPasses.RunAnimationSyncPass"/>). Uses real
/// <see cref="AnimatedSpriteComponent"/>/<see cref="Animation2d"/> instances (both headless-constructible,
/// as CasaEngine.Tests's own animation tests do), not a fake/spy - the component only needs asset loading
/// for sprites, which this proxy never touches.
/// </summary>
public class AlundraWorldProxyAnimationSyncTests
{
    // -----------------------------------------------------------------------------------------
    // TryResolveAnimationTarget
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TryResolveAnimationTarget_FreshlySpawnedEntity_AlwaysFiresOnFirstCall()
    {
        // ApplySpawnInitialization leaves CurrentAnimationId = ~TargetAnimationId - guaranteed different.
        var proxy = new AlundraEntityScriptProxy
        {
            CurrentAnimationId = ~0u,
            TargetAnimationId = 0,
            AnimationDirection = 0,
            TargetDirection = 0,
        };

        var fired = AlundraFrameSyncPasses.TryResolveAnimationTarget(proxy, out var newAnim, out var newDirection);

        Assert.True(fired);
        Assert.Equal(0u, newAnim);
        Assert.Equal(0, newDirection); // g_animationDirectionTable[0*8+0] = 0
    }

    [Fact]
    public void TryResolveAnimationTarget_NoChange_ReturnsFalse()
    {
        var proxy = new AlundraEntityScriptProxy
        {
            CurrentAnimationId = 0,
            TargetAnimationId = 0,
            AnimationDirection = 0,
            TargetDirection = 0,
        };

        var fired = AlundraFrameSyncPasses.TryResolveAnimationTarget(proxy, out var newAnim, out var newDirection);

        Assert.False(fired);
        Assert.Equal(proxy.CurrentAnimationId, newAnim);
        Assert.Equal(proxy.AnimationDirection, newDirection);
    }

    [Fact]
    public void TryResolveAnimationTarget_DirectionChangesFromLeftToUp_NonIdentityTableLookup()
    {
        // AnimationDirection=2 (left), TargetDirection=0x10 (facing index 1, "up"):
        // col = ((0x10+2)&0x1c)>>2 = 4; g_animationDirectionTable[2*8+4] = 1 ("up") - different from the
        // current row (2), proving this resolves through the table rather than the raw facing index.
        var proxy = new AlundraEntityScriptProxy
        {
            CurrentAnimationId = 0,
            TargetAnimationId = 0,
            AnimationDirection = 2,
            TargetDirection = 0x10,
        };

        var fired = AlundraFrameSyncPasses.TryResolveAnimationTarget(proxy, out var newAnim, out var newDirection);

        Assert.True(fired);
        Assert.Equal(0u, newAnim);
        Assert.Equal(1, newDirection);
    }

    // -----------------------------------------------------------------------------------------
    // TrySelectAnimationByNameSuffix
    // -----------------------------------------------------------------------------------------

    private static AnimatedSpriteComponent BuildComponentWithAllDirections(int animationId)
    {
        var component = new AnimatedSpriteComponent();
        foreach (var direction in new[] { "down", "up", "left", "right" })
        {
            component.AddAnimation(new Animation2d(new Animation2dData { Name = $"bankalundra_25_anim{animationId}_{direction}" }));
        }

        return component;
    }

    [Theory]
    [InlineData(0, "down")]
    [InlineData(1, "up")]
    [InlineData(2, "left")]
    [InlineData(3, "right")]
    public void TrySelectAnimationByNameSuffix_AllFourDirections_MatchBySuffix(int animationDirection, string expectedDirectionName)
    {
        var component = BuildComponentWithAllDirections(animationId: 0);

        var found = AlundraFrameSyncPasses.TrySelectAnimationByNameSuffix(component, 0, animationDirection, out var selected);

        Assert.True(found);
        Assert.EndsWith($"_anim0_{expectedDirectionName}", selected!.Animation2dData.Name);
    }

    [Fact]
    public void TrySelectAnimationByNameSuffix_NoMatchingAnimation_ReturnsFalse()
    {
        var component = BuildComponentWithAllDirections(animationId: 0);

        var found = AlundraFrameSyncPasses.TrySelectAnimationByNameSuffix(component, 5, 0, out var selected);

        Assert.False(found);
        Assert.Null(selected);
    }

    // -----------------------------------------------------------------------------------------
    // RunAnimationSyncPass
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RunAnimationSyncPass_FreshlySpawnedEntity_SelectsInitialAnimationOnceThenStaysStable()
    {
        var component = BuildComponentWithAllDirections(animationId: 0);
        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = component,
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.CurrentAnimationId = ~0u; // as ApplySpawnInitialization leaves it: TargetAnimationId=0, CurrentAnimationId=~0.

        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(0, proxy.AnimationDirection);
        Assert.NotNull(component.CurrentAnimation);
        Assert.EndsWith("_anim0_down", component.CurrentAnimation!.Animation2dData.Name);

        // Second call: nothing changed since the first sync, so no further work happens (change-detected).
        var animationBeforeSecondCall = component.CurrentAnimation;
        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });
        Assert.Same(animationBeforeSecondCall, component.CurrentAnimation);
    }

    /// <summary>
    /// User-reported bug (2026-08-26, "the walk animation does not loop when it should"). The hero's walk
    /// is exported as <c>Once</c> plus a chain edge onto ITSELF (<c>anim 1 -&gt; ChainTo 1</c>, all four
    /// directions), which is how the original spells a looping walk; his idle is a real engine
    /// <c>Loop</c>. On the chain, <see cref="AlundraEntitySpawnFactory.OnAnimationFinished"/> writes
    /// <c>TargetAnimationId = 1</c> - the id it already had - so
    /// <see cref="AlundraFrameSyncPasses.TryResolveAnimationTarget"/> reports "no change" and the sync pass
    /// used to return early, leaving the sampler parked at its terminal pose: a frozen walk.
    ///
    /// This test drives the real observable rather than an internal flag: it advances playback with
    /// <see cref="AnimatedSpriteComponent.SeekCurrentAnimation"/>, then checks that a pending chain
    /// restart rewinds <see cref="AnimatedSpriteComponent.CurrentAnimationTimeSeconds"/> to 0 even though
    /// neither the animation id nor the direction changed. The control case below proves the assertion is
    /// discriminating: without the flag, the very same setup leaves playback exactly where it was.
    /// </summary>
    [Fact]
    public void RunAnimationSyncPass_PendingChainRestart_RewindsPlaybackDespiteUnchangedAnimationId()
    {
        var (entity, component, proxy) = BuildSyncedEntityOnAnimationZero();

        Assert.True(component.SeekCurrentAnimation(0.05f));
        Assert.True(component.CurrentAnimationTimeSeconds > 0f);

        proxy.PendingChainRestartFlag = 1; // as OnAnimationFinished raises it on a self-chain
        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.Equal(0f, component.CurrentAnimationTimeSeconds);
        Assert.Equal(0, proxy.PendingChainRestartFlag); // consumed, so one ending causes one restart
    }

    [Fact]
    public void RunAnimationSyncPass_NoPendingChainRestart_LeavesPlaybackWhereItWas()
    {
        var (entity, component, proxy) = BuildSyncedEntityOnAnimationZero();

        Assert.True(component.SeekCurrentAnimation(0.05f));
        var timeBefore = component.CurrentAnimationTimeSeconds;
        Assert.True(timeBefore > 0f);

        Assert.Equal(0, proxy.PendingChainRestartFlag);
        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.Equal(timeBefore, component.CurrentAnimationTimeSeconds);
    }

    /// <summary>Spawns an entity and runs one sync pass, leaving it settled on animation 0 facing down -
    /// the shared starting point of the two chain-restart tests above.</summary>
    private static (Entity Entity, AnimatedSpriteComponent Component, AlundraEntityScriptProxy Proxy)
        BuildSyncedEntityOnAnimationZero()
    {
        // Unlike BuildComponentWithAllDirections, these animations carry a Part: AnimatedSpriteComponent
        // only builds a composition sampler for animations that have one, and the sampler is what holds
        // the playback clock these two tests observe.
        var component = new AnimatedSpriteComponent();
        foreach (var direction in new[] { "down", "up", "left", "right" })
        {
            var data = new Animation2dData { Name = $"bankalundra_25_anim0_{direction}" };
            data.Parts.Add(new Animation2dPartData { Id = "body" });
            // Gives the animation a non-zero duration (Animation2dData.GetDurationSeconds takes the max
            // over tracks, events and collision keyframes) so playback has somewhere to advance TO.
            data.CollisionKeyframes.Add(new Animation2dCollisionKeyframeData { TimeSeconds = 0.2f });
            component.AddAnimation(new Animation2d(data));
        }

        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = component,
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.CurrentAnimationId = ~0u;

        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        return (entity, component, proxy);
    }

    [Fact]
    public void RunAnimationSyncPass_TargetChangesAfterSpawn_SelectsNewAnimation()
    {
        var component = BuildComponentWithAllDirections(animationId: 0);
        component.AddAnimation(new Animation2d(new Animation2dData { Name = "bankalundra_25_anim1_down" }));
        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = component,
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.CurrentAnimationId = 0;
        proxy.TargetAnimationId = 0;
        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity }); // settle the initial spawn state

        proxy.TargetAnimationId = 1;

        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.Equal(1u, proxy.CurrentAnimationId);
        Assert.EndsWith("_anim1_down", component.CurrentAnimation!.Animation2dData.Name);
    }

    [Fact]
    public void RunAnimationSyncPass_BareEntityWithNoAnimatedSpriteComponent_ResolvesStateButDoesNotThrow()
    {
        var entity = new Entity { Name = "bare", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.CurrentAnimationId = ~0u;

        AlundraFrameSyncPasses.RunAnimationSyncPass(new List<Entity> { entity });

        Assert.Equal(0u, proxy.CurrentAnimationId);
    }

    [Fact]
    public void RunAnimationSyncPass_EmptyList_DoesNothing()
    {
        AlundraFrameSyncPasses.RunAnimationSyncPass(Array.Empty<Entity>());
    }
}
