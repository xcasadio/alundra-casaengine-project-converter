#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace Alundra.Scripts;

/// <summary>
/// E13 C2 (docs/plan-e13-hud.md, tranche "Écran MGUI : la composition statique"): the non-modal screen
/// that draws the permanent jauge from <see cref="AlundraHudDirector"/>'s own DISPLAYED state, read every
/// frame in <see cref="Update"/> - this screen never ticks anything itself (mission item 2: "L'écran ne
/// fait que dessiner"), and reads <see cref="AlundraHudDirector.MagicPipFrame"/>/<see cref="AlundraHudDirector.CoinIconFrame"/>
/// only as already-computed inputs to <see cref="AlundraHudComposer"/>.
///
/// <b>Why a single <see cref="MGCanvas"/> panel, D-E13-5</b>: the plan requires the jauge be ONE element
/// inside its host window, because C3 will translate it via <c>RenderTransform.Translation</c>, which a
/// window itself does not honour (§1.8: "une fenêtre n'honore pas RenderTransform" - its own slide target
/// is <c>internal</c>). <see cref="MGCanvas"/> is MGUI's absolute-positioning panel (<c>Canvas.Left</c>/
/// <c>Canvas.Top</c>), which is exactly what fixed native pixel offsets need - every other MGUI panel in
/// this codebase (stack/dock) repositions children relative to each other, not at fixed coordinates.
///
/// <b>Sprite loading</b>: the SAME path <see cref="Sprite.Create"/> already uses for every other Alundra
/// sprite consumer (<c>Sprite.Create(spriteData, assetContentManager)</c> loads the sheet
/// <c>Texture</c>, and <c>Sprite.SpriteData.PositionInTexture</c> is the source rectangle) - the wind
/// tiles are ALREADY exported <c>.sprite</c> assets (D-E13-2, alundra-project/UI/wind_NNN.sprite), so no
/// converter change and no new asset kind was needed, only the 24 asset ids this screen resolves through
/// <see cref="AssetContentManager.Load{T}(Guid, string, bool)"/> exactly like
/// <c>AlundraWorldProxy.InstallDialogueSystems</c>'s own <c>world.Game.AssetContentManager.Load&lt;Entity&gt;</c>
/// (grep hit, same call shape).
///
/// <b>Pixel scale (§6 point 1, TRANCHÉ HERE)</b>: an INTEGER factor derived from the actual viewport width
/// divided by the native width (320, <c>AlundraDisplay.NativeWidth</c>,
/// alundra-casaengine-project-converter/AlundraDisplay.cs:32 - not referenced by project link, this DLL
/// never depends on the converter, only the constant's VALUE is re-declared here with that citation), read
/// from <c>root.Desktop.ValidScreenBounds.Width</c> - the SAME real-window-bounds accessor
/// <see cref="CasaEngine.Framework.Dialogue.UI.DialogueScreen.BuildWindow"/> and
/// <c>CasaEngine.RPGDemo.Scripts.Screens.MainHUDScreen.OnInitialize</c> both already use, never
/// <see cref="UIRoot.Metrics"/>/<see cref="UIScaler"/> (mission item 4's own instruction to check first:
/// <c>UIScaler</c> computes a scale that is applied to nothing anywhere in this engine, and
/// <see cref="UIRoot.Metrics"/> defaults to a 1920x1080 reference nobody updates for this project's own
/// view - dividing by it would silently derive the wrong factor whenever it stays at that default).
/// Integer division floors, so a non-exact-multiple viewport still yields a whole factor, never a
/// fractional magnification (mission item 4: "n'agrandis jamais d'un facteur non entier").
///
/// <b>Sharpness (mission item 5)</b>: every <see cref="MGImage"/> below sets
/// <see cref="MGImage.UseLinearFilteringWhenDownscaling"/> = false and is sized to an exact integer
/// multiple of its native rectangle, so <see cref="MGUI.Shared.Rendering.DrawSettings"/>'s own default
/// sampler (<see cref="MGUI.Shared.Rendering.SamplerType.PointClamp"/>, DrawSettings.cs:82) is the one
/// actually used - this screen never constructs a <c>DrawSettings</c> of its own, so there is nothing here
/// that could override that default to linear.
/// </summary>
public sealed class AlundraHudScreen : UIScreenBase
{
    // AlundraDisplay.cs:32 - "public const int NativeWidth = 320;" - re-declared, not referenced, this
    // DLL never depends on the converter project (mission's own "ne jamais toucher... converter").
    private const int NativeWidth = 320;

