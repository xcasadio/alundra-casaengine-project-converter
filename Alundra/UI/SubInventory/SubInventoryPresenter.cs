#nullable enable
using System;
using CasaEngine.Framework.UI;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Items;
using Alundra.UI.Inventory;
using Alundra.UI.Shared;
using Alundra.Maps;

namespace Alundra.UI.SubInventory;

/// <summary>
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md): reads <see cref="SubInventoryDirector"/>'s own
/// already-ticked state plus the item lookups it does not own (<see cref="ItemTables"/>,
/// <see cref="GameState"/>), composes a <see cref="SubInventoryDisplayModel"/> through the pure
/// <see cref="SubInventoryComposer"/>, and writes it into the screen's
/// <see cref="SubInventoryViewModel"/> (parent ADR-0002), which the screen's XAML binds - one
/// <see cref="Tick"/> call per LOGIC tick, made from <see cref="WorldProxy.Update(float)"/>'s own
/// pad-tick loop, immediately after <see cref="SubInventoryDirector.Tick"/> (same "presenter runs
/// right after its director, inside the per-tick loop" shape <see cref="InventoryPresenter"/> already
/// establishes).
///
/// <b>Push/remove (same precedent as <see cref="InventoryPresenter"/>)</b>: this presenter owns the
/// screen (constructed once, alongside this presenter) and pushes it onto the active
/// <see cref="IUIViewRuntime"/> the moment <see cref="SubInventoryDirector.IsDrawn"/> turns true
/// (never on the setup tick, where it is still false), removing it the moment
/// <see cref="SubInventoryDirector.IsActive"/> turns false (the close slide's very last tick). Since
/// the two inventory screens are never pushed together (D-E13D-26 - the bascule always crosses at least one
/// tick where neither director is drawn, plan §1.2), this presenter and <see cref="InventoryPresenter"/>
/// never both hold their own screen pushed at the same tick.
/// </summary>
public sealed class SubInventoryPresenter
{
    private readonly SubInventoryDirector _director;
    private readonly GameState _gameState;
    private readonly ItemTables _itemTables;
    private readonly SubInventoryViewModel _viewModel;
    private readonly IUIScreen _screen;
    private readonly IUIViewRuntime? _uiView;

    // docs/plan-portrait-inventaire.md PI8: the portrait sprite, resolved once (null in degraded mode).
    private readonly Guid? _portraitAssetId;
    private bool _pushed;

    public SubInventoryPresenter(
        SubInventoryDirector director,
        GameState gameState,
        ItemTables itemTables,
        SubInventoryViewModel viewModel,
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
        _portraitAssetId = itemTables.TryGetInventoryPortraitAssetId(out var portraitAssetId) ? portraitAssetId : null;
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
            _viewModel.Portrait.Apply(InventoryPortrait.Instance, _portraitAssetId);
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

    private SubInventoryDisplayModel ComposeModel()
    {
        var boxPositions = new (int X, int Y)[7];
        for (var i = 0; i < boxPositions.Length; i++)
        {
            boxPositions[i] = _director.BoxPosition(i);
        }

        var armoryIconAssetIds = new Guid?[7];
        for (var i = 0; i < 7; i++)
        {
            if (_director.ArmoryOwned(i) && _itemTables.TryGetIconAssetId(ArmoryItemIdAt(i), out var assetId))
            {
                armoryIconAssetIds[i] = assetId;
            }
        }

        var keyItemIconAssetIds = new Guid?[5];
        for (var i = 0; i < 5; i++)
        {
            var itemId = _director.KeyItemIds[i];
            if (itemId != -1 && _itemTables.TryGetIconAssetId(itemId, out var assetId))
            {
                keyItemIconAssetIds[i] = assetId;
            }
        }

        Guid? armorIconAssetId = _director.ArmorItemId is { } armorId && _itemTables.TryGetIconAssetId(armorId, out var armorAsset)
            ? armorAsset
            : null;
        Guid? bootsIconAssetId = _director.BootsItemId is { } bootsId && _itemTables.TryGetIconAssetId(bootsId, out var bootsAsset)
            ? bootsAsset
            : null;

        var keyCount = PlayerManager.GetNumberOfItem(_gameState, KeyItemId);
        var falconTotal = _gameState.PlayerStats.Falcon + _gameState.PlayerStats.FalconTemp;

        return SubInventoryComposer.Compose(
            _director.IsDrawn,
            boxPositions,
            armoryIconAssetIds,
            keyItemIconAssetIds,
            armorIconAssetId,
            bootsIconAssetId,
            _director.ArmorName,
            _director.BootsName,
            _director.DrawnDescriptionLine0,
            _director.DrawnDescriptionLine1,
            _director.CursorReference(_director.SelectedPosition),
            _gameState.PlayerStats.Money,
            keyCount,
            falconTotal);
    }

    // FUN_80052dd8 (SubInventoryManager.cs:1203-1236), INT_ARRAY_800b42dc's own 7 item ids - re-cited here
    // (not exposed by the director) since this presenter, not the director, is what asks AlundraItemTables
    // for their icon asset ids (same "presenter asks the item tables" shape AlundraInventoryPresenter
    // already uses for its own slot icons).
    private const int KeyItemId = 0x3d; // MainInventoryManager.cs:1524 (g_3d) - the key item id.
    private static readonly int[] ArmoryItemIds = { 0x3e, 0x3f, 0x40, 0x41, 0x42, 0x43, 0x44 };

    private static int ArmoryItemIdAt(int index) => ArmoryItemIds[index];
}
