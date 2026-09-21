#nullable enable
using System;
using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.FontStashSharp;
using MGUI.Shared.Helpers;
using MGUI.Shared.Text;
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
public sealed class AlundraInventoryScreen : XamlUIScreenBase, IAlundraInventoryView
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

    // The font3 texture asset the converter already writes (alundra-project/UI/Textures/font3.texture) -
    // this DLL never depends on the converter project, only the id's VALUE is re-declared here, the same
    // "re-declared, not referenced" shape as NativeWidth above.
    private static readonly Guid Font3TextureAssetId = Guid.Parse("ef063d37-c2c7-583a-9d3f-53f86129ac6f");
    private const string Font3FamilyName = "font3";
    private static bool _font3RegistrationAttempted;
    private static bool _font3Registered;

    private readonly System.Collections.Generic.Dictionary<Guid, Sprite?> _iconSprites = new();

    public AlundraInventoryScreen(AssetContentManager assetContentManager)
        : base(EmbeddedXaml())
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);
        _assetContentManager = assetContentManager;
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

        TryRegisterFont3(window);

        if (_font3Registered)
        {
            _weaponNameText.TrySetFont(Font3FamilyName, _weaponNameText.FontSize);
            _itemNameText.TrySetFont(Font3FamilyName, _itemNameText.FontSize);
            _descriptionLine0Text.TrySetFont(Font3FamilyName, _descriptionLine0Text.FontSize);
            _descriptionLine1Text.TrySetFont(Font3FamilyName, _descriptionLine1Text.FontSize);
        }
    }

    /// <summary>E12.a's own engine mechanism (dialogue-choices-and-bitmap-fonts.md §2): a static bitmap
    /// font needs a real <see cref="Microsoft.Xna.Framework.Graphics.Texture2D"/>, which needs a live
    /// <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/> - unavailable headless (confirmed by
    /// the engine's own <c>FontStashSharpBitmapFontRegistrationTests</c>, which documents "two
    /// constructibility walls" for this exact reason), but reachable here: this screen's own
    /// <see cref="_assetContentManager"/> is the SAME live one every sprite on this screen already loads
    /// through, and <c>Texture.Load</c> resolves a real <c>Texture2D</c> from it
    /// (<c>CasaEngine/Framework/Assets/Textures/Texture.cs:62-67</c>) - the exact mechanism
    /// <see cref="Sprite.Create"/> itself relies on. Registered once per process (static guard, like every
    /// other "load once" cache in this DLL, e.g. <see cref="AlundraEtcStringTable"/>'s own table cache):
    /// a second <see cref="AlundraInventoryScreen"/> (a second open) must not re-parse the font.
    /// <para/>
    /// D-E13D-4 (no workaround): if the project has no exported <c>UI/font3.fnt</c>, or the texture/BMFont
    /// parse fails, this logs ONCE and leaves every TextBlock on the theme's default TTF font - reported
    /// as a gap rather than worked around further.</summary>
    private void TryRegisterFont3(MGWindow window)
    {
        if (_font3RegistrationAttempted)
        {
            return;
        }

        _font3RegistrationAttempted = true;

        if (window.Desktop.TextEngine is not FontStashSharpTextEngine fontStashSharpTextEngine)
        {
            Logs.WriteWarning(
                "AlundraInventoryScreen: the desktop's text engine is not a FontStashSharpTextEngine - "
                + "font3 cannot be registered (D-E13D-4 gap); TextBlocks fall back to the default font.");
            return;
        }

        try
        {
            var fntPath = System.IO.Path.Combine(EngineEnvironment.ProjectPath, "UI", "font3.fnt");
            if (!System.IO.File.Exists(fntPath))
            {
                Logs.WriteWarning(
                    $"AlundraInventoryScreen: '{fntPath}' does not exist - font3 cannot be registered "
                    + "(D-E13D-4 gap); TextBlocks fall back to the default font.");
                return;
            }

            var texture = _assetContentManager.Load<Texture>(Font3TextureAssetId);
            texture.Load(_assetContentManager);

            var fntContents = System.IO.File.ReadAllText(fntPath);
            var font = FontStashSharp.StaticSpriteFont.FromBMFont(
                fntContents,
                _ => new FontStashSharp.TextureWithOffset(texture.Resource, Point.Zero));

            fontStashSharpTextEngine.AddStaticFont(Font3FamilyName, CustomFontStyles.Normal, font);
            _font3Registered = true;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                "AlundraInventoryScreen: font3 registration failed (D-E13D-4 gap, reported not worked "
                + $"around); TextBlocks fall back to the default font. {ex.Message}");
        }
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
                var spriteData = _assetContentManager.Load<SpriteData>(assetId);
                cached = spriteData == null ? null : Sprite.Create(spriteData, _assetContentManager);
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
