#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// E19.f2a F2A-1 (docs/plan-e19-opcodes.md section 1.2j.3, docs/plan-e19-f2a-valeurs.md section B): the oracle of the dialogue text box,
/// a C# port of the tick-exact model drawn from the binary (ALUN_CD.EXE, France: <c>MsgBoxRender</c> 0x80046EF0 and its callees; the
/// addresses are in the comments of the model, <c>e19f2-disc/model/model.py</c> of the discovery) with the two corrections of the author,
/// D-E19-62 and D-E19-63. It is written from the rules of the plan and from that model, NEVER from <c>AlundraDialogueDirector</c>: the
/// director's tests compare the director against these values, so a shared mistake would not show.
/// <para/>
/// Three parts: <see cref="TextBoxOracle"/> (the box: one <see cref="TextBoxOracle.Render"/> is one pass of the binary's
/// callback), <see cref="OraclePages"/> (the exported Yarn page back to the bytes the binary's box reads, F2-R3) and
/// <see cref="OracleHost"/> (the order of a frame of a host - production or intro harness, F2-R1 - the pads and the scripts that open,
/// write and wait, which together give every value the DLL must reach).
/// </summary>
internal sealed class TextBoxOracle
{
    /// <summary>Advance width of glyph g (the int32 at 0x800993C4 + 20 * g).</summary>
    private static readonly int[] Widths =
    {
        16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 6, 1, 11, 5, 5, 14, 14, 14, 14, 14, 14, 14, 14, 14, 1, 1, 4, 3, 5, 11, 8, 11, 10,
        3, 5, 5, 6, 7, 3, 4, 2, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 4, 4, 5, 6, 5, 6, 10, 7, 7, 7, 7, 7, 7, 7, 8, 4, 6, 7, 7, 11, 8, 7, 7, 7, 8, 7, 8, 8, 7,
        11, 7, 7, 7, 4, 8, 4, 4, 8, 4, 7, 7, 5, 7, 5, 5, 6, 7, 4, 3, 7, 4, 11, 8, 6, 7, 7, 6, 5, 4, 8, 6, 11, 6, 6, 7, 5, 3, 5, 8, 1, 1, 1, 5, 8, 7, 11,
        7, 7, 6, 16, 8, 5, 13, 1, 1, 1, 1, 4, 4, 7, 7, 5, 9, 15, 6, 14, 6, 5, 9, 1, 1, 10, 16, 3, 7, 7, 8, 9, 3, 7, 5, 11, 5, 8, 7, 5, 11, 8, 6, 8, 6, 5,
        4, 8, 8, 3, 4, 5, 6, 8, 11, 11, 12, 7, 7, 7, 7, 7, 7, 7, 13, 9, 7, 7, 7, 7, 4, 4, 4, 4, 7, 8, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 7, 7, 8, 7, 7, 7,
        7, 7, 7, 9, 5, 5, 5, 5, 5, 4, 4, 4, 4, 6, 7, 6, 6, 6, 6, 6, 8, 6, 8, 8, 8, 8, 6, 7, 6,
    };

    public const int ScrollLength = 8;      // 0x80149CAC, set at 0x80044FBC
    public const int ScrollWaitFrames = 10; // 0x80149CB8, set at 0x80044FC8
    public const int CloseTimerFrames = 360; // 0x80149CC8, set at 0x80044FF0
    public const int DelayReset = 4;        // 0x80149BD0, set at 0x80045548 at every open
    public const int BoxY = 168;
    public const int BoxYOut = 240;
    private const int BoxX = 16;               // entry cfg 0x8009CFBC: x
    private const int FrameWidthPx = 36 * 8;   // cfg.w * 8
    private const int ClipX = BoxX + 16;       // entry +8 = 16
    private const int ClipWidth = 32 * 8 + 2;  // entry +0xC = 32
    private const int ClipHeightMax = 6 * 8 + 2; // entry +0xE = 6
    private const int RowY0 = 5;               // entry +0xA
    private const int CursorX = BoxX + FrameWidthPx - 16;
    private const int CursorDy = 7 * 8 - 24;
    public const int SoundOpen = 6;
    public const int SoundClose = 7;
    public const int SoundVoice0 = 0x4F;
    public const int MessageBoxBit = 0x10;
    public const int MenuOpenBit = 0x08;

