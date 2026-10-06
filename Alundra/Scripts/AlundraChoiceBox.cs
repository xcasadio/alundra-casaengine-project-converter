#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>What one drawn pass of the choice box shows (E19.f3a: the logic only, the view is E19.f3b). Positions are screen pixels.</summary>
internal sealed record ChoiceDrawnState(
    int FrameX, int FrameY, int Label0X, int Label1X, int LabelY,
    int CursorX, int CursorY, int CursorImage, int Selection, int CursorTick);

/// <summary>
/// E19.f3a (docs/plan-e19-opcodes.md, F3A-R1): the binary's CHOICE box (slot 3 of the callback table <c>0x800A731C</c>, update <c>0x800501FC</c>, opener
/// <c>0x80050BA8</c>), a line-for-line port of <c>ChoiceBox</c> of <c>docs/plan-e19-f3-annexe/model/choice_model.py</c>. One <see cref="Pass"/> is one dispatcher pass of
/// the slot: the init pass (nothing drawn), the slide-in (15 steps and 2 settling passes), the interactive passes, the slide-out, the close that writes the result
/// word. It reads the pad words of the PREVIOUS tick (the caller's concern): just-pressed (Cross validates, no cancel) and by-interval (Left and Right move the
/// selection). The sounds, the labels (cut at 6 characters, <c>strncpy</c> n = 6) and the cursor counter (never reset by the opener, wraps at 40) are the binary's.
/// The labels are the font3 characters of the dialogue asset: the binary's own <c>{c</c> and <c>}c</c> escape pairs, which its first update rewrites, have no
/// counterpart here (no label of the corpus carries one).
/// </summary>
internal sealed class AlundraChoiceBox
{
    internal enum Stage { Closed, Init, SlideIn, Active, SlideOut }

    public const int FrameX = 176;
    public const int FrameY = 144;
    public const int SlideFromX = 320;
    public const int LabelDx = 16;
    public const int LabelDy = 8;
    public const int LabelPitch = 48;
    public const int CursorDy = -8;
    public const int SlideSteps = 15;
    public const int SlideSettle = 2;
    public const int CursorImageCount = 4;
    public const int CursorTickWrap = 40;
    public const int MaxLabelLength = 6;

    public const int SfxMove = 1;
    public const int SfxOpen = 4;
    public const int SfxClick = 5;
    public const int SfxFirst = 2;
    public const int SfxSecond = 3;

    private const uint PadCross = AlundraPadState.Cross;
    private const uint PadLeft = AlundraPadState.Left;
    private const uint PadRight = AlundraPadState.Right;

    private readonly List<int> _sounds = new();
    private string[] _labels = { string.Empty, string.Empty };
    private int _slideStep;
    private int _slideSettle;
    private int _slideStart;
    private int _slideTarget;
    private int _cfgX = FrameX;

    public Stage CurrentStage { get; private set; } = Stage.Closed;

    public bool IsActive => CurrentStage != Stage.Closed;

    /// <summary>The next pass reads the pad (its update function is the interactive one).</summary>
    public bool AcceptsInputThisPass => CurrentStage == Stage.Active;

    public int Selection { get; private set; }

    /// <summary><c>0x8017E640</c>: selection + 1 once Cross is seen.</summary>
    public int ResultAtPress { get; private set; }

    /// <summary>The caller's result word (<c>0x8013D8D0</c> for 0x44): 0 pending, 1 first option, 2 second; written when the slide-out ends.</summary>
    public int ResultWord { get; private set; }

    /// <summary><c>0x8017E644</c>: the cursor counter, NEVER reset at open (wraps at 40); the cursor image is the counter / 10.</summary>
    public int CursorTick { get; private set; }

    public IReadOnlyList<int> SoundsOfLastPass => _sounds;

    /// <summary>The drawn state of the last pass, or null when it drew nothing (init pass, close pass, inactive).</summary>
    public ChoiceDrawnState? Drawn { get; private set; }

    public bool ClosedThisPass { get; private set; }

    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Opener <c>0x80050BA8</c> (and <c>0x80050C00</c> with <paramref name="defaultSelection"/>): the labels are cut at 6 characters, the slot is opened, the
    /// selection set and the result word zeroed; the cursor counter is NOT touched. Returns the sound of the opener (4), which the caller plays at once.</summary>
    public int Open(string yes, string no, int defaultSelection = 0)
    {
        _labels = new[] { Cut(yes), Cut(no) };
        CurrentStage = Stage.Init;
        Selection = defaultSelection;
        ResultWord = 0;
        ResultAtPress = 0;
        _cfgX = FrameX;
        ClosedThisPass = false;
        Drawn = null;
        _sounds.Clear();
        return SfxOpen;
    }

    private static string Cut(string label) => label.Length <= MaxLabelLength ? label : label.Substring(0, MaxLabelLength);

    /// <summary>The caller read the word (0x44 takes it, then asks again the next time it is reached): back to 0.</summary>
    public void ClearResultWord() => ResultWord = 0;

