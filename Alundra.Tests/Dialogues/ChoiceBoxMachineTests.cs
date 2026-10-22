#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Dialogues;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// E19.f3a F3A-1: the machine of the choice box (<see cref="ChoiceBox"/>), the port of <c>ChoiceBox</c> of <c>docs/plan-e19-f3-annexe/model/choice_model.py</c>
/// (slot 3 of the binary's callback table, opener 0x80050BA8), against the values V1 to V7 of <c>docs/plan-e19-f3-annexe/values.json</c> (the model run on
/// the pad words listed there): per pass, the update function, whether it drew, the sounds, the close, the x of the frame, of the labels and of the cursor, the cursor
/// image, the selection, then the pass at which the result word is written and its value.
/// </summary>
public sealed class ChoiceBoxMachineTests
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

    private static string PhaseName(ChoiceBox.Stage stage) => stage switch
    {
        ChoiceBox.Stage.Init => "init",
        ChoiceBox.Stage.SlideIn => "in",
        ChoiceBox.Stage.Active => "active",
        ChoiceBox.Stage.SlideOut => "out",
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
        var box = new ChoiceBox();
        box.SeedCursorTickForTests(scenario.GetProperty("anim0").GetInt32());
        var pad = new TickPad();
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
                Assert.True(ChoiceBox.FrameY == drawn.FrameY, $"{where}: frame y");
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
        var box = new ChoiceBox();
        box.Open("ABCDEFGH", "IJKLMNOP");
        Assert.Equal(new[] { "ABCDEF", "IJKLMN" }, box.Labels);

        box.Pass(0, 0); // the init pass
        box.Pass(0, 0); // the first drawn pass: x 311
        Assert.Equal(311 + 16 + 4 * 6 - 8, box.Drawn!.CursorX); // x + 16 + 48 * selection + 4 * the length of the selected label - 8
    }

    [Fact]
    public void TheOpener_DoesNotResetTheCursorCounter_AndTheCounterWrapsAt40()
    {
        var box = new ChoiceBox();
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
        var box = new ChoiceBox();
        box.Open("OUI", "NON");
        for (var t = 1; t <= 18; t++)
        {
            box.Pass(PadState.Cross, 0); // N+1 to N+18: the init pass and the slide-in do not read the pad
            Assert.False(box.ClosedThisPass);
        }

        Assert.Equal(ChoiceBox.Stage.Active, box.CurrentStage);
        for (var t = 19; t <= 60; t++)
        {
            box.Pass(0, 0);
        }

        Assert.Equal(ChoiceBox.Stage.Active, box.CurrentStage); // still waiting for an answer
        Assert.Equal(0, box.ResultWord);
    }
}
