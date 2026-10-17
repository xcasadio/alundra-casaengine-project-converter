#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Messages;

/// <summary>E19.f2b1c F2B1C-R2: the frame of the text box, <c>(16, Top)</c> in the XAML. E19.f4c2: it folds while only the speaker is drawn (default visible).</summary>
public sealed class TextBoxFrameViewModel : ViewModelBase
{
    private int? _top;
    private Visibility _visibility = Visibility.Visible;

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
}
