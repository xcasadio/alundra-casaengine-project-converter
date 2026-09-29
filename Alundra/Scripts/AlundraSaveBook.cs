#nullable enable
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// E16.e L4 (docs/plan-e16-etat-partie.md): what the save book needs from the save screen - the port of the
/// original's <c>g_globalTransitionState</c> seen from the book (L5). The save screen's director implements it;
/// the book's tests fake it.
/// </summary>
internal interface IAlundraSaveBookScreen
{
    /// <summary>True while the save screen runs (L5: from state <c>0x2710</c> to the end of <c>0x63</c>) - the book's
    /// state 6 waits for it to turn false, as the original waits for <c>g_globalTransitionState == 0</c>.</summary>
    bool IsActive { get; }

    /// <summary>Starts the save screen with the validated capture <paramref name="save"/>; false, without starting
    /// anything, while <see cref="IsActive"/> (SE7).</summary>
    bool Start(AlundraSaveGame save);

    /// <summary>Starts the save screen straight on the failure message <c>0x15</c> and the common end chain, writing
    /// nothing (SE8); false, without starting anything, while <see cref="IsActive"/>.</summary>
    bool StartFailure();

    /// <summary>L5/SE3: a book flow begins (slot F) - the screen's map entry clears the flags a flow left behind.</summary>
    void NoteBookFlowStarted();

    /// <summary>L5/SE1: the book flow ended (abandon or reset).</summary>
    void NoteBookFlowEnded();
}

/// <summary>
/// E16.e L1 (docs/plan-e16-etat-partie.md): the save book, « SaveBook (Ne pas toucher !) » - sprite type 237,
/// one per map on 65 maps (§2, Q4) - the ONLY entity whose native sprite AI this DLL runs. Its two native
/// handlers are reached through <see cref="AlundraEventProgramRunner.RunSpriteEvent"/>, which dispatches here only
/// when <see cref="TryMatch"/> holds: slot F code 1 (<c>Script_FInteract_FUN_8007fc64</c>, 0x8007FC64) and slot C
/// code 72 (<c>AI_ProcessWarpTransitionState</c>, 0x8007B998, entry 72 of the <c>ProgramCTick</c> table at
/// 0x800C4F34, word 0x800C5054). Porting any other native handler is E14's, never this class's.
///
/// <para><b>The book (L4, H1).</b> A port of <c>AI_ProcessWarpTransitionState</c> (0x8007B998-0x8007BB2C, jump table
/// 0x80028204, <c>FunctionTypeC.cs:6823-6951</c>): its state is the four <see cref="AlundraEntityScriptProxy.Bytes"/>
/// read as one little-endian int (entity <c>+0x274</c>), its wait counter
/// <see cref="AlundraEntityScriptProxy.DelayOrAngleOrEntityId"/> (<c>+0x278</c>). State 1 shows ETC <c>0x40</c>
/// « Enregistrer tes progrès? » in the ordinary box, closable by script only; state 2, 61 ticks later, asks OUI/NON
/// (ETC <c>0x41</c>/<c>0x42</c>); state 4 takes the answer and closes the box; state 5, 61 ticks after OUI,
/// captures and validates the save and starts the save screen; state 6 waits for the screen to end, then resets.</para>
///
/// <para><b>Corrections of the original</b> (author's rule: a proven defect of the original is fixed, never
/// reproduced):</para>
/// <list type="bullet">
/// <item><description>H2: when <c>InitializeAsyncOperation</c> fails, the original parks the book in state 3, which
/// does nothing - the book freezes and the hero stays locked (<c>0x8007BA5C</c>-<c>0x8007BA7C</c>). Here every
/// observable failure of the question is an abandon (SE2).</description></item>
/// <item><description>SE7: slot F restarts the original's book at state 1 even in the middle of a flow; here it is
/// ignored unless the book is at rest (state 0).</description></item>
/// <item><description>SE1: no path leaves the hero locked, a box of the book open, or a choice of the book
/// waiting - the abandon and the reset close the box the book owns (<see cref="AlundraDialogueDirector.OpenSerial"/>
/// unchanged since its <see cref="AlundraDialogueDirector.Open"/>), cancel its choice, and clear
/// <c>ControlLocked</c>.</description></item>
/// </list>
/// <para>The book does not reuse opcode <c>0x44</c>'s degraded rule, which takes a missing presenter for a OUI
/// (<c>AlundraEventProgramRunner.cs:704-710</c>): without a presenter the book abandons.</para>
/// </summary>
public sealed class AlundraSaveBook
{
    /// <summary>The one session instance.</summary>
    public static readonly AlundraSaveBook Instance = new();

