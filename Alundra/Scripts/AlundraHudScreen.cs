#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// E13 C2/C3 (docs/plan-e13-hud.md): the non-modal screen that draws the permanent jauge. Since the bound screens
/// program (parent ADR-0002) it is a project asset, <c>UI/Screens/HudScreen.uiscreen</c> and its XAML, versioned in
/// <c>alundra-project</c>, loaded through the asset manager and bound to an <see cref="AlundraHudViewModel"/> that
/// <see cref="AlundraHudPresenter"/> writes every LOGIC tick (D-E13-8: never from this screen's rendered-frame
/// <see cref="Update"/>, which <see cref="CasaEngine.Framework.UI.ScreenStack"/> freezes below a modal screen). The
/// XAML declares every element - the two equipment background quads, the two equipment icons and the 26 tiles by
/// role (<see cref="MaxTileCount"/>) - and binds what changes; the magic pips and the coin play the converter's UI
/// animations.
///
/// <b>Pixel scale (D-E13-9)</b>: an INTEGER factor, <c>Math.Max(1, ValidScreenBounds.Width / 320)</c>
/// (<c>AlundraDisplay.NativeWidth</c>, re-declared, never referenced), applied ONCE as the canvas's own
/// <c>RenderTransform.Scale</c> - every element is authored in native pixels, as the inventory's. The presenter's
/// slide, in screen pixels, goes to the same transform's <c>Translation</c>, which MGUI applies after the scale
/// (<c>UIRenderTransform</c>); MGUI's XAML cannot bind it (gap G9), so this class applies it from the view model.
///
/// <b>Sharpness</b>: every <see cref="MGImage"/> sets <see cref="MGImage.UseLinearFilteringWhenDownscaling"/> = false,
/// which MGUI's XAML cannot declare (gap G8).
/// </summary>
public sealed class AlundraHudScreen : XamlUIScreenBase, IDisposable
{
    /// <summary>The id of <c>UI/Screens/HudScreen.uiscreen</c>, fixed in its envelope.</summary>
    internal const string ScreenAssetId = "37d8200e-55f0-4906-a484-97a04be0d2aa";

    // AlundraDisplay.cs:32 - "public const int NativeWidth = 320;" - re-declared, not referenced, this
    // DLL never depends on the converter project.
    private const int NativeWidth = 320;

    // The tiles the composer can emit at once, recomputed (E13 C2 fourth pass) from the original's own five HUD
    // sprite arrays (StaticVariables.cs) and how many of each element DisplayHpMaxWithNumber/DisplayLife/DisplayMp/
    // DisplayMoney ever address: 3 HP-max tiles (g_HpMaxNumberSprites, only indices 2-4 drawn), 4 big life tiles
    // (g_lifeBigIconSprites, DisplayLife's "3-i" never reaches index 4), 10 small life tiles (g_lifeSmallIconSprites,
    // array slots 5 and 11 never addressed), 4 magic pips (g_MpIconSprites) and 5 money tiles (g_HudMoneySprites,
    // 4 digits + coin): 3 + 4 + 10 + 4 + 5 = 26. The XAML declares exactly these, one element per role. Internal so
    // AlundraHudComposerTests sizes its own exhaustive grid test against it.
    internal const int MaxTileCount = 26;

    private readonly AlundraHudDirector _director;
    private readonly AssetContentManager _assetContentManager;
    private MGCanvas? _canvas;
    private MGImage? _equipmentIcon0;
    private MGImage? _equipmentIcon1;
    private bool _disposed;

    // Engine ADR-0037: the SpriteData of every equipment icon whose size was read, held while this screen lives and
    // given back in Dispose. A null entry is an icon whose sprite could not be read (warned once).
    private readonly System.Collections.Generic.Dictionary<Guid, AssetHandle<SpriteData>?> _iconSprites = new();

    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    /// <exception cref="InvalidOperationException">The export has no HUD screen asset (no silent fallback).</exception>
    public AlundraHudScreen(AlundraHudDirector director, AssetContentManager assetContentManager)
        : base(assetContentManager, ScreenAssetId)
    {
        ArgumentNullException.ThrowIfNull(director);
        _director = director;
        _assetContentManager = assetContentManager;
        ViewModel = new AlundraHudViewModel(ReadIconSize);
    }

    /// <summary>What the XAML binds, and the view <see cref="AlundraHudPresenter"/> writes.</summary>
    public AlundraHudViewModel ViewModel { get; }

    /// <summary>Test-only seam: the host window, so a headless layout test can inspect it.</summary>
    internal MGWindow? WindowForTests => Window;

