#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.c T4 (docs/plan-e16-etat-partie.md, C3, C4, D-E16-32, SC5) on the REAL converter export
/// (<c>alundra-project/</c>): a New Game capture validates, map 389's real <c>.tileMap</c> reads 52 x 60, and a
/// save at the bounds of every domain, applied, is drawn by the jauge and both inventories without an exception.
/// These tests need the export and FAIL naming it when it is missing, never self-skip (the convention of
/// <see cref="AlundraCellStoreProductionTests"/>). The item table is the export's own, loaded once per test and
/// used both for the state (New Game inventory, counters at their ceiling) and for the validation rules. Nothing
/// here reads or writes a save file (D-E16-31).
/// </summary>
public sealed class AlundraSaveGameProductionTests : IDisposable
{
    private const string Map389WorldName = "Ship Klark (beginning)-389";

    public AlundraSaveGameProductionTests()
    {
        ResetSingletons();
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static void ResetSingletons()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraInventoryPortrait.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "Maps", "world-index.json"))
                && File.Exists(Path.Combine(candidate, "Data", "items-properties.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraSaveGameProductionTests: no 'alundra-project/Maps/world-index.json' with "
            + $"'alundra-project/Data/items-properties.json' found above '{AppContext.BaseDirectory}' - these tests "
            + "need the real converter export and cannot self-skip without one (docs/plan-e16-etat-partie.md, E16.c T4).");
    }

    // ---- The real export -------------------------------------------------------------------------------

    [Fact]
    public void RealMap389_ReadsAs52By60()
    {
        var root = FindProjectRoot();
        var worldPath = new AlundraWorldIndexTable(root).Resolve(389);
        Assert.NotNull(worldPath);

        Assert.True(AlundraMapSizeReader.TryRead(root, worldPath!, out var w, out var h, out var error), error);
        Assert.Equal(52, w);
        Assert.Equal(60, h);
    }

    [Fact]
    public void RealWorldIndex_Has483Keys_0To482()
    {
        var index = new AlundraWorldIndexTable(FindProjectRoot());

        for (var id = 0; id <= 482; id++)
        {
            Assert.NotNull(index.Resolve(id));
        }

        Assert.Null(index.Resolve(483));
    }

    /// <summary>The acceptance's "capture valide acceptée": a New Game state (inventory included), the hero at the
    /// New Game tile of the real map 389, captured from the world, validates against the real export - with the
    /// SAME item table the inventory was filled from, and a catalog that answers yes.</summary>
    [Fact]
    public void NewGameCapture_OnMap389_IsAccepted_AtDataVersion1()
    {
        var root = FindProjectRoot();
        var tables = new AlundraItemTables(root);
        var state = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameInventory(state, tables);
        Assert.Contains(state.NumberOfItems, count => count != 0); // the New Game inventory is really there.
        var hero = new AlundraEntityScriptProxy
        {
            TileX = AlundraGameState.CameraTileX,
            TileY = AlundraGameState.CameraTileY,
            TileZ = AlundraGameState.CameraTileZ,
        };

        Assert.True(AlundraSaveGame.TryCaptureFromWorld(state, Map389WorldName, hero, out var save));
        var rules = new AlundraSaveGameRules(root, _ => true, tables);

        Assert.True(save!.TryValidate(rules, out var error), error);
        Assert.Equal(1, save.LoadedDataVersion);
        Assert.Equal(389, save.InitialMapId);
        Assert.Equal((33, 59, 0), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
    }

    // ---- Bounds, then the jauge and the inventories (SC5) --------------------------------------------------

    private static AlundraSaveGame BoundsSave(AlundraItemTables tables, bool atMaximum)
    {
        var save = new AlundraSaveGame { InitialMapId = 389 };
        for (var i = 0; i < save.MapIdToInternalMapIndexTable.Length; i++)
        {
            save.MapIdToInternalMapIndexTable[i] = (ushort)i;
        }

        if (!atMaximum)
        {
            save.WeaponId = -1; // the domain's lowest value; every other field at 0.
            return save;
        }

        save.GameTime = AlundraGameState.GameTimeMax;
        save.CameraTileX = 51;
        save.CameraTileY = 59;
        save.CameraTileZ = AlundraSaveGame.MaxCameraTileZ;
        save.HpMax = AlundraSaveGame.MaxHpMax;
        save.Hp = AlundraSaveGame.MaxHpMax;
        save.MpMax = AlundraSaveGame.MaxMpMax;
        save.Mp = AlundraSaveGame.MaxMpMax;
        save.Money = AlundraSaveGame.MaxMoney;
        save.WeaponId = AlundraSaveGame.MaxWeaponId;
        save.ItemId = AlundraPlayerManager.ItemsCount - 1;
        save.FalconTemp = AlundraSaveGame.MaxFalcon;
        save.Falcon = AlundraSaveGame.MaxFalcon;
        save.DeathRetryCount = byte.MaxValue;
        for (var id = 0; id < AlundraPlayerManager.ItemsCount; id++)
        {
            save.NumberOfItems[id * 2 + 1] = (short)tables.ItemsProperties[id * AlundraItemTables.ItemColumnCount + 3];
        }

        Assert.Contains(save.NumberOfItems, count => count > 1); // a real ceiling above 1 (items 36 and 61).
        return save;
    }

    private sealed class FakeScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Menu;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGUI.Core.UI.MGWindow> GetWindows() => Array.Empty<MGUI.Core.UI.MGWindow>();
    }

    private sealed class RecordingUIViewRuntime : IUIViewRuntime
    {
        public bool IsPointerOverUI => false;
        public bool IsPointerCaptured => false;
        public bool IsKeyboardCaptured => false;
        public UIViewInputState InputState => UIViewInputState.Empty;
        public bool HasModalInput => false;
        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }
        public void Draw() { }
        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;
        public void PushScreen(IUIScreen screen) { }
        public IUIScreen? PopScreen() => null;
        public void RemoveScreen(IUIScreen screen) { }
        public void Dispose() { }
    }

    /// <summary>
    /// SC5: a save at the bounds of every domain (all at the minimum, then all at the maximum) validates, is
    /// applied, and is then really drawn - the jauge opened by its own display request and ticked through every
    /// roll (money rolls 10 per tick, about 1000 ticks for 9999) with its real presenter, view model and equipment
    /// lookup; then the main inventory and the sub-inventory opened by the pad, their presenters computing from the
    /// state with the drawn flag up. No exception anywhere. A jauge caught mid-roll of the max MP is E16.d's case
    /// (SC1): this one starts from a reset jauge.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveAtTheBounds_Applied_ThenDrawnByTheJaugeAndBothInventories_WithoutException(bool atMaximum)
    {
        var root = FindProjectRoot();
        var tables = new AlundraItemTables(root);
        var rules = new AlundraSaveGameRules(root, _ => true, tables);
        var save = BoundsSave(tables, atMaximum);
        Assert.True(save.TryValidate(rules, out var error), error);

        var state = AlundraGameState.Instance;
        save.ApplyTo(state);

        // The jauge: opened by its display request, ticked with its real presenter until every roll settles.
        var hud = AlundraHudDirector.Instance;
        hud.AttachToWorld(state);
        var hudView = new AlundraHudViewModel();
        var hudPresenter = new AlundraHudPresenter(hud, hudView, () => AlundraPlayerManager.ResolveHudEquipmentIcons(state, tables));
        state.AddFlag(AlundraHudDirector.ScriptOpenRequestFlag, AlundraHudDirector.ScriptOpenRequestMask);

        var settled = false;
        for (var tick = 0; tick < 3000 && !settled; tick++)
        {
            hud.Tick();
            hudPresenter.Tick();
            settled = hud.Phase == AlundraHudDirector.HudPhase.Displayed
                && !hud.IsMoneyRolling
                && hud.Hp == save.Hp && hud.HpMax == save.HpMax
                && hud.Mp == save.Mp && hud.MpMax == save.MpMax
                && hud.Money == save.Money
                && !hud.HpDisplayPreviewIncrement && !hud.MpDisplayPreviewIncrement;
        }

        Assert.True(settled, $"the jauge did not settle: phase {hud.Phase}, hp {hud.Hp}/{hud.HpMax}, mp {hud.Mp}/{hud.MpMax}, money {hud.Money}");
        Assert.True(hud.IsDrawn);
        Assert.Equal(0, hudView.TileOverflowCount);

        // The inventories: main opened by Start, then the sub-inventory by R1, then back - the presenters compute
        // their display models from the applied state every drawn tick.
        var main = AlundraInventoryDirector.Instance;
        var sub = AlundraSubInventoryDirector.Instance;
        main.AttachToWorld(state, tables, null);
        sub.AttachToWorld(state, tables, null);
        var mainView = new AlundraInventoryViewModel(_ => new Point(16, 16));
        var subView = new AlundraSubInventoryViewModel();
        var mainPresenter = new AlundraInventoryPresenter(main, state, tables, mainView, new FakeScreen(), new RecordingUIViewRuntime());
        var subPresenter = new AlundraSubInventoryPresenter(sub, state, tables, subView, new FakeScreen(), new RecordingUIViewRuntime());

        void OneTick(uint hold)
        {
            state.TickPad.Update(hold);
            main.Tick(null);
            sub.Tick();
            AlundraInventoryPostProcess.Instance.Run();
            mainPresenter.Tick();
            subPresenter.Tick();
            hud.Tick();
            hudPresenter.Tick();
        }

        const int SettleTicks = 40; // AlundraSubInventoryPresenterTests: close slide + handoff + opening slide.

        OneTick(AlundraPadState.Start);
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0);
        }

        Assert.True(main.IsDrawn);
        Assert.True(mainPresenter.IsPushedForTests);
        var mainRenders = mainView.AppliedModelCount;
        Assert.True(mainRenders > 0);

        OneTick(AlundraPadState.R1);
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0);
        }

        Assert.True(sub.IsDrawn);
        Assert.True(subPresenter.IsPushedForTests);
        Assert.True(subView.AppliedModelCount > 0);

        OneTick(AlundraPadState.R1);
        for (var i = 0; i < SettleTicks; i++)
        {
            OneTick(0);
        }

        Assert.True(main.IsDrawn);
        Assert.True(mainView.AppliedModelCount > mainRenders);

        // The applied values are still what the save carried: drawing never clamped or rewrote them.
        Assert.Equal(save.Money, state.PlayerStats.Money);
        Assert.Equal(save.HpMax, state.PlayerStats.HpMax);
        Assert.Equal(save.MpMax, state.PlayerStats.MpMax);
        Assert.Equal(save.Falcon, state.PlayerStats.Falcon);
        Assert.Equal(save.FalconTemp, state.PlayerStats.FalconTemp);
    }
}