    private static readonly Dictionary<char, int> VoiceCodes = new() { ['B'] = -1, ['C'] = 0, ['D'] = 1, ['E'] = 2, ['F'] = 3, ['G'] = 4 };

    /// <summary>UpdateUiBoxesPosition 0x80047DD0 on a block {step, total = 15, settle = 2, start, target}.</summary>
    private sealed class Slide
    {
        private int _step;
        private int _settle = 2;
        private readonly int _start;
        private readonly int _target;

        public Slide(int start, int target)
        {
            _start = start;
            _target = target;
        }

        public bool Update(ref int position)
        {
            if (_settle == 0)
            {
                return true;
            }

            if (_step != 15)
            {
                position = _start + (_target - _start) * _step / 15; // truncating division, like the MIPS div
                _step++;
            }
            else
            {
                position = _target;
                _settle--;
            }

            return false;
        }
    }

    private readonly bool _corrected;
    private List<byte> _text = new() { 0 };
    private Slide _slide = new(BoxYOut, BoxY);

    // The globals of the box.
    public int DialogFlags;       // 1 slide in, 2 slide out, 4 active
    public int Control;           // bits 0x10 and 0x08 only
    public int CloseTimer;        // not reset at open
    public int ScrollCount;       // not reset at open
    public int ScrollWait;        // not reset at open
    public int TextFlags = 3;
    public int CloseMode = 3;
    public int ScrollMode = 3;
    public int PendingStep;       // the 0x4D latch
    public int PendingScroll;     // the 0x4F latch
    public int ScriptCloseRequest; // the 0x51 latch
    public int Y = BoxYOut;
    public readonly HashSet<int> TemporaryFlags = new();

    // The text state.
    public int Cursor;
    public int LineIndex;
    public int BufferX;
    public int RenderStep;
    public readonly List<int>[] Bands = { new(), new(), new() };
    public int Voice = -1;
    public int TypingDone;
    public int ScrollPending;
    public int CursorShown;
    public int CursorTick;
    public int? CursorImage;
    public int ScrollPixels;
    public int Delay = 1;

    // E19.f2b1b F2B1B-R2: the DRAWN state of a pass (docs/plan-e19-f2b1-annexe/scripts/model_drawn.py and oracle-notes.md section 1, ported from the model, never from the
    // box). CfgY is the binary's configuration Y (0x8009CFBC + 2): 168 at the start, written by the slide, restored at the release, untouched by an opening; Y above starts at 240
    // (the position the frame is drawn at) and the clip of a pass is computed from CfgY as the PREVIOUS pass left it.
    public int CfgY = BoxY;
    public readonly int[] LineWidth = new int[3]; // 0x80149BE8: the \H width of each band, 0 = not centred
    public OracleDrawn? Drawn;                    // what the pass drew; null when it drew nothing (closed, opening pass, release pass)
    private int _passCfgYBefore;
    private (int X, int Top, int Width, int Height) _passClip;

    // What one pass did (cleared by the host before each pass).
    public readonly List<string> Notes = new();
    public readonly List<int> Sounds = new();
    public readonly List<int> FlagsSet = new();
    public int GlyphsThisPass;
    public int GlyphTotal;

    /// <param name="corrected">true: D-E19-62 and D-E19-63 applied (what the DLL reproduces); false: the box exactly as the binary has it.</param>
    public TextBoxOracle(bool corrected = true)
    {
        _corrected = corrected;
        ResetTextState(Array.Empty<byte>());
    }

    public bool IsActive => (DialogFlags & 4) != 0;

    private void ResetTextState(byte[] text)
    {
        _text = new List<byte>(text) { 0 };
        Cursor = 0;
        LineIndex = 0;
        BufferX = 0;
        RenderStep = 0;
        foreach (var band in Bands)
        {
            band.Clear();
        }

        Voice = -1;
        TypingDone = 0;
        ScrollPending = 0;
        CursorShown = 0;
        CursorTick = 0;
        CursorImage = null;
        ScrollPixels = 0;
        Delay = 1; // the first gate falls on the first interpreter call
        Array.Clear(LineWidth);
        Drawn = null;
    }

