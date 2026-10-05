#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Dialogue.Assets;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f2a F2A-1 (docs/plan-e19-opcodes.md section 1.2j.3, task F2A-1): the oracle's own tests. Every number below is written in
/// <c>docs/plan-e19-f2a-valeurs.md</c> (section B for the values of the discovery's <c>selftest()</c> and of the three real texts, section
/// C and K for the synthetic boxes of the director's tests) and was drawn from the binary's model before this oracle ran: a test that goes
/// red here is a fault of the oracle, never a value to re-pin. N is the tick of the opening.
/// </summary>
public sealed class AlundraTextBoxOracleTests
{
    private static OracleHost Production(IOraclePad pad, bool corrected = true) => new(pad, OracleHost.Production, corrected);

    private static OracleHost Harness(IOraclePad pad, bool corrected = true) => new(pad, OracleHost.Harness, corrected);

    /// <summary>The discovery's <c>simple_script</c>: opens, applies the opcodes of the opening tick, then the opcodes of each later tick (relative to the opening), then waits for the release.</summary>
    private static Func<OracleScript, IEnumerable<int>> Simple(byte[] text, int mode = 0, (string Name, int[] Args)[]? sameTick = null,
        Dictionary<int, (string Name, int[] Args)[]>? timed = null) =>
        script => SimpleBody(script, text, mode, sameTick ?? Array.Empty<(string, int[])>(), timed ?? new());

    private static IEnumerable<int> SimpleBody(OracleScript script, byte[] text, int mode, (string Name, int[] Args)[] sameTick,
        Dictionary<int, (string Name, int[] Args)[]> timed)
    {
        foreach (var x in script.Open(text, mode))
        {
            yield return x;
        }

        var n = script.Frame;
        foreach (var (name, args) in sameTick)
        {
            script.Op(name, args);
        }

        var released = false;
        var last = timed.Count == 0 ? 0 : timed.Keys.Max();
        while (true)
        {
            var rel = script.Frame - n;
            if (timed.TryGetValue(rel, out var ops))
            {
                foreach (var (name, args) in ops)
                {
                    script.Op(name, args);
                }
            }

            if (!released && !script.Box.IsActive)
            {
                script.Note("0x39 released");
                released = true;
            }

            if (released && rel >= last)
            {
                yield break;
            }

            yield return 0;
        }
    }

    private static (string, int[]) Op(string name, params int[] args) => (name, args);

    private static OracleHost RunSimple(byte[] text, IOraclePad pad, int frames, string origin = "M", int mode = 0, bool corrected = true,
        (string Name, int[] Args)[]? sameTick = null, Dictionary<int, (string Name, int[] Args)[]>? timed = null)
    {
        var host = Production(pad, corrected);
        host.Add(origin, Simple(text, mode, sameTick, timed));
        return host.Run(frames);
    }

    private static byte[] Text(string text) => OraclePages.Bytes(text);

    private static int[] AtFrames(OracleHost host, string note) => host.FramesWith(note).ToArray();

    // ---- O-01 .. O-11: the values of selftest() --------------------------------------------------------------------------

    [Fact]
    public void O01_TheEntrySlide_GoesDownFifteenSteps_ThenWaitsThreeFramesBeforeTheFirstLetter()
    {
        var host = RunSimple(Text("AB"), new OraclePadNone(), 1000);

        Assert.Equal(
            new[] { 240, 236, 231, 226, 221, 216, 212, 207, 202, 197, 192, 188, 183, 178, 173, 168, 168, 168 },
            host.Rows.Skip(1).Take(18).Select(r => r.Y).ToArray());
        Assert.Equal(19, host.GlyphFrames()[0]);
    }

    [Fact]
    public void O02_AB_WithNoPad_TypesAt19And23_EndsAt27_ClosesByTheTimerAt387_ReleasesAt405()
    {
        var host = RunSimple(Text("AB"), new OraclePadNone(), 1000);

        Assert.Equal(new[] { 19, 23 }, host.GlyphFrames());
        var (first, last, e, t, r) = host.Summary();
        Assert.Equal((19, 23, 27, 387, 405), (first, last, e, t, r));
        Assert.Equal(405, host.EventFrames("0x39 released")[0]);
    }

