#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>What one drawn pass of the name box shows (E19.f4b: the logic; the view is E19.f4c2, <see cref="TextBoxNameViewModel"/>). Positions are screen pixels; the frame is at <see cref="AlundraDialogueNameBox.FrameY"/>, the text at <see cref="AlundraDialogueNameBox.TextY"/>.</summary>
internal sealed record NameBoxDrawnState(int FrameX, int TextX);

/// <summary>
/// E19.f4b (docs/plan-e19-opcodes.md, F4B-R2): the speaker's NAME box of the dialogue, a line-for-line port of <c>NameBox</c> of <c>docs/plan-e19-f4-annexe/model/f4_model.py</c>
/// (slot 12 of the UI callback dispatcher of ALUN_CD.EXE, France; globals <c>0x80180240</c>; opener <c>0x80059F6C</c>, closer <c>0x80059FE0</c>, update <c>0x8005A3E0</c>). One
/// <see cref="Pass"/> is one dispatcher pass of the slot. The flags are the binary's: 1 sliding in, 2 closing, 4 open (5 from the opener to the 18th pass, 4 at rest, 6 while it
/// leaves). The slide is the box's own (<c>UpdateUiBoxesPosition</c>: fifteen steps from x 320 to 64, then two waiting passes; the 18th pass reports done). The closer is called by
/// the text box at its close trigger and rewrites the slide unconditionally, from the current x to 320: a close during the slide-in leaves from where the box is, a second close
/// restarts the slide; the release comes on the 18th closing pass (T + 17), which draws nothing. The text is centred in the 112 px frame with the font3 advances of its glyphs.
/// The binary's clip of the text (<c>DR_AREA</c>) is not ported: it never cuts a real name (60 names x 30 positions, none cut, margin 12 px).
/// </summary>
internal sealed class AlundraDialogueNameBox
{
    public const int SlideFromX = 320;
    public const int RestX = 64;
    public const int FrameY = 140;
    public const int TextY = 148;
    public const int FrameWidth = 112;
    public const int SlideSteps = 15;
    public const int SlideSettle = 2;
    public const int FirstId = 0x100;
    public const int IdCount = 0x100;

    public const int FlagSlidingIn = 1;
    public const int FlagClosing = 2;
    public const int FlagOpen = 4;

    private int _slideStep;
    private int _slideSettle = SlideSettle;
    private int _slideStart = SlideFromX;
    private int _slideTarget = RestX;

    /// <summary>The binary's flags word (<c>0x80180240</c>): 1 sliding in, 2 closing, 4 open.</summary>
    public int Flags { get; private set; }

    /// <summary>The slot is open (the update runs each pass until the release).</summary>
    public bool IsSlotOpen { get; private set; }

    /// <summary>The binary's <c>cfg.x</c> of the frame: 64 at rest, 320 off screen on the right.</summary>
    public int X { get; private set; } = RestX;

    /// <summary>The ETC index of the name shown (<c>0x80180288</c>).</summary>
    public int NameId { get; private set; }

    /// <summary>E19.f4c2: the ETC text of the name, locked at the opening (<see cref="TryOpen"/>): the view reads this, never the speaker or the ETC again.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The width of the text, the sum of the font3 advances of its glyphs.</summary>
    public int TextWidth { get; private set; }

    /// <summary>What the last <see cref="Pass"/> drew, or null when it drew nothing (closed, or the release pass).</summary>
    public NameBoxDrawnState? Drawn { get; private set; }

    /// <summary>
    /// The opener. Refused, in this order and from the cheapest test to the dearest: a name box already open (flags 5, 4 or 6: <c>flags &amp; 4</c>), an id outside <c>0x100..0x1FF</c>, an
    /// ETC text that is empty or absent (<paramref name="resolveText"/> gives null or the empty string). Accepted: the slot opens at once (flags 5, a new slide from x 320 to 64, the
    /// first move is the first pass) and the width of the text is measured with <paramref name="advance"/>.
    /// </summary>
    public bool TryOpen(int id, Func<int, string?> resolveText, Func<char, int> advance)
    {
        if ((Flags & FlagOpen) != 0)
        {
            return false;
        }

        if (id < FirstId || id >= FirstId + IdCount)
        {
            return false;
        }

        var text = resolveText(id);
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var width = 0;
        foreach (var glyph in text)
        {
            width += advance(glyph);
        }

        NameId = id;
        Text = text;
        TextWidth = width;
        IsSlotOpen = true;
        Flags = FlagSlidingIn | FlagOpen;
        BeginSlide(SlideFromX, RestX);
        return true;
    }

    /// <summary>The closer (<c>0x80059FE0</c>), called at the close trigger of the text box. Unconditional like the binary's: the slide is rewritten from the current x to 320, the flags
    /// become closing only when the box is open.</summary>
    public void Close()
    {
        if ((Flags & FlagOpen) != 0)
        {
            Flags = FlagClosing | FlagOpen;
        }

        BeginSlide(X, SlideFromX);
    }

    /// <summary>One pass of the slot (<c>0x8005A3E0</c>), after the text box's pass of the same tick.</summary>
    public void Pass()
    {
        Drawn = null;
        if (!IsSlotOpen)
        {
            return;
        }

        if ((Flags & (FlagSlidingIn | FlagClosing)) != 0)
        {
            if (UpdateSlide())
            {
                if ((Flags & FlagSlidingIn) != 0)
                {
                    Flags &= ~FlagSlidingIn;
                }

                if ((Flags & FlagClosing) != 0)
                {
                    X = RestX;
                    Flags = 0;
                    IsSlotOpen = false;
                    return;
                }
            }
        }

        Drawn = new NameBoxDrawnState(X, X + (FrameWidth - TextWidth) / 2);
    }

    /// <summary>Back to the closed, at rest state (a map entry, a box closed out of band, the tests); the id and the width are left as they are, like the binary's.</summary>
    public void Reset()
    {
        Flags = 0;
        IsSlotOpen = false;
        X = RestX;
        Drawn = null;
        BeginSlide(SlideFromX, RestX);
    }

    private void BeginSlide(int start, int target)
    {
        _slideStep = 0;
        _slideSettle = SlideSettle;
        _slideStart = start;
        _slideTarget = target;
    }

    /// <summary><c>UpdateUiBoxesPosition</c> <c>0x80047DD0</c>: true once the waiting passes are spent (no write then), else the next position.</summary>
    private bool UpdateSlide()
    {
        if (_slideSettle == 0)
        {
            return true;
        }

        if (_slideStep != SlideSteps)
        {
            X = _slideStart + (_slideTarget - _slideStart) * _slideStep / SlideSteps;
            _slideStep++;
        }
        else
        {
            X = _slideTarget;
            _slideSettle--;
        }

        return false;
    }
}
