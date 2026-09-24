#nullable enable
using System;
using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace Alundra.Scripts;

/// <summary>E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-6): the seam <see cref="AlundraInventoryScreen"/>
/// implements so <see cref="AlundraInventoryPresenter"/> can push a freshly composed
/// <see cref="InventoryDisplayModel"/> into it every LOGIC tick, without the presenter (or the pure
/// <see cref="AlundraInventoryComposer"/>) ever depending on MGUI - same "director/presenter/screen" split
/// as <see cref="AlundraHudDirector"/>/<see cref="AlundraHudPresenter"/>/<see cref="AlundraHudScreen"/>
/// and as the dialogue box. A recording double of this interface is what the presenter's own tests drive,
/// since a real <see cref="MGWindow"/> needs a live graphics stack.</summary>
public interface IAlundraInventoryView
{
    /// <summary>Pushes the freshly composed display for this tick. Called only while the director's own
    /// <c>IsDrawn</c> is true (D-E13D-6's own "nothing drawn on the setup tick") - the presenter owns
    /// push/remove of the screen itself around this.</summary>
    void Render(InventoryDisplayModel model);
}

/// <summary>
/// E13.d D5: the XAML-declared main inventory screen (<c>Alundra/Screens/InventoryScreen.xaml</c>,
/// D-E13D-16/ADR-0001) - structure lives in the XAML, this class finds every named element and pushes
/// values into it, exactly the rule the author set for every screen in this project (engine ADR-0035).
///
/// <b>Layer/modal (D-E13D-7)</b>: <see cref="UILayer.Menu"/>, <see cref="IsModal"/> true - the layer the
/// engine documents for "menu pause, inventaire, carte", and modal so a screen below (the HUD) is blocked
/// while it is up, same as the original the whole screen freezes gameplay while the inventory is drawn.
///
/// <b>Pixel scale (D-E13D-6, brief item 2)</b>: one INTEGER factor, <c>Math.Max(1, ValidScreenBounds.Width / 320)</c> -
/// the same formula <see cref="AlundraHudScreen"/> already uses - applied ONCE as
/// <see cref="RootCanvas"/>'s own <c>RenderTransform.Scale</c> (MGUI <c>UIRenderTransform</c>, engine
/// ADR-0006): every child below stays in NATIVE pixel coordinates/sizes (including every
/// <c>TextBlock</c>'s own font size), and the single render-only matrix grows the whole tree together -
/// unlike <see cref="AlundraHudScreen"/>, which instead multiplies every position/size by the factor
/// itself (an equally valid but more verbose approach the HUD chose before this screen existed).
///
/// <b>Point sampling</b>: every <see cref="MGImage"/> sets <c>UseLinearFilteringWhenDownscaling = false</c>,
/// same as the HUD's own images, so the default point sampler stays the one actually used at any integer
/// scale.
/// </summary>
public sealed class AlundraInventoryScreen : XamlUIScreenBase, IAlundraInventoryView, IDisposable
{
    private const string XamlResourceName = "Alundra.Screens.InventoryScreen.xaml";

    // AlundraDisplay.cs:32 - re-declared, not referenced (this DLL never depends on the converter
    // project), same citation AlundraHudScreen.NativeWidth already uses.
    private const int NativeWidth = 320;

    private readonly AssetContentManager _assetContentManager;

    private MGCanvas? _rootCanvas;
    private int _pixelScale = 1;

    private MGImage _boxWeapon = null!;
    private MGImage _boxItem = null!;
    private MGImage _boxWeaponName = null!;
    private MGImage _boxItemName = null!;
    private MGImage _boxMoneyFalconKey = null!;
    private MGImage _boxDescription = null!;
    private readonly MGImage[] _slotIcons = new MGImage[AlundraInventoryComposer.SlotCount];
    private MGImage _herbCountDigit = null!;
    private readonly MGImage[] _moneyDigits = new MGImage[4];
    private readonly MGImage[] _falconDigits = new MGImage[2];
    private readonly MGImage[] _keyDigits = new MGImage[2];
    private MGTextBlock _weaponNameText = null!;
    private MGTextBlock _itemNameText = null!;
    private MGTextBlock _descriptionLine0Text = null!;
    private MGTextBlock _descriptionLine1Text = null!;
    private MGImage _weaponSelectionFrame = null!;
    private MGImage _itemSelectionFrame = null!;
    private MGImage _cursorImage = null!;