    [Fact]
    public void O03_AB_UnderThePadOfTheBinary_TypesAt19And20_EndsAt21_ClosesAt22_ReleasesAt40()
    {
        var host = RunSimple(Text("AB"), new OraclePadEveryFrame(), 200);

        Assert.Equal(new[] { 19, 20 }, host.GlyphFrames());
        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((21, 22, 40), (e, t, r));
    }

    [Fact]
    public void O04_TheSlowCode_PutsEightFramesBetweenTheTwoLetters()
    {
        var host = RunSimple(Text(@"A\TB"), new OraclePadNone(), 100);

        Assert.Equal(new[] { 19, 31 }, host.GlyphFrames());
    }

    [Fact]
    public void O05_ALineOfFour_ArmsTheScrollOfTheThirdLine_WaitsTenFrames_ThenScrollsEightTimesTwoPixels()
    {
        var host = RunSimple(Text(@"a\Nb\Nc\Nd"), new OraclePadNone(), 200);

        Assert.Equal(new[] { 19, 27, 35, 60 }, host.GlyphFrames());
        Assert.Equal(
            Enumerable.Range(0, 8).Select(k => (49 + k, 2 * (k + 1))).ToArray(),
            host.Rows.Where(r => r.ScrollPixels != 0).Select(r => (r.Frame, r.ScrollPixels)).ToArray());
    }

    [Fact]
    public void O06_AnAReleasedOnTheFirstLine_ThenAScrollOfTheThirdLine_BinaryStartsItAtOnce_TheCorrectedBoxWaitsItsTenFrames()
    {
        var pad = () => new OraclePadScheduled(new() { [24] = (true, true) });

        var binary = RunSimple(Text(@"a\Ab\Nc\Nd"), pad(), 200, corrected: false);
        var corrected = RunSimple(Text(@"a\Ab\Nc\Nd"), pad(), 200, corrected: true);

        Assert.Equal(new[] { 19, 28, 36, 52 }, binary.GlyphFrames()); // the stale bit 8 of the binary
        Assert.Equal(new[] { 19, 28, 36, 61 }, corrected.GlyphFrames()); // D-E19-63
        Assert.Equal(new[] { (23, 0) }, corrected.Rows.Where(r => r.Cursor != null).Select(r => (r.Frame, r.Cursor!.Value)).ToArray());
    }

    [Fact]
    public void O07_ANewLineThenA_LeavesAnEmptyLine()
    {
        foreach (var corrected in new[] { false, true })
        {
            var host = RunSimple(Text(@"a\N\Ab"), new OraclePadScheduled(new() { [28] = (true, true) }), 100, corrected: corrected);

            Assert.Equal(new[] { "a", string.Empty, "b" }, host.Rows[40].Rows);
        }
    }

    [Fact]
    public void O08_AManualMode_TypesNothingUntilA0x4D_AndALatchSetDuringTheSlideIsKept()
    {
        foreach (var corrected in new[] { false, true })
        {
            var host = RunSimple(Text("ABC"), new OraclePadEveryFrame(), 100, mode: 1, corrected: corrected,
                sameTick: new[] { Op("4C", 4), Op("4D") },
                timed: new() { [5] = new[] { Op("4D") }, [30] = new[] { Op("4D") } });

            Assert.Equal(new[] { 19, 31 }, host.GlyphFrames());
        }
    }

    [Fact]
    public void O09_TheCursor_ShowsImage0NineTimes_ThenTenFramesOfEachImage()
    {
        var host = RunSimple(Text(@"a\Ab"), new OraclePadNone(), 120);

        var images = host.Rows.Where(r => r.Cursor != null).Select(r => r.Cursor!.Value).Take(50).ToArray();
        Assert.Equal(Enumerable.Repeat(0, 9).Concat(Enumerable.Repeat(1, 10)).Concat(Enumerable.Repeat(2, 10)).Concat(Enumerable.Repeat(3, 10))
            .Concat(Enumerable.Repeat(0, 10)).Append(1).ToArray(), images);
    }

