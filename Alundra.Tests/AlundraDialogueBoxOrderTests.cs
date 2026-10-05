#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Assets;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f2a F2A-3 (docs/plan-e19-opcodes.md section 1.2j.3, F2-R1, F2-R2, F2-R6): the order of a logic tick at the proxy's own <see cref="AlundraWorldProxy.Update"/>,
/// seen by the box. Every number is a value of docs/plan-e19-f2a-valeurs.md section K, written by the oracle before the director ran. N is the frame of the opening; a
/// <b>map event</b> script runs after the pass of the box of its frame (so its box is first updated at N + 1 and released, for it, at T + 18), an <b>entity</b> script
/// before it (first update at N, release seen at T + 19: the engine updates the entities before the world's proxy - the named gap of D-E19-64, no dating).
/// </summary>
public sealed class AlundraDialogueBoxOrderTests
{
    private const uint Square = AlundraPadState.Square;

    private static DialogueAsset Bonjour(string name = "Bonjour") => DialogueTestAssets.SinglePage(name, "bonjour");

    /// <summary>What a test reads of a box, frame by frame: first glyph, end of the typing (E), trigger of the close (T), release (R).</summary>
    private sealed class Timeline
    {
        public int? FirstGlyph;
        public int? E;
        public int? T;
        public int? R;
        private bool _opened;

        public void Take(DialogueBoxMontage montage)
        {
            var frame = montage.Frame - 1;
            var director = montage.Director;
            _opened |= director.IsOpen;
            FirstGlyph ??= director.Box.GlyphCount > 0 ? frame : null;
            E ??= director.IsOpen && director.Box.IsTypingDone ? frame : null;
            T ??= director.IsOpen && director.Box.IsClosing ? frame : null;
            R ??= _opened && !director.IsOpen ? frame : null;
        }
    }

    private static Timeline Run(DialogueBoxMontage montage, int frames)
    {
        var timeline = new Timeline();
        for (var i = 0; i < frames; i++)
        {
            montage.RunFrame();
            timeline.Take(montage);
        }

        return timeline;
    }

    // ---- K-1: the origin of an opening -----------------------------------------------------------------------------------

    [Fact]
    public void K1_AnEntityScriptOpeningAtTick10_HasItsFirstPassAt10_ItsFirstGlyphAt28_AndSeesTheReleaseAtTPlus19()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var seen = -1;
        var opened = false;
        montage.EntityScript = frame =>
        {
            if (frame == 10)
            {
                montage.Director.Open(asset, "Start", 1);
                opened = true;
            }
            else if (opened && seen < 0 && !montage.Director.IsOpen)
            {
                seen = frame; // the 0x39 of the entity script
            }
        };

        var timeline = Run(montage, 500);

