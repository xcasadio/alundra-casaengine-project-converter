#nullable enable
using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.Scripts;

/// <summary>
/// One image of the inventory screen as its XAML binds it (parent ADR-0002): its source (an asset id, resolved
/// by the engine's UI asset provider), its native canvas position, and its visibility. Every setter notifies only
/// on an actual change, so a tick that changes nothing pushes nothing.
/// </summary>
public sealed class InventoryImageViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;

    /// <summary>The image's asset id (engine ADR-0038, "Images are named by asset"), or null when the XAML names
    /// the source itself.</summary>
    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName != value)
            {
                _sourceName = value;
                _sourceId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
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

    /// <summary>Sets <see cref="SourceName"/> to <paramref name="assetId"/>, formatting the id only when it changed.</summary>
    internal void SetSource(Guid assetId)
    {
        if (_sourceId != assetId || _sourceName == null)
        {
            SourceName = assetId.ToString("D");
        }
    }

    internal void Show(int left, int top)
    {
        Left = left;
        Top = top;
        Visibility = Visibility.Visible;
    }
}

/// <summary>
/// The opening portrait as both inventory screens bind it (docs/plan-portrait-inventaire.md, PI8): the image sits at
/// its rest position (<see cref="AlundraInventoryPortrait.RestX"/>, <see cref="AlundraInventoryPortrait.RestY"/>) in
/// the XAML, and the flight moves and scales it through the bindable render transform (MGUI ADR-0020): the
/// translation is the quad's top-left minus the rest position, the scale its size over 48x56, anchored at the
/// top-left corner. <see cref="Vector2"/> on both sides, so each push is a typed copy. Every setter notifies only on
/// an actual change: a portrait at rest pushes nothing.
/// </summary>
public sealed class InventoryPortraitViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private Vector2 _translation;
    private Vector2 _scale = Vector2.One;
    private Visibility _visibility = Visibility.Collapsed;

    /// <summary>The portrait sprite's asset id (<c>Data/inventory-portrait.json</c>), null until known.</summary>
    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName != value)
            {
                _sourceName = value;
                _sourceId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
                NotifyPropertyChanged();
            }
        }
    }

    public Vector2 Translation
    {
        get => _translation;
        set
        {
            if (_translation != value)
            {
                _translation = value;
                NotifyPropertyChanged();
            }
        }
    }

    public Vector2 Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
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

    /// <summary>Writes the quad <paramref name="portrait"/> drew this tick. A quad with no area (idle, the first
    /// opening call, the last return call) is a scale of zero, exactly the original's 0x0 quad, and never a
    /// visibility change: collapsing and showing the element would run the window's layout twice per flight, which the
    /// render-only transform exists to avoid (plan D3, MGUI ADR-0006). Only a missing source (degraded export)
    /// collapses the element.</summary>
    internal void Apply(AlundraInventoryPortrait portrait, Guid? sourceId)
    {
        if (!sourceId.HasValue)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        if (_sourceId != sourceId.Value || _sourceName == null)
        {
            SourceName = sourceId.Value.ToString("D");
        }

        Translation = new Vector2(portrait.X - AlundraInventoryPortrait.RestX, portrait.Y - AlundraInventoryPortrait.RestY);
        Scale = new Vector2(
            portrait.DrawnWidth / (float)AlundraInventoryPortrait.FullWidth,
            portrait.DrawnHeight / (float)AlundraInventoryPortrait.FullHeight);
        Visibility = Visibility.Visible;
    }
}

/// <summary>One text line of the inventory screen as its XAML binds it: its text and native canvas position.</summary>
public sealed class InventoryTextViewModel : ViewModelBase
{
    private string _text = string.Empty;
    private int? _left;
    private int? _top;

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
}

/// <summary>
/// The inventory screen's view model (parent ADR-0002, engine ADR-0038): what <c>UI/Screens/InventoryScreen.xaml</c>
/// binds, one named member per element of the screen. <see cref="AlundraInventoryPresenter"/> writes it once per
/// logic tick through <see cref="Apply"/>, from the composer's <see cref="InventoryDisplayModel"/>; only the values
/// that change notify, so a settled inventory pushes nothing to the screen.
/// <para/>
/// The six boxes, the two selection frames and the cursor name their source in the XAML; the icons and the digits
/// take theirs from here. The cursor is the <c>ui_inventory_cursor</c> animation, which carries the original's
/// per-phase pixel offset itself: its position here is the composer's base position.
/// </summary>
public sealed class AlundraInventoryViewModel : ViewModelBase
{
    // wind_000/002/009/016/023/030/037/045/055/065, the digit glyphs 0-9 (the same ids AlundraHudScreen resolves).
    // Internal (not private): AlundraSubInventoryViewModel reuses this table as-is for its own money/falcon/key
    // digits (E13.d SI4 brief - "reuse the main VM's, make it internal static shared if needed").
    internal static readonly Guid[] DigitAssetIds =
    {
        Guid.Parse("bc300193-4244-5138-a27f-f242a150bed7"), Guid.Parse("01dbdef2-854c-5c76-a482-e19bc579fa80"),
        Guid.Parse("91a9c466-d267-5063-a60d-8f4b60cf2a41"), Guid.Parse("fc448c9c-7759-58dc-a85d-68c02bc09380"),
        Guid.Parse("0740074c-09c3-5bfd-b982-45b75869ce6e"), Guid.Parse("fbeb05c1-a686-5e2d-a08a-13a6f8287b96"),
        Guid.Parse("fb437995-e6fd-51f0-ad3e-4d6dcc51dbcc"), Guid.Parse("c5af13a8-6890-56bb-b2a4-eb3912509c93"),
        Guid.Parse("d12a690e-48ba-5a1c-84e1-d1f56d8bb3ec"), Guid.Parse("cd607fcd-8657-5856-9136-76e1194389c9"),
    };

