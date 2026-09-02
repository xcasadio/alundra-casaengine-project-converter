#nullable enable
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// The camera-wiring assertion docs/plan-e10-fondu.md, slice E10.b makes mandatory (blocage de
/// relecture): E10.a's own <see cref="CasaEngine.Framework.Application.Components.ScreenEffectComponent"/>
/// reads the camera off <c>ViewManager.ActiveView.Camera</c>, but nothing under <c>Alundra/Scripts</c>
/// references <c>ActiveView</c> anywhere - <see cref="AlundraBackdropStage"/> instead reuses whatever
/// <see cref="AlundraCameraDirector"/> resolved (the FIRST <see cref="Camera2dComponent"/> found in
/// <c>World.Entities</c>, by component - see that class's own doc). This test proves, on the REAL
/// installation path (<see cref="DefaultRuntimeViewBootstrapper"/>, the same bootstrapper
/// <see cref="CasaEngineGame"/> always uses - nothing under <c>Alundra/Scripts</c> overrides
/// <c>RuntimeViewBootstrapper</c>), that both resolve the SAME <see cref="Camera2dComponent"/> instance:
/// <see cref="DefaultRuntimeViewBootstrapper.BootstrapViews"/> picks its camera the exact same way
/// (<c>world.Entities.Select(e =&gt; e.GetComponent&lt;CameraComponent&gt;()).FirstOrDefault(...)</c>) as
/// <see cref="AlundraCameraDirector.ResolveDebugCameraOnce"/> does for <see cref="Camera2dComponent"/> (a
/// subclass of <see cref="CameraComponent"/>) - same world, same entity order, same "first match" rule.
///
/// Outcome: <c>ActiveView</c> IS present and IS the resolved camera in the Alundra runtime (not absent,
/// not a different camera) - so no STOP is warranted here; the assertion is a passing test, not a
/// silent workaround.
/// </summary>
public class AlundraScreenFadeCameraWiringTests : IDisposable
{
    public AlundraScreenFadeCameraWiringTests()
    {
        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    public void Dispose()
    {
        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    [Fact]
    public void ActiveViewCamera_IsTheSameInstance_AsTheCameraAlundraBackdropStageAlreadyResolves()
    {
        var world = new World { Name = "TestWorld" }; // no map id - BackdropLoader degrades to null, safe.

        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        // ScreenSizeWidth/Height (read by DefaultRuntimeViewBootstrapper.BootstrapViews below) go through
        // Window.ClientBounds unless ExecutionPolicy.UseExternalViewManagement is set - neither a real
        // Window nor a GraphicsDevice exists on this headless-constructed Game, so the external-view-
        // management policy plus a direct field write are what let this run with no device at all.
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorSimulation;
        var screenWidthField = typeof(CasaEngineGame).GetField("_screenSizeWidth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var screenHeightField = typeof(CasaEngineGame).GetField("_screenSizeHeight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        screenWidthField.SetValue(game, 320);
        screenHeightField.SetValue(game, 240);

        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManager = new ViewManager();
        var viewManagerField = typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, viewManager);
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        var camera = new Camera2dComponent();
        var cameraEntity = new Entity { Name = "camera", RootComponent = camera };
        world.Entities.Add(cameraEntity);

        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        // The REAL production bootstrapper - CasaEngineGame's own constructor always wires this one
        // (DefaultRuntimeViewBootstrapper.Instance), and nothing under Alundra/Scripts overrides it.
        DefaultRuntimeViewBootstrapper.Instance.BootstrapViews(game, world, viewManager);

        Assert.NotNull(viewManager.ActiveView);
        Assert.Same(camera, viewManager.ActiveView!.Camera);

        // The real install path: InitializeWithWorld arms the camera resolve lazily, consumed by the
        // first Update (ResolveDebugCameraOnce, called from AlundraCameraDirector).
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no "tileMap" entity -> early return, harmless for this proof.
        proxy.Update(0.02f);

        Assert.Same(camera, proxy._cameraDirector.ResolvedCamera);

        // The point of the whole test: BOTH seams resolved the exact SAME Camera2dComponent instance.
        Assert.Same(viewManager.ActiveView.Camera, proxy._cameraDirector.ResolvedCamera);
    }
}
