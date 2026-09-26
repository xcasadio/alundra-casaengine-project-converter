#nullable enable
using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.Scripts;

/// <summary>
/// The sub-inventory screen's view model (parent ADR-0002, engine ADR-0038): what
/// <c>UI/Screens/SubInventoryScreen.xaml</c> binds, one named member per element of the screen.
/// <see cref="AlundraSubInventoryPresenter"/> writes it once per logic tick through <see cref="Apply"/>, from
/// the composer's <see cref="SubInventoryDisplayModel"/>; only the values that change notify, so a settled
/// sub-inventory pushes nothing to the screen. Reuses <see cref="InventoryImageViewModel"/>/
/// <see cref="InventoryTextViewModel"/> from <see cref="AlundraInventoryViewModel"/> (E13.d SI4 brief) - the
/// same per-element shape, no behaviour change.
/// <para/>
/// The seven boxes, the two selection frames and the cursor name their source in the XAML; the icons and the
/// digits take theirs from here. The cursor is the <c>ui_inventory_cursor</c> animation, which carries the
/// original's per-phase pixel offset itself (D-E13D-27): its position here is the composer's base position.
/// </summary>
public sealed class AlundraSubInventoryViewModel : ViewModelBase
{
    private readonly InventoryImageViewModel[] _armoryIcons = { new(), new(), new(), new(), new(), new(), new() };
    private readonly InventoryImageViewModel[] _keyItemIcons = { new(), new(), new(), new(), new() };
    private readonly InventoryImageViewModel[] _moneyDigits = { new(), new(), new(), new() };
    private readonly InventoryImageViewModel[] _falconDigits = { new(), new() };
    private readonly InventoryImageViewModel[] _keyDigits = { new(), new() };
    private Visibility _rootVisibility = Visibility.Collapsed;

    /// <summary>How many models <see cref="Apply"/> received: one per logic tick while the sub-inventory is
    /// drawn.</summary>
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

    public InventoryImageViewModel BoxArmory { get; } = new();
    public InventoryImageViewModel BoxKeyItems { get; } = new();
    public InventoryImageViewModel BoxArmorName { get; } = new();
    public InventoryImageViewModel BoxBootsName { get; } = new();
    public InventoryImageViewModel BoxEquipmentIcons { get; } = new();
    public InventoryImageViewModel BoxDescription { get; } = new();
    public InventoryImageViewModel BoxMoneyFalconKey { get; } = new();

    public InventoryImageViewModel ArmorFrame { get; } = new();
    public InventoryImageViewModel BootsFrame { get; } = new();

    public InventoryImageViewModel ArmoryIcon0 => _armoryIcons[0];
    public InventoryImageViewModel ArmoryIcon1 => _armoryIcons[1];
    public InventoryImageViewModel ArmoryIcon2 => _armoryIcons[2];
    public InventoryImageViewModel ArmoryIcon3 => _armoryIcons[3];
    public InventoryImageViewModel ArmoryIcon4 => _armoryIcons[4];
    public InventoryImageViewModel ArmoryIcon5 => _armoryIcons[5];
    public InventoryImageViewModel ArmoryIcon6 => _armoryIcons[6];

    public InventoryImageViewModel KeyItemIcon0 => _keyItemIcons[0];
    public InventoryImageViewModel KeyItemIcon1 => _keyItemIcons[1];
    public InventoryImageViewModel KeyItemIcon2 => _keyItemIcons[2];
    public InventoryImageViewModel KeyItemIcon3 => _keyItemIcons[3];
    public InventoryImageViewModel KeyItemIcon4 => _keyItemIcons[4];

    public InventoryImageViewModel ArmorIcon { get; } = new();
    public InventoryImageViewModel BootsIcon { get; } = new();

    public InventoryTextViewModel ArmorName { get; } = new();
    public InventoryTextViewModel BootsName { get; } = new();
    public InventoryTextViewModel DescriptionLine0 { get; } = new();
    public InventoryTextViewModel DescriptionLine1 { get; } = new();

    public InventoryImageViewModel MoneyDigit0 => _moneyDigits[0];
    public InventoryImageViewModel MoneyDigit1 => _moneyDigits[1];
    public InventoryImageViewModel MoneyDigit2 => _moneyDigits[2];
    public InventoryImageViewModel MoneyDigit3 => _moneyDigits[3];
    public InventoryImageViewModel FalconDigit0 => _falconDigits[0];
    public InventoryImageViewModel FalconDigit1 => _falconDigits[1];
    public InventoryImageViewModel KeyDigit0 => _keyDigits[0];
    public InventoryImageViewModel KeyDigit1 => _keyDigits[1];