    /// <summary>Drops the box without a result (an abandoned flow, a map entry, another box opened): the DLL's, not the binary's. The cursor counter stays.</summary>
    public void Cancel()
    {
        CurrentStage = Stage.Closed;
        ResultWord = 0;
        ResultAtPress = 0;
        _cfgX = FrameX;
        Drawn = null;
        ClosedThisPass = false;
        _sounds.Clear();
    }

    public void SeedCursorTickForTests(int value) => CursorTick = value;

    public void ResetForTests()
    {
        Cancel();
        CursorTick = 0;
        Selection = 0;
        _labels = new[] { string.Empty, string.Empty };
    }

    /// <summary>One dispatcher pass. <paramref name="pressed"/> is the just-pressed word, <paramref name="interval"/> the by-interval word (both of the tick before).</summary>
    public void Pass(uint pressed, uint interval)
    {
        _sounds.Clear();
        Drawn = null;
        ClosedThisPass = false;
        switch (CurrentStage)
        {
            case Stage.Closed:
                return;

            case Stage.Init: // 0x800501FC: labels decoded, slide armed, first UpdateUiBoxesPosition (x = 320), nothing drawn
                _slideStep = 0;
                _slideSettle = SlideSettle;
                _slideStart = SlideFromX;
                _slideTarget = FrameX;
                UpdateSlide();
                CurrentStage = Stage.SlideIn;
                return;

            case Stage.SlideIn: // 0x800501A4
                if (UpdateSlide())
                {
                    CurrentStage = Stage.Active;
                }

                Draw();
                return;

            case Stage.Active: // 0x8004FFA8
                if ((pressed & PadCross) != 0)
                {
                    ResultAtPress = Selection + 1;
                    _slideStep = 0;
                    _slideSettle = SlideSettle;
                    _slideStart = FrameX;
                    _slideTarget = SlideFromX;
                    _sounds.Add(SfxClick);
                    _sounds.Add(ResultAtPress == 1 ? SfxFirst : SfxSecond);
                    CurrentStage = Stage.SlideOut;
                }

                if ((interval & PadLeft) != 0)
                {
                    if (Selection == 1)
                    {
                        _sounds.Add(SfxMove);
                    }

                    Selection = 0;
                }

                if ((interval & PadRight) != 0)
                {
                    if (Selection == 0)
                    {
                        _sounds.Add(SfxMove);
                    }

                    Selection = 1;
                }

                Draw();
                return;

            case Stage.SlideOut: // 0x8004FEFC
                if (UpdateSlide())
                {
                    _cfgX = FrameX;
                    CurrentStage = Stage.Closed;
                    ResultWord = ResultAtPress;
                    ClosedThisPass = true;
                }
                else
                {
                    Draw();
                }

                return;
        }
    }

    /// <summary><c>UpdateUiBoxesPosition</c> <c>0x80047DD0</c> on the slide block: true once the slide is over (its two settling passes used).</summary>
    private bool UpdateSlide()
    {
        if (_slideSettle == 0)
        {
            return true;
        }

        if (_slideStep != SlideSteps)
        {
            _cfgX = _slideStart + (_slideTarget - _slideStart) * _slideStep / SlideSteps; // C# integer division truncates toward zero, as the binary's
            _slideStep++;
        }
        else
        {
            _cfgX = _slideTarget;
            _slideSettle--;
        }

        return false;
    }

    private void Draw() // 0x8004FCE8
    {
        CursorTick++;
        if (CursorTick == CursorTickWrap)
        {
            CursorTick = 0;
        }

        var x = _cfgX;
        var cursorX = x + LabelDx + LabelPitch * Selection + 4 * _labels[Selection].Length - 8;
        Drawn = new ChoiceDrawnState(
            x, FrameY, x + LabelDx, x + LabelDx + LabelPitch, FrameY + LabelDy,
            cursorX, FrameY + CursorDy, CursorTick / 10, Selection, CursorTick);
    }
}

/// <summary>
/// E19.f3a (F3A-R4): the presses of a player answering the open choice, for the hook of the tests (<c>SelectChoiceForTests</c>): Left or Right until the wanted option is
/// selected, then Cross, one press per pass, from the first interactive pass that follows the arming. The words it yields replace the pad of the tick before.
/// </summary>
internal sealed class AlundraChoiceAnswerPlan
{
    public AlundraChoiceAnswerPlan(int index) => Index = index;

    public int Index { get; }

    public AlundraTickPad Pad { get; } = new();

    public bool CrossSent { get; private set; }

    /// <summary>The held word of this pass for a choice whose state is given.</summary>
    public uint HeldWord(bool acceptsInput, int selection)
    {
        if (!acceptsInput || CrossSent)
        {
            return 0;
        }

        if (selection != Index)
        {
            return Index > selection ? AlundraPadState.Right : AlundraPadState.Left;
        }

        CrossSent = true;
        return AlundraPadState.Cross;
    }
}
