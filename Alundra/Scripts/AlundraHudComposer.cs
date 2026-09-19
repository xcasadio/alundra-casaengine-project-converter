#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// E13 C2 (docs/plan-e13-hud.md, tranche "Écran MGUI : la composition statique"): the semantic identity
/// of one of the jauge's 24 ACTIVE tiles (relevé C, all 24 re-read from data-extracted/ui/wind.json's own
/// array position, reproduced identically by alundra-project/UI/wind-sprites.json's own "index"/
/// "asset_id" - the wind index -> asset-id mapping this enum stands in for lives on
/// <see cref="AlundraHudScreen"/>, never here, so this file stays MGUI/asset-agnostic (mission item 7's
/// own "fonction pure").
/// </summary>
public enum HudGlyph
{
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Slash,
    BigHeartFull,
    BigHeartEmpty,
    SmallHeartFull,
    SmallHeartEmpty,
    MagicPipFull0,
    MagicPipFull1,
    MagicPipFull2,
    MagicPipFull3,
    MagicPipEmpty,
    Coin0,
    Coin1,
    Coin2,
    Coin3,
}

/// <summary>One drawn tile: which glyph, at which NATIVE (320x236, unscaled) pixel position. The screen
/// (E13 C2) multiplies both coordinates by the integer pixel-scale factor before handing them to MGUI -
/// this struct itself carries no notion of scale.</summary>
public readonly record struct AlundraHudTile(HudGlyph Glyph, int NativeX, int NativeY);

/// <summary>
/// Pure port of the jauge's own composition logic - <c>HudManager.DisplayHpMaxWithNumber</c>
/// (HudManager.cs:910-953), <c>DisplayLife</c> (:716-826), <c>DisplayMp</c> (:605-690 minus its own
/// animated-icon computation, which <see cref="AlundraHudDirector.MagicPipFrame"/> already owns, C1) and
/// <c>DisplayMoney</c> (:857-906). Takes only the DIRECTOR's own DISPLAYED (rolled) values - never
/// <see cref="AlundraPlayerStats"/> directly (mission item 2: "la composition suit les valeurs affichées
/// (roulées) du directeur, pas les vraies stats") - and returns the flat list of tiles the original would
/// actually have called <c>Renderer.AddSprite</c> for, in the same left-to-right, top-to-bottom order the
/// original's own loops draw them. No MGUI type appears anywhere in this file (mission item 2's own "il
/// ne tique rien... le directeur reste ignorant de MGUI" - the composer stays ignorant of it too, that is
/// what makes it independently testable, mission item 7).
/// </summary>
public static class AlundraHudComposer
{
    // UIBoxHud = { X = 0, Y = 0x10 } (StaticVariables.cs:12270, HudManager.cs:33-36/52-54). Named
    // constants (not inlined 0/16) so every offset below reads exactly like HudManager.cs's own
    // "UIBoxHud.X + ..." / "UIBoxHud.Y + ..." expressions - X is kept even though it is always 0 for
    // this box (AlundraHudDirector's own tween-field doc: "the tween's X axis is degenerate for the HUD
    // specifically") so a future non-zero UIBoxHud.X would only need this one constant changed.
    private const int BoxX = 0;
    private const int BoxY = 0x10;

    private static readonly HudGlyph[] Digits =
    {
        HudGlyph.Digit0, HudGlyph.Digit1, HudGlyph.Digit2, HudGlyph.Digit3, HudGlyph.Digit4,
        HudGlyph.Digit5, HudGlyph.Digit6, HudGlyph.Digit7, HudGlyph.Digit8, HudGlyph.Digit9,
    };

    private static readonly HudGlyph[] MagicPipFullByFrame =
    {
        HudGlyph.MagicPipFull0, HudGlyph.MagicPipFull1, HudGlyph.MagicPipFull2, HudGlyph.MagicPipFull3,
    };

