#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;
using Alundra.UI.Choice;
using Alundra.UI.Inventory;

namespace Alundra.Scripts;

/// <summary>
/// E19.f3b (docs/plan-e19-opcodes.md): what <c>UI/Screens/ChoiceScreen.xaml</c> binds - the frame's x, the two labels (text and x) and the cursor (sprite and x); the rows
/// (144, 152, 136) never move and are declared in the XAML. <see cref="AlundraChoicePresenter"/> writes it after each pass of the choice box from the box's drawn state
/// (<see cref="Apply"/>); only the values that change notify. <c>Top</c> of the two labels is not bound.
/// </summary>
public sealed class AlundraChoiceViewModel : ViewModelBase
{
    private Visibility _rootVisibility = Visibility.Collapsed;

    /// <summary>How many times <see cref="Apply"/> ran.</summary>
    internal int AppliedCount { get; private set; }

    public Visibility RootVisibility
    {
        get => _rootVisibility;
        set
        {
            if (_rootVisibility != value)
            {
                _rootVisibility = value;
                NotifyPropertyChanged();
            }
        }
    }

    public ChoiceFrameViewModel Frame { get; } = new();

    public InventoryTextViewModel Label0 { get; } = new();

    public InventoryTextViewModel Label1 { get; } = new();

    public ChoiceCursorViewModel Cursor { get; } = new();

    /// <summary>
    /// Writes the drawn state of <paramref name="choice"/> after its last pass: nothing shows while the box drew nothing (the init pass, the close pass that writes the result, no
    /// box); else the frame at the pass's x, each label's text (cut at 6 characters by the box) at its x, and the cursor sprite (image 0 to 3) at its x.
    /// </summary>
    internal void Apply(ChoiceBox choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        AppliedCount++;

        if (choice.Drawn is not { } drawn)
        {
            RootVisibility = Visibility.Collapsed;
            return;
        }

        RootVisibility = Visibility.Visible;
        Frame.Left = drawn.FrameX;
        Label0.Text = choice.Labels[0];
        Label0.Left = drawn.Label0X;
        Label1.Text = choice.Labels[1];
        Label1.Left = drawn.Label1X;
        Cursor.SourceName = AlundraTextBoxViewModel.CursorSprites[drawn.CursorImage]; // image 0 to 3 of the box (u 176 + 16 i of the wind sheet): the text box's wait cursor sprites
        Cursor.Left = drawn.CursorX;
    }
}
