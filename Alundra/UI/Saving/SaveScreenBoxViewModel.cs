#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Alundra.UI.Inventory;

namespace Alundra.UI.Saving;

/// <summary>
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5): one box of the save screen's picker - the shared
/// <see cref="InventoryImageViewModel"/> shape (position, visibility) plus the box tint (J4), which
/// <c>SaveScreen.xaml</c> binds to the image's <see cref="MGImage.TextureColor"/>.
/// </summary>
public sealed class SaveScreenBoxViewModel : ViewModelBase
{
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;
    private Color? _textureColor;

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

    /// <summary>The box's colour modulation: the original's tint, 0x80 the texture's own colour.</summary>
    public Color? TextureColor
    {
        get => _textureColor;
        set
        {
            if (_textureColor != value)
            {
                _textureColor = value;
                NotifyPropertyChanged();
            }
        }
    }
}