    // The six baked box sprites (D-E13D-13/D3.b), keyed by AlundraInventoryDirector.BoxPosition's own
    // index (4, the spacer, is never a key: nothing to draw there).
    private static readonly (int BoxIndex, string AssetId)[] BoxSprites =
    {
        (0, "c2447262-414f-5b78-9def-b54b080636f4"), // g_UiBoxesInventoryWeaponBackground
        (1, "615fe384-c2e5-5ad3-82ec-f0bd137c877a"), // g_UiBoxesInventoryItemBackground
        (2, "eb357d61-17ab-5ea1-a287-b98196a6e7e3"), // g_UiBoxesInventoryWeaponNameBackground
        (3, "1f5d5bab-7408-5da3-a8d6-20a02d3c0747"), // g_UiBoxesInventoryItemNameBackground
        (5, "e8d58247-f02b-57cb-9ebf-f1df2b9be874"), // g_UiBoxesInventoryMoneyFalconKeyIcons
        (6, "973a9208-c867-57fe-bee3-cf30237221ef"), // g_uiBoxesInventoryDescriptionBackground
    };

    // wind_000/002/009/016/023/030/037/045/055/065 - the SAME ten glyph ids AlundraHudScreen.LoadSprites
    // already resolves (re-cited here rather than shared: this screen never references AlundraHudScreen).
    private static readonly string[] DigitAssetIds =
    {
        "bc300193-4244-5138-a27f-f242a150bed7", "01dbdef2-854c-5c76-a482-e19bc579fa80",
        "91a9c466-d267-5063-a60d-8f4b60cf2a41", "fc448c9c-7759-58dc-a85d-68c02bc09380",
        "0740074c-09c3-5bfd-b982-45b75869ce6e", "fbeb05c1-a686-5e2d-a08a-13a6f8287b96",
        "fb437995-e6fd-51f0-ad3e-4d6dcc51dbcc", "c5af13a8-6890-56bb-b2a4-eb3912509c93",
        "d12a690e-48ba-5a1c-84e1-d1f56d8bb3ec", "cd607fcd-8657-5856-9136-76e1194389c9",
    };

    // Cursor phases 0-3 (plan §1.4/D0.9): (176,160)/(192,160)/(208,160)/(224,160) -> wind_159/182/210/237.
    private static readonly string[] CursorPhaseAssetIds =
    {
        "6ed4380a-ba9c-5d0b-84db-22e1ddf61361", // wind_159
        "c4a43c82-d394-5929-a1fa-61f29cc5dde4", // wind_182
        "76368217-1aa5-5fee-a940-465e40601d66", // wind_210
        "366c35dc-c165-5e6a-a4fc-897fe877391e", // wind_237
    };

    // wind_039 - the selection frame, both boxes.
    private const string SelectionFrameAssetId = "5f56fba6-2abb-5163-bb1e-e5a4f6fdb499";

    // The font3 BMFont asset the converter already writes (alundra-project/UI/font3.fnt, type fnt) - this
    // DLL never depends on the converter project, only the id's VALUE is re-declared here, the same
    // "re-declared, not referenced" shape as NativeWidth above.
    internal static readonly Guid Font3FontAssetId = Guid.Parse("d18a3985-004d-59cc-a080-ce51fa57da99");

    private readonly System.Collections.Generic.Dictionary<Guid, Sprite?> _iconSprites = new();
    private IDisposable? _font3;

    // Engine ADR-0037: the SpriteData of every sprite in _iconSprites, held while this screen lives and
    // given back in Dispose, with the sprites themselves (which hold their sheet texture).
    private readonly System.Collections.Generic.List<IDisposable> _spriteDataHolds = new();