    private static readonly HudGlyph[] CoinByFrame =
    {
        HudGlyph.Coin0, HudGlyph.Coin1, HudGlyph.Coin2, HudGlyph.Coin3,
    };

    /// <param name="isDrawn"><see cref="AlundraHudDirector.IsDrawn"/> - false returns an empty list
    /// (mission item 7's own "IsDrawn faux -&gt; liste vide"), the outer guard every original Display*
    /// call sits behind (HudManager.cs:223, Fun_8004bea4).</param>
    /// <param name="hp">Displayed/rolled current HP - <see cref="AlundraHudDirector.Hp"/> ([0]).</param>
    /// <param name="hpMax">Displayed/rolled HP max - <see cref="AlundraHudDirector.HpMax"/> ([1]).</param>
    /// <param name="hpMaxTrue">The TRUE current HP cap - <see cref="AlundraHudDirector.TrueHpMax"/>. Read
    /// only where the original itself reads it (<c>DisplayLife</c>'s own <c>GetPlayerHpMax()</c> call,
    /// HudManager.cs:767, for the empty-big-crystal count) - every OTHER original Display* read in this
    /// composer uses the rolled <paramref name="hpMax"/> instead (mission item 2's own audit: :921/:926,
    /// :863-881 and :617-690 all read <c>g_playerDataHud</c>, never a "true" getter).</param>
    /// <param name="hpCatchUpPreview">DisplayLife's own <c>g_playerDataHud[5] != 0 || g_playerDataHud[6]
    /// != 0</c> (HudManager.cs:729-730) - true while either the current-HP or the HP-max catch-up
    /// sub-step is mid-cycle, previewing the icon composition one point ahead of <paramref name="hp"/>
    /// itself.</param>
    /// <param name="mp">Displayed/rolled current MP - <see cref="AlundraHudDirector.Mp"/> ([2]).</param>
    /// <param name="mpMax">Displayed/rolled MP max - <see cref="AlundraHudDirector.MpMax"/> ([3]).</param>
    /// <param name="mpCatchUpPreview">DisplayMp's own <c>g_playerDataHud[7] != 0 || g_playerDataHud[8]
    /// != 0</c> (HudManager.cs:620-621), the MP counterpart of <paramref name="hpCatchUpPreview"/>.</param>
    /// <param name="money">Displayed/rolled money - <see cref="AlundraHudDirector.Money"/> ([4]), always
    /// rendered as exactly 4 digits, no leading-zero suppression (HudManager.cs:863-881).</param>
    /// <param name="coinIconFrame"><see cref="AlundraHudDirector.CoinIconFrame"/> ([9]).</param>
    /// <param name="magicPipFrame"><see cref="AlundraHudDirector.MagicPipFrame"/> - one animation frame
    /// per magic pip (0..3), read only for the pips actually drawn full (<paramref name="mp"/> of them).</param>
    public static IReadOnlyList<AlundraHudTile> Compose(
        bool isDrawn,
        int hp, int hpMax, int hpMaxTrue, bool hpCatchUpPreview,
        int mp, int mpMax, bool mpCatchUpPreview,
        int money, int coinIconFrame,
        IReadOnlyList<int> magicPipFrame)
    {
        var tiles = new List<AlundraHudTile>(26);

        if (!isDrawn)
        {
            return tiles;
        }

        ComposeHpMaxNumber(tiles, hpMax);
        ComposeLife(tiles, hp, hpMax, hpMaxTrue, hpCatchUpPreview);
        ComposeMagic(tiles, mp, mpMax, mpCatchUpPreview, magicPipFrame);
        ComposeMoney(tiles, money, coinIconFrame);

        return tiles;
    }

