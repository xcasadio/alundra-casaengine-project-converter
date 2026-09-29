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
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5): the save screen - a project asset,
/// <c>UI/Screens/SaveScreen.uiscreen</c> and its XAML, versioned in <c>alundra-project</c> (parent ADR-0002), loaded
/// through the asset manager and bound to an <see cref="AlundraSaveScreenViewModel"/> that
/// <see cref="AlundraSaveScreenPresenter"/> writes every logic tick. Same shape as <see cref="AlundraSubInventoryScreen"/>:
/// the code only sizes and scales the window, the XAML declares everything else (the author's rule: screens are
/// declared in XAML).
///
/// <para><b>Layer/modal</b>: <see cref="UILayer.Menu"/>, modal, like the inventories; the OUI/NON question is the
/// dialogue screen, on <see cref="UILayer.Modal"/>, above it.</para>
///
/// <para><b>Pixel scale / point sampling</b>: identical to <see cref="AlundraInventoryScreen"/> - one INTEGER factor
/// applied once as <c>RootCanvas</c>'s own <c>RenderTransform.Scale</c> (engine ADR-0006), every
/// <see cref="MGImage"/> point-sampled.</para>
/// </summary>
public sealed class AlundraSaveScreen : XamlUIScreenBase, IDisposable
{
    /// <summary>The id of <c>UI/Screens/SaveScreen.uiscreen</c>, fixed in its envelope.</summary>
    internal const string ScreenAssetId = "4d6cb7b6-ed7c-40dc-964c-79f0e8c6f1bc";

    /// <summary>The layer this screen lives on, below the dialogue screen's <see cref="UILayer.Modal"/>.</summary>
    internal const UILayer ScreenLayer = UILayer.Menu;

    // AlundraDisplay.cs:32 - re-declared, the same citation AlundraInventoryScreen.NativeWidth uses.
    private const int NativeWidth = 320;

    private MGCanvas? _rootCanvas;
    private IDisposable? _font3;

    /// <summary>Holds font3 from construction to <see cref="Dispose"/>, through the game's UI font registry, the
    /// contract of <see cref="AlundraInventoryScreen"/>.</summary>
    /// <exception cref="InvalidOperationException">The export has no loadable font3, or no save screen asset (no
    /// silent fallback).</exception>
    public AlundraSaveScreen(AssetContentManager assetContentManager, UIFontRegistry fonts)
        : base(assetContentManager, ScreenAssetId)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ViewModel = new AlundraSaveScreenViewModel();

        try
        {
            _font3 = fonts.Acquire(AlundraInventoryScreen.Font3FontAssetId);
        }
        catch (Exception ex)
        {
            base.Dispose();
            throw new InvalidOperationException(
                $"AlundraSaveScreen: font3 ('UI\\font3.fnt', asset {AlundraInventoryScreen.Font3FontAssetId}) cannot be held; "
                + "the export must provide it.", ex);
        }
    }

    /// <summary>What the XAML binds, and what <see cref="AlundraSaveScreenPresenter"/> writes.</summary>
    public AlundraSaveScreenViewModel ViewModel { get; }

    /// <summary>Gives font3 back and the screen asset itself, when the world that built this screen ends. Idempotent.</summary>
    public override void Dispose()
    {
        _font3?.Dispose();
        _font3 = null;

        base.Dispose();
    }

    public override UILayer Layer => ScreenLayer;
    public override bool IsModal => true;

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