    [Fact]
    public void O10_A0x4CClearsThePending0x4D_TheBinaryKeepsItAndTypesALetterTooMany()
    {
        var sameTick = new[] { Op("4C", 4) };
        var timed = new Dictionary<int, (string Name, int[] Args)[]>
        {
            [25] = new[] { Op("4D"), Op("4C", 3) },
            [40] = new[] { Op("4C", 4) },
        };

        var binary = RunSimple(Text("ABCDEFGH"), new OraclePadNone(), 200, mode: 1, corrected: false, sameTick: sameTick, timed: timed);
        var corrected = RunSimple(Text("ABCDEFGH"), new OraclePadNone(), 200, mode: 1, corrected: true, sameTick: sameTick, timed: timed);

        Assert.Equal(new[] { 26, 30, 34, 38, 41 }, binary.GlyphFrames());
        Assert.Equal(new[] { 26, 30, 34, 38 }, corrected.GlyphFrames()); // D-E19-62
    }

    [Fact]
    public void O11_AB_UnderTheArcsPad_FromAMapEvent_AndFromAnEntityScript()
    {
        var fromEvent = RunSimple(Text("AB"), OracleRulePad.ForArcs(), 200, "M", mode: 1);
        Assert.Equal(new[] { 19, 20 }, fromEvent.GlyphFrames());
        var (_, _, e1, t1, r1) = fromEvent.Summary();
        Assert.Equal((21, 24, 42), (e1, t1, r1));

        var fromEntity = RunSimple(Text("AB"), OracleRulePad.ForArcs(), 200, "E", mode: 1);
        Assert.Equal(new[] { 18, 19 }, fromEntity.GlyphFrames());
        var (_, _, e2, t2, r2) = fromEntity.Summary();
        Assert.Equal((20, 23, 41), (e2, t2, r2));
        Assert.Equal(42, fromEntity.EventFrames("0x39 released")[0]); // the entity sees the release one tick after the pass
    }

    // ---- the synthetic boxes of the director's tests (section C and K of the annex) ---------------------------------

    [Fact]
    public void Bonjour_WithNoPad_TypesFrom19ToThe43_EndsAt47_TimerAt407_ReleasesAt425()
    {
        var host = RunSimple(Text("bonjour"), new OraclePadNone(), 600, mode: 1);

        Assert.Equal(new[] { 19, 23, 27, 31, 35, 39, 43 }, host.GlyphFrames());
        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((47, 407, 425), (e, t, r));
    }

    [Fact]
    public void Bonjour_APressSeenAt48_TriggersTheCloseAt48_ReleasesAt66()
    {
        var host = RunSimple(Text("bonjour"), new OraclePadScheduled(new() { [48] = (true, true) }), 200, mode: 1);

        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((47, 48, 66), (e, t, r));
    }

    [Fact]
    public void K1_AnEntityScriptOpeningAtTick10_GetsItsFirstPassAt10_AnEventAt11()
    {
        var entity = Production(new OraclePadNone());
        entity.Add("E", Simple(Text("bonjour"), 1), 10);
        entity.Run(600);
        Assert.Equal(28, entity.GlyphFrames()[0]);
        var (_, _, e1, t1, r1) = entity.Summary();
        Assert.Equal((56, 416, 434), (e1, t1, r1));
        Assert.Equal(435, entity.EventFrames("0x39 released")[0]); // T + 19

        var mapEvent = Production(new OraclePadNone());
        mapEvent.Add("M", Simple(Text("bonjour"), 1), 10);
        mapEvent.Run(600);
        Assert.Equal(29, mapEvent.GlyphFrames()[0]);
        var (_, _, e2, t2, r2) = mapEvent.Summary();
        Assert.Equal((57, 417, 435), (e2, t2, r2));
        Assert.Equal(435, mapEvent.EventFrames("0x39 released")[0]); // T + 18
    }

    [Fact]
    public void K2_AMenuOpenBoxClosedByAPressSeenAtETimes1_IsSeenReleasedByAMapEventAt76()
    {
        var host = Production(new OraclePadScheduled(new() { [58] = (true, true) }));
        host.Add("M", Simple(Text("bonjour"), 0), 10);
        host.Run(300);

        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((57, 58, 76), (e, t, r));
        Assert.Equal(76, host.EventFrames("0x39 released")[0]);
    }