    private readonly Func<Guid, Point?>? _iconSize;
    private readonly InventoryImageViewModel[] _iconSlots = new InventoryImageViewModel[AlundraInventoryComposer.SlotCount];
    private readonly InventoryImageViewModel[] _moneyDigits = { new(), new(), new(), new() };
    private readonly InventoryImageViewModel[] _falconDigits = { new(), new() };
    private readonly InventoryImageViewModel[] _keyDigits = { new(), new() };
    private Visibility _rootVisibility = Visibility.Collapsed;

    /// <summary>A view model with no icon sizes: the editor's design-time data, which gives every position itself.</summary>
    public AlundraInventoryViewModel()
        : this(null)
    {
    }

    /// <param name="iconSize">The native size of an icon sprite, or null when it cannot be read: an icon is centred
    /// in its cell from its size (D-E13D-10), and stays hidden without one.</param>
    public AlundraInventoryViewModel(Func<Guid, Point?>? iconSize)
    {
        _iconSize = iconSize;
        for (var i = 0; i < _iconSlots.Length; i++)
        {
            _iconSlots[i] = new InventoryImageViewModel();
        }
    }

    /// <summary>How many models <see cref="Apply"/> received: one per logic tick while the inventory is drawn.</summary>
    internal int AppliedModelCount { get; private set; }

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

    public InventoryImageViewModel BoxWeapon { get; } = new();
    public InventoryImageViewModel BoxItem { get; } = new();
    public InventoryImageViewModel BoxWeaponName { get; } = new();
    public InventoryImageViewModel BoxItemName { get; } = new();
    public InventoryImageViewModel BoxMoneyFalconKey { get; } = new();
    public InventoryImageViewModel BoxDescription { get; } = new();

    public InventoryImageViewModel IconSlot0 => _iconSlots[0];
    public InventoryImageViewModel IconSlot1 => _iconSlots[1];
    public InventoryImageViewModel IconSlot2 => _iconSlots[2];
    public InventoryImageViewModel IconSlot3 => _iconSlots[3];
    public InventoryImageViewModel IconSlot4 => _iconSlots[4];
    public InventoryImageViewModel IconSlot5 => _iconSlots[5];
    public InventoryImageViewModel IconSlot6 => _iconSlots[6];
    public InventoryImageViewModel IconSlot7 => _iconSlots[7];
    public InventoryImageViewModel IconSlot8 => _iconSlots[8];
    public InventoryImageViewModel IconSlot9 => _iconSlots[9];
    public InventoryImageViewModel IconSlot10 => _iconSlots[10];
    public InventoryImageViewModel IconSlot11 => _iconSlots[11];
    public InventoryImageViewModel IconSlot12 => _iconSlots[12];
    public InventoryImageViewModel IconSlot13 => _iconSlots[13];
    public InventoryImageViewModel IconSlot14 => _iconSlots[14];
    public InventoryImageViewModel IconSlot15 => _iconSlots[15];
    public InventoryImageViewModel IconSlot16 => _iconSlots[16];
    public InventoryImageViewModel IconSlot17 => _iconSlots[17];
    public InventoryImageViewModel IconSlot18 => _iconSlots[18];
    public InventoryImageViewModel IconSlot19 => _iconSlots[19];
    public InventoryImageViewModel IconSlot20 => _iconSlots[20];
    public InventoryImageViewModel IconSlot21 => _iconSlots[21];
    public InventoryImageViewModel IconSlot22 => _iconSlots[22];
    public InventoryImageViewModel IconSlot23 => _iconSlots[23];

    public InventoryImageViewModel HerbCountDigit { get; } = new();
    public InventoryImageViewModel MoneyDigit0 => _moneyDigits[0];
    public InventoryImageViewModel MoneyDigit1 => _moneyDigits[1];
    public InventoryImageViewModel MoneyDigit2 => _moneyDigits[2];
    public InventoryImageViewModel MoneyDigit3 => _moneyDigits[3];
    public InventoryImageViewModel FalconDigit0 => _falconDigits[0];
    public InventoryImageViewModel FalconDigit1 => _falconDigits[1];
    public InventoryImageViewModel KeyDigit0 => _keyDigits[0];
    public InventoryImageViewModel KeyDigit1 => _keyDigits[1];