    // ------------------------------------------------------------ script side
    /// <summary>TryOpenDialog 0x800423F8 then InitializeDialogMessage 0x800450F0. False: the opcode returns 0 and tries again next tick.</summary>
    public bool TryOpen(byte[] text, int mode)
    {
        if (IsActive)
        {
            return false;
        }

        ResetTextState(text);
        _slide = new Slide(BoxYOut, BoxY);
        DialogFlags = 5;
        Control |= mode == 1 ? MessageBoxBit : MenuOpenBit;
        ScriptCloseRequest = 0;
        PendingScroll = 0;
        PendingStep = 0;
        TextFlags = 3;
        ScrollMode = 3;
        CloseMode = 3;
        Sounds.Add(SoundOpen);
        return true;
    }

    public void Op4C(int value)
    {
        TextFlags = value;
        if (_corrected)
        {
            PendingStep = 0; // D-E19-62: the 0x4C clears the 0x4D in waiting
        }
    }

    public void Op4D()
    {
        if ((TextFlags & 4) != 0)
        {
            PendingStep = 1;
        }
    }

    public void Op4E(int value) => ScrollMode = value;

    public void Op4F()
    {
        if ((ScrollMode & 4) != 0)
        {
            PendingScroll = 1;
        }
    }

    public void Op50(int value) => CloseMode = value;

    public void Op51()
    {
        if ((CloseMode & 4) != 0)
        {
            ScriptCloseRequest = 1;
        }
    }

    // ------------------------------------------------------------ one pass (MsgBoxRender)
    public void Render(bool held, bool pressed)
    {
        ScrollPixels = 0;
        Drawn = null;
        var before = GlyphTotal;
        if ((DialogFlags & 4) != 0)
        {
            _passCfgYBefore = CfgY;
            _passClip = ClipFor(CfgY); // 0x80046F10-0x80046FC4: BEFORE UpdateUiBoxesPosition, from the Y the previous pass left
            MsgBoxRender(held, pressed);
        }

        GlyphsThisPass = GlyphTotal - before;
    }

    private void MsgBoxRender(bool held, bool pressed)
    {
        if ((DialogFlags & 3) != 0)
        {
            var done = _slide.Update(ref Y);
            if (!done)
            {
                CfgY = Y; // UpdateUiBoxesPosition writes cfg.Y
            }
            else
            {
                DialogFlags &= ~1;
                if ((DialogFlags & 2) != 0)
                {
                    Y = BoxY;
                    CfgY = BoxY; // cfg restored from the slide origin (0x80047038-0x80047050)
                    DialogClosed();
                    return;
                }
            }

            RenderText();
            return;
        }

        if (TypingDone != 0)
        {
            ProcessClose(pressed);
            RenderText();
            return;
        }

        if (ScrollPending != 0)
        {
            ScrollText(pressed);
            return;
        }

        Interpret(held, pressed);
        RenderText();
    }

    private void DialogClosed()
    {
        DialogFlags = 0;
        Control &= ~0x18;
        CursorImage = null;
        Notes.Add("DialogClosed");
    }

    private void RenderText()
    {
        if (CursorShown != 0)
        {
            CursorTick++;
            if (CursorTick >= 40)
            {
                CursorTick = 0;
            }

            CursorImage = CursorTick / 10;
        }
        else
        {
            CursorImage = null;
        }

        Capture("RenderText", 0, cursor: true, ending: false);
    }

    private void ProcessClose(bool pressed)
    {
        var go = false;
        if ((CloseMode & 2) != 0)
        {
            go = pressed;
        }

        if ((CloseMode & 1) != 0)
        {
            CloseTimer--;
            if (CloseTimer == 0)
            {
                go = true;
            }
        }

        if ((CloseMode & 4) != 0 && ScriptCloseRequest == 1)
        {
            go = true;
            ScriptCloseRequest = 0;
        }

        if (go)
        {
            DialogFlags |= 2;
            Sounds.Add(SoundClose);
            Notes.Add("close-trigger");
            _slide = new Slide(BoxY, BoxYOut);
        }
    }

