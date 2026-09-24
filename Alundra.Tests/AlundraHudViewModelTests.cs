#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Alundra.Scripts;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// The bound screens program (parent ADR-0002): <see cref="AlundraHudViewModel"/> places the composer's tiles by role
/// instead of the pool <see cref="AlundraHudScreen"/> used to fill in paint order. These tests pin that it shows the
/// same thing - every tile's sprite at its position, the full pips and the coin as their UI animations - and that it
/// starts those animations in phase with the original's counters.
/// </summary>
public sealed class AlundraHudViewModelTests
{
    private static readonly string PipAnimation = AlundraHudViewModel.MagicPipAnimationId;
    private static readonly string CoinAnimation = AlundraHudViewModel.CoinAnimationId;
    private const string EmptyPipSprite = "ace49f58-4b1c-54c1-8a08-52ecec193305"; // wind_104

    private static IReadOnlyList<AlundraHudTile> Compose(int hp, int hpMax, int mp, int mpMax, int money, int coinFrame, int frameCounter)
    {
        var phase = (frameCounter / 10) % 4;
        var pipFrames = new[] { phase % 4, (phase + 1) % 4, (phase + 2) % 4, (phase + 3) % 4 };
        return AlundraHudComposer.Compose(true, hp, hpMax, hpMax, false, mp, mpMax, false, money, coinFrame, pipFrames);
    }

    private static IEnumerable<HudImageViewModel> Tiles(AlundraHudViewModel viewModel) => new[]
    {
        viewModel.HpMaxSlash, viewModel.HpMaxTens, viewModel.HpMaxUnits,
        viewModel.LifeBig0, viewModel.LifeBig1, viewModel.LifeBig2, viewModel.LifeBig3,
        viewModel.LifeSmall0, viewModel.LifeSmall1, viewModel.LifeSmall2, viewModel.LifeSmall3, viewModel.LifeSmall4,
        viewModel.LifeSmall5, viewModel.LifeSmall6, viewModel.LifeSmall7, viewModel.LifeSmall8, viewModel.LifeSmall9,
        viewModel.MagicPip0, viewModel.MagicPip1, viewModel.MagicPip2, viewModel.MagicPip3,
        viewModel.MoneyDigit0, viewModel.MoneyDigit1, viewModel.MoneyDigit2, viewModel.MoneyDigit3,
        viewModel.Coin,
    };

    /// <summary>What the old pool drew for a tile: the glyph's sprite at the tile's position, except the full pips and
    /// the coin, which are now their animations.</summary>
    private static (string Source, int X, int Y) Expected(AlundraHudTile tile) => tile.Glyph switch
    {
        HudGlyph.MagicPipFull0 or HudGlyph.MagicPipFull1 or HudGlyph.MagicPipFull2 or HudGlyph.MagicPipFull3 => (PipAnimation, tile.NativeX, tile.NativeY),
        HudGlyph.Coin0 or HudGlyph.Coin1 or HudGlyph.Coin2 or HudGlyph.Coin3 => (CoinAnimation, tile.NativeX, tile.NativeY),
        _ => (SpriteOf(tile.Glyph), tile.NativeX, tile.NativeY),
    };

