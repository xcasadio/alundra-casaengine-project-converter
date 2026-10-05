#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Alundra.Scripts;

/// <summary>What a <see cref="DialogueToken"/> stands for in the text of a page (F2-R3).</summary>
internal enum DialogueTokenKind
{
    /// <summary>A character of the text: one glyph, counted for the voice's rank.</summary>
    Character,

    /// <summary>A <c>glyph</c> marker (<c>\W</c>): one glyph, not counted for the voice.</summary>
    MarkedGlyph,

    /// <summary><c>br</c> (<c>\N</c>): a new line.</summary>
    NewLine,

    /// <summary>The page boundary of the export (<c>\A</c>): the cursor, then a press, then a new line.</summary>
    CursorWait,

    /// <summary><c>slow</c> (<c>\T</c>): the next step waits twice the delay.</summary>
    Slow,

    /// <summary><c>yield</c> (<c>\Y</c>): a step in which nothing is drawn.</summary>
    Yield,

    /// <summary><c>flag</c> (<c>\digits</c>): sets the temporary flag <see cref="DialogueToken.Value"/> when reached; free.</summary>
    Flag,

    /// <summary><c>voice</c> (<c>\B</c> to <c>\G</c>): the voice <see cref="DialogueToken.Value"/> (-1 to 4); free.</summary>
    Voice,

    /// <summary><c>center</c> (<c>\H</c>): centers the line (the view's concern); free.</summary>
    Center,
}

/// <summary>One unit of a page's text as the box reads it: a code or a glyph. <see cref="Display"/> is the character the view shows for a glyph.</summary>
internal readonly record struct DialogueToken(DialogueTokenKind Kind, int Value = 0, char Display = '\0');

/// <summary>What the box does in the world, which it cannot do itself.</summary>
internal interface IAlundraDialogueBoxHost
{
    /// <summary>The step reached the code of the flag <paramref name="flag"/>.</summary>
    void FlagReached(int flag);

    /// <summary>The box asks a sound effect (6 at the opening, 7 at the start of the close, 79 + voice while typing).</summary>
    void PlaySound(int sfxId);

    /// <summary>The cursor was released: the page that follows is wanted (appended with <see cref="AlundraDialogueBox.AppendPage"/>).</summary>
    void PageTurned();

    /// <summary>The slide of the exit ended: the box is released.</summary>
    void Released();
}

/// <summary>
/// E19.f2a F2-R2 (docs/plan-e19-opcodes.md section 1.2j.3): the machine of the dialogue box, exactly as the binary has it (ALUN_CD.EXE, France:
/// the pass is <c>MsgBoxRender</c> 0x80046EF0, slot 0 of the UI callback dispatcher 0x80048054). One <see cref="Pass"/> is one call of that
/// callback; the world calls it once per logic tick BEFORE the scripts of the tick, on the square button of the tick before (F2-R1).
/// <para/>
/// What a pass does, in the order of the binary: while the box slides in or out (<c>flags &amp; 3</c>: fifteen steps then two waiting passes) it
/// only moves; typing done, it evaluates the close (a press when <c>closeMode &amp; 2</c>, the timer of 360 passes armed at the end of the typing when
/// <c>&amp; 1</c>, the latch of the script when <c>&amp; 4</c>) and, on the trigger, plays sound 7 and slides out, the release coming 18 passes later;
/// a scroll of the third line waits its ten passes (or a press, or a latch) and then takes eight passes of two pixels, during which the interpreter
/// is not called; else the interpreter gates one step per pass when the delay falls to 0 (every four passes), or the square is held
/// (<c>textFlags &amp; 1</c>), or a <c>0x4D</c> is latched (<c>&amp; 4</c>); a step is the free codes (flags, voices, centering) then exactly one of a
/// glyph, a new line, the cursor, the slow code, the yield code or the end of the text. Two corrections of the author apply: D-E19-62 (a
/// <c>0x4C</c> clears the <c>0x4D</c> in waiting) and D-E19-63 (the release of a cursor sets the bit that starts a scroll at once only when that
/// release arms the scroll of the third line).
/// </summary>
internal sealed class AlundraDialogueBox
{
    internal const int SlideInOrOut = 3;
    internal const int Active = 4;
    private const int SlideStepCount = 15;
    private const int SlideSettlePasses = 2;
    private const int ScrollPasses = 8;
    private const int ScrollWaitPasses = 10;
    private const int CloseTimerPasses = 360;
    private const int StepDelay = 4;
    private const int ClosedY = 240;
    private const int OpenY = 168;
    private const int OpenSound = 6;
    private const int CloseSound = 7;
    private const int FirstVoiceSound = 79;