    [Fact]
    public void K3_ATickLevelHost_AMapEventBoxOpenedAtTick0_GetsItsPressAtTick49_ReleasesAtTick67()
    {
        var host = Production(new OraclePadScheduled(new() { [49] = (true, true) }));
        host.Add("M", Simple(Text("bonjour"), 0));
        host.Run(200);

        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((47, 49, 67), (e, t, r));
        Assert.Equal(67, host.EventFrames("0x39 released")[0]);
    }

    [Fact]
    public void K4_A0x51FromAnEntityScriptActsAtK_FromAMapEventAtKPlus1()
    {
        IEnumerable<int> Script(OracleScript script)
        {
            foreach (var x in script.Open(Text("bonjour"), 1))
            {
                yield return x;
            }

            script.Op("50", 4);
            while (script.Frame < 100)
            {
                yield return 0;
            }

            script.Op("51");
            foreach (var x in script.Wait39())
            {
                yield return x;
            }
        }

        var entity = Production(new OraclePadNone());
        entity.Add("E", Script, 10);
        entity.Run(300);
        var (_, _, e1, t1, r1) = entity.Summary();
        Assert.Equal((56, 100, 118), (e1, t1, r1));
        Assert.Equal(119, entity.EventFrames("0x39")[0]);

        var mapEvent = Production(new OraclePadNone());
        mapEvent.Add("M", Script, 10);
        mapEvent.Run(300);
        var (_, _, e2, t2, r2) = mapEvent.Summary();
        Assert.Equal((57, 101, 119), (e2, t2, r2));
        Assert.Equal(119, mapEvent.EventFrames("0x39")[0]);
    }

    [Fact]
    public void K5_TheBoxKeepsTypingWhileAChoiceWaits_AndTheCloseComesWithThe0x51AfterIt()
    {
        IEnumerable<int> Script(OracleScript script)
        {
            foreach (var x in script.Open(Text("bonjour"), 1))
            {
                yield return x;
            }

            script.Op("50", 4);
            while (script.Frame < 90)
            {
                yield return 0; // the choice (0x44) waits: the box is another slot of the dispatcher and runs on
            }

            script.Op("51");
            foreach (var x in script.Wait39())
            {
                yield return x;
            }
        }

        var host = Production(new OraclePadNone());
        host.Add("E", Script, 10);
        host.Run(300);

        Assert.Equal(new[] { 28, 32, 36, 40, 44, 48, 52 }, host.GlyphFrames());
        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((56, 90, 108), (e, t, r));
        Assert.Equal(109, host.EventFrames("0x39")[0]);
    }

    [Fact]
    public void K7_AnEntityScriptOpeningAtTick10_ClosedByAPressSeenAt57_IsSeenReleasedAt76()
    {
        var host = Production(new OraclePadScheduled(new() { [57] = (true, true) }));
        host.Add("E", Simple(Text("bonjour"), 1), 10);
        host.Run(300);

        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((56, 57, 75), (e, t, r));
        Assert.Equal(76, host.EventFrames("0x39 released")[0]);
    }

    // ---- the pads --------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheArcsPad_HoldsTheButtonWhileTyping_ThenReleasesOneFrameAndPressesWhenTheBoxWaits()
    {
        var pad = OracleRulePad.ForArcs();
        var host = Production(pad);
        host.Add("M", Simple(Text(@"a\Ab"), 1));
        host.Run(120);

        // Square is held while the letter is typed (one letter a pass), so the \A cursor shows at the pass a = 20. The helper sees it at the start of
        // frame 21 (Square held at 20, released at 21), presses at 22; the box reads that press two frames later: the \A is released at a + 3 = 23.
        Assert.Equal(new[] { 20 }, AtFrames(host, "A-wait"));
        Assert.Equal(new[] { 23 }, AtFrames(host, "A-release"));
    }

    // ---- O-20 .. O-22: the three real texts ---------------------------------------------------------------------------------

    private static string ProjectRoot => SaveGameDirectorTestSupport.FindProjectRoot();

    private static readonly Dictionary<int, DialogueAsset> MapAssets = new();

