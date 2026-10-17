#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Physics;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a3a probe P-A (docs/plan-e20-heros.md section 1.16.3a, "Resultat de P-A"). MEASURES, and asserts only what is true today of the production code (nothing here is a requirement
/// of E20.e0a3b): what an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> (the arrow, number 3 of the global page, direction 0x18) does BEFORE the engine integrates it
/// (<c>Entity.World</c> still null) when (1) the sprite is asked for its first animation with <c>SetCurrentAnimation(forceReset: true)</c>, (2) <c>Controller.Move</c> is called,
/// (3) the generic kinematics of the DLL (<see cref="AlundraScriptedMotion.TickScriptedNpc"/>) runs the birth tick and the tick after, with <c>CurrentAnimationId</c> as the interface leaves it (~0)
/// and with <c>Current = Target</c> posed by hand. Every measured value is also written to the test output with the prefix <c>PA:</c> (the plan copies them).
/// No production code is changed by this slice; the interface is not called by any production path.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraBirthTickProbePaTests
{
    private const int StartX = 150;
    private const int StartY = 100;
    private const int Arrow = 3;
    private const int Direction = 0x18;

    private readonly ITestOutputHelper _output;

    public AlundraBirthTickProbePaTests(ITestOutputHelper output) => _output = output;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0a3a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static AlundraEntityScriptProxy SpawnArrow(ArcRun arc)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, StartX << 16, StartY << 16, 0, Direction)!;

    private void Say(string line) => _output.WriteLine("PA: " + line);

    private static string Sprite(AnimatedSpriteComponent s)
        => $"animations={s.Animations.Count} current={(s.CurrentAnimation == null ? "null" : s.CurrentAnimation.Animation2dData.Name)} time={s.CurrentAnimationTimeSeconds:0.####} " +
           $"logicalRate={s.LogicalTickRate} logicalTick={s.LogicalTick} endReached={s.IsLogicalEndReached} loops={s.CompletedLoopCount}";

    /// <summary>The animation the first switch of the binary would select: animation 0 in the direction the table derives from the target direction (the pure half of the sync pass).</summary>
    private static Animation2d? FirstAnimation(AlundraEntityScriptProxy proxy, AnimatedSpriteComponent sprite)
    {
        FrameSyncPasses.TryResolveAnimationTarget(proxy, out var id, out var dir);
        return FrameSyncPasses.TrySelectAnimationByNameSuffix(sprite, id, dir, out var selected) ? selected : null;
    }

    [Fact]
    public void PA1_TheSpriteAskedForItsFirstAnimationBeforeTheIntegration_ThenIntegrated()
    {
        using var arc = Arc("pa1");
        var proxy = SpawnArrow(arc);
        var entity = ArcRun.EntityOf(proxy)!;
        var sprite = entity.GetComponent<AnimatedSpriteComponent>()!;
        Assert.Null(entity.World); // not integrated: the engine does it at the head of the next World.Update.
        Say($"PA1 spawned: world={(entity.World == null ? "null" : "set")} proxy current={(int)proxy.CurrentAnimationId} target={proxy.TargetAnimationId} animationDirection={proxy.AnimationDirection} targetDirection={proxy.TargetDirection}");
        Say("PA1 sprite as spawned: " + Sprite(sprite));

        var selected = FirstAnimation(proxy, sprite);
        Say($"PA1 first animation found by suffix before the integration: {(selected == null ? "none" : selected.Animation2dData.Name)}");

        string outcome;
        try
        {
            if (selected != null)
            {
                sprite.SetCurrentAnimation(selected, forceReset: true);
                outcome = "SetCurrentAnimation(selected, true): no exception";
            }
            else if (sprite.CurrentAnimation != null)
            {
                sprite.SetCurrentAnimation(sprite.CurrentAnimation, forceReset: true);
                outcome = "SetCurrentAnimation(CurrentAnimation, true): no exception";
            }
            else
            {
                outcome = "no animation to set (Animations empty, CurrentAnimation null): nothing called";
            }
        }
        catch (Exception e)
        {
            outcome = "EXCEPTION " + e.GetType().Name + ": " + e.Message;
        }

        Say("PA1 " + outcome);
        Assert.Null(selected);
        Assert.Equal(0, sprite.Animations.Count);
        Assert.Null(sprite.CurrentAnimation);

        // The two other ways to ask for a first animation, with nothing to choose from: by index it throws, by name it answers false.
        var byIndex = Record.Exception(() => sprite.SetCurrentAnimation(0, forceReset: true));
        var byName = false;
        var byNameException = Record.Exception(() => byName = sprite.SetCurrentAnimation("bankalundra_3_anim0_right", forceReset: true));
        Say($"PA1 SetCurrentAnimation(0, true): {(byIndex == null ? "no exception" : byIndex.GetType().Name)}; SetCurrentAnimation(name, true): {(byNameException == null ? "returned " + byName : byNameException.GetType().Name)}");
        Assert.IsType<ArgumentOutOfRangeException>(byIndex);
        Assert.Null(byNameException);
        Assert.False(byName);
        Say("PA1 sprite after the calls, still before the integration: " + Sprite(sprite));
        Assert.Equal(0, sprite.Animations.Count);
        Assert.Null(sprite.CurrentAnimation);

        arc.OneFrame();
        Say($"PA1 after one engine frame (integration): world={(entity.World == null ? "null" : "set")} proxy current={(int)proxy.CurrentAnimationId} animationDirection={proxy.AnimationDirection}");
        Say("PA1 sprite after one engine frame: " + Sprite(sprite));
        Assert.NotNull(entity.World);
        Assert.True(sprite.Animations.Count > 0);
        Assert.EndsWith("_anim0_right", sprite.CurrentAnimation!.Animation2dData.Name);
        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(3, proxy.AnimationDirection);
    }

    [Fact]
    public void PA1b_WithCurrentEqualTarget_PosedByHand_TheFirstFrameAfterTheIntegration()
    {
        using var arc = Arc("pa1b");
        var proxy = SpawnArrow(arc);
        var entity = ArcRun.EntityOf(proxy)!;
        var sprite = entity.GetComponent<AnimatedSpriteComponent>()!;
        proxy.CurrentAnimationId = proxy.TargetAnimationId;
        proxy.CurrentDirection = proxy.TargetDirection;
        var pending = FrameSyncPasses.TryResolveAnimationTarget(proxy, out var id, out var dir);
        Say($"PA1b Current=Target posed by hand: a switch is still pending={pending} (resolved id={id} animationDirection={dir}; the proxy's AnimationDirection={proxy.AnimationDirection})");
        arc.OneFrame();
        Say($"PA1b after one engine frame: proxy current={(int)proxy.CurrentAnimationId} animationDirection={proxy.AnimationDirection}");
        Say("PA1b sprite after one engine frame: " + Sprite(sprite));
        Assert.True(pending); // AnimationDirection (0) is not the one the table derives (3): the first sync still switches the sprite.
        Assert.EndsWith("_anim0_right", sprite.CurrentAnimation!.Animation2dData.Name);
        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(3, proxy.AnimationDirection);
    }

    [Fact]
    public void PA2_ControllerMoveBeforeTheIntegration_AddsTheDisplacementWithoutATest_AndAfterIt()
    {
        using var arc = Arc("pa2");
        var proxy = SpawnArrow(arc);
        var entity = ArcRun.EntityOf(proxy)!;
        var controller = entity.GetComponent<CharacterControllerComponent>();
        Say($"PA2 controller present={(controller != null)} world={(entity.World == null ? "null" : "set")} root position={entity.RootComponent!.Position}");
        Assert.NotNull(controller);
        Assert.Null(entity.World);

        var start = entity.RootComponent.Position;
        var requested = new Vector3(5f, 0f, 0f);
        string outcome;
        Vector3 actual = default;
        try
        {
            actual = controller!.Move(requested);
            outcome = "no exception";
        }
        catch (Exception e)
        {
            outcome = "EXCEPTION " + e.GetType().Name + ": " + e.Message;
        }

        var moved = entity.RootComponent.Position - start;
        Say($"PA2 before the integration: Move({requested}) {outcome}; returned {actual}; root moved by {moved}");
        Assert.Equal("no exception", outcome);
        Assert.Equal(requested, actual);
        Assert.Equal(requested, moved);

        arc.OneFrame();
        var startAfter = entity.RootComponent.Position;
        var actualAfter = controller!.Move(requested);
        Say($"PA2 after the integration (world {(entity.World == null ? "null" : "set")}): root before {startAfter}; Move({requested}) returned {actualAfter}; root moved by {entity.RootComponent.Position - startAfter}");
        Assert.NotNull(entity.World);
        Assert.Equal(requested, actualAfter); // nothing in the way at this point of map 14, so the test of the field changes nothing here.
    }

    private int[] Kinematics(string label, bool currentEqualsTarget)
    {
        var pixels = new int[3];
        using var arc = Arc(label);
        var proxy = SpawnArrow(arc);
        var entity = ArcRun.EntityOf(proxy)!;
        if (currentEqualsTarget)
        {
            proxy.CurrentAnimationId = proxy.TargetAnimationId;
            proxy.CurrentDirection = proxy.TargetDirection;
        }

        var hasSet = proxy.AnimSetsByAnim != null && proxy.AnimSetsByAnim.TryGetValue((int)proxy.CurrentAnimationId, out var entryOfCurrent);
        var hasSetTarget = proxy.AnimSetsByAnim != null && proxy.AnimSetsByAnim.TryGetValue((int)proxy.TargetAnimationId, out var entryOfTarget);
        var speedOfTarget = hasSetTarget ? proxy.AnimSetsByAnim![(int)proxy.TargetAnimationId].Speed : 0;
        Say($"{label} start: world={(entity.World == null ? "null" : "set")} PosX={proxy.PosX >> 16} px (raw {proxy.PosX}) current={(int)proxy.CurrentAnimationId} target={proxy.TargetAnimationId} targetDirection={proxy.TargetDirection} " +
            $"animSetOfCurrent={hasSet} animSetOfTarget={hasSetTarget} speedOfTarget={speedOfTarget} AnimSets={(proxy.AnimSetsByAnim == null ? -1 : proxy.AnimSetsByAnim.Count)}");

        for (var tick = 1; tick <= 3; tick++)
        {
            string outcome = "ok";
            try
            {
                AlundraScriptedMotion.TickScriptedNpc(proxy);
            }
            catch (Exception e)
            {
                outcome = "EXCEPTION " + e.GetType().Name + ": " + e.Message;
            }

            Assert.Equal("ok", outcome);
            pixels[tick - 1] = proxy.PosX >> 16;
            Say($"{label} after tick {tick} (before any integration): {outcome}; PosX={proxy.PosX >> 16} px (raw {proxy.PosX}) PosY={proxy.PosY >> 16} ForceX={proxy.ForceX} Speed={proxy.Speed} TargetForceX={proxy.TargetForceX}");
        }

        return pixels;
    }

    [Fact]
    public void PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentAsTheInterfaceLeavesIt_MeasuredAfterE0a3b()
    {
        // MEASURED AFTER E20.e0a3b (D-E0A-8, ADR-0065): the entity of the interface carries the marker, so the generic kinematics keys its animation set on the TARGET while Current is still ~0: 3 px at the birth tick, then 6 px
        // per tick (153, 159, 165), as the binary's arrow. The historical measurement of this probe (the state before e0a3b: Current ~0 keys no animation, X = 150, 150, 150) stays written in "Resultat de P-A" of the plan.
        Assert.Equal(new[] { StartX + 3, StartX + 9, StartX + 15 }, Kinematics("PA3a(current=~0, marked)", currentEqualsTarget: false));
    }

    [Fact]
    public void PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentEqualTarget_PosedByHand()
    {
        // With Current = Target (animation 0, speed 512): 3 px after the birth tick (the force ramps to 3 px), then 6 px per tick.
        Assert.Equal(new[] { StartX + 3, StartX + 9, StartX + 15 }, Kinematics("PA3b(current=target)", currentEqualsTarget: true));
    }
}