    // Fixed slot count this screen ever needs simultaneously, recomputed (E13 C2 fourth pass) from the
    // original's own five HUD sprite arrays (StaticVariables.cs) and how many of each element
    // DisplayHpMaxWithNumber/DisplayLife/DisplayMp/DisplayMoney ever actually addresses - not a value
    // invented independently of those declarations:
    //   - g_HpMaxNumberSprites (StaticVariables.cs:13148, 5 elements) - only 3 are ever filled/drawn
    //     (ComposeHpMaxNumber's own doc: DisplayHpMaxWithNumber's loop starts at i=2, HudManager.cs:918/938).
    //   - g_lifeBigIconSprites (StaticVariables.cs:13149, 5 elements) - at most 4 are ever drawn as a
    //     crystal (ComposeLife's own doc on the E13 C2 fourth-pass finding: DisplayLife addresses slot
    //     3-i for i in 0..nbBigIcons-1, HudManager.cs:751/771; nbBigIcons==5 gives slot -1, one SPRT
    //     BEFORE the array, not a real slot-4 draw - the array's own index 4 is positioned by
    //     FUN_8004b770/Fun_8004bea4 but never addressed by DisplayLife's own "3-i" formula for any i).
    //   - g_lifeSmallIconSprites (StaticVariables.cs:13150, 12 elements) - only 10 are ever addressed
    //     (HudManager.cs:791-793/812-814: index = y*6+x for y in 0..1, x in 0..4, skipping array slots 5
    //     and 11).
    //   - g_MpIconSprites (StaticVariables.cs:13151, 4 elements) - all 4 addressable (HudManager.cs:632/676,
    //     i in 0..maxMp-1 with maxMp<=4).
    //   - g_HudMoneySprites (StaticVariables.cs:13152, 5 elements) - all 5 drawn (4 digits + coin,
    //     HudManager.cs:867-906).
    // 3 + 4 + 10 + 4 + 5 = 26. Internal (not private) so AlundraHudComposerTests can size its own
    // exhaustive hp x hpMax x preview grid test against this exact constant instead of a copy of it.
    internal const int MaxTileCount = 26;

    private readonly AlundraHudDirector _director;
    private readonly AssetContentManager _assetContentManager;
    private readonly Dictionary<HudGlyph, Sprite> _sprites = new();
    private readonly MGImage[] _pool = new MGImage[MaxTileCount];

    private MGWindow? _window;
    private int _pixelScale = 1;

    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    public AlundraHudScreen(AlundraHudDirector director, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(assetContentManager);
        _director = director;
        _assetContentManager = assetContentManager;
    }

    /// <summary>Test-only seam: the host window, so a headless layout test can inspect its bounds/brush
    /// the same way <c>AlundraDialoguePresenter.ScreenForTests</c> exposes its own screen.</summary>
    internal MGWindow? WindowForTests => _window;

    protected override void OnInitialize(UIRoot root)
    {
        LoadSprites();

        var bounds = root.Desktop.ValidScreenBounds;
        _pixelScale = Math.Max(1, bounds.Width / NativeWidth);

        _window = new MGWindow(root.Desktop, bounds.X, bounds.Y, bounds.Width, bounds.Height)
        {
            TitleText = string.Empty,
            IsTitleBarVisible = false,
            IsUserResizable = false,
        };
        _window.Padding = new Thickness(0);
        _window.BorderThickness = new Thickness(0);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Transparent);

