#nullable enable
using System;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.SaveGames;

namespace Alundra.Scripts;

/// <summary>
/// E16.e L2/L3/L5 (docs/plan-e16-etat-partie.md, D-E16-34 to D-E16-37): the save screen the save book starts - a
/// pure, tick-driven port of the save flow of <c>StartMemoryCardProcess</c> (0x8005F458) and of its two UI
/// transitions, the message box (transition 9, <c>Fun_80050ec8</c>, 0x80050EC8) and the slot picker (transition 10,
/// <c>InitializeMemoryCardMenu</c> 0x800583EC then <c>DisplayMemoryCardMenu</c> 0x80058F24). SESSION-scoped
/// singleton without any MGUI dependency, same director/presenter/screen split as <see cref="AlundraInventoryDirector"/>
/// (J8): everything the screen draws is exposed as plain read-only state. It replaces the original's
/// <c>g_globalTransitionState</c>: <see cref="IsActive"/> is true from state <c>0x2710</c> to the end of <c>0x63</c>.
///
/// <para><b>States (L2).</b> Kept, in the original's order: <c>0x2710</c> (message ETC <c>0x87</c> « Examen de la
/// Carte Mémoire . . . »), the waits <c>0x2711</c> and <c>0x3f4</c> (which closes that message), <c>0x0C</c> (the
/// four labels, then the picker, headings ETC <c>0x83</c>/<c>0x84</c>), <c>0x3f8</c> (message <c>0xA5</c>),
/// <c>0x10</c> (the write, ADAPTED: <see cref="IAlundraSaveSlots.Save"/> of the captured object to <c>slot{n}</c>, in
/// binary), <c>0x3fe</c> then <c>0x16</c> (message <c>0xB1</c>), <c>0x3fd</c> then <c>0x15</c> (failure message
/// <c>0xAF</c>/<c>0xB0</c>), and the common end chain <c>0x3f5</c> (waits for Square), <c>0x0D</c>, <c>0x3f6</c>,
/// <c>0x0E</c> (the gauge put back before it hides), <c>0x44B</c>, <c>0x63</c>. The NON path is <c>0x3f8</c> →
/// <c>0x10</c> → <c>0x3f6</c> → <c>0x0E</c> → <c>0x44B</c> → <c>0x63</c>. Every state that only serves the PS1
/// card (port scan, format, free space, delete-when-full, block reservation, icon and checksum, absent, changed or
/// unformattable card) has no object with four fixed files: <c>0x2711</c> leads straight to <c>0x3f4</c>. The waits
/// keep the original's lengths (<c>AdvanceFadeOldCheck</c>, 0x13 and 0x0B): they are timers, not fades.</para>
///
/// <para><b>Labels (L3, D-E16-36).</b> Once per opening, at the entry of <c>0x0C</c> and never during the picker
/// (SE9): <see cref="IAlundraSaveSlots.TryLoad"/> then <see cref="AlundraSaveGame.TryValidate"/> of <c>slot1</c> to
/// <c>slot4</c>, with the guarded rules factory of E16.d (<see cref="AlundraSaveGameDirector.TryCreateRules"/>). A
/// valid slot shows, recomputed, the chapter's name (ETC text <see cref="AlundraChapterFlags.GetFirstEnabledFlagIndex"/>)
/// on line 1 and <see cref="AlundraSaveGame.BuildSummary"/> on line 2, the order of the binary (O-E16-18,
/// 0x80060128-0x80060134, 0x80058D74-0x80058DC4). Any other case shows what the binary shows for an empty block of
/// the file: two empty lines in a box still drawn (<see cref="EmptySlotLine"/>). The file's metadata is never read;
/// the loaded objects live only in that computation (SE4).</para>
///
/// <para><b>Corrections of the original</b> (author's rule: a proven defect of the original is fixed, never
/// reproduced):</para>
/// <list type="bullet">
/// <item><description>The box tint of the picker (<c>FUN_80059e0c</c>, 0x80059E0C) snaps to its target only when its
/// counter EQUALS the duration (0x80059E1C) and keeps counting afterwards, so each later call interpolates past the
/// target (18/15 of the way after an opening, 17/15 after a scroll: the middle box rests at 0x99 or 0x8C instead of
/// 0x80, a leaving box goes below 0). Here the tint stays on its target from the duration on.</description></item>
/// </list>
/// </summary>
public sealed class AlundraSaveScreenDirector : IAlundraSaveBookScreen
{
    /// <summary>The one session instance, built in a nested holder so the class's own static tables
    /// (<see cref="SlotNames"/>, <see cref="RowY"/>), which the constructor reads, are initialized first.</summary>
    public static AlundraSaveScreenDirector Instance => Holder.Value;

    private static class Holder
    {
        internal static readonly AlundraSaveScreenDirector Value = new();
    }

    private AlundraSaveScreenDirector()
    {
        for (var i = 0; i < RecordCount; i++)
        {
            _records[i] = new Record();
        }

        ResetFlowState();
    }

    // ---- g_globalTransitionState (StartMemoryCardProcess, MemoryCardManager.cs:592-1737) ---------------------

    internal const int StateIdle = 0;
    internal const int StateExamine = 0x2710;
    internal const int StateExamineWait = 0x2711;
    internal const int StateReadWait = 0x3f4;
    internal const int StatePick = 0x0C;
    internal const int StatePickWait = 0x3f8;
    internal const int StateWrite = 0x10;
    internal const int StateSaved = 0x3fe;
    internal const int StateSavedMessage = 0x16;
    internal const int StateFailed = 0x3fd;
    internal const int StateFailedMessage = 0x15;
    internal const int StateWaitSquare = 0x3f5;
    internal const int StateBlankMessage = 0x0D;
    internal const int StateCloseWait = 0x3f6;
    internal const int StateHud = 0x0E;
    internal const int StateEndWait = 0x44B;
    internal const int StateEnd = 0x63;

    // ---- ETC texts (L2, D-E16-37) ------------------------------------------------------------------------------