    private readonly IAlundraDialogueBoxHost _host;
    private readonly List<DialogueToken> _tokens = new();
    private int _next;
    private readonly StringBuilder _typed = new();
    private int _pendingNewLines;
    private bool _typedChanged;

    // The slide in progress (UpdateUiBoxesPosition 0x80047DD0): from, to, step, passes left to settle.
    private int _slideFrom;
    private int _slideTo;
    private int _slideStep;
    private int _slideSettle;

    private int _textFlags = 3;
    private int _closeMode = 3;
    private int _scrollMode = 3;
    private bool _stepLatched;
    private bool _scrollLatched;
    private bool _closeLatched;
    private int _delay = 1;
    private int _closeTimer;     // not reset by an opening
    private int _scrollLeft;     // not reset by an opening
    private int _scrollWait;     // not reset by an opening
    private int _lineIndex;
    private int _bufferShift;
    private int _voiceRank;
    private int _voice = -1;
    private bool _typingDone;
    private bool _scrollPending;
    private bool _cursorShown;
    private int _cursorCounter;
    private int _flags;
    private int _scrollPixels;
    private readonly StringBuilder[] _bands = { new(), new(), new() };

    public AlundraDialogueBox(IAlundraDialogueBoxHost host)
    {
        _host = host;
    }

    /// <summary>True from the opening to the release (the binary's <c>g_dialog_flags &amp; 4</c>): what opcode 0x39 waits on.</summary>
    public bool IsActive => (_flags & Active) != 0;

    public int CloseMode => _closeMode;

    public int TextFlags => _textFlags;

    public int ScrollMode => _scrollMode;

    /// <summary>The vertical position of the box (240 closed, 168 open, in between while it slides).</summary>
    public int Y { get; private set; } = ClosedY;

    public bool IsSliding => (_flags & SlideInOrOut) != 0;

    public bool IsClosing => (_flags & 2) != 0;

    /// <summary>The cursor of <c>\A</c> is shown.</summary>
    public bool IsCursorShown => _cursorShown;

    /// <summary>The image (0 to 3) of the cursor, one per ten passes drawn; -1 while no cursor is shown.</summary>
    public int CursorImage => _cursorShown ? _cursorCounter / 10 : -1;

    /// <summary>The cursor of <c>\A</c> is shown, or the typing is done with the button allowed to close: what a press answers.</summary>
    public bool IsWaitingForPress => IsActive && !IsSliding && ((!_typingDone && !_scrollPending && (_textFlags & 8) != 0) || (_typingDone && (_closeMode & 2) != 0));

    public bool IsTypingDone => _typingDone;

    /// <summary>Where the box is in its life: <c>closed</c>, <c>slide-in</c>, <c>slide-out</c>, <c>typed</c>, <c>scroll-wait</c>, <c>scroll</c>, <c>wait-A</c> or <c>typing</c>.</summary>
    public string Phase
    {
        get
        {
            if (!IsActive)
            {
                return "closed";
            }

            if (IsClosing)
            {
                return "slide-out";
            }

            if ((_flags & 1) != 0)
            {
                return "slide-in";
            }

            if (_typingDone)
            {
                return "typed";
            }

            if (_scrollPending)
            {
                return _scrollLeft == ScrollPasses ? "scroll-wait" : "scroll";
            }

            return (_textFlags & 8) != 0 ? "wait-A" : "typing";
        }
    }