    public InventoryTextViewModel WeaponName { get; } = new();
    public InventoryTextViewModel ItemName { get; } = new();
    public InventoryTextViewModel DescriptionLine0 { get; } = new();
    public InventoryTextViewModel DescriptionLine1 { get; } = new();

    public InventoryImageViewModel WeaponSelectionFrame { get; } = new();
    public InventoryImageViewModel ItemSelectionFrame { get; } = new();
    public InventoryImageViewModel Cursor { get; } = new();

    /// <summary>The opening portrait (docs/plan-portrait-inventaire.md, PI8), written by the presenter.</summary>
    public InventoryPortraitViewModel Portrait { get; } = new();

    /// <summary>Writes one tick's composed display into the bound members. Mirrors what the screen used to push
    /// into its controls directly: a box or an icon absent from the model stays as it was, or hidden.</summary>
    public void Apply(InventoryDisplayModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        AppliedModelCount++;

        if (!model.Visible)
        {
            RootVisibility = Visibility.Collapsed;
            return;
        }

        RootVisibility = Visibility.Visible;

        for (var i = 0; i < model.Boxes.Count; i++)
        {
            var box = model.Boxes[i];
            BoxAt(box.BoxIndex)?.Show(box.NativeX, box.NativeY);
        }

        ApplyIcons(model);

        ApplyDigit(HerbCountDigit, model.HerbDigitVisible, model.HerbDigit);
        for (var i = 0; i < _moneyDigits.Length; i++)
        {
            ApplyDigit(_moneyDigits[i], true, model.MoneyDigits.Count > i ? model.MoneyDigits[i] : default);
        }

        for (var i = 0; i < _falconDigits.Length; i++)
        {
            ApplyDigit(_falconDigits[i], true, model.FalconDigits.Count > i ? model.FalconDigits[i] : default);
        }

        for (var i = 0; i < _keyDigits.Length; i++)
        {
            ApplyDigit(_keyDigits[i], true, model.KeyDigits.Count > i ? model.KeyDigits[i] : default);
        }

        ApplyText(WeaponName, model.WeaponName, model.WeaponNameX, model.WeaponNameY);
        ApplyText(ItemName, model.ItemName, model.ItemNameX, model.ItemNameY);
        ApplyText(DescriptionLine0, model.DescriptionLine0, model.DescriptionLine0X, model.DescriptionLine0Y);
        ApplyText(DescriptionLine1, model.DescriptionLine1, model.DescriptionLine1X, model.DescriptionLine1Y);

        ApplyFrame(WeaponSelectionFrame, model.WeaponSelectionFrame);
        ApplyFrame(ItemSelectionFrame, model.ItemSelectionFrame);

        Cursor.Show(model.Cursor.BaseNativeX, model.Cursor.BaseNativeY);
    }

    private InventoryImageViewModel? BoxAt(int boxIndex) => boxIndex switch
    {
        0 => BoxWeapon,
        1 => BoxItem,
        2 => BoxWeaponName,
        3 => BoxItemName,
        5 => BoxMoneyFalconKey,
        6 => BoxDescription,
        _ => null,
    };

    private void ApplyIcons(InventoryDisplayModel model)
    {
        // A slot the composer lists shows its icon; every other slot is hidden. Visibility is decided per slot
        // before it is set, so a slot that stays shown never notifies.
        for (var slot = 0; slot < _iconSlots.Length; slot++)
        {
            var shown = false;
            for (var i = 0; i < model.SlotIcons.Count; i++)
            {
                var slotIcon = model.SlotIcons[i];
                if (slotIcon.SlotIndex != slot)
                {
                    continue;
                }

                var icon = slotIcon.Icon;
                var size = _iconSize?.Invoke(icon.AssetId);
                if (size == null)
                {
                    break;
                }

                var image = _iconSlots[slot];
                image.SetSource(icon.AssetId);

                // The canvas is scaled once as a whole, so every position here is native: pixel scale 1.
                image.Show(icon.ScreenLeft(size.Value.X, 1), icon.ScreenTop(size.Value.Y, 1));
                shown = true;
                break;
            }

            if (!shown)
            {
                _iconSlots[slot].Visibility = Visibility.Collapsed;
            }
        }
    }

    private static void ApplyDigit(InventoryImageViewModel image, bool visible, InventoryDigit digit)
    {
        if (!visible)
        {
            image.Visibility = Visibility.Collapsed;
            return;
        }

        image.SetSource(DigitAssetIds[Math.Clamp(digit.Value, 0, 9)]);
        image.Show(digit.NativeX, digit.NativeY);
    }

    private static void ApplyText(InventoryTextViewModel text, string value, int left, int top)
    {
        text.Text = value;
        text.Left = left;
        text.Top = top;
    }

    private static void ApplyFrame(InventoryImageViewModel image, InventorySelectionFrame frame)
    {
        if (!frame.Visible)
        {
            image.Visibility = Visibility.Collapsed;
            return;
        }

        image.Show(frame.NativeX, frame.NativeY);
    }
}