    /// <summary>
    /// D5.f (engine ADR-0036, plan D-E13D-18): the screen holds font3 through the game's UI font registry
    /// from its construction to its <see cref="Dispose"/>. The registry gives it by reference to every UI
    /// text engine - the engine rebuilds them with every world - so the XAML's <c>FontFamily="font3"</c>
    /// resolves on whichever desktop this screen's window is built on, and the font is never reloaded
    /// across a map change: the next world's screen takes it again while it is still pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export has no loadable font3 (no silent fallback).</exception>
    public AlundraInventoryScreen(AssetContentManager assetContentManager, UIFontRegistry fonts)
        : base(EmbeddedXaml())
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);
        ArgumentNullException.ThrowIfNull(fonts);
        _assetContentManager = assetContentManager;

        try
        {
            _font3 = fonts.Acquire(Font3FontAssetId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"AlundraInventoryScreen: font3 ('UI\\font3.fnt', asset {Font3FontAssetId}) cannot be held; "
                + "the export must provide it.", ex);
        }
    }

    /// <summary>True once <see cref="Dispose"/> gave font3 back.</summary>
    internal bool IsDisposed => _font3 == null;

    /// <summary>Gives font3 back, and every sprite and sprite data this screen holds (engine ADR-0037).
    /// Called by the world proxy that built this screen, when its world ends
    /// (<c>AlundraWorldProxy.OnEndPlay</c>). Idempotent. Overrides the screen base's release, so disposing the
    /// screen through a base or <see cref="IDisposable"/> reference gives everything back too.</summary>
    public override void Dispose()
    {
        _font3?.Dispose();
        _font3 = null;

        foreach (var sprite in _iconSprites.Values)
        {
            sprite?.Dispose();
        }

        foreach (var hold in _spriteDataHolds)
        {
            hold.Dispose();
        }

        _iconSprites.Clear();
        _spriteDataHolds.Clear();
        base.Dispose();
    }

    public override UILayer Layer => UILayer.Menu;
    public override bool IsModal => true;

    /// <summary>Test-only seam, same shape as <see cref="AlundraHudScreen.WindowForTests"/>.</summary>
    internal MGWindow? WindowForTests => Window;

    private static XamlDocumentSource EmbeddedXaml()
        => XamlDocumentSource.FromString(
            GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), XamlResourceName),
            "InventoryScreen.xaml");

    protected override void OnWindowLoaded(MGWindow window)
    {
        var bounds = window.Desktop.ValidScreenBounds;
        _pixelScale = Math.Max(1, bounds.Width / NativeWidth);

        window.WindowWidth = bounds.Width;
        window.WindowHeight = bounds.Height;
        window.Left = bounds.X;
        window.Top = bounds.Y;
        window.Padding = new MonoGame.Extended.Thickness(0);
        window.BorderThickness = new MonoGame.Extended.Thickness(0);

        _rootCanvas = FindControl<MGCanvas>("RootCanvas");
        _rootCanvas.RenderTransform.Scale = new Vector2(_pixelScale, _pixelScale);

        _boxWeapon = FindControl<MGImage>("BoxWeapon");
        _boxItem = FindControl<MGImage>("BoxItem");
        _boxWeaponName = FindControl<MGImage>("BoxWeaponName");
        _boxItemName = FindControl<MGImage>("BoxItemName");
        _boxMoneyFalconKey = FindControl<MGImage>("BoxMoneyFalconKey");
        _boxDescription = FindControl<MGImage>("BoxDescription");

        for (var i = 0; i < _slotIcons.Length; i++)
        {
            _slotIcons[i] = FindControl<MGImage>($"IconSlot{i}");
        }

        _herbCountDigit = FindControl<MGImage>("HerbCountDigit");

        for (var i = 0; i < _moneyDigits.Length; i++)
        {
            _moneyDigits[i] = FindControl<MGImage>($"MoneyDigit{i}");
        }

        for (var i = 0; i < _falconDigits.Length; i++)
        {
            _falconDigits[i] = FindControl<MGImage>($"FalconDigit{i}");
        }

        for (var i = 0; i < _keyDigits.Length; i++)
        {
            _keyDigits[i] = FindControl<MGImage>($"KeyDigit{i}");
        }

        _weaponNameText = FindControl<MGTextBlock>("WeaponNameText");
        _itemNameText = FindControl<MGTextBlock>("ItemNameText");
        _descriptionLine0Text = FindControl<MGTextBlock>("DescriptionLine0Text");
        _descriptionLine1Text = FindControl<MGTextBlock>("DescriptionLine1Text");

        _weaponSelectionFrame = FindControl<MGImage>("WeaponSelectionFrame");
        _itemSelectionFrame = FindControl<MGImage>("ItemSelectionFrame");
        _cursorImage = FindControl<MGImage>("CursorImage");

        foreach (var image in AllImages())
        {
            image.UseLinearFilteringWhenDownscaling = false;
            image.Visibility = Visibility.Collapsed;
        }

        ApplySprite(_boxWeapon, "c2447262-414f-5b78-9def-b54b080636f4");
        ApplySprite(_boxItem, "615fe384-c2e5-5ad3-82ec-f0bd137c877a");
        ApplySprite(_boxWeaponName, "eb357d61-17ab-5ea1-a287-b98196a6e7e3");
        ApplySprite(_boxItemName, "1f5d5bab-7408-5da3-a8d6-20a02d3c0747");
        ApplySprite(_boxMoneyFalconKey, "e8d58247-f02b-57cb-9ebf-f1df2b9be874");
        ApplySprite(_boxDescription, "973a9208-c867-57fe-bee3-cf30237221ef");
        ApplySprite(_weaponSelectionFrame, SelectionFrameAssetId);
        ApplySprite(_itemSelectionFrame, SelectionFrameAssetId);
    }

    private System.Collections.Generic.IEnumerable<MGImage> AllImages()
    {
        yield return _boxWeapon;
        yield return _boxItem;
        yield return _boxWeaponName;
        yield return _boxItemName;
        yield return _boxMoneyFalconKey;
        yield return _boxDescription;

        foreach (var icon in _slotIcons)
        {
            yield return icon;
        }

        yield return _herbCountDigit;

        foreach (var digit in _moneyDigits)
        {
            yield return digit;
        }

        foreach (var digit in _falconDigits)
        {
            yield return digit;
        }

        foreach (var digit in _keyDigits)
        {
            yield return digit;
        }

        yield return _weaponSelectionFrame;
        yield return _itemSelectionFrame;
        yield return _cursorImage;
    }

    void IAlundraInventoryView.Render(InventoryDisplayModel model)
    {
        if (_rootCanvas == null)
        {
            return; // not built yet
        }

        if (!model.Visible)
        {
            _rootCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        _rootCanvas.Visibility = Visibility.Visible;

        foreach (var box in model.Boxes)
        {
            var image = box.BoxIndex switch
            {
                0 => _boxWeapon,
                1 => _boxItem,
                2 => _boxWeaponName,
                3 => _boxItemName,
                5 => _boxMoneyFalconKey,
                6 => _boxDescription,
                _ => null,
            };

            if (image == null)
            {
                continue;
            }

            image.Visibility = Visibility.Visible;
            MGCanvas.SetLeft(image, box.NativeX);
            MGCanvas.SetTop(image, box.NativeY);
        }

        for (var i = 0; i < _slotIcons.Length; i++)
        {
            _slotIcons[i].Visibility = Visibility.Collapsed;
        }

        ApplyIcons(model.SlotIcons);

        ApplyDigit(_herbCountDigit, model.HerbDigitVisible, model.HerbDigit);

        for (var i = 0; i < _moneyDigits.Length; i++)
        {
            ApplyDigit(_moneyDigits[i], true, model.MoneyDigits.Count > i ? model.MoneyDigits[i] : default);
        }

        for (var i = 0; i < _falconDigits.Length; i++)
        {
            ApplyDigit(_falconDigits[i], true, model.FalconDigits.Count > i ? model.FalconDigits[i] : default);
        }

        for (var i = 0; i < _keyDigits.Length; i++)
        {
            ApplyDigit(_keyDigits[i], true, model.KeyDigits.Count > i ? model.KeyDigits[i] : default);
        }

        _weaponNameText.Text = model.WeaponName;
        MGCanvas.SetLeft(_weaponNameText, model.WeaponNameX);
        MGCanvas.SetTop(_weaponNameText, model.WeaponNameY);

        _itemNameText.Text = model.ItemName;
        MGCanvas.SetLeft(_itemNameText, model.ItemNameX);
        MGCanvas.SetTop(_itemNameText, model.ItemNameY);

        _descriptionLine0Text.Text = model.DescriptionLine0;
        MGCanvas.SetLeft(_descriptionLine0Text, model.DescriptionLine0X);
        MGCanvas.SetTop(_descriptionLine0Text, model.DescriptionLine0Y);

        _descriptionLine1Text.Text = model.DescriptionLine1;
        MGCanvas.SetLeft(_descriptionLine1Text, model.DescriptionLine1X);
        MGCanvas.SetTop(_descriptionLine1Text, model.DescriptionLine1Y);

        ApplyFrame(_weaponSelectionFrame, model.WeaponSelectionFrame);
        ApplyFrame(_itemSelectionFrame, model.ItemSelectionFrame);

        var cursorAssetId = CursorPhaseAssetIds[Math.Clamp(model.Cursor.Phase, 0, CursorPhaseAssetIds.Length - 1)];
        ApplySprite(_cursorImage, cursorAssetId);
        _cursorImage.Visibility = Visibility.Visible;
        MGCanvas.SetLeft(_cursorImage, model.Cursor.NativeX);
        MGCanvas.SetTop(_cursorImage, model.Cursor.NativeY);
    }

    private void ApplyIcons(System.Collections.Generic.IReadOnlyList<InventorySlotIcon> icons)
    {
        // Slots not present in the composer's own list stay Collapsed (already reset by the caller).
        foreach (var slotIcon in icons)
        {
            if (slotIcon.SlotIndex < 0 || slotIcon.SlotIndex >= _slotIcons.Length)
            {
                continue;
            }

            var image = _slotIcons[slotIcon.SlotIndex];
            var icon = slotIcon.Icon;

            if (!TryGetIconSprite(icon.AssetId, out var sprite))
            {
                continue;
            }

            var sourceRect = sprite.SpriteData.PositionInTexture;
            image.Source = new MGTextureData(new CasaMonoGameImageResource(sprite.Texture.Resource), sourceRect);
            image.PreferredWidth = sourceRect.Width;
            image.PreferredHeight = sourceRect.Height;
            image.Visibility = Visibility.Visible;

            // AlundraHudIcon.ScreenLeft/Top take a pixel scale to fold into the returned SCREEN pixel -
            // this screen instead keeps every child in NATIVE coordinates (the whole canvas is scaled
            // once via RenderTransform.Scale, this class' own doc), so pixelScale = 1 here.
            MGCanvas.SetLeft(image, icon.ScreenLeft(sourceRect.Width, 1));
            MGCanvas.SetTop(image, icon.ScreenTop(sourceRect.Height, 1));
        }
    }

    private void ApplyDigit(MGImage image, bool visible, InventoryDigit digit)
    {
        if (!visible)
        {
            image.Visibility = Visibility.Collapsed;
            return;
        }

        ApplySprite(image, DigitAssetIds[Math.Clamp(digit.Value, 0, 9)]);
        image.Visibility = Visibility.Visible;
        MGCanvas.SetLeft(image, digit.NativeX);
        MGCanvas.SetTop(image, digit.NativeY);
    }

    private void ApplyFrame(MGImage image, InventorySelectionFrame frame)
    {
        if (!frame.Visible)
        {
            image.Visibility = Visibility.Collapsed;
            return;
        }

        image.Visibility = Visibility.Visible;
        MGCanvas.SetLeft(image, frame.NativeX);
        MGCanvas.SetTop(image, frame.NativeY);
    }

    private bool TryGetIconSprite(Guid assetId, out Sprite sprite)
    {
        if (!_iconSprites.TryGetValue(assetId, out var cached))
        {
            var reason = "no such asset";
            try
            {
                // Engine ADR-0037: the sprite data is held for as long as this screen lives.
                var spriteDataHold = _assetContentManager.Acquire<SpriteData>(assetId);
                try
                {
                    cached = Sprite.Create(spriteDataHold.Asset, _assetContentManager);
                    _spriteDataHolds.Add(spriteDataHold);
                }
                catch
                {
                    spriteDataHold.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                cached = null;
            }

            if (cached == null)
            {
                Logs.WriteWarning($"AlundraInventoryScreen: icon sprite {assetId} failed to load ({reason}); its slot stays empty.");
            }

            _iconSprites[assetId] = cached;
        }

        sprite = cached!;
        return cached != null;
    }

    private void ApplySprite(MGImage image, string assetId)
    {
        if (!TryGetIconSprite(Guid.Parse(assetId), out var sprite))
        {
            return;
        }

        var sourceRect = sprite.SpriteData.PositionInTexture;
        image.Source = new MGTextureData(new CasaMonoGameImageResource(sprite.Texture.Resource), sourceRect);
        image.PreferredWidth = sourceRect.Width;
        image.PreferredHeight = sourceRect.Height;
    }
}