    /// <summary>How many glyphs the box has drawn since it was last <see cref="Reset"/> (a counter for the tests).</summary>
    public int GlyphCount { get; private set; }

    /// <summary>The pixels the three lines are scrolled by during the current pass (0 outside a scroll).</summary>
    public int ScrollPixels => _scrollPixels;

    /// <summary>The line (0 to 2) the next glyph goes to.</summary>
    public int LineIndex => _lineIndex;

    /// <summary>The page typed so far: the glyphs in font3 and the line breaks reached (a break comes with the glyph that follows it).</summary>
    public string TypedText => _typed.ToString();

    /// <summary>The three visible lines (the text of each band of the box, top to bottom).</summary>
    public string[] Lines => new[]
    {
        _bands[_bufferShift % 3].ToString(), _bands[(_bufferShift + 1) % 3].ToString(), _bands[(_bufferShift + 2) % 3].ToString(),
    };

    /// <summary>True once, after a pass that changed <see cref="TypedText"/> or began a page: the view asks for it to be shown.</summary>
    public bool TakeTypedChanged()
    {
        var changed = _typedChanged;
        _typedChanged = false;
        return changed;
    }

    /// <summary>InitializeDialogMessage 0x800450F0: a new box, every text state at its start; the timer of the close and the counters of the scroll are NOT reset.</summary>
    public void Open()
    {
        _tokens.Clear();
        _next = 0;
        _typed.Clear();
        _pendingNewLines = 0;
        _typedChanged = true;
        foreach (var band in _bands)
        {
            band.Clear();
        }

        _lineIndex = 0;
        _bufferShift = 0;
        _voiceRank = 0;
        _voice = -1;
        _typingDone = false;
        _scrollPending = false;
        _cursorShown = false;
        _cursorCounter = 0;
        _scrollPixels = 0;
        _delay = 1; // the first gate falls on the first call of the interpreter
        _textFlags = 3;
        _scrollMode = 3;
        _closeMode = 3;
        _stepLatched = false;
        _scrollLatched = false;
        _closeLatched = false;
        _flags = 5;
        Y = ClosedY;
        BeginSlide(ClosedY, OpenY);
        _host.PlaySound(OpenSound);
    }

    /// <summary>Appends the tokens of a page; <paramref name="hasFollowingPage"/> adds the cursor that waits for the press which turns it.</summary>
    public void AppendPage(IEnumerable<DialogueToken> page, bool hasFollowingPage)
    {
        _tokens.AddRange(page);
        if (hasFollowingPage)
        {
            _tokens.Add(new DialogueToken(DialogueTokenKind.CursorWait));
        }
    }

    /// <summary>Back to a closed box, at once and without the slide: the out-of-band close of the window, and the map entry.</summary>
    public void Reset()
    {
        GlyphCount = 0;
        _flags = 0;
        Y = ClosedY;
        _tokens.Clear();
        _next = 0;
        _typed.Clear();
        _pendingNewLines = 0;
        _cursorShown = false;
        _typingDone = false;
        _scrollPending = false;
        _stepLatched = false;
        _scrollLatched = false;
        _closeLatched = false;
        _textFlags = 3;
        _scrollMode = 3;
        _closeMode = 3;
        foreach (var band in _bands)
        {
            band.Clear();
        }
    }

    // ---- the opcodes 0x4C to 0x51 (none of them gives the hand back)

    /// <summary>0x4C: the text flags (1 the held button gates a step, 2 the delay does, 4 a 0x4D does, 8 waits for the cursor's press); clears the 0x4D in waiting (D-E19-62).</summary>
    public void SetTextFlags(int value)
    {
        _textFlags = value;
        _stepLatched = false;
    }

    /// <summary>0x4D: latches one step, if the text flags say a latch gates (<c>&amp; 4</c>).</summary>
    public void LatchStep()
    {
        if ((_textFlags & 4) != 0)
        {
            _stepLatched = true;
        }
    }

