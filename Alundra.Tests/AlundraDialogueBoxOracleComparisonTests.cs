#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Assets;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f2a F2A-2 (docs/plan-e19-opcodes.md section 1.2j.3, "tests unitaires de la boîte contre l'oracle"): the DLL's box, driven at the proxy's own
/// <see cref="AlundraWorldProxy.Update"/> in the production order (<see cref="DialogueBoxMontage"/>), against <see cref="AlundraTextBoxOracle"/> frame by frame - the
/// state at the end of each frame (phase, glyphs drawn, cursor, MessageBox and MenuOpen, the position of the box where it is comparable), the first frame of each text
/// flag, and every sound with its frame - on the arcs' pad (<see cref="OracleRulePad.ForArcs"/>, the rule of <c>RunUntilPressingTheButtonOnEveryDialogueFrame</c>, which the
/// montage applies the same way). Two families: synthetic boxes (opcodes 0x4C to 0x51 at chosen ticks, both origins), and boxes of the real export (the three real texts
/// of the plan and the nodes of the maps of the arcs), which the oracle reads from the exported pages and the director from the compiled Yarn of the same asset.
/// </summary>
public sealed class AlundraDialogueBoxOracleComparisonTests
{
    /// <summary>A box to compare: the text (an asset and its node), the control mode, the origin ("E" an entity script, "M" a map event), the frame of the opening, the
    /// opcodes written at ticks relative to the opening ("4C" and "4E" and "50" take an argument; "4D", "4F" and "51" none), and the pad (the arcs' rule, or no button at all).</summary>
    private sealed record Box(string Name, DialogueAsset Asset, string Node, int Mode, string Origin, int OpenAt, (int Rel, string Op, int Arg)[] Ops, bool ArcsPad = true);

    private static readonly (int Rel, string Op, int Arg)[] NoOps = Array.Empty<(int, string, int)>();

    // ---- the scripts, once for the oracle and once for the DLL ----------------------------------------------------------

    private static bool TakesAnArgument(string op) => op is "4C" or "4E" or "50";

    private static IEnumerable<int> OracleScript(OracleScript script, Box box, byte[] text)
    {
        foreach (var x in script.Open(text, box.Mode))
        {
            yield return x;
        }

        var n = script.Frame;
        foreach (var (_, op, arg) in box.Ops.Where(o => o.Rel == 0))
        {
            script.Op(op, TakesAnArgument(op) ? new[] { arg } : Array.Empty<int>());
        }

        var last = box.Ops.Length == 0 ? 0 : box.Ops.Max(o => o.Rel);
        while (true)
        {
            var now = script.Frame - n;
            if (now > 0)
            {
                foreach (var (_, op, arg) in box.Ops.Where(o => o.Rel == now))
                {
                    script.Op(op, TakesAnArgument(op) ? new[] { arg } : Array.Empty<int>());
                }
            }

            if (!script.Box.IsActive && now >= last)
            {
                yield break;
            }

            yield return 0;
        }
    }

    private static Action<int> DllScript(DialogueBoxMontage montage, Box box)
    {
        var opened = false;
        var n = 0;
        return frame =>
        {
            if (!opened)
            {
                if (frame < box.OpenAt)
                {
                    return;
                }

                montage.Director.Open(box.Asset, box.Node, box.Mode);
                opened = true;
                n = frame;
                foreach (var (_, op, arg) in box.Ops.Where(o => o.Rel == 0))
                {
                    Apply(montage.Director, op, arg);
                }

                return;
            }

            var now = frame - n;
            if (now <= 0)
            {
                return;
            }

            foreach (var (_, op, arg) in box.Ops.Where(o => o.Rel == now))
            {
                Apply(montage.Director, op, arg);
            }
        };
    }

    private static void Apply(AlundraDialogueDirector director, string op, int arg)
    {
        switch (op)
        {
            case "4C": director.SetTextFlags(arg); break;
            case "4D": director.LatchTextStep(); break;
            case "4E": director.SetScrollMode(arg); break;
            case "4F": director.LatchScrollStart(); break;
            case "50": director.SetCloseMask(arg); break;
            case "51": director.RequestScriptClose(); break;
            default: throw new ArgumentException(op);
        }
    }

    // ---- one run of each --------------------------------------------------------------------------------------------------

    private sealed record FrameEnd(string Phase, int Glyphs, bool Cursor, bool MessageBox, bool MenuOpen, int Y);

    private static readonly Regex FlagMarker = new(@"\[flag id=(\d+)", RegexOptions.Compiled);

