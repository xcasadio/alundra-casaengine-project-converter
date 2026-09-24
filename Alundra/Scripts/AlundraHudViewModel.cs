#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.Scripts;

/// <summary>
/// One image of the HUD as its XAML binds it (parent ADR-0002): source (an asset id the engine's UI asset provider
/// resolves, a sprite or a UI animation), native canvas position, visibility, and - for an animation - where it
/// restarts and whether it plays. Every setter notifies only on an actual change.
/// </summary>
public sealed class HudImageViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;
    private TimeSpan _animationStartOffset;
    private bool _isAnimationPlaying = true;
    private Vector2 _subPixelOffset;

    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName != value)
            {
                _sourceName = value;
                _sourceId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
                NotifyPropertyChanged();
            }
        }
    }

    public int? Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                _left = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int? Top
    {
        get => _top;
        set
        {
            if (_top != value)
            {
                _top = value;
                NotifyPropertyChanged();
            }
        }
    }

    public Visibility Visibility
    {
        get => _visibility;
        set
        {
            if (_visibility != value)
            {
                _visibility = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Where the image's animation restarts (MGImage restarts it when this changes).</summary>
    public TimeSpan AnimationStartOffset
    {
        get => _animationStartOffset;
        set
        {
            if (_animationStartOffset != value)
            {
                _animationStartOffset = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Whether the image's animation plays; MGImage restarts it at <see cref="AnimationStartOffset"/> on
    /// every change, and holds that frame while false.</summary>
    public bool IsAnimationPlaying
    {
        get => _isAnimationPlaying;
        set
        {
            if (_isAnimationPlaying != value)
            {
                _isAnimationPlaying = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>The part of the position finer than a native pixel, in native pixels (0 or a fraction): the screen
    /// applies it to the image's <c>RenderTransform</c>, which MGUI's XAML cannot bind (gap G9). Only the equipment
    /// icons use it: they are centred in screen pixels (<see cref="AlundraHudIcon.ScreenLeft"/>).</summary>
    public Vector2 SubPixelOffset
    {
        get => _subPixelOffset;
        set
        {
            if (_subPixelOffset != value)
            {
                _subPixelOffset = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Sets <see cref="SourceName"/> to <paramref name="assetId"/>, formatting the id only when it changed.</summary>
    internal void SetSource(Guid assetId)
    {
        if (_sourceId != assetId || _sourceName == null)
        {
            SourceName = assetId.ToString("D");
        }
    }

    internal void Show(int left, int top)
    {
        Left = left;
        Top = top;
        Visibility = Visibility.Visible;
    }

    /// <summary>Restarts the animation at <paramref name="offset"/> and plays it, even when the offset did not change:
    /// the play flag's two changes each restart it (MGImage), so the image is in phase whatever it showed before.</summary>
    internal void RestartAt(TimeSpan offset)
    {
        AnimationStartOffset = offset;
        IsAnimationPlaying = false;
        IsAnimationPlaying = true;
    }
}

/// <summary>
/// The HUD's view model (parent ADR-0002, engine ADR-0038): what <c>UI/Screens/HudScreen.xaml</c> binds, one named
/// member per element, and the <see cref="IAlundraHudView"/> <see cref="AlundraHudPresenter"/> writes every logic
/// tick. It places the composer's tiles by role - the three HP-max tiles, up to four big and ten small life tiles,
/// up to four magic pips, four money digits and the coin - instead of the pool the screen used to fill in paint
/// order, so an element never changes role and its animation never restarts because its neighbours moved.
/// <para/>
/// The magic pips and the coin are UI animations written by the converter (<c>UI/Animations/ui_hud_magic_pip</c> and
/// <c>ui_hud_coin</c>), played on the UI clock. They start in phase with the original's counters: a pip <c>i</c>
/// shows frame <c>(FrameCounter / 10 + i) % 4</c> (<c>AlundraHudDirector.UpdateMagicPipPhase</c>), the coin advances
/// every 6 ticks while the money rolls and holds its first frame otherwise. Every full pip is restarted together
/// whenever the pips change (a pip fills or empties, the maximum changes, the HUD reappears), so the four keep their
/// 200 ms offsets from one another; the coin is restarted when it starts rolling.
/// </summary>
public sealed class AlundraHudViewModel : ViewModelBase, IAlundraHudView
{
    private const double TickMilliseconds = 20.0;
    private const int PipTicksPerFrame = 10;
    private const int CoinTicksPerFrame = 6;

    // UI/Animations/ui_hud_magic_pip.anim2d and ui_hud_coin.anim2d (the converter's Ids.For("anim2d-ui:" + name)).
    internal const string MagicPipAnimationId = "ca0a227b-1e10-591f-9802-05de254d0492";
    internal const string CoinAnimationId = "aee15589-1e91-5a53-b5d0-6226e12256a9";

    // The wind_NNN sprite of every static glyph (AlundraHudComposer's HudGlyph); the pips and the coin are animations.
    private static readonly Dictionary<HudGlyph, Guid> GlyphSprites = new()
    {
        [HudGlyph.Digit0] = Guid.Parse("bc300193-4244-5138-a27f-f242a150bed7"), // wind_000
        [HudGlyph.Digit1] = Guid.Parse("01dbdef2-854c-5c76-a482-e19bc579fa80"), // wind_002
        [HudGlyph.Digit2] = Guid.Parse("91a9c466-d267-5063-a60d-8f4b60cf2a41"), // wind_009
        [HudGlyph.Digit3] = Guid.Parse("fc448c9c-7759-58dc-a85d-68c02bc09380"), // wind_016
        [HudGlyph.Digit4] = Guid.Parse("0740074c-09c3-5bfd-b982-45b75869ce6e"), // wind_023
        [HudGlyph.Digit5] = Guid.Parse("fbeb05c1-a686-5e2d-a08a-13a6f8287b96"), // wind_030
        [HudGlyph.Digit6] = Guid.Parse("fb437995-e6fd-51f0-ad3e-4d6dcc51dbcc"), // wind_037
        [HudGlyph.Digit7] = Guid.Parse("c5af13a8-6890-56bb-b2a4-eb3912509c93"), // wind_045
        [HudGlyph.Digit8] = Guid.Parse("d12a690e-48ba-5a1c-84e1-d1f56d8bb3ec"), // wind_055
        [HudGlyph.Digit9] = Guid.Parse("cd607fcd-8657-5856-9136-76e1194389c9"), // wind_065
        [HudGlyph.Slash] = Guid.Parse("55ad7755-c9a1-5be8-ba45-8dacaf4ca628"), // wind_075
        [HudGlyph.BigHeartFull] = Guid.Parse("8a38c0d4-f55a-55b7-96d5-32c21172f1e0"), // wind_094
        [HudGlyph.BigHeartEmpty] = Guid.Parse("0cca1fb2-b8d5-5169-9719-567e5562e03f"), // wind_125
        [HudGlyph.SmallHeartFull] = Guid.Parse("1784c287-534c-5f7d-a2fc-5d81a22e7557"), // wind_113
        [HudGlyph.SmallHeartEmpty] = Guid.Parse("935a267e-1cda-5c56-a438-817fec8c0b45"), // wind_121
        [HudGlyph.MagicPipEmpty] = Guid.Parse("ace49f58-4b1c-54c1-8a08-52ecec193305"), // wind_104
    };

    private readonly Func<Guid, Point?>? _iconSize;
    private readonly HudImageViewModel[] _hpMax = { new(), new(), new() };
    private readonly HudImageViewModel[] _lifeBig = { new(), new(), new(), new() };
    private readonly HudImageViewModel[] _lifeSmall = { new(), new(), new(), new(), new(), new(), new(), new(), new(), new() };
    private readonly HudImageViewModel[] _magicPips = { new(), new(), new(), new() };
    private readonly HudImageViewModel[] _moneyDigits = { new(), new(), new(), new() };
    private readonly HudImageViewModel[] _equipmentIcons = { new(), new() };

    private Visibility _rootVisibility = Visibility.Collapsed;
    private Vector2 _translation;
    private int _frameCounter;
    private bool _moneyRolling;
    private bool _coinPlaying;
    private int _pipFullMask = -1;
    private int _pipCount = -1;
    private bool _animationsNeedRestart = true;

    /// <summary>A view model with no icon sizes: the editor's design-time data, which gives every position itself.</summary>
    public AlundraHudViewModel()
        : this(null)
    {
    }

    /// <param name="iconSize">The native size of an equipment icon sprite, or null when it cannot be read: an icon is
    /// centred in its box from its size (D-E13D-10), and stays hidden without one.</param>
    public AlundraHudViewModel(Func<Guid, Point?>? iconSize)
    {
        _iconSize = iconSize;
        Coin.IsAnimationPlaying = false;
    }

    /// <summary>The integer pixel scale the screen applies to the whole canvas (D-E13-9); set by the screen when its
    /// window is built, read by <see cref="AlundraHudPresenter"/> to scale the slide.</summary>
    public int PixelScale { get; set; } = 1;

    /// <summary>Tiles the composer produced beyond the elements of their role: none, by construction of the
    /// composer; counted so an overflow stays observable in every build.</summary>
    internal int TileOverflowCount { get; private set; }

    public Visibility RootVisibility
    {
        get => _rootVisibility;
        set
        {
            if (_rootVisibility != value)
            {
                _rootVisibility = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>The slide of the whole jauge, in screen pixels: the screen applies it to its canvas's
    /// <c>RenderTransform</c>, which MGUI's XAML cannot bind (gap G9 of the engine's MGUI gaps report).</summary>
    public Vector2 Translation
    {
        get => _translation;
        set
        {
            if (_translation != value)
            {
                _translation = value;
                NotifyPropertyChanged();
            }
        }
    }

    public HudImageViewModel HpMaxSlash => _hpMax[0];
    public HudImageViewModel HpMaxTens => _hpMax[1];
    public HudImageViewModel HpMaxUnits => _hpMax[2];
    public HudImageViewModel LifeBig0 => _lifeBig[0];
    public HudImageViewModel LifeBig1 => _lifeBig[1];
    public HudImageViewModel LifeBig2 => _lifeBig[2];
    public HudImageViewModel LifeBig3 => _lifeBig[3];
    public HudImageViewModel LifeSmall0 => _lifeSmall[0];
    public HudImageViewModel LifeSmall1 => _lifeSmall[1];
    public HudImageViewModel LifeSmall2 => _lifeSmall[2];
    public HudImageViewModel LifeSmall3 => _lifeSmall[3];
    public HudImageViewModel LifeSmall4 => _lifeSmall[4];
    public HudImageViewModel LifeSmall5 => _lifeSmall[5];
    public HudImageViewModel LifeSmall6 => _lifeSmall[6];
    public HudImageViewModel LifeSmall7 => _lifeSmall[7];
    public HudImageViewModel LifeSmall8 => _lifeSmall[8];
    public HudImageViewModel LifeSmall9 => _lifeSmall[9];
    public HudImageViewModel MagicPip0 => _magicPips[0];
    public HudImageViewModel MagicPip1 => _magicPips[1];
    public HudImageViewModel MagicPip2 => _magicPips[2];
    public HudImageViewModel MagicPip3 => _magicPips[3];
    public HudImageViewModel MoneyDigit0 => _moneyDigits[0];
    public HudImageViewModel MoneyDigit1 => _moneyDigits[1];
    public HudImageViewModel MoneyDigit2 => _moneyDigits[2];
    public HudImageViewModel MoneyDigit3 => _moneyDigits[3];
    public HudImageViewModel Coin { get; } = new();
    public HudImageViewModel EquipmentIcon0 => _equipmentIcons[0];
    public HudImageViewModel EquipmentIcon1 => _equipmentIcons[1];

    void IAlundraHudView.SetVisible(bool visible)
    {
        var shown = visible ? Visibility.Visible : Visibility.Collapsed;
        if (shown == Visibility.Visible && RootVisibility != Visibility.Visible)
        {
            // A collapsed canvas does not update its images: their animations paused while it was hidden.
            _animationsNeedRestart = true;
        }

        RootVisibility = shown;
    }

    void IAlundraHudView.SetTranslation(Vector2 translation) => Translation = translation;

    void IAlundraHudView.SetAnimationClock(int frameCounter, bool moneyRolling)
    {
        _frameCounter = frameCounter;
        _moneyRolling = moneyRolling;
    }

    void IAlundraHudView.SetTiles(IReadOnlyList<AlundraHudTile> tiles)
    {
        var hpMax = 0;
        var lifeBig = 0;
        var lifeSmall = 0;
        var pips = 0;
        var pipFullMask = 0;
        var money = 0;
        var coinShown = false;
        var coinFrame = 0;

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            switch (tile.Glyph)
            {
                case HudGlyph.BigHeartFull or HudGlyph.BigHeartEmpty:
                    Place(_lifeBig, ref lifeBig, tile);
                    break;

                case HudGlyph.SmallHeartFull or HudGlyph.SmallHeartEmpty:
                    Place(_lifeSmall, ref lifeSmall, tile);
                    break;

                case HudGlyph.MagicPipFull0 or HudGlyph.MagicPipFull1 or HudGlyph.MagicPipFull2 or HudGlyph.MagicPipFull3:
                case HudGlyph.MagicPipEmpty:
                    if (pips >= _magicPips.Length)
                    {
                        TileOverflowCount++;
                        break;
                    }

                    if (tile.Glyph == HudGlyph.MagicPipEmpty)
                    {
                        _magicPips[pips].SetSource(GlyphSprites[HudGlyph.MagicPipEmpty]);
                    }
                    else
                    {
                        pipFullMask |= 1 << pips;
                    }

                    _magicPips[pips].Show(tile.NativeX, tile.NativeY);
                    pips++;
                    break;

                case HudGlyph.Coin0 or HudGlyph.Coin1 or HudGlyph.Coin2 or HudGlyph.Coin3:
                    Coin.Show(tile.NativeX, tile.NativeY);
                    coinShown = true;
                    coinFrame = tile.Glyph - HudGlyph.Coin0;
                    break;

                default:
                    // Digits and the slash: the composer emits the three HP-max tiles first, the money digits last.
                    if (hpMax < _hpMax.Length)
                    {
                        Place(_hpMax, ref hpMax, tile);
                    }
                    else
                    {
                        Place(_moneyDigits, ref money, tile);
                    }

                    break;
            }
        }

        HideFrom(_hpMax, hpMax);
        HideFrom(_lifeBig, lifeBig);
        HideFrom(_lifeSmall, lifeSmall);
        HideFrom(_magicPips, pips);
        HideFrom(_moneyDigits, money);
        if (!coinShown)
        {
            Coin.Visibility = Visibility.Collapsed;
        }

        RestartPipsIfChanged(pips, pipFullMask);
        UpdateCoin(coinShown, coinFrame);
        _animationsNeedRestart = false;
    }

    void IAlundraHudView.SetEquipmentIcons(IReadOnlyList<AlundraHudIcon> icons)
    {
        for (var i = 0; i < _equipmentIcons.Length; i++)
        {
            var image = _equipmentIcons[i];
            var size = i < icons.Count ? _iconSize?.Invoke(icons[i].AssetId) : null;
            if (size == null)
            {
                image.Visibility = Visibility.Collapsed;
                continue;
            }

            // Centred in SCREEN pixels, as the jauge always was (AlundraHudIcon.ScreenLeft's own doc: the one free
            // native pixel of a 23-wide icon in its 24-wide box is split evenly at an even scale). The canvas is
            // scaled once as a whole, so the whole native part goes to the position and the remainder, finer than a
            // native pixel, to the sub-pixel offset.
            var scale = Math.Max(1, PixelScale);
            var screenLeft = icons[i].ScreenLeft(size.Value.X, scale);
            var screenTop = icons[i].ScreenTop(size.Value.Y, scale);
            var nativeLeft = screenLeft / scale;
            var nativeTop = screenTop / scale;
            image.SetSource(icons[i].AssetId);
            image.Show(nativeLeft, nativeTop);
            image.SubPixelOffset = new Vector2((screenLeft - nativeLeft * scale) / (float)scale, (screenTop - nativeTop * scale) / (float)scale);
        }
    }

    private void Place(HudImageViewModel[] role, ref int count, AlundraHudTile tile)
    {
        if (count >= role.Length)
        {
            TileOverflowCount++;
            return;
        }

        role[count].SetSource(GlyphSprites[tile.Glyph]);
        role[count].Show(tile.NativeX, tile.NativeY);
        count++;
    }

    private static void HideFrom(HudImageViewModel[] role, int count)
    {
        for (var i = count; i < role.Length; i++)
        {
            role[i].Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Restarts every full pip together, each at its own phase of the original's pip clock, when the pips
    /// changed since the last tick or when the jauge reappeared.</summary>
    private void RestartPipsIfChanged(int pipCount, int pipFullMask)
    {
        if (pipCount == _pipCount && pipFullMask == _pipFullMask && !_animationsNeedRestart)
        {
            return;
        }

        _pipCount = pipCount;
        _pipFullMask = pipFullMask;

        var phase = _frameCounter / PipTicksPerFrame;
        var ticksIntoFrame = _frameCounter % PipTicksPerFrame;
        for (var i = 0; i < pipCount; i++)
        {
            if ((pipFullMask & (1 << i)) == 0)
            {
                continue;
            }

            var frame = (phase + i) % 4;
            var pip = _magicPips[i];
            pip.AnimationStartOffset = TimeSpan.FromMilliseconds((frame * PipTicksPerFrame + ticksIntoFrame) * TickMilliseconds);
            pip.SourceName = MagicPipAnimationId;
            pip.RestartAt(pip.AnimationStartOffset);
        }
    }

    /// <summary>Plays the coin while the money rolls, from the frame and tick the original is at; holds its first
    /// frame once the roll settles (the original garages it at frame 0).</summary>
    private void UpdateCoin(bool coinShown, int coinFrame)
    {
        if (coinShown && _moneyRolling && (!_coinPlaying || _animationsNeedRestart))
        {
            var ticksIntoFrame = _frameCounter % CoinTicksPerFrame;
            Coin.RestartAt(TimeSpan.FromMilliseconds((coinFrame * CoinTicksPerFrame + ticksIntoFrame) * TickMilliseconds));
            _coinPlaying = true;
        }
        else if (!_moneyRolling && _coinPlaying)
        {
            Coin.AnimationStartOffset = TimeSpan.Zero;
            Coin.IsAnimationPlaying = false;
            _coinPlaying = false;
        }
    }
}
