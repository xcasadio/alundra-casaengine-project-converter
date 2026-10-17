#nullable enable
using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Entities;
using Alundra.Dialogues;

namespace Alundra.UI.Inventory;

/// <summary>
/// One image of the inventory screen as its XAML binds it (parent ADR-0002): its source (an asset id, resolved
/// by the engine's UI asset provider), its native canvas position, and its visibility. Every setter notifies only
/// on an actual change, so a tick that changes nothing pushes nothing.
/// </summary>
public sealed class InventoryImageViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;

    /// <summary>The image's asset id (engine ADR-0038, "Images are named by asset"), or null when the XAML names
    /// the source itself.</summary>
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
}