    /// <summary>Compares the two runs of <paramref name="box"/>; returns false, comparing nothing, when the oracle cannot read the page (a function call in it, a character with no byte).</summary>
    private static bool Compare(Box box, int maxFrames = 4000)
    {
        byte[] text;
        try
        {
            text = OraclePages.NodeBytes(box.Asset, box.Node, out var pages);
            if (pages == 0)
            {
                return false;
            }
        }
        catch (NotSupportedException)
        {
            return false;
        }

        // The oracle, on the arcs' pad.
        var oracle = new OracleHost(box.ArcsPad ? OracleRulePad.ForArcs() : new OraclePadNone(), OracleHost.Production);
        oracle.Add(box.Origin, script => OracleScript(script, box, text), box.OpenAt);
        var oracleOpened = -1;
        var oracleReleased = -1;
        for (var f = 0; f < maxFrames; f++)
        {
            oracle.Step();
            if (oracleOpened < 0 && oracle.EventFrames("open").Count > 0)
            {
                oracleOpened = oracle.EventFrames("open")[0];
            }

            if (oracleOpened >= 0 && oracleReleased < 0 && !oracle.Box.IsActive)
            {
                oracleReleased = f;
            }

            if (oracleReleased >= 0 && f >= oracleReleased + 3)
            {
                break;
            }
        }

        // The DLL, in the production order, on the same rule of the button.
        using var montage = new DialogueBoxMontage();
        var held = new Dictionary<int, bool>();
        if (box.ArcsPad)
        {
            montage.PadForFrame = frame =>
            {
                held.TryGetValue(frame - 1, out var previous);
                var hold = montage.Director.IsOpen && !(montage.Director.Box.IsWaitingForPress && previous);
                held[frame] = hold;
                return hold ? AlundraPadState.Square : 0u;
            };
        }
        var script = DllScript(montage, box);
        if (box.Origin == "E")
        {
            montage.EntityScript = script;
        }
        else
        {
            montage.MapEventScript = () => script(montage.Frame);
        }

        var flags = box.Asset.LineTexts.Where(l => l.Key.StartsWith($"line:{box.Node}_p", StringComparison.Ordinal))
            .SelectMany(l => FlagMarker.Matches(l.Value).Select(m => int.Parse(m.Groups[1].Value))).Distinct().ToArray();
        var firstFlagFrame = flags.ToDictionary(flag => flag, _ => (int?)null);
        var ends = new List<FrameEnd>();
        var opened = false;
        var released = -1;
        for (var f = 0; f < maxFrames; f++)
        {
            montage.RunFrame();
            var b = montage.Director.Box;
            ends.Add(new FrameEnd(
                b.Phase, b.GlyphCount, b.IsCursorShown,
                (montage.State.PlayerControlFlags & AlundraGameState.PlayerControlBits.MessageBox) != 0,
                (montage.State.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0, b.Y));
            foreach (var flag in flags)
            {
                if (firstFlagFrame[flag] == null && (montage.State.GetFlag((uint)flag | 0x8000u) & (1u << (flag & 0x1f))) != 0)
                {
                    firstFlagFrame[flag] = f;
                }
            }

            opened |= montage.Director.IsOpen;
            if (opened && !montage.Director.IsOpen && released < 0)
            {
                released = f;
            }

            if (released >= 0 && f >= released + 3)
            {
                break;
            }
        }

        var frames = Math.Min(oracle.Ends.Count, ends.Count);
        Assert.True(frames > 20, $"{box.Name}: the run is too short to compare ({frames} frames)");
        Assert.True(oracle.Ends.Count == ends.Count, $"{box.Name}: the oracle ran {oracle.Ends.Count} frames, the DLL {ends.Count} (the box was released at a different frame)");
        for (var f = 0; f < frames; f++)
        {
            var o = oracle.Ends[f];
            var d = ends[f];
            var where = $"{box.Name}, frame {f}";
            Assert.True(o.Phase == d.Phase, $"{where}: phase {d.Phase}, oracle {o.Phase}");
            Assert.True(o.GlyphTotal == d.Glyphs, $"{where}: {d.Glyphs} glyphs drawn, oracle {o.GlyphTotal}");
            Assert.True(o.CursorShown == d.Cursor, $"{where}: cursor shown {d.Cursor}, oracle {o.CursorShown}");
            Assert.True(o.MessageBox == d.MessageBox && o.MenuOpen == d.MenuOpen, $"{where}: MessageBox/MenuOpen ({d.MessageBox}, {d.MenuOpen}), oracle ({o.MessageBox}, {o.MenuOpen})");

            // The box's y is the binary's (240 closed, 168 open, the slide in between). The one place it is not comparable: a box opened by a map event is opened AFTER the
            // pass of its frame, and the director shows its box at the start of the slide (240) from that instant, where the binary's y still holds the one of the last release.
            if (box.Origin == "E" || f != oracleOpened)
            {
                Assert.True(o.Y == d.Y, $"{where}: y {d.Y}, oracle {o.Y}");
            }
        }

        var oracleSounds = oracle.AllSounds.Where(s => s.Frame < frames).ToList();
        var dllSounds = montage.Sounds.Played.Where(s => s.Frame < frames).Select(s => (s.Frame, s.Sfx)).ToList();
        Assert.True(
            oracleSounds.SequenceEqual(dllSounds),
            $"{box.Name}: the sounds differ. DLL [{string.Join(", ", dllSounds.Take(14))}], oracle [{string.Join(", ", oracleSounds.Take(14))}]");

        foreach (var flag in flags)
        {
            var expected = oracle.AllFlags.Where(a => a.Flag == flag && a.Frame < frames).Select(a => (int?)a.Frame).FirstOrDefault();
            Assert.True(firstFlagFrame[flag] == expected, $"{box.Name}: the flag {flag} was first set at frame {firstFlagFrame[flag]}, oracle {expected}");
        }

        return true;
    }

    // ---- synthetic boxes ------------------------------------------------------------------------------------------------------

    private const string Br = "[br trimwhitespace=false/]";

    private static IEnumerable<Box> Synthetic()
    {
        DialogueAsset Asset(string name, params string[] pages) => DialogueTestAssets.BuildRaw(name, "Start", pages);

        foreach (var origin in new[] { "E", "M" })
        {
            yield return new Box($"plain-{origin}", Asset("plain", "bonjour"), "Start", 1, origin, 10, NoOps);
            yield return new Box($"two-pages-{origin}", Asset("pages", "page un", "page deux"), "Start", 1, origin, 3, NoOps);
            yield return new Box($"four-lines-{origin}", Asset("lines", $"a{Br}b{Br}c{Br}d"), "Start", 1, origin, 0, NoOps);
            yield return new Box($"slow-voice-{origin}",
                Asset("voice", "[voice id=2 trimwhitespace=false/]ab[slow trimwhitespace=false/]cd[voice id=4 trimwhitespace=false/]ef"), "Start", 1, origin, 5, NoOps);
            yield return new Box($"flags-{origin}",
                Asset("flags", "Salut[flag id=10 trimwhitespace=false/][yield trimwhitespace=false/] ami", "page one", "[flag id=20 trimwhitespace=false/]page two"), "Start", 1, origin, 1, NoOps);
            yield return new Box($"close-mode-4-{origin}", Asset("mask4", "hello"), "Start", 1, origin, 2, new[] { (0, "50", 4), (100, "51", 0) });
            yield return new Box($"manual-latch-{origin}", Asset("latch", "ABCDEFGH"), "Start", 1, origin, 0,
                new[] { (0, "4C", 4), (25, "4D", 0), (25, "4C", 3), (40, "4C", 4), (45, "4D", 0), (60, "4D", 0) });
            yield return new Box($"scroll-mode-4-{origin}", Asset("scroll", $"a{Br}b{Br}c{Br}d{Br}e"), "Start", 1, origin, 0, new[] { (0, "4E", 4), (70, "4F", 0), (110, "4F", 0) });
            yield return new Box($"menu-open-{origin}", Asset("menu", "menu box"), "Start", 0, origin, 4, NoOps);

            // The same boxes on no button at all: the typing at its own pace (a step every four passes), the timer of the close.
            yield return new Box($"plain-nopad-{origin}", Asset("plainnp", "bonjour"), "Start", 1, origin, 10, NoOps, ArcsPad: false);
            yield return new Box($"four-lines-nopad-{origin}", Asset("linesnp", $"a{Br}b{Br}c{Br}d"), "Start", 1, origin, 0, NoOps, ArcsPad: false);
            yield return new Box($"slow-voice-nopad-{origin}",
                Asset("voicenp", "[voice id=2 trimwhitespace=false/]ab[slow trimwhitespace=false/]cd[voice id=4 trimwhitespace=false/]ef"), "Start", 1, origin, 5, NoOps, ArcsPad: false);
            yield return new Box($"flags-nopad-{origin}",
                Asset("flagsnp", "Salut[flag id=10 trimwhitespace=false/][yield trimwhitespace=false/] ami[flag id=999 trimwhitespace=false/]"), "Start", 1, origin, 1, NoOps, ArcsPad: false);
            yield return new Box($"manual-latch-nopad-{origin}", Asset("latchnp", "ABCDEFGH"), "Start", 1, origin, 0,
                new[] { (0, "4C", 4), (25, "4D", 0), (25, "4C", 3), (40, "4C", 4), (45, "4D", 0), (60, "4D", 0) }, ArcsPad: false);
            yield return new Box($"held-gate-nopad-{origin}", Asset("heldnp", "ABCDEFGH"), "Start", 1, origin, 0, new[] { (0, "4C", 4), (3, "4D", 0), (5, "4D", 0), (30, "4C", 8) }, ArcsPad: false);
            yield return new Box($"scroll-mode-4-nopad-{origin}", Asset("scrollnp", $"a{Br}b{Br}c{Br}d{Br}e"), "Start", 1, origin, 0, new[] { (0, "4E", 4), (70, "4F", 0), (110, "4F", 0) }, ArcsPad: false);
            yield return new Box($"close-mode-4-nopad-{origin}", Asset("mask4np", "hello"), "Start", 1, origin, 2, new[] { (0, "50", 4), (100, "51", 0) }, ArcsPad: false);
            yield return new Box($"mask-6-late-latch-nopad-{origin}", Asset("mask6np", "hello"), "Start", 1, origin, 2, new[] { (0, "50", 6), (150, "51", 0) }, ArcsPad: false);
        }
    }

    public static IEnumerable<object[]> SyntheticNames() => Synthetic().Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(SyntheticNames))]
    public void ASyntheticBox_FollowsTheOracle_FrameByFrame(string name)
    {
        Assert.True(Compare(Synthetic().Single(s => s.Name == name)), $"{name}: the oracle could not read the page");
    }

    // ---- the real export ------------------------------------------------------------------------------------------------------

    private static readonly int[] SweptMaps = { 389, 391, 164, 163, 162, 165, 179, 172, 185, 10, 176, 178, 135, 476, 390 };

    private static DialogueAsset LoadMapAsset(int mapId)
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var file = Directory.GetFiles(Path.Combine(root, "Maps"), $"*-{mapId}.dialogue", SearchOption.AllDirectories)
            .Single(p => Path.GetFileNameWithoutExtension(p).EndsWith("-" + mapId, StringComparison.Ordinal));
        return DialogueTestAssets.LoadFromDisk(file);
    }

    private static IEnumerable<string> NodesOf(DialogueAsset asset) =>
        asset.LineTexts.Keys.Select(k => k.Substring(5, k.LastIndexOf("_p", StringComparison.Ordinal) - 5)).Distinct().OrderBy(n => n, StringComparer.Ordinal);

    [Fact]
    public void TheThreeRealTextsOfThePlan_FollowTheOracle_UnderTheArcsPad()
    {
        // The texts of the plan's section B.2: M389_S001 and S002 (the sailor 12), M391_S019, S020 and S022 (the ship), M164_S003 (Septimus).
        foreach (var (mapId, node) in new[] { (389, "M389_S001"), (389, "M389_S002"), (391, "M391_S019"), (391, "M391_S020"), (391, "M391_S022"), (164, "M164_S003") })
        {
            foreach (var origin in new[] { "E", "M" })
            {
                Assert.True(Compare(new Box($"{node}-{origin}", LoadMapAsset(mapId), node, 1, origin, 0, NoOps)), $"{node}: the oracle could not read the page");
            }
        }
    }

    [Fact]
    public void TheBoxesOfTheMapsOfTheArcs_FollowTheOracle_UnderTheArcsPad()
    {
        // Every fourth node of each map of the arcs' sweep (the oracle skips a page with a function call in it): a few hundred frames each, the whole export of these
        // maps being the audit's own sweep (475 nodes, run once against the Python oracle).
        var compared = 0;
        foreach (var mapId in SweptMaps)
        {
            var asset = LoadMapAsset(mapId);
            foreach (var node in NodesOf(asset).Where((_, i) => i % 4 == 0))
            {
                if (Compare(new Box($"{node}-sweep", asset, node, 1, "E", 0, NoOps)))
                {
                    compared++;
                }
            }
        }

        Assert.True(compared > 60, $"only {compared} boxes of the sweep could be compared");
    }
}
