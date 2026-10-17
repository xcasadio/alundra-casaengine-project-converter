#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Alundra.Hero;
using Alundra.UI.Speech;
using Alundra.Maps;

namespace Alundra.UI.Hud;

/// <summary>
/// E13 C3 (docs/plan-e13-hud.md, D-E13-8: "le tick logique possede tout le temps du HUD. MGUI dessine.").
/// Reads <see cref="HudDirector"/>'s own already-ticked state and pushes it into an
/// <see cref="IHudView"/> - never the reverse, and never on its own clock: one <see cref="Tick"/>
/// call is made from <see cref="WorldProxy.Update(float)"/>'s own <c>ticksThisFrame</c> loop,
/// immediately after <see cref="HudDirector.Tick"/> itself - the same "presenter wraps the engine
/// seam, director stays ignorant of it" shape <see cref="DialoguePresenter"/> already uses for the
/// dialogue box (E12's own patron the mission cites), except this presenter never pushes/pops any screen
/// itself: <see cref="HudScreen"/> is pushed once by <see cref="WorldProxy"/>'s own
/// <c>TryWireHudScreenOnce</c> and stays up for the whole session (C2), so there is no open/close state to
/// unwind here.
///
/// <b>No MGUI animation anywhere in this class</b> (D-E13-8/mission item 3): the translation below is a
/// plain assignment of an already-known integer, never a <c>UIAnimation</c>/<c>UIAnimationClock</c>/
/// <c>EnterExit</c> call - the 18 values of plan §1.3 are the director's OWN tween output (C1), not
/// something this class or MGUI interpolates a second time.
/// </summary>
public sealed class HudPresenter
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

    private readonly HudDirector _director;
    private readonly IHudView _view;
    private readonly Func<(Guid? Weapon, Guid? Accessory)> _equipmentSource;

    /// <param name="equipmentSource">E13.c S3: what the two equipment boxes show, asked once per drawn tick -
    /// in production <see cref="PlayerManager.ResolveHudEquipmentIcons"/> over the session state. Omitted,
    /// both boxes stay empty, which is what every presenter built before S3 showed.</param>
    public HudPresenter(
        HudDirector director,
        IHudView view,
        Func<(Guid? Weapon, Guid? Accessory)>? equipmentSource = null)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(view);
        _director = director;
        _view = view;
        _equipmentSource = equipmentSource ?? (static () => (null, null));
    }

    /// <summary>
    /// One LOGIC tick: push visibility, translation and the recomposed tile list. All three read the
    /// director's CURRENT (already-ticked) state - this method never advances anything itself, and must be
    /// called strictly AFTER <see cref="HudDirector.Tick"/> for the same tick (production call site:
    /// <see cref="WorldProxy.Update(float)"/>'s own <c>hudTick</c> loop).
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

        _view.SetAnimationClock(_director.FrameCounter, _director.IsMoneyRolling);
        _view.SetTiles(HudComposer.Compose(
            _director.IsDrawn,
            _director.Hp, _director.HpMax, _director.TrueHpMax, _director.HpDisplayPreviewIncrement,
            _director.Mp, _director.MpMax, _director.MpDisplayPreviewIncrement,
            _director.Money, _director.CoinIconFrame,
            _director.MagicPipFrame));

        // E13.c S3: asked only while the jauge is drawn. The original resolves both boxes inside the HUD's
        // own draw (HudManager.DisplayHudWeaponAndItem), and the accessory lookup has a side effect
        // (AlundraPlayerManager.SetItemIdFromCurrentItemId), so a hidden jauge must not run it.
        var (weapon, accessory) = _director.IsDrawn ? _equipmentSource() : (null, null);
        _view.SetEquipmentIcons(HudComposer.ComposeEquipmentIcons(_director.IsDrawn, weapon, accessory));
    }
}
