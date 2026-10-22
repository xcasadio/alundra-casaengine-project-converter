#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

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
        Box = new TextBoxOracle(corrected);
    }

    public TextBoxOracle Box { get; }

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

    private bool GateOpen => (Box.Control & TextBoxOracle.MenuOpenBit) == 0;

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
        Ends.Add(new OracleFrameEnd(frame, Box.Phase, Box.Y, Box.GlyphTotal, Box.CursorShown != 0, (Box.Control & TextBoxOracle.MessageBoxBit) != 0,
            (Box.Control & TextBoxOracle.MenuOpenBit) != 0));
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
