#nullable enable
using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Cameras;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Maps;

namespace Alundra.Tests.Cameras;

/// <summary>
/// E19.k1 K2b (docs/plan-e19-opcodes.md, section 1.2k.1): the sway through the real <see cref="WorldProxy.Update"/>
/// - one site (<c>AlundraCameraDirector.UpdateCameraFollow</c>), exactly <c>ticksThisFrame</c> steps, with or without a
/// camera, and none during a transition. Montage of <c>AlundraWorldProxyUpdateCharacterizationTests</c>: headless world, a
/// camera entity added to <c>World.Entities</c>, a still followed entity, <c>proxy.Update(0.02f)</c> = one 50 Hz tick.
/// </summary>
public sealed class CameraSwayWiringTests : IDisposable
{
    public CameraSwayWiringTests()
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

    private static void PoseSway(CameraSway sway) => sway.Start(1, 1, 3, 2);

    [Fact]
    public void WithACamera_ThreeFrames_MoveTheTargetByTheIntegralOfTheOffset()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();

        // Rest: frames without sway until the camera stops.
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var rest = CameraMath.ToOriginalScrollSpace(camera.Target);

        PoseSway(CameraSway.Instance);

        var xs = new int[3];
        var ys = new int[3];
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
            var (x, y) = CameraMath.ToOriginalScrollSpace(camera.Target);
            xs[i] = x - rest.X;
            ys[i] = y - rest.Y;
        }

        Assert.Equal(new[] { -1, -3, -6 }, xs);
        Assert.Equal(new[] { -1, -3, -4 }, ys);
    }

    [Fact]
    public void WithoutACamera_ThreeFrames_StillStepTheSwayOncePerTick()
    {
        var world = new World { Name = "TestWorld" };
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        PoseSway(CameraSway.Instance);

        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var sway = CameraSway.Instance;
        Assert.Equal((-3, -1), (sway.OffsetX, sway.OffsetY));
        Assert.Equal((1, 1), (sway.ReachX, sway.ReachY));
    }

    [Fact]
    public void DuringATransition_NeitherTheSwayStepsNorTheTargetMoves()
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

        var sway = CameraSway.Instance;
        PoseSway(sway);
        sway.Step();
        var swayBefore = (sway.Flag, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY);
        var targetBefore = camera.Target;

        var state = new GameState();
        var player = new EntityScriptProxy
        {
            IsPlayer = true,
            PosX = (18 * 24 + 12) << 16,
            PosY = (38 * 16 + 8) << 16,
        };
        WarpDirector.Instance.BeginDeparture(
            new PortalRecord
            {
                Index = 0, X1 = 18, Y1 = 38, X2 = 18, Y2 = 38, DestMapId = 390, DestTileX = 10, DestTileY = 40,
                ZLevel = 0, Flags = 0x5001,
            },
            0x10, player, state);
        Assert.True(WarpDirector.Instance.IsTransitionInProgress);

        proxy.Update(0.02f);

        Assert.Equal(swayBefore, (sway.Flag, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY));
        Assert.Equal(targetBefore, camera.Target);
    }
}
