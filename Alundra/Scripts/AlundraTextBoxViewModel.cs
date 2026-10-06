#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.Scripts;

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

/// <summary>
/// E19.f2b1c F2B1C-R2 (docs/plan-e19-opcodes.md): what <c>UI/Screens/TextBoxScreen.xaml</c> binds - the frame, the clip canvas, the three rows (text and
/// position relative to the clip canvas) and the cursor. <see cref="AlundraTextBoxPresenter"/> writes it after each pass of the dialogue box from the box's drawn
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

    /// <summary>E19.f4c2: the speaker's name box (written by <see cref="AlundraTextBoxPresenter"/> from <see cref="AlundraDialogueDirector.NameBox"/>).</summary>
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
    internal void Apply(AlundraDialogueBox box) => Apply(box, false);

    /// <summary>
    /// E19.f4c2 F4C2-R3: <see cref="Apply(AlundraDialogueBox)"/> for a screen that also shows the speaker. <paramref name="speakerDrawn"/>: the name box or the portrait drew this pass.
    /// The root shows while the box or the speaker is drawn; while only the speaker is (the second speaker of two in a row draws before its box does, and a portrait left at rest has no
    /// box), the frame, the clip with its rows and the cursor fold.
    /// </summary>
    internal void Apply(AlundraDialogueBox box, bool speakerDrawn)
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
