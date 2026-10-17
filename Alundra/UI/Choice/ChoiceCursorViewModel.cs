#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Choice;

/// <summary>E19.f3b: the selection cursor of the choice box, <c>(Left, 136)</c> in the XAML, one of four sprites by name (the text box's wait cursor sprites).</summary>
public sealed class ChoiceCursorViewModel : ViewModelBase
{
    private string? _sourceName;
    private int? _left;

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
}
