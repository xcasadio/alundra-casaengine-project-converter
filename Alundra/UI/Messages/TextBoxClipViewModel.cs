#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Messages;

/// <summary>E19.f2b1c F2B1C-R2: the clip canvas of the three rows, <c>(32, Top)</c> wide 258 in the XAML. E19.f4c2: it folds with the frame while only the speaker is drawn (default visible).</summary>
public sealed class TextBoxClipViewModel : ViewModelBase
{
    private int? _top;
    private int? _height;
    private Visibility _visibility = Visibility.Visible;

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

    public int? Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                _height = value;
                NotifyPropertyChanged();
            }
        }
    }
}