    internal const int ExamineEtcIndex = 0x87;
    internal const int PickHeadingEtcIndex = 0x83;
    internal const int QuestionHeadingEtcIndex = 0x84;
    internal const int YesEtcIndex = 0x4A;
    internal const int NoEtcIndex = 0x4B;
    internal const int SavingEtcIndex = 0xA5;
    internal const int SavedEtcIndex = 0xB1;
    internal const int FailedEtcIndex = 0xAF;
    internal const int BlankEtcIndex = 0x9F;

    /// <summary>L3/SE10: the longest text a line shows - the <c>0x40</c> characters <c>DisplayIconName</c> receives for
    /// the message line (0x8005953C); every text shown comes from the game (ETC texts, computed summary).</summary>
    internal const int MaxLineLength = 0x40;

    /// <summary>L3 (established in the binary by T3): what each line of an empty slot shows. <c>FUN_800818e4</c>
    /// returns the block itself when its first byte is 0 (0x800818F0-0x80081904), and line 2 is the block at
    /// <c>+0x20</c>; <c>BuildDataAndSaveInMemoryCard</c> zeroes the four blocks when it creates the file
    /// (0x80061124-0x80061140), so both lines are empty strings. The pointers are not null, so
    /// <c>FUN_80058b28</c> (0x80058B5C) still marks the record drawn: an empty slot is an empty box.</summary>
    internal const string EmptySlotLine = "";

    /// <summary>D-E16-35: the four fixed slots, <c>slot1</c> to <c>slot4</c>, indexed by the picker's entry (SE5: the
    /// name always comes from this constant table, indexed by the entry frozen at Cross).</summary>
    internal static readonly string[] SlotNames = { "slot1", "slot2", "slot3", "slot4" };

    internal const int SlotCount = 4;

    // ---- Waits (AdvanceFadeOldCheck, MemoryCardManager.cs:1741-1754) ------------------------------------------

    internal const int WaitLimit = 0x13;
    internal const int EndWaitLimit = 0x0B;

    /// <summary><c>TryOpenMemoryCardMenu</c> (0x80060CF8) on two empty lines: no box, <c>g_fadeFrame = 0x11</c>.</summary>
    internal const int BlankMessageFadeFrame = 0x11;

    // ---- Geometry (StaticVariables.cs:11195-11203, :11329-11386; T3 note on 0x80058F24) ---------------------

    /// <summary><c>g_uiBoxesInventoryDescriptionBackground</c>: the message box, (16, 168), 36 x 7 cells.</summary>
    internal const int MessageBoxX = 16;
    internal const int MessageBoxY = 168;

    /// <summary>The four picker boxes: X = 16, 36 x 7 cells (288 x 56), the same drawing as the message box.</summary>
    internal const int RecordBoxX = 16;
    internal const int BoxHeightCells = 7;

    /// <summary>The y a box slides from or to when it enters or leaves by the bottom (<c>0xF0</c>).</summary>
    internal const int OffscreenBottomY = 0xF0;

    /// <summary><c>SHORT_ARRAY_800C436C</c> (int stride): the picker's rows. <c>-1</c> is above the screen.</summary>
    private static readonly int[] RowY = { -1, 0, 64, 128, 240 };

    /// <summary>Transition 9's text offset (<c>g_callbackTable[9]</c>, X = 0x10, Y = 0x0C) and its line spacing.</summary>
    internal const int MessageTextOffsetX = 0x10;
    internal const int MessageTextOffsetY = 0x0C;
    internal const int MessageLineSpacing = 0x10;

    /// <summary>The picker's heading line at (box.X + 16, box.Y + 16) (0x80059D4C-0x80059D68).</summary>
    internal const int PickerHeadingOffsetX = 0x10;
    internal const int PickerHeadingOffsetY = 0x10;

    /// <summary>A record's two lines at (X + 16, Y + 8) and (X + 16, Y + 32) (<c>FUN_80058c44</c>, 0x80058D74-0x80058DC4).</summary>
    internal const int RecordTextOffsetX = 0x10;
    internal const int RecordLine0OffsetY = 0x08;
    internal const int RecordLine1OffsetY = 0x20;

    // ---- Tints (InitializeUIMemoryFileBox 0x80059DC8, FUN_80059e0c 0x80059E0C) --------------------------------

    internal const int TintDuration = 0x0F;
    internal const int TintSide = 0x40;
    internal const int TintMiddle = 0x80;

    /// <summary>The slide speed of <c>UpdateUiBoxesPosition</c> (0x80047DD0), 15.</summary>
    private const int SlideSpeed = 0x0F;

    internal const int RecordCount = 4;

    // ---- Picker flags (UINT_ARRAY_800c4190[2]) ---------------------------------------------------------------

    private const int PickerOpening = 1;
    private const int PickerClosing = 2;
    private const int PickerScrolling = 4;
    private const int PickerQuestion = 8;

    // ---- Message box state (g_memoryCardMenuState, UIManager.cs:1561, MemoryCardManager.cs:2747-2803) --------

    private const int MessageOpening = 1;
    private const int MessageClosing = 2;
    private const int MessageCloseable = 4;
    private const int MessageCloseRequested = 8;

    /// <summary>The sound the message box plays as it starts closing (0x80051054).</summary>
    internal const int MessageCloseSfx = 5;

    /// <summary>The picker's result (<c>SetMemoryCardMenuResult</c>): -1 while picking, -2 for NON, else the
    /// entry.</summary>
    internal const int PickPending = -1;
    internal const int PickCancelled = -2;

    private const string LogPrefix = "AlundraSaveScreenDirector: ";

    private enum Transition
    {
        None,
        Message,
        PickerInitialize,
        Picker,
    }

    private struct Slide
    {
        public int Mode;
        public int Tick;
        public int SourceX;
        public int SourceY;
        public int TargetX;
        public int TargetY;
    }

    private struct Tint
    {
        public int Start;
        public int Target;
        public int Current;
        public int Tick;
    }

    private sealed class Record
    {
        public bool Drawn;
        public int X = RecordBoxX;
        public int Y;
        public Slide Slide;
        public Tint Tint;
        public string Line0 = string.Empty;
        public string Line1 = string.Empty;
    }

    private AlundraGameState? _gameState;
    private IAlundraSoundPlayer? _soundPlayer;

