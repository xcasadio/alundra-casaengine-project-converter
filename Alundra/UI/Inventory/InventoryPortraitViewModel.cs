#nullable enable
using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Entities;
using Alundra.Dialogues;
using Alundra.UI.Shared;

namespace Alundra.UI.Inventory;

/// <summary>
/// The opening portrait as both inventory screens bind it (docs/plan-portrait-inventaire.md, PI8): the image sits at
/// its rest position (<see cref="AlundraInventoryPortrait.RestX"/>, <see cref="AlundraInventoryPortrait.RestY"/>) in
/// the XAML, and the flight moves and scales it through the bindable render transform (MGUI ADR-0020): the
/// translation is the quad's top-left minus the rest position, the scale its size over 48x56, anchored at the
/// top-left corner. <see cref="Vector2"/> on both sides, so each push is a typed copy. Every setter notifies only on
/// an actual change: a portrait at rest pushes nothing.
/// </summary>
public sealed class InventoryPortraitViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private Vector2 _translation;
    private Vector2 _scale = Vector2.One;
    private float _brightness = 1f;
    private Visibility _visibility = Visibility.Collapsed;

    /// <summary>The portrait sprite's asset id (<c>Data/inventory-portrait.json</c>), null until known.</summary>
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

    public Vector2 Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
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

    /// <summary>
    /// E19.f4c2 (D-E19-100, MGUI ADR-0021): the tint of the portrait, the original's vertex colour over 128: 1 is the texture as it is, above 1 it brightens (up to 2, the original's
    /// 255), below 1 it darkens. Bound to <c>Image.Brightness</c>. f4c2 owns the property and the dialogue's writer (<see cref="ApplyDialogue"/>); the inventories' <see cref="Apply"/> sets it in E19.f4c3.
    /// </summary>
    public float Brightness
    {
        get => _brightness;
        set
        {
            if (_brightness != value)
            {
                _brightness = value;
                NotifyPropertyChanged();
            }
        }
    }

    // The rest position of the dialogue's portrait in TextBoxScreen.xaml (CanvasLeft, CanvasTop): the flight and the tall portraits' gap are in the translation.
    private const int DialogueRestX = 8;
    private const int DialogueRestY = 116;

    /// <summary>
    /// E19.f4c2 (F4C2-R2): writes what the dialogue's portrait machine drew this pass (<paramref name="portrait"/>, <see cref="DialogueDirector.Portrait"/>) for the image of
    /// <paramref name="source"/> (the speaker's portrait as the opening locked it, <see cref="DialogueDirector.PortraitSource"/>). Nothing drawn this pass, or no source, collapses
    /// the element. Else the translation is the quad's top-left minus (8, 116) - the image sits at (8, 116) in the XAML, so a 48 x 72 portrait (rest (8, 100)) has a translation of -16 at rest -
    /// the scale is the drawn size over the image's own size (48 x its height), the brightness is Rgb / 128, and a pass of 0 x 0 stays visible at the scale 0, exactly the original's 0 x 0 quad
    /// (no visibility change in flight, as the inventories).
    /// </summary>
    internal void ApplyDialogue(AlundraInventoryPortrait portrait, DialoguePortraitRef? source)
    {
        ArgumentNullException.ThrowIfNull(portrait);
        if (!portrait.DrawnThisStep || !source.HasValue)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var image = source.Value;
        if (_sourceId != image.SpriteAssetId || _sourceName == null)
        {
            SourceName = image.SpriteAssetId.ToString("D");
        }

        Translation = new Vector2(portrait.X - DialogueRestX, portrait.Y - DialogueRestY);
        Scale = new Vector2(portrait.DrawnWidth / (float)AlundraInventoryPortrait.FullWidth, portrait.DrawnHeight / (float)image.Height);
        Brightness = portrait.Rgb / 128f;
        Visibility = Visibility.Visible;
    }

    /// <summary>Writes the quad <paramref name="portrait"/> drew this tick. A quad with no area (idle, the first
    /// opening call, the last return call) is a scale of zero, exactly the original's 0x0 quad, and never a
    /// visibility change: collapsing and showing the element would run the window's layout twice per flight, which the
    /// render-only transform exists to avoid (plan D3, MGUI ADR-0006). Only a missing source (degraded export)
    /// collapses the element.</summary>
    internal void Apply(AlundraInventoryPortrait portrait, Guid? sourceId)
    {
        if (!sourceId.HasValue)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        if (_sourceId != sourceId.Value || _sourceName == null)
        {
            SourceName = sourceId.Value.ToString("D");
        }

        Translation = new Vector2(portrait.X - AlundraInventoryPortrait.RestX, portrait.Y - AlundraInventoryPortrait.RestY);
        Scale = new Vector2(
            portrait.DrawnWidth / (float)AlundraInventoryPortrait.FullWidth,
            portrait.DrawnHeight / (float)AlundraInventoryPortrait.FullHeight);
        // E19.f4c3 (D-E19-100): the original's vertex colour over 128 (1 at rest, 0 once idle); the early return above, with no source, leaves it alone.
        Brightness = portrait.Rgb / 128f;
        Visibility = Visibility.Visible;
    }
}
