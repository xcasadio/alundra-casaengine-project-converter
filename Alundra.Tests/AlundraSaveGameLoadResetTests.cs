#nullable enable
using System;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.d T1 (docs/plan-e16-etat-partie.md, K8 steps 1, 3 and 4, D-E16-19, SC1, SC7): the session resets a
/// load runs on the arrival map - <see cref="AlundraGameState.ResetSessionForLoad"/>,
/// <see cref="AlundraHudDirector.ResetDisplayForLoad"/> and the <c>ResetSessionForLoad</c> of both inventory
/// directors, the post-process and the portrait. Nothing here reads or writes a save file (D-E16-31).
/// </summary>
public sealed class AlundraSaveGameLoadResetTests : IDisposable
{
    public AlundraSaveGameLoadResetTests()
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
        AlundraWarpDirector.Instance.ResetForTests();
    }

    // ---- AlundraGameState.ResetSessionForLoad (K8 step 1) ------------------------------------------------

    [Fact]
    public void GameState_ResetSessionForLoad_ResetsEverySessionField_AndKeepsEveryFieldTheSaveCarries()
    {
        var state = new AlundraGameState();

        // Session fields, all away from their start-of-session value.
        state.PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked | AlundraGameState.PlayerControlBits.MenuOpen;
        state.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Start };
        state.TickPad.Update(AlundraPadState.Start);
        Assert.NotEqual(0u, state.TickPad.ButtonsHold);
        state.InteractLatchEntity = new AlundraEntityScriptProxy();
        state.InteractLatchFacing = 1;
        state.InteractLatchEntityX = 2;
        state.InteractLatchEntityY = 3;
        state.InteractLatchEntityZ = 4;
        state.InteractLatchPlayerX = 5;
        state.InteractLatchPlayerY = 6;
        state.InteractLatchPlayerZ = 7;
        state.InteractLatchDirection = 8;
        state.IsWarpDisabled = true;
        state.TextCategoryIndex = 3;
        state.GameVariables[0] = 11;
        state.GameVariables[3] = -4;
        state.NewGameInventoryInitialized = false;

        // Fields the save carries (C2), and the session-only flag bank the map entry owns.
        state.GameFlags[0] = 0x1234;
        state.GameFlags[63] = 0x8000_0001;
        state.GameFlags[100] = 0x55;
        state.MapIdToInternalMapIndexTable[5] = 390;
        state.NumberOfItems[3] = 2;
        state.PlayerStats.Hp = 7;
        state.PlayerStats.HpMax = 20;
        state.PlayerStats.Mp = 1;
        state.PlayerStats.MpMax = 2;
        state.PlayerStats.Money = 123;
        state.PlayerStats.WeaponId = 3;
        state.PlayerStats.ItemId = 17;
        state.PlayerStats.FalconTemp = 4;
        state.PlayerStats.Falcon = 5;
        state.GameTime = 9999;
        state.DeathRetryCount = 6;
        state.TemporaryFlags[2] = 0x77;

        state.ResetSessionForLoad();

        Assert.Equal(0u, state.PlayerControlFlags);
        Assert.Equal(default, state.LastPadState);
        Assert.Equal((0u, 0u), (state.TickPad.ButtonsHold, state.TickPad.ButtonsJustPressed));
        Assert.Null(state.InteractLatchEntity);
        Assert.Equal(
            (0, 0, 0, 0, 0, 0, 0, 0u),
            (state.InteractLatchFacing, state.InteractLatchEntityX, state.InteractLatchEntityY, state.InteractLatchEntityZ,
                state.InteractLatchPlayerX, state.InteractLatchPlayerY, state.InteractLatchPlayerZ, state.InteractLatchDirection));
        Assert.False(state.IsWarpDisabled);
        Assert.Equal(0, state.TextCategoryIndex); // D-E16-19.
        Assert.All(state.GameVariables, value => Assert.Equal(0, value)); // D-E16-19.
        Assert.True(state.NewGameInventoryInitialized); // SC7: TRUE after a load, unlike a New Game.

        Assert.Equal(0x1234u, state.GameFlags[0]);
        Assert.Equal(0x8000_0001u, state.GameFlags[63]);
        Assert.Equal(0x55u, state.GameFlags[100]);
        Assert.Equal(390, state.MapIdToInternalMapIndexTable[5]);
        Assert.Equal(2, state.NumberOfItems[3]);
        Assert.Equal(
            (7, 20, 1, 2, 123, 3, 17, 4, 5),
            (state.PlayerStats.Hp, state.PlayerStats.HpMax, state.PlayerStats.Mp, state.PlayerStats.MpMax, state.PlayerStats.Money,
                state.PlayerStats.WeaponId, state.PlayerStats.ItemId, state.PlayerStats.FalconTemp, state.PlayerStats.Falcon));
        Assert.Equal(9999u, state.GameTime);
        Assert.Equal(6, state.DeathRetryCount);
        Assert.Equal(0x77u, state.TemporaryFlags[2]); // the map entry's own job (InstallForMapEntry), not this one.
    }

    // ---- AlundraHudDirector.ResetDisplayForLoad (K8 step 3, SC1) --------------------------------------------

    private static void TickHud(AlundraHudDirector hud, AlundraHudPresenter presenter, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            hud.Tick();
            presenter.Tick();
        }
    }

    /// <summary>
    /// SC1: a jauge caught mid-catch-up of the MP max (displayed 3, true 4, <c>_mpMaxSubStep</c> non-zero) and
    /// closed instantly (the script's "hide now" request), then a load of a save at 4 MP out of 4: after
    /// <see cref="AlundraHudDirector.ResetDisplayForLoad"/>, the jauge reopened by its display request and ticked,
    /// with its real presenter and composer, until every roll settles - no exception. Without the reset the preview
    /// stays on with an MP max of 4, the path SC1 names.
    /// </summary>
    [Fact]
    public void Hud_CaughtMidMpMaxCatchUp_ThenLoadAtFourOfFour_ReopensAndSettles_WithoutException()
    {
        var state = new AlundraGameState();
        state.PlayerStats.MpMax = 3;
        state.PlayerStats.Mp = 3;
        var hud = AlundraHudDirector.Instance;
        hud.AttachToWorld(state);
        var view = new AlundraHudViewModel();
        var presenter = new AlundraHudPresenter(hud, view);

        // Open the jauge and let the MP roll up to 3 out of 3.
        state.AddFlag(AlundraHudDirector.ScriptOpenRequestFlag, AlundraHudDirector.ScriptOpenRequestMask);
        for (var i = 0; i < 200 && !(hud.Phase == AlundraHudDirector.HudPhase.Displayed && hud.Mp == 3 && !hud.MpDisplayPreviewIncrement); i++)
        {
            TickHud(hud, presenter, 1);
        }

        Assert.Equal((3, 3), (hud.Mp, hud.MpMax));

        // The true MP max rises to 4: the max catch-up starts its sub-step cycle.
        state.PlayerStats.MpMax = 4;
        for (var i = 0; i < 10 && hud.SubStepsForTests.MpMax == 0; i++)
        {
            TickHud(hud, presenter, 1);
        }

        Assert.NotEqual(0, hud.SubStepsForTests.MpMax);
        Assert.Equal(3, hud.MpMax);

        // The script's "hide now" request: the jauge goes idle at once, the sub-step frozen mid-cycle.
        state.AddFlag(1814, 0x400000);
        TickHud(hud, presenter, 1);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, hud.Phase);
        Assert.NotEqual(0, hud.SubStepsForTests.MpMax);
        Assert.True(hud.MpDisplayPreviewIncrement);

        // The load: stats at 4 MP out of 4, then the jauge's reset with those stats.
        state.PlayerStats.Mp = 4;
        state.PlayerStats.MpMax = 4;
        hud.ResetDisplayForLoad(state.PlayerStats);

        Assert.Equal((0, 0, 0, 0), hud.SubStepsForTests);
        Assert.False(hud.MpDisplayPreviewIncrement);
        Assert.Equal((4, 4), (hud.Mp, hud.MpMax));

        // Reopened and ticked until every roll settles, through the real presenter.
        state.AddFlag(AlundraHudDirector.ScriptOpenRequestFlag, AlundraHudDirector.ScriptOpenRequestMask);
        var settled = false;
        for (var tick = 0; tick < 500 && !settled; tick++)
        {
            TickHud(hud, presenter, 1);
            settled = hud.Phase == AlundraHudDirector.HudPhase.Displayed
                && !hud.IsMoneyRolling
                && hud.Mp == 4 && hud.MpMax == 4
                && !hud.HpDisplayPreviewIncrement && !hud.MpDisplayPreviewIncrement;
        }

        Assert.True(settled, $"the jauge did not settle: phase {hud.Phase}, mp {hud.Mp}/{hud.MpMax}");
        Assert.True(hud.IsDrawn);
        Assert.Equal(0, view.TileOverflowCount);
    }

    [Fact]
    public void Hud_ResetDisplayForLoad_TakesTheLoadedValues_AndKeepsPhaseYFrameCounterAndTheAttachedState()
    {
        var state = new AlundraGameState();
        var hud = AlundraHudDirector.Instance;
        hud.AttachToWorld(state);
        state.PlayerStats.Money = 500;
        state.AddFlag(AlundraHudDirector.ScriptOpenRequestFlag, AlundraHudDirector.ScriptOpenRequestMask);
        for (var i = 0; i < 25; i++)
        {
            hud.Tick(); // opened, displayed, money still rolling up (10 per tick).
        }

        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, hud.Phase);
        Assert.True(hud.IsMoneyRolling);
        var (phase, y, frameCounter) = (hud.Phase, hud.Y, hud.FrameCounter);

        var loaded = new AlundraPlayerStats { Hp = 12, HpMax = 30, Mp = 1, MpMax = 2, Money = 42 };
        hud.ResetDisplayForLoad(loaded);

        Assert.Equal((12, 30, 1, 2, 42), (hud.Hp, hud.HpMax, hud.Mp, hud.MpMax, hud.Money));
        Assert.Equal((0, 0, 0, 0), hud.SubStepsForTests);
        Assert.Equal(0, hud.CoinIconFrame);
        Assert.False(hud.IsMoneyRolling);
        Assert.All(hud.MagicPipFrame, frame => Assert.Equal(0, frame));

        Assert.Equal((phase, y, frameCounter), (hud.Phase, hud.Y, hud.FrameCounter));
        state.PlayerStats.HpMax = 44;
        Assert.Equal(44, hud.TrueHpMax); // still reads the attached state, not the displayed fallback.
    }

    // ---- Inventories, post-process and portrait (K8 step 4) ------------------------------------------------

    private static void InventoryTick(AlundraGameState state, uint hold)
    {
        state.TickPad.Update(hold);
        AlundraInventoryDirector.Instance.Tick(null);
        AlundraSubInventoryDirector.Instance.Tick();
        AlundraInventoryPostProcess.Instance.Run();
        AlundraInventoryPortrait.Instance.Step();
    }

    private static void InventoryTicks(AlundraGameState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            InventoryTick(state, 0);
        }
    }

    private static void ResetAllInventoriesForLoad()
    {
        AlundraInventoryDirector.Instance.ResetSessionForLoad();
        AlundraSubInventoryDirector.Instance.ResetSessionForLoad();
        AlundraInventoryPostProcess.Instance.ResetSessionForLoad();
        AlundraInventoryPortrait.Instance.ResetSessionForLoad();
    }

    [Fact]
    public void Inventories_ResetSessionForLoad_CloseEverything_AndKeepTheAttachments()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameInventory(state, tables);
        var main = AlundraInventoryDirector.Instance;
        var sub = AlundraSubInventoryDirector.Instance;
        main.AttachToWorld(state, tables, null);
        sub.AttachToWorld(state, tables, null);

        // The main inventory open and settled, portrait at rest.
        InventoryTick(state, AlundraPadState.Start);
        InventoryTicks(state, 20);
        Assert.True(main.IsDrawn);
        Assert.NotEqual(AlundraInventoryPortrait.StateIdle, AlundraInventoryPortrait.Instance.State);

        ResetAllInventoriesForLoad();

        Assert.False(main.IsActive);
        Assert.False(main.IsDrawn);
        Assert.False(main.IsCallbackArmed);
        Assert.Equal(0, main.SelectedSlotId);
        Assert.False(sub.IsActive);
        Assert.Equal(0, AlundraInventoryPostProcess.Instance.State);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, AlundraInventoryPortrait.Instance.State);
        Assert.False(AlundraInventoryPortrait.Instance.IsVisible);

        // The attachments survived: Start opens the main inventory again, then R1 hands over to the
        // sub-inventory (both degrade to no-ops without their attached state and tables).
        state.PlayerControlFlags = 0;
        InventoryTick(state, 0);
        InventoryTick(state, AlundraPadState.Start);
        InventoryTicks(state, 20);
        Assert.True(main.IsDrawn);

        InventoryTick(state, AlundraPadState.R1);
        InventoryTicks(state, 40);
        Assert.True(sub.IsDrawn);

        // Reset again with the SUB-inventory open: it closes too, and still reopens afterwards.
        ResetAllInventoriesForLoad();
        Assert.False(sub.IsActive);
        Assert.False(main.IsActive);
        Assert.Equal(0, sub.SelectedPosition);

        state.PlayerControlFlags = 0;
        InventoryTick(state, 0);
        InventoryTick(state, AlundraPadState.Start);
        InventoryTicks(state, 20);
        InventoryTick(state, AlundraPadState.R1);
        InventoryTicks(state, 40);
        Assert.True(sub.IsDrawn);
    }

    [Fact]
    public void PostProcessAndPortrait_ResetSessionForLoad_GoBackToIdle()
    {
        AlundraInventoryPostProcess.Instance.State = 2;
        AlundraInventoryPortrait.Instance.Start(10, 20);
        AlundraInventoryPortrait.Instance.Step();
        Assert.True(AlundraInventoryPortrait.Instance.State != AlundraInventoryPortrait.StateIdle);

        AlundraInventoryPostProcess.Instance.ResetSessionForLoad();
        AlundraInventoryPortrait.Instance.ResetSessionForLoad();

        Assert.Equal(0, AlundraInventoryPostProcess.Instance.State);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, AlundraInventoryPortrait.Instance.State);
        Assert.Equal((0, 0, 0, 0), (AlundraInventoryPortrait.Instance.X, AlundraInventoryPortrait.Instance.Y,
            AlundraInventoryPortrait.Instance.DrawnWidth, AlundraInventoryPortrait.Instance.DrawnHeight));

        // A new flight starts normally afterwards.
        AlundraInventoryPortrait.Instance.Start(10, 20);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, AlundraInventoryPortrait.Instance.State);
    }
}
