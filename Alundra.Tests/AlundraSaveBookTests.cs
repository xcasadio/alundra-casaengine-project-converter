#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests;

/// <summary>
/// E16.e T2 (docs/plan-e16-etat-partie.md, L4, H1, H2, SE1, SE2, SE7, SE8): the save book, driven through the real
/// dispatch (<see cref="AlundraEntityScriptProxy.RunPickedEvent"/>) on the REAL dialogue director, with a real
/// <see cref="AlundraDialoguePresenter"/> over a recording UI view, the real export's <c>Dialogues/Etc.dialogue</c>
/// and validation rules (K1), and a fake save screen (<see cref="IAlundraSaveBookScreen"/>). Every abandon and the
/// normal end are followed by <see cref="AssertReleased"/>: the whole <c>PlayerControlFlags</c> at 0, no box, no
/// choice, then the hero walks and the inventory opens (SE1).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveBookTests : IDisposable
{
    private const int Wait = AlundraSaveBook.WaitTicks + 1; // 0x3C counted down to -1: 61 ticks.

    private readonly FakeScreen _screen = new();
    private readonly FakeSaveSlots _slots = new();
    private readonly AlundraEntityScriptProxy _hero = HeroAt(tileX: 20, tileY: 30, tileZ: 1);
    private readonly FakeContext _context;
    private readonly AlundraEventProgramRunner _runner;
    private readonly AlundraEntityScriptProxy _book;

    public AlundraSaveBookTests()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();

        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        AlundraDialogueDirector.Instance.AttachToWorld(new AlundraDialoguePresenter(new RecordingUIViewRuntime()), State);
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = RealRules;
        AlundraSaveGameDirector.Instance.SaveSlots = _slots;
        AlundraSaveBook.Instance.Screen = _screen;

        // A New Game state already carries the sword's slot (F10): a capture of it validates.
        State.PlayerStats.WeaponId = 1;

        _hero.TargetAnimationId = 1;
        _context = new FakeContext(_hero, Map389WorldName);
        _runner = new AlundraEventProgramRunner(null, State, _context);
        _book = new AlundraEntityScriptProxy { SpriteType = AlundraSaveBook.SpriteType, Status = EntityStatus.Normal };
        _book.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = 254;
        _book.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = AlundraSaveBook.TickCode;
        _book.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = AlundraSaveBook.InteractCode;
    }

    public void Dispose()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
    }

    private static AlundraGameState State => AlundraGameState.Instance;

    private static AlundraDialogueDirector Dialogue => AlundraDialogueDirector.Instance;

    private int BookState => AlundraSaveBook.ReadState(_book);

    private void Interact()
    {
        _book.EventTrigger = ScriptHelper.ProgramFInteract;
        _book.RunPickedEvent(_runner);
    }

    /// <summary>One logic tick as the frame runs it: the book's slot C, then the dialogue pass.</summary>
    private void Tick(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            _book.EventTrigger = ScriptHelper.ProgramCTick;
            _book.RunPickedEvent(_runner);
            Dialogue.Tick();
        }
    }

    /// <summary>F, then state 1 (the question box), then the 61-tick wait of state 2: the choice is asked.</summary>
    private void RunToTheQuestion()
    {
        Interact();
        Tick();
        Tick(Wait);
        Assert.Equal(AlundraSaveBook.StateAnswer, BookState);
        Assert.True(Dialogue.IsAwaitingChoice);
    }

    /// <summary>SE1: after an abandon or the end - the whole flags field at 0, no box, no choice; then, over a few
    /// ticks, the hero walks and the inventory opens.</summary>
    private void AssertReleased()
    {
        Assert.Equal(AlundraSaveBook.StateIdle, BookState);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.False(Dialogue.IsOpen);
        Assert.False(Dialogue.IsAwaitingChoice);

        Tick(5);
        Assert.Equal(0u, State.PlayerControlFlags);

        _hero.TargetAnimationId = 0;
        var right = new AlundraPadState { ButtonsHold = AlundraPadState.Right };
        AlundraPlayerManager.MovePlayer(_hero, in right, State, host: null);
        Assert.Equal(1u, _hero.TargetAnimationId); // Moving.

        var tables = new AlundraItemTables(FindProjectRoot());
        AlundraInventoryDirector.Instance.AttachToWorld(State, tables, null);
        AlundraSubInventoryDirector.Instance.AttachToWorld(State, tables, null);
        InventoryTick(AlundraPadState.Start);
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
    }

    // ---- The whole flow (H1) --------------------------------------------------------------------------------

    [Fact]
    public void Yes_RunsEveryStateWithTheOriginalWaits_CapturesTheHerosTile_AndStartsTheScreen()
    {
        Interact();
        Assert.Equal(AlundraSaveBook.StateMessage, BookState);
        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, State.PlayerControlFlags);
        Assert.Equal(0u, _hero.TargetAnimationId);
        Assert.Equal(1, _screen.FlowStarted);

        // State 1: the question in a box closable by script only (SetEtcAnimationMode(4)).
        Tick();
        Assert.Equal(AlundraSaveBook.StateQuestion, BookState);
        Assert.True(Dialogue.IsOpen);
        Assert.Equal(AlundraSaveBook.ScriptCloseOnlyMask, Dialogue.CloseMaskForTests);
        Assert.Equal("Enregistrer tes progrès?", Dialogue.CurrentLineForTests?.Text);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.ControlLocked | AlundraGameState.PlayerControlBits.MessageBox,
            State.PlayerControlFlags);

        // State 2: 0x3C counted down to -1.
        Tick(Wait - 1);
        Assert.Equal(AlundraSaveBook.StateQuestion, BookState);
        Assert.False(Dialogue.IsAwaitingChoice);
        Tick();
        Assert.Equal(AlundraSaveBook.StateAnswer, BookState);
        Assert.True(Dialogue.IsAwaitingChoice);
        Assert.Equal(new[] { "OUI", "NON" }, Dialogue.ChoicesForTests);

        // State 4: OUI closes the box first, then waits.
        Assert.True(Dialogue.SelectChoiceForTests(0));
        Tick();
        Assert.Equal(AlundraSaveBook.StateCapture, BookState);
        Assert.False(Dialogue.IsOpen);
        Assert.False(Dialogue.IsAwaitingChoice);
        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, State.PlayerControlFlags);

        // State 5: 61 ticks, then the capture and the screen.
        Tick(Wait - 1);
        Assert.Empty(_screen.Started);
        Tick();
        Assert.Equal(AlundraSaveBook.StateWaitScreen, BookState);
        var save = Assert.Single(_screen.Started);
        Assert.Equal(389, save.InitialMapId);
        Assert.Equal((20, 30, 1), (save.CameraTileX, save.CameraTileY, save.CameraTileZ));
        Assert.Equal(0, _screen.FailuresStarted);
        Assert.True(_slots.NothingCalled); // the book itself never writes.

        // State 6: waits for the screen's end, then the reset.
        Tick(50);
        Assert.Equal(AlundraSaveBook.StateWaitScreen, BookState);
        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, State.PlayerControlFlags);
        _screen.IsActive = false;
        Tick();
        Assert.Equal(1, _screen.FlowEnded);
        AssertReleased();
    }

    [Fact]
    public void No_Abandons_WithTheBoxClosed_AndNothingStarted()
    {
        RunToTheQuestion();

        Assert.True(Dialogue.SelectChoiceForTests(1));
        Tick();

        Assert.Empty(_screen.Started);
        Assert.Equal(0, _screen.FailuresStarted);
        Assert.Equal(1, _screen.FlowEnded);
        AssertReleased();
    }

    // ---- SE2: every observable failure abandons -------------------------------------------------------------

    [Fact]
    public void State1_ADialogueAlreadyOpen_Abandons_WithoutOpeningAnything()
    {
        Interact();
        Dialogue.Open(null, null, controlMode: 5); // another box, touching no flag.
        var serial = Dialogue.OpenSerial;

        Tick();

        Assert.Equal(AlundraSaveBook.StateIdle, BookState);
        Assert.Equal(serial, Dialogue.OpenSerial);
        Assert.True(Dialogue.IsOpen); // not the book's: left alone.
        Dialogue.NotifyPresenterClosed();
        AssertReleased();
    }

    [Fact]
    public void State1_AChoiceAlreadyWaiting_Abandons()
    {
        Interact();
        Dialogue.OpenChoice(new[] { "A", "B" });
        var serial = Dialogue.OpenSerial;

        Tick();

        Assert.Equal(AlundraSaveBook.StateIdle, BookState);
        Assert.Equal(serial, Dialogue.OpenSerial);
        Assert.True(Dialogue.IsAwaitingChoice); // not the book's: left alone.
        Dialogue.CancelChoice();
        AssertReleased();
    }

    [Fact]
    public void State1_NoPresenter_Abandons_WithoutOpeningAnything()
    {
        Dialogue.AttachToWorld(null, State);
        Interact();
        var serial = Dialogue.OpenSerial;

        Tick();

        Assert.Equal(serial, Dialogue.OpenSerial);
        Assert.Equal(1, _screen.FlowEnded);
        AssertReleased();
    }

    [Fact]
    public void State1_QuestionTextUnresolved_Abandons()
    {
        AlundraEtcStringTable.ResetForTests(); // no dialogue_etc asset at all.
        Interact();

        Tick();

        Assert.False(Dialogue.IsOpen);
        AssertReleased();
    }

    [Fact]
    public void State2_NoPresenter_Abandons_AndClosesTheBooksBox()
    {
        Interact();
        Tick();
        Assert.True(Dialogue.IsOpen);

        Dialogue.AttachToWorld(null, State);
        Tick(Wait);

        Assert.Empty(_screen.Started);
        AssertReleased();
    }

    /// <summary>H2: the original parks the book in state 3 when the question cannot be asked - the book froze and the
    /// hero stayed locked. Here the only observable failures (no presenter, above; texts that do not resolve, here)
    /// abandon.</summary>
    [Fact]
    public void State2_ChoiceTextsUnresolved_Abandons_NeverParksInState3()
    {
        Interact();
        Tick();
        Assert.True(Dialogue.IsOpen);

        AlundraEtcStringTable.ResetForTests(); // 0x41/0x42 no longer resolve.
        Tick(Wait);

        Assert.NotEqual(3, BookState);
        Assert.False(Dialogue.IsAwaitingChoice);
        AssertReleased();
    }

    [Fact]
    public void State4_AnotherOpenClearsTheQuestion_Abandons_LeavingThatBoxToItsOwner()
    {
        RunToTheQuestion();

        Dialogue.Open(null, null, controlMode: 1); // another box: clears the waiting choice.
        var serial = Dialogue.OpenSerial;
        Tick();

        Assert.Equal(AlundraSaveBook.StateIdle, BookState);
        Assert.Empty(_screen.Started);
        Assert.Equal(0, _screen.FailuresStarted);
        Assert.Equal(serial, Dialogue.OpenSerial);
        Assert.True(Dialogue.IsOpen); // the other box is not the book's.
        Assert.Equal(0u, State.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked);

        Dialogue.NotifyPresenterClosed();
        AssertReleased();
    }

    // ---- SE7: the book cannot be restarted in mid-flow --------------------------------------------------------

    [Fact]
    public void SlotF_RepeatedInMidFlow_IsIgnored_AtStates2_4And6()
    {
        Interact();
        Tick();
        Tick(10);
        var delay = _book.DelayOrAngleOrEntityId;
        Interact();
        Assert.Equal(AlundraSaveBook.StateQuestion, BookState);
        Assert.Equal(delay, _book.DelayOrAngleOrEntityId);

        Tick(Wait - 10);
        Assert.Equal(AlundraSaveBook.StateAnswer, BookState);
        var serial = Dialogue.OpenSerial;
        Interact();
        Assert.Equal(AlundraSaveBook.StateAnswer, BookState);
        Assert.Equal(serial, Dialogue.OpenSerial);
        Assert.True(Dialogue.IsAwaitingChoice);

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);
        Assert.Equal(AlundraSaveBook.StateWaitScreen, BookState);
        Interact();
        Assert.Equal(AlundraSaveBook.StateWaitScreen, BookState);
        Assert.Single(_screen.Started);
        Assert.Equal(1, _screen.FlowStarted);

        _screen.IsActive = false;
        Tick();
        AssertReleased();
    }

    [Fact]
    public void State5_ScreenAlreadyActive_StartRefused_Abandons()
    {
        RunToTheQuestion();
        _screen.RefuseStart = true;

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);

        Assert.Single(_screen.Started); // asked, refused.
        Assert.Equal(1, _screen.FlowEnded);
        AssertReleased();
    }

    // ---- SE8/SE4: a refused capture writes nothing -----------------------------------------------------------

    [Fact]
    public void State5_CaptureRefusedByTheValidation_StartsTheFailureMessage_AndWritesNothing()
    {
        State.PlayerStats.HpMax = 60; // outside 0..50: TryValidate refuses.
        RunToTheQuestion();
        using var log = LogCapture.Install();

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);

        Assert.Empty(_screen.Started);
        Assert.Equal(1, _screen.FailuresStarted);
        Assert.True(_slots.NothingCalled);
        Assert.Contains(log.Warnings, line => line.Contains("nothing written") && line.Contains("playerStats.hpMax"));
        Assert.Equal(AlundraSaveBook.StateWaitScreen, BookState);
    }

    [Fact]
    public void State5_RulesFactoryThrows_StartsTheFailureMessage()
    {
        AlundraSaveGameDirector.Instance.RulesFactoryForTests = () => throw new InvalidOperationException("boom");
        RunToTheQuestion();

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);

        Assert.Empty(_screen.Started);
        Assert.Equal(1, _screen.FailuresStarted);
        Assert.True(_slots.NothingCalled);
    }

    [Fact]
    public void State5_NoWorldName_StartsTheFailureMessage()
    {
        _context.WorldNameValue = null;
        RunToTheQuestion();

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);

        Assert.Empty(_screen.Started);
        Assert.Equal(1, _screen.FailuresStarted);
    }

    /// <summary>SE8: a refused capture ends, in a bounded number of ticks, with the book at rest, every flag clear
    /// and the screen inactive - here the fake screen plays the failure chain (MenuOpen from its first state to its
    /// last, as <c>0x2710</c>..<c>0x63</c> do).</summary>
    [Fact]
    public void State5_CaptureRefused_EndsInBoundedTicks_WithEverythingReleased()
    {
        State.PlayerStats.HpMax = 60;
        _screen.FailureLengthTicks = 40;
        RunToTheQuestion();

        Dialogue.SelectChoiceForTests(0);
        var ticks = 0;
        do
        {
            Tick();
            _screen.Step(State);
            ticks++;
        }
        while ((BookState != AlundraSaveBook.StateIdle || _screen.IsActive) && ticks < 500);

        Assert.True(ticks < 500);
        Assert.Equal(1, _screen.FailuresStarted);
        Assert.False(_screen.IsActive);
        Assert.Equal(0u, State.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);
        State.PlayerStats.HpMax = 10;
        AssertReleased();
    }

    [Fact]
    public void State5_NoScreenWired_Abandons()
    {
        AlundraSaveBook.Instance.Screen = null;
        RunToTheQuestion();

        Dialogue.SelectChoiceForTests(0);
        Tick(1 + Wait);

        AssertReleased();
    }

    // ---- Fakes ------------------------------------------------------------------------------------------------

    private sealed class FakeScreen : IAlundraSaveBookScreen
    {
        public readonly List<AlundraSaveGame> Started = new();
        public int FailuresStarted;
        public int FlowStarted;
        public int FlowEnded;
        public bool RefuseStart;
        public int FailureLengthTicks;
        private int _failureTicksLeft;

        public bool IsActive { get; set; }

        public bool Start(AlundraSaveGame save)
        {
            Started.Add(save);
            if (RefuseStart)
            {
                return false;
            }

            IsActive = true;
            return true;
        }

        public bool StartFailure()
        {
            FailuresStarted++;
            IsActive = true;
            _failureTicksLeft = FailureLengthTicks;
            return true;
        }

        /// <summary>Plays a failure chain: MenuOpen posed, then cleared on its last tick with the screen's end.</summary>
        public void Step(AlundraGameState state)
        {
            if (!IsActive)
            {
                return;
            }

            state.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
            if (--_failureTicksLeft <= 0)
            {
                state.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
                IsActive = false;
            }
        }

        public void NoteBookFlowStarted() => FlowStarted++;

        public void NoteBookFlowEnded() => FlowEnded++;
    }

    private sealed class FakeContext : IEntityWorldContext
    {
        public FakeContext(AlundraEntityScriptProxy hero, string? worldName)
        {
            PlayerEntity = hero;
            WorldNameValue = worldName;
        }

        public string? WorldNameValue;

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

        public string? WorldName => WorldNameValue;
    }

    internal sealed class RecordingUIViewRuntime : IUIViewRuntime
    {
        public readonly List<IUIScreen> Pushed = new();
        public readonly List<IUIScreen> Removed = new();

        public bool IsPointerOverUI => false;
        public bool IsPointerCaptured => false;
        public bool IsKeyboardCaptured => false;
        public UIViewInputState InputState => UIViewInputState.Empty;
        public bool HasModalInput => false;
        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }
        public void Draw() { }
        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;
        public void PushScreen(IUIScreen screen) => Pushed.Add(screen);
        public IUIScreen? PopScreen() => null;
        public void RemoveScreen(IUIScreen screen) => Removed.Add(screen);
        public void Dispose() { }
    }
}
