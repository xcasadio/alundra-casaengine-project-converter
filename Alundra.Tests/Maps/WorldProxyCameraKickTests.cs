#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Ai.Items;
using Alundra.Audio;
using Alundra.Cameras;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Maps;

/// <summary>
/// E20.h4c2 T1 (docs/plan-e20-heros.md section 1.31.4, D-H4C2-2, tests (k2) and (k3)): the camera kick through the real <see cref="WorldProxy"/>. E13 (<see cref="EarthTome2Ai.ImpactDeactivate"/>) calls <c>IEntityWorldContext.SetCameraKick(2)</c>; the world keeps ONE kick per logic tick of the
/// frame (the binary's byte is assigned: two E13 at the same tick give 2, never 4), filed at the index of the tick that wrote it, consumed by the camera step of the same frame and emptied AFTER it (never at the head of the frame, which would lose the kick of an entity that ticked before
/// <c>Update</c>). (k2) is a driven world (the real arena of map 14: the entities tick inside <c>Update</c>, the kicks are written in the tick of their entity block); (k3) a world with no driver (a headless world: the entities ticked by themselves before <c>Update</c>, the kick goes to the
/// index 0 of the image, and the camera step of the same image applies it).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WorldProxyCameraKickTests : IDisposable
{
    public WorldProxyCameraKickTests()
    {
        WorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        Reset();
    }

    public void Dispose()
    {
        WorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        Reset();
    }

    private static void Reset()
    {
        CameraSway.Instance.ResetForTests();
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k2) the driven world: the index of the tick, the assignment, the emptying after the camera step.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K2_TheKickIsFiledAtTheIndexOfItsTick_TwoE13AtTheSameTickGive2NotFour_TheListIsEmptyAfterTheCameraStepOfTheFrame()
    {
        using var arena = new HitArena("h4c2-k2", 100, 100, new List<int[]>());
        var proxy = arena.Arc.Proxy;
        var previous = proxy.TickTraceForTests;
        var tickOfTheFrame = -1;
        var plan = new Dictionary<int, int[]>(); // the kicks each tick of the frame writes, in order (the calls of E13).
        var seen = new List<int[]>(); // the list of the frame as the last tick left it, read at its inventory pass (still inside Update).
        var lastTick = 0;
        proxy.TickTraceForTests = pass =>
        {
            previous?.Invoke(pass);
            switch (pass)
            {
                case "pad":
                    tickOfTheFrame++;
                    break;
                case "programs":
                    foreach (var kick in plan.GetValueOrDefault(tickOfTheFrame) ?? Array.Empty<int>())
                    {
                        ((IEntityWorldContext)proxy).SetCameraKick(kick);
                    }

                    break;
                case "inventory" when tickOfTheFrame == lastTick:
                    seen.Add(proxy.CameraKickOfTickForTests.ToArray());
                    break;
            }
        };

        // One frame of three ticks: two E13 at the tick 0, none at the tick 1, one at the tick 2.
        tickOfTheFrame = -1;
        lastTick = 2;
        plan[0] = new[] { 2, 2 };
        plan[2] = new[] { 2 };
        arena.Frame(0, 3);

        Assert.Equal(new[] { new[] { 2, 0, 2 } }, seen);
        Assert.Empty(proxy.CameraKickOfTickForTests); // emptied after the camera step of the frame.

        // One frame of two ticks, the kick at the second: the list is the image's own, not the previous one's.
        tickOfTheFrame = -1;
        lastTick = 1;
        plan.Clear();
        plan[1] = new[] { 2 };
        arena.Frame(0, 2);
        Assert.Equal(new[] { 2, 0, 2 }, seen[0]);
        Assert.Equal(new[] { 0, 2 }, seen[1]);
        Assert.Empty(proxy.CameraKickOfTickForTests);

        // A frame with no kick leaves an empty list (or zeros), and one tick per frame puts the kick at the index 0.
        tickOfTheFrame = -1;
        lastTick = 0;
        plan.Clear();
        plan[0] = new[] { 2 };
        arena.Frame(0, 1);
        Assert.Equal(new[] { 2 }, seen[2]);
        Assert.Empty(proxy.CameraKickOfTickForTests);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (k3) a world with no driver: E13 files its kick at the index 0 before Update, the camera step of the same image applies it.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Camera2dComponent AddCameraEntity(World world)
    {
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });
        return camera;
    }

    private static EntityScriptProxy StillTarget() => new()
    {
        Status = EntityStatus.Normal,
        PosX = 100 << 16,
        PosY = 200 << 16,
        PosZ = 0,
    };

    private static (int X, int Y) Scroll(Camera2dComponent camera) => CameraMath.ToOriginalScrollSpace(camera.Target);

    [Fact]
    public void K3_AWorldWithNoDriver_E13FilesTheKickAtTheIndex0BeforeUpdate_TheCameraStepOfTheSameImageAppliesIt_TheListIsEmptyAfterwards()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var rest = Scroll(camera);
        var rock = new EntityScriptProxy { SpriteType = 17, IsOnGround = 1, Flags = 0x132014, ScriptHost = proxy };

        EarthTome2Ai.ImpactDeactivate(rock); // the entity ticked before Update, with no driver: the kick of this tick of E13.

        Assert.Equal(new[] { 2 }, proxy.CameraKickOfTickForTests);
        proxy.Update(0.02f);
        Assert.Equal((rest.X, rest.Y + 2), Scroll(camera)); // the scroll Y of the image carries the kick.
        Assert.Empty(proxy.CameraKickOfTickForTests);

        // The kick is not a bump that returns by itself and is not applied twice: the next images pull back as the formula says (the gap -2, then -1, then rest).
        proxy.Update(0.02f);
        Assert.Equal((rest.X, rest.Y + 1), Scroll(camera));
        proxy.Update(0.02f);
        Assert.Equal((rest.X, rest.Y), Scroll(camera));
    }

    [Fact]
    public void K3_AnImageOfTwoTicksWithNoDriver_TheKickIsTheFirstTickOfTheImage_TheSecondPullsBack()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var rest = Scroll(camera);
        ((IEntityWorldContext)proxy).SetCameraKick(2);

        proxy.Update(0.04f); // two ticks in the image: the kick is at the first (+2), the second pulls back (+1).

        Assert.Equal((rest.X, rest.Y + 1), Scroll(camera));
        Assert.Empty(proxy.CameraKickOfTickForTests);
    }

    [Fact]
    public void K3_DuringAWarpTransition_NoKickIsApplied_AndTheListIsEmptiedAllTheSame()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var targetBefore = camera.Target;
        var state = new GameState();
        var player = new EntityScriptProxy { IsPlayer = true, PosX = (18 * 24 + 12) << 16, PosY = (38 * 16 + 8) << 16 };
        WarpDirector.Instance.BeginDeparture(
            new PortalRecord { Index = 0, X1 = 18, Y1 = 38, X2 = 18, Y2 = 38, DestMapId = 390, DestTileX = 10, DestTileY = 40, ZLevel = 0, Flags = 0x5001 },
            0x10, player, state);
        Assert.True(WarpDirector.Instance.IsTransitionInProgress);
        ((IEntityWorldContext)proxy).SetCameraKick(2);

        proxy.Update(0.02f);

        Assert.Equal(targetBefore, camera.Target); // neither the sway nor the kick moves the camera during a transition.
        Assert.Empty(proxy.CameraKickOfTickForTests);
    }

    [Fact]
    public void K3_AWorldWithNoCamera_StillEmptiesTheList_TheKickIsNotKeptForALaterImage()
    {
        var world = new World { Name = "TestWorld" };
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);

        ((IEntityWorldContext)proxy).SetCameraKick(2);
        proxy.Update(0.02f);

        Assert.Empty(proxy.CameraKickOfTickForTests);
    }
}