        var canvas = new MGCanvas(_window);

        // Placeholder resource so the pool's MGImage constructor has a real (struct) MGTextureData to
        // start from - ApplyTile overwrites Source with the real sprite the first time each slot is
        // actually used, exactly like every other field this screen only fills in once a tile is composed.
        var placeholder = _sprites[HudGlyph.Digit0];

        for (var i = 0; i < MaxTileCount; i++)
        {
            var image = new MGImage(
                _window,
                new CasaMonoGameImageResource(placeholder.Texture.Resource),
                placeholder.SpriteData.PositionInTexture,
                TextureColor: null,
                Stretch: Stretch.Fill)
            {
                Visibility = Visibility.Collapsed,
                UseLinearFilteringWhenDownscaling = false,
            };

            _pool[i] = image;
            canvas.TryAddChild(image, left: 0, top: 0);
        }

        _window.SetContent(canvas);
        RefreshFromDirector();
    }

    public override void Update(GameTime gameTime)
    {
        RefreshFromDirector();
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null)
        {
            yield return _window;
        }
    }

    /// <summary>Count of <see cref="RefreshFromDirector"/> calls where <see cref="AlundraHudComposer.Compose"/>
    /// returned more tiles than <see cref="MaxTileCount"/> - E13 C2 fourth pass's "plus aucune tuile jetée
    /// en silence": a genuine overflow must be observable in every build configuration, not just under
    /// <see cref="Debug.Assert"/> (which no-ops in Release), so this counter is incremented unconditionally
    /// alongside the assert below. Internal so a test can observe it stays zero across a grid of inputs.</summary>
    internal int TileOverflowCount { get; private set; }

    private void RefreshFromDirector()
    {
        var tiles = AlundraHudComposer.Compose(
            _director.IsDrawn,
            _director.Hp, _director.HpMax, _director.TrueHpMax, _director.HpDisplayPreviewIncrement,
            _director.Mp, _director.MpMax, _director.MpDisplayPreviewIncrement,
            _director.Money, _director.CoinIconFrame,
            _director.MagicPipFrame);

        Debug.Assert(
            tiles.Count <= MaxTileCount,
            $"AlundraHudComposer.Compose returned {tiles.Count} tiles, over the {MaxTileCount}-slot pool " +
            "(see MaxTileCount's own derivation) - this is a genuine overflow, not a silent drop.");

        if (tiles.Count > MaxTileCount)
        {
            TileOverflowCount++;
        }

        var slot = 0;
        for (; slot < tiles.Count && slot < MaxTileCount; slot++)
        {
            ApplyTile(_pool[slot], tiles[slot]);
        }

        for (; slot < MaxTileCount; slot++)
        {
            _pool[slot].Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyTile(MGImage image, AlundraHudTile tile)
    {
        var sprite = _sprites[tile.Glyph];
        var sourceRect = sprite.SpriteData.PositionInTexture;

        image.Source = new MGTextureData(new CasaMonoGameImageResource(sprite.Texture.Resource), sourceRect);
        image.PreferredWidth = sourceRect.Width * _pixelScale;
        image.PreferredHeight = sourceRect.Height * _pixelScale;
        image.Visibility = Visibility.Visible;

        MGCanvas.SetLeft(image, tile.NativeX * _pixelScale);
        MGCanvas.SetTop(image, tile.NativeY * _pixelScale);
    }

    /// <summary>
    /// Loads all 24 ACTIVE tiles once, through <see cref="AssetContentManager.Load{T}(Guid, string, bool)"/>
    /// exactly like every other Alundra sprite consumer (<see cref="Sprite.Create"/>). Every guid below is
    /// the <c>asset_id</c> of the named <c>alundra-project/UI/wind_NNN.sprite</c> (re-read and cross
    /// checked against data-extracted/ui/wind.json's own array index, which the mission's own relevé C
    /// enumerates - the citations name the wind_NNN this screen actually resolves, not the relevé's own
    /// U0/V0 table, which is only how that index was FOUND).
    /// </summary>
    private void LoadSprites()
    {
        if (_sprites.Count > 0)
        {
            return;
        }

        AddSprite(HudGlyph.Digit0, "bc300193-4244-5138-a27f-f242a150bed7"); // wind_000
        AddSprite(HudGlyph.Digit1, "01dbdef2-854c-5c76-a482-e19bc579fa80"); // wind_002
        AddSprite(HudGlyph.Digit2, "91a9c466-d267-5063-a60d-8f4b60cf2a41"); // wind_009
        AddSprite(HudGlyph.Digit3, "fc448c9c-7759-58dc-a85d-68c02bc09380"); // wind_016
        AddSprite(HudGlyph.Digit4, "0740074c-09c3-5bfd-b982-45b75869ce6e"); // wind_023
        AddSprite(HudGlyph.Digit5, "fbeb05c1-a686-5e2d-a08a-13a6f8287b96"); // wind_030
        AddSprite(HudGlyph.Digit6, "fb437995-e6fd-51f0-ad3e-4d6dcc51dbcc"); // wind_037
        AddSprite(HudGlyph.Digit7, "c5af13a8-6890-56bb-b2a4-eb3912509c93"); // wind_045
        AddSprite(HudGlyph.Digit8, "d12a690e-48ba-5a1c-84e1-d1f56d8bb3ec"); // wind_055
        AddSprite(HudGlyph.Digit9, "cd607fcd-8657-5856-9136-76e1194389c9"); // wind_065
        AddSprite(HudGlyph.Slash, "55ad7755-c9a1-5be8-ba45-8dacaf4ca628"); // wind_075
        AddSprite(HudGlyph.BigHeartFull, "8a38c0d4-f55a-55b7-96d5-32c21172f1e0"); // wind_094
        AddSprite(HudGlyph.BigHeartEmpty, "0cca1fb2-b8d5-5169-9719-567e5562e03f"); // wind_125
        AddSprite(HudGlyph.SmallHeartFull, "1784c287-534c-5f7d-a2fc-5d81a22e7557"); // wind_113
        AddSprite(HudGlyph.SmallHeartEmpty, "935a267e-1cda-5c56-a438-817fec8c0b45"); // wind_121
        AddSprite(HudGlyph.MagicPipFull0, "eb70224b-d6d5-558e-a1ef-438f072135f8"); // wind_001
        AddSprite(HudGlyph.MagicPipFull1, "f82389e9-f867-54e2-98c1-e3cdef9ef110"); // wind_003
        AddSprite(HudGlyph.MagicPipFull2, "82b86c2a-c312-53ac-843c-2395db413512"); // wind_010
        AddSprite(HudGlyph.MagicPipFull3, "6975db38-c10f-5210-92e8-ccdc5b426d40"); // wind_017
        AddSprite(HudGlyph.MagicPipEmpty, "ace49f58-4b1c-54c1-8a08-52ecec193305"); // wind_104
        AddSprite(HudGlyph.Coin0, "e7a9df0e-b996-57fb-b12b-b4f99a0e8319"); // wind_126
        AddSprite(HudGlyph.Coin1, "ee099380-b4d7-55f5-8976-66368b94b07d"); // wind_130
        AddSprite(HudGlyph.Coin2, "e389abb9-1c65-59dd-8489-5605850cd425"); // wind_134
        AddSprite(HudGlyph.Coin3, "0b54121d-727f-5786-8d27-48b598a24192"); // wind_139
    }

    private void AddSprite(HudGlyph glyph, string assetId)
    {
        var spriteData = _assetContentManager.Load<SpriteData>(Guid.Parse(assetId));
        _sprites[glyph] = Sprite.Create(spriteData, _assetContentManager);
    }
}