    private int _state;
    private int _fadeFrame;
    private int _fadeSubstate;
    private bool _menuOpenPosted;
    private bool _bookFlowActive;
    private AlundraSaveGame? _capturedSave;

    private Transition _transition;
    private bool _messageWasEmpty;
    private int _messageState;
    private int _messageBoxX = MessageBoxX;
    private int _messageBoxY = MessageBoxY;
    private Slide _messageSlide;
    private string _messageLine0 = string.Empty;
    private string _messageLine1 = string.Empty;
    private bool _messageBoxDrawn;

    private readonly Record[] _records = new Record[RecordCount];
    private readonly string[] _labelLine0 = new string[SlotCount];
    private readonly string[] _labelLine1 = new string[SlotCount];
    private int _pickerFlags;
    private int _ring;
    private int _fillCount;
    private int _selection;
    private int _pickResult = PickPending;
    private string _pickHeading = string.Empty;
    private string _questionHeading = string.Empty;
    private bool _pickerDrawn;
    private bool _questionOpened;
    private int? _answer;
    private int _confirmedEntry;

    // The heading shows ETC 0x84 from Cross until the picker's end (0x80059534); PickerQuestion is replaced by
    // PickerClosing at the answer, so this latch keeps that heading through the closing slide.
    private bool _questionShownSinceCross;

    // ---- Public state ------------------------------------------------------------------------------------------

    /// <summary>L5: true from state <c>0x2710</c> (or the failure message) until the end of <c>0x63</c>.</summary>
    public bool IsActive => _state != StateIdle;

    /// <summary>The port of <c>g_globalTransitionState</c>, 0 at rest.</summary>
    public int State => _state;

    /// <summary>L5/SE3: a book flow runs, from its slot F to its reset.</summary>
    public bool IsBookFlowActive => _bookFlowActive;

    /// <summary>True on a tick where the message box is drawn (transition 9, or the picker's heading box).</summary>
    public bool IsMessageBoxDrawn => _messageBoxDrawn;

    /// <summary>The message box's current native position.</summary>
    public (int X, int Y) MessageBoxPosition => (_messageBoxX, _messageBoxY);

    /// <summary>The message box's first line and its native position (the picker's heading while it runs).</summary>
    public (string Text, int X, int Y) MessageLine0 => IsPickerActive
        ? (_questionShownSinceCross ? _questionHeading : _pickHeading, _messageBoxX + PickerHeadingOffsetX, _messageBoxY + PickerHeadingOffsetY)
        : (_messageLine0, _messageBoxX + MessageTextOffsetX, _messageBoxY + MessageTextOffsetY);

    /// <summary>The message box's second line (empty while the picker runs: its heading is one line).</summary>
    public (string Text, int X, int Y) MessageLine1 => IsPickerActive
        ? (string.Empty, _messageBoxX + PickerHeadingOffsetX, _messageBoxY + PickerHeadingOffsetY + MessageLineSpacing)
        : (_messageLine1, _messageBoxX + MessageTextOffsetX, _messageBoxY + MessageTextOffsetY + MessageLineSpacing);

    /// <summary>True on a tick where the picker record <paramref name="record"/> is drawn (its flag, bit 0 of
    /// <c>0x800C419C + 0x74 * record</c>, while the picker draws).</summary>
    public bool IsRecordDrawn(int record) => _pickerDrawn && _records[record].Drawn;

    /// <summary>The record's current native position.</summary>
    public (int X, int Y) RecordPosition(int record) => (_records[record].X, _records[record].Y);

    /// <summary>The record's box tint, one channel (the original's three are always equal): 0x80 is the texture's own
    /// colour, 0 black.</summary>
    public int RecordTint(int record) => _records[record].Tint.Current;

    /// <summary>The record's first line (the chapter's name) and its native position.</summary>
    public (string Text, int X, int Y) RecordLine0(int record)
    {
        var r = _records[record];
        return (r.Line0, r.X + RecordTextOffsetX, r.Y + RecordLine0OffsetY);
    }

    /// <summary>The record's second line (the summary) and its native position.</summary>
    public (string Text, int X, int Y) RecordLine1(int record)
    {
        var r = _records[record];
        return (r.Line1, r.X + RecordTextOffsetX, r.Y + RecordLine1OffsetY);
    }

    /// <summary>L3: the two lines computed for <paramref name="slot"/> (0 to 3) at the last opening.</summary>
    public (string Line0, string Line1) SlotLabel(int slot) => (_labelLine0[slot], _labelLine1[slot]);

    /// <summary>The picker's selected entry (<c>INT_80180120</c>).</summary>
    public int Selection => _selection;

    /// <summary>The picker's ring index (<c>UINT_ARRAY_800c4190[0]</c>): the hidden spare record.</summary>
    internal int Ring => _ring;

    /// <summary>True while the picker runs (transition 10).</summary>
    public bool IsPickerActive => _transition is Transition.Picker or Transition.PickerInitialize;

    /// <summary>True while the picker's OUI/NON question waits for its answer.</summary>
    public bool IsQuestionPending => (_pickerFlags & PickerQuestion) != 0;

    // ---- Attachment and session ----------------------------------------------------------------------------------

    /// <summary>Re-points this session instance at the world's game state and sound player, touching no flow state
    /// (same contract as the other session directors). Null is tolerated: <see cref="Tick"/> then does nothing.</summary>
    public void AttachToWorld(AlundraGameState? gameState, IAlundraSoundPlayer? soundPlayer)
    {
        _gameState = gameState;
        _soundPlayer = soundPlayer;
    }

