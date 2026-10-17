#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Messages;

/// <summary>E19.f2b1c F2B1C-R2: the wait cursor of <c>\A</c>, <c>(288, Top)</c> in the XAML, one of four sprites by name.</summary>
public sealed class TextBoxCursorViewModel : ViewModelBase
{
    private string? _sourceName;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;

    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName != value)
            {
                _sourceName = value;
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
}