    private AlundraSaveBook()
    {
    }

    /// <summary>§2, Q4: the save book's sprite type (<see cref="AlundraEntityScriptProxy.SpriteType"/>), line 239 of
    /// the analyser's <c>EntityNames.csv</c> (index 237 once its header line is skipped).</summary>
    public const int SpriteType = 237;

    /// <summary>Slot F code of <c>Script_FInteract_FUN_8007fc64</c> (<c>SpriteEventHandlers.cs:233-234</c>).</summary>
    internal const int InteractCode = 1;

    /// <summary>Slot C code of <c>AI_ProcessWarpTransitionState</c> (§2, Q4: entry 72 of the <c>ProgramCTick</c>
    /// table).</summary>
    internal const int TickCode = 72;

    /// <summary>ETC <c>0x40</c>, « Enregistrer tes progrès? » (0x8007B9FC).</summary>
    internal const int QuestionEtcIndex = 0x40;

    /// <summary>ETC <c>0x41</c>/<c>0x42</c>, « OUI »/« NON » of the book's question (0x8007BA38-0x8007BA48).</summary>
    internal const int YesEtcIndex = 0x41;

    internal const int NoEtcIndex = 0x42;

    /// <summary>The Yarn node of ETC <c>0x40</c> in <c>dialogue_etc</c> (decimal, four digits).</summary>
    internal const string QuestionNode = "Etc_0064";

    /// <summary><c>SetEtcAnimationMode(4)</c> (0x8004507C, opcode <c>0x50</c>'s mask): the box closes by script only.</summary>
    internal const int ScriptCloseOnlyMask = 4;

    /// <summary>The wait written at states 1 and 4 (<c>0x3C</c>); the next state counts it down to -1, so the wait
    /// lasts 61 ticks (<c>FunctionTypeC.cs:6858-6868</c>, <c>:6901-6913</c>).</summary>
    internal const int WaitTicks = 0x3C;

    internal const int StateIdle = 0;
    internal const int StateMessage = 1;
    internal const int StateQuestion = 2;
    internal const int StateAnswer = 4;
    internal const int StateCapture = 5;
    internal const int StateWaitScreen = 6;

    private const string LogPrefix = "AlundraSaveBook: ";

    /// <summary>
    /// L4/L5: the save screen, as the book sees it - the session's <see cref="AlundraSaveScreenDirector"/> (E16.e T3).
    /// Null makes state 5 abandon (nothing can be shown), logged. Tests pose a fake.
    /// </summary>
    internal IAlundraSaveBookScreen? Screen { get; set; } = ProductionScreen;

    /// <summary>E16.e T5: the production screen, one source for the default and for <see cref="ResetForTests"/> - so
    /// the end-to-end test, which never replaces it, pins the production wiring.</summary>
    private static IAlundraSaveBookScreen ProductionScreen => AlundraSaveScreenDirector.Instance;

    /// <summary>L4/SE1: <see cref="AlundraDialogueDirector.OpenSerial"/> right after the book's own
    /// <see cref="AlundraDialogueDirector.Open"/>, or null when the book owns no box.</summary>
    private int? _ownedOpenSerial;

    /// <summary>L4/SE1: true from the book's <see cref="AlundraDialogueDirector.OpenChoice"/> until its answer is
    /// taken - the choice to cancel on an abandon.</summary>
    private bool _choiceOpened;