    private static byte[] NodeBytes(string node)
    {
        var mapId = int.Parse(node.Substring(1, node.IndexOf('_') - 1));
        lock (MapAssets)
        {
            if (!MapAssets.TryGetValue(mapId, out var asset))
            {
                var file = Directory.GetFiles(Path.Combine(ProjectRoot, "Maps"), $"*-{mapId}.dialogue", SearchOption.AllDirectories)
                    .Single(p => Path.GetFileNameWithoutExtension(p).EndsWith("-" + mapId, StringComparison.Ordinal));
                asset = DialogueTestAssets.LoadFromDisk(file);
                MapAssets[mapId] = asset;
            }

            return OraclePages.NodeBytes(asset, node, out _);
        }
    }

    private static IEnumerable<int> Sailor12(OracleScript script)
    {
        // Map 389, C[12] from @1390: 0x0D [0x81,1]; 0x50 4; 0x36 T999; 0x44 (resolved a tick after); 0x51; 0x0D tried again; 0x39.
        foreach (var x in script.Open(NodeBytes("M389_S001"), 1, "open S001"))
        {
            yield return x;
        }

        script.Op("50", 4);
        foreach (var x in script.Wait36(999))
        {
            yield return x;
        }

        script.Note("0x44 first entry");
        foreach (var x in script.Hold(1))
        {
            yield return x;
        }

        script.Note("0x44 resolved");
        script.Op("51");
        foreach (var x in script.Open(NodeBytes("M389_S002"), 1, "open S002"))
        {
            yield return x;
        }

        foreach (var x in script.Wait39())
        {
            yield return x;
        }
    }

    /// <summary>The frames of the box opened at <paramref name="from"/> (the next opening, or the end, bounds it).</summary>
    private static (int? First, int? Last, int? E, int? T, int? R) Segment(OracleHost host, int from, int to)
    {
        var rows = host.Rows.Where(r => r.Frame > from && r.Frame <= to).ToArray();
        var glyphs = rows.SelectMany(r => Enumerable.Repeat(r.Frame, r.Drawn)).ToArray();
        int? Of(string note) => rows.Where(r => r.Notes.Contains(note)).Select(r => (int?)r.Frame).FirstOrDefault();
        return (glyphs.Length > 0 ? glyphs[0] : null, glyphs.Length > 0 ? glyphs[^1] : null, Of("typing-done"), Of("close-trigger"), Of("DialogClosed"));
    }

    private static int? FlagFrame(OracleHost host, int flag, int from, int to) =>
        host.FlagFrames().Where(f => f.Flag == flag && f.Frame > from && f.Frame <= to).Select(f => (int?)f.Frame).FirstOrDefault();

    [Fact]
    public void O20_Sailor12Of389_UnderTheBinarysPad_FirstBoxFrom19To93_T999At94_ClosesAt96_SecondBoxOpensAt114()
    {
        var host = Production(new OraclePadEveryFrame(), corrected: false);
        host.Add("M", Sailor12);
        host.Run(1200);

        var second = host.EventFrames("open S002")[0];
        Assert.Equal(114, second);
        var s1 = Segment(host, 0, second);
        Assert.Equal((19, 93, 95, 96), (s1.First, s1.Last, s1.E, s1.T));
        Assert.Equal(94, FlagFrame(host, 999, 0, second));
    }

    [Fact]
    public void O20_Sailor12Of389_CorrectedUnderTheArcsPad_TheSecondBoxMovesAndTheScrollsCome141And166After()
    {
        var host = Production(OracleRulePad.ForArcs());
        host.Add("M", Sailor12);
        host.Run(1200);

        var second = host.EventFrames("open S002")[0];
        Assert.Equal(114, second);
        var s1 = Segment(host, 0, second);
        Assert.Equal((19, 93, 95, 96), (s1.First, s1.Last, s1.E, s1.T));

        var s2 = Segment(host, second, int.MaxValue);
        Assert.Equal((second + 19, second + 184, second + 187, second + 205), (s2.First, s2.E, s2.T, s2.R));
        Assert.Equal(new[] { second + 106 }, host.FramesWith("A-wait"));
        Assert.Equal(new[] { second + 109 }, host.FramesWith("A-release"));
        Assert.Equal(new[] { second + 141, second + 166 }, host.FramesWith("scroll-armed"));
        Assert.Equal(319, host.EventFrames("0x39")[0]);
    }

