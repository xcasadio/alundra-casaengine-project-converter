#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.d D4 acceptance (docs/plan-e13d-inventaire.md) - drives <see cref="AlundraInventoryDirector"/> the
/// way <see cref="AlundraHudDirectorTests"/> drives <see cref="AlundraHudDirector"/>: a tick is
/// <c>state.TickPad.Update(hold)</c> then <c>director.Tick(player)</c>, isolated <see cref="AlundraGameState"/>
/// instances for every test but the last (which drives the real <see cref="AlundraWorldProxy"/>).
/// </summary>
public sealed class AlundraInventoryDirectorTests : IDisposable
{
    private readonly string? _previousProjectPath;

    public AlundraInventoryDirectorTests()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        _previousProjectPath = EngineEnvironment.ProjectPath;
    }

    public void Dispose()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        EngineEnvironment.ProjectPath = _previousProjectPath!;
    }

    // -----------------------------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------------------------

    private sealed class RecordingSoundPlayer : IAlundraSoundPlayer
    {
        public readonly List<int> Requests = new();
        public void PlaySfx(int sfxId) => Requests.Add(sfxId);
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() { }
    }

    /// <summary>Writes a synthetic <c>Dialogues/etc-index.json</c>/<c>global-strings.json</c> pair
    /// resolving item <paramref name="itemId"/>'s name and two description lines - same JSON shape
    /// <see cref="AlundraEtcStringTable"/> reads (<c>int[]</c>/<c>Dictionary&lt;string,string&gt;</c>).
    /// Returns the temp project path; the caller deletes it.</summary>
    private static string WriteEtcFixture(int itemId, string name, string desc0, string desc1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraInventoryDirectorEtcFixture_" + Guid.NewGuid());
        var dialoguesPath = Path.Combine(projectPath, "Dialogues");
        Directory.CreateDirectory(dialoguesPath);

        var etcIndex = new int[1024];
        Array.Fill(etcIndex, -1);
        etcIndex[itemId + 0x200] = 100;
        etcIndex[itemId + 0x280] = 101;
        etcIndex[itemId + 0x300] = 102;

        File.WriteAllText(Path.Combine(dialoguesPath, "etc-index.json"), "[" + string.Join(",", etcIndex) + "]");
        File.WriteAllText(
            Path.Combine(dialoguesPath, "global-strings.json"),
            $"{{\"100\":\"{name}\",\"101\":\"{desc0}\",\"102\":\"{desc1}\"}}");

        return projectPath;
    }

    private static AlundraGameState NewState() => new();

    private static AlundraInventoryDirector NewDirector(AlundraGameState state, AlundraItemTables? tables = null, IAlundraSoundPlayer? sound = null)
    {
        var director = AlundraInventoryDirector.Instance;
        director.AttachToWorld(state, tables ?? ItemTablesFixture.LoadReal(), sound);
        return director;
    }

    private static void Tick(AlundraGameState state, AlundraInventoryDirector director, uint hold, AlundraEntityScriptProxy? player = null)
    {
        state.TickPad.Update(hold);
        director.Tick(player);
    }

    /// <summary>Ticks (no input) until <paramref name="predicate"/> holds, up to <paramref name="maxTicks"/> -
    /// used for the text-reveal milestones whose exact tick count is secondary to the state reached.</summary>
    private static void TickUntil(AlundraGameState state, AlundraInventoryDirector director, Func<bool> predicate, int maxTicks = 300)
    {
        for (var i = 0; i < maxTicks && !predicate(); i++)
        {
            Tick(state, director, 0);
        }

        Assert.True(predicate(), "TickUntil: predicate never became true within maxTicks.");
    }

    /// <summary>Opens the inventory (one trigger tick) and drives it through the full 18-call opening
    /// slide (this class' own <see cref="OpeningOrder_SoundHudFlagsAndBoxSlide_MatchTheTracedSeventeenTickShape"/>
    /// documents why 18, the same "mode == 0 on entry, two calls after the target is reached" shape
    /// <see cref="AlundraHudDirector"/>'s own tween has) so the caller starts from the settled,
    /// input-accepting state.</summary>
    private static void OpenAndSettle(AlundraGameState state, AlundraInventoryDirector director)
    {
        Tick(state, director, AlundraPadState.Start); // trigger + DisplayInventory + FUN_80054f1c, same tick.
        for (var i = 0; i < 18; i++)
        {
            Tick(state, director, 0); // 15 interpolating calls + 2 snap calls + 1 completion-detecting call.
        }
    }

    // -----------------------------------------------------------------------------------------
    // Trigger
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData((uint)0x0800u)] // Start
    [InlineData((uint)0x0001u)] // L2
    [InlineData((uint)0x0002u)] // R2
    public void Trigger_FiresOnStartL2R2Edges(uint button)
    {
        var state = NewState();
        var director = NewDirector(state);

        Tick(state, director, button);

        Assert.True(director.IsActive);
        Assert.Equal(5u, director.ForbiddenWarpFlag);
    }

    [Fact]
    public void Trigger_DoesNotFire_WhileTheButtonIsAlreadyHeld_NoEdge()
    {
        var state = NewState();
        var director = NewDirector(state);

        // ButtonsJustPressed is only an EDGE - simulate "already held before this test starts" by
        // priming ButtonsHold first, THEN ticking with the same mask (no change -> no edge).
        state.TickPad.Update(AlundraPadState.Start);
        Assert.False(director.IsActive);

        Tick(state, director, AlundraPadState.Start); // same mask again - no edge, ButtonsJustPressed == 0.

        Assert.False(director.IsActive);
    }

    [Fact]
    public void Trigger_Refused_WhenPlayerControlFlagsNonZero()
    {
        var state = NewState();
        state.PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked;
        var director = NewDirector(state);

        Tick(state, director, AlundraPadState.Start);

        Assert.False(director.IsActive);
    }

    [Fact]
    public void Trigger_Refused_WhenPlayerIsBlockedByEntity()
    {
        var state = NewState();
        var director = NewDirector(state);
        var player = new AlundraEntityScriptProxy { BlockedByEntity = new CasaEngine.Framework.Scene.Entities.Entity() };

        state.TickPad.Update(AlundraPadState.Start);
        director.Tick(player);

        Assert.False(director.IsActive);
    }

    [Fact]
    public void Trigger_Refused_WhenSelectIsHeld()
    {
        var state = NewState();
        var director = NewDirector(state);

        Tick(state, director, AlundraPadState.Start | AlundraPadState.Select);

        Assert.False(director.IsActive);
    }

    [Fact]
    public void Trigger_Refused_WhileADialogueBoxIsOpen()
    {
        var state = NewState();
        var director = NewDirector(state);
        AlundraDialogueDirector.Instance.AttachToWorld(null, state); // no view needed - IsOpen flips regardless (Open's own doc).
        AlundraDialogueDirector.Instance.Open("hello", 0);
        Assert.True(AlundraDialogueDirector.Instance.IsOpen);

        Tick(state, director, AlundraPadState.Start);

        Assert.False(director.IsActive);
    }

    // -----------------------------------------------------------------------------------------
    // Opening order
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void OpeningOrder_SoundHudFlagsAndBoxSlide_MatchTheTracedSeventeenTickShape()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);

        // Setup tick: DisplayInventory (HUD ArmDisappearance + sound 4) then FUN_80054f1c (flag 5, MenuOpen,
        // boxes armed but NOT yet moved from wherever they were - construction default is already origin).
        Tick(state, director, AlundraPadState.Start);

        Assert.Equal(new[] { 4 }, sound.Requests);
        Assert.Equal(5u, director.ForbiddenWarpFlag);
        Assert.True((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, AlundraHudDirector.Instance.Phase); // ArmDisappearance's own guard (Phase&3==1) is false while Idle - never armed from a cold Idle HUD.
        Assert.Equal((8, 16), director.BoxPosition(0)); // weapon box - untouched this same tick.

        // First slide tick (tween.Tick == 0 -> interpolate at t=0 -> box snaps to its off-screen SOURCE).
        Tick(state, director, 0);
        Assert.Equal((-169, 16), director.BoxPosition(0)); // ~(0x15<<3) = ~168 = -169.
        Assert.Equal((0x140, 16), director.BoxPosition(2)); // weapon name box - slides in from the right.
        Assert.Equal((16, 0xf0), director.BoxPosition(6)); // description box - slides in from below.

        // No input is read while sliding (ForbiddenWarpFlag & 6 != 0): hold Down for the rest of the
        // slide - if the director ever read it, SelectedSlotId would move; it must not, until settled.
        // 17 more calls (18 total slide calls): 14 more interpolating + 2 snap + the completion-detecting call.
        for (var i = 0; i < 17; i++)
        {
            Tick(state, director, AlundraPadState.Down);
        }

        Assert.Equal(0, director.SelectedSlotId);
        Assert.Equal(1u, director.ForbiddenWarpFlag); // bit 2 (SlideOpenBit) dropped once the tween settled.
        Assert.Equal((8, 16), director.BoxPosition(0)); // back at origin.
        Assert.Equal((176, 16), director.BoxPosition(2));
        Assert.Equal((16, 168), director.BoxPosition(6));
        Assert.True(director.IsActive);

        // Now settled: input is read again - release then a FRESH press (Down was already held
        // throughout the slide above, so its own edge was already consumed while sliding, ignored).
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.Down);
        Assert.Equal(6, director.SelectedSlotId);
    }

    // -----------------------------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Navigation_DownWrapsColumnFromLastRowToFirst_PlaysSound1_ResetsText()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);

        // Reach row 3 (slot 18, column 0) by three Down presses (needs three separate press EDGES).
        for (var i = 0; i < 3; i++)
        {
            Tick(state, director, AlundraPadState.Down);
            Tick(state, director, 0); // release, so the next press is a fresh edge.
        }
        Assert.Equal(18, director.SelectedSlotId);

        Tick(state, director, AlundraPadState.Down); // wraps 18+6=24 > 0x17 -> 18-0x12=0.
        Assert.Equal(0, director.SelectedSlotId);
        Assert.Contains(1, sound.Requests);
    }

    [Fact]
    public void Navigation_UpFromTopRowWrapsToBottomRow()
    {
        var state = NewState();
        var director = NewDirector(state);
        OpenAndSettle(state, director);

        Tick(state, director, AlundraPadState.Up); // 0-6 = -6 < 0 -> 0+0x12=18.

        Assert.Equal(18, director.SelectedSlotId);
    }

    [Fact]
    public void Navigation_RightFromLastColumnWrapsToFirstColumn()
    {
        var state = NewState();
        var director = NewDirector(state);
        OpenAndSettle(state, director);

        for (var i = 0; i < 5; i++)
        {
            Tick(state, director, AlundraPadState.Right);
            Tick(state, director, 0);
        }
        Assert.Equal(5, director.SelectedSlotId);

        Tick(state, director, AlundraPadState.Right); // 5+1=6, 6==6/6*6 -> 5-5=0.
        Assert.Equal(0, director.SelectedSlotId);
    }

    [Fact]
    public void Navigation_LeftFromFirstColumnWrapsToLastColumn()
    {
        var state = NewState();
        var director = NewDirector(state);
        OpenAndSettle(state, director);

        Tick(state, director, AlundraPadState.Left); // 0 == 0/6*6 -> 0+5=5.

        Assert.Equal(5, director.SelectedSlotId);
    }

    [Fact]
    public void Navigation_ResetsTextRevealState()
    {
        var state = NewState();
        var etcPath = WriteEtcFixture(0x24, "Herb", "d0", "d1");
        EngineEnvironment.ProjectPath = etcPath;
        try
        {
            var director = NewDirector(state);
            OpenAndSettle(state, director);
            Tick(state, director, AlundraPadState.Down); // select slot 6 (fixed item 0x24, owned below).
            Tick(state, director, 0);

            state.NumberOfItems[0x24 * 2 + 1] = 1; // own it, so the text machine actually advances.
            Tick(state, director, 0); // state 0 -> 1.
            Assert.Equal(1, director.TextRevealState);

            Tick(state, director, AlundraPadState.Right); // moves off slot 6 and back is irrelevant - only the reset matters.
            Assert.Equal(0, director.TextRevealState);
        }
        finally
        {
            Directory.Delete(etcPath, recursive: true);
        }
    }

    [Fact]
    public void Navigation_HoldingADirection_MovesOnce_ThenSilentTwentyTicks_ThenRepeatsEveryTick()
    {
        var state = NewState();
        var director = NewDirector(state);
        OpenAndSettle(state, director);

        Tick(state, director, AlundraPadState.Down); // edge - one move.
        Assert.Equal(6, director.SelectedSlotId);

        // AlundraTickPad.Update: the edge tick above took the "ButtonsHold == 0 before" branch (does not
        // count toward NumberOfFrameHold at all); the NEXT 20 held ticks each see NumberOfFrameHold < 20
        // and stay silent (NumberOfFrameHold reaches 20 on the 20th) - only the 21st held tick finds
        // NumberOfFrameHold == MaxNbFrameHeld and repeats.
        for (var i = 0; i < 20; i++)
        {
            Tick(state, director, AlundraPadState.Down); // held - silent for MaxNbFrameHeld (20) ticks total.
        }
        Assert.Equal(6, director.SelectedSlotId);

        Tick(state, director, AlundraPadState.Down); // 21st held tick - RepeatInterval == 0 -> repeats now.
        Assert.Equal(12, director.SelectedSlotId);

        Tick(state, director, AlundraPadState.Down); // RepeatInterval == 0 -> repeats every tick after that too.
        Assert.Equal(18, director.SelectedSlotId);
    }

    // -----------------------------------------------------------------------------------------
    // Equip weapon (FUN_8005795c)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EquipWeapon_ValidSlot_PlaysSound2_AndSetsTheWeaponId()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var tables = ItemTablesFixture.LoadReal(); // items 1..4 all in weapon slot 1, priority 0..3.
        var director = NewDirector(state, tables, sound);
        OpenAndSettle(state, director);

        state.NumberOfItems[4 * 2 + 1] = 1; // own item 4 - the highest-priority owned match for slot 1.
        Tick(state, director, AlundraPadState.Cross); // grid slot 0 -> weapon slot 1.

        Assert.Contains(2, sound.Requests);
        Assert.Equal(1, state.PlayerStats.WeaponId);
    }

    [Fact]
    public void EquipWeapon_AlreadyEquipped_Silent()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var tables = ItemTablesFixture.LoadReal();
        var director = NewDirector(state, tables, sound);
        OpenAndSettle(state, director);

        state.NumberOfItems[4 * 2 + 1] = 1;
        AlundraPlayerManager.SetPlayerWeaponId(state, tables, 1); // already equipped through this same slot.
        sound.Requests.Clear();

        Tick(state, director, AlundraPadState.Cross);

        Assert.Empty(sound.Requests);
    }

    [Fact]
    public void EquipWeapon_EmptySlot_PlaysSound3()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var tables = ItemTablesFixture.LoadReal(); // weapon slots 2..5 have no rows at all - always NoItem.
        var director = NewDirector(state, tables, sound);
        OpenAndSettle(state, director);

        // Equip slot 0 first (item 4, real): MainInventoryManager.cs:1639-1641 compares the EMPTY slot's
        // NoItem against the CURRENTLY EQUIPPED weapon's item - with nothing equipped yet (WeaponId 0),
        // both sides are NoItem and the original's own comparison reads "already equipped", not "empty"
        // (ported faithfully - ActuallyAlreadyEquipped's own quirk). A real current weapon avoids that.
        state.NumberOfItems[4 * 2 + 1] = 1;
        Tick(state, director, AlundraPadState.Cross); // grid slot 0 -> weapon slot 1, equips item 4.
        sound.Requests.Clear();

        Tick(state, director, AlundraPadState.Right); // grid slot 1 -> weapon slot 3 (empty in the fixture).
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.Cross);

        Assert.Contains(3, sound.Requests);
        Assert.DoesNotContain(2, sound.Requests);
    }

    // -----------------------------------------------------------------------------------------
    // Equip item (FUN_80057854) - the fixed-item slots (g_ItemIdBySlotIndex != -1) need no tables data.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EquipItem_FixedSlotOwned_SetsCurrentItemId_PlaysSound2()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);
        state.NumberOfItems[0x24 * 2 + 1] = 1; // slot 6 = item 0x24 (herbs).

        Tick(state, director, AlundraPadState.Down); // grid slot 6.
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.Cross);

        Assert.Contains(2, sound.Requests);
        Assert.Equal(0x24, state.PlayerStats.ItemId);
    }

    [Fact]
    public void EquipItem_FixedSlotUnowned_PlaysSound3()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);
        // slot 7 = item 0x29, count left at 0 (unowned).

        Tick(state, director, AlundraPadState.Down);
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.Right);
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.Cross);

        Assert.Contains(3, sound.Requests);
        Assert.DoesNotContain(2, sound.Requests);
    }

    [Fact]
    public void EquipItem_AlreadyEquipped_Silent()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);
        state.NumberOfItems[0x24 * 2 + 1] = 1;
        state.PlayerStats.ItemId = 0x24; // already equipped.

        Tick(state, director, AlundraPadState.Down);
        Tick(state, director, 0);
        sound.Requests.Clear(); // drop the navigation's own sound 1.
        Tick(state, director, AlundraPadState.Cross);

        Assert.Empty(sound.Requests);
    }

    // -----------------------------------------------------------------------------------------
    // Closing
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Closing_PlaysSound5_ReverseSlides_ClearsMenuOpenOnlyAfterTheSlide_ResetsFlagToZero()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);
        Assert.Equal((8, 16), director.BoxPosition(0));

        Tick(state, director, AlundraPadState.Start); // Start/L2/R2 while settled -> RunCloseSetup.

        Assert.Contains(5, sound.Requests);
        Assert.Equal(3u, director.ForbiddenWarpFlag); // bit0 (residual) | bit1 (SlideCloseBit).
        Assert.True((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0); // still open mid-slide.

        Tick(state, director, 0); // first close-slide tick - box snaps to its off-screen TARGET-bound source (== origin here).
        Assert.Equal((8, 16), director.BoxPosition(0)); // source for box0/1 on close IS the origin (unmoved this tick).

        for (var i = 0; i < 17; i++)
        {
            Tick(state, director, 0);
        }

        Assert.Equal(0u, director.ForbiddenWarpFlag);
        Assert.False(director.IsActive);
        Assert.False((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
        // MainInventoryManager.cs:881-894 - the close-completion branch snaps every box back to its
        // OWN origin, even though it was just at its off-screen close target: matches the original,
        // which ports the same "snap all boxes to originX/Y" regardless of the box's live position.
        Assert.Equal((8, 16), director.BoxPosition(0));
    }

    [Fact]
    public void Closing_ArmsHudAppearance_OnlyWhenThePersistentLatchIsSet()
    {
        var state = NewState();
        var director = NewDirector(state);
        AlundraHudDirector.Instance.AttachToWorld(state);
        OpenAndSettle(state, director);

        // Latch NOT set - InitializeHudPositionBeforeHide must be a no-op (HudManager.cs:42-57's own guard).
        Tick(state, director, AlundraPadState.Start);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, AlundraHudDirector.Instance.Phase);
        for (var i = 0; i < 18; i++) // let the close slide finish (clears MenuOpen) before re-opening.
        {
            Tick(state, director, 0);
        }
        Assert.False(director.IsActive);

        // Re-open, close again, this time WITH the latch set.
        var director2 = NewDirector(state);
        OpenAndSettle(state, director2);
        state.AddFlag(1662, 0x40000000); // the persistent latch - see AlundraHudDirector's own doc.

        Tick(state, director2, AlundraPadState.Start);

        Assert.Equal(AlundraHudDirector.HudPhase.Opening, AlundraHudDirector.Instance.Phase);
    }

    [Fact]
    public void SubInventoryShoulderButtons_Ignored_NoStateChange()
    {
        var state = NewState();
        var sound = new RecordingSoundPlayer();
        var director = NewDirector(state, sound: sound);
        OpenAndSettle(state, director);
        sound.Requests.Clear(); // drop the opening's own sound 4.
        var flagBefore = director.ForbiddenWarpFlag;
        var slotBefore = director.SelectedSlotId;

        Tick(state, director, AlundraPadState.L1);
        Tick(state, director, 0);
        Tick(state, director, AlundraPadState.R1);

        Assert.Equal(flagBefore, director.ForbiddenWarpFlag);
        Assert.Equal(slotBefore, director.SelectedSlotId);
        Assert.True(director.IsActive); // never closed.
        Assert.Empty(sound.Requests);
    }

    // -----------------------------------------------------------------------------------------
    // Text reveal
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void TextReveal_OneCharacterEveryThreeTicks_ThenHolds_ThenBothDescriptionLines()
    {
        var etcPath = WriteEtcFixture(0x24, "Ab", "Cd", "Ef");
        EngineEnvironment.ProjectPath = etcPath;
        try
        {
            var state = NewState();
            var director = NewDirector(state);
            OpenAndSettle(state, director);
            state.NumberOfItems[0x24 * 2 + 1] = 1;
            Tick(state, director, AlundraPadState.Down); // select slot 6 (item 0x24) - the tail this
            Tick(state, director, 0);                    // same tick, and the release tick, already run
                                                           // the state-0 setup AND the first char's reveal
                                                           // (its own countdown starts at 0 - commits with
                                                           // no delay, MainInventoryManager.cs:962/1147).
            Assert.Equal(2, director.TextRevealState);
            Assert.Equal("A", director.NameVisiblePrefix);

            // The SECOND character is delayed the full 3 ticks (countdown reset to 2 after the first
            // commit) - MainInventoryManager.cs:1147, "one character every 3 ticks" from here on.
            Tick(state, director, 0);
            Tick(state, director, 0);
            Assert.Equal(2, director.TextRevealState); // not yet committed.
            Tick(state, director, 0);
            Assert.Equal(3, director.TextRevealState);
            Assert.Equal("Ab", director.NameVisiblePrefix);

            TickUntil(state, director, () => director.TextRevealState == 0x11); // name fully revealed -> hold.
            TickUntil(state, director, () => director.TextRevealState == 0x4d); // hold elapsed -> line 0 setup.
            Assert.Equal(string.Empty, director.Description0VisiblePrefix);

            TickUntil(state, director, () => director.Description0VisiblePrefix == "Cd");
            TickUntil(state, director, () => director.TextRevealState == 0x8e); // line 0 done -> line 1 setup.
            Assert.Equal(string.Empty, director.Description1VisiblePrefix);

            TickUntil(state, director, () => director.Description1VisiblePrefix == "Ef");
            TickUntil(state, director, () => director.TextRevealState == 0xcf); // both lines complete.

            Tick(state, director, 0); // held forever once done.
            Assert.Equal(0xcf, director.TextRevealState);
        }
        finally
        {
            Directory.Delete(etcPath, recursive: true);
        }
    }

    /// <summary>
    /// Closing verifier's F1 (P1, reproduced on the real export): global-strings.json holds a JSON null for
    /// every offset with no text - the base dagger's second description line among them - and the original
    /// reads it as an empty string (MainInventoryManager.cs:972/:1010/:1041, "?? string.Empty"). The reveal
    /// must run through to 0xcf without throwing, the empty lines staying empty.
    /// </summary>
    [Fact]
    public void TextReveal_NullDescriptionLines_ReadAsEmpty_RunToTheEndWithoutThrowing()
    {
        var etcPath = Path.Combine(Path.GetTempPath(), "AlundraInventoryDirectorEtcFixture_" + Guid.NewGuid());
        var dialoguesPath = Path.Combine(etcPath, "Dialogues");
        Directory.CreateDirectory(dialoguesPath);
        var etcIndex = new int[1024];
        Array.Fill(etcIndex, -1);
        etcIndex[0x24 + 0x200] = 100;
        etcIndex[0x24 + 0x280] = 101;
        etcIndex[0x24 + 0x300] = 102;
        File.WriteAllText(Path.Combine(dialoguesPath, "etc-index.json"), "[" + string.Join(",", etcIndex) + "]");
        File.WriteAllText(Path.Combine(dialoguesPath, "global-strings.json"), "{\"100\":\"Ab\",\"101\":null,\"102\":null}");
        EngineEnvironment.ProjectPath = etcPath;
        try
        {
            var state = NewState();
            var director = NewDirector(state);
            OpenAndSettle(state, director);
            state.NumberOfItems[0x24 * 2 + 1] = 1;
            Tick(state, director, AlundraPadState.Down); // slot 6, item 0x24

            var exception = Record.Exception(() =>
                TickUntil(state, director, () => director.TextRevealState == 0xcf, maxTicks: 600));

            Assert.Null(exception);
            Assert.Equal("Ab", director.NameVisiblePrefix);
            Assert.Equal(string.Empty, director.Description0VisiblePrefix);
            Assert.Equal(string.Empty, director.Description1VisiblePrefix);
        }
        finally
        {
            Directory.Delete(etcPath, recursive: true);
        }
    }

    [Fact]
    public void EtcStringTable_JsonNullValue_ResolvesToAnEmptyString()
    {
        var etcPath = Path.Combine(Path.GetTempPath(), "AlundraEtcStringTableNullFixture_" + Guid.NewGuid());
        var dialoguesPath = Path.Combine(etcPath, "Dialogues");
        Directory.CreateDirectory(dialoguesPath);
        var etcIndex = new int[1024];
        Array.Fill(etcIndex, -1);
        etcIndex[7 + 0x300] = 102;
        File.WriteAllText(Path.Combine(dialoguesPath, "etc-index.json"), "[" + string.Join(",", etcIndex) + "]");
        File.WriteAllText(Path.Combine(dialoguesPath, "global-strings.json"), "{\"102\":null}");
        try
        {
            Assert.True(AlundraEtcStringTable.TryResolveItemDescriptionLine1(etcPath, 7, out var line));
            Assert.Equal(string.Empty, line);
        }
        finally
        {
            Directory.Delete(etcPath, recursive: true);
        }
    }

    /// <summary>
    /// D5 verifier's F1 (P2, reproduced on the real export) and A1: the screen shows what the original's
    /// DisplayInventoryDescription drew THIS tick (MainInventoryManager.cs:929-1064) - nothing on state 0 or
    /// 0x4d, nothing on an empty or unowned slot, line 0 only up to 0x8e, both lines from 0x8f. The raw
    /// prefixes keep their values across ticks that draw nothing, so a second line revealed on one item must
    /// not stay on screen after the cursor moves.
    /// </summary>
    [Fact]
    public void DrawnDescriptionLines_FollowWhatTheOriginalDrawsEachTick()
    {
        var etcPath = WriteEtcFixture(0x24, "Ab", "Cd", "Ef");
        EngineEnvironment.ProjectPath = etcPath;
        try
        {
            var state = NewState();
            var director = NewDirector(state);
            OpenAndSettle(state, director);
            state.NumberOfItems[0x24 * 2 + 1] = 1;
            Tick(state, director, AlundraPadState.Down); // slot 6, item 0x24 (owned)
            Tick(state, director, 0);

            // Name phase: line 0 is the name, line 1 nothing.
            Assert.Equal("A", director.DrawnDescriptionLine0);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine1);

            // The tick that runs state 0x4d draws nothing at all (:997-1005, no DisplayInventoryDescription).
            TickUntil(state, director, () => director.TextRevealState == 0x4d);
            Tick(state, director, 0);
            Assert.Equal(0x4e, director.TextRevealState);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine0);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine1);

            // First line only until 0x8e included, then both.
            TickUntil(state, director, () => director.TextRevealState == 0x8e);
            Tick(state, director, 0);
            Assert.Equal("Cd", director.DrawnDescriptionLine0);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine1);
            TickUntil(state, director, () => director.TextRevealState == 0xcf);
            Tick(state, director, 0);
            Assert.Equal("Cd", director.DrawnDescriptionLine0);
            Assert.Equal("Ef", director.DrawnDescriptionLine1);

            // Right to slot 7 (item 0x29, not owned): nothing drawn, although the prefixes keep "Cd"/"Ef".
            Tick(state, director, AlundraPadState.Right);
            Tick(state, director, 0);
            Assert.Equal(7, director.SelectedSlotId);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine0);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine1);

            // Back on the herbs: the name again on line 0, and still nothing on line 1.
            Tick(state, director, AlundraPadState.Left);
            Tick(state, director, 0);
            Tick(state, director, 0);
            Assert.Equal(6, director.SelectedSlotId);
            Assert.Equal("A", director.DrawnDescriptionLine0);
            Assert.Equal(string.Empty, director.DrawnDescriptionLine1);
        }
        finally
        {
            Directory.Delete(etcPath, recursive: true);
        }
    }

    [Fact]
    public void RevealedPrefix_NeverSplitsAnEscapePairAcrossTheBoundary()
    {
        var method = typeof(AlundraInventoryDirector).GetMethod("RevealedPrefix", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // "A{b" with visibleLength 2 lands right after '{' - must back up to 1, not split the pair.
        var result = (string)method!.Invoke(null, new object[] { "A{b", 2 })!;
        Assert.Equal("A", result);

        var resultClosing = (string)method.Invoke(null, new object[] { "A}b", 2 })!;
        Assert.Equal("A", resultClosing);

        var resultUnaffected = (string)method.Invoke(null, new object[] { "Abc", 2 })!;
        Assert.Equal("Ab", resultUnaffected);
    }

    // -----------------------------------------------------------------------------------------
    // World wiring
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void WorldUpdate_TwoTickFrame_GivesExactlyOneCursorMove()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new AlundraEntityScriptProxy();

        // InstallInventorySystems only runs from InitializeWithWorld's own tile-map-gated installation
        // block (same gate InstallHudSystems/InstallDialogueSystems share) - a bare headless world (no
        // TileMap entity) never reaches it, same reason AlundraHudDirectorTests wires its own view
        // directly for a headless montage. Attach directly here: this test's own subject is the TICK
        // WIRING inside Update (the per-tick loop next to the dialogue/HUD ones), not the attach path.
        AlundraInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, ItemTablesFixture.LoadReal(), worldProxy.SoundPlayer);

        // Open, through the real world proxy's own per-tick pad loop (Start held for exactly one 1-tick
        // frame gives one edge), then settle the 18-call slide the same way.
        worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Start };
        worldProxy.Update(0.02f);
        worldProxy.GameState.LastPadState = default;
        for (var i = 0; i < 18; i++)
        {
            worldProxy.Update(0.02f);
        }
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        var slotBefore = AlundraInventoryDirector.Instance.SelectedSlotId;

        // One rendered frame, two logic ticks, Down held throughout - must move the cursor exactly once
        // (D-E13D-9's own "un appui donne exactement un front" even across a multi-tick frame).
        worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Down };
        worldProxy.Update(0.04f);

        Assert.Equal(slotBefore + 6, AlundraInventoryDirector.Instance.SelectedSlotId);
    }
}