    private void ScrollText(bool pressed)
    {
        if (ScrollCount == ScrollLength)
        {
            var start = false;
            if ((ScrollMode & 8) != 0)
            {
                start = true;
                ScrollMode &= ~8;
            }

            if ((ScrollMode & 2) != 0 && pressed)
            {
                start = true;
            }

            if ((ScrollMode & 1) != 0)
            {
                ScrollWait--;
                if (ScrollWait == 0)
                {
                    start = true;
                }
            }

            if ((ScrollMode & 4) != 0 && PendingScroll == 1)
            {
                start = true;
                PendingScroll = 0;
            }

            if (!start)
            {
                CursorShown = 0;
                CursorImage = null;
                Capture("ScrollText", 0, cursor: false, ending: false);
                return;
            }
        }

        ScrollCount--;
        ScrollPixels = (ScrollLength - ScrollCount) * 16 / ScrollLength;
        CursorImage = null;
        Capture("ScrollText", ScrollPixels, cursor: false, ending: ScrollCount == 0); // the rows are drawn BEFORE the shift
        if (ScrollCount == 0)
        {
            ScrollPending = 0;
            BufferX = (BufferX + 1) % 3;
            var band = (BufferX + LineIndex) % 3;
            Bands[band].Clear();
            LineWidth[band] = 0; // 0x80045DD8-0x80045DEC, after the rows were drawn
            Notes.Add("scroll-done");
        }
    }

    private void Interpret(bool held, bool pressed)
    {
        if ((TextFlags & 8) != 0)
        {
            if (pressed)
            {
                CursorShown = 0;
                TextFlags &= ~8;
                if (!_corrected || LineIndex == 2)
                {
                    ScrollMode |= 8; // D-E19-63: only when this release arms the scroll of the 3rd line
                }

                Notes.Add("A-release");
                NewLine();
            }

            return;
        }

        var gate = false;
        if ((TextFlags & 2) != 0)
        {
            Delay--;
            if (Delay == 0)
            {
                Delay = DelayReset;
                gate = true;
            }
        }

        if ((TextFlags & 1) != 0 && held)
        {
            gate = true;
        }

        if ((TextFlags & 4) != 0 && PendingStep == 1)
        {
            gate = true;
            PendingStep = 0;
        }

        if (gate)
        {
            Step();
        }
    }

    private int Band => (BufferX + LineIndex) % 3;

    private void Draw(int glyph, bool counted)
    {
        Bands[Band].Add(glyph);
        GlyphTotal++;
        if (counted)
        {
            if ((RenderStep & 1) == 0 && Voice != 4 && Voice >= 0)
            {
                Sounds.Add(SoundVoice0 + Voice);
            }

            RenderStep++;
        }
    }

    private void NewLine()
    {
        RenderStep = 0;
        if (LineIndex == 2)
        {
            ScrollPending = 1;
            ScrollCount = ScrollLength;
            if ((ScrollMode & 1) != 0)
            {
                ScrollWait = ScrollWaitFrames;
            }

            Notes.Add("scroll-armed");
        }
        else
        {
            LineIndex++;
        }
    }

