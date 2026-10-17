#nullable enable
using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Entities;
using Alundra.Dialogues;
using Alundra.UI.Inventory;

namespace Alundra.Scripts;

/// <summary>
/// The inventory screen's view model (parent ADR-0002, engine ADR-0038): what <c>UI/Screens/InventoryScreen.xaml</c>
/// binds, one named member per element of the screen. <see cref="InventoryPresenter"/> writes it once per
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
    private readonly InventoryImageViewModel[] _iconSlots = new InventoryImageViewModel[InventoryComposer.SlotCount];
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