    /// <summary>
    /// L5/SE3: the map entry, called by <see cref="AlundraWorldProxy.InitializeWithWorld"/> right after the dialogue
    /// director's own map entry (which already dropped any box and choice and rebuilt its runner). A book flow or a
    /// screen left running by a world change (a map event, another entity, a portal during the book's states 1 to 5)
    /// is ended here without writing: <c>ControlLocked</c> cleared, <c>MenuOpen</c> cleared when this screen posted
    /// it, a choice of the screen closed, the book's flow forgotten, and one warning logged.
    /// </summary>
    public void InstallForMapEntry()
    {
        if (!_bookFlowActive && !IsActive)
        {
            return;
        }

        if (_questionOpened)
        {
            AlundraDialogueDirector.Instance.CloseStandaloneChoice();
        }

        if (_gameState != null)
        {
            _gameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.ControlLocked;
            if (_menuOpenPosted)
            {
                _gameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
            }
        }

        Logs.WriteWarning(
            LogPrefix + $"a world change cut the save flow (screen state 0x{_state:X}, book flow {(_bookFlowActive ? "running" : "idle")}); "
            + "nothing written, the hero released.");

        AlundraSaveBook.Instance.ForgetFlow();
        ResetFlowState();
    }

    /// <inheritdoc/>
    public bool Start(AlundraSaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (IsActive)
        {
            return false;
        }

        ResetScreenState();
        _capturedSave = save;
        _state = StateExamine;
        return true;
    }

    /// <inheritdoc/>
    public bool StartFailure()
    {
        if (IsActive)
        {
            return false;
        }

        ResetScreenState();
        _capturedSave = null;
        PostMenuOpen();
        _state = StateFailedMessage;
        return true;
    }

    /// <inheritdoc/>
    public void NoteBookFlowStarted() => _bookFlowActive = true;

    /// <inheritdoc/>
    public void NoteBookFlowEnded() => _bookFlowActive = false;

    // ---- Tick --------------------------------------------------------------------------------------------------

    /// <summary>
    /// One logic tick, from <see cref="AlundraWorldProxy.Update"/>'s per-tick loop: the state machine
    /// (<c>UpdateMemoryCardProcess</c>, <c>GraphicManager.cs:62</c>), then the active transition's render
    /// (<c>UpdateUserInterface</c>, which the original runs right after it in the same frame). A no-op at rest or
    /// without an attached game state. Never throws: the service calls are guarded.
    /// </summary>
    public void Tick()
    {
        if (_gameState == null || !IsActive)
        {
            _messageBoxDrawn = false;
            _pickerDrawn = false;
            return;
        }

        RunState(_gameState);
        RunTransition(_gameState);
    }

    // ---- The state machine ---------------------------------------------------------------------------------------

    private void RunState(AlundraGameState state)
    {
        switch (_state)
        {
            case StateExamine:
                // 0x2710: g_isMemoryCopyInProgress = 1, MenuOpen, message 0x87/0x88.
                _fadeFrame = 0;
                PostMenuOpen();
                OpenMessage(ExamineEtcIndex);
                _state = StateExamineWait;
                return;

            case StateExamineWait:
                // 0x2711: wait, then the port scan, format and space states, which have no object (L2).
                if (AdvanceFade(WaitLimit))
                {
                    _state = StateReadWait;
                }

                return;

            case StateReadWait:
                // 0x3f4 (0x80060090): the read of the file and the close of the message, once, then a wait.
                if (_fadeSubstate == 0)
                {
                    RequestMessageClose();
                    _fadeSubstate = 1;
                }

                if (AdvanceFade(WaitLimit))
                {
                    _state = StatePick;
                }

                return;

            case StatePick:
                // 0x0C: the four labels (L3, SE9: here only), the headings, the picker; g_fadeFrame = 0 (0x800601A0).
                _fadeSubstate = 0;
                ComputeLabels();
                _pickHeading = Bound(ResolveEtc(PickHeadingEtcIndex));
                _questionHeading = Bound(ResolveEtc(QuestionHeadingEtcIndex));
                OpenPicker();
                _fadeFrame = 0;
                _state = StatePickWait;
                return;

            case StatePickWait:
                // 0x3f8: wait for the picker's result; NON goes to the write state without a message.
                if (_pickResult == PickPending)
                {
                    return;
                }

                if (!AdvanceFade(WaitLimit))
                {
                    return;
                }

                if (_pickResult != PickCancelled)
                {
                    OpenMessage(SavingEtcIndex);
                }

                _state = StateWrite;
                return;

            case StateWrite:
                RunWrite(state);
                return;

            case StateSaved:
            case StateFailed:
                // 0x3fe/0x3fd: close the message once, wait, then the success or the failure message.
                if (_fadeSubstate == 0)
                {
                    RequestMessageClose();
                    _fadeSubstate = 1;
                }

                if (AdvanceFade(WaitLimit))
                {
                    _state = _state == StateSaved ? StateSavedMessage : StateFailedMessage;
                }

                return;

            case StateSavedMessage:
            case StateFailedMessage:
                // 0x16/0x15: the message, then the wait for Square.
                _fadeSubstate = 0;
                _fadeFrame = 0;
                OpenMessage(_state == StateSavedMessage ? SavedEtcIndex : FailedEtcIndex);
                _state = StateWaitSquare;
                return;

            case StateWaitSquare:
                // 0x3f5 (MemoryCardManager.cs:1507-1531): passes alone only after an empty message; Square closes.
                if (_messageWasEmpty)
                {
                    _fadeSubstate = 1;
                }

                if ((state.TickPad.ButtonsJustPressed & AlundraPadState.Square) != 0)
                {
                    RequestMessageClose();
                    _fadeSubstate = 1;
                }

                if (_fadeSubstate == 0 || !AdvanceFade(WaitLimit))
                {
                    return;
                }

                _state = StateBlankMessage;
                return;

            case StateBlankMessage:
                // 0x0D: ETC 0x9F/0xA0, both empty in ETC_RES.R (J5): no box, g_fadeFrame = 0x11.
                _fadeSubstate = 0;
                _fadeFrame = 0;
                OpenMessage(BlankEtcIndex);
                _state = StateCloseWait;
                return;

            case StateCloseWait:
                // 0x3f6.
                if (_fadeSubstate == 0)
                {
                    RequestMessageClose();
                    _fadeSubstate = 1;
                }

                if (AdvanceFade(WaitLimit))
                {
                    _state = StateHud;
                }

                return;

            case StateHud:
                // 0x0E: the gauge put back before it hides.
                _fadeSubstate = 0;
                _fadeFrame = 0;
                AlundraHudDirector.Instance.InitializeHudPositionBeforeHide();
                _state = StateEndWait;
                return;

            case StateEndWait:
                // 0x44B.
                if (_fadeSubstate == 0)
                {
                    RequestMessageClose();
                    _fadeSubstate = 1;
                }

                if (AdvanceFade(EndWaitLimit))
                {
                    _state = StateEnd;
                }

                return;

            case StateEnd:
                // 0x63: g_isMemoryCopyInProgress = 0, g_globalTransitionState = 0, MenuOpen cleared.
                _fadeSubstate = 0;
                _fadeFrame = 0;
                state.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
                _menuOpenPosted = false;
                _capturedSave = null;
                _transition = Transition.None;
                _state = StateIdle;
                return;
        }
    }

