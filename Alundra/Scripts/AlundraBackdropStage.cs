#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Rendering instance wiring extracted out of <see cref="AlundraWorldProxy"/>
/// (docs/plan-update-caracterisation.md, slice S3), with NO behaviour change - see that plan's §3
/// "S3" for the extraction rules this class follows (the "règle de preuve étendue" defined ahead of
/// S2 and reused here) and AlundraWorldProxyUpdateCharacterizationTests for the oracle this move must
/// keep satisfying with zero assertions changed.
///
/// Owns the one-time background-clear-color lookup (<see cref="ApplyOriginalBackgroundClearColorOnce"/>)
/// and the per-frame backdrop tick/draw (<see cref="UpdateAndDrawBackdrop"/>), plus every field those two
/// exclusively read or write. Built in <see cref="AlundraWorldProxy"/>'s own FIELD INITIALIZER (never
/// lazily from a <c>World</c>, never static, never handed a back-reference to the proxy) - the plan's
/// trap 9: <c>AlundraWorldProxy.Clone</c> returns a bare <c>new AlundraWorldProxy()</c> and copies
/// nothing, which is only safe while every collaborator is constructed exactly that way.
///
/// <c>AlundraWorldProxy.InitializeWithWorld</c> still calls <see cref="BackdropRenderer.Load"/> itself,
/// requalified to <see cref="_backdropRenderer"/>'s new owner (extended proof rule delta (b), same shape
/// as S2's <c>_cameraNeedsSnap</c>) - that <c>Load</c> call needs a live <c>GraphicsDevice</c>, unlike the
/// two members this class owns outright, so it is not itself one of them.
///
/// The world and the resolved debug camera (owned by <see cref="AlundraCameraDirector"/>, S2) are read by
/// the caller at USE TIME and passed in per frame rather than captured here (extended proof rule
/// delta (a)): a stage that captured <c>World</c> at construction would predate
/// <c>AlundraWorldProxy.InitializeWithWorld</c> assigning it, and the resolved camera is this stage's own
/// collaborator's state, not its own - passing it in keeps this class from reaching into
/// <c>AlundraCameraDirector</c> itself.
/// </summary>
internal sealed class AlundraBackdropStage
{
    /// <summary>Renders this world's scrolling background layers (see <see cref="BackdropRenderer"/>'s
    /// class doc) - loaded once by <see cref="AlundraWorldProxy.InitializeWithWorld"/> (requalified field
    /// access, this class' own extended proof rule delta (b)), ticked and drawn every frame by
    /// <see cref="UpdateAndDrawBackdrop"/>. Internal (rather than private) purely so
    /// <c>AlundraWorldProxy.InitializeWithWorld</c> can call <see cref="BackdropRenderer.Load"/> on it
    /// directly, the same shape S2 used for <c>_cameraNeedsSnap</c>.</summary>
    private readonly BackdropRenderer _backdropRenderer = new();

    /// <summary>Cached once <see cref="ApplyOriginalBackgroundClearColorOnce"/> has set the world's
    /// runtime view <see cref="CasaEngine.Framework.Rendering.RenderView.ClearColor"/> - the view does not
    /// exist yet when <see cref="AlundraWorldProxy.InitializeWithWorld"/> runs (<c>GameManager.EndLoadContent</c>
    /// calls <c>World.LoadContent</c>, which drives the proxy, strictly before
    /// <c>IRuntimeViewBootstrapper.BootstrapViews</c>), so the lookup is retried lazily from
    /// <see cref="AlundraWorldProxy.Update"/>, mirroring <c>AlundraCameraDirector</c>'s own
    /// one-time-retry shape for the debug camera lookup.</summary>
    private bool _clearColorApplied;

    /// <summary>Faithful port (E2, docs/plan-e2-rendu.md) of the original engine's own background clear
    /// (<c>AlundraGame.Draw</c>'s <c>GraphicsDevice.Clear(Color.Black)</c>, both for the game's off-screen
    /// render target and the final backbuffer blit -
    /// alundra-datas-analyser/AlundraTools/AlundraGame/AlundraGame.cs:199,236) instead of the engine's
    /// default <c>Color.CornflowerBlue</c> (<see cref="CasaEngine.Framework.Application.DefaultRuntimeViewBootstrapper"/>):
    /// without this, every pixel no cell tile (or, now, no <see cref="BackdropRenderer"/> layer) covers
    /// shows turquoise instead of the black the original always drew there. Retried lazily once per world
    /// from <see cref="AlundraWorldProxy.Update"/> (see <see cref="_clearColorApplied"/>'s own doc for why
    /// <see cref="AlundraWorldProxy.InitializeWithWorld"/> is too early to find the view).
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (extended proof rule delta (a)) -
    /// the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this class never
    /// stores.
    /// </summary>
    internal void ApplyOriginalBackgroundClearColorOnce(World? world)
    {
        if (_clearColorApplied || world?.Game == null)
        {
            return;
        }

        foreach (var view in world.Game.GameManager.ViewManager.Views)
        {
            if (view.World == world)
            {
                view.ClearColor = Color.Black;
                _clearColorApplied = true;
                break;
            }
        }
    }

    /// <summary>Ticks and draws this world's scrolling background layers - see
    /// <see cref="BackdropRenderer"/>'s class doc for the render pass/camera-space reasoning. A no-op
    /// when the world has no backdrop companion at all, or one with neither a Tiles-mode layer nor the
    /// overlay tint (the common case), or before the engine's <see cref="SpriteRendererComponent"/> is
    /// resolvable.
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (extended proof rule delta (a)) -
    /// the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this class never
    /// stores. <paramref name="resolvedCamera"/> is passed in rather than re-resolved (extended proof
    /// rule delta (a), the one named for S3) - it reuses the same <c>Camera2dComponent</c> the debug pan
    /// drives (see <c>AlundraCameraDirector.UpdateDebugCameraPan</c>, which already ran earlier this frame
    /// and resolved it) - both are "the world's camera", and the runtime has no other camera reference yet
    /// (E4 follow-up).</summary>
    internal void UpdateAndDrawBackdrop(float elapsedTime, World? world, Camera2dComponent? resolvedCamera)
    {
        if (!_backdropRenderer.HasContent || world?.Game == null)
        {
            return;
        }

        _backdropRenderer.Tick(elapsedTime);

        var spriteRenderer = world.Game.GetGameComponent<SpriteRendererComponent>();
        if (spriteRenderer == null)
        {
            return;
        }

        var cameraPosition = resolvedCamera?.Target ?? Vector3.Zero;
        _backdropRenderer.Draw(spriteRenderer, cameraPosition, world.Game.ScreenSizeWidth, world.Game.ScreenSizeHeight);
    }

    /// <summary>
    /// Loads this map's backdrop document and textures. Called from
    /// <c>AlundraWorldProxy.InitializeWithWorld</c>, never from the frame path: it needs a live
    /// GraphicsDevice, unlike <see cref="UpdateAndDrawBackdrop"/> which only queues sprites.
    /// Exposed as an operation rather than by handing out <c>_backdropRenderer</c>: a responsibility
    /// callers reach through is not an extracted one (docs/plan-update-caracterisation.md, slice S3).
    /// </summary>
    internal void Load(World world, string projectPath) => _backdropRenderer.Load(world, projectPath);
}