    /// <summary>0x4E: the scroll mode (1 waits ten passes, 2 a press, 4 a 0x4F, 8 starts at once).</summary>
    public void SetScrollMode(int value) => _scrollMode = value;

    /// <summary>0x4F: latches the start of a scroll, if the scroll mode says a latch starts it (<c>&amp; 4</c>).</summary>
    public void LatchScroll()
    {
        if ((_scrollMode & 4) != 0)
        {
            _scrollLatched = true;
        }
    }

    /// <summary>0x50: the close mode (1 the timer, 2 a press, 4 the script's latch).</summary>
    public void SetCloseMode(int value) => _closeMode = value;

    /// <summary>0x51: latches the close, if the close mode says the script closes (<c>&amp; 4</c>); true when it did.</summary>
    public bool LatchClose()
    {
        if ((_closeMode & 4) == 0)
        {
            return false;
        }

        _closeLatched = true;
        return true;
    }

    // ---- one pass (MsgBoxRender 0x80046EF0)

    /// <param name="squareHeld">The square button is held, as of the tick before.</param>
    /// <param name="squarePressed">The square button was pressed (rising edge), as of the tick before.</param>
    public void Pass(bool squareHeld, bool squarePressed)
    {
        _scrollPixels = 0;
        if (!IsActive)
        {
            return;
        }

        if (IsSliding)
        {
            if (AdvanceSlide())
            {
                _flags &= ~1;
                if (IsClosing)
                {
                    Y = OpenY;
                    _flags = 0;
                    _cursorShown = false;
                    _host.Released();
                    return;
                }
            }

            AnimateCursor();
            return;
        }

        if (_typingDone)
        {
            EvaluateClose(squarePressed);
            AnimateCursor();
            return;
        }

        if (_scrollPending)
        {
            Scroll(squarePressed);
            return;
        }

        Interpret(squareHeld, squarePressed);
        AnimateCursor();
    }

    private void BeginSlide(int from, int to)
    {
        _slideFrom = from;
        _slideTo = to;
        _slideStep = 0;
        _slideSettle = SlideSettlePasses;
    }

    /// <summary>One call of UpdateUiBoxesPosition: true once the slide is over (the two settling passes are used).</summary>
    private bool AdvanceSlide()
    {
        if (_slideSettle == 0)
        {
            return true;
        }

        if (_slideStep != SlideStepCount)
        {
            Y = _slideFrom + (_slideTo - _slideFrom) * _slideStep / SlideStepCount;
            _slideStep++;
        }
        else
        {
            Y = _slideTo;
            _slideSettle--;
        }

        return false;
    }

    private void AnimateCursor()
    {
        if (!_cursorShown)
        {
            return;
        }

        _cursorCounter++;
        if (_cursorCounter >= 40)
        {
            _cursorCounter = 0;
        }
    }

    private void EvaluateClose(bool squarePressed)
    {
        var trigger = false;
        if ((_closeMode & 2) != 0)
        {
            trigger = squarePressed;
        }

        if ((_closeMode & 1) != 0)
        {
            _closeTimer--;
            if (_closeTimer == 0)
            {
                trigger = true;
            }
        }

        if ((_closeMode & 4) != 0 && _closeLatched)
        {
            trigger = true;
            _closeLatched = false;
        }

        if (!trigger)
        {
            return;
        }

        _flags |= 2;
        _host.PlaySound(CloseSound);
        BeginSlide(OpenY, ClosedY);
    }