    protected override void OnWindowLoaded(MGWindow window)
    {
        var bounds = window.Desktop.ValidScreenBounds;
        var pixelScale = Math.Max(1, bounds.Width / NativeWidth);

        window.WindowWidth = bounds.Width;
        window.WindowHeight = bounds.Height;
        window.Left = bounds.X;
        window.Top = bounds.Y;
        window.Padding = new MonoGame.Extended.Thickness(0);
        window.BorderThickness = new MonoGame.Extended.Thickness(0);

        _canvas = FindControl<MGCanvas>("RootCanvas");
        _canvas.RenderTransform.Scale = new Vector2(pixelScale, pixelScale);

        foreach (var image in _canvas.TraverseVisualTree().OfType<MGImage>())
        {
            image.UseLinearFilteringWhenDownscaling = false;
        }

        _equipmentIcon0 = FindControl<MGImage>("EquipmentIcon0");
        _equipmentIcon1 = FindControl<MGImage>("EquipmentIcon1");

        ViewModel.PixelScale = pixelScale;

        // A rebuilt window subscribes once.
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.EquipmentIcon0.PropertyChanged -= OnEquipmentIcon0PropertyChanged;
        ViewModel.EquipmentIcon1.PropertyChanged -= OnEquipmentIcon1PropertyChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.EquipmentIcon0.PropertyChanged += OnEquipmentIcon0PropertyChanged;
        ViewModel.EquipmentIcon1.PropertyChanged += OnEquipmentIcon1PropertyChanged;

        _canvas.RenderTransform.Translation = ViewModel.Translation;
        _equipmentIcon0.RenderTransform.Translation = ViewModel.EquipmentIcon0.SubPixelOffset;
        _equipmentIcon1.RenderTransform.Translation = ViewModel.EquipmentIcon1.SubPixelOffset;
        window.WindowDataContext = ViewModel;
    }

    // The jauge's slide and the icons' sub-pixel offsets go to render transforms, which MGUI's XAML cannot bind (G9).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_canvas != null && e.PropertyName == nameof(AlundraHudViewModel.Translation))
        {
            _canvas.RenderTransform.Translation = ViewModel.Translation;
        }
    }

    private void OnEquipmentIcon0PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_equipmentIcon0 != null && e.PropertyName == nameof(HudImageViewModel.SubPixelOffset))
        {
            _equipmentIcon0.RenderTransform.Translation = ViewModel.EquipmentIcon0.SubPixelOffset;
        }
    }

    private void OnEquipmentIcon1PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_equipmentIcon1 != null && e.PropertyName == nameof(HudImageViewModel.SubPixelOffset))
        {
            _equipmentIcon1.RenderTransform.Translation = ViewModel.EquipmentIcon1.SubPixelOffset;
        }
    }

    /// <summary>E13 C3 (D-E13-8, mission item 2): deliberately EMPTY. <see cref="AlundraHudPresenter"/> owns every
    /// refresh, ticked once per LOGIC tick from <see cref="AlundraWorldProxy.Update(float)"/> - never from this
    /// rendered-frame callback, which <see cref="CasaEngine.Framework.UI.ScreenStack"/> freezes for every screen
    /// below a modal one (<c>ScreenStack.cs:5-13</c>).</summary>
    public override void Update(GameTime gameTime)
    {
    }

    /// <summary>The native size of an equipment icon sprite, read once from its sprite data and held (engine
    /// ADR-0037), or null when the sprite cannot be read: its box then stays empty, as it always did.</summary>
    private Point? ReadIconSize(Guid assetId)
    {
        if (!_iconSprites.TryGetValue(assetId, out var hold))
        {
            try
            {
                hold = _assetContentManager.Acquire<SpriteData>(assetId);
            }
            catch (Exception ex)
            {
                Logs.WriteWarning($"AlundraHudScreen: equipment icon sprite {assetId} failed to load ({ex.Message}); its box stays empty.");
                hold = null;
            }

            _iconSprites[assetId] = hold;
        }

        if (hold == null)
        {
            return null;
        }

        var rect = hold.Asset.PositionInTexture;
        return new Point(rect.Width, rect.Height);
    }

    /// <summary>True once <see cref="Dispose"/> gave everything back.</summary>
    internal bool IsDisposed => _disposed;

    /// <summary>Gives back every icon sprite data this screen holds (engine ADR-0037) and the screen asset itself.
    /// Called by the world proxy that built it, when its world ends (<c>AlundraWorldProxy.OnEndPlay</c>).
    /// Idempotent.</summary>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.EquipmentIcon0.PropertyChanged -= OnEquipmentIcon0PropertyChanged;
        ViewModel.EquipmentIcon1.PropertyChanged -= OnEquipmentIcon1PropertyChanged;

        foreach (var hold in _iconSprites.Values)
        {
            hold?.Dispose();
        }

        _iconSprites.Clear();
        base.Dispose();
    }
}
