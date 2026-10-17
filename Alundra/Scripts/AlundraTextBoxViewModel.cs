#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Dialogues;
using Alundra.UI.Inventory;
using Alundra.UI.Messages;

namespace Alundra.Scripts;

/// <summary>
/// E19.f2b1c F2B1C-R2 (docs/plan-e19-opcodes.md): what <c>UI/Screens/TextBoxScreen.xaml</c> binds - the frame, the clip canvas, the three rows (text and
/// position relative to the clip canvas) and the cursor. <see cref="TextBoxPresenter"/> writes it after each pass of the dialogue box from the box's drawn
/// state (<see cref="Apply"/>); only the values that change notify.
/// </summary>
public sealed class AlundraTextBoxViewModel : ViewModelBase
{
    private readonly InventoryTextViewModel[] _rows = { new(), new(), new() };
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

    public TextBoxFrameViewModel Frame { get; } = new();

    public TextBoxClipViewModel Clip { get; } = new();

    public InventoryTextViewModel Row0 => _rows[0];

    public InventoryTextViewModel Row1 => _rows[1];

    public InventoryTextViewModel Row2 => _rows[2];

    public TextBoxCursorViewModel Cursor { get; } = new();

    /// <summary>E19.f4c2: the speaker's name box (written by <see cref="TextBoxPresenter"/> from <see cref="DialogueDirector.NameBox"/>).</summary>
    public TextBoxNameViewModel NameBox { get; } = new();

    /// <summary>E19.f4c2: the speaker's portrait, the inventories' view model of it (written by the presenter with <see cref="InventoryPortraitViewModel.ApplyDialogue"/>).</summary>
    public InventoryPortraitViewModel Portrait { get; } = new();

    /// <summary>Row <paramref name="r"/> (0 to 2, top to bottom).</summary>
    internal InventoryTextViewModel Row(int r) => _rows[r];

    // The cursor sprites, by image 0 to 3 of the box (u 176 + 16 i of the wind sheet), named by the catalogue.
    // E19.f3b: shared with the choice box's view model (AlundraChoiceViewModel), whose cursor is the same four sprites.
    internal static readonly string[] CursorSprites = { "wind_150", "wind_173", "wind_201", "wind_228" };

    private const int TextLeft = 32;      // the clip canvas and the rows start at x 32
    private const int RowTop = 5;         // a row sits 5 pixels under the frame's top ...
    private const int RowPitch = 16;      // ... and the rows are 16 apart
    private const int CursorTop = 32;     // the cursor sits 32 pixels under the frame's top

    /// <summary>
    /// Writes the drawn state of <paramref name="box"/> after its last pass: nothing shows while the box is not drawn; else the frame at the box's Y, the clip of the pass,
    /// each row's text at <c>RowX - 32</c> and <c>Y + 5 + 16 i - RowOffset - ClipTop</c> (relative to the clip canvas), and the cursor when its image is posed.
    /// </summary>
    internal void Apply(DialogueBox box) => Apply(box, false);

    /// <summary>
    /// E19.f4c2 F4C2-R3: <see cref="Apply(DialogueBox)"/> for a screen that also shows the speaker. <paramref name="speakerDrawn"/>: the name box or the portrait drew this pass.
    /// The root shows while the box or the speaker is drawn; while only the speaker is (the second speaker of two in a row draws before its box does, and a portrait left at rest has no
    /// box), the frame, the clip with its rows and the cursor fold.
    /// </summary>
    internal void Apply(DialogueBox box, bool speakerDrawn)
    {
        ArgumentNullException.ThrowIfNull(box);
        AppliedCount++;

        if (!box.Drawn && !speakerDrawn)
        {
            RootVisibility = Visibility.Collapsed;
            return;
        }

        RootVisibility = Visibility.Visible;
        if (!box.Drawn)
        {
            Frame.Visibility = Visibility.Collapsed;
            Clip.Visibility = Visibility.Collapsed;
            Cursor.Visibility = Visibility.Collapsed;
            return;
        }

        Frame.Visibility = Visibility.Visible;
        Clip.Visibility = Visibility.Visible;
        Frame.Top = box.Y;
        Clip.Top = box.ClipTop;
        Clip.Height = box.ClipHeight;
        for (var r = 0; r < _rows.Length; r++)
        {
            var row = _rows[r];
            row.Text = box.Row(r);
            row.Left = box.RowX(r) - TextLeft;
            row.Top = box.Y + RowTop + RowPitch * r - box.RowOffset - box.ClipTop;
        }

        var image = box.CursorImage;
        if (image >= 0 && image < CursorSprites.Length)
        {
            Cursor.SourceName = CursorSprites[image];
            Cursor.Top = box.Y + CursorTop;
            Cursor.Visibility = Visibility.Visible;
        }
        else
        {
            Cursor.Visibility = Visibility.Collapsed;
        }
    }
}