    /// <summary>
    /// L1: true when <paramref name="entity"/>'s picked event (<see cref="AlundraEntityScriptProxy.EventTrigger"/>) is
    /// one of the save book's two native handlers - ALL of: sprite type <see cref="SpriteType"/>, a native slot
    /// (<c>ProgramIndexes[slot] &amp; 0x7f == 0</c>, the test <see cref="AlundraEntityScriptProxy.RunPickedEvent"/>
    /// already made), and (slot, <c>SpriteProgramIndexes[slot]</c>) either (F, <see cref="InteractCode"/>) or (C,
    /// <see cref="TickCode"/>). <paramref name="slot"/> is the matched slot.
    /// </summary>
    internal static bool TryMatch(AlundraEntityScriptProxy entity, out int slot)
    {
        slot = entity.EventTrigger;
        if (entity.SpriteType != SpriteType || slot < 0 || slot >= entity.ProgramIndexes.Length)
        {
            return false;
        }

        if ((entity.ProgramIndexes[slot] & 0x7f) != 0)
        {
            return false;
        }

        var code = entity.SpriteProgramIndexes[slot];
        return (slot == ScriptHelper.ProgramFInteract && code == InteractCode)
            || (slot == ScriptHelper.ProgramCTick && code == TickCode);
    }

    /// <summary>The book's state: the four <see cref="AlundraEntityScriptProxy.Bytes"/> as one little-endian int
    /// (<c>ReadWarpState</c>, <c>FunctionTypeC.cs:6937-6941</c>).</summary>
    internal static int ReadState(AlundraEntityScriptProxy book)
        => book.Bytes[0] | (book.Bytes[1] << 8) | (book.Bytes[2] << 16) | (book.Bytes[3] << 24);

    private static void WriteState(AlundraEntityScriptProxy book, int state)
    {
        book.Bytes[0] = (byte)(state & 0xFF);
        book.Bytes[1] = (byte)((state >> 8) & 0xFF);
        book.Bytes[2] = (byte)((state >> 16) & 0xFF);
        book.Bytes[3] = (byte)((state >> 24) & 0xFF);
    }

    /// <summary>
    /// Slot F code 1: port of <c>Script_FInteract_FUN_8007fc64</c> (0x8007FC64, <c>SpriteEventHandlers.cs:270-277</c>)
    /// for the book only (J6: other entities of code 1 stay a no-op) - state 1, the HERO's
    /// <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> to 0, <c>ControlLocked</c>, and the flow noted to the
    /// screen. SE7: ignored while the book is not at rest (the original restarts it at state 1 in mid-flow).
    /// </summary>
    internal void RunInteract(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state)
    {
        if (ReadState(book) != StateIdle)
        {
            return;
        }

        WriteState(book, StateMessage);
        if (context.PlayerEntity is { } player)
        {
            player.TargetAnimationId = 0;
        }

        state.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
        _ownedOpenSerial = null;
        _choiceOpened = false;
        Screen?.NoteBookFlowStarted();
    }

    /// <summary>
    /// Slot C code 72: port of <c>AI_ProcessWarpTransitionState</c> (0x8007B998), states 1, 2, 4, 5 and 6, with the
    /// corrections of the class doc. Any other state does nothing, as in the original (state 0: at rest).
    /// </summary>
    internal void RunTick(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state)
    {
        var dialogue = AlundraDialogueDirector.Instance;

        switch (ReadState(book))
        {
            case StateMessage:
                RunMessage(book, state, dialogue);
                return;

            case StateQuestion:
                RunQuestion(book, state, dialogue);
                return;

            case StateAnswer:
                RunAnswer(book, state, dialogue);
                return;

            case StateCapture:
                RunCapture(book, context, state, dialogue);
                return;

            case StateWaitScreen:
                // 0x8007BAEC: wait for g_globalTransitionState == 0 - the save screen's end (L5).
                if (Screen is { IsActive: true })
                {
                    return;
                }

                Reset(book, state, dialogue, null);
                return;
        }
    }

