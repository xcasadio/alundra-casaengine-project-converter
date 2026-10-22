#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Alundra.Hero;
using Alundra.UI.Speech;
using Alundra.Maps;

namespace Alundra.UI.Hud;

/// <summary>
/// E13 C3 (docs/plan-e13-hud.md, D-E13-8): the seam <see cref="HudViewModel"/> implements (the screen's
/// view model since the bound screens program, parent ADR-0002; <see cref="HudScreen"/> itself before) so
/// <see cref="HudPresenter"/> can push state INTO it every LOGIC tick without <see cref="HudScreen"/>
/// ever depending on <see cref="CasaEngine.Framework.UI.IUIScreen.Update"/> - the exact freeze C2's own
/// class doc left as a P3 for this slice: a modal <c>DialogueScreen</c> stops
/// <see cref="CasaEngine.Framework.UI.ScreenStack"/> from calling <c>Update</c> on every screen below it
/// (<c>CasaEngine/Framework/UI/ScreenStack.cs:5-13</c>), so the jauge must be driven from OUTSIDE that
/// call, exactly like <see cref="HudDirector"/> itself already is (from
/// <see cref="WorldProxy.Update(float)"/>'s own tick loop).
///
/// <para>Also the reason this interface exists at all rather than the presenter reaching straight into
/// <see cref="HudScreen"/>'s own <c>MGCanvas</c>: a real <c>MGWindow</c>/<c>MGCanvas</c> is not
/// constructible headless (<see cref="DialoguePresenterWiringTests"/>'s own class doc: "a real
/// <c>ScreenStack</c>/<c>UIRoot</c> is NOT constructible headless... <c>Push</c> initializes the pushed
/// screen against a live graphics stack"). A recording double of THIS interface is what C3's own tests
/// drive instead - the same shape <see cref="CasaEngine.Framework.UI.IUIViewRuntime"/> already serves for
/// <see cref="DialoguePresenter"/>.</para>
/// </summary>
public interface IHudView
{
    /// <summary>The integer pixel-scale factor this screen derived at <c>OnInitialize</c> time (D-E13-9,
    /// <c>Math.Max(1, viewport width / 320)</c>) - owned by the screen, never by the presenter (D-E13-9:
    /// "l'echelle pixel appartient a l'ecran du HUD"). <see cref="HudPresenter"/> reads it only to
    /// fold it into the translation it pushes, the SAME multiplication <see cref="HudScreen"/>
    /// itself already applies to every tile's own native position (<c>ApplyTile</c>,
    /// <c>tile.NativeX * _pixelScale</c>).</summary>
    int PixelScale { get; }

    /// <summary>Shows or collapses the whole jauge element at once - <see cref="HudDirector.IsDrawn"/>
    /// pushed verbatim (mission item 2: "IsDrawn faux -&gt; element invisible, vrai -&gt; visible").</summary>
    void SetVisible(bool visible);

    /// <summary>The jauge element's own <c>RenderTransform.Translation</c>, already in the SAME scaled
    /// layout-pixel units <see cref="HudScreen"/> positions its tiles in (mission item 1: derived
    /// from the director's own ENTIRE ordinate, never a float carried through an intermediate step - see
    /// <see cref="HudPresenter.Tick"/>'s own body).</summary>
    void SetTranslation(Vector2 translation);

    /// <summary>The freshly recomposed tile list for this tick - <see cref="HudComposer.Compose"/>'s
    /// own return value, applied to the pool exactly like <see cref="HudScreen"/>'s own former
    /// per-frame <c>RefreshFromDirector</c> (C2) did.</summary>
    void SetTiles(IReadOnlyList<HudTile> tiles);

    /// <summary>E13.c S3: the equipment icons for this tick - <see cref="HudComposer.ComposeEquipmentIcons"/>'s
    /// own return value, at most one per box, empty when a box has nothing or the jauge is not drawn.</summary>
    void SetEquipmentIcons(IReadOnlyList<HudIcon> icons);

    /// <summary>The director's clock for this tick, pushed before <see cref="SetTiles"/>: the magic pips and the coin
    /// are UI animations (parent ADR-0002) and start in phase with the original's own counters -
    /// <see cref="HudDirector.FrameCounter"/> (the pips step every 10 of its ticks, the coin every 6) - and
    /// the coin plays only while <see cref="HudDirector.IsMoneyRolling"/>.</summary>
    void SetAnimationClock(int frameCounter, bool moneyRolling);
}
