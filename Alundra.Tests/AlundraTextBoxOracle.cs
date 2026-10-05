#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests;

/// <summary>
/// E19.f2a F2A-1 (docs/plan-e19-opcodes.md section 1.2j.3, docs/plan-e19-f2a-valeurs.md section B): the oracle of the dialogue text box,
/// a C# port of the tick-exact model drawn from the binary (ALUN_CD.EXE, France: <c>MsgBoxRender</c> 0x80046EF0 and its callees; the
/// addresses are in the comments of the model, <c>e19f2-disc/model/model.py</c> of the discovery) with the two corrections of the author,
/// D-E19-62 and D-E19-63. It is written from the rules of the plan and from that model, NEVER from <c>AlundraDialogueDirector</c>: the
/// director's tests compare the director against these values, so a shared mistake would not show.
/// <para/>
/// Three parts: <see cref="AlundraTextBoxOracle"/> (the box: one <see cref="AlundraTextBoxOracle.Render"/> is one pass of the binary's
/// callback), <see cref="OraclePages"/> (the exported Yarn page back to the bytes the binary's box reads, F2-R3) and
/// <see cref="OracleHost"/> (the order of a frame of a host - production or intro harness, F2-R1 - the pads and the scripts that open,
/// write and wait, which together give every value the DLL must reach).
/// </summary>
internal sealed class AlundraTextBoxOracle
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

    // What one pass did (cleared by the host before each pass).
    public readonly List<string> Notes = new();
    public readonly List<int> Sounds = new();
    public readonly List<int> FlagsSet = new();
    public int GlyphsThisPass;
    public int GlyphTotal;

    /// <param name="corrected">true: D-E19-62 and D-E19-63 applied (what the DLL reproduces); false: the box exactly as the binary has it.</param>
    public AlundraTextBoxOracle(bool corrected = true)
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
        var before = GlyphTotal;
        if ((DialogFlags & 4) != 0)
        {
            MsgBoxRender(held, pressed);
        }

        GlyphsThisPass = GlyphTotal - before;
    }

    private void MsgBoxRender(bool held, bool pressed)
    {
        if ((DialogFlags & 3) != 0)
        {
            var done = _slide.Update(ref Y);
            if (done)
            {
                DialogFlags &= ~1;
                if ((DialogFlags & 2) != 0)
                {
                    Y = BoxY;
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
                return;
            }
        }

        ScrollCount--;
        ScrollPixels = (ScrollLength - ScrollCount) * 16 / ScrollLength;
        CursorImage = null;
        if (ScrollCount == 0)
        {
            ScrollPending = 0;
            BufferX = (BufferX + 1) % 3;
            Bands[(BufferX + LineIndex) % 3].Clear();
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
                    Cursor++; // the width of the line (centering) only matters to the view
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

/// <summary>
/// F2-R3, the oracle's side: an exported Yarn page (its text and its markers, the edge spaces already cut by D-E15-8) back to the bytes of
/// the original's box: <c>br</c> is <c>\N</c>, <c>glyph</c> a glyph <c>\W</c>, <c>voice</c>, <c>center</c> is <c>\H</c>, <c>slow</c> is <c>\T</c>,
/// <c>flag</c> is <c>\digits</c>, <c>yield</c> is <c>\Y</c>, <c>empty</c> nothing; a following page is an <c>\A</c> (the export's page
/// boundary, ADR-0006). Read from the raw <c>LineTexts</c> of the asset, never from the engine's markup parser nor from the director.
/// </summary>
internal static class OraclePages
{
    private static readonly Dictionary<char, byte> Windows1252 = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87,
        ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91,
        ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98,
        ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B, ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static readonly Dictionary<int, char> VoiceLetter = new() { [-1] = 'B', [0] = 'C', [1] = 'D', [2] = 'E', [3] = 'F', [4] = 'G' };
    private static readonly Regex Attribute = new(@"(\w+)=(-?\d+)", RegexOptions.Compiled);

    public static byte ByteOf(char ch) =>
        ch < 256 ? (byte)ch : Windows1252.TryGetValue(ch, out var b) ? b : throw new NotSupportedException($"U+{(int)ch:X4} has no byte of the box");

    public static byte[] PageBytes(string body)
    {
        var output = new List<byte>();
        var i = 0;
        while (i < body.Length)
        {
            var c = body[i];
            if (c == '\\' && i + 1 < body.Length)
            {
                output.Add(ByteOf(body[i + 1]));
                i += 2;
                continue;
            }

            if (c == '[')
            {
                var j = body.IndexOf(']', i);
                var tag = body.Substring(i + 1, j - i - 1);
                var name = tag.Split(' ')[0].TrimEnd('/');
                var attributes = Attribute.Matches(tag).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
                switch (name)
                {
                    case "br":
                        output.AddRange(new[] { (byte)'\\', (byte)'N' });
                        break;
                    case "voice":
                        output.AddRange(new[] { (byte)'\\', (byte)VoiceLetter[int.Parse(attributes["id"])] });
                        break;
                    case "center":
                        output.AddRange(new[] { (byte)'\\', (byte)'H' });
                        break;
                    case "slow":
                        output.AddRange(new[] { (byte)'\\', (byte)'T' });
                        break;
                    case "glyph":
                    {
                        var id = int.Parse(attributes["id"]);
                        output.AddRange(new[] { (byte)'\\', (byte)'W', (byte)(id < 26 ? id + 0x20 : id + 0x27) });
                        break;
                    }

                    case "flag":
                        output.Add((byte)'\\');
                        output.AddRange(attributes["id"].Select(d => (byte)d));
                        break;
                    case "yield":
                        output.AddRange(new[] { (byte)'\\', (byte)'Y' });
                        break;
                    case "empty":
                        break;
                    default:
                        throw new NotSupportedException("unknown marker " + tag);
                }

                i = j + 1;
                continue;
            }

            if (c == '{')
            {
                throw new NotSupportedException("function call in a page (its value is not modelled): " + body.Substring(i, body.IndexOf('}', i) - i + 1));
            }

            output.Add(ByteOf(c));
            i++;
        }

        return output.ToArray();
    }

    /// <summary>The pages of <paramref name="node"/> joined by <c>\A</c>; <paramref name="pageCount"/> is the number of pages.</summary>
    public static byte[] NodeBytes(DialogueAsset asset, string node, out int pageCount)
    {
        pageCount = 0;
        while (asset.LineTexts.ContainsKey($"line:{node}_p{pageCount}"))
        {
            pageCount++;
        }

        var output = new List<byte>();
        for (var k = 0; k < pageCount; k++)
        {
            if (k > 0)
            {
                output.AddRange(new[] { (byte)'\\', (byte)'A' });
            }

            output.AddRange(PageBytes(asset.LineTexts[$"line:{node}_p{k}"]));
        }

        return output.ToArray();
    }

    public static byte[] Bytes(string text) => text.Select(ByteOf).ToArray();
}

/// <summary>The square button the box reads at one pass.</summary>
internal interface IOraclePad
{
    /// <summary>Called at the start of frame <paramref name="frame"/>, before any phase runs.</summary>
    void StartFrame(int frame, AlundraTextBoxOracle box);

    /// <summary>Called after the last phase of frame <paramref name="frame"/>.</summary>
    void EndFrame(int frame, AlundraTextBoxOracle box);

    /// <summary>(held, pressed) as the box sees them at the pass of frame <paramref name="frame"/>.</summary>
    (bool Held, bool Pressed) ForBox(int frame);
}

/// <summary>No button at all.</summary>
internal sealed class OraclePadNone : IOraclePad
{
    public void StartFrame(int frame, AlundraTextBoxOracle box) { }
    public void EndFrame(int frame, AlundraTextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => (false, false);
}

/// <summary>The binary model's pad A: held and just pressed on every pass. Impossible on a real pad; only the values of the discovery's selftest use it.</summary>
internal sealed class OraclePadEveryFrame : IOraclePad
{
    public void StartFrame(int frame, AlundraTextBoxOracle box) { }
    public void EndFrame(int frame, AlundraTextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => (true, true);
}

/// <summary>An explicit pad: the (held, pressed) the box sees at each pass.</summary>
internal sealed class OraclePadScheduled : IOraclePad
{
    private readonly Dictionary<int, (bool Held, bool Pressed)> _seen;

    public OraclePadScheduled(Dictionary<int, (bool Held, bool Pressed)> seen) => _seen = seen;
    public void StartFrame(int frame, AlundraTextBoxOracle box) { }
    public void EndFrame(int frame, AlundraTextBoxOracle box) { }
    public (bool, bool) ForBox(int frame) => _seen.TryGetValue(frame, out var v) ? v : (false, false);
}

/// <summary>
/// The pad of the arcs' helper and of the harness, the same rule (plan, "Manette des arcs"): Square is held at frame f when a box is open and not
/// (the box waits for a press and Square was held at f-1) - held during the typing, then a release of one frame and a press when the box waits.
/// The two differ only by when it is decided: the arcs' helper at the start of the frame f (the box of f+1 reads it: two frames behind the state it
/// read), the harness's callback after the box pass of f (the box of f+1 reads it: one frame behind).
/// </summary>
internal sealed class OracleRulePad : IOraclePad
{
    private readonly bool _decidedAtStartOfFrame;
    private readonly Dictionary<int, bool> _held = new();

    private OracleRulePad(bool decidedAtStartOfFrame) => _decidedAtStartOfFrame = decidedAtStartOfFrame;

    public static OracleRulePad ForArcs() => new(true);

    public static OracleRulePad ForHarness() => new(false);

    private void Decide(int frame, AlundraTextBoxOracle box)
    {
        _held.TryGetValue(frame - 1, out var previous);
        _held[frame] = box.IsActive && !(box.WaitingForPress && previous);
    }

    public void StartFrame(int frame, AlundraTextBoxOracle box)
    {
        if (_decidedAtStartOfFrame)
        {
            Decide(frame, box);
        }
    }

    public void EndFrame(int frame, AlundraTextBoxOracle box)
    {
        if (!_decidedAtStartOfFrame)
        {
            Decide(frame, box);
        }
    }

    public (bool, bool) ForBox(int frame)
    {
        _held.TryGetValue(frame - 1, out var h1);
        _held.TryGetValue(frame - 2, out var h2);
        return (h1, h1 && !h2);
    }

    /// <summary>The frames at which Square is pressed (a rising edge of the held state).</summary>
    public List<int> Presses() => _held.Where(kv => kv.Value && !(_held.TryGetValue(kv.Key - 1, out var p) && p)).Select(kv => kv.Key).OrderBy(f => f).ToList();
}

/// <summary>What a script of the oracle's host does: open, write, wait. A script is an iterator; <c>yield return</c> ends its tick.</summary>
internal sealed class OracleScript
{
    private readonly OracleHost _host;
    private readonly string _phase;

    public OracleScript(OracleHost host, string phase)
    {
        _host = host;
        _phase = phase;
    }

    public AlundraTextBoxOracle Box => _host.Box;

    public int Frame => _host.Frame;

    public void Note(string what) => _host.Events.Add((_host.Frame, _phase, what));

    /// <summary>0x0D / 0x5C / 0xC4: opens, trying again every tick while a box is active.</summary>
    public IEnumerable<int> Open(byte[] text, int mode, string label = "open")
    {
        while (!Box.TryOpen(text, mode))
        {
            yield return 0;
        }

        Note(label);
    }

    /// <summary>0x39: returns the tick the box is released.</summary>
    public IEnumerable<int> Wait39(string label = "0x39")
    {
        while (Box.IsActive)
        {
            yield return 0;
        }

        Note(label);
    }

    /// <summary>0x36 on a text flag: the tick the flag is seen.</summary>
    public IEnumerable<int> Wait36(int flag, string? label = null)
    {
        while (!Box.TemporaryFlags.Contains(flag))
        {
            yield return 0;
        }

        Note(label ?? $"0x36 T{flag}");
    }

    /// <summary>0x37 n: advances in the tick T0 + n + 1.</summary>
    public IEnumerable<int> Wait37(int n)
    {
        var count = 0;
        yield return 0;
        while (true)
        {
            var old = count;
            count++;
            if (old >= n)
            {
                yield break;
            }

            yield return 0;
        }
    }

    /// <summary>0x00: the break of the tick.</summary>
    public IEnumerable<int> Break()
    {
        yield return 0;
    }

    /// <summary>k more ticks, for a wait of the physics (0x24, 0x0B).</summary>
    public IEnumerable<int> Hold(int k)
    {
        for (var i = 0; i < k; i++)
        {
            yield return 0;
        }
    }

    public void Op(string name, params int[] args)
    {
        switch (name)
        {
            case "4C": Box.Op4C(args[0]); break;
            case "4D": Box.Op4D(); break;
            case "4E": Box.Op4E(args[0]); break;
            case "4F": Box.Op4F(); break;
            case "50": Box.Op50(args[0]); break;
            case "51": Box.Op51(); break;
            default: throw new ArgumentException("not a dialogue opcode: " + name);
        }

        Note("0x" + name + string.Concat(args.Select(a => " " + a)));
    }
}

/// <summary>One pass of the box, as the host recorded it.</summary>
internal sealed record OracleRow(int Frame, string Phase, string[] Notes, int Drawn, int[] Flags, int Y, int[] Sounds, int? Cursor, string[] Rows, int ScrollPixels,
    bool Held, bool Pressed);

/// <summary>The state of the box at the end of a frame (after every phase of that frame): what a host that samples the DLL after its tick sees.</summary>
internal sealed record OracleFrameEnd(int Frame, string Phase, int Y, int GlyphTotal, bool CursorShown, bool MessageBox, bool MenuOpen);

/// <summary>
/// The order of a frame of a host (F2-R1). <b>Production</b>: the scripts of the entities (E), the pass of the box, the events of the map (M), the
/// triggers in waiting (P). <b>Intro harness</b>: the scripts of RunFrame (R), the pass of the box, the callback of the test (C). A script
/// of a phase runs one tick per frame while the gate is open: opening a MenuOpen box freezes every script from the next tick to the release.
/// </summary>
internal sealed class OracleHost
{
    public static readonly string[] Production = { "E", "box", "M", "P" };
    public static readonly string[] Harness = { "R", "box", "C" };

    private readonly string[] _order;
    private readonly IOraclePad _pad;
    private readonly List<(string Phase, Func<OracleScript, IEnumerable<int>> Factory, IEnumerator<int>? Generator, bool Alive, int StartFrame)> _scripts = new();

    public OracleHost(IOraclePad pad, string[] order, bool corrected = true)
    {
        _pad = pad;
        _order = order;
        Box = new AlundraTextBoxOracle(corrected);
    }

    public AlundraTextBoxOracle Box { get; }

    public int Frame { get; private set; }

    public List<(int Frame, string Phase, string What)> Events { get; } = new();

    public List<OracleRow> Rows { get; } = new();

    /// <summary>The state of the box at the end of each frame, in order.</summary>
    public List<OracleFrameEnd> Ends { get; } = new();

    /// <summary>Every temporary flag set by the box's steps, with the frame of the pass that set it.</summary>
    public List<(int Frame, int Flag)> AllFlags { get; } = new();

    /// <summary>Every sound of every frame (6 at an opening, 7 at a close trigger, 79 and above for the voices), the ones of the scripts included.</summary>
    public List<(int Frame, int Sound)> AllSounds { get; } = new();

    /// <summary>Adds a script of <paramref name="phase"/>; it runs from frame <paramref name="startFrame"/> on.</summary>
    public void Add(string phase, Func<OracleScript, IEnumerable<int>> script, int startFrame = 0) => _scripts.Add((phase, script, null, true, startFrame));

    private bool GateOpen => (Box.Control & AlundraTextBoxOracle.MenuOpenBit) == 0;

    private void RunPhase(string phase)
    {
        for (var i = 0; i < _scripts.Count; i++)
        {
            var s = _scripts[i];
            if (s.Phase != phase || !s.Alive || Frame < s.StartFrame || !GateOpen)
            {
                continue;
            }

            s.Generator ??= s.Factory(new OracleScript(this, phase)).GetEnumerator();
            if (!s.Generator.MoveNext())
            {
                s.Alive = false;
            }

            _scripts[i] = s;
        }
    }

    public void Step()
    {
        var frame = Frame;
        Box.ClearPassObservations();
        _pad.StartFrame(frame, Box);
        foreach (var phase in _order)
        {
            if (phase != "box")
            {
                RunPhase(phase);
                continue;
            }

            var (held, pressed) = _pad.ForBox(frame);
            Box.Render(held, pressed);
            Rows.Add(new OracleRow(frame, Box.Phase, Box.Notes.ToArray(), Box.GlyphsThisPass, Box.FlagsSet.ToArray(), Box.Y, Box.Sounds.ToArray(), Box.CursorImage,
                Box.VisibleRows(), Box.ScrollPixels, held, pressed));
        }

        _pad.EndFrame(frame, Box);
        AllSounds.AddRange(Box.Sounds.Select(s => (frame, s)));
        AllFlags.AddRange(Box.FlagsSet.Select(f => (frame, f)));
        Ends.Add(new OracleFrameEnd(frame, Box.Phase, Box.Y, Box.GlyphTotal, Box.CursorShown != 0, (Box.Control & AlundraTextBoxOracle.MessageBoxBit) != 0,
            (Box.Control & AlundraTextBoxOracle.MenuOpenBit) != 0));
        Frame++;
    }

    public OracleHost Run(int frames, Func<OracleHost, bool>? until = null)
    {
        for (var i = 0; i < frames; i++)
        {
            Step();
            if (until != null && until(this))
            {
                break;
            }
        }

        return this;
    }

    // ---- reports
    public List<int> FramesWith(string note) => Rows.Where(r => r.Notes.Contains(note)).Select(r => r.Frame).ToList();

    public List<int> GlyphFrames() => Rows.SelectMany(r => Enumerable.Repeat(r.Frame, r.Drawn)).ToList();

    public List<(int Frame, int Flag)> FlagFrames() => Rows.SelectMany(r => r.Flags.Select(f => (r.Frame, f))).ToList();

    public List<int> EventFrames(string what) =>
        Events.Where(e => e.What == what || e.What.StartsWith(what, StringComparison.Ordinal)).Select(e => e.Frame).ToList();

    /// <summary>The frames of the sounds (6, 7, voices) of every frame.</summary>
    public List<(int Frame, int Sound)> SoundFrames() => AllSounds.ToList();

    /// <summary>First and last glyph, end of typing E, trigger T and release R of the first box.</summary>
    public (int? First, int? Last, int? E, int? T, int? R) Summary()
    {
        var glyphs = GlyphFrames();
        return (glyphs.Count > 0 ? glyphs[0] : null, glyphs.Count > 0 ? glyphs[^1] : null, FirstFrameWith("typing-done"), FirstFrameWith("close-trigger"),
            FirstFrameWith("DialogClosed"));
    }

    private int? FirstFrameWith(string note)
    {
        var frames = FramesWith(note);
        return frames.Count > 0 ? frames[0] : null;
    }
}
