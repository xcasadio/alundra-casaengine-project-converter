#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Choice;

/// <summary>E19.f3b: the frame of the choice box, <c>(Left, 144)</c> in the XAML; <c>Left</c> is the slide (311 to 176, then 176 to 320).</summary>
public sealed class ChoiceFrameViewModel : ViewModelBase
{
    private int? _left;

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
