#nullable enable
using System;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-6): reads <see cref="AlundraInventoryDirector"/>'s own
/// already-ticked state plus the item/stat lookups it does not own (<see cref="AlundraPlayerManager"/>,
/// <see cref="AlundraItemTables"/>, <see cref="AlundraPlayerStats"/>), composes an
/// <see cref="InventoryDisplayModel"/> through the pure <see cref="AlundraInventoryComposer"/>, and writes
/// it into the screen's <see cref="AlundraInventoryViewModel"/> (parent ADR-0002), which the screen's XAML binds -
/// one <see cref="Tick"/> call per LOGIC tick, made from
/// <see cref="AlundraWorldProxy.Update(float)"/>'s own pad-tick loop, immediately after
/// <see cref="AlundraInventoryDirector.Tick"/> (same site the director itself already reads its pad edges
/// from - <c>AlundraWorldProxy.cs</c>'s own comment on why a consumer of those edges must run inside that
/// same per-tick loop).
///
/// <b>Push/remove (dialogue precedent, <see cref="AlundraDialoguePresenter"/>, its own class doc and
/// <c>:114-133</c>)</b>: this presenter owns the screen (constructed once, alongside this presenter) and
/// pushes it onto the active <see cref="IUIViewRuntime"/> the moment
/// <see cref="AlundraInventoryDirector.IsDrawn"/> turns true (never on the setup tick, where it is still
/// false - <see cref="AlundraInventoryDirector.IsDrawn"/>'s own doc), removing it the moment
/// <see cref="AlundraInventoryDirector.IsActive"/> turns false (the close slide's very last tick).
/// </summary>
public sealed class AlundraInventoryPresenter
{
    // MainInventoryManager.cs:1305 (herbs, g_ItemIdBySlotIndex[6]) and :1524 (g_3d, the key item id) -
    // re-cited here since this presenter, not the director, is what asks AlundraPlayerManager for their
    // owned counts (the director's own GItemIdBySlotIndex/SlotIdByInventorySlotIndex tables stay private).
    private const int HerbItemId = 0x24;
    private const int KeyItemId = 0x3d;

    private readonly AlundraInventoryDirector _director;
    private readonly AlundraGameState _gameState;
    private readonly AlundraItemTables _itemTables;
    private readonly AlundraInventoryViewModel _viewModel;
    private readonly IUIScreen _screen;
    private readonly IUIViewRuntime? _uiView;
    private bool _pushed;

    public AlundraInventoryPresenter(
        AlundraInventoryDirector director,
        AlundraGameState gameState,
        AlundraItemTables itemTables,
        AlundraInventoryViewModel viewModel,
        IUIScreen screen,
        IUIViewRuntime? uiView)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(itemTables);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(screen);

        _director = director;
        _gameState = gameState;
        _itemTables = itemTables;
        _viewModel = viewModel;
        _screen = screen;
        _uiView = uiView;
    }

    /// <summary>Test-only seam: whether this presenter currently believes its screen is pushed - so a
    /// test can pin push/remove timing without a real <see cref="IUIViewRuntime"/>.</summary>
    internal bool IsPushedForTests => _pushed;

    public void Tick()
    {
        if (_director.IsDrawn)
        {
            PushScreenIfNeeded();
            _viewModel.Apply(ComposeModel());
            return;
        }

        if (!_director.IsActive)
        {
            RemoveScreenIfPushed();
        }
    }

    private void PushScreenIfNeeded()
    {
        if (_pushed)
        {
            return;
        }

        _uiView?.PushScreen(_screen);
        _pushed = true;
    }

    private void RemoveScreenIfPushed()
    {
        if (!_pushed)
        {
            return;
        }

        _uiView?.RemoveScreen(_screen);
        _pushed = false;
    }

    private InventoryDisplayModel ComposeModel()
    {
        var boxPositions = new (int X, int Y)[7];
        for (var i = 0; i < boxPositions.Length; i++)
        {
            boxPositions[i] = _director.BoxPosition(i);
        }

        var slotItemIds = new int?[AlundraInventoryComposer.SlotCount];
        var slotIconAssetIds = new Guid?[AlundraInventoryComposer.SlotCount];

        for (var slot = 0; slot < AlundraInventoryComposer.SlotCount; slot++)
        {
            var itemId = _director.ResolveSlotItemId(slot);
            slotItemIds[slot] = itemId;

            if (itemId != null && _itemTables.TryGetIconAssetId(itemId.Value, out var assetId))
            {
                slotIconAssetIds[slot] = assetId;
            }
        }

        var currentWeaponItem = AlundraPlayerManager.GetItemIdFromCurrentWeapon(_gameState, _itemTables);
        int? currentWeaponItemId = currentWeaponItem == AlundraPlayerManager.NoItem ? null : (int)currentWeaponItem;

        // MainInventoryManager.cs:1420 (SetItemIdFromCurrentItemId) - has a side effect
        // (AlundraPlayerManager.SetItemIdFromCurrentItemId's own doc), same "only while drawn" guard
        // AlundraHudPresenter.Tick already applies to its own equipment-icon lookup for the same reason.
        var currentItem = AlundraPlayerManager.SetItemIdFromCurrentItemId(_gameState, _itemTables);
        int? currentItemId = currentItem == AlundraPlayerManager.NoItem ? null : (int)currentItem;

        var herbCount = AlundraPlayerManager.GetNumberOfItem(_gameState, HerbItemId);
        var keyCount = AlundraPlayerManager.GetNumberOfItem(_gameState, KeyItemId);
        var falconTotal = _gameState.PlayerStats.Falcon + _gameState.PlayerStats.FalconTemp;

        return AlundraInventoryComposer.Compose(
            _director.IsDrawn,
            boxPositions,
            _director.SelectedSlotId,
            _director.CursorFrameDelay,
            _director.EquippedWeaponName,
            _director.EquippedItemName,
            _director.DrawnDescriptionLine0,
            _director.DrawnDescriptionLine1,
            slotItemIds,
            slotIconAssetIds,
            currentWeaponItemId,
            currentItemId,
            herbCount,
            _gameState.PlayerStats.Money,
            keyCount,
            falconTotal);
    }
}