    /// <summary>The sprite of every static glyph, as the screen resolved it before the program
    /// (<c>AlundraHudScreen.LoadSprites</c> at <c>79c0099</c>), copied here so the view model's own table is checked
    /// against it rather than against itself.</summary>
    private static string SpriteOf(HudGlyph glyph) => glyph switch
    {
        HudGlyph.Digit0 => "bc300193-4244-5138-a27f-f242a150bed7",
        HudGlyph.Digit1 => "01dbdef2-854c-5c76-a482-e19bc579fa80",
        HudGlyph.Digit2 => "91a9c466-d267-5063-a60d-8f4b60cf2a41",
        HudGlyph.Digit3 => "fc448c9c-7759-58dc-a85d-68c02bc09380",
        HudGlyph.Digit4 => "0740074c-09c3-5bfd-b982-45b75869ce6e",
        HudGlyph.Digit5 => "fbeb05c1-a686-5e2d-a08a-13a6f8287b96",
        HudGlyph.Digit6 => "fb437995-e6fd-51f0-ad3e-4d6dcc51dbcc",
        HudGlyph.Digit7 => "c5af13a8-6890-56bb-b2a4-eb3912509c93",
        HudGlyph.Digit8 => "d12a690e-48ba-5a1c-84e1-d1f56d8bb3ec",
        HudGlyph.Digit9 => "cd607fcd-8657-5856-9136-76e1194389c9",
        HudGlyph.Slash => "55ad7755-c9a1-5be8-ba45-8dacaf4ca628",
        HudGlyph.BigHeartFull => "8a38c0d4-f55a-55b7-96d5-32c21172f1e0",
        HudGlyph.BigHeartEmpty => "0cca1fb2-b8d5-5169-9719-567e5562e03f",
        HudGlyph.SmallHeartFull => "1784c287-534c-5f7d-a2fc-5d81a22e7557",
        HudGlyph.SmallHeartEmpty => "935a267e-1cda-5c56-a438-817fec8c0b45",
        HudGlyph.MagicPipEmpty => EmptyPipSprite,
        _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, "an animated glyph has no single sprite"),
    };

    [Fact]
    public void AcrossAGridOfStates_TheVisibleElements_AreExactlyTheComposersTiles()
    {
        for (var hpMax = 1; hpMax <= 40; hpMax += 3)
        {
            for (var hp = 0; hp <= hpMax; hp += 2)
            {
                for (var mpMax = 0; mpMax <= 4; mpMax++)
                {
                    for (var mp = 0; mp <= mpMax; mp++)
                    {
                        var viewModel = new AlundraHudViewModel();
                        IAlundraHudView view = viewModel;
                        var tiles = Compose(hp, hpMax, mp, mpMax, money: 1234, coinFrame: 2, frameCounter: 17);
                        view.SetAnimationClock(17, moneyRolling: true);
                        view.SetTiles(tiles);

                        var shown = Tiles(viewModel)
                            .Where(image => image.Visibility == Visibility.Visible)
                            .Select(image => (Source: image == viewModel.Coin ? CoinAnimation : image.SourceName!, X: image.Left!.Value, Y: image.Top!.Value))
                            .OrderBy(entry => entry.X).ThenBy(entry => entry.Y).ThenBy(entry => entry.Source, StringComparer.Ordinal)
                            .ToList();
                        var expected = tiles.Select(Expected)
                            .OrderBy(entry => entry.X).ThenBy(entry => entry.Y).ThenBy(entry => entry.Source, StringComparer.Ordinal)
                            .ToList();

                        Assert.Equal(expected, shown);
                        Assert.Equal(0, viewModel.TileOverflowCount);
                    }
                }
            }
        }
    }

    [Fact]
    public void TheSameTilesAgain_NotifyNothing()
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        view.SetVisible(true);
        view.SetAnimationClock(17, moneyRolling: true);
        view.SetTiles(Compose(9, 12, 3, 4, 1234, 2, 17));

        var notifications = 0;
        void Count(object? sender, PropertyChangedEventArgs e) => notifications++;
        viewModel.PropertyChanged += Count;
        foreach (var image in Tiles(viewModel))
        {
            image.PropertyChanged += Count;
        }

        view.SetVisible(true);
        view.SetAnimationClock(18, moneyRolling: true);
        view.SetTiles(Compose(9, 12, 3, 4, 1234, 2, 18));

        Assert.Equal(0, notifications);
    }

    /// <summary>A full pip <c>i</c> starts at the original's frame <c>(FrameCounter / 10 + i) % 4</c>, that many ticks
    /// into it - and every full pip is restarted together, so the four keep their 200 ms offsets.</summary>
    [Fact]
    public void FullPips_StartInPhaseWithTheOriginal_AndRestartTogetherWhenThePipsChange()
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        view.SetVisible(true);
        view.SetAnimationClock(37, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 2, 4, 0, 0, 37));

        // FrameCounter 37: phase 3, 7 ticks into it. Pip 0 at frame 3, pip 1 at frame 0.
        Assert.Equal(PipAnimation, viewModel.MagicPip0.SourceName);
        Assert.Equal(TimeSpan.FromMilliseconds((3 * 10 + 7) * 20), viewModel.MagicPip0.AnimationStartOffset);
        Assert.Equal(TimeSpan.FromMilliseconds((0 * 10 + 7) * 20), viewModel.MagicPip1.AnimationStartOffset);
        Assert.Equal(EmptyPipSprite, viewModel.MagicPip2.SourceName);
        Assert.True(viewModel.MagicPip0.IsAnimationPlaying);

        // Nothing changed: no restart (the offsets stay those of tick 37).
        view.SetAnimationClock(38, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 2, 4, 0, 0, 38));
        Assert.Equal(TimeSpan.FromMilliseconds((3 * 10 + 7) * 20), viewModel.MagicPip0.AnimationStartOffset);

        // A third pip fills at tick 45 (phase 0, 5 ticks in): the three restart together, pip 2 two frames ahead.
        view.SetAnimationClock(45, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 3, 4, 0, 0, 45));
        Assert.Equal(TimeSpan.FromMilliseconds((0 * 10 + 5) * 20), viewModel.MagicPip0.AnimationStartOffset);
        Assert.Equal(TimeSpan.FromMilliseconds((1 * 10 + 5) * 20), viewModel.MagicPip1.AnimationStartOffset);
        Assert.Equal(TimeSpan.FromMilliseconds((2 * 10 + 5) * 20), viewModel.MagicPip2.AnimationStartOffset);
        Assert.Equal(PipAnimation, viewModel.MagicPip2.SourceName);
    }

    [Fact]
    public void Pips_RestartWhenTheJaugeReappears()
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        view.SetVisible(true);
        view.SetAnimationClock(37, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 1, 4, 0, 0, 37));

        view.SetVisible(false);
        view.SetTiles(Array.Empty<AlundraHudTile>());
        view.SetVisible(true);
        view.SetAnimationClock(52, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 1, 4, 0, 0, 52));

        // Tick 52: phase 1, 2 ticks in.
        Assert.Equal(TimeSpan.FromMilliseconds((1 * 10 + 2) * 20), viewModel.MagicPip0.AnimationStartOffset);
    }

    /// <summary>The coin plays from the original's frame and tick while the money rolls (it advances every 6 ticks,
    /// at <c>FrameCounter % 6 == 0</c>), and holds its first frame once the roll settles.</summary>
    [Fact]
    public void Coin_PlaysInPhaseWhileTheMoneyRolls_AndHoldsItsFirstFrameOnceSettled()
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        view.SetVisible(true);

        view.SetAnimationClock(40, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 0, 0, 100, 0, 40));
        Assert.False(viewModel.Coin.IsAnimationPlaying);

        // Rolling starts at tick 41 with the coin still at frame 0, 5 ticks since tick 36's boundary.
        view.SetAnimationClock(41, moneyRolling: true);
        view.SetTiles(Compose(5, 5, 0, 0, 110, 0, 41));
        Assert.True(viewModel.Coin.IsAnimationPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds((0 * 6 + 5) * 20), viewModel.Coin.AnimationStartOffset);

        // Still rolling: no restart.
        view.SetAnimationClock(42, moneyRolling: true);
        view.SetTiles(Compose(5, 5, 0, 0, 120, 1, 42));
        Assert.Equal(TimeSpan.FromMilliseconds((0 * 6 + 5) * 20), viewModel.Coin.AnimationStartOffset);

        view.SetAnimationClock(60, moneyRolling: false);
        view.SetTiles(Compose(5, 5, 0, 0, 200, 0, 60));
        Assert.False(viewModel.Coin.IsAnimationPlaying);
        Assert.Equal(TimeSpan.Zero, viewModel.Coin.AnimationStartOffset);
    }

    /// <summary>The jauge centres an icon in SCREEN pixels (<see cref="AlundraHudIcon.ScreenLeft"/>): at scale 4, the
    /// one free native pixel of a 23-wide icon in its 24-wide box puts it 2 screen pixels in. The canvas is scaled
    /// as a whole, so the whole native part goes to the position and the half pixel to the sub-pixel offset.</summary>
    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(1)]
    public void EquipmentIcons_LandOnTheSameScreenPixel_AsTheScreenCentredThem(int scale)
    {
        var weapon = Guid.Parse("aeebd7a0-faa7-57b0-a844-34470272a4eb");
        var viewModel = new AlundraHudViewModel(_ => new Point(23, 29)) { PixelScale = scale };
        IAlundraHudView view = viewModel;

        view.SetEquipmentIcons(AlundraHudComposer.ComposeEquipmentIcons(true, weapon, null));

        var box = AlundraHudComposer.ComposeEquipmentIcons(true, weapon, null)[0];
        var icon = viewModel.EquipmentIcon0;
        Assert.Equal(box.ScreenLeft(23, scale), (icon.Left!.Value + icon.SubPixelOffset.X) * scale, 3);
        Assert.Equal(box.ScreenTop(29, scale), (icon.Top!.Value + icon.SubPixelOffset.Y) * scale, 3);
        Assert.InRange(icon.SubPixelOffset.X, 0f, 0.999f);
        Assert.InRange(icon.SubPixelOffset.Y, 0f, 0.999f);
    }

    [Fact]
    public void EquipmentIcons_AreCentredInTheirBox_FromTheirSpriteSize_AndHiddenWithoutOne()
    {
        var weapon = Guid.Parse("aeebd7a0-faa7-57b0-a844-34470272a4eb");
        var unreadable = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var viewModel = new AlundraHudViewModel(id => id == weapon ? new Point(16, 20) : null);
        IAlundraHudView view = viewModel;

        view.SetEquipmentIcons(AlundraHudComposer.ComposeEquipmentIcons(true, weapon, unreadable));

        var box = AlundraHudComposer.ComposeEquipmentIcons(true, weapon, null)[0];
        Assert.Equal(weapon.ToString("D"), viewModel.EquipmentIcon0.SourceName);
        Assert.Equal(box.ScreenLeft(16, 1), viewModel.EquipmentIcon0.Left);
        Assert.Equal(box.ScreenTop(20, 1), viewModel.EquipmentIcon0.Top);
        Assert.Equal(Visibility.Visible, viewModel.EquipmentIcon0.Visibility);
        Assert.Equal(Visibility.Collapsed, viewModel.EquipmentIcon1.Visibility);
    }
}
