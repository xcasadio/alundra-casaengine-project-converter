#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5): one box of the save screen's picker - the shared
/// <see cref="InventoryImageViewModel"/> shape (position, visibility) plus the box tint (J4), which
/// <c>SaveScreen.xaml</c> binds to the image's <see cref="MGImage.TextureColor"/>.
/// </summary>
public sealed class SaveScreenBoxViewModel : ViewModelBase
{
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;
    private Color? _textureColor;

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

    /// <summary>The box's colour modulation: the original's tint, 0x80 the texture's own colour.</summary>
    public Color? TextureColor
    {
        get => _textureColor;
        set
        {
            if (_textureColor != value)
            {
                _textureColor = value;
                NotifyPropertyChanged();
            }
        }
    }
}

/// <summary>
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5, J4): what <c>UI/Screens/SaveScreen.xaml</c> binds - the message box
/// and its two lines, the picker's four boxes with their tint and their two lines each.
/// <see cref="AlundraSaveScreenPresenter"/> writes it once per logic tick from
/// <see cref="AlundraSaveScreenDirector"/>'s already-ticked state (<see cref="Apply"/>); only the values that change
/// notify. Every text comes from the game (ETC texts, the computed summary), bounded by the director (L3).
/// </summary>
public sealed class AlundraSaveScreenViewModel : ViewModelBase
{
    private readonly SaveScreenBoxViewModel[] _recordBoxes = { new(), new(), new(), new() };
    private readonly InventoryTextViewModel[] _recordLines0 = { new(), new(), new(), new() };
    private readonly InventoryTextViewModel[] _recordLines1 = { new(), new(), new(), new() };
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

    /// <summary><c>g_uiBoxesInventoryDescriptionBackground</c>: the message box, also the picker's heading box.</summary>
    public InventoryImageViewModel MessageBox { get; } = new();

    public InventoryTextViewModel MessageLine0 { get; } = new();
    public InventoryTextViewModel MessageLine1 { get; } = new();

    public SaveScreenBoxViewModel RecordBox0 => _recordBoxes[0];
    public SaveScreenBoxViewModel RecordBox1 => _recordBoxes[1];
    public SaveScreenBoxViewModel RecordBox2 => _recordBoxes[2];
    public SaveScreenBoxViewModel RecordBox3 => _recordBoxes[3];

    public InventoryTextViewModel Record0Line0 => _recordLines0[0];
    public InventoryTextViewModel Record0Line1 => _recordLines1[0];
    public InventoryTextViewModel Record1Line0 => _recordLines0[1];
    public InventoryTextViewModel Record1Line1 => _recordLines1[1];
    public InventoryTextViewModel Record2Line0 => _recordLines0[2];
    public InventoryTextViewModel Record2Line1 => _recordLines1[2];
    public InventoryTextViewModel Record3Line0 => _recordLines0[3];
    public InventoryTextViewModel Record3Line1 => _recordLines1[3];

    /// <summary>
    /// Writes this tick's state of <paramref name="director"/>: the root shown while the screen is active; the
    /// message box and each record only on a tick the original draws them (a hidden one keeps its place and empties
    /// its lines); a record's tint as the colour of its box.
    /// </summary>
    public void Apply(AlundraSaveScreenDirector director)
    {
        ArgumentNullException.ThrowIfNull(director);
        AppliedCount++;

        if (!director.IsActive)
        {
            RootVisibility = Visibility.Collapsed;
            return;
        }

        RootVisibility = Visibility.Visible;

        var messageDrawn = director.IsMessageBoxDrawn;
        var (boxX, boxY) = director.MessageBoxPosition;
        MessageBox.Left = boxX;
        MessageBox.Top = boxY;
        MessageBox.Visibility = messageDrawn ? Visibility.Visible : Visibility.Collapsed;
        ApplyText(MessageLine0, director.MessageLine0, messageDrawn);
        ApplyText(MessageLine1, director.MessageLine1, messageDrawn);

        for (var i = 0; i < AlundraSaveScreenDirector.RecordCount; i++)
        {
            var drawn = director.IsRecordDrawn(i);
            var box = _recordBoxes[i];
            var (x, y) = director.RecordPosition(i);
            box.Left = x;
            box.Top = y;
            box.Visibility = drawn ? Visibility.Visible : Visibility.Collapsed;
            box.TextureColor = TintColor(director.RecordTint(i));
            ApplyText(_recordLines0[i], director.RecordLine0(i), drawn);
            ApplyText(_recordLines1[i], director.RecordLine1(i), drawn);
        }
    }

    /// <summary>The PS1 texture modulation: a channel of 0x80 draws the texel unchanged, 0 black; so twice the tint,
    /// capped at 255.</summary>
    internal static Color TintColor(int tint)
    {
        var channel = Math.Clamp(tint * 2, 0, 255);
        return new Color(channel, channel, channel, 255);
    }

    private static void ApplyText(InventoryTextViewModel target, (string Text, int X, int Y) line, bool drawn)
    {
        target.Text = drawn ? line.Text : string.Empty;
        target.Left = line.X;
        target.Top = line.Y;
    }
}