    /// <summary>State 1 (0x8007B9E4): the question text in a box closable by script only, then a 61-tick wait. SE2:
    /// abandon, opening nothing, without a presenter, while a box is open or a choice waits (the equivalent of the
    /// original's <c>0x80045004</c> test), or when ETC <c>0x40</c> does not resolve.</summary>
    private void RunMessage(AlundraEntityScriptProxy book, AlundraGameState state, AlundraDialogueDirector dialogue)
    {
        if (!dialogue.HasPresenter)
        {
            Reset(book, state, dialogue, "no dialogue presenter to show the question");
            return;
        }

        if (dialogue.IsOpen || dialogue.IsAwaitingChoice)
        {
            Reset(book, state, dialogue, "a dialogue box or a choice is already up");
            return;
        }

        var asset = AlundraEtcStringTable.EtcDialogueAsset;
        if (asset == null || !AlundraEtcStringTable.TryResolveText(QuestionEtcIndex, out _))
        {
            Reset(book, state, dialogue, $"ETC 0x{QuestionEtcIndex:X2} does not resolve");
            return;
        }

        dialogue.Open(asset, QuestionNode, 1);
        _ownedOpenSerial = dialogue.OpenSerial;
        dialogue.SetCloseMask(ScriptCloseOnlyMask);
        book.DelayOrAngleOrEntityId = WaitTicks;
        WriteState(book, StateQuestion);
    }

    /// <summary>State 2 (0x8007BA18): after the wait, OUI/NON. SE2/H2: without a presenter, or when ETC
    /// <c>0x41</c>/<c>0x42</c> do not resolve - the only observable failures of the question - abandon instead of the
    /// original's park in state 3.</summary>
    private void RunQuestion(AlundraEntityScriptProxy book, AlundraGameState state, AlundraDialogueDirector dialogue)
    {
        book.DelayOrAngleOrEntityId--;
        if (book.DelayOrAngleOrEntityId != -1)
        {
            return;
        }

        if (!dialogue.HasPresenter)
        {
            Reset(book, state, dialogue, "no dialogue presenter to ask OUI/NON");
            return;
        }

        if (!AlundraEtcStringTable.TryResolveText(YesEtcIndex, out var yes)
            || !AlundraEtcStringTable.TryResolveText(NoEtcIndex, out var no))
        {
            Reset(book, state, dialogue, $"ETC 0x{YesEtcIndex:X2}/0x{NoEtcIndex:X2} do not resolve");
            return;
        }

        dialogue.OpenChoice(new[] { yes, no });
        _choiceOpened = true;
        WriteState(book, StateAnswer);
    }

    /// <summary>State 4 (0x8007BA80): the answer. The box of the book is closed first (<c>TryActivateTextHoldState</c>,
    /// <c>FunctionTypeC.cs:6893</c>, SE1); NON abandons, OUI waits 61 ticks. SE2: a choice another
    /// <see cref="AlundraDialogueDirector.Open"/> cleared (no result, nothing waiting) abandons.</summary>
    private void RunAnswer(AlundraEntityScriptProxy book, AlundraGameState state, AlundraDialogueDirector dialogue)
    {
        var answer = dialogue.TakeChoiceResult();
        if (answer == null)
        {
            if (!dialogue.IsAwaitingChoice)
            {
                Reset(book, state, dialogue, "the question was cleared before any answer");
            }

            return;
        }

        _choiceOpened = false;
        CloseOwnedBox(dialogue);

        if (answer.Value != 1)
        {
            Reset(book, state, dialogue, null); // NON: the original's own abandon, nothing to report.
            return;
        }

        book.DelayOrAngleOrEntityId = WaitTicks;
        WriteState(book, StateCapture);
    }