    /// <summary>Port of <c>DisplayHpMaxWithNumber</c> (HudManager.cs:910-953). Only 3 of the original's 5
    /// <c>g_HpMaxNumberSprites</c> slots (indices 2, 3, 4) are ever filled with content and drawn - indices
    /// 0 and 1 are repositioned every frame (Fun_8004bea4:243-249) but never given a glyph nor an
    /// <c>AddSprite</c> call anywhere in this function's own loop (which starts at <c>i = 2</c>,
    /// HudManager.cs:918/938), so they are not ported at all: the original itself never draws them.</summary>
    private static void ComposeHpMaxNumber(List<AlundraHudTile> tiles, int hpMax)
    {
        var tens = (hpMax / 10) % 10; // HudManager.cs:921.
        var units = hpMax % 10; // HudManager.cs:926.

        tiles.Add(new AlundraHudTile(HudGlyph.Slash, BoxX + 0x90 + 2 * 8, BoxY)); // :932-933.
        tiles.Add(new AlundraHudTile(Digits[tens], BoxX + 0x90 + 3 * 8, BoxY)); // :923-924.
        tiles.Add(new AlundraHudTile(Digits[units], BoxX + 0x90 + 4 * 8, BoxY)); // :928-929.
    }

    /// <summary>Port of <c>DisplayLife</c> (HudManager.cs:716-826): the big-crystal row (up to 4 slots,
    /// filled right-to-left then empty continuing left) plus the small-pip row (10 slots, two ranks of
    /// 5, always either full or empty, never hidden).</summary>
    private static void ComposeLife(List<AlundraHudTile> tiles, int hp, int hpMaxDisplayed, int hpMaxTrue, bool catchUpPreview)
    {
        // :727-733, ported verbatim - NO upper guard. A third-pass verifier finding clamped this to
        // hpMaxDisplayed, reasoning that hp==hpMaxDisplayed with the preview flag set was unreachable; a
        // fourth-pass review showed that reasoning wrong and the clamp itself a regression. It IS reachable
        // through the real director: AlundraHudDirector.UpdateHpAndMpCatchUp's HpMax catch-up branch
        // (AlundraHudDirector.cs:462-473) only starts once Hp==hpTarget==HpMax already (hpTarget itself is
        // bounded to HpMax at :432-435), and HpDisplayPreviewIncrement (:131, "_hpSubStep != 0 ||
        // _hpMaxSubStep != 0") stays true for the 3 sub-steps before HpMax itself is bumped at :471-472 -
        // e.g. picking up a heart container at full HP. The original DOES draw this tick's HUD with
        // "displayed" one past hpMaxDisplayed, so the port must too, not clamp it away.
        var displayed = hp + (catchUpPreview ? 1 : 0);

        var nbBigIcons = displayed / 10; // :735.
        var remainder = displayed % 10; // :736.

        if (displayed > 9 && remainder == 0) // :738-742 - avoids an empty small-pip row after an exact multiple of 10.
        {
            nbBigIcons -= 1;
            remainder = 10;
        }

        // Painting order (E13 C2 verifier finding, P2): the 16px-wide crystals sit on an 8px pitch
        // (:94-96) so each one overlaps its neighbour by 8px, which makes DRAW ORDER visible. The
        // original paints full crystals at SpriteDepth.ForegroundUI - i with i = 0 for slot 3 (:751/
        // :757) and empty ones at SpriteDepth.BackgroundUI - 5 - i (:777); SpriteDepth.BackgroundUI =
        // ForegroundUI - 1 (SpriteDepth.cs:5-11) so every empty crystal's depth is always lower than
        // every full one's. Renderer.Render walks its depth buckets in ASCENDING key order and the
        // last bucket painted ends up on top (Graphics/Renderer.cs:7/113-119) - same rule an MGCanvas
        // applies to its children (last added paints last, on top).
        //
        // Within the EMPTY group itself (E13 C2 third-pass verifier finding, point 1): depth is
        // BackgroundUI-5-i (:777), which DECREASES as i increases, so i=0 has the LARGEST depth key of
        // the group - the one ascending-order iteration reaches LAST, i.e. on top. slot = 3-i-nbBigIcons
        // (:771) is at its LARGEST (rightmost, immediately left of the lowest filled crystal) exactly at
        // i=0. So the rightmost empty crystal paints last (on top of its left neighbour), and the
        // leftmost (largest i, smallest slot) paints first. Emitting this composer's own list in
        // INCREASING i order (as the previous revision did) put the rightmost crystal FIRST instead -
        // backwards - so the loop below now walks i from nbEmptyBigIcons-1 down to 0, which yields slot
        // in INCREASING order (leftmost emitted first, rightmost last/topmost), matching the full-crystal
        // loop right after it (which also ends on slot 3, the rightmost, last/topmost).
        //
        // :767 - the ORIGINAL reads the TRUE current HpMax (GetPlayerHpMax()), never the displayed/rolled
        // g_playerDataHud[1] every OTHER Display* read in this composer uses (mission item 2's audit:
        // ComposeHpMaxNumber's :921/:926 and ComposeMoney's :863-881 both read g_playerDataHud, and
        // ComposeMagic's :617-690 reads g_playerDataHud[3] too - DisplayLife's own nbEmptyBigIcons is the
        // ONE exception). hpMaxTrue is threaded in for exactly this one read; every other computation in
        // this method still uses hpMaxDisplayed, per that same audit.
        var nbEmptyBigIcons = (hpMaxTrue - 1) / 10 - nbBigIcons; // :767-782.
        for (var i = nbEmptyBigIcons - 1; i >= 0; i--) // :767-782 - leftmost first, rightmost (i=0) last/topmost.
        {
            var slot = 3 - i - nbBigIcons;
            tiles.Add(new AlundraHudTile(HudGlyph.BigHeartEmpty, BoxX + 0x50 + slot * 8, BoxY));
        }

        // :747-765 - addresses g_lifeBigIconSprites[3-i] for i in 0..nbBigIcons-1, the SAME "3-i" formula
        // (and the same BoxX+0x50+slot*8 position FUN_8004b770 gives index "slot", HudManager.cs:95) as the
        // empty loop above - not remapped. For nbBigIcons<=4 (i in 0..3) every index stays inside the
        // array's own 0..4 range (3-i gives 3,2,1,0). nbBigIcons==5 is reachable now that the clamp above
        // is gone (hp==hpMaxDisplayed with the preview flag): i=4 then gives 3-4=-1, ONE SPRT-sized (0x14
        // byte, the standard PSX libgpu primitive size confirmed by every other 0x14-stride table index in
        // this file, e.g. DisplayMoney's "iVar4 % 10 * 0x14", HudManager.cs:866) region immediately BEFORE
        // g_lifeBigIconSprites[0] - i.e. address 0x80175e08-0x14=0x80175df4. That is NOT index 4 (X=112),
        // which FUN_8004b770 (HudManager.cs:95-96) and Fun_8004bea4's own per-frame reposition loop
        // (HudManager.cs:255-259) both DO position every frame but which this "3-i" formula never reaches
        // for any i. Where that address really lands: the HUD primitive arrays are DOUBLE-BUFFERED - the
        // original addresses a second bank at +5 (g_lifeBigIconSprites[iVar1 + 5], HudManager.cs:760) and
        // at +12 (g_lifeSmallIconSprites[... + 0xc], HudManager.cs:802) - and the declared addresses in
        // StaticVariables.cs:13148-13153 confirm it, each array spanning twice its element count
        // (0x80175ed8 - 0x80175e08 = 0xD0 = 10 SPRT for the 5 declared big crystals, and so on down the
        // list). So g_HpMaxNumberSprites occupies 10 SPRT from 0x80175d38 to 0x80175e00, and 0x80175df4
        // falls 8 bytes INSIDE element 9 of its second bank, misaligned by part of a primitive header. What
        // the original chains from that torn primitive is undefined; it is in no case a fifth crystal, and
        // neither positioning function above ever gives that address an x0/y0 as a big-life-crystal sprite.
        // This composer reproduces what is actually DRAWN, so slot -1 renders no tile at all. (A first
        // draft of this note claimed the address fell in an unlabeled gap after g_HpMaxNumberSprites; the
        // fourth-pass verifier refuted that from :760/:802 and the address strides, and this note now says
        // what the source says.)
        for (var i = nbBigIcons - 1; i >= 0; i--)
        {
            var slot = 3 - i;
            if (slot < 0)
            {
                continue;
            }

            tiles.Add(new AlundraHudTile(HudGlyph.BigHeartFull, BoxX + 0x50 + slot * 8, BoxY));
        }

        for (var i = 0; i < remainder; i++) // :787-807 - full pips, rank-major (5 per rank).
        {
            var rank = i / 5;
            var column = i % 5;
            tiles.Add(new AlundraHudTile(HudGlyph.SmallHeartFull, BoxX + 0x78 + column * 8, BoxY + rank * 8));
        }

        for (var i = remainder; i < 10; i++) // :810-826 - empty pips filling the remaining ranks.
        {
            var rank = i / 5;
            var column = i % 5;
            tiles.Add(new AlundraHudTile(HudGlyph.SmallHeartEmpty, BoxX + 0x78 + column * 8, BoxY + rank * 8));
        }
    }