    [Fact]
    public void O20_Sailor12Of389_InTheOrderOfTheDll_AnEntityScriptHasItsFirstPassAtN_AndSeesTheReleaseAtRPlus1()
    {
        var host = Production(OracleRulePad.ForArcs());
        host.Add("E", Sailor12);
        host.Run(1200);

        var second = host.EventFrames("open S002")[0];
        Assert.Equal(114, second);
        var s1 = Segment(host, 0, second);
        Assert.Equal((18, 94, 95, 113), (s1.First, s1.E, s1.T, s1.R));
        Assert.Equal(93, FlagFrame(host, 999, 0, second));

        var s2 = Segment(host, second, int.MaxValue);
        Assert.Equal((second + 18, second + 183, second + 186, second + 204), (s2.First, s2.E, s2.T, s2.R));
        Assert.Equal(new[] { second + 105 }, host.FramesWith("A-wait"));
        Assert.Equal(new[] { second + 108 }, host.FramesWith("A-release"));
        Assert.Equal(319, host.EventFrames("0x39")[0]);
    }

    private static IEnumerable<int> Ship391Sub(OracleScript script, int k)
    {
        script.Op("4C", 2);
        script.Op("50", 4);
        while (!script.Box.TemporaryFlags.Contains(999))
        {
            yield return 0;
        }

        script.Note($"T999 seen {k}");
        foreach (var x in script.Wait37(60))
        {
            yield return x;
        }

        script.Op("51");
        foreach (var x in script.Wait39($"0x39 {k}"))
        {
            yield return x;
        }

        script.Box.TemporaryFlags.Remove(999);
        script.Box.TemporaryFlags.Remove(1000);
        foreach (var x in script.Wait37(30))
        {
            yield return x;
        }
    }