    /// <summary>One step of the interpreter: the free codes, then exactly one of a glyph, \N, \A, \T, \Y or the end of the text.</summary>
    private void Step()
    {
        var b = _text;
        while (true)
        {
            var c0 = b[Cursor];
            if (c0 == 0)
            {
                TypingDone = 1;
                if ((CloseMode & 1) != 0)
                {
                    CloseTimer = CloseTimerFrames;
                }

                Notes.Add("typing-done");
                return;
            }

            if (c0 == 0x0A)
            {
                Cursor++;
                continue;
            }

            if (c0 is 0x7B or 0x7D)
            {
                var glyph = (b[Cursor + 1] + (c0 == 0x7B ? 0x50 : 0x90)) & 0xFF;
                Cursor += 2;
                Draw(glyph, counted: false);
                return;
            }

            if (c0 != 0x5C)
            {
                DrawNormal();
                return;
            }

            Cursor++;
            var c = b[Cursor];
            if (c < 0x30 || c >= 0x30 + 0x2A)
            {
                DrawNormal();
                return;
            }

            var ch = (char)c;
            if (ch is >= '0' and <= '9')
            {
                var j = Cursor;
                while (b[j] is >= 0x30 and <= 0x39)
                {
                    j++;
                }

                var number = 0;
                for (var i = Cursor; i < j; i++)
                {
                    number = number * 10 + (b[i] - 0x30);
                }

                Cursor = j;
                TemporaryFlags.Add(number);
                FlagsSet.Add(number);
                continue;
            }

            switch (ch)
            {
                case 'A':
                    CursorShown = 1;
                    TextFlags |= 8;
                    Cursor++;
                    Notes.Add("A-wait");
                    return;
                case 'B' or 'C' or 'D' or 'E' or 'F' or 'G':
                    Voice = VoiceCodes[ch];
                    Cursor++;
                    continue;
                case 'H':
                    Cursor++;
                    LineWidth[Band] = CalcTextWidth(Cursor); // 0x800469F0: the width of the REST of the text, up to a \A, a \N or the end
                    continue;
                case 'M':
                    Cursor++;
                    if (b[Cursor] == 0x43)
                    {
                        Cursor++;
                        if (b[Cursor] == 0x45)
                        {
                            Cursor++;
                            TextFlags = 4;
                        }
                    }

                    continue;
                case 'N':
                    Cursor++;
                    NewLine();
                    return;
                case 'T':
                    Cursor++;
                    Delay = DelayReset * 2;
                    return;
                case 'W':
                {
                    Cursor++;
                    var a = b[Cursor];
                    var glyph = a < 0x41 ? (a - 0x20) & 0xFF : (a + 0xD9) & 0xFF;
                    Cursor++;
                    Draw(glyph, counted: false);
                    return;
                }

                case 'Y':
                    Cursor++;
                    return;
                default:
                    DrawNormal(); // ':'..'@', I-L, O-S, U: the character itself is drawn
                    return;
            }
        }
    }

    private void DrawNormal()
    {
        var glyph = _text[Cursor];
        Cursor++;
        Draw(glyph, counted: true);
    }

    // ------------------------------------------------------------ the drawn state (F2B1B-R2)
    private static (int X, int Top, int Width, int Height) ClipFor(int cfgY)
    {
        var top = Math.Min(cfgY + RowY0 - 1, 239);
        var height = top + ClipHeightMax < 240 ? ClipHeightMax : 240 - top;
        return (ClipX, top, ClipWidth, height);
    }

    private static string GlyphsText(IEnumerable<int> glyphs)
    {
        var sb = new StringBuilder();
        foreach (var glyph in glyphs)
        {
            sb.Append((glyph >= 0x20 && glyph < 0x7F) || (glyph >= 0xA0 && glyph <= 0xFF) ? ((char)glyph).ToString() : $"<{glyph}>");
        }

        return sb.ToString();
    }

    private OracleDrawnRow RowOf(int i, int offset)
    {
        var band = (BufferX + i) % 3;
        var width = LineWidth[band];
        var x = width == 0 ? BoxX + 16 : BoxX + (FrameWidthPx - width) / 2; // 0x80045640-0x800456B8, the MIPS div truncates toward zero like C#
        return new OracleDrawnRow(GlyphsText(Bands[band]), x, Y + RowY0 + 16 * i - offset);
    }

    private void Capture(string kind, int offset, bool cursor, bool ending)
    {
        var rows = new[] { RowOf(0, offset), RowOf(1, offset), RowOf(2, offset) };
        var visible = (OracleDrawnRow[])rows.Clone();
        if (ending)
        {
            visible[0] = visible[0] with { Text = string.Empty }; // the ClearImage of the old top band runs before the OT is drawn (H1)
        }

        OracleCursor? cursorState = null;
        if (cursor && CursorShown != 0 && CursorImage is { } image)
        {
            cursorState = new OracleCursor(image, 0xB0 + 0x10 * image, CursorX, Y + CursorDy);
        }

        Drawn = new OracleDrawn(kind, _passCfgYBefore, Y, _passClip.X, _passClip.Top, _passClip.Width, _passClip.Height, offset, ending, rows, visible, cursorState);
    }

