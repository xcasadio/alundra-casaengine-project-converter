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
/// E19.f3b (docs/plan-e19-opcodes.md): the screen of the choice box - a project asset, <c>UI/Screens/ChoiceScreen.uiscreen</c> and its XAML, versioned in <c>alundra-project</c>
/// (parent ADR-0002) - bound to an <see cref="AlundraChoiceViewModel"/> that <see cref="AlundraChoicePresenter"/> writes after each pass of the choice box. Same shape as
/// <see cref="AlundraTextBoxScreen"/>: the code only sizes and scales the window, the XAML declares everything else.
///
/// <para><b>Layer/modal</b>: <see cref="UILayer.Modal"/>, modal, like the text box screen it is drawn above (D-E19-80): the windows are drawn in the order they were pushed.</para>
///
/// <para><b>Pixel scale / point sampling</b>: one INTEGER factor applied once as <c>RootCanvas</c>'s own <c>RenderTransform.Scale</c> (engine ADR-0006), every
/// <see cref="MGImage"/> point-sampled; the window follows the bounds of the desktop (engine ADR-0048).</para>
/// </summary>
public sealed class AlundraChoiceScreen : XamlUIScreenBase, IDisposable
{
    /// <summary>The id of <c>UI/Screens/ChoiceScreen.uiscreen</c>, fixed in its envelope.</summary>
    internal const string ScreenAssetId = "a623e495-f197-475c-887d-042140ec7248";

    // AlundraDisplay.cs:32 - re-declared, the same citation AlundraTextBoxScreen uses.
    private const int NativeWidth = 320;

    private MGCanvas? _rootCanvas;
    private IDisposable? _font3;

    /// <summary>Holds font3 from construction to <see cref="Dispose"/>, through the game's UI font registry.</summary>
    /// <exception cref="InvalidOperationException">The export has no loadable font3, or no choice screen asset (no silent fallback).</exception>
    public AlundraChoiceScreen(AssetContentManager assetContentManager, UIFontRegistry fonts)
        : base(assetContentManager, ScreenAssetId)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ViewModel = new AlundraChoiceViewModel();

        try
        {
            _font3 = fonts.Acquire(AlundraInventoryScreen.Font3FontAssetId);
        }
        catch (Exception ex)
        {
            base.Dispose();
            throw new InvalidOperationException(
                $"AlundraChoiceScreen: font3 ('UI\\font3.fnt', asset {AlundraInventoryScreen.Font3FontAssetId}) cannot be held; "
                + "the export must provide it.", ex);
        }
    }

    /// <summary>What the XAML binds, and what <see cref="AlundraChoicePresenter"/> writes.</summary>
    public AlundraChoiceViewModel ViewModel { get; }

    /// <summary>Gives font3 back and the screen asset itself, when the world that built this screen ends. Idempotent.</summary>
    public override void Dispose()
    {
        _font3?.Dispose();
        _font3 = null;

        base.Dispose();
    }

    public override UILayer Layer => UILayer.Modal;
    public override bool IsModal => true;

    protected override void OnWindowLoaded(MGWindow window)
    {
        _rootCanvas = FindControl<MGCanvas>("RootCanvas");
        ApplyScreenBounds(window, window.Desktop.ValidScreenBounds);

        foreach (var image in _rootCanvas.TraverseVisualTree().OfType<MGImage>())
        {
            image.UseLinearFilteringWhenDownscaling = false;
        }

        window.WindowDataContext = ViewModel;
    }

    /// <summary>The image is fitted into the window by the engine and the view changes size with it (engine ADR-0048): the window and the integer scale are
    /// redone from the new bounds.</summary>
    protected override void OnScreenBoundsChanged(Rectangle bounds)
    {
        if (Window != null && _rootCanvas != null)
        {
            ApplyScreenBounds(Window, bounds);
        }
    }

    /// <summary>The window takes the bounds of the desktop (the view's rectangle: 320 k wide, so the scale is k) and the canvas the integer scale of its native
    /// 320 pixels.</summary>
    private void ApplyScreenBounds(MGWindow window, Rectangle bounds)
    {
        var pixelScale = Math.Max(1, bounds.Width / NativeWidth);

        window.WindowWidth = bounds.Width;
        window.WindowHeight = bounds.Height;
        window.Left = bounds.X;
        window.Top = bounds.Y;
        _rootCanvas!.RenderTransform.Scale = new Vector2(pixelScale, pixelScale);
    }
}
