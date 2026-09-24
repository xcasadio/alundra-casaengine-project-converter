#nullable enable
using System;
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
/// The main inventory screen (E13.d D5, parent ADR-0002): a project asset, <c>UI/Screens/InventoryScreen.uiscreen</c>
/// and its XAML, versioned in <c>alundra-project</c>, loaded through the asset manager and bound to an
/// <see cref="AlundraInventoryViewModel"/> that <see cref="AlundraInventoryPresenter"/> writes every logic tick.
/// The XAML declares every element, the sources that never change (the six boxes, the selection frames, the
/// cursor animation) and binds everything else; this class only sizes the window, scales the canvas, and reads the
/// icon sprites' sizes the view model centres them with.
///
/// <b>Layer/modal (D-E13D-7)</b>: <see cref="UILayer.Menu"/>, <see cref="IsModal"/> true - the layer the
/// engine documents for "menu pause, inventaire, carte", and modal so a screen below (the HUD) is blocked
/// while it is up, same as the original the whole screen freezes gameplay while the inventory is drawn.
///
/// <b>Pixel scale (D-E13D-6)</b>: one INTEGER factor, <c>Math.Max(1, ValidScreenBounds.Width / 320)</c> - the same
/// formula <see cref="AlundraHudScreen"/> uses - applied ONCE as <c>RootCanvas</c>'s own <c>RenderTransform.Scale</c>
/// (MGUI <c>UIRenderTransform</c>, engine ADR-0006): every child stays in NATIVE pixel coordinates and sizes, and
/// the single render-only matrix grows the whole tree together.
///
/// <b>Point sampling</b>: every <see cref="MGImage"/> sets <c>UseLinearFilteringWhenDownscaling = false</c>, same as
/// the HUD's images. MGUI's XAML cannot declare it (gap G8 of the engine's MGUI gaps report), so it stays here.
/// </summary>
public sealed class AlundraInventoryScreen : XamlUIScreenBase, IDisposable
{
    /// <summary>The id of <c>UI/Screens/InventoryScreen.uiscreen</c>, fixed in its envelope.</summary>
    internal const string ScreenAssetId = "d79fc172-1faf-41f2-894d-81d6903484fd";

    // AlundraDisplay.cs:32 - re-declared, not referenced (this DLL never depends on the converter
    // project), same citation AlundraHudScreen.NativeWidth already uses.
    private const int NativeWidth = 320;

    // The font3 BMFont asset the converter already writes (alundra-project/UI/font3.fnt, type fnt) - this
    // DLL never depends on the converter project, only the id's VALUE is re-declared here, the same
    // "re-declared, not referenced" shape as NativeWidth above.
    internal static readonly Guid Font3FontAssetId = Guid.Parse("d18a3985-004d-59cc-a080-ce51fa57da99");

    private readonly AssetContentManager _assetContentManager;
    private MGCanvas? _rootCanvas;
    private IDisposable? _font3;

    // Engine ADR-0037: the SpriteData of every icon whose size was read, held while this screen lives and given
    // back in Dispose. A null entry is an icon whose sprite could not be read (warned once).
    private readonly System.Collections.Generic.Dictionary<Guid, AssetHandle<SpriteData>?> _iconSprites = new();

    /// <summary>
    /// D5.f (engine ADR-0036, plan D-E13D-18): the screen holds font3 through the game's UI font registry
    /// from its construction to its <see cref="Dispose"/>. The registry gives it by reference to every UI
    /// text engine - the engine rebuilds them with every world - so the XAML's <c>FontFamily="font3"</c>
    /// resolves on whichever desktop this screen's window is built on, and the font is never reloaded
    /// across a map change: the next world's screen takes it again while it is still pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">The export has no loadable font3, or no inventory screen asset
    /// (no silent fallback).</exception>
    public AlundraInventoryScreen(AssetContentManager assetContentManager, UIFontRegistry fonts)
        : base(assetContentManager, ScreenAssetId)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _assetContentManager = assetContentManager;
        ViewModel = new AlundraInventoryViewModel(ReadIconSize);

        try
        {
            _font3 = fonts.Acquire(Font3FontAssetId);
        }
        catch (Exception ex)
        {
            base.Dispose();
            throw new InvalidOperationException(
                $"AlundraInventoryScreen: font3 ('UI\\font3.fnt', asset {Font3FontAssetId}) cannot be held; "
                + "the export must provide it.", ex);
        }
    }

    /// <summary>What the XAML binds, and what <see cref="AlundraInventoryPresenter"/> writes.</summary>
    public AlundraInventoryViewModel ViewModel { get; }

    /// <summary>True once <see cref="Dispose"/> gave font3 back.</summary>
    internal bool IsDisposed => _font3 == null;

    /// <summary>Gives font3 back, every icon sprite data this screen holds (engine ADR-0037) and the screen asset
    /// itself. Called by the world proxy that built this screen, when its world ends
    /// (<c>AlundraWorldProxy.OnEndPlay</c>). Idempotent.</summary>
    public override void Dispose()
    {
        _font3?.Dispose();
        _font3 = null;

        foreach (var hold in _iconSprites.Values)
        {
            hold?.Dispose();
        }

        _iconSprites.Clear();
        base.Dispose();
    }

    public override UILayer Layer => UILayer.Menu;
    public override bool IsModal => true;

    /// <summary>Test-only seam, same shape as <see cref="AlundraHudScreen.WindowForTests"/>.</summary>
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

    /// <summary>The native size of an icon sprite, read once from its sprite data and held (engine ADR-0037), or
    /// null when the sprite cannot be read: its slot then stays hidden, as it always did.</summary>
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
                Logs.WriteWarning($"AlundraInventoryScreen: icon sprite {assetId} failed to load ({ex.Message}); its slot stays empty.");
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
}
