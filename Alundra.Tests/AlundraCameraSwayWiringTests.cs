#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.k1 K2b (docs/plan-e19-opcodes.md, section 1.2k.1): the sway through the real <see cref="AlundraWorldProxy.Update"/>
/// - one site (<c>AlundraCameraDirector.UpdateCameraFollow</c>), exactly <c>ticksThisFrame</c> steps, with or without a
/// camera, and none during a transition. Montage of <c>AlundraWorldProxyUpdateCharacterizationTests</c>: headless world, a
/// camera entity added to <c>World.Entities</c>, a still followed entity, <c>proxy.Update(0.02f)</c> = one 50 Hz tick.
/// </summary>
public sealed class AlundraCameraSwayWiringTests : IDisposable
{
    public AlundraCameraSwayWiringTests()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        Reset();
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        Reset();
    }

    private static void Reset()
    {
        AlundraCameraSway.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static Camera2dComponent AddCameraEntity(World world)
    {
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });
        return camera;
    }

    private static AlundraEntityScriptProxy StillTarget() => new()
    {
        Status = EntityStatus.Normal,
        PosX = 100 << 16,
        PosY = 200 << 16,
        PosZ = 0,
    };

    private static void PoseSway(AlundraCameraSway sway) => sway.Start(1, 1, 3, 2);

    [Fact]
    public void WithACamera_ThreeFrames_MoveTheTargetByTheIntegralOfTheOffset()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();

        // Rest: frames without sway until the camera stops.
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var rest = AlundraCameraMath.ToOriginalScrollSpace(camera.Target);

        PoseSway(AlundraCameraSway.Instance);

        var xs = new int[3];
        var ys = new int[3];
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
            var (x, y) = AlundraCameraMath.ToOriginalScrollSpace(camera.Target);
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
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        PoseSway(AlundraCameraSway.Instance);

        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var sway = AlundraCameraSway.Instance;
        Assert.Equal((-3, -1), (sway.OffsetX, sway.OffsetY));
        Assert.Equal((1, 1), (sway.ReachX, sway.ReachY));
    }

    [Fact]
    public void DuringATransition_NeitherTheSwayStepsNorTheTargetMoves()
    {
        var world = new World { Name = "TestWorld" };
        var camera = AddCameraEntity(world);
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);
        proxy.EntityFollowedByCamera = StillTarget();
        for (var i = 0; i < 3; i++)
        {
            proxy.Update(0.02f);
        }

        var sway = AlundraCameraSway.Instance;
        PoseSway(sway);
        sway.Step();
        var swayBefore = (sway.Flag, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY);
        var targetBefore = camera.Target;

        var state = new AlundraGameState();
        var player = new AlundraEntityScriptProxy
        {
            IsPlayer = true,
            PosX = (18 * 24 + 12) << 16,
            PosY = (38 * 16 + 8) << 16,
        };
        AlundraWarpDirector.Instance.BeginDeparture(
            new AlundraPortalRecord
            {
                Index = 0, X1 = 18, Y1 = 38, X2 = 18, Y2 = 38, DestMapId = 390, DestTileX = 10, DestTileY = 40,
                ZLevel = 0, Flags = 0x5001,
            },
            0x10, player, state);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);

        proxy.Update(0.02f);

        Assert.Equal(swayBefore, (sway.Flag, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY));
        Assert.Equal(targetBefore, camera.Target);
    }
}