    /// <summary>0x10 (MemoryCardManager.cs:1311-1348): after the wait, NON leads to <c>0x3f6</c>; otherwise the
    /// write, ADAPTED (L2): the object captured at the book's state 5, itself (SE4), to the slot of the entry frozen
    /// at Cross (SE5), in binary, with its metadata. Any status but <see cref="SaveGameSaveStatus.Saved"/>, and any
    /// exception, is the failure <c>0x3fd</c>.</summary>
    private void RunWrite(AlundraGameState state)
    {
        if (!AdvanceFade(WaitLimit))
        {
            return;
        }

        if (_pickResult == PickCancelled)
        {
            _fadeSubstate = 1;
            _fadeFrame = 0;
            _state = StateCloseWait;
            return;
        }

        _fadeSubstate = 0;
        if (TryWrite())
        {
            _fadeSubstate = 0;
            _fadeFrame = 0;
            _state = StateSaved;
        }
        else
        {
            _state = StateFailed;
        }
    }

    private bool TryWrite()
    {
        var save = _capturedSave;
        if (save == null || _pickResult < 0 || _pickResult >= SlotCount)
        {
            Logs.WriteWarning(LogPrefix + $"nothing to write (entry {_pickResult}); the save fails.");
            return false;
        }

        var slot = SlotNames[_pickResult];
        try
        {
            var outcome = AlundraSaveGameDirector.Instance.SaveSlots.Save(slot, save, SaveGameFormat.Binary, save.BuildMetadata());
            if (outcome.Status == SaveGameSaveStatus.Saved)
            {
                Logs.WriteInfo(LogPrefix + $"saved map {save.InitialMapId} to slot '{slot}' (binary).");
                return true;
            }

            Logs.WriteWarning(LogPrefix + $"slot '{slot}' not written: {outcome.Status} {outcome.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(LogPrefix + $"slot '{slot}' not written: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary><c>AdvanceFadeOldCheck</c> (MemoryCardManager.cs:1741-1754): true on the call whose previous count
    /// reached <paramref name="limit"/>, which resets the count.</summary>
    private bool AdvanceFade(int limit)
    {
        var old = _fadeFrame;
        _fadeFrame = old + 1;
        if (old < limit)
        {
            return false;
        }

        _fadeFrame = 0;
        return true;
    }

    private void PostMenuOpen()
    {
        if (_gameState != null)
        {
            _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
        }

        _menuOpenPosted = true;
    }

    // ---- L3: the labels --------------------------------------------------------------------------------------------

    /// <summary>L3/SE4/SE9: the four labels, computed once per opening; every failure gives the empty slot's two
    /// lines, and nothing escapes.</summary>
    private void ComputeLabels()
    {
        for (var i = 0; i < SlotCount; i++)
        {
            _labelLine0[i] = EmptySlotLine;
            _labelLine1[i] = EmptySlotLine;
        }

        AlundraSaveGameRules? rules;
        try
        {
            if (!AlundraSaveGameDirector.Instance.TryCreateRules(out rules, out var rulesError) || rules == null)
            {
                Logs.WriteWarning(LogPrefix + $"no validation rules, every slot shows as empty: {rulesError}");
                return;
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(LogPrefix + $"no validation rules, every slot shows as empty: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        for (var i = 0; i < SlotCount; i++)
        {
            try
            {
                var outcome = AlundraSaveGameDirector.Instance.SaveSlots.TryLoad(SlotNames[i], out var loaded);
                if (outcome.Status != SaveGameLoadStatus.Loaded || loaded == null)
                {
                    continue;
                }

                if (!loaded.TryValidate(rules, out _))
                {
                    continue;
                }

                var chapter = AlundraChapterFlags.GetFirstEnabledFlagIndex(loaded.GameFlags);
                _labelLine0[i] = Bound(ResolveEtc(chapter));
                _labelLine1[i] = Bound(AlundraSaveGame.BuildSummary(loaded.HpMax, loaded.GameTime));
            }
            catch (Exception ex)
            {
                _labelLine0[i] = EmptySlotLine;
                _labelLine1[i] = EmptySlotLine;
                Logs.WriteWarning(LogPrefix + $"slot '{SlotNames[i]}' shows as empty: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static string ResolveEtc(int etcIndex)
        => AlundraEtcStringTable.TryResolveText(etcIndex, out var text) ? text ?? string.Empty : string.Empty;

    private static string Bound(string text) => text.Length <= MaxLineLength ? text : text.Substring(0, MaxLineLength);

    // ---- Transition 9: the message box --------------------------------------------------------------------------

    /// <summary><c>TryOpenMemoryCardMenu</c> (0x80060CF8) then <c>FUN_80050c88</c> (UIManager.cs:1473-1561): two
    /// empty lines open nothing and set <c>g_fadeFrame</c> to 0x11; otherwise the box slides in from y = 240.</summary>
    private void OpenMessage(int etcIndex)
    {
        var line0 = Bound(ResolveEtc(etcIndex));
        var line1 = Bound(ResolveEtc(etcIndex + 1));
        if (line0.Length == 0 && line1.Length == 0)
        {
            _fadeFrame = BlankMessageFadeFrame;
            _messageWasEmpty = true;
            return;
        }

        _messageWasEmpty = false;
        _messageLine0 = line0;
        _messageLine1 = line1;
        _messageState = MessageOpening | MessageCloseable;
        _messageSlide = new Slide { Mode = 2, Tick = 0, SourceX = MessageBoxX, SourceY = OffscreenBottomY, TargetX = MessageBoxX, TargetY = MessageBoxY };
        _transition = Transition.Message;
    }

    /// <summary><c>ResetMemoryCardMenuState</c> (0x80050C64): asks the message box to close once it is closeable.</summary>
    private void RequestMessageClose()
    {
        if ((_messageState & MessageCloseable) != 0)
        {
            _messageState |= MessageCloseRequested;
        }
    }

    /// <summary><c>Fun_80050ec8</c> (0x80050EC8): the slide in, the close once asked (sound 5), and the end of the
    /// transition when the slide out completes - a tick that draws nothing.</summary>
    private void RenderMessage()
    {
        if ((_messageState & (MessageOpening | MessageClosing)) == 0)
        {
            if ((_messageState & MessageCloseRequested) != 0)
            {
                _messageState = (_messageState & ~MessageCloseRequested) | MessageClosing;
                _messageSlide = new Slide
                {
                    Mode = 2, Tick = 0, SourceX = _messageBoxX, SourceY = _messageBoxY, TargetX = _messageBoxX, TargetY = OffscreenBottomY,
                };
                _soundPlayer?.PlaySfx(MessageCloseSfx);
            }
        }
        else if (AdvanceSlide(ref _messageBoxX, ref _messageBoxY, ref _messageSlide))
        {
            if ((_messageState & MessageOpening) != 0)
            {
                _messageState &= ~MessageOpening;
            }

            if ((_messageState & MessageClosing) != 0)
            {
                _messageBoxX = MessageBoxX;
                _messageBoxY = MessageBoxY;
                _transition = Transition.None;
                _messageBoxDrawn = false;
                return;
            }
        }

        _messageBoxDrawn = true;
    }

    // ---- Transition 10: the picker -----------------------------------------------------------------------------

    /// <summary><c>FUN_80058ab4</c> (0x80058AB4): the result pending, transition 10 armed; its render initializes
    /// the picker in this same tick (<c>SetTransitionType</c> then <c>UpdateUserInterface</c>).</summary>
    private void OpenPicker()
    {
        _pickResult = PickPending;
        _transition = Transition.PickerInitialize;
    }

    /// <summary><c>InitializeMemoryCardMenu</c> (0x800583EC): rows 0, 64, 128, 240, all empty; each record and the
    /// message box slide from y = 240; tints from 0 to 0x40, 0x80, 0x40, 0x40; ring from 1, entries 0 and 1 filled
    /// into records 1 and 2, selection 0, ring 3; heading ETC <c>0x83</c> (0x800589A8). Draws nothing.</summary>
    private void InitializePicker()
    {
        for (var i = 0; i < RecordCount; i++)
        {
            var record = _records[i];
            record.Drawn = false;
            record.X = RecordBoxX;
            record.Y = RowY[i + 1];
            record.Slide = new Slide
            {
                Mode = 2, Tick = 0, SourceX = RecordBoxX, SourceY = OffscreenBottomY, TargetX = RecordBoxX, TargetY = Adjust(RowY[i + 1]),
            };
            record.Line0 = string.Empty;
            record.Line1 = string.Empty;
        }

        _pickerFlags = 0;
        _questionOpened = false;
        _questionShownSinceCross = false;
        _answer = null;
        InitializeTint(0, 0, TintSide);
        InitializeTint(1, 0, TintMiddle);
        InitializeTint(2, 0, TintSide);
        InitializeTint(3, 0, TintSide);
        _messageSlide = new Slide { Mode = 2, Tick = 0, SourceX = MessageBoxX, SourceY = OffscreenBottomY, TargetX = MessageBoxX, TargetY = MessageBoxY };
        _pickerFlags |= PickerOpening;

        _ring = 1;
        _fillCount = 0;
        if (Fill(_ring, _fillCount))
        {
            _fillCount++;
        }

        _ring = (_ring + 1) & 3;
        if (Fill(_ring, _fillCount))
        {
            _fillCount++;
        }

        _selection = 0;
        _ring = (_ring + 1) & 3; // 0x80058A80-0x80058A94.
        _transition = Transition.Picker;
        _pickerDrawn = false;
        _messageBoxDrawn = false;
    }

    /// <summary><c>DisplayMemoryCardMenu</c> (0x80058F24), one call.</summary>
    private void RenderPicker(AlundraGameState state)
    {
        if ((_pickerFlags & (PickerOpening | PickerScrolling)) != 0)
        {
            // 0x80058F64-0x80059038: tints, the message box, then records 0, 1, 3, 2 - record 2's return decides.
            TickTints();
            AdvanceSlide(ref _messageBoxX, ref _messageBoxY, ref _messageSlide);
            AdvanceRecord(0);
            AdvanceRecord(1);
            AdvanceRecord(3);
            if (AdvanceRecord(2))
            {
                _pickerFlags &= ~PickerOpening;
                if ((_pickerFlags & PickerScrolling) != 0)
                {
                    _pickerFlags &= ~PickerScrolling;
                    _records[_ring].Drawn = false;
                }
            }
        }
        else if ((_pickerFlags & PickerClosing) != 0)
        {
            // 0x8005903C-0x80059128: records 0 to 3, then the message box, whose return ends the picker.
            TickTints();
            AdvanceRecord(0);
            AdvanceRecord(1);
            AdvanceRecord(2);
            AdvanceRecord(3);
            if (AdvanceSlide(ref _messageBoxX, ref _messageBoxY, ref _messageSlide))
            {
                _pickerFlags &= ~PickerClosing;
                _messageBoxX = MessageBoxX;
                _messageBoxY = MessageBoxY;
                _transition = Transition.None;
                _pickResult = _answer == 1 ? _confirmedEntry : PickCancelled;
                _questionShownSinceCross = false;
                _pickerDrawn = false;
                _messageBoxDrawn = false;
                return;
            }
        }
        else if ((_pickerFlags & PickerQuestion) != 0)
        {
            // 0x8005912C-0x800594C4: the answer arms the closing slide.
            if (PollAnswer())
            {
                ArmPickerClose();
            }
        }
        else
        {
            RunPickerInput(state);
        }

        _pickerDrawn = true;
        _messageBoxDrawn = true;
    }

    /// <summary>0x800594C8-0x80059C84: Cross asks OUI/NON; otherwise Down, then Up, in the same tick
    /// (<c>ButtonsJustPressedByInterval</c>, the key repeat).</summary>
    private void RunPickerInput(AlundraGameState state)
    {
        var pad = state.TickPad.ButtonsJustPressedByInterval;

        if ((pad & AlundraPadState.Cross) != 0)
        {
            AskQuestion();
            return;
        }

        if ((pad & AlundraPadState.Down) != 0 && _records[(_ring + 3) & 3].Drawn)
        {
            ScrollDown();
        }

        if ((pad & AlundraPadState.Up) != 0 && _selection != 0)
        {
            ScrollUp();
        }
    }

    /// <summary>
    /// 0x800594E0-0x80059548: Cross freezes the entry (SE5), shows ETC <c>0x84</c> and asks OUI/NON (<c>0x4A</c>/
    /// <c>0x4B</c>) through <see cref="AlundraDialogueDirector.OpenChoice"/> (L5): the dialogue screen shows it over
    /// this one and owns its keys. Without a presenter (<see cref="AlundraDialogueDirector.OpenChoice"/> would wait
    /// forever), or when the labels do not resolve, no choice opens and the answer is NON, logged.
    /// </summary>
    private void AskQuestion()
    {
        _confirmedEntry = _selection;
        _questionShownSinceCross = true;
        _pickerFlags = PickerQuestion;
        _answer = null;

        var dialogue = AlundraDialogueDirector.Instance;
        if (!dialogue.HasPresenter)
        {
            Logs.WriteWarning(LogPrefix + "no dialogue presenter to ask OUI/NON; the answer is NON, nothing written.");
            _answer = 0;
            return;
        }

        var yes = ResolveEtc(YesEtcIndex);
        var no = ResolveEtc(NoEtcIndex);
        if (yes.Length == 0 || no.Length == 0)
        {
            Logs.WriteWarning(LogPrefix + $"ETC 0x{YesEtcIndex:X2}/0x{NoEtcIndex:X2} do not resolve; the answer is NON, nothing written.");
            _answer = 0;
            return;
        }

        dialogue.OpenChoice(new[] { Bound(yes), Bound(no) });
        _questionOpened = true;
    }

    /// <summary>The answer: 1 (first choice) is OUI, 0 NON, as <c>g_asyncOperationResult</c> 1 and 2. The choice is
    /// closed as soon as it is taken (<see cref="AlundraDialogueDirector.CloseStandaloneChoice"/>). A choice another
    /// caller cleared (no result, nothing waiting) is NON.</summary>
    private bool PollAnswer()
    {
        if (_answer != null)
        {
            return true;
        }

        if (!_questionOpened)
        {
            _answer = 0;
            return true;
        }

        var dialogue = AlundraDialogueDirector.Instance;
        var result = dialogue.TakeChoiceResult();
        if (result == null && dialogue.IsAwaitingChoice)
        {
            return false;
        }

        if (result == null)
        {
            Logs.WriteWarning(LogPrefix + "the OUI/NON question was cleared before any answer; the answer is NON.");
        }

        dialogue.CloseStandaloneChoice();
        _questionOpened = false;
        _answer = result == 1 ? 1 : 0;
        return true;
    }

    /// <summary>0x80059150-0x800594C4: records ring+1 to ring+3 leave upwards from rows 0, 64, 128 to y = -1 (drawn at
    /// <c>-1 - 8 x 7</c>), stepped once now; their tints fall from 0x40/0x80/0x40 to 0; the message box slides down to
    /// y = 240.</summary>
    private void ArmPickerClose()
    {
        _pickerFlags = PickerClosing;
        for (var k = 0; k < 3; k++)
        {
            var index = (_ring + 1 + k) & 3;
            var record = _records[index];
            record.Slide = new Slide
            {
                Mode = 2, Tick = 0, SourceX = RecordBoxX, SourceY = Adjust(RowY[k + 1]), TargetX = RecordBoxX, TargetY = Adjust(RowY[0]),
            };
            AdvanceRecord(index);
        }

        InitializeTint((_ring + 1) & 3, TintSide, 0);
        InitializeTint((_ring + 2) & 3, TintMiddle, 0);
        InitializeTint((_ring + 3) & 3, TintSide, 0);
        _messageSlide = new Slide
        {
            Mode = 2, Tick = 0, SourceX = _messageBoxX, SourceY = Adjust(_messageBoxY), TargetX = _messageBoxX, TargetY = OffscreenBottomY,
        };
    }

    /// <summary>0x80059590-0x800598D4: records ring+1, ring+2, ring+3, ring move from rows 0, 64, 128, 240 to -1, 0, 64,
    /// 128, each stepped once now; tints ring 0→0x40, ring+1 0x40→0, ring+2 0x80→0x40, ring+3 0x40→0x80; the next
    /// entry fills record ring; ring + 1, selection + 1.</summary>
    private void ScrollDown()
    {
        for (var k = 0; k < RecordCount; k++)
        {
            var index = (_ring + 1 + k) & 3;
            _records[index].Slide = new Slide
            {
                Mode = 2, Tick = 0, SourceX = RecordBoxX, SourceY = Adjust(RowY[k + 1]), TargetX = RecordBoxX, TargetY = Adjust(RowY[k]),
            };
            AdvanceRecord(index);
        }

        InitializeTint(_ring, 0, TintSide);
        InitializeTint((_ring + 1) & 3, TintSide, 0);
        InitializeTint((_ring + 2) & 3, TintMiddle, TintSide);
        InitializeTint((_ring + 3) & 3, TintSide, TintMiddle);
        _pickerFlags |= PickerScrolling;

        if (Fill(_ring, _fillCount))
        {
            _fillCount++;
        }

        _ring = (_ring + 1) & 3;
        _selection++;
    }

    /// <summary>0x800598FC-0x80059C84: records ring to ring+3 move from rows -1, 0, 64, 128 to 0, 64, 128, 240, each
    /// stepped once now; entry selection - 2 fills record ring when selection is at least 2; tints ring 0→0x40,
    /// ring+1 0x40→0x80, ring+2 0x80→0x40, ring+3 0x40→0; the next entry to fill is selection + 1 (selection when that
    /// entry is empty); selection - 1, ring - 1.</summary>
    private void ScrollUp()
    {
        _pickerFlags |= PickerScrolling;
        for (var k = 0; k < RecordCount; k++)
        {
            var index = (_ring + k) & 3;
            _records[index].Slide = new Slide
            {
                Mode = 2, Tick = 0, SourceX = RecordBoxX, SourceY = Adjust(RowY[k]), TargetX = RecordBoxX, TargetY = Adjust(RowY[k + 1]),
            };
            AdvanceRecord(index);
        }

        if (_selection >= 2)
        {
            Fill(_ring, _selection - 2);
        }

        InitializeTint(_ring, 0, TintSide);
        InitializeTint((_ring + 1) & 3, TintSide, TintMiddle);
        InitializeTint((_ring + 2) & 3, TintMiddle, TintSide);
        InitializeTint((_ring + 3) & 3, TintSide, 0);

        _fillCount = _selection < SlotCount ? _selection + 1 : _selection;
        _selection--;
        _ring = (_ring - 1) & 3;
    }

    /// <summary><c>FUN_80058b28</c> (0x80058B28): record <paramref name="record"/> shows entry <paramref name="entry"/>;
    /// past the four entries (the list's null terminator) it is emptied and not drawn.</summary>
    private bool Fill(int record, int entry)
    {
        var target = _records[record & 3];
        if (entry < 0 || entry >= SlotCount)
        {
            target.Drawn = false;
            return false;
        }

        target.Drawn = true;
        target.Line0 = _labelLine0[entry];
        target.Line1 = _labelLine1[entry];
        return true;
    }

    private bool AdvanceRecord(int record)
    {
        var r = _records[record];
        return AdvanceSlide(ref r.X, ref r.Y, ref r.Slide);
    }

    private void InitializeTint(int record, int start, int target)
    {
        _records[record & 3].Tint = new Tint { Start = start, Target = target, Current = start, Tick = 0 };
    }

    /// <summary><c>FUN_80059e0c</c> (0x80059E0C) on the four tints, with the correction of the class doc: from the
    /// duration on, the tint stays on its target.</summary>
    private void TickTints()
    {
        for (var i = 0; i < RecordCount; i++)
        {
            ref var tint = ref _records[i].Tint;
            tint.Tick++;
            tint.Current = tint.Tick >= TintDuration
                ? tint.Target
                : tint.Start + (tint.Target - tint.Start) * tint.Tick / TintDuration;
        }
    }

    /// <summary>The negative-coordinate rule of every slide set-up: a coordinate below 0 moves by the box's size
    /// (<c>y - 8 x height</c>), so y = -1 puts a 7-cell box just above the screen.</summary>
    private static int Adjust(int y) => y < 0 ? y - BoxHeightCells * 8 : y;

    /// <summary>Port of <c>UIManager.UpdateUiBoxesPosition</c> (0x80047DD0), the same shape as the inventory's
    /// <c>AdvanceBoxTween</c>: true exactly when the slide was ALREADY settled on entry.</summary>
    private static bool AdvanceSlide(ref int x, ref int y, ref Slide slide)
    {
        if (slide.Mode == 0)
        {
            return true;
        }

        if (slide.Tick == SlideSpeed)
        {
            x = slide.TargetX;
            y = slide.TargetY;
            slide.Mode -= 1;
        }
        else
        {
            x = slide.SourceX + (slide.TargetX - slide.SourceX) * slide.Tick / SlideSpeed;
            y = slide.SourceY + (slide.TargetY - slide.SourceY) * slide.Tick / SlideSpeed;
            slide.Tick += 1;
        }

        return false;
    }

    private void RunTransition(AlundraGameState state)
    {
        switch (_transition)
        {
            case Transition.Message:
                RenderMessage();
                _pickerDrawn = false;
                return;

            case Transition.PickerInitialize:
                InitializePicker();
                return;

            case Transition.Picker:
                RenderPicker(state);
                return;

            default:
                _messageBoxDrawn = false;
                _pickerDrawn = false;
                return;
        }
    }

    // ---- Reset -------------------------------------------------------------------------------------------------------

    private void ResetScreenState()
    {
        _state = StateIdle;
        _fadeFrame = 0;
        _fadeSubstate = 0;
        _transition = Transition.None;
        _messageWasEmpty = false;
        _messageState = 0;
        _messageBoxX = MessageBoxX;
        _messageBoxY = MessageBoxY;
        _messageSlide = default;
        _messageLine0 = string.Empty;
        _messageLine1 = string.Empty;
        _messageBoxDrawn = false;
        _pickerFlags = 0;
        _ring = 0;
        _fillCount = 0;
        _selection = 0;
        _pickResult = PickPending;
        _pickHeading = string.Empty;
        _questionHeading = string.Empty;
        _pickerDrawn = false;
        _questionOpened = false;
        _questionShownSinceCross = false;
        _answer = null;
        _confirmedEntry = 0;
        for (var i = 0; i < SlotCount; i++)
        {
            _labelLine0[i] = EmptySlotLine;
            _labelLine1[i] = EmptySlotLine;
        }

        for (var i = 0; i < RecordCount; i++)
        {
            var record = _records[i];
            record.Drawn = false;
            record.X = RecordBoxX;
            record.Y = RowY[i + 1];
            record.Slide = default;
            record.Tint = default;
            record.Line0 = string.Empty;
            record.Line1 = string.Empty;
        }
    }

    private void ResetFlowState()
    {
        ResetScreenState();
        _menuOpenPosted = false;
        _bookFlowActive = false;
        _capturedSave = null;
    }

    /// <summary>Test-only: back to the construction state, attachments dropped.</summary>
    internal void ResetForTests()
    {
        _gameState = null;
        _soundPlayer = null;
        ResetFlowState();
    }
}
