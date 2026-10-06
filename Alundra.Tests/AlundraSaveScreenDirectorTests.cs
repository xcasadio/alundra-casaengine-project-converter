#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.SaveGames;
using CasaEngine.Framework.UI;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E16.e T3 (docs/plan-e16-etat-partie.md, L2, L3, L5, J3, J4, SE3, SE4, SE5, SE9): the save screen's director
/// without MGUI, on the REAL dialogue director (a real <see cref="AlundraDialoguePresenter"/> over a recording UI
/// view, whose choice the test picks), the real export's <c>Dialogues/Etc.dialogue</c> and validation rules (K1), and
/// a fake save service behind <see cref="AlundraSaveGameDirector.SaveSlots"/> (D-E16-31: the real save folder is
/// never reached).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveScreenDirectorTests : IDisposable
{
    private const string Examine = "Examen de la Carte Mémoire . . .";
    private const string PickHeading = "Sélectionne une fente pour l'enregistrement.";
    private const string QuestionHeading = "Enregistrer tes exploits ici?";
    private const string Saving = "Enregistrement de l'histoire . . .";
    private const string Saved = "Histoire enregistrée.";
    private const string Failed0 = "Echec de l'enregistrement!";
    private const string Failed1 = "Essaye d'enregistrer l'histoire de nouveau.";

    /// <summary>Ticks from <see cref="AlundraSaveScreenDirector.Start"/> to the settled picker: 1 (0x2710), 20
    /// (0x2711), 20 (0x3f4), 1 (0x0C, the picker's initialization), 18 (the opening slide).</summary>
    private const int TicksToSettledPicker = 1 + 20 + 20 + 1 + 18;

    private readonly SlotsBySlot _slots = new();
    private readonly AlundraSaveBookTests.RecordingUIViewRuntime _uiView = new();
    private readonly AlundraDialoguePresenter _presenter;
    private readonly RecordingSound _sound = new();

    public AlundraSaveScreenDirectorTests()
    {
        ResetAll();
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        _presenter = new AlundraDialoguePresenter(_uiView);
        Dialogue.AttachToWorld(_presenter, State);
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = RealRules;
        AlundraSaveGameDirector.Instance.SaveSlots = _slots;
        Director.AttachToWorld(State, _sound);
    }

    public void Dispose() => ResetAll();

    private static void ResetAll()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
        AlundraSaveScreenDirector.Instance.ResetForTests();
    }

    private static AlundraGameState State => AlundraGameState.Instance;

    private static AlundraDialogueDirector Dialogue => AlundraDialogueDirector.Instance;

    private static AlundraSaveScreenDirector Director => AlundraSaveScreenDirector.Instance;

    private readonly List<int> _stateLog = new();

    /// <summary>One logic tick as the frame runs it: the pad, then the director, then (E19.f3a, the order of the world proxy) the pass of the dialogue director,
    /// whose choice box answers. Records the state the tick starts in.</summary>
    private void Tick(int count = 1, uint hold = 0)
    {
        for (var i = 0; i < count; i++)
        {
            TickScreen(hold);
            Dialogue.Tick();
        }
    }

    /// <summary>The screen's half of a tick (the pad, then the director), for the callers that run the dialogue pass themselves.</summary>
    private void TickScreen(uint hold = 0)
    {
        _stateLog.Add(Director.State);
        State.TickPad.Update(hold);
        Director.Tick();
    }

    private void Press(uint button) => Tick(hold: button);

    /// <summary>How many ticks started in <paramref name="state"/>.</summary>
    private int TicksIn(int state) => _stateLog.Count(s => s == state);

    /// <summary>The distinct states in the order the flow visited them.</summary>
    private List<int> VisitedStates()
    {
        var visited = new List<int>();
        foreach (var s in _stateLog)
        {
            if (visited.Count == 0 || visited[^1] != s)
            {
                visited.Add(s);
            }
        }

        return visited;
    }

    private AlundraSaveGame StartAndOpenPicker(AlundraSaveGame? save = null)
    {
        save ??= ValidSave();
        Assert.True(Director.Start(save));
        Tick(TicksToSettledPicker);
        Assert.Equal(AlundraSaveScreenDirector.StatePickWait, Director.State);
        return save;
    }

    /// <summary>Cross, then the answer through the real dialogue director (0 = OUI, 1 = NON), then the closing slide: the answer
    /// is taken 37 (OUI) or 38 (NON) ticks after the Cross (E19.f3a, the choice box of the binary), the picker ends 18 ticks after it.</summary>
    private void AnswerAndClose(int choice)
    {
        Press(AlundraPadState.Cross);
        Assert.True(Director.IsQuestionPending);
        Assert.True(Dialogue.IsAwaitingChoice);
        Assert.True(Dialogue.SelectChoiceForTests(choice));
        Tick((choice == 0 ? 37 : 38) + 18);
        Assert.False(Director.IsPickerActive);
    }

    private void TickUntilState(int state, int max = 400)
    {
        for (var i = 0; i < max && Director.State != state; i++)
        {
            Tick();
        }

        Assert.Equal(state, Director.State);
    }

    // ---- L2: the kept states, their waits and texts -------------------------------------------------------------

    [Fact]
    public void Yes_RunsTheKeptStatesInOrder_WithTheOriginalWaits_Texts_AndOneBinaryWriteOfTheCapture()
    {
        var save = ValidSave();
        Assert.True(Director.Start(save));
        Assert.True(Director.IsActive);
        Assert.Equal(AlundraSaveScreenDirector.StateExamine, Director.State);

        // 0x2710: MenuOpen and the "Examen" message, which slides in from y = 240.
        Tick();
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);
        Assert.True(Director.IsMessageBoxDrawn);
        Assert.Equal((16, 240), Director.MessageBoxPosition);
        Assert.Equal((Examine, 32, 252), Director.MessageLine0);
        Assert.Equal(string.Empty, Director.MessageLine1.Text);
        Tick(17);
        Assert.Equal((16, 168), Director.MessageBoxPosition);

        // 0x3f4 closes the message (sound 5); the picker opens at 0x0C, with ETC 0x83 in the message box.
        Tick(TicksToSettledPicker - 18);
        Assert.Equal(new[] { AlundraSaveScreenDirector.MessageCloseSfx }, _sound.Played);
        Assert.True(Director.IsPickerActive);
        Assert.Equal(PickHeading, Director.MessageLine0.Text);
        Assert.Equal((32, 184), (Director.MessageLine0.X, Director.MessageLine0.Y));

        AnswerAndClose(0);

        // 0x3f8, then 0xA5 and the write at the end of 0x10's wait.
        TickUntilState(AlundraSaveScreenDirector.StateWrite);
        Assert.True(Director.IsMessageBoxDrawn);
        Assert.Equal(Saving, Director.MessageLine0.Text);
        Assert.Empty(_slots.SaveCalls);
        TickUntilState(AlundraSaveScreenDirector.StateSaved);
        var call = Assert.Single(_slots.SaveCalls);
        Assert.Equal("slot1", call.Slot);
        Assert.Equal(SaveGameFormat.Binary, call.Format);
        Assert.Same(save, call.Data);
        Assert.Equal(save.BuildMetadata(), call.Metadata);

        // 0x3fe, 0x16: "Histoire enregistrée.", which waits for Square: nothing passes alone.
        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);
        Assert.Equal((Saved, string.Empty), (Director.MessageLine0.Text, Director.MessageLine1.Text));
        Tick(300);
        Assert.Equal(AlundraSaveScreenDirector.StateWaitSquare, Director.State);
        Assert.True(Director.IsActive);
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);

        // Square closes the message; the end chain; MenuOpen cleared at 0x63.
        Press(AlundraPadState.Square);
        TickUntilState(AlundraSaveScreenDirector.StateIdle);
        Assert.False(Director.IsActive);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.Single(_slots.SaveCalls);

        Assert.Equal(
            new[]
            {
                AlundraSaveScreenDirector.StateExamine, AlundraSaveScreenDirector.StateExamineWait,
                AlundraSaveScreenDirector.StateReadWait, AlundraSaveScreenDirector.StatePick,
                AlundraSaveScreenDirector.StatePickWait, AlundraSaveScreenDirector.StateWrite,
                AlundraSaveScreenDirector.StateSaved, AlundraSaveScreenDirector.StateSavedMessage,
                AlundraSaveScreenDirector.StateWaitSquare, AlundraSaveScreenDirector.StateBlankMessage,
                AlundraSaveScreenDirector.StateCloseWait, AlundraSaveScreenDirector.StateHud,
                AlundraSaveScreenDirector.StateEndWait, AlundraSaveScreenDirector.StateEnd,
            },
            VisitedStates());

        // The waits of AdvanceFadeOldCheck: 0x13 is 20 ticks, 0x0B 12; 0x3f6 starts at 0x11 after the empty 0x0D.
        Assert.Equal(20, TicksIn(AlundraSaveScreenDirector.StateExamineWait));
        Assert.Equal(20, TicksIn(AlundraSaveScreenDirector.StateReadWait));
        Assert.Equal(20, TicksIn(AlundraSaveScreenDirector.StateWrite));
        Assert.Equal(20, TicksIn(AlundraSaveScreenDirector.StateSaved));
        Assert.Equal(3, TicksIn(AlundraSaveScreenDirector.StateCloseWait));
        Assert.Equal(12, TicksIn(AlundraSaveScreenDirector.StateEndWait));
        Assert.Equal(1, TicksIn(AlundraSaveScreenDirector.StateEnd));
    }

    [Fact]
    public void No_WritesNothing_AndTakesTheNoPath()
    {
        StartAndOpenPicker();
        AnswerAndClose(1);

        TickUntilState(AlundraSaveScreenDirector.StateIdle);
        Assert.Empty(_slots.SaveCalls);
        Assert.Equal(0u, State.PlayerControlFlags);

        var visited = VisitedStates();
        var fromPick = visited.SkipWhile(s => s != AlundraSaveScreenDirector.StatePickWait).ToList();
        Assert.Equal(
            new[]
            {
                AlundraSaveScreenDirector.StatePickWait, AlundraSaveScreenDirector.StateWrite,
                AlundraSaveScreenDirector.StateCloseWait, AlundraSaveScreenDirector.StateHud,
                AlundraSaveScreenDirector.StateEndWait, AlundraSaveScreenDirector.StateEnd,
            },
            fromPick);
        Assert.Equal(20, TicksIn(AlundraSaveScreenDirector.StateCloseWait));
    }

    [Fact]
    public void StartFailure_ShowsTheFailureMessage_WaitsForSquare_ThenEndsWithoutWriting()
    {
        Assert.True(Director.StartFailure());
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);

        Tick();
        Assert.Equal((Failed0, Failed1), (Director.MessageLine0.Text, Director.MessageLine1.Text));
        Assert.Equal((32, 240 + 12 + 16), (Director.MessageLine1.X, Director.MessageLine1.Y));
        Tick(200);
        Assert.Equal(AlundraSaveScreenDirector.StateWaitSquare, Director.State);

        Press(AlundraPadState.Square);
        TickUntilState(AlundraSaveScreenDirector.StateIdle);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.True(_slots.NothingCalled);
    }

    [Fact]
    public void Start_WhileActive_IsRefused()
    {
        Assert.True(Director.Start(ValidSave()));
        Assert.False(Director.Start(ValidSave()));
        Assert.False(Director.StartFailure());
    }

    // ---- L3: the labels -----------------------------------------------------------------------------------------

    [Fact]
    public void Labels_AValidSlotShowsItsRecomputedChapterThenSummary_OtherSlotsShowTwoEmptyLines()
    {
        var chapter1 = ValidSave();
        chapter1.GameFlags[0] = 1u << 3; // chapter flag 0x0003 set: chapter 1.
        chapter1.HpMax = 12;
        chapter1.Hp = 12;
        chapter1.GameTime = (1u * 216000u) + (2u * 3600u) + (3u * 60u);
        _slots.Answers["slot2"] = (SaveGameLoadStatus.Loaded, chapter1);
        _slots.Answers["slot4"] = (SaveGameLoadStatus.Loaded, ValidSave());

        StartAndOpenPicker();

        Assert.Equal(("", ""), Director.SlotLabel(0));
        Assert.Equal(("Wendell Succombe", "  HP 12       TIME 01:02:03   "), Director.SlotLabel(1));
        Assert.Equal(("", ""), Director.SlotLabel(2));
        Assert.Equal(("Un Nouveau Départ", "  HP 10       TIME 00:00:00   "), Director.SlotLabel(3));

        // The file's metadata is never read: the listing, which carries it, is never asked for.
        Assert.Equal(0, _slots.ListCalls);
    }

    [Fact]
    public void Labels_AreReadFourTimesPerOpening_NeverDuringThePicker()
    {
        StartAndOpenPicker();
        Assert.Equal(new[] { "slot1", "slot2", "slot3", "slot4" }, _slots.LoadCalls);

        Press(AlundraPadState.Down);
        Tick(20);
        Press(AlundraPadState.Down);
        Tick(20);
        Press(AlundraPadState.Up);
        Tick(20);
        AnswerAndClose(0);
        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);

        Assert.Equal(4, _slots.LoadCalls.Count);
    }

    public static IEnumerable<object[]> HostileLoads()
    {
        foreach (var status in Enum.GetValues<SaveGameLoadStatus>().Where(s => s != SaveGameLoadStatus.Loaded))
        {
            yield return new object[] { status.ToString() };
        }

        yield return new object[] { "LoadedNull" };
        yield return new object[] { "DataVersion0" };
        yield return new object[] { "HpMaxOutOfDomain" };
    }

    [Theory]
    [MemberData(nameof(HostileLoads))]
    public void Labels_HostileSlots_ShowTwoEmptyLines_AndChangeNothing(string hostile)
    {
        for (var i = 1; i <= 4; i++)
        {
            AlundraSaveGame? save = ValidSave();
            var status = SaveGameLoadStatus.Loaded;
            switch (hostile)
            {
                case "LoadedNull":
                    save = null;
                    break;
                case "DataVersion0":
                    save.LoadedDataVersion = 0;
                    break;
                case "HpMaxOutOfDomain":
                    save.HpMax = 99;
                    break;
                default:
                    status = Enum.Parse<SaveGameLoadStatus>(hostile);
                    break;
            }

            _slots.Answers[$"slot{i}"] = (status, save);
        }

        var capture = ValidSave();
        Assert.True(Director.Start(capture));
        Tick();
        var before = StateSnapshot.Take(State);
        Tick(TicksToSettledPicker - 1);

        StateSnapshot.Take(State).AssertSameAs(before);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(("", ""), Director.SlotLabel(i));
        }

        Assert.Empty(_slots.SaveCalls);

        // Still overwritable: OUI writes the capture itself.
        AnswerAndClose(0);
        TickUntilState(AlundraSaveScreenDirector.StateSaved);
        Assert.Same(capture, Assert.Single(_slots.SaveCalls).Data);
    }

    [Fact]
    public void Labels_ARulesFactoryThatThrows_GivesEmptyLabels_WithoutException_NorWrite()
    {
        _slots.Answers["slot1"] = (SaveGameLoadStatus.Loaded, ValidSave());
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = () => throw new InvalidOperationException("no rules");

        StartAndOpenPicker();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(("", ""), Director.SlotLabel(i));
        }

        Assert.Empty(_slots.SaveCalls);
    }

    [Fact]
    public void Labels_AServiceThatThrowsBehindTheAdapter_GivesEmptyLabels_WithoutException_NorWrite()
    {
        var calls = 0;
        AlundraSaveGameDirector.Instance.SaveSlots = new AlundraEngineSaveSlots(() =>
        {
            calls++;
            throw new IOException("disk gone");
        });

        StartAndOpenPicker();

        Assert.Equal(4, calls);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(("", ""), Director.SlotLabel(i));
        }
    }

    [Fact]
    public void Labels_AFakeThatThrowsItself_NeverEscapesTheDirector()
    {
        _slots.ThrowOnLoad = true;

        StartAndOpenPicker();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(("", ""), Director.SlotLabel(i));
        }
    }

    // ---- J4: the carousel ---------------------------------------------------------------------------------------

    [Fact]
    public void Carousel_Opening_RowsFromY240_TintsTo40_80_40_40_TwoEntriesFilled_HeadingSlidesIn()
    {
        _slots.Answers["slot1"] = (SaveGameLoadStatus.Loaded, ValidSave());
        Assert.True(Director.Start(ValidSave()));
        Tick(1 + 20 + 20 + 1); // the tick of 0x0C: the picker initializes and draws nothing.
        Assert.True(Director.IsPickerActive);
        Assert.False(Director.IsMessageBoxDrawn);
        Assert.False(Director.IsRecordDrawn(1));

        // First drawn tick: every box at its source, y = 240; tints one fifteenth of the way.
        Tick();
        Assert.True(Director.IsMessageBoxDrawn);
        Assert.Equal((16, 240), Director.MessageBoxPosition);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal((16, 240), Director.RecordPosition(i));
        }

        Assert.Equal(0x80 / 15, Director.RecordTint(1));

        Tick(17);
        Assert.Equal((16, 168), Director.MessageBoxPosition);
        Assert.Equal(new[] { (16, 0), (16, 64), (16, 128), (16, 240) }, Enumerable.Range(0, 4).Select(Director.RecordPosition));
        Assert.Equal(new[] { 0x40, 0x80, 0x40, 0x40 }, Enumerable.Range(0, 4).Select(Director.RecordTint));
        Assert.Equal(new[] { false, true, true, false }, Enumerable.Range(0, 4).Select(Director.IsRecordDrawn));
        Assert.Equal(0, Director.Selection);
        Assert.Equal(3, Director.Ring);

        // Record 1 shows entry 0 (slot1, valid), record 2 entry 1 (slot2, empty: an empty box).
        Assert.Equal(("Un Nouveau Départ", 32, 72), Director.RecordLine0(1));
        Assert.Equal(("  HP 10       TIME 00:00:00   ", 32, 96), Director.RecordLine1(1));
        Assert.Equal(("", 32, 136), Director.RecordLine0(2));
        Assert.Equal(PickHeading, Director.MessageLine0.Text);
    }

    [Fact]
    public void Carousel_Down_MovesTheRowsUp_RetintsThem_FillsTheNextEntry_AndHidesTheLeavingRecord()
    {
        StartAndOpenPicker();

        Press(AlundraPadState.Down);
        // The arming tick steps each slide once: every record at its source.
        Assert.Equal(new[] { (16, 0), (16, 64), (16, 128), (16, 240) }, Enumerable.Range(0, 4).Select(Director.RecordPosition));
        Assert.Equal(1, Director.Selection);
        Assert.Equal(0, Director.Ring);
        Assert.True(Director.IsRecordDrawn(3)); // entry 2 filled into the old ring record.

        Tick(17);
        Assert.Equal(new[] { (16, -57), (16, 0), (16, 64), (16, 128) }, Enumerable.Range(0, 4).Select(Director.RecordPosition));
        Assert.Equal(new[] { 0, 0x40, 0x80, 0x40 }, Enumerable.Range(0, 4).Select(Director.RecordTint));
        Assert.Equal(new[] { false, true, true, true }, Enumerable.Range(0, 4).Select(Director.IsRecordDrawn));
    }

    [Fact]
    public void Carousel_Up_AfterDown_PutsTheOpeningLayoutBack()
    {
        StartAndOpenPicker();
        Press(AlundraPadState.Down);
        Tick(17);

        Press(AlundraPadState.Up);
        Tick(17);

        Assert.Equal(0, Director.Selection);
        Assert.Equal(3, Director.Ring);
        Assert.Equal(new[] { (16, 0), (16, 64), (16, 128), (16, 240) }, Enumerable.Range(0, 4).Select(Director.RecordPosition));
        Assert.Equal(new[] { 0x40, 0x80, 0x40, 0 }, Enumerable.Range(0, 4).Select(Director.RecordTint));
        Assert.Equal(new[] { false, true, true, false }, Enumerable.Range(0, 4).Select(Director.IsRecordDrawn));
    }

    [Fact]
    public void Carousel_Bounds_NoScrollAboveTheFirstNorBelowTheFourthEntry()
    {
        StartAndOpenPicker();

        Press(AlundraPadState.Up);
        Tick(20);
        Assert.Equal(0, Director.Selection);

        for (var i = 0; i < 5; i++)
        {
            Press(AlundraPadState.Down);
            Tick(20);
        }

        Assert.Equal(3, Director.Selection);
    }

    [Fact]
    public void Carousel_HeldDown_Repeats_AtTheOriginalRate()
    {
        StartAndOpenPicker();

        Tick(hold: AlundraPadState.Down);
        Assert.Equal(1, Director.Selection);
        Tick(20, hold: AlundraPadState.Down); // 20 held ticks of silence (MaxNbFrameHeld)...
        Assert.Equal(1, Director.Selection);
        Tick(hold: AlundraPadState.Down); // ...then the held button every tick (RepeatInterval 0).
        Assert.Equal(2, Director.Selection);
    }

    [Fact]
    public void Carousel_Closing_ThreeRowsLeaveUpwards_TintsFallToZero_ResultOnlyAtTheMessageBoxsEnd()
    {
        StartAndOpenPicker();
        Press(AlundraPadState.Cross);
        Assert.Equal(QuestionHeading, Director.MessageLine0.Text);
        Assert.True(Dialogue.SelectChoiceForTests(0));

        var taken = 0;
        while (Director.IsQuestionPending && taken < 150)
        {
            Tick(); // E19.f3a: the answer arms the closing slide at the 37th tick.
            taken++;
        }

        Assert.Equal(37, taken);
        Assert.Equal(AlundraSaveScreenDirector.StatePickWait, Director.State);

        Tick(16);
        Assert.Equal(new[] { (16, -57), (16, -57), (16, -57) }, new[] { 0, 1, 2 }.Select(Director.RecordPosition));
        Assert.Equal(new[] { 0, 0, 0 }, new[] { 0, 1, 2 }.Select(Director.RecordTint));
        Assert.Equal((16, 240), Director.MessageBoxPosition);
        Assert.True(Director.IsPickerActive);
        Assert.Equal(QuestionHeading, Director.MessageLine0.Text);

        Tick(); // the message box's 17th call: settled, not yet reported.
        Assert.True(Director.IsPickerActive);

        Tick(); // its 18th call: the picker ends and this tick draws nothing.
        Assert.False(Director.IsPickerActive);
        Assert.False(Director.IsMessageBoxDrawn);
        Assert.False(Director.IsRecordDrawn(1));

        Tick();
        Assert.Equal(AlundraSaveScreenDirector.StatePickWait, Director.State); // then 0x3f8's own wait.
    }

    // ---- L5: OUI/NON through the dialogue director ---------------------------------------------------------------

    [Fact]
    public void Cross_AsksOuiNon_OverTheScreen_ThenTheAnswerClosesTheLoneChoice_MenuOpenKept()
    {
        StartAndOpenPicker();

        Press(AlundraPadState.Cross);
        Assert.Equal(new[] { "OUI", "NON" }, Dialogue.ChoicesForTests);
        Assert.False(Dialogue.IsOpen); // E19.f3a: the engine's window is not pushed any more

        // Its own keys are ignored while the question waits.
        Press(AlundraPadState.Down);
        Assert.Equal(0, Director.Selection);

        Assert.True(Dialogue.SelectChoiceForTests(1));
        var taken = 0;
        while (Dialogue.IsAwaitingChoice && taken < 150)
        {
            Tick();
            taken++;
        }

        Assert.Equal(37, taken); // E19.f3a: NON armed one tick after the Cross is taken 37 ticks later (38 after the Cross)
        Assert.False(Dialogue.IsAwaitingChoice);
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);

        TickUntilState(AlundraSaveScreenDirector.StateEnd);
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);
        Tick();
        Assert.Equal(0u, State.PlayerControlFlags);
    }

    [Fact]
    public void DownDuringTheQuestion_ThenOui_WritesTheFrozenSlot_Once()
    {
        StartAndOpenPicker();
        Press(AlundraPadState.Down);
        Tick(17);
        Assert.Equal(1, Director.Selection);

        Press(AlundraPadState.Cross);
        Press(AlundraPadState.Down);
        Tick(25, hold: AlundraPadState.Down);
        Assert.True(Dialogue.SelectChoiceForTests(0));
        var taken = 0;
        while (Director.IsQuestionPending && taken < 150)
        {
            Tick(hold: AlundraPadState.Down);
            taken++;
        }

        Assert.Equal(20, taken); // E19.f3a: armed 26 ticks after the Cross, past the first interactive pass (18): pressed at the next pass; 20 ticks in the order of the proxy (19 in the binary's)

        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);
        Assert.Equal("slot2", Assert.Single(_slots.SaveCalls).Slot);
    }

    [Fact]
    public void Cross_WithoutAPresenter_IsNon_NothingWritten()
    {
        Dialogue.AttachToWorld(null, State);
        Assert.False(Dialogue.HasPresenter);
        using var logs = LogCapture.Install();

        StartAndOpenPicker();
        Press(AlundraPadState.Cross);
        Assert.False(Dialogue.IsAwaitingChoice);

        TickUntilState(AlundraSaveScreenDirector.StateIdle);
        Assert.Empty(_slots.SaveCalls);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.Contains(logs.Warnings, w => w.Contains("no dialogue presenter"));
    }

    [Fact]
    public void CloseStandaloneChoice_DoesNothingWhileABoxIsOpen_AndNeverTouchesTheFlags()
    {
        // The book's own choice: a box, then its choice.
        Dialogue.Open(null, null, controlMode: 1);
        Dialogue.OpenChoice(new[] { "OUI", "NON" });
        var flags = State.PlayerControlFlags;

        Assert.False(Dialogue.CloseStandaloneChoice());
        Assert.True(Dialogue.IsOpen);
        Assert.True(Dialogue.IsAwaitingChoice);
        Assert.Equal(flags, State.PlayerControlFlags);
    }

    [Fact]
    public void CloseStandaloneChoice_ClosesALoneChoice_AndNeverTouchesTheFlags()
    {
        State.PlayerControlFlags = AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.ControlLocked;
        Dialogue.OpenChoice(new[] { "OUI", "NON" });

        Assert.True(Dialogue.CloseStandaloneChoice());

        Assert.False(Dialogue.IsAwaitingChoice);
        Assert.Null(Dialogue.TakeChoiceResult());
        Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.ControlLocked, State.PlayerControlFlags);
    }

    // ---- L2 adapted: every failure of the write ------------------------------------------------------------------

    public static IEnumerable<object[]> FailedSaves()
        => Enum.GetValues<SaveGameSaveStatus>().Where(s => s != SaveGameSaveStatus.Saved).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(FailedSaves))]
    public void AnySaveStatusButSaved_ShowsTheFailureMessage(SaveGameSaveStatus status)
    {
        _slots.SaveStatus = status;
        StartAndOpenPicker();
        AnswerAndClose(0);

        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);
        Assert.Contains(AlundraSaveScreenDirector.StateFailed, _stateLog);
        Assert.DoesNotContain(AlundraSaveScreenDirector.StateSaved, _stateLog);
        Assert.Equal((Failed0, Failed1), (Director.MessageLine0.Text, Director.MessageLine1.Text));
    }

    [Fact]
    public void AServiceThatThrowsBehindTheAdapterOnSave_ShowsTheFailureMessage_WithoutException()
    {
        StartAndOpenPicker();
        AlundraSaveGameDirector.Instance.SaveSlots = new AlundraEngineSaveSlots(() => throw new IOException("disk gone"));
        AnswerAndClose(0);

        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);
        Assert.Contains(AlundraSaveScreenDirector.StateFailed, _stateLog);
        Assert.Equal(Failed0, Director.MessageLine0.Text);
    }

    [Fact]
    public void AFakeThatThrowsItselfOnSave_ShowsTheFailureMessage_WithoutException()
    {
        StartAndOpenPicker();
        _slots.ThrowOnSave = true;
        AnswerAndClose(0);

        TickUntilState(AlundraSaveScreenDirector.StateWaitSquare);
        Assert.Contains(AlundraSaveScreenDirector.StateFailed, _stateLog);
    }

    // ---- L5: IsActive, MenuOpen, the inventory -------------------------------------------------------------------

    [Fact]
    public void IsActiveAndMenuOpen_HoldOverTheWholeFlow_AndTheInventoryNeverOpens()
    {
        var tables = new AlundraItemTables(FindProjectRoot());
        AlundraInventoryDirector.Instance.AttachToWorld(State, tables, null);
        AlundraSubInventoryDirector.Instance.AttachToWorld(State, tables, null);

        Assert.True(Director.Start(ValidSave()));
        Tick();
        StartAndOpenPickerTail();
        AnswerAndClose(0);
        while (Director.State != AlundraSaveScreenDirector.StateWaitSquare)
        {
            Assert.True(Director.IsActive);
            Assert.Equal(AlundraGameState.PlayerControlBits.MenuOpen, State.PlayerControlFlags);
            Tick();
        }

        // J9: even with the flags cleared by hand, the save screen alone keeps the inventory shut.
        State.PlayerControlFlags = 0;
        InventoryTick(AlundraPadState.Start);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        State.PlayerControlFlags = AlundraGameState.PlayerControlBits.MenuOpen;

        Press(AlundraPadState.Square);
        TickUntilState(AlundraSaveScreenDirector.StateIdle);
        Assert.False(Director.IsActive);

        InventoryTick(0);
        InventoryTick(AlundraPadState.Start);
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
    }

    private void StartAndOpenPickerTail() => Tick(TicksToSettledPicker - 1);

    // ---- SE3: a world change -------------------------------------------------------------------------------------

    [Fact]
    public void WorldChange_WhileTheScreensChoiceWaits_ClosesIt_ReleasesTheFlags_WritesNothing_AndNothingStaysPushed()
    {
        Director.NoteBookFlowStarted();
        State.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
        StartAndOpenPicker();
        Press(AlundraPadState.Cross);
        Assert.True(Dialogue.IsAwaitingChoice);

        // The new world's install, in InitializeWithWorld's order: the dialogue systems, then the save screen.
        var newView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var world = SaveScreenTestWorlds.WorldWithUIView("NextWorld", newView);
        var proxy = new AlundraWorldProxy();
        using var logs = LogCapture.Install();
        proxy.InstallDialogueSystems(world);
        proxy.InstallSaveScreenSystems();

        Assert.False(Dialogue.IsAwaitingChoice);
        Assert.False(Director.IsActive);
        Assert.False(Director.IsBookFlowActive);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.Empty(newView.Pushed.Except(newView.Removed));
        Assert.Contains(logs.Warnings, w => w.Contains("world change cut the save flow"));

        Tick(300);
        Assert.Empty(_slots.SaveCalls);
    }

    [Fact]
    public void WorldChange_WithTheBookAtState2_TheRealInstallation_ReleasesEverything_WritesNothing()
    {
        var hero = HeroAt(tileX: 33, tileY: 59, tileZ: 0);
        var context = new BookContext(hero, Map389WorldName);
        var runner = new AlundraEventProgramRunner(null, State, context);
        var book = NewBook();
        State.PlayerStats.WeaponId = 1;

        book.EventTrigger = ScriptHelper.ProgramFInteract;
        book.RunPickedEvent(runner);
        book.EventTrigger = ScriptHelper.ProgramCTick;
        book.RunPickedEvent(runner);
        Assert.Equal(AlundraSaveBook.StateQuestion, AlundraSaveBook.ReadState(book));
        Assert.True(Dialogue.IsOpen);
        Assert.True(Director.IsBookFlowActive);
        Assert.NotEqual(0u, State.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked);

        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        AddHeroPawn(world);
        InitializeWithRealProject(new AlundraWorldProxy(), world);

        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.False(Director.IsActive);
        Assert.False(Director.IsBookFlowActive);
        Assert.False(Dialogue.IsOpen);
        Assert.False(Dialogue.IsAwaitingChoice);
        Assert.Empty(_slots.SaveCalls);
    }

    // ---- The book and the real screen ----------------------------------------------------------------------------

    [Fact]
    public void Book_WithTheRealScreen_Oui_WritesTheHerosCapture_ThenReleasesTheHero()
    {
        var hero = HeroAt(tileX: 20, tileY: 30, tileZ: 1);
        var context = new BookContext(hero, Map389WorldName);
        var runner = new AlundraEventProgramRunner(null, State, context);
        var book = NewBook();
        State.PlayerStats.WeaponId = 1;
        Assert.Same(Director, AlundraSaveBook.Instance.Screen);

        void BookTick(int count = 1, uint hold = 0)
        {
            for (var i = 0; i < count; i++)
            {
                book.EventTrigger = ScriptHelper.ProgramCTick;
                book.RunPickedEvent(runner);
                Dialogue.Tick();
                TickScreen(hold); // E19.f3a: the dialogue pass is this block's own (the helper Tick would add a second one)
            }
        }

        book.EventTrigger = ScriptHelper.ProgramFInteract;
        book.RunPickedEvent(runner);
        BookTick(1 + AlundraSaveBook.WaitTicks + 1);
        Assert.True(Dialogue.SelectChoiceForTests(0)); // the book's own OUI.
        BookTick(37 + AlundraSaveBook.WaitTicks + 1); // E19.f3a: 37 ticks to the answer, then the 61 of the wait
        Assert.True(Director.IsActive);

        BookTick(TicksToSettledPicker);
        Assert.True(Director.IsPickerActive);
        BookTick(hold: AlundraPadState.Cross);
        Assert.True(Dialogue.SelectChoiceForTests(0)); // the screen's OUI.
        for (var i = 0; i < 400 && Director.State != AlundraSaveScreenDirector.StateWaitSquare; i++)
        {
            BookTick();
        }

        var call = Assert.Single(_slots.SaveCalls);
        Assert.Equal("slot1", call.Slot);
        Assert.Equal((389, 20, 30, 1), (call.Data.InitialMapId, call.Data.CameraTileX, call.Data.CameraTileY, call.Data.CameraTileZ));

        BookTick(hold: AlundraPadState.Square);
        for (var i = 0; i < 100 && (Director.IsActive || AlundraSaveBook.ReadState(book) != AlundraSaveBook.StateIdle); i++)
        {
            BookTick();
        }

        Assert.Equal(AlundraSaveBook.StateIdle, AlundraSaveBook.ReadState(book));
        Assert.False(Director.IsBookFlowActive);
        Assert.Equal(0u, State.PlayerControlFlags);
    }

    [Fact]
    public void Book_WithTheRealScreen_ARefusedCapture_EndsInBoundedTicks_AfterSquare_WithEverythingReleased()
    {
        var hero = HeroAt(tileX: 20, tileY: 30, tileZ: 1);
        hero.TileX = 5000; // outside every map: the capture does not validate.
        var context = new BookContext(hero, Map389WorldName);
        var runner = new AlundraEventProgramRunner(null, State, context);
        var book = NewBook();
        State.PlayerStats.WeaponId = 1;

        void BookTick(int count = 1, uint hold = 0)
        {
            for (var i = 0; i < count; i++)
            {
                book.EventTrigger = ScriptHelper.ProgramCTick;
                book.RunPickedEvent(runner);
                Dialogue.Tick();
                TickScreen(hold); // E19.f3a: the dialogue pass is this block's own (the helper Tick would add a second one)
            }
        }

        book.EventTrigger = ScriptHelper.ProgramFInteract;
        book.RunPickedEvent(runner);
        BookTick(1 + AlundraSaveBook.WaitTicks + 1);
        Assert.True(Dialogue.SelectChoiceForTests(0));
        BookTick(37 + AlundraSaveBook.WaitTicks + 1); // E19.f3a
        Assert.True(Director.IsActive);
        BookTick(3);
        Assert.Equal((Failed0, Failed1), (Director.MessageLine0.Text, Director.MessageLine1.Text));

        BookTick(hold: AlundraPadState.Square);
        BookTick(60);

        Assert.Equal(AlundraSaveBook.StateIdle, AlundraSaveBook.ReadState(book));
        Assert.False(Director.IsActive);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.True(_slots.NothingCalled);
    }

    private static AlundraEntityScriptProxy NewBook()
    {
        var book = new AlundraEntityScriptProxy { SpriteType = AlundraSaveBook.SpriteType, Status = EntityStatus.Normal };
        book.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = 254;
        book.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = AlundraSaveBook.TickCode;
        book.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = AlundraSaveBook.InteractCode;
        return book;
    }

    // ---- Doubles -----------------------------------------------------------------------------------------------

    /// <summary>A fake service answering per slot (NotFound by default), recording every call.</summary>
    internal sealed class SlotsBySlot : IAlundraSaveSlots
    {
        public readonly Dictionary<string, (SaveGameLoadStatus Status, AlundraSaveGame? Save)> Answers = new();
        public readonly List<(string Slot, AlundraSaveGame Data, SaveGameFormat Format, IReadOnlyDictionary<string, string> Metadata)> SaveCalls = new();
        public readonly List<string> LoadCalls = new();
        public int ListCalls;
        public SaveGameSaveStatus SaveStatus = SaveGameSaveStatus.Saved;
        public bool ThrowOnLoad;
        public bool ThrowOnSave;

        public bool NothingCalled => SaveCalls.Count == 0 && LoadCalls.Count == 0 && ListCalls == 0;

        public AlundraSaveOutcome Save(string slot, AlundraSaveGame data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata)
        {
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("fake save throws");
            }

            SaveCalls.Add((slot, data, format, metadata));
            return new AlundraSaveOutcome(SaveStatus, string.Empty);
        }

        public AlundraLoadOutcome TryLoad(string slot, out AlundraSaveGame? data)
        {
            LoadCalls.Add(slot);
            if (ThrowOnLoad)
            {
                throw new InvalidOperationException("fake load throws");
            }

            var (status, save) = Answers.TryGetValue(slot, out var answer) ? answer : (SaveGameLoadStatus.NotFound, null);
            data = save; // hostile: an object even with a failure status.
            return new AlundraLoadOutcome(status, string.Empty);
        }

        public IReadOnlyList<AlundraSlotEntry> ListSlots()
        {
            ListCalls++;
            return Array.Empty<AlundraSlotEntry>();
        }
    }

    private sealed class RecordingSound : IAlundraSoundPlayer
    {
        public readonly List<int> Played = new();

        public void PlaySfx(int sfxId) => Played.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    private sealed class BookContext : IEntityWorldContext
    {
        private readonly string? _worldName;

        public BookContext(AlundraEntityScriptProxy hero, string? worldName)
        {
            PlayerEntity = hero;
            _worldName = worldName;
        }

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public AlundraEntityScriptProxy? PlayerEntity { get; }

        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }

        public NavigationGrid2D? NavigationGrid => null;

        public string? WorldName => _worldName;
    }
}

/// <summary>E16.e T3/T4: a headless world with a game whose active render view carries a given UI runtime - the
/// recipe of <c>AlundraDialoguePresenterWiringTests</c>.</summary>
internal static class SaveScreenTestWorlds
{
    internal static World WorldWithUIView(string name, IUIViewRuntime uiView)
    {
        var world = new World { Name = name };
        var game = (CasaEngine.Framework.Application.CasaEngineGame)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Application.CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game)
            .GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var gameManager = (CasaEngine.Framework.Application.GameManager)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Application.GameManager));
        var viewManager = new CasaEngine.Framework.Rendering.ViewManager();
        typeof(CasaEngine.Framework.Application.GameManager)
            .GetField("<ViewManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(gameManager, viewManager);
        typeof(CasaEngine.Framework.Application.CasaEngineGame)
            .GetField("<GameManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(game, gameManager);

        var view = (CasaEngine.Framework.Rendering.RenderView)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Rendering.RenderView));
        view.UIView = uiView;
        view.Enabled = true;
        view.IsVisible = true;
        viewManager.Add(view);
        viewManager.SetActive(view);

        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);
        return world;
    }
}
