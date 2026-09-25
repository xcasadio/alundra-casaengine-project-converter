#nullable enable
using System;
using System.Linq;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// The sub-inventory screen (E13.d SI4, parent ADR-0002): a project asset,
/// <c>UI/Screens/SubInventoryScreen.uiscreen</c> and its XAML, versioned in <c>alundra-project</c>, loaded
/// through the asset manager and bound to an <see cref="AlundraSubInventoryViewModel"/> that
/// <see cref="AlundraSubInventoryPresenter"/> writes every logic tick. Same shape as
/// <see cref="AlundraInventoryScreen"/> - only the sizes/scales/reads the window, XAML declares everything else.
///
/// <b>Layer/modal (D-E13D-26)</b>: <see cref="UILayer.Menu"/>, <see cref="IsModal"/> true, same layer as the
/// main inventory's own screen - the two are never pushed together (plan §1.2, at least one tick with
/// neither director drawn crosses every switch).
///
/// <b>Pixel scale / point sampling</b>: identical to <see cref="AlundraInventoryScreen"/> - one INTEGER
/// factor applied once as <c>RootCanvas</c>'s own <c>RenderTransform.Scale</c> (engine ADR-0006), every
/// <see cref="MGImage"/> point-sampled.
/// </summary>
public sealed class AlundraSubInventoryScreen : XamlUIScreenBase, IDisposable
{
    /// <summary>The id of <c>UI/Screens/SubInventoryScreen.uiscreen</c>, fixed in its envelope.</summary>
    internal const string ScreenAssetId = "3314fd4e-d6c7-4316-b55f-c7349911d31a";

    // AlundraDisplay.cs:32 - re-declared, not referenced, same citation AlundraInventoryScreen.NativeWidth
    // already uses.
    private const int NativeWidth = 320;

    private readonly AssetContentManager _assetContentManager;
    private MGCanvas? _rootCanvas;
    private IDisposable? _font3;

    /// <summary>
    /// D5.f (engine ADR-0036, plan D-E13D-18): same "holds font3 from construction to Dispose, through the
    /// game's UI font registry" contract as <see cref="AlundraInventoryScreen"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export has no loadable font3, or no sub-inventory
    /// screen asset (no silent fallback).</exception>
    public AlundraSubInventoryScreen(AssetContentManager assetContentManager, UIFontRegistry fonts)
        : base(assetContentManager, ScreenAssetId)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _assetContentManager = assetContentManager;
        ViewModel = new AlundraSubInventoryViewModel();

        try
        {
            _font3 = fonts.Acquire(AlundraInventoryScreen.Font3FontAssetId);
        }
        catch (Exception ex)
        {
            base.Dispose();
            throw new InvalidOperationException(
                $"AlundraSubInventoryScreen: font3 ('UI\\font3.fnt', asset {AlundraInventoryScreen.Font3FontAssetId}) cannot be held; "
                + "the export must provide it.", ex);
        }
    }

    /// <summary>What the XAML binds, and what <see cref="AlundraSubInventoryPresenter"/> writes.</summary>
    public AlundraSubInventoryViewModel ViewModel { get; }

    /// <summary>True once <see cref="Dispose"/> gave font3 back.</summary>
    internal bool IsDisposed => _font3 == null;

    /// <summary>Gives font3 back and the screen asset itself. Called by the world proxy that built this screen, when its world ends
    /// (<c>AlundraWorldProxy.OnEndPlay</c>). Idempotent.</summary>
    public override void Dispose()
    {
        _font3?.Dispose();
        _font3 = null;

        base.Dispose();
    }

    public override UILayer Layer => UILayer.Menu;
    public override bool IsModal => true;

    /// <summary>Test-only seam, same shape as <see cref="AlundraInventoryScreen.WindowForTests"/>.</summary>
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

        _rootCanvas = FindControl<MGCanvas>("RootCanvas");
        _rootCanvas.RenderTransform.Scale = new Vector2(pixelScale, pixelScale);

        foreach (var image in _rootCanvas.TraverseVisualTree().OfType<MGImage>())
        {
            image.UseLinearFilteringWhenDownscaling = false;
        }

        window.WindowDataContext = ViewModel;
    }
}
