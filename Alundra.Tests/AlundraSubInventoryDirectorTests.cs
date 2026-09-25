#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13.d SI3 (docs/plan-e13d-sous-inventaire.md) acceptance: drives the REAL
/// <see cref="AlundraSubInventoryDirector"/>, <see cref="AlundraInventoryDirector"/> and
/// <see cref="AlundraInventoryPostProcess"/> together, the way <see cref="AlundraInventoryDirectorTests"/>
/// drives the main inventory alone - a tick is <c>state.TickPad.Update(hold)</c> then each director's own
/// <c>Tick</c>, THEN the post-process (plan §1.2's own clock), isolated <see cref="AlundraGameState"/>
/// instances for every test but the last (which drives the real <see cref="AlundraWorldProxy"/>).
/// </summary>
public sealed class AlundraSubInventoryDirectorTests : IDisposable
{
    private readonly string? _previousProjectPath;

    public AlundraSubInventoryDirectorTests()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
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
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
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

    /// <summary>Same synthetic <c>Dialogues/etc-index.json</c>/<c>global-strings.json</c> shape
    /// <see cref="AlundraInventoryDirectorTests"/> already writes.</summary>
    private static string WriteEtcFixture(int itemId, string name, string desc0, string desc1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraSubInventoryDirectorEtcFixture_" + Guid.NewGuid());
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

    /// <summary>The real project's own <c>Dialogues/</c> - for the New Game names test (D-E13D's own "use
    /// the real exported tables" instruction), same lookup shape
    /// <c>AlundraWorldProxyAudioInstallationTests.FindProjectRoot</c> uses for <c>Sounds/</c>.</summary>
    private static string FindAlundraProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Dialogues")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraSubInventoryDirectorTests: no 'alundra-project/Dialogues' directory found above "
            + $"'{AppContext.BaseDirectory}' (docs/plan-e13d-sous-inventaire.md).");
    }

    private static AlundraGameState NewGameState(AlundraItemTables tables)
    {
        var state = new AlundraGameState();
        AlundraPlayerManager.InitializeNewGameInventory(state, tables);
        return state;
    }

    private static void AttachAll(AlundraGameState state, AlundraItemTables tables, IAlundraSoundPlayer? sound)
    {
        AlundraInventoryDirector.Instance.AttachToWorld(state, tables, sound);
        AlundraSubInventoryDirector.Instance.AttachToWorld(state, tables, sound);
    }

    /// <summary>One logic tick, both directors then the post-process - <see cref="AlundraWorldProxy"/>'s
    /// own per-tick pad loop order (E13.d SI3).</summary>
    private static void Tick(AlundraGameState state, uint hold)
    {
        state.TickPad.Update(hold);
        AlundraInventoryDirector.Instance.Tick(null);
        AlundraSubInventoryDirector.Instance.Tick();
        AlundraInventoryPostProcess.Instance.Run();
    }

    private static void TickUntil(AlundraGameState state, Func<bool> predicate, int maxTicks = 60)
    {
        for (var i = 0; i < maxTicks && !predicate(); i++)
        {
            Tick(state, 0);
        }

        Assert.True(predicate(), "TickUntil: predicate never became true within maxTicks.");
    }

    /// <summary>Opens the MAIN inventory (one trigger tick) and settles its 18-call opening slide - same
    /// shape as <see cref="AlundraInventoryDirectorTests"/>'s own <c>OpenAndSettle</c>.</summary>
    private static void OpenMainAndSettle(AlundraGameState state)
    {
        Tick(state, AlundraPadState.Start);
        for (var i = 0; i < 18; i++)
        {
            Tick(state, 0);
        }
    }

    // -----------------------------------------------------------------------------------------
    // 1/2: the full round trip, tick by tick (plan §1.2's own clock table).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SwitchRoundTrip_TickByTick_MatchesTheOriginalClock()
    {
        var state = NewGameState(ItemTablesFixture.LoadReal());
        var sound = new RecordingSoundPlayer();
        AttachAll(state, ItemTablesFixture.LoadReal(), sound);
        AlundraHudDirector.Instance.AttachToWorld(state);

        // The gauge really displayed, with the persistent latch (flag 1662) set: otherwise the gauge sits at rest,
        // InitializeHudPositionBeforeHide can do nothing, and "neither recalled nor re-hidden" below would prove
        // nothing (plan SI8, D-E13D-32).
        state.AddFlag(AlundraHudDirector.PersistentLatchFlag, AlundraHudDirector.PersistentLatchMask);
        AlundraHudDirector.Instance.InitializeHudPositionBeforeHide();
        for (var i = 0; i < 40; i++)
        {
            AlundraHudDirector.Instance.Tick();
        }

        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, AlundraHudDirector.Instance.Phase);

        var menuOpenEveryTick = new List<bool>();
        var hudPhaseEveryTick = new List<AlundraHudDirector.HudPhase>();

        void RecordAndTick(uint hold)
        {
            Tick(state, hold);
            AlundraHudDirector.Instance.Tick();
            menuOpenEveryTick.Add((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
            hudPhaseEveryTick.Add(AlundraHudDirector.Instance.Phase);
        }

        // Open the main inventory first (Start), settle its opening slide, exactly like D6's own flow; the gauge
        // hides (InitializeHudPosition at the opening).
        RecordAndTick(AlundraPadState.Start);
        for (var i = 0; i < 18; i++)
        {
            RecordAndTick(0);
        }

        for (var i = 0; i < 40 && AlundraHudDirector.Instance.Phase != AlundraHudDirector.HudPhase.Idle; i++)
        {
            RecordAndTick(0);
        }

        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        var hudPhaseBeforeSwitch = AlundraHudDirector.Instance.Phase;
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, hudPhaseBeforeSwitch); // hidden by the opening.
        hudPhaseEveryTick.Clear(); // from here on, only the switch: the gauge must not move.
        sound.Requests.Clear();

        // T0 (main -> sub): R1 read, close armed, sound 5, post-process state 1. The gauge is NOT
        // recalled (plan §1.1 - no InitializeHudPositionBeforeHide on this branch).
        RecordAndTick(AlundraPadState.R1);
        Assert.Equal(new[] { 5 }, sound.Requests);
        Assert.Equal(1, AlundraInventoryPostProcess.Instance.State);
        Assert.Equal(hudPhaseBeforeSwitch, AlundraHudDirector.Instance.Phase);
        sound.Requests.Clear();

        // T0+1..Tc: the main inventory's closing slide - exactly 18 more ticks (the same "15 interpolating
        // + 2 snap + 1 completion-detecting" shape the opening slide uses). On the LAST of these (Tc), the
        // main inventory is inactive AND the sub-inventory has ALREADY opened, in the SAME tick, by the
        // post-process - opened but not drawn yet: that tick is plan §1.2's "image sans aucun inventaire",
        // the frozen scene alone, as in the original.
        for (var i = 0; i < 17; i++)
        {
            RecordAndTick(0);
            Assert.False(AlundraSubInventoryDirector.Instance.IsActive); // not yet - still sliding.
        }
        RecordAndTick(0); // Tc.
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        Assert.True(AlundraSubInventoryDirector.Instance.IsActive);
        Assert.False(AlundraSubInventoryDirector.Instance.IsDrawn); // nothing drawn by either, this tick.
        Assert.Equal(new[] { 4 }, sound.Requests); // StartFadeOut's own sound.
        Assert.Equal(0, AlundraInventoryPostProcess.Instance.State);
        Assert.Equal(hudPhaseBeforeSwitch, AlundraHudDirector.Instance.Phase); // still untouched.
        sound.Requests.Clear();

        // Tc+1: the sub-inventory's own first per-frame tick - now drawn.
        RecordAndTick(0);
        Assert.True(AlundraSubInventoryDirector.Instance.IsDrawn);
        Assert.Empty(sound.Requests);

        // Settle the sub-inventory's own opening slide (same 18-call shape).
        for (var i = 0; i < 17; i++)
        {
            RecordAndTick(0);
        }

        // Sub -> main: L1. Close armed, sound 5, post-process state 2.
        sound.Requests.Clear();
        RecordAndTick(AlundraPadState.L1);
        Assert.Equal(new[] { 5 }, sound.Requests);
        Assert.Equal(2, AlundraInventoryPostProcess.Instance.State);
        sound.Requests.Clear();

        // The sub-inventory's own closing slide - 18 ticks, the last one (Tc) opens the main inventory's
        // HEAD (sound 4) in the SAME tick, through the post-process - main inventory NOT active yet
        // (D-E13D-22: only the head ran, the setup is still pending for the NEXT tick).
        for (var i = 0; i < 17; i++)
        {
            RecordAndTick(0);
            Assert.False(AlundraInventoryDirector.Instance.IsActive);
        }
        RecordAndTick(0); // Tc.
        Assert.False(AlundraSubInventoryDirector.Instance.IsActive);
        Assert.False(AlundraInventoryDirector.Instance.IsActive); // head ran, but setup not yet (D-E13D-22).
        Assert.Equal(new[] { 4 }, sound.Requests);
        Assert.Equal(0, AlundraInventoryPostProcess.Instance.State);
        sound.Requests.Clear();

        // Tc+1: the setup runs alone - ForbiddenWarpFlag armed, but NOT drawn yet (no RunPerFrame ran).
        RecordAndTick(0);
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        Assert.False(AlundraInventoryDirector.Instance.IsDrawn);
        Assert.Empty(sound.Requests);

        // Tc+2: the first per-frame tick - drawn.
        RecordAndTick(0);
        Assert.True(AlundraInventoryDirector.Instance.IsDrawn);

        // MenuOpen must have stayed set on EVERY tick recorded so far - the world stays frozen without
        // interruption across the whole switch, both directions (plan §1.1: "MenuOpen ne retombe donc
        // jamais pendant une bascule").
        Assert.All(menuOpenEveryTick, isSet => Assert.True(isSet));

        // The HUD's own phase never moved during the whole switch (item 2 of the brief's own test list) -
        // only the final Start-close, below, is allowed to touch it.
        Assert.All(hudPhaseEveryTick, phase => Assert.Equal(hudPhaseBeforeSwitch, phase));

        // Settle the main inventory's re-opening slide, then close it for good with Start: the gauge
        // returns and MenuOpen finally drops, exactly at the completion tick.
        for (var i = 0; i < 17; i++)
        {
            Tick(state, 0);
        }
        Assert.True((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);

        Tick(state, AlundraPadState.Start);
        Assert.True((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0); // still, mid-slide.

        // The final close recalls the gauge (InitializeHudPositionBeforeHide, latch set) - the one call the switch
        // itself never makes.
        Assert.Equal(AlundraHudDirector.HudPhase.Opening, AlundraHudDirector.Instance.Phase);

        var stillOpen = true;
        var ticksToClose = 0;
        while (stillOpen && ticksToClose < 30)
        {
            Tick(state, 0);
            ticksToClose++;
            stillOpen = AlundraInventoryDirector.Instance.IsActive;
        }

        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        Assert.False((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
    }

    // -----------------------------------------------------------------------------------------
    // 3: navigation - the 14 x 4 transitions, sound 1, text state reset.
    // -----------------------------------------------------------------------------------------

    private static readonly int[] NavRight = { 0x02, 0x00, 0x07, 0x06, 0x08, 0x04, 0x02, 0x01, 0x03, 0x0A, 0x0B, 0x0C, 0x0D, 0x09 };
    private static readonly int[] NavLeft = { 0x01, 0x07, 0x00, 0x08, 0x05, 0x03, 0x01, 0x02, 0x04, 0x0D, 0x09, 0x0A, 0x0B, 0x0C };
    private static readonly int[] NavUp = { 0x0A, 0x09, 0x0B, 0x01, 0x02, 0x06, 0x00, 0x0D, 0x07, 0x03, 0x05, 0x04, 0x04, 0x08 };
    private static readonly int[] NavDown = { 0x06, 0x03, 0x04, 0x09, 0x0B, 0x0A, 0x05, 0x08, 0x0D, 0x01, 0x00, 0x02, 0x02, 0x07 };

    /// <summary>The buttons of a shortest path from <paramref name="from"/> to <paramref name="to"/> over the four
    /// navigation tables (breadth-first; every position is reachable).</summary>
    private static List<uint> PathTo(int from, int to)
    {
        var moves = new (uint Button, int[] Table)[]
        {
            (AlundraPadState.Right, NavRight), (AlundraPadState.Left, NavLeft), (AlundraPadState.Up, NavUp), (AlundraPadState.Down, NavDown),
        };
        var previous = new Dictionary<int, (int Position, uint Button)> { [from] = (-1, 0) };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0 && !previous.ContainsKey(to))
        {
            var position = queue.Dequeue();
            foreach (var (button, table) in moves)
            {
                var next = table[position];
                if (previous.TryAdd(next, (position, button)))
                {
                    queue.Enqueue(next);
                }
            }
        }

        Assert.True(previous.ContainsKey(to), $"PathTo: position {to} is unreachable from {from}.");
        var path = new List<uint>();
        for (var position = to; position != from; position = previous[position].Position)
        {
            path.Insert(0, previous[position].Button);
        }

        return path;
    }

    private void OpenSubInventoryDirectly(AlundraGameState state)
    {
        // Bypasses the main inventory's own switch (already covered above) - opens the sub-inventory the
        // same way the post-process does, for tests whose own subject is the sub-inventory alone.
        AlundraSubInventoryDirector.Instance.OpenFromPostProcess();
        for (var i = 0; i < 18; i++)
        {
            Tick(state, 0);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(13)]
    public void Navigation_AllFourDirections_MatchTheTables_SoundOneAndTextReset(int startPosition)
    {
        // Every position must describe something, or its text never leaves state 0 (an unowned or empty
        // position freezes the reveal, plan §1.5) and "the move resets the text" would prove nothing: the real
        // exported tables, the seven crests and five key items owned, armor and boots from the New Game.
        var tables = new AlundraItemTables(FindAlundraProjectPath());
        var state = NewGameState(tables);
        Own(state, tables, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44);
        Own(state, tables, AllKeyItemIds[..5]);
        var sound = new RecordingSoundPlayer();
        AttachAll(state, tables, sound);
        OpenSubInventoryDirectly(state);

        // Walk to startPosition along a shortest path over the four tables (Right alone cycles 0 -> 2 -> 7 -> 1
        // -> 0 and never reaches 3, 9 or 13).
        foreach (var button in PathTo(AlundraSubInventoryDirector.Instance.SelectedPosition, startPosition))
        {
            Tick(state, button);
            Tick(state, 0); // release, next press is a fresh edge.
        }

        Assert.Equal(startPosition, AlundraSubInventoryDirector.Instance.SelectedPosition);

        void AssertMove(uint button, int[] table)
        {
            // Let the reveal of the current position run a few characters, so a missing reset would show.
            TickUntil(state, () => AlundraSubInventoryDirector.Instance.TextRevealState > 5, maxTicks: 30);

            var before = AlundraSubInventoryDirector.Instance.SelectedPosition;
            sound.Requests.Clear();
            Tick(state, button);

            Assert.Equal(table[before], AlundraSubInventoryDirector.Instance.SelectedPosition);
            Assert.Equal(new[] { 1 }, sound.Requests);

            // The move reset the text to 0, and the draw tail of the same tick ran state 0 (name setup, nothing
            // drawn) - SubInventoryManager.cs:339 then :522-530, as in the original, where the input and the
            // description both run in DisplaySubInventory.
            Assert.Equal(1, AlundraSubInventoryDirector.Instance.TextRevealState);
            Assert.Equal(string.Empty, AlundraSubInventoryDirector.Instance.DrawnDescriptionLine0);

            Tick(state, 0); // release.
        }

        AssertMove(AlundraPadState.Right, NavRight);
        AssertMove(AlundraPadState.Left, NavLeft);
        AssertMove(AlundraPadState.Up, NavUp);
        AssertMove(AlundraPadState.Down, NavDown);
    }

    // -----------------------------------------------------------------------------------------
    // 4: key items - 0, 1, 2 and 6 owned, no duplicate; D-E13D-24's own executable rule.
    // -----------------------------------------------------------------------------------------

    // Slot 0x1C ("key item") items, plan §1.5: 33, 57, 60, 73, 74, 75, 76, 77, 78, 87, 88, 89.
    private static readonly int[] AllKeyItemIds = { 33, 57, 60, 73, 74, 75, 76, 77, 78, 87, 88, 89 };

    // Gives one of each item outright (the count lives at NumberOfItems[id * 2 + 1], the same field the tests
    // of AlundraItemInventoryTests write): AddOneItemIfUnlocked would refuse every key item, none of them
    // carries the new-game unlock bit.
    private static void Own(AlundraGameState state, AlundraItemTables tables, params int[] itemIds)
    {
        _ = tables;
        foreach (var itemId in itemIds)
        {
            state.NumberOfItems[itemId * 2 + 1] = 1;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void KeyItems_Recomputed_NoRestartOnceExhausted(int ownedCount)
    {
        // The real exported tables: ItemTablesFixture.LoadReal() only carries items 0-4, 17 and 25, where every
        // key item would have inventory slot 0.
        var tables = new AlundraItemTables(FindAlundraProjectPath());
        var state = NewGameState(tables);
        Own(state, tables, AllKeyItemIds[..ownedCount]);

        AttachAll(state, tables, null);
        OpenSubInventoryDirectly(state);
        Tick(state, 0); // one more tail tick to be sure KeyItemIds settled post-open.

        var ids = AlundraSubInventoryDirector.Instance.KeyItemIds;
        var filled = new List<int>();
        for (var i = 0; i < ids.Length; i++)
        {
            if (i < ownedCount)
            {
                Assert.True(ids[i] != -1,
                    $"key item position {i} is empty: ids [{string.Join(",", ids)}], state {AlundraSubInventoryDirector.Instance.State}, "
                    + $"count(33) {AlundraPlayerManager.GetNumberOfItem(state, 33)}, slot(33) {tables.ItemsProperties[33 * AlundraItemTables.ItemColumnCount]}");
                filled.Add(ids[i]);
            }
            else
            {
                Assert.Equal(-1, ids[i]);
            }
        }

        Assert.Equal(filled.Count, new HashSet<int>(filled).Count); // no duplicate.
    }

    /// <summary>D-E13D-24 with a single owned key item: only position 0 is filled. The decompilation's loop,
    /// which searches again from item 0 after an empty search, would put the same item at positions 0, 2 and 4
    /// (plan §1.5); the executable, and this port, leave 1 to 4 empty. Proven discriminating by a temporary
    /// mutation of <c>RecomputeKeyItems</c> (plan SI3, journal).</summary>
    [Fact]
    public void KeyItems_OneOwned_OnlyTheFirstPositionIsFilled()
    {
        var tables = new AlundraItemTables(FindAlundraProjectPath());
        var state = NewGameState(tables);
        Own(state, tables, AllKeyItemIds[0]);

        AttachAll(state, tables, null);
        OpenSubInventoryDirectly(state);
        Tick(state, 0);

        Assert.Equal(new[] { AllKeyItemIds[0], -1, -1, -1, -1 }, AlundraSubInventoryDirector.Instance.KeyItemIds);
    }

    [Fact]
    public void Armory_UnownedPosition_NothingDescribed_TextFrozen()
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables); // owns nothing in the armory range (0x3E..0x44).
        AttachAll(state, tables, null);
        OpenSubInventoryDirectly(state);

        Assert.Equal(0, AlundraSubInventoryDirector.Instance.SelectedPosition); // armory position 0.
        Assert.False(AlundraSubInventoryDirector.Instance.ArmoryOwned(0));

        for (var i = 0; i < 10; i++)
        {
            Tick(state, 0);
        }

        Assert.Equal(0, AlundraSubInventoryDirector.Instance.TextRevealState);
        Assert.Equal(string.Empty, AlundraSubInventoryDirector.Instance.DrawnDescriptionLine0);
    }

    // -----------------------------------------------------------------------------------------
    // 5: description - armor 17 at position 7: name, then both description lines (D-E13D-30: the original's
    // one-byte-early read of the second line is corrected).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Description_Armor17AtPosition7_NameThenBothLines()
    {
        var projectPath = WriteEtcFixture(17, "Armure en tissu", "Confortable protection en tissu.", "Faible capacite de protection.");
        try
        {
            EngineEnvironment.ProjectPath = projectPath;
            var tables = ItemTablesFixture.LoadReal();
            var state = NewGameState(tables); // New Game owns item 17 (armor slot 7).
            AttachAll(state, tables, null);
            OpenSubInventoryDirectly(state);

            // Walk to position 7 (armor/boots icons).
            while (AlundraSubInventoryDirector.Instance.SelectedPosition != 7)
            {
                Tick(state, AlundraPadState.Right);
                Tick(state, 0);
            }

            // One character every third tick: 15 characters take about 45 ticks.
            TickUntil(state, () => AlundraSubInventoryDirector.Instance.DrawnDescriptionLine0 == "Armure en tissu", maxTicks: 80);

            TickUntil(state, () => AlundraSubInventoryDirector.Instance.DrawnDescriptionLine0 == "Confortable protection en tissu.", maxTicks: 300);

            // D-E13D-30: the second line is revealed one character every third tick from state 0x8f, like the main
            // inventory. The executable reads line2[c - 0x90] and would end the reveal at 0xcf on the very next
            // tick with nothing shown (the byte before every second line is 0): this port corrects that defect.
            TickUntil(state, () => AlundraSubInventoryDirector.Instance.TextRevealState == 0x8f, maxTicks: 100);
            Tick(state, 0);
            Assert.NotEqual(0xcf, AlundraSubInventoryDirector.Instance.TextRevealState);

            // Mid-reveal, the second line is drawn as a growing prefix, as the first one is.
            TickUntil(state, () => AlundraSubInventoryDirector.Instance.TextRevealState >= 0x95, maxTicks: 30);
            Assert.True(AlundraSubInventoryDirector.Instance.TextRevealState < 0xcf);
            var partial = AlundraSubInventoryDirector.Instance.DrawnDescriptionLine1;
            Assert.NotEqual(string.Empty, partial);
            Assert.StartsWith(partial, "Faible capacite de protection.");
            Assert.NotEqual("Faible capacite de protection.", partial);

            TickUntil(state, () => AlundraSubInventoryDirector.Instance.DrawnDescriptionLine1 == "Faible capacite de protection.", maxTicks: 120);
            TickUntil(state, () => AlundraSubInventoryDirector.Instance.TextRevealState == 0xcf, maxTicks: 10);

            // Both lines stay drawn once the reveal is done.
            for (var i = 0; i < 50; i++)
            {
                Tick(state, 0);
            }

            Assert.Equal("Confortable protection en tissu.", AlundraSubInventoryDirector.Instance.DrawnDescriptionLine0);
            Assert.Equal("Faible capacite de protection.", AlundraSubInventoryDirector.Instance.DrawnDescriptionLine1);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    // -----------------------------------------------------------------------------------------
    // 6: New Game names - "Armure en tissu", "Bottes courtes" (real exported tables).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void NewGame_ArmorAndBootsNames_AreTheRealExportedStrings()
    {
        EngineEnvironment.ProjectPath = FindAlundraProjectPath();
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        AttachAll(state, tables, null);

        AlundraSubInventoryDirector.Instance.OpenFromPostProcess();

        Assert.Equal("Armure en tissu", AlundraSubInventoryDirector.Instance.ArmorName);
        Assert.Equal("Bottes courtes", AlundraSubInventoryDirector.Instance.BootsName);
    }

    // -----------------------------------------------------------------------------------------
    // 7: Triangle/Start/L2/R2 close; L1 and R1 both switch.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData((uint)0x0800u)] // Start
    [InlineData((uint)0x0010u)] // Triangle
    [InlineData((uint)0x0001u)] // L2
    [InlineData((uint)0x0002u)] // R2
    public void Closing_StartTriangleL2R2_ClosesTheSame(uint button)
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        var sound = new RecordingSoundPlayer();
        AttachAll(state, tables, sound);
        AlundraHudDirector.Instance.AttachToWorld(state);
        OpenSubInventoryDirectly(state);
        sound.Requests.Clear();

        Tick(state, button);

        Assert.Equal(new[] { 5 }, sound.Requests);
        Assert.Equal(3, AlundraSubInventoryDirector.Instance.State); // residual(1) | closing(2).

        for (var i = 0; i < 18; i++)
        {
            Tick(state, 0);
        }

        Assert.False(AlundraSubInventoryDirector.Instance.IsActive);
        Assert.False((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
    }

    [Theory]
    [InlineData((uint)0x0004u)] // L1
    [InlineData((uint)0x0008u)] // R1
    public void Closing_L1OrR1_SwitchesToMain(uint button)
    {
        var tables = ItemTablesFixture.LoadReal();
        var state = NewGameState(tables);
        var sound = new RecordingSoundPlayer();
        AttachAll(state, tables, sound);
        OpenSubInventoryDirectly(state);
        sound.Requests.Clear();

        Tick(state, button);

        Assert.Equal(new[] { 5 }, sound.Requests);
        Assert.Equal(2, AlundraInventoryPostProcess.Instance.State);
        Assert.True((state.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0);
    }

    // -----------------------------------------------------------------------------------------
    // 9: wiring through the real AlundraWorldProxy.Update.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void WorldUpdate_R1InMainInventory_OpensTheSubInventory()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new AlundraEntityScriptProxy();

        var tables = ItemTablesFixture.LoadReal();
        AlundraInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);
        AlundraSubInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);

        worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Start };
        worldProxy.Update(0.02f);
        worldProxy.GameState.LastPadState = default;
        for (var i = 0; i < 18; i++)
        {
            worldProxy.Update(0.02f);
        }
        Assert.True(AlundraInventoryDirector.Instance.IsActive);

        worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.R1 };
        worldProxy.Update(0.02f);
        worldProxy.GameState.LastPadState = default;

        for (var i = 0; i < 40 && !AlundraSubInventoryDirector.Instance.IsActive; i++)
        {
            worldProxy.Update(0.02f);
        }

        Assert.True(AlundraSubInventoryDirector.Instance.IsActive);
    }
}