    /// <summary>Port of <c>DisplayMp</c> (HudManager.cs:605-690), minus its own animated-icon computation
    /// (:634-646, C1's <see cref="AlundraHudDirector.MagicPipFrame"/> already owns that per §6 point 4's
    /// unison table). Full pips 0..mp-1 (animated), empty pips mp..mpMax-1; a pip index &gt;= mpMax gets no
    /// tile at all (the original's own loop bound, HudManager.cs:674/689, never draws it).</summary>
    private static void ComposeMagic(
        List<AlundraHudTile> tiles, int mp, int mpMaxDisplayed, bool catchUpPreview, IReadOnlyList<int> magicPipFrame)
    {
        var displayedMp = mp + (catchUpPreview ? 1 : 0); // :617-624.

        for (var i = 0; i < displayedMp; i++) // :628-668.
        {
            var frame = magicPipFrame[i] % 4; // defensive against a future per-pip table, §6 point 4.
            tiles.Add(new AlundraHudTile(MagicPipFullByFrame[frame], BoxX + 0xd8 + i * 8, BoxY));
        }

        for (var i = displayedMp; i < mpMaxDisplayed; i++) // :670-690.
        {
            tiles.Add(new AlundraHudTile(HudGlyph.MagicPipEmpty, BoxX + 0xd8 + i * 8, BoxY));
        }
    }

    /// <summary>Port of <c>DisplayMoney</c> (HudManager.cs:857-906): always exactly 4 digits (thousands,
    /// hundreds, tens, units, in that left-to-right screen order) with no leading-zero suppression, plus
    /// the coin icon.</summary>
    private static void ComposeMoney(List<AlundraHudTile> tiles, int money, int coinIconFrame)
    {
        var thousands = money / 1000 % 10; // :864-868.
        var hundreds = money / 100 % 10; // :870-872.
        var tens = money / 10 % 10; // :875-877.
        var units = money % 10; // :879-881.

        tiles.Add(new AlundraHudTile(Digits[thousands], BoxX + 0x100 + 0 * 8, BoxY));
        tiles.Add(new AlundraHudTile(Digits[hundreds], BoxX + 0x100 + 1 * 8, BoxY));
        tiles.Add(new AlundraHudTile(Digits[tens], BoxX + 0x100 + 2 * 8, BoxY));
        tiles.Add(new AlundraHudTile(Digits[units], BoxX + 0x100 + 3 * 8, BoxY));
        tiles.Add(new AlundraHudTile(CoinByFrame[coinIconFrame], BoxX + 0x100 + 4 * 8, BoxY)); // :883-884.
    }
}