    public InventoryImageViewModel Cursor { get; } = new();

    /// <summary>The opening portrait, shared with the main inventory (docs/plan-portrait-inventaire.md, PI8).</summary>
    public InventoryPortraitViewModel Portrait { get; } = new();

    /// <summary>Writes one tick's composed display into the bound members. Mirrors
    /// <see cref="AlundraInventoryViewModel.Apply"/>: a box or an icon absent from the model stays as it was,
    /// or hidden.</summary>
    public void Apply(SubInventoryDisplayModel model)
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

        ApplySlotIcons(_armoryIcons, model.ArmoryIcons);
        ApplySlotIcons(_keyItemIcons, model.KeyItemIcons);

        ApplyEquipmentIcon(ArmorIcon, ArmorFrame, model.Armor);
        ApplyEquipmentIcon(BootsIcon, BootsFrame, model.Boots);

        ApplyText(ArmorName, model.ArmorName, model.ArmorNameX, model.ArmorNameY);
        ApplyText(BootsName, model.BootsName, model.BootsNameX, model.BootsNameY);
        ApplyText(DescriptionLine0, model.DescriptionLine0, model.DescriptionLine0X, model.DescriptionLine0Y);
        ApplyText(DescriptionLine1, model.DescriptionLine1, model.DescriptionLine1X, model.DescriptionLine1Y);

        for (var i = 0; i < _moneyDigits.Length; i++)
        {
            ApplyDigit(_moneyDigits[i], model.MoneyDigits.Count > i ? model.MoneyDigits[i] : default);
        }

        for (var i = 0; i < _falconDigits.Length; i++)
        {
            ApplyDigit(_falconDigits[i], model.FalconDigits.Count > i ? model.FalconDigits[i] : default);
        }

        for (var i = 0; i < _keyDigits.Length; i++)
        {
            ApplyDigit(_keyDigits[i], model.KeyDigits.Count > i ? model.KeyDigits[i] : default);
        }

        Cursor.Show(model.Cursor.NativeX, model.Cursor.NativeY);
    }

    private InventoryImageViewModel? BoxAt(int boxIndex) => boxIndex switch
    {
        0 => BoxArmory,
        1 => BoxKeyItems,
        2 => BoxArmorName,
        3 => BoxBootsName,
        4 => BoxEquipmentIcons,
        5 => BoxDescription,
        6 => BoxMoneyFalconKey,
        _ => null,
    };

    private void ApplySlotIcons(InventoryImageViewModel[] slots, System.Collections.Generic.IReadOnlyList<SubInventorySlotIcon> icons)
    {
        // Same "decide visibility per slot before setting it" shape as AlundraInventoryViewModel.ApplyIcons -
        // a slot that stays shown never notifies.
        for (var slot = 0; slot < slots.Length; slot++)
        {
            var shown = false;
            for (var i = 0; i < icons.Count; i++)
            {
                var slotIcon = icons[i];
                if (slotIcon.SlotIndex != slot)
                {
                    continue;
                }

                var icon = slotIcon.Icon;
                var image = slots[slot];
                image.SetSource(icon.AssetId);

                // D-E13D-34: the original's own top-left, no centring. The canvas is scaled once as a whole, so
                // every position here is native.
                image.Show(icon.NativeX, icon.NativeY);
                shown = true;
                break;
            }

            if (!shown)
            {
                slots[slot].Visibility = Visibility.Collapsed;
            }
        }
    }

    private static void ApplyEquipmentIcon(InventoryImageViewModel iconImage, InventoryImageViewModel frameImage, SubInventoryEquipmentIcon? equipment)
    {
        if (equipment == null)
        {
            iconImage.Visibility = Visibility.Collapsed;
            frameImage.Visibility = Visibility.Collapsed;
            return;
        }

        // FUN_80052fb4: the frame is visible whenever the item resolves, at the icon's own position - the same
        // "hollow frame" the main inventory's own selection frames are. D-E13D-34: both at the original's
        // top-left, no centring.
        var (assetId, x, y) = equipment.Value;
        frameImage.Show(x, y);
        iconImage.SetSource(assetId);
        iconImage.Show(x, y);
    }

    private static void ApplyDigit(InventoryImageViewModel image, InventoryDigit digit)
    {
        image.SetSource(AlundraInventoryViewModel.DigitAssetIds[Math.Clamp(digit.Value, 0, 9)]);
        image.Show(digit.NativeX, digit.NativeY);
    }

    private static void ApplyText(InventoryTextViewModel text, string? value, int left, int top)
    {
        text.Text = value ?? string.Empty;
        text.Left = left;
        text.Top = top;
    }
}