    /// <summary>CalcTextWidth 0x8004771C (jump table 0x80024098): the sum of the widths of the text from <paramref name="i"/> to the first 0, \A or \N.</summary>
    private int CalcTextWidth(int i)
    {
        var b = _text;
        var total = 0;
        while (i < b.Count && b[i] != 0)
        {
            var c0 = b[i];
            if (c0 == 0x7B)
            {
                total += Widths[(b[i + 1] + 0x50) & 0xFF];
                i += 2;
                continue;
            }

            if (c0 == 0x7D)
            {
                total += Widths[(b[i + 1] + 0x90) & 0xFF];
                i += 2;
                continue;
            }

            if (c0 != 0x5C)
            {
                total += Widths[c0];
                i++;
                continue;
            }

            i++;
            var c = i < b.Count ? b[i] : 0;
            var k = c - 0x30;
            if (k < 0 || k >= 0x2A)
            {
                continue; // the code char is read again as a normal char (0x800478A4)
            }

            var ch = (char)c;
            if (ch is >= '0' and <= '9')
            {
                while (i < b.Count && b[i] is >= 0x30 and <= 0x39)
                {
                    i++;
                }

                continue;
            }

            switch (ch)
            {
                case 'W':
                {
                    i++;
                    var a = b[i];
                    total += Widths[a < 0x41 ? (a - 0x20) & 0xFF : (a - 0x27) & 0xFF];
                    i++;
                    continue;
                }

                case 'X':
                    i += 2;
                    continue;
                case 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or 'T' or 'Y':
                    i++;
                    continue;
                case 'A' or 'N':
                    return total;
                default:
                    continue; // ':'..'@', H, I-M, O-S, U, V: the code char is counted as a normal char
            }
        }

        return total;
    }

    // ------------------------------------------------------------ what the tests read
    public string Phase
    {
        get
        {
            if ((DialogFlags & 4) == 0)
            {
                return "closed";
            }

            if ((DialogFlags & 2) != 0)
            {
                return "slide-out";
            }

            if ((DialogFlags & 1) != 0)
            {
                return "slide-in";
            }

            if (TypingDone != 0)
            {
                return "typed";
            }

            if (ScrollPending != 0)
            {
                return ScrollCount != ScrollLength ? "scroll" : "scroll-wait";
            }

            return (TextFlags & 8) != 0 ? "wait-A" : "typing";
        }
    }

    /// <summary>The cursor of \A, or the typing done with the button allowed to close: what the pads wait to answer.</summary>
    public bool WaitingForPress => Phase == "wait-A" || (Phase == "typed" && (CloseMode & 2) != 0);

    public string[] VisibleRows()
    {
        var rows = new string[3];
        for (var r = 0; r < 3; r++)
        {
            var sb = new StringBuilder();
            foreach (var glyph in Bands[(BufferX + r) % 3])
            {
                sb.Append((glyph >= 0x20 && glyph < 0x7F) || (glyph >= 0xA0 && glyph <= 0xFF) ? ((char)glyph).ToString() : $"<{glyph}>");
            }

            rows[r] = sb.ToString();
        }

        return rows;
    }

    public static int GlyphWidth(int glyph) => Widths[glyph];

    public void ClearPassObservations()
    {
        Notes.Clear();
        Sounds.Clear();
        FlagsSet.Clear();
    }
}

/// <summary>One band as drawn: its text (glyphs as characters, <c>&lt;n&gt;</c> for a glyph with no character), its x and its y.</summary>
internal sealed record OracleDrawnRow(string Text, int X, int Y);

/// <summary>The cursor of <c>\A</c> as drawn: its image (0 to 3), its texture u (0xB0 + 0x10 image), and where.</summary>
internal sealed record OracleCursor(int Image, int U, int X, int Y);

/// <summary>What one pass drew (<c>model_drawn.py</c>'s <c>drawn</c>): <see cref="RowsCpu"/> is the draw list of the CPU, <see cref="RowsVisible"/> what the GPU shows (the old top row blanked on the pass that ends a scroll).</summary>
internal sealed record OracleDrawn(string Kind, int CfgYBefore, int Y, int ClipX, int ClipTop, int ClipWidth, int ClipHeight, int Offset, bool Ending,
    OracleDrawnRow[] RowsCpu, OracleDrawnRow[] RowsVisible, OracleCursor? Cursor);

/// <summary>The state of the box at the end of a frame (after every phase of that frame): what a host that samples the DLL after its tick sees.</summary>
internal sealed record OracleFrameEnd(int Frame, string Phase, int Y, int GlyphTotal, bool CursorShown, bool MessageBox, bool MenuOpen);
