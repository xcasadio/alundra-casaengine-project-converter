#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13 C2 acceptance (docs/plan-e13-hud.md, mission item 7): the pure composition function, exercised
/// against HudManager.cs's own DisplayLife (:716-826), DisplayMp (:605-690), DisplayMoney (:857-906) and
/// DisplayHpMaxWithNumber (:910-953) - expected positions re-derived from those citations directly in
/// each test's own comments, never copied from <see cref="AlundraHudComposer"/> itself.
/// </summary>
public sealed class AlundraHudComposerTests
{
    private static readonly IReadOnlyList<int> UnisonPipFrame0 = new[] { 0, 0, 0, 0 };

    [Fact]
    public void Compose_WhenNotDrawn_ReturnsAnEmptyList()
    {
        var tiles = AlundraHudComposer.Compose(
            isDrawn: false,
            hp: 38, hpMax: 45, hpMaxTrue: 45, hpCatchUpPreview: false,
            mp: 2, mpMax: 3, mpCatchUpPreview: false,
            money: 2163, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        Assert.Empty(tiles);
    }

    [Fact]
    public void Compose_DebugStats_38Of45Hp_2Of3Mp_2163Money()
    {
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 38, hpMax: 45, hpMaxTrue: 45, hpCatchUpPreview: false,
            mp: 2, mpMax: 3, mpCatchUpPreview: false,
            money: 2163, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        // DisplayHpMaxWithNumber (:921-933): tens=(45/10)%10=4, units=45%10=5, slash at X=0x90+2*8=0xA0=160.
        Assert.Contains(new AlundraHudTile(HudGlyph.Slash, 160, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit4, 168, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit5, 176, 16), tiles);

        // DisplayLife (:735-765): displayed=38, nbBigIcons=3 (38/10), remainder=8 (38%10), no override.
        // Filled slots 3,2,1 -> X = 0x50+slot*8 = 104, 96, 88.
        Assert.Contains(new AlundraHudTile(HudGlyph.BigHeartFull, 104, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.BigHeartFull, 96, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.BigHeartFull, 88, 16), tiles);
        // nbEmptyBigIcons = (45-1)/10 - 3 = 4-3 = 1, slot = 3-0-3 = 0 -> X = 0x50 = 80.
        Assert.Contains(new AlundraHudTile(HudGlyph.BigHeartEmpty, 80, 16), tiles);
        // remainder=8: 8 full small pips (rank/column), 2 empty (i=8,9).
        Assert.Contains(new AlundraHudTile(HudGlyph.SmallHeartFull, 120, 16), tiles); // i=0 (col0,rank0)
        Assert.Contains(new AlundraHudTile(HudGlyph.SmallHeartFull, 136, 24), tiles); // i=7 (col2,rank1)
        Assert.Contains(new AlundraHudTile(HudGlyph.SmallHeartEmpty, 144, 24), tiles); // i=8 (col3,rank1)
        Assert.Contains(new AlundraHudTile(HudGlyph.SmallHeartEmpty, 152, 24), tiles); // i=9 (col4,rank1)

        // DisplayMp (:617-690): displayedMp=2, both pips full at unison frame 0, X=0xd8+i*8=216,224;
        // then one empty pip at i=2 (< mpMax=3), X=232.
        Assert.Contains(new AlundraHudTile(HudGlyph.MagicPipFull0, 216, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.MagicPipFull0, 224, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.MagicPipEmpty, 232, 16), tiles);
        Assert.DoesNotContain(tiles, t => t.NativeX == 240); // i=3 >= mpMax(3): no tile at all.

        // DisplayMoney (:863-884): 2163 -> thousands=2, hundreds=1, tens=6, units=3, coin frame 0.
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit2, 256, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit1, 264, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit6, 272, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit3, 280, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Coin0, 288, 16), tiles);
    }

    [Fact]
    public void Compose_NewGame_10Of10Hp_ShowsTenSmallHeartsAndNoBigIcons()
    {
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 10, hpMax: 10, hpMaxTrue: 10, hpCatchUpPreview: false,
            mp: 0, mpMax: 0, mpCatchUpPreview: false,
            money: 0, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        // displayed=10, nbBigIcons=1, remainder=0 -> override (displayed>9 && remainder==0):
        // nbBigIcons=0, remainder=10. nbEmptyBigIcons=(10-1)/10-0=0.
        Assert.DoesNotContain(tiles, t => t.Glyph is HudGlyph.BigHeartFull or HudGlyph.BigHeartEmpty);

        var smallHearts = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.SmallHeartFull)
            {
                smallHearts++;
            }
        }

        Assert.Equal(10, smallHearts);
        Assert.DoesNotContain(tiles, t => t.Glyph == HudGlyph.SmallHeartEmpty);

        // DisplayHpMaxWithNumber: tens=(10/10)%10=1, units=10%10=0 -> "10".
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit1, 168, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit0, 176, 16), tiles);
    }

    [Fact]
    public void Compose_Max_50Of50Hp_ShowsFourFullBigHeartsAndTenSmallHearts()
    {
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 50, hpMax: 50, hpMaxTrue: 50, hpCatchUpPreview: false,
            mp: 4, mpMax: 4, mpCatchUpPreview: false,
            money: 9999, coinIconFrame: 3,
            magicPipFrame: UnisonPipFrame0);

        // displayed=50, nbBigIcons=5, remainder=0 -> override: nbBigIcons=4, remainder=10.
        // nbEmptyBigIcons = (50-1)/10 - 4 = 4-4 = 0.
        var bigFull = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.BigHeartFull)
            {
                bigFull++;
            }
        }

        Assert.Equal(4, bigFull);
        Assert.DoesNotContain(tiles, t => t.Glyph == HudGlyph.BigHeartEmpty);

        var smallFull = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.SmallHeartFull)
            {
                smallFull++;
            }
        }

        Assert.Equal(10, smallFull);

        // mp=mpMax=4: four full pips, no empty pip slot at all (loop :670-690 never runs).
        var pipFull = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph is HudGlyph.MagicPipFull0 or HudGlyph.MagicPipFull1
                or HudGlyph.MagicPipFull2 or HudGlyph.MagicPipFull3)
            {
                pipFull++;
            }
        }

        Assert.Equal(4, pipFull);
        Assert.DoesNotContain(tiles, t => t.Glyph == HudGlyph.MagicPipEmpty);
    }

    [Fact]
    public void Compose_ZeroMoney_ShowsFourZeroDigitsWithNoLeadingZeroSuppression()
    {
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 10, hpMax: 10, hpMaxTrue: 10, hpCatchUpPreview: false,
            mp: 0, mpMax: 0, mpCatchUpPreview: false,
            money: 0, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        Assert.Contains(new AlundraHudTile(HudGlyph.Digit0, 256, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit0, 264, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit0, 272, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Digit0, 280, 16), tiles);
        Assert.Contains(new AlundraHudTile(HudGlyph.Coin0, 288, 16), tiles);
    }

    [Fact]
    public void Compose_CatchUpPreview_AddsOnePointToTheDisplayedIconComposition()
    {
        // hp=9 with the preview flag set previews as if hp=10: DisplayLife's own iVar6 = g_playerDataHud[0]+1
        // (HudManager.cs:729-733). displayed=10, override fires -> 10 small full hearts, no big icons.
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 9, hpMax: 10, hpMaxTrue: 10, hpCatchUpPreview: true,
            mp: 0, mpMax: 1, mpCatchUpPreview: false,
            money: 0, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        var smallFull = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.SmallHeartFull)
            {
                smallFull++;
            }
        }

        Assert.Equal(10, smallFull);
        Assert.DoesNotContain(tiles, t => t.Glyph is HudGlyph.BigHeartFull or HudGlyph.BigHeartEmpty);
    }

    [Fact]
    public void Compose_EmptyBigCrystals_AreOrderedLeftToRightSoTheRightmostPaintsOnTop()
    {
        // E13 C2 third-pass verifier finding, point 1. hp=0, hpMaxTrue=45 -> nbBigIcons=0 (0/10),
        // nbEmptyBigIcons=(45-1)/10-0=4 (HudManager.cs:767): all four big-crystal slots start empty.
        // Depth is BackgroundUI-5-i (:777) - i=0 has the group's LARGEST key (least subtracted), so
        // Renderer's ascending-order paint (Renderer.cs:113-119) reaches it LAST, on top; slot =
        // 3-i-nbBigIcons (:771) is 3 (the rightmost of the four) exactly at i=0. So the rightmost empty
        // crystal must be the LAST BigHeartEmpty tile in the composed list, and the leftmost (slot 0,
        // i=3) must be first.
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 0, hpMax: 45, hpMaxTrue: 45, hpCatchUpPreview: false,
            mp: 0, mpMax: 0, mpCatchUpPreview: false,
            money: 0, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        var emptySlotsInPaintOrder = new List<int>();
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.BigHeartEmpty)
            {
                emptySlotsInPaintOrder.Add((tile.NativeX - 0x50) / 8);
            }
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, emptySlotsInPaintOrder);
    }

    [Fact]
    public void Compose_EmptyBigCrystalCount_UsesTheTrueHpMaxNotTheDisplayedLaggingOne()
    {
        // E13 C2 third-pass verifier finding, point 2. hp=20 displayed (nbBigIcons: 20/10=2, remainder
        // 0 -> override :738-742 fires -> nbBigIcons=1, one full crystal at slot 3). hpMax DISPLAYED is
        // still lagging at 30 (mid catch-up toward the true cap), but HudManager.cs:767 reads the TRUE
        // GetPlayerHpMax() for nbEmptyBigIcons, here 50: nbEmptyBigIcons=(50-1)/10-1=3, filling slots
        // 0,1,2 empty. Using the displayed 30 instead (the bug this test guards against) would give
        // (30-1)/10-1=1 empty crystal (slot 2 only) - one full crystal ahead of two crystals that should
        // still read empty.
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 20, hpMax: 30, hpMaxTrue: 50, hpCatchUpPreview: false,
            mp: 0, mpMax: 0, mpCatchUpPreview: false,
            money: 0, coinIconFrame: 0,
            magicPipFrame: UnisonPipFrame0);

        var emptySlots = new List<int>();
        var fullSlots = new List<int>();
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.BigHeartEmpty)
            {
                emptySlots.Add((tile.NativeX - 0x50) / 8);
            }
            else if (tile.Glyph == HudGlyph.BigHeartFull)
            {
                fullSlots.Add((tile.NativeX - 0x50) / 8);
            }
        }

        Assert.Equal(new[] { 3 }, fullSlots);
        emptySlots.Sort();
        Assert.Equal(new[] { 0, 1, 2 }, emptySlots);
    }

    [Fact]
    public void Compose_Max_50Of50Hp_WithCatchUpPreview_ReproducesTheOriginalsUnclampedTransitoryTick()
    {
        // E13 C2 fourth-pass finding: a third-pass clamp made Compose(hp:50, hpMax:50, hpMaxTrue:50,
        // hpCatchUpPreview:true, ...) collapse to the ordinary steady 50/50 picture, reasoning this input
        // combination was unreachable. It IS reachable (AlundraHudDirector.cs:462-473's HpMax catch-up
        // branch previews one tick past HpMaxDisplayed at full HP - see ComposeLife's own doc), so the
        // clamp was itself a regression: this test now fixes what HudManager.cs:727-782 actually DRAWS at
        // that tick, index by index, not what a caller-side guard would prevent.
        //
        // displayed = hp+1 = 51 (:727-733, no upper guard). nbBigIcons=51/10=5, remainder=51%10=1 - the
        // :738-742 override does NOT fire (remainder is 1, not 0).
        //
        // Empty loop (:767-782): nbEmptyBigIcons=(hpMaxTrue-1)/10-nbBigIcons=(50-1)/10-5=4-5=-1 - the loop
        // bound is negative, so it runs zero times: no empty crystal.
        //
        // Full loop (:747-765): i runs 0..4 (nbBigIcons-1=4), slot=3-i gives 3,2,1,0,-1. Slot -1 (i=4) is
        // the original's own out-of-bounds write to g_lifeBigIconSprites[-1] - ComposeLife's own doc
        // establishes that address belongs to no field StaticVariables.cs declares and is never positioned
        // by FUN_8004b770/Fun_8004bea4 as a crystal, so nothing is drawn for it here either. Slots 3,2,1,0
        // ARE the array's real indices 0..3 and each gets exactly the same full-crystal tile the ordinary
        // 50/50 state does.
        //
        // remainder=1: one full small pip (i=0, rank0/col0), nine empty ones (i=1..9) - :787-826.
        var tiles = AlundraHudComposer.Compose(
            isDrawn: true,
            hp: 50, hpMax: 50, hpMaxTrue: 50, hpCatchUpPreview: true,
            mp: 4, mpMax: 4, mpCatchUpPreview: false,
            money: 9999, coinIconFrame: 3,
            magicPipFrame: UnisonPipFrame0);

        // Never a tile at slot -1 (X = BoxX+0x50-8 = 72) - the exact defect the third-pass clamp masked
        // and the fourth-pass finding said must instead simply not be drawn.
        Assert.DoesNotContain(tiles, t => t.NativeX == 72 && t.Glyph is HudGlyph.BigHeartFull or HudGlyph.BigHeartEmpty);

        var bigFull = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph is HudGlyph.BigHeartFull or HudGlyph.BigHeartEmpty)
            {
                Assert.InRange(tile.NativeX, 80, 104); // Slots 0-3 only: X in [80,104].
            }

            if (tile.Glyph == HudGlyph.BigHeartFull)
            {
                bigFull++;
            }
        }

        Assert.Equal(4, bigFull);
        Assert.DoesNotContain(tiles, t => t.Glyph == HudGlyph.BigHeartEmpty);

        var smallFull = 0;
        var smallEmpty = 0;
        foreach (var tile in tiles)
        {
            if (tile.Glyph == HudGlyph.SmallHeartFull)
            {
                smallFull++;
            }
            else if (tile.Glyph == HudGlyph.SmallHeartEmpty)
            {
                smallEmpty++;
            }
        }

        Assert.Equal(1, smallFull);
        Assert.Equal(9, smallEmpty);
        Assert.Contains(new AlundraHudTile(HudGlyph.SmallHeartFull, 120, 16), tiles); // i=0: col0,rank0.

        // Exactly one tile fewer than the 27 the pre-fourth-pass unclamped code would have produced (the
        // dropped slot -1 write): 3 (HpMaxNumber) + 4 (big full) + 0 (big empty) + 1 (small full) +
        // 9 (small empty) + 4 (mp full, mp==mpMax==4) + 5 (money) = 26 - exactly AlundraHudScreen.MaxTileCount.
        Assert.Equal(AlundraHudScreen.MaxTileCount, tiles.Count);
    }

    [Fact]
    public void Compose_NeverExceedsMaxTileCount_AcrossAGridOfHpHpMaxAndPreviewCombinations()
    {
        // E13 C2 fourth pass, mission item 5's second test: AlundraHudScreen's pool (MaxTileCount, sized
        // and derived in AlundraHudScreen.cs's own comment from the original's five HUD sprite arrays)
        // must never receive more tiles than it has slots for, for ANY hp x hpMax x hpMaxTrue x preview
        // combination this pure function can be called with - not just the one transitory tick above.
        // hpMax(True) is bounded to [0,50]: AlundraPlayerManager.SetPlayerHpMax (:656-669, ported from
        // PlayerManager.cs:1705-1731) ceilings any value >= 0x33 down to 0x32=50, so the true cap this
        // grid exercises never exceeds the game's own real ceiling.
        for (var hpMax = 0; hpMax <= 50; hpMax += 5)
        {
            for (var hp = 0; hp <= hpMax + 1; hp++)
            {
                foreach (var hpCatchUpPreview in new[] { false, true })
                {
                    // hpMaxTrue >= hpMax: the director only ever previews growth (HpMax < trueHpMax,
                    // AlundraHudDirector.cs:462), never shrink. Capped at 50 for the same reason as the
                    // outer hpMax loop.
                    foreach (var hpMaxTrue in new[] { hpMax, System.Math.Min(hpMax + 1, 50) })
                    {
                        // mp/mpMax/mpCatchUpPreview held at their own maximal-but-in-bounds combination
                        // (mp==mpMax==4, no MP preview - magicPipFrame only has 4 slots, ComposeMagic's own
                        // doc) since this grid targets the HP-side regression only.
                        var tiles = AlundraHudComposer.Compose(
                            isDrawn: true,
                            hp, hpMax, hpMaxTrue, hpCatchUpPreview,
                            mp: 4, mpMax: 4, mpCatchUpPreview: false,
                            money: 9999, coinIconFrame: 0,
                            magicPipFrame: UnisonPipFrame0);

                        Assert.True(
                            tiles.Count <= AlundraHudScreen.MaxTileCount,
                            $"hp={hp} hpMax={hpMax} hpMaxTrue={hpMaxTrue} hpCatchUpPreview={hpCatchUpPreview} " +
                            $"produced {tiles.Count} tiles, over MaxTileCount={AlundraHudScreen.MaxTileCount}.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// E13 C6 acceptance (docs/plan-e13-hud.md, "Les fonds des cases arme et accessoire"): the two
    /// POLY_G4-equivalent background quads, re-derived directly from HudManager.cs's own citations, never
    /// copied from <see cref="AlundraHudComposer"/> itself.
    /// </summary>
    public sealed class AlundraHudComposerEquipmentBackgroundsTests
    {
        [Fact]
        public void ComposeEquipmentBackgrounds_ReturnsExactlyTwoQuads()
        {
            var quads = AlundraHudComposer.ComposeEquipmentBackgrounds();

            Assert.Equal(2, quads.Count);
        }

        [Fact]
        public void ComposeEquipmentBackgrounds_WeaponQuad_IsAtNativePosition16_16_24x32()
        {
            // HudManager.cs:180-187: X = UIBoxHud.X + g_inventoryWeaponIconX[8 + 0] = 0 + 0x10 = 16,
            // Y = UIBoxHud.Y = 0x10 = 16. Graphics/POLY_G4.cs:3-32: 24x32 (0x18 x 0x20).
            var weapon = AlundraHudComposer.ComposeEquipmentBackgrounds()[0];

            Assert.Equal(16, weapon.NativeX);
            Assert.Equal(16, weapon.NativeY);
            Assert.Equal(24, weapon.NativeWidth);
            Assert.Equal(32, weapon.NativeHeight);
        }

        [Fact]
        public void ComposeEquipmentBackgrounds_AccessoryQuad_IsAtNativePosition48_16_24x32()
        {
            // HudManager.cs:180-187: X = UIBoxHud.X + g_inventoryWeaponIconX[8 + 1] = 0 + 0x30 = 48.
            var accessory = AlundraHudComposer.ComposeEquipmentBackgrounds()[1];

            Assert.Equal(48, accessory.NativeX);
            Assert.Equal(16, accessory.NativeY);
            Assert.Equal(24, accessory.NativeWidth);
            Assert.Equal(32, accessory.NativeHeight);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ComposeEquipmentBackgrounds_BothQuads_HaveTheSameFourCornerColoursAndAlpha(int index)
        {
            // FUN_8004b770:189-201, posed once and shared by both boxes: top-left (0,0,0), top-right
            // (255,255,0), bottom-left (0,255,255), bottom-right (0,0,255). AddQuadColor(polyG4,
            // SpriteDepth.BackgroundUI, 0.5f) at HudManager.cs:587/591: alpha 0.5.
            var quad = AlundraHudComposer.ComposeEquipmentBackgrounds()[index];

            Assert.Equal(new HudRgb(0, 0, 0), quad.TopLeftColor);
            Assert.Equal(new HudRgb(255, 255, 0), quad.TopRightColor);
            Assert.Equal(new HudRgb(0, 255, 255), quad.BottomLeftColor);
            Assert.Equal(new HudRgb(0, 0, 255), quad.BottomRightColor);
            Assert.Equal(0.5f, quad.Alpha);
        }

        [Fact]
        public void ComposeEquipmentBackgrounds_QuadsAreNotCountedInMaxTileCount()
        {
            // C6's own mission item 1: "MaxTileCount ne change pas : ce ne sont pas des tuiles" - this
            // pure function returns its own separate list type (AlundraHudBackgroundQuad, not
            // AlundraHudTile), so it can never contribute to a Compose() tile-overflow count.
            var quads = AlundraHudComposer.ComposeEquipmentBackgrounds();

            Assert.All(quads, quad => Assert.IsType<AlundraHudBackgroundQuad>(quad));
            Assert.Equal(26, AlundraHudScreen.MaxTileCount);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // E13.c S3 (docs/plan-e13c-icones-hud.md): the equipment icons - g_inventoryWeaponIconX[8] = 16 and
    // [9] = 48, both at UIBoxHud.Y = 16, the very positions of the two C6 backgrounds above.
    // -----------------------------------------------------------------------------------------------

    private static readonly System.Guid SwordIcon = System.Guid.Parse("aeebd7a0-faa7-57b0-a844-34470272a4eb");
    private static readonly System.Guid OtherIcon = System.Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void ComposeEquipmentIcons_Weapon_IsAtNative16_16_ExactlyOnItsBackground()
    {
        var icon = Assert.Single(AlundraHudComposer.ComposeEquipmentIcons(isDrawn: true, SwordIcon, null));

        Assert.Equal(new AlundraHudIcon(SwordIcon, 16, 16), icon);
        var weaponBox = AlundraHudComposer.ComposeEquipmentBackgrounds()[0];
        Assert.Equal(weaponBox.NativeX, icon.NativeX);
        Assert.Equal(weaponBox.NativeY, icon.NativeY);
    }

    [Fact]
    public void ComposeEquipmentIcons_Accessory_IsAtNative48_16_ExactlyOnItsBackground()
    {
        var icons = AlundraHudComposer.ComposeEquipmentIcons(isDrawn: true, SwordIcon, OtherIcon);

        Assert.Equal(2, icons.Count);
        Assert.Equal(new AlundraHudIcon(OtherIcon, 48, 16), icons[1]);
        Assert.Equal(AlundraHudComposer.ComposeEquipmentBackgrounds()[1].NativeX, icons[1].NativeX);
    }

    [Fact]
    public void ComposeEquipmentIcons_NothingEquipped_OrJaugeNotDrawn_IsEmpty()
    {
        Assert.Empty(AlundraHudComposer.ComposeEquipmentIcons(isDrawn: true, null, null));
        Assert.Empty(AlundraHudComposer.ComposeEquipmentIcons(isDrawn: false, SwordIcon, OtherIcon));
    }
}