    private void Scroll(bool squarePressed)
    {
        if (_scrollLeft == ScrollPasses)
        {
            var start = false;
            if ((_scrollMode & 8) != 0)
            {
                start = true;
                _scrollMode &= ~8;
            }

            if ((_scrollMode & 2) != 0 && squarePressed)
            {
                start = true;
            }

            if ((_scrollMode & 1) != 0)
            {
                _scrollWait--;
                if (_scrollWait == 0)
                {
                    start = true;
                }
            }

            if ((_scrollMode & 4) != 0 && _scrollLatched)
            {
                start = true;
                _scrollLatched = false;
            }

            if (!start)
            {
                _cursorShown = false;
                return;
            }
        }

        _scrollLeft--;
        _scrollPixels = (ScrollPasses - _scrollLeft) * 16 / ScrollPasses;
        if (_scrollLeft == 0)
        {
            _scrollPending = false;
            _bufferShift = (_bufferShift + 1) % 3;
            _bands[(_bufferShift + _lineIndex) % 3].Clear();
        }
    }

    private void Interpret(bool squareHeld, bool squarePressed)
    {
        if ((_textFlags & 8) != 0)
        {
            if (squarePressed)
            {
                ReleaseCursor();
            }

            return;
        }

        var gate = false;
        if ((_textFlags & 2) != 0)
        {
            _delay--;
            if (_delay == 0)
            {
                _delay = StepDelay;
                gate = true;
            }
        }

        if ((_textFlags & 1) != 0 && squareHeld)
        {
            gate = true;
        }

        if ((_textFlags & 4) != 0 && _stepLatched)
        {
            gate = true;
            _stepLatched = false;
        }

        if (gate)
        {
            Step();
        }
    }

    private void ReleaseCursor()
    {
        _cursorShown = false;
        _textFlags &= ~8;
        if (_lineIndex == 2)
        {
            _scrollMode |= 8; // D-E19-63: only the release that arms the scroll of the third line starts it at once
        }

        NewLine();

        // The page that follows starts a fresh typed text (the engine's box shows one page at a time until the view of E19.f2b takes over).
        _typed.Clear();
        _pendingNewLines = 0;
        _typedChanged = true;
        _host.PageTurned();
    }

    private void NewLine()
    {
        _voiceRank = 0;
        _pendingNewLines++;
        if (_lineIndex == 2)
        {
            _scrollPending = true;
            _scrollLeft = ScrollPasses;
            if ((_scrollMode & 1) != 0)
            {
                _scrollWait = ScrollWaitPasses;
            }
        }
        else
        {
            _lineIndex++;
        }
    }

    /// <summary>One step: the free codes, then exactly one of a glyph, a new line, the cursor, the slow code, the yield code or the end of the text.</summary>
    private void Step()
    {
        while (true)
        {
            if (_next >= _tokens.Count)
            {
                _typingDone = true;
                if ((_closeMode & 1) != 0)
                {
                    _closeTimer = CloseTimerPasses;
                }

                return;
            }

            var token = _tokens[_next++];
            switch (token.Kind)
            {
                case DialogueTokenKind.Flag:
                    _host.FlagReached(token.Value);
                    continue;
                case DialogueTokenKind.Voice:
                    _voice = token.Value;
                    continue;
                case DialogueTokenKind.Center:
                    continue;
                case DialogueTokenKind.Character:
                    Draw(token.Display, counted: true);
                    return;
                case DialogueTokenKind.MarkedGlyph:
                    Draw(token.Display, counted: false);
                    return;
                case DialogueTokenKind.NewLine:
                    NewLine();
                    return;
                case DialogueTokenKind.CursorWait:
                    _cursorShown = true;
                    _textFlags |= 8;
                    return;
                case DialogueTokenKind.Slow:
                    _delay = StepDelay * 2;
                    return;
                case DialogueTokenKind.Yield:
                    return;
            }
        }
    }

    private void Draw(char display, bool counted)
    {
        for (; _pendingNewLines > 0; _pendingNewLines--)
        {
            _typed.Append('\n');
        }

        _typed.Append(display);
        GlyphCount++;
        _bands[(_bufferShift + _lineIndex) % 3].Append(display);
        _typedChanged = true;
        if (!counted)
        {
            return;
        }

        if ((_voiceRank & 1) == 0 && _voice is >= 0 and not 4)
        {
            _host.PlaySound(FirstVoiceSound + _voice);
        }

        _voiceRank++;
    }
}
