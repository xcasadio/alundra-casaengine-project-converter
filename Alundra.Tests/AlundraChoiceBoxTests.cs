#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f3a F3A-1: the machine of the choice box (<see cref="AlundraChoiceBox"/>), the port of <c>ChoiceBox</c> of <c>docs/plan-e19-f3-annexe/model/choice_model.py</c>
/// (slot 3 of the binary's callback table, opener 0x80050BA8), against the values V1 to V7 of <c>docs/plan-e19-f3-annexe/values.json</c> (the model run on
/// the pad words listed there): per pass, the update function, whether it drew, the sounds, the close, the x of the frame, of the labels and of the cursor, the cursor
/// image, the selection, then the pass at which the result word is written and its value.
/// </summary>
public sealed class AlundraChoiceBoxMachineTests
{
    private static string ValuesPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f3-annexe", "values.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("no docs/plan-e19-f3-annexe/values.json found above " + AppContext.BaseDirectory);
    }

    private static JsonElement[] Scenarios()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ValuesPath()));
        return document.RootElement.GetProperty("bare").EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    public static IEnumerable<object[]> ScenarioIndexes() => Enumerable.Range(0, 7).Select(i => new object[] { i });

    private static string PhaseName(AlundraChoiceBox.Stage stage) => stage switch
    {
        AlundraChoiceBox.Stage.Init => "init",
        AlundraChoiceBox.Stage.SlideIn => "in",
        AlundraChoiceBox.Stage.Active => "active",
        AlundraChoiceBox.Stage.SlideOut => "out",
        _ => "closed",
    };

    [Theory]
    [MemberData(nameof(ScenarioIndexes))]
    public void TheMachine_EqualsTheModel_OnV1ToV7_PassByPass(int index)
    {
        var scenario = Scenarios()[index];
        var name = scenario.GetProperty("name").GetString()!;
        Assert.StartsWith("V" + (index + 1), name, StringComparison.Ordinal);

        var raw = new Dictionary<int, uint>();
        foreach (var entry in scenario.GetProperty("pad").EnumerateObject())
        {
            raw[int.Parse(entry.Name.Substring(2), CultureInfo.InvariantCulture)] = Convert.ToUInt32(entry.Value.GetString(), 16);
        }

        var table = scenario.GetProperty("table").EnumerateArray().ToArray();
        var box = new AlundraChoiceBox();
        box.SeedCursorTickForTests(scenario.GetProperty("anim0").GetInt32());
        var pad = new AlundraTickPad();
        var closedAt = -1;

        for (var t = 0; t < table.Length; t++)
        {
            var row = table[t];
            pad.Update(raw.GetValueOrDefault(t));
            var where = $"{name}, pass N+{t}";

            if (t == 0)
            {
                // The script tick of the opener: sound 4, nothing else.
                var openSound = box.Open("OUI", "NON", scenario.GetProperty("default_sel").GetInt32());
                Assert.Equal(row.GetProperty("sfx").EnumerateArray().Select(e => e.GetInt32()).ToArray(), new[] { openSound });
                continue;
            }

            var phase = PhaseName(box.CurrentStage);
            box.Pass(pad.ButtonsJustPressed, pad.ButtonsJustPressedByInterval);

            Assert.True(row.GetProperty("update").GetString() == phase, $"{where}: update function");
            Assert.True(row.GetProperty("drawn").GetBoolean() == (box.Drawn != null), $"{where}: drawn");
            Assert.True(row.GetProperty("closed").GetBoolean() == box.ClosedThisPass, $"{where}: closed");
            Assert.Equal(row.GetProperty("sfx").EnumerateArray().Select(e => e.GetInt32()).ToArray(), box.SoundsOfLastPass.ToArray());

            if (box.Drawn is { } drawn)
            {
                Assert.True(row.GetProperty("frame_x").GetInt32() == drawn.FrameX, $"{where}: frame x");
                Assert.True(AlundraChoiceBox.FrameY == drawn.FrameY, $"{where}: frame y");
                Assert.True(row.GetProperty("label0_x").GetInt32() == drawn.Label0X, $"{where}: label 0 x");
                Assert.True(row.GetProperty("label1_x").GetInt32() == drawn.Label1X, $"{where}: label 1 x");
                Assert.True(row.GetProperty("cursor_x").GetInt32() == drawn.CursorX, $"{where}: cursor x");
                Assert.True(row.GetProperty("cursor_img").GetInt32() == drawn.CursorImage, $"{where}: cursor image");
                Assert.True(row.GetProperty("sel").GetInt32() == drawn.Selection, $"{where}: selection");
            }

            if (box.ClosedThisPass)
            {
                closedAt = t;
            }
            else
            {
                Assert.Equal(0, box.ResultWord);
            }
        }

        Assert.Equal(scenario.GetProperty("summary").GetProperty("closed").GetInt32(), closedAt);
        var result = scenario.GetProperty("result").GetInt32(); // the Result of 0x44: 1 for the first option, 0 for the second
        Assert.Equal(result == 1 ? 1 : 2, box.ResultWord); // the word the box writes: 1 first option, 2 second
        Assert.False(box.IsActive);
    }

    [Fact]
    public void TheLabelsAreCutAtSixCharacters_AndTheCursorSitsOnTheSelectedLabel()
    {
        var box = new AlundraChoiceBox();
        box.Open("ABCDEFGH", "IJKLMNOP");
        Assert.Equal(new[] { "ABCDEF", "IJKLMN" }, box.Labels);

        box.Pass(0, 0); // the init pass
        box.Pass(0, 0); // the first drawn pass: x 311
        Assert.Equal(311 + 16 + 4 * 6 - 8, box.Drawn!.CursorX); // x + 16 + 48 * selection + 4 * the length of the selected label - 8
    }

    [Fact]
    public void TheOpener_DoesNotResetTheCursorCounter_AndTheCounterWrapsAt40()
    {
        var box = new AlundraChoiceBox();
        box.SeedCursorTickForTests(38);
        box.Open("OUI", "NON");
        box.Pass(0, 0); // init: nothing drawn, the counter is not touched
        Assert.Equal(38, box.CursorTick);
        box.Pass(0, 0);
        Assert.Equal(39, box.CursorTick);
        Assert.Equal(3, box.Drawn!.CursorImage);
        box.Pass(0, 0);
        Assert.Equal(0, box.CursorTick); // 40 wraps to 0
        Assert.Equal(0, box.Drawn!.CursorImage);

        box.Open("OUI", "NON"); // a second box: the counter goes on
        Assert.Equal(0, box.CursorTick);
    }

    [Fact]
    public void ACrossSeenBeforeTheFirstInteractivePass_IsLost()
    {
        var box = new AlundraChoiceBox();
        box.Open("OUI", "NON");
        for (var t = 1; t <= 18; t++)
        {
            box.Pass(AlundraPadState.Cross, 0); // N+1 to N+18: the init pass and the slide-in do not read the pad
            Assert.False(box.ClosedThisPass);
        }

        Assert.Equal(AlundraChoiceBox.Stage.Active, box.CurrentStage);
        for (var t = 19; t <= 60; t++)
        {
            box.Pass(0, 0);
        }

        Assert.Equal(AlundraChoiceBox.Stage.Active, box.CurrentStage); // still waiting for an answer
        Assert.Equal(0, box.ResultWord);
    }
}