    private static IEnumerable<int> Ship391(OracleScript script)
    {
        // Map 391, B[1]: the sub-program @706 closes each box (0x4C 2, 0x50 4, wait for T999, 0x37 60, 0x51, 0x39).
        foreach (var x in script.Open(NodeBytes("M391_S019"), 1, "open S019")) { yield return x; }
        foreach (var x in Ship391Sub(script, 1)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        foreach (var x in script.Open(NodeBytes("M391_S020"), 1, "open S020")) { yield return x; }
        foreach (var x in Ship391Sub(script, 2)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        foreach (var x in script.Wait37(30)) { yield return x; }
        foreach (var x in script.Wait37(15)) { yield return x; }
        foreach (var x in script.Wait37(30)) { yield return x; }
        foreach (var x in script.Open(NodeBytes("M391_S022"), 1, "open S022")) { yield return x; }
        foreach (var x in Ship391Sub(script, 3)) { yield return x; }
    }

    [Fact]
    public void O21_ShipOf391_TheThreeBoxesCloseByTheirScript_WhateverThePad()
    {
        foreach (var pad in new Func<IOraclePad>[] { () => OracleRulePad.ForArcs(), () => new OraclePadEveryFrame() })
        {
            var host = Production(pad());
            host.Add("M", Ship391);
            host.Run(4000);

            var o1 = host.EventFrames("open S019")[0];
            var o2 = host.EventFrames("open S020")[0];
            var o3 = host.EventFrames("open S022")[0];
            Assert.Equal((0, 463, 1152), (o1, o2 - o1, o3 - o1));

            var s1 = Segment(host, o1, o2);
            Assert.Equal(351, FlagFrame(host, 999, o1, o2) - o1);
            Assert.Equal((355, 413, 431), (s1.E - o1, s1.T - o1, s1.R - o1));
            Assert.Equal(431, host.EventFrames("0x39 1")[0] - o1);

            var s2 = Segment(host, o2, o3);
            Assert.Equal(499, FlagFrame(host, 999, o2, o3) - o2);
            Assert.Equal((503, 561, 579), (s2.E - o2, s2.T - o2, s2.R - o2));

            var s3 = Segment(host, o3, int.MaxValue);
            Assert.Equal(87, FlagFrame(host, 1000, o3, int.MaxValue) - o3);
            Assert.Equal(407, FlagFrame(host, 999, o3, int.MaxValue) - o3);
            Assert.Equal((411, 469, 487), (s3.E - o3, s3.T - o3, s3.R - o3));
        }
    }

    private static IEnumerable<int> Septimus164(OracleScript script)
    {
        // Map 164, C[2] from @325 (the physics waits at zero, like the model): 0x0D; 0x36 T200; 0x4C 4; 0x4D paced by 0x37; 0x4C 3; 0x39.
        foreach (var x in script.Open(NodeBytes("M164_S003"), 1, "open S003")) { yield return x; }
        foreach (var x in script.Wait36(200)) { yield return x; }
        script.Op("4C", 4);
        foreach (var x in script.Wait37(5)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        script.Op("4D");
        script.Op("4C", 3);
        foreach (var x in script.Wait36(201)) { yield return x; }
        script.Op("4C", 4);
        foreach (var x in script.Wait37(15)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        script.Op("4D");
        foreach (var x in script.Wait37(2)) { yield return x; }
        foreach (var x in script.Wait37(5)) { yield return x; }
        foreach (var x in script.Wait37(5)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        for (var i = 0; i < 4; i++)
        {
            script.Op("4D");
            foreach (var x in script.Wait37(2)) { yield return x; }
        }

        foreach (var x in script.Break()) { yield return x; }
        script.Op("4D");
        foreach (var x in script.Wait37(2)) { yield return x; }
        script.Op("4D");
        foreach (var x in script.Wait37(2)) { yield return x; }
        script.Op("4D");
        foreach (var x in script.Wait37(30)) { yield return x; }
        foreach (var x in script.Break()) { yield return x; }
        script.Op("4C", 3);
        foreach (var x in script.Wait39()) { yield return x; }
    }

    [Fact]
    public void O22_SeptimusOf164_UnderTheBinarysPad_T200At29_ACursorAt63ReleasedAt64_EndsAt172_ReleasesAt191()
    {
        var host = Production(new OraclePadEveryFrame(), corrected: false);
        host.Add("M", Septimus164);
        host.Run(800);

        Assert.Equal(29, FlagFrame(host, 200, 0, 999));
        Assert.Equal(new[] { 63 }, host.FramesWith("A-wait"));
        Assert.Equal(new[] { 64 }, host.FramesWith("A-release"));
        Assert.Equal(65, FlagFrame(host, 201, 0, 999));
        var (_, _, e, t, r) = host.Summary();
        Assert.Equal((172, 173, 191), (e, t, r));
    }

    [Fact]
    public void O22_SeptimusOf164_CorrectedUnderTheArcsPad_TheBinarysOrder_AndTheDllsOrderForAnEntityScript()
    {
        var binaryOrder = Production(OracleRulePad.ForArcs());
        binaryOrder.Add("M", Septimus164);
        binaryOrder.Run(800);

        Assert.Equal(29, FlagFrame(binaryOrder, 200, 0, 999));
        Assert.Equal(new[] { 63 }, binaryOrder.FramesWith("A-wait"));
        Assert.Equal(new[] { 66 }, binaryOrder.FramesWith("A-release"));
        Assert.Equal(68, FlagFrame(binaryOrder, 201, 0, 999));
        var (_, _, e1, t1, r1) = binaryOrder.Summary();
        Assert.Equal((176, 179, 197), (e1, t1, r1));

        var dllOrder = Production(OracleRulePad.ForArcs());
        dllOrder.Add("E", Septimus164);
        dllOrder.Run(800);

        Assert.Equal(18, dllOrder.GlyphFrames()[0]);
        Assert.Equal(28, FlagFrame(dllOrder, 200, 0, 999));
        Assert.Equal(new[] { 62 }, dllOrder.FramesWith("A-wait"));
        Assert.Equal(new[] { 65 }, dllOrder.FramesWith("A-release"));
        Assert.Equal(67, FlagFrame(dllOrder, 201, 0, 999));
        var (_, _, e2, t2, r2) = dllOrder.Summary();
        Assert.Equal((175, 178, 196), (e2, t2, r2));
        Assert.Equal(197, dllOrder.EventFrames("0x39")[0]);
    }

    [Fact]
    public void ThePageBytes_OfAMarkedPage_AreTheOriginalsCodes()
    {
        var bytes = OraclePages.PageBytes("Salut[flag id=10 trimwhitespace=false/][yield/] ami[br/][voice id=1/][glyph id=3/][slow/]x[center/]");

        Assert.Equal(@"Salut\10\Y ami\N\D\W#\Tx\H", System.Text.Encoding.Latin1.GetString(bytes));
    }
}
