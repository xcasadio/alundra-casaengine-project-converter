#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.Scripts;

/// <summary>E19.f2b1c F2B1C-R2: the frame of the text box, <c>(16, Top)</c> in the XAML.</summary>
public sealed class TextBoxFrameViewModel : ViewModelBase
{
    private int? _top;

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
}

/// <summary>E19.f2b1c F2B1C-R2: the clip canvas of the three rows, <c>(32, Top)</c> wide 258 in the XAML.</summary>
public sealed class TextBoxClipViewModel : ViewModelBase
{
    private int? _top;
    private int? _height;

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

    /// <summary>Row <paramref name="r"/> (0 to 2, top to bottom).</summary>
    internal InventoryTextViewModel Row(int r) => _rows[r];

    // The cursor sprites, by image 0 to 3 of the box (u 176 + 16 i of the wind sheet), named by the catalogue.
    private static readonly string[] CursorSprites = { "wind_150", "wind_173", "wind_201", "wind_228" };

    private const int TextLeft = 32;      // the clip canvas and the rows start at x 32
    private const int RowTop = 5;         // a row sits 5 pixels under the frame's top ...
    private const int RowPitch = 16;      // ... and the rows are 16 apart
    private const int CursorTop = 32;     // the cursor sits 32 pixels under the frame's top

    /// <summary>
    /// Writes the drawn state of <paramref name="box"/> after its last pass: nothing shows while the box is not drawn; else the frame at the box's Y, the clip of the pass,
    /// each row's text at <c>RowX - 32</c> and <c>Y + 5 + 16 i - RowOffset - ClipTop</c> (relative to the clip canvas), and the cursor when its image is posed.
    /// </summary>
    internal void Apply(AlundraDialogueBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        AppliedCount++;

        if (!box.Drawn)
        {
            RootVisibility = Visibility.Collapsed;
            return;
        }

        RootVisibility = Visibility.Visible;
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
