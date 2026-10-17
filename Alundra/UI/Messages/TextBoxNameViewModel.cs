#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;

namespace Alundra.UI.Messages;

/// <summary>
/// E19.f4c2 F4C2-R2: the speaker's name box as the XAML binds it - the baked 112 x 32 frame at <c>(Left, 140)</c> and the name (font3) at <c>(TextLeft, 148)</c>, which slide together with
/// the box's own slide. <see cref="Apply"/> writes what the last pass of <see cref="AlundraDialogueNameBox"/> drew.
/// </summary>
public sealed class TextBoxNameViewModel : ViewModelBase
{
    private int? _left;
    private int? _textLeft;
    private string _text = string.Empty;
    private Visibility _visibility = Visibility.Collapsed;

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

    public int? TextLeft
    {
        get => _textLeft;
        set
        {
            if (_textLeft != value)
            {
                _textLeft = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
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

    /// <summary>Nothing drawn by the last pass (closed, or the release pass) collapses the name; else the frame's x, the text's x and the text locked at the opening, visible.</summary>
    internal void Apply(AlundraDialogueNameBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (box.Drawn is not { } drawn)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Left = drawn.FrameX;
        TextLeft = drawn.TextX;
        Text = box.Text;
        Visibility = Visibility.Visible;
    }
}
