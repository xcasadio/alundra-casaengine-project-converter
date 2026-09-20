#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// E13 C3 (docs/plan-e13-hud.md, D-E13-8): the seam <see cref="AlundraHudScreen"/> implements so
/// <see cref="AlundraHudPresenter"/> can push state INTO it every LOGIC tick without <see cref="AlundraHudScreen"/>
/// ever depending on <see cref="CasaEngine.Framework.UI.IUIScreen.Update"/> - the exact freeze C2's own
/// class doc left as a P3 for this slice: a modal <c>DialogueScreen</c> stops
/// <see cref="CasaEngine.Framework.UI.ScreenStack"/> from calling <c>Update</c> on every screen below it
/// (<c>CasaEngine/Framework/UI/ScreenStack.cs:5-13</c>), so the jauge must be driven from OUTSIDE that
/// call, exactly like <see cref="AlundraHudDirector"/> itself already is (from
/// <see cref="AlundraWorldProxy.Update(float)"/>'s own tick loop).
///
/// <para>Also the reason this interface exists at all rather than the presenter reaching straight into
/// <see cref="AlundraHudScreen"/>'s own <c>MGCanvas</c>: a real <c>MGWindow</c>/<c>MGCanvas</c> is not
/// constructible headless (<see cref="AlundraDialoguePresenterWiringTests"/>'s own class doc: "a real
/// <c>ScreenStack</c>/<c>UIRoot</c> is NOT constructible headless... <c>Push</c> initializes the pushed
/// screen against a live graphics stack"). A recording double of THIS interface is what C3's own tests
/// drive instead - the same shape <see cref="CasaEngine.Framework.UI.IUIViewRuntime"/> already serves for
/// <see cref="AlundraDialoguePresenter"/>.</para>
/// </summary>
public interface IAlundraHudView
{
    /// <summary>The integer pixel-scale factor this screen derived at <c>OnInitialize</c> time (D-E13-9,
    /// <c>Math.Max(1, viewport width / 320)</c>) - owned by the screen, never by the presenter (D-E13-9:
    /// "l'echelle pixel appartient a l'ecran du HUD"). <see cref="AlundraHudPresenter"/> reads it only to
    /// fold it into the translation it pushes, the SAME multiplication <see cref="AlundraHudScreen"/>
    /// itself already applies to every tile's own native position (<c>ApplyTile</c>,
    /// <c>tile.NativeX * _pixelScale</c>).</summary>
    int PixelScale { get; }

    /// <summary>Shows or collapses the whole jauge element at once - <see cref="AlundraHudDirector.IsDrawn"/>
    /// pushed verbatim (mission item 2: "IsDrawn faux -&gt; element invisible, vrai -&gt; visible").</summary>
    void SetVisible(bool visible);

    /// <summary>The jauge element's own <c>RenderTransform.Translation</c>, already in the SAME scaled
    /// layout-pixel units <see cref="AlundraHudScreen"/> positions its tiles in (mission item 1: derived
    /// from the director's own ENTIRE ordinate, never a float carried through an intermediate step - see
    /// <see cref="AlundraHudPresenter.Tick"/>'s own body).</summary>
    void SetTranslation(Vector2 translation);

    /// <summary>The freshly recomposed tile list for this tick - <see cref="AlundraHudComposer.Compose"/>'s
    /// own return value, applied to the pool exactly like <see cref="AlundraHudScreen"/>'s own former
    /// per-frame <c>RefreshFromDirector</c> (C2) did.</summary>
    void SetTiles(IReadOnlyList<AlundraHudTile> tiles);
}