    /// <summary>State 5 (0x8007BAC4): after the wait, the port of <c>UpdateSavedData</c> (0x8003153C) - capture at the
    /// hero's map and tile, then the validation of the capture (E16.c C5). SE4/SE8: without rules, or a capture that
    /// does not validate, the screen starts on its failure message and nothing is written; SE7: a screen already
    /// active abandons.</summary>
    private void RunCapture(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state, AlundraDialogueDirector dialogue)
    {
        book.DelayOrAngleOrEntityId--;
        if (book.DelayOrAngleOrEntityId != -1)
        {
            return;
        }

        var screen = Screen;
        if (screen == null)
        {
            Reset(book, state, dialogue, "no save screen is wired");
            return;
        }

        var refusal = CaptureAndValidate(context, state, out var save);
        if (refusal != null)
        {
            Logs.WriteWarning(LogPrefix + $"the save is refused, nothing written: {refusal}");
            if (!screen.StartFailure())
            {
                Reset(book, state, dialogue, "the save screen is already active");
                return;
            }
        }
        else if (!screen.Start(save!))
        {
            Reset(book, state, dialogue, "the save screen is already active");
            return;
        }

        WriteState(book, StateWaitScreen);
    }

    private static string? CaptureAndValidate(IEntityWorldContext context, AlundraGameState state, out AlundraSaveGame? save)
    {
        save = null;
        var worldName = context.WorldName;
        if (!AlundraSaveGame.TryCaptureFromWorld(state, worldName ?? string.Empty, context.PlayerEntity, out save) || save == null)
        {
            return $"nothing captured: no hero, or world '{worldName}' carries no map id.";
        }

        if (!AlundraSaveGameDirector.Instance.TryCreateRules(out var rules, out var rulesError))
        {
            save = null;
            return $"no validation rules: {rulesError}";
        }

        if (!save.TryValidate(rules!, out var validationError))
        {
            save = null;
            return $"the capture does not validate: {validationError}";
        }

        return null;
    }

    /// <summary>SE1: closes the box the book opened, only while <see cref="AlundraDialogueDirector.OpenSerial"/> is
    /// still the one it remembered - otherwise another <see cref="AlundraDialogueDirector.Open"/> replaced it, and the
    /// box on screen is not the book's.</summary>
    private void CloseOwnedBox(AlundraDialogueDirector dialogue)
    {
        if (_ownedOpenSerial is { } serial && serial == dialogue.OpenSerial && dialogue.IsOpen)
        {
            dialogue.RequestScriptClose();
        }

        _ownedOpenSerial = null;
    }

    /// <summary>
    /// The abandon and the reset (<c>ResetWarpState</c>, <c>FunctionTypeC.cs:6931-6935</c>), completed by SE1: close the
    /// box the book owns, cancel the choice it opened, clear <c>ControlLocked</c>, state 0, and end the flow at the
    /// screen. <paramref name="reason"/> is logged when not null.
    /// </summary>
    private void Reset(AlundraEntityScriptProxy book, AlundraGameState state, AlundraDialogueDirector dialogue, string? reason)
    {
        if (reason != null)
        {
            Logs.WriteWarning(LogPrefix + $"flow abandoned: {reason}.");
        }

        var ownedBoxStillUp = _ownedOpenSerial is { } serial && serial == dialogue.OpenSerial;
        CloseOwnedBox(dialogue);

        if (_choiceOpened && ownedBoxStillUp)
        {
            dialogue.CancelChoice();
        }

        _choiceOpened = false;
        state.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.ControlLocked;
        WriteState(book, StateIdle);
        Screen?.NoteBookFlowEnded();
    }

    /// <summary>SE3: forgets the box and the choice of a flow cut by a world change - the new world's dialogue
    /// director already dropped both (<see cref="AlundraDialogueDirector.InstallForMapEntry"/>).</summary>
    internal void ForgetFlow()
    {
        _ownedOpenSerial = null;
        _choiceOpened = false;
    }

    /// <summary>Test-only: clears the session state and puts the production screen back.</summary>
    internal void ResetForTests()
    {
        ForgetFlow();
        Screen = ProductionScreen;
    }
}