/// <summary>
/// E19.f3a F3A-1: the choice box at the director (<see cref="AlundraDialogueDirector"/>): the opener, the hook of the tests, the answer 37 (OUI) or 38 (NON) passes
/// after the opener, the cancels, and the real pad through the world proxy (<see cref="DialogueBoxMontage"/>, five sequences).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraChoiceBoxDirectorTests : IDisposable
{
    private readonly AlundraGameState _state = new();
    private readonly PassStampedSoundPlayer _sounds;

    public AlundraChoiceBoxDirectorTests()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        _sounds = new PassStampedSoundPlayer(() => 0, () => Director.PassCountForTests);
        Director.AttachToWorld(new DialogueService(), _state, _sounds);
        Director.InstallForMapEntry();
    }

    public void Dispose() => AlundraDialogueDirector.Instance.ResetForTests();

    private static AlundraDialogueDirector Director => AlundraDialogueDirector.Instance;

    private static readonly string[] OuiNon = { "OUI", "NON" };

    private List<(int Pass, int Sfx)> Played => _sounds.Played.Select(p => (p.Pass, p.Sfx)).ToList();

    /// <summary>Ticks the director until the result is taken; returns the number of ticks (the one that took it included), or -1.</summary>
    private static int TicksUntilTaken(out int? result, int max = 200)
    {
        result = null;
        for (var n = 1; n <= max; n++)
        {
            Director.Tick();
            result = Director.TakeChoiceResult();
            if (result != null)
            {
                return n;
            }
        }

        return -1;
    }

    [Fact]
    public void Oui_IsTakenAtThe37thPassAfterTheOpener_WithTheSoundsOfTheBinary()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.IsAwaitingChoice);
        Assert.Equal(OuiNon, Director.ChoicesForTests);
        Assert.Equal(new[] { (0, 4) }, Played); // sound 4 at the tick of the opener

        Assert.True(Director.SelectChoiceForTests(0));
        for (var n = 1; n <= 36; n++)
        {
            Director.Tick();
            Assert.Null(Director.TakeChoiceResult());
            Assert.True(Director.IsAwaitingChoice);
            Assert.Equal(OuiNon, Director.ChoicesForTests); // the list lives from the opener to the close pass
        }

        Director.Tick(); // the 37th pass: the box closes and writes the result
        Assert.Empty(Director.ChoicesForTests);
        Assert.True(Director.IsAwaitingChoice); // until its consumer reads it
        Assert.Equal(1, Director.TakeChoiceResult());
        Assert.False(Director.IsAwaitingChoice);
        Assert.Null(Director.TakeChoiceResult());
        Assert.Equal(new[] { (0, 4), (19, 5), (19, 2) }, Played);
    }

    [Fact]
    public void Non_IsTakenAtThe38thPass_RightThenCross()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(1));

        Assert.Equal(38, TicksUntilTaken(out var result));
        Assert.Equal(0, result);
        Assert.Equal(new[] { (0, 4), (19, 1), (20, 5), (20, 3) }, Played);
    }

    [Fact]
    public void TheHook_IsFalseWithoutAChoice_TrueWhileItWaits_AndIdempotent()
    {
        Assert.False(Director.SelectChoiceForTests(0));

        Director.OpenChoice(OuiNon);
        for (var n = 1; n <= 37; n++)
        {
            Assert.True(Director.SelectChoiceForTests(0)); // called at every tick, as the arcs do
            Director.Tick();
        }

        Assert.True(Director.SelectChoiceForTests(0)); // the result is written, not yet taken
        Assert.Equal(1, Director.TakeChoiceResult());
        Assert.Equal(new[] { (0, 4), (19, 5), (19, 2) }, Played); // one Cross only
        Assert.False(Director.SelectChoiceForTests(0));
    }

    [Fact]
    public void TheHook_ArmedAfterTheFirstInteractivePass_PressesAtTheNextPass()
    {
        Director.OpenChoice(OuiNon);
        for (var n = 1; n <= 25; n++)
        {
            Director.Tick();
        }

        Assert.True(Director.SelectChoiceForTests(0));
        Assert.Equal(44 - 25, TicksUntilTaken(out var result)); // Cross at the pass 26, closed and written at the pass 44
        Assert.Equal(1, result);
        Assert.Equal(new[] { (0, 4), (26, 5), (26, 2) }, Played);
    }

    [Fact]
    public void WithoutTheHook_NothingAnswers()
    {
        Director.OpenChoice(OuiNon);
        Assert.Equal(-1, TicksUntilTaken(out var result, 120));
        Assert.Null(result);
        Assert.True(Director.IsAwaitingChoice);
        Assert.Equal(new[] { (0, 4) }, Played);
    }

    [Fact]
    public void ThePadOfTheTickBefore_IsReadThroughTheGameState_WhenThereIsNoProxy()
    {
        Director.OpenChoice(OuiNon);
        for (var n = 1; n <= 18; n++)
        {
            Director.Tick();
        }

        _state.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Cross };
        Director.Tick(); // the pass 19 does not see the Cross yet: the seam reads the pad after the pass
        _state.LastPadState = new AlundraPadState { ButtonsHold = 0 };
        Assert.Null(Director.TakeChoiceResult());

        Assert.Equal(19, TicksUntilTaken(out var result)); // the pass 20 sees the Cross: written at 20 + 18 = 38
        Assert.Equal(1, result);
        Assert.Equal(new[] { (0, 4), (20, 5), (20, 2) }, Played);
    }

    [Fact]
    public void CancelChoice_ClosesTheMachine_AndNothingMoreHappens()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(0)); // an armed press must not play after the cancel
        for (var n = 1; n <= 8; n++)
        {
            Director.Tick();
        }

        Director.CancelChoice();

        for (var n = 9; n <= 45; n++)
        {
            Director.Tick();
            Assert.Equal(AlundraChoiceBox.Stage.Closed, Director.ChoiceBoxForTests.CurrentStage);
            Assert.Null(Director.ChoiceBoxForTests.Drawn);
            Assert.Null(Director.TakeChoiceResult());
            Assert.Empty(Director.ChoicesForTests);
            Assert.False(Director.IsAwaitingChoice);
        }

        Assert.Equal(new[] { (0, 4) }, Played); // no 5, no 2
    }

    [Fact]
    public void ATickWithoutAChoice_DoesNothing()
    {
        for (var n = 1; n <= 50; n++)
        {
            Director.Tick();
        }

        Assert.Empty(_sounds.Played);
        Assert.Equal(AlundraChoiceBox.Stage.Closed, Director.ChoiceBoxForTests.CurrentStage);
        Assert.Null(Director.TakeChoiceResult());
        Assert.False(Director.IsAwaitingChoice);
    }

    [Fact]
    public void AnotherOpen_CancelsAWaitingChoice_AndClosesTheMachine()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(0));
        for (var n = 1; n <= 10; n++)
        {
            Director.Tick();
        }

        Director.Open(null, null, controlMode: 1);
        Assert.False(Director.IsAwaitingChoice);
        for (var n = 11; n <= 60; n++)
        {
            Director.Tick();
            Assert.Equal(AlundraChoiceBox.Stage.Closed, Director.ChoiceBoxForTests.CurrentStage);
            Assert.Null(Director.TakeChoiceResult());
        }

        Assert.DoesNotContain(_sounds.Played, p => p.Sfx is 5 or 2);
    }

    [Fact]
    public void AMapEntry_DuringAChoice_ResetsTheMachine_ButNotTheCursorCounter()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(0));
        for (var n = 1; n <= 12; n++)
        {
            Director.Tick();
        }

        var counter = Director.ChoiceBoxForTests.CursorTick;
        Assert.Equal(11, counter); // the passes 2 to 12 drew

        Director.InstallForMapEntry();

        Assert.False(Director.IsAwaitingChoice);
        Assert.Empty(Director.ChoicesForTests);
        for (var n = 13; n <= 60; n++)
        {
            Director.Tick();
            Assert.Equal(AlundraChoiceBox.Stage.Closed, Director.ChoiceBoxForTests.CurrentStage);
            Assert.Null(Director.TakeChoiceResult());
        }

        Assert.Equal(counter, Director.ChoiceBoxForTests.CursorTick);
        Assert.DoesNotContain(_sounds.Played, p => p.Sfx is 5 or 2);
    }

    [Fact]
    public void CloseStandaloneChoice_ClosesTheMachine_AndNothingMoreHappens()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(0));
        for (var n = 1; n <= 10; n++)
        {
            Director.Tick();
        }

        Assert.True(Director.CloseStandaloneChoice());

        for (var n = 11; n <= 60; n++)
        {
            Director.Tick();
            Assert.Equal(AlundraChoiceBox.Stage.Closed, Director.ChoiceBoxForTests.CurrentStage);
            Assert.Null(Director.TakeChoiceResult());
        }

        Assert.False(Director.IsAwaitingChoice);
        Assert.Empty(Director.ChoicesForTests);
        Assert.Equal(new[] { (0, 4) }, Played);
    }

    [Fact]
    public void ASecondChoice_AsksAgain_AndTheCursorCounterGoesOn()
    {
        Director.OpenChoice(OuiNon);
        Assert.True(Director.SelectChoiceForTests(0));
        Assert.Equal(37, TicksUntilTaken(out var first));
        Assert.Equal(1, first);
        Assert.Equal(35, Director.ChoiceBoxForTests.CursorTick); // the passes 2 to 36 drew, nothing at the close pass

        // The same 0x44 reached again: the opener asks again, from a result word at 0.
        Director.OpenChoice(OuiNon);
        Assert.True(Director.IsAwaitingChoice);
        Assert.Null(Director.TakeChoiceResult());
        Director.Tick(); // init pass: nothing drawn
        Assert.Equal(35, Director.ChoiceBoxForTests.CursorTick);
        Director.Tick(); // the first drawn pass: the counter goes on
        Assert.Equal(36, Director.ChoiceBoxForTests.CursorTick);
        Assert.Equal(3, Director.ChoiceBoxForTests.Drawn!.CursorImage);
    }

    // ---- the real pad through the world proxy --------------------------------------------------------------------------------

    private const int OpenerFrame = 30;

    private static (int ClosedAt, int? Result, List<(int Rel, int Sfx)> Sounds) RunThroughTheProxy(Dictionary<int, uint> holds, int frames = 150)
    {
        using var montage = new DialogueBoxMontage();
        var takenAt = -1;
        int? result = null;
        montage.PadForFrame = f => holds.GetValueOrDefault(f);
        montage.EntityScript = frame =>
        {
            if (frame == OpenerFrame)
            {
                montage.Director.OpenChoice(OuiNon);
            }
            else if (frame > OpenerFrame && takenAt < 0)
            {
                var r = montage.Director.TakeChoiceResult(); // the 0x44 polls every tick
                if (r != null)
                {
                    takenAt = frame;
                    result = r;
                }
            }
        };
        montage.RunFrames(frames);
        var sounds = montage.Sounds.Played.Where(s => s.Pass > OpenerFrame).Select(s => (s.Pass - OpenerFrame, s.Sfx)).ToList();
        return (takenAt < 0 ? -1 : takenAt - OpenerFrame, result, sounds);
    }

    [Fact]
    public void TheRealPadOfTheProxy_GivesTheModelsResolutionTicks_AndSounds()
    {
        // The pass N+t reads the pad of the frame N+t-2 (one tick for the pad pass of the proxy, one for the "tick before" rule); N = 30.

        // a: Right held on the frames 36..44 (seen by passes the box does not read yet), Cross at 60: OUI, the Cross is seen by the pass N+32
        var holds = new Dictionary<int, uint>();
        for (var f = 36; f <= 44; f++)
        {
            holds[f] = AlundraPadState.Right;
        }

        holds[60] = AlundraPadState.Cross;
        var a = RunThroughTheProxy(holds);
        Assert.Equal((50, 1), (a.ClosedAt, a.Result));
        Assert.Equal(new[] { (32, 5), (32, 2) }, a.Sounds);

        // b: Cross held on the frames 40..58 (its edge is seen before the first interactive pass: lost), released, pressed again at 62
        holds = new Dictionary<int, uint>();
        for (var f = 40; f <= 58; f++)
        {
            holds[f] = AlundraPadState.Cross;
        }

        holds[62] = AlundraPadState.Cross;
        var b = RunThroughTheProxy(holds);
        Assert.Equal((52, 1), (b.ClosedAt, b.Result));
        Assert.Equal(new[] { (34, 5), (34, 2) }, b.Sounds);

        // c: Cross at the frame 47 only: seen by the pass N+19, the first interactive one: OUI at N+37
        var c = RunThroughTheProxy(new Dictionary<int, uint> { [47] = AlundraPadState.Cross });
        Assert.Equal((37, 1), (c.ClosedAt, c.Result));
        Assert.Equal(new[] { (19, 5), (19, 2) }, c.Sounds);

        // d: Right held on the frames 36..80 (the edge is lost, the repeat comes 21 passes later and moves the cursor at N+29), Cross at 90: NON
        holds = new Dictionary<int, uint>();
        for (var f = 36; f <= 80; f++)
        {
            holds[f] = AlundraPadState.Right;
        }

        holds[90] = AlundraPadState.Cross;
        var d = RunThroughTheProxy(holds);
        Assert.Equal((80, 0), (d.ClosedAt, d.Result));
        Assert.Equal(new[] { (29, 1), (62, 5), (62, 3) }, d.Sounds);

        // e: Cross at the frame 46 only: one pass too early (N+18, still sliding in): lost, the choice waits for ever
        var e = RunThroughTheProxy(new Dictionary<int, uint> { [46] = AlundraPadState.Cross }, frames: 200);
        Assert.Equal(-1, e.ClosedAt);
        Assert.Empty(e.Sounds);
    }
}