/// <summary>
/// E13 C3 (docs/plan-e13-hud.md, D-E13-8: "le tick logique possede tout le temps du HUD. MGUI dessine.").
/// Reads <see cref="AlundraHudDirector"/>'s own already-ticked state and pushes it into an
/// <see cref="IAlundraHudView"/> - never the reverse, and never on its own clock: one <see cref="Tick"/>
/// call is made from <see cref="AlundraWorldProxy.Update(float)"/>'s own <c>ticksThisFrame</c> loop,
/// immediately after <see cref="AlundraHudDirector.Tick"/> itself - the same "presenter wraps the engine
/// seam, director stays ignorant of it" shape <see cref="AlundraDialoguePresenter"/> already uses for the
/// dialogue box (E12's own patron the mission cites), except this presenter never pushes/pops any screen
/// itself: <see cref="AlundraHudScreen"/> is pushed once by <see cref="AlundraWorldProxy"/>'s own
/// <c>TryWireHudScreenOnce</c> and stays up for the whole session (C2), so there is no open/close state to
/// unwind here.
///
/// <b>No MGUI animation anywhere in this class</b> (D-E13-8/mission item 3): the translation below is a
/// plain assignment of an already-known integer, never a <c>UIAnimation</c>/<c>UIAnimationClock</c>/
/// <c>EnterExit</c> call - the 18 values of plan §1.3 are the director's OWN tween output (C1), not
/// something this class or MGUI interpolates a second time.
/// </summary>
public sealed class AlundraHudPresenter
{
    // AlundraHudComposer's own private "BoxY = 0x10" (HudManager.cs:33-36/52-54, UIBoxHud.Y) is baked into
    // EVERY tile's own NativeY that composer emits (AlundraHudComposer.cs, every ComposeXxx method) - this
    // presenter re-declares the SAME literal (not a shared constant - the same "duplicated citation, not a
    // cross-file constant" shape AlundraHudScreen's own NativeWidth already re-declares from the
    // converter) because it needs to know it too: AlundraHudDirector.Y IS that same UIBoxHud.Y ordinate,
    // resting at 16 (AlundraHudDirector.OpenTargetY) whenever the jauge is merely Displayed. Since every
    // tile position the composer emits is already drawn AS IF Y == 16, this presenter's own translation
    // must carry only the DELTA away from that baked rest value - (Y - 16) * scale, never Y * scale alone -
    // or the jauge would sit twice as far from its resting position at every non-resting Y. Mission item 1's
    // own "ATTENTION AU DOUBLE DECALAGE".
    private const int BakedBoxY = 0x10;

    private readonly AlundraHudDirector _director;
    private readonly IAlundraHudView _view;

    public AlundraHudPresenter(AlundraHudDirector director, IAlundraHudView view)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(view);
        _director = director;
        _view = view;
    }

    /// <summary>
    /// One LOGIC tick: push visibility, translation and the recomposed tile list. All three read the
    /// director's CURRENT (already-ticked) state - this method never advances anything itself, and must be
    /// called strictly AFTER <see cref="AlundraHudDirector.Tick"/> for the same tick (production call site:
    /// <see cref="AlundraWorldProxy.Update(float)"/>'s own <c>hudTick</c> loop).
    /// </summary>
    public void Tick()
    {
        _view.SetVisible(_director.IsDrawn);

        // Both intermediate values stay integers - mission item 1's own "tout doit rester entier: jamais
        // de flottant intermediaire" - the Vector2 below is the ONLY place a float appears, and only
        // because UIRenderTransform.Translation itself is a Vector2 (UIRenderTransform.cs:32).
        var nativeDeltaY = _director.Y - BakedBoxY;
        var scaledDeltaY = nativeDeltaY * _view.PixelScale;
        _view.SetTranslation(new Vector2(0f, scaledDeltaY));

        _view.SetTiles(AlundraHudComposer.Compose(
            _director.IsDrawn,
            _director.Hp, _director.HpMax, _director.TrueHpMax, _director.HpDisplayPreviewIncrement,
            _director.Mp, _director.MpMax, _director.MpDisplayPreviewIncrement,
            _director.Money, _director.CoinIconFrame,
            _director.MagicPipFrame));
    }
}