        Assert.Equal(28, timeline.FirstGlyph); // N + 18: the first pass is at N
        Assert.Equal((56, 416, 434), (timeline.E, timeline.T, timeline.R));
        Assert.Equal(435, seen); // T + 19: an entity script sees the release a tick late
    }

    [Fact]
    public void K1_AMapEventOpeningAtTick10_HasItsFirstPassAt11_ItsFirstGlyphAt29_AndSeesTheReleaseAtTPlus18()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var seen = -1;
        var opened = false;
        montage.MapEventScript = () =>
        {
            if (!opened && montage.Frame >= 10)
            {
                montage.Director.Open(asset, "Start", 1);
                opened = true;
            }
            else if (opened && seen < 0 && !montage.Director.IsOpen)
            {
                seen = montage.Frame; // the 0x39 of the map event
            }
        };

        var timeline = Run(montage, 500);

        Assert.Equal(29, timeline.FirstGlyph); // N + 19: the first pass is at N + 1
        Assert.Equal((57, 417, 435), (timeline.E, timeline.T, timeline.R));
        Assert.Equal(435, seen); // T + 18: the gate is read again after the pass, in the same tick
    }

    [Fact]
    public void TheSoundsOfTheBox_AreThe6AtTheOpening_The7AtTheTrigger_AndTheVoicesOfTheCharacters()
    {
        using var montage = new DialogueBoxMontage();
        var voiced = DialogueTestAssets.BuildRaw("Voiced", "Start", "[voice id=1 trimwhitespace=false/]abcd");
        var opened = false;
        montage.MapEventScript = () =>
        {
            if (!opened)
            {
                montage.Director.Open(voiced, "Start", 1);
                opened = true;
            }
        };

        Run(montage, 500);

        // 6 at the opening (tick 0, a map event: after the pass), 79 + 1 = 80 on the letters of an even rank (the first and the third of "abcd": ticks 19 and 27;
        // the tick of a pass is the number of passes done when the sound is asked, minus 1), 7 once, at the trigger of the timer (E 35, T 395).
        Assert.Equal(
            new[] { (0, 6), (19, 80), (27, 80) },
            montage.Sounds.Played.Where(p => p.Sfx != 7).Select(p => (p.Pass - 1, p.Sfx)).ToArray());
        Assert.Equal(395, montage.Sounds.Played.Single(p => p.Sfx == 7).Pass - 1);
    }

    // ---- K-2: a MenuOpen box released, seen by a map event at T + 18 -----------------------------------------------------

    [Fact]
    public void K2_AMenuOpenBoxClosedByAPressSeenAt58_IsSeenReleasedByTheMapEventAt76_ThroughTheGateReadAgainAfterThePass()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var opened = false;
        var mapEventCalls = new List<int>();
        var seen = -1;
        montage.MapEventScript = () =>
        {
            mapEventCalls.Add(montage.Frame);
            if (!opened && montage.Frame >= 10)
            {
                montage.Director.Open(asset, "Start", 0);
                opened = true;
            }
            else if (opened && seen < 0 && !montage.Director.IsOpen)
            {
                seen = montage.Frame;
            }
        };

        // The square held for the tick 57 is read by the pass of the tick 58 (E = 57: the typing ends at N + 47).
        montage.PadForFrame = frame => frame == 57 ? Square : 0u;
        var timeline = Run(montage, 200);

        Assert.Equal((57, 58, 76), (timeline.E, timeline.T, timeline.R));
        Assert.Equal(76, seen);
        Assert.Equal(10, mapEventCalls.Max(c => c < 76 ? c : -1)); // frozen from the tick after the opening to the release
        Assert.Contains(76, mapEventCalls);
    }

    // ---- K-3: a frame of several ticks interleaves the box and the map events -------------------------------------------

    [Fact]
    public void K3_OnACatchUpFrame_TheBoxPassAndTheMapEventsInterleaveTickByTick()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var opened = false;
        var passesSeenByTheMapEvents = new List<int>();
        montage.MapEventScript = () =>
        {
            passesSeenByTheMapEvents.Add(montage.Director.PassCountForTests);
            if (!opened)
            {
                montage.Director.Open(asset, "Start", 1);
                opened = true;
            }
        };

        montage.RunFrames(30, 0.06f); // three ticks a frame

        // B0 M0 B1 M1 B2 M2 ...: the map event of the tick t sees t + 1 passes done - never the passes of the whole frame (B0 B1 B2 M0 M1 M2) nor none (M0 B0).
        Assert.Equal(Enumerable.Range(1, 90).ToArray(), passesSeenByTheMapEvents.ToArray());
    }

    [Fact]
    public void K3_AMenuOpenBoxOpenedAtTick0_ReleasedAtTick67_ThawsTheMapEventsInTheMiddleOfTheCatchUpFrame22_AndTheEntitiesAtFrame23()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var opened = false;
        var mapEventTicks = new List<int>();
        var mapEventSeen = -1;
        var entitySeen = -1;
        montage.MapEventScript = () =>
        {
            var tick = montage.Director.PassCountForTests - 1;
            mapEventTicks.Add(tick);
            if (!opened)
            {
                montage.Director.Open(asset, "Start", 0);
                opened = true;
            }
            else if (mapEventSeen < 0 && !montage.Director.IsOpen)
            {
                mapEventSeen = tick; // the 0x39 of the map event
            }
        };
        montage.EntityScript = frame =>
        {
            if (opened && entitySeen < 0 && !montage.Director.IsOpen)
            {
                entitySeen = frame;
            }
        };

        // The first pass is the tick 1 (the opening is the tick 0 of a map event); E at the tick 47; the square held for the frame 16 (ticks 48 to 50, the front at 48)
        // is read by the pass of the tick 49: T 49, the release at the tick 67 - the frame 22, its second tick.
        montage.PadForFrame = frame => frame == 16 ? Square : 0u;
        montage.RunFrames(30, 0.06f);

        var close = montage.Sounds.Played.Single(p => p.Sfx == 7);
        Assert.Equal(49, close.Pass - 1);
        Assert.Equal(new[] { 0, 67, 68 }, mapEventTicks.Take(3).ToArray()); // frozen from the tick 1 to the tick 66, thawed at j = 1 of the frame 22
        Assert.Equal(67, mapEventSeen);
        Assert.Equal(23, entitySeen); // the entities of the frame 22 ran before its ticks
    }

    // ---- K-4: a 0x51 written by a script acts at its own tick (entity) or the next one (map event) -------------------------

    [Fact]
    public void K4_A0x51FromAnEntityScriptAtTick100_ActsAt100_FromAMapEvent_At101()
    {
        // From an entity script: E 56, T = k = 100, R 118, the 0x39 of the entity at 119.
        using (var montage = new DialogueBoxMontage())
        {
            var asset = Bonjour();
            var seen = -1;
            var opened = false;
            montage.EntityScript = frame =>
            {
                if (frame == 10)
                {
                    montage.Director.Open(asset, "Start", 1);
                    montage.Director.SetCloseMask(4);
                    opened = true;
                }
                else if (frame == 100)
                {
                    Assert.True(montage.Director.RequestScriptClose());
                }
                else if (opened && frame > 100 && seen < 0 && !montage.Director.IsOpen)
                {
                    seen = frame;
                }
            };

            var timeline = Run(montage, 300);

            Assert.Equal((56, 100, 118), (timeline.E, timeline.T, timeline.R));
            Assert.Equal(119, seen);
        }

        // From a map event: E 57, T = k + 1 = 101, R 119, the 0x39 of the map event at 119.
        using (var montage = new DialogueBoxMontage())
        {
            var asset = Bonjour();
            var seen = -1;
            var opened = false;
            montage.MapEventScript = () =>
            {
                if (!opened && montage.Frame >= 10)
                {
                    montage.Director.Open(asset, "Start", 1);
                    montage.Director.SetCloseMask(4);
                    opened = true;
                }
                else if (opened && montage.Frame == 100)
                {
                    Assert.True(montage.Director.RequestScriptClose());
                }
                else if (opened && montage.Frame > 100 && seen < 0 && !montage.Director.IsOpen)
                {
                    seen = montage.Frame;
                }
            };

            var timeline = Run(montage, 300);

            Assert.Equal((57, 101, 119), (timeline.E, timeline.T, timeline.R));
            Assert.Equal(119, seen);
        }
    }

    // ---- K-5: the box keeps typing while a choice waits ------------------------------------------------------------------

    [Fact]
    public void K5_TheBoxFinishesItsTypingWhileAChoiceWaits_TheChoiceListStaysTheEngines_AndTheCloseComesWithThe0x51AfterTheAnswer()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var glyphFrames = new List<int>();
        var seen = -1;
        var opened = false;
        montage.EntityScript = frame =>
        {
            if (frame == 10)
            {
                montage.Director.Open(asset, "Start", 1);
                montage.Director.SetCloseMask(4);
                opened = true;
            }
            else if (frame == 30)
            {
                montage.Director.OpenChoice(new[] { "OUI", "NON" }); // 0x44 asks, the script waits
            }
            else if (frame == 90)
            {
                Assert.True(montage.Director.SelectChoiceForTests(0));
                Assert.Equal(1, montage.Director.TakeChoiceResult()); // the result of 0x44 ...
                Assert.True(montage.Director.RequestScriptClose()); // ... and the 0x51 that follows it, in the same tick
            }
            else if (opened && frame > 90 && seen < 0 && !montage.Director.IsOpen)
            {
                seen = frame;
            }
        };

        var timeline = new Timeline();
        var lastGlyphs = 0;
        for (var i = 0; i < 300; i++)
        {
            montage.RunFrame();
            timeline.Take(montage);
            if (montage.Director.Box.GlyphCount != lastGlyphs)
            {
                glyphFrames.Add(montage.Frame - 1);
                lastGlyphs = montage.Director.Box.GlyphCount;
            }

            if (montage.Frame == 60)
            {
                Assert.Equal(new[] { "OUI", "NON" }, montage.Director.ChoicesForTests); // the typing did not wipe the engine's list
                Assert.True(montage.Director.IsAwaitingChoice);
            }
        }

        Assert.Equal(new[] { 28, 32, 36, 40, 44, 48, 52 }, glyphFrames);
        Assert.Equal((56, 90, 108), (timeline.E, timeline.T, timeline.R));
        Assert.Equal(109, seen);
        Assert.Equal("bonjour", montage.Presenter.Shown.Last(s => s.Text != string.Empty).Text); // the text typed during the choice is sent after the answer
    }

    // ---- K-6: the presenter receives the text typed so far ----------------------------------------------------------------

    [Fact]
    public void K6_ThePresenterReceivesTheTypedPrefix_ThatGrowsAtEachStepThatChangesTheVisibleText_AndTheDirectorHoldsTheWholePage()
    {
        using var montage = new DialogueBoxMontage();
        var asset = DialogueTestAssets.BuildRaw("Prefix", "Start", "ab[br trimwhitespace=false/]cd");

        montage.Director.Open(asset, "Start", 1);
        var passes = new DialogueBoxPassDriver(montage.Director, montage.State);
        passes.RunTo(20);
        Assert.Equal("ab\ncd", montage.Director.CurrentLineForTests?.Text); // the whole page, during the typing

        passes.RunTo(60);

        // The opening shows the empty prefix (the box appears); then the passes 19 (a), 23 (ab), 31 (ab / c), 35 (ab / cd); the step of the line break (27) sends nothing.
        Assert.Equal(new[] { (0, string.Empty), (19, "a"), (23, "ab"), (31, "ab\nc"), (35, "ab\ncd") }, montage.Presenter.Shown.ToArray());
        Assert.Equal("ab\ncd", montage.Director.CurrentLineForTests?.Text);
    }

    // ---- K-7 --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void K7_AnEntityScriptOpeningAtTick10_ClosedByAPressSeenAt57_IsReleasedAt75_AndSeesItAt76()
    {
        using var montage = new DialogueBoxMontage();
        var asset = Bonjour();
        var seen = -1;
        var opened = false;
        montage.EntityScript = frame =>
        {
            if (frame == 10)
            {
                montage.Director.Open(asset, "Start", 1);
                opened = true;
            }
            else if (opened && seen < 0 && !montage.Director.IsOpen)
            {
                seen = frame;
            }
        };
        montage.PadForFrame = frame => frame == 56 ? Square : 0u; // held for the tick 56: read by the pass of the tick 57 (E = 56)

        var timeline = Run(montage, 200);

        Assert.Equal((56, 57, 75), (timeline.E, timeline.T, timeline.R));
        Assert.Equal(76, seen);
    }
}
