#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using MGUI.Core.UI;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f3b F3B-1 (docs/plan-e19-opcodes.md, F3B-R2): <see cref="AlundraChoiceViewModel.Apply"/> against the real <see cref="AlundraChoiceBox"/>, pass by pass, on V1 and V2 of
/// <c>docs/plan-e19-f3-annexe/values.json</c> (the model run on the pad words listed there): nothing shows while the box draws nothing (the init pass, the close pass); else
/// the frame at the pass's x (311 ... 176 at the entry, 176, 185, ... 320 at the exit), the labels and the cursor (sprite by image, x by selection); and the view model
/// notifies only what changes.
/// </summary>
public sealed class AlundraChoiceViewModelTests
{
    private static readonly string[] Sprites = { "wind_150", "wind_173", "wind_201", "wind_228" };

    private static JsonElement Scenario(int index)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f3-annexe", "values.json");
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.GetProperty("bare").EnumerateArray().ElementAt(index).Clone();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("no docs/plan-e19-f3-annexe/values.json found above " + AppContext.BaseDirectory);
    }

    /// <summary>Every property change of the view model and of its parts.</summary>
    private static List<string> Watch(AlundraChoiceViewModel viewModel)
    {
        var events = new List<string>();
        void Hook(string owner, INotifyPropertyChanged source) => source.PropertyChanged += (_, e) => events.Add($"{owner}.{e.PropertyName}");
        Hook("Root", viewModel);
        Hook("Frame", viewModel.Frame);
        Hook("Label0", viewModel.Label0);
        Hook("Label1", viewModel.Label1);
        Hook("Cursor", viewModel.Cursor);
        return events;
    }

    /// <summary>Runs the scenario's passes on the real box and applies the view model after each one (the presenter's job), checking every pass against the table.</summary>
    private static void Run(int index, Action<int, JsonElement, AlundraChoiceBox, AlundraChoiceViewModel, List<string>> afterEachPass)
    {
        var scenario = Scenario(index);
        var raw = new Dictionary<int, uint>();
        foreach (var entry in scenario.GetProperty("pad").EnumerateObject())
        {
            raw[int.Parse(entry.Name.Substring(2), CultureInfo.InvariantCulture)] = Convert.ToUInt32(entry.Value.GetString(), 16);
        }

        var table = scenario.GetProperty("table").EnumerateArray().ToArray();
        var box = new AlundraChoiceBox();
        box.SeedCursorTickForTests(scenario.GetProperty("anim0").GetInt32());
        var viewModel = new AlundraChoiceViewModel();
        var events = Watch(viewModel);
        var pad = new AlundraTickPad();
        for (var t = 0; t < table.Length; t++)
        {
            pad.Update(raw.GetValueOrDefault(t));
            if (t == 0)
            {
                box.Open("OUI", "NON", scenario.GetProperty("default_sel").GetInt32());
                continue;
            }

            box.Pass(pad.ButtonsJustPressed, pad.ButtonsJustPressedByInterval);
            events.Clear();
            viewModel.Apply(box);
            afterEachPass(t, table[t], box, viewModel, events);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Apply_FollowsTheTable_PassByPass_OnV1AndV2(int index)
    {
        var lastDrawnFrame = (int?)null;
        Run(index, (t, row, box, viewModel, events) =>
        {
            var where = $"V{index + 1}, pass N+{t}";
            if (!row.GetProperty("drawn").GetBoolean())
            {
                Assert.True(Visibility.Collapsed == viewModel.RootVisibility, $"{where}: collapsed while nothing is drawn");
                Assert.True(lastDrawnFrame == viewModel.Frame.Left, $"{where}: nothing else is touched ({lastDrawnFrame} then {viewModel.Frame.Left})");
                return;
            }

            Assert.True(Visibility.Visible == viewModel.RootVisibility, $"{where}: visible");
            Assert.True(row.GetProperty("frame_x").GetInt32() == viewModel.Frame.Left, $"{where}: frame x");
            Assert.True(row.GetProperty("label0_x").GetInt32() == viewModel.Label0.Left, $"{where}: label 0 x");
            Assert.True(row.GetProperty("label1_x").GetInt32() == viewModel.Label1.Left, $"{where}: label 1 x");
            Assert.True("OUI" == viewModel.Label0.Text && "NON" == viewModel.Label1.Text, $"{where}: label texts");
            Assert.True(row.GetProperty("cursor_x").GetInt32() == viewModel.Cursor.Left, $"{where}: cursor x");
            Assert.True(Sprites[row.GetProperty("cursor_img").GetInt32()] == viewModel.Cursor.SourceName, $"{where}: cursor sprite");
            lastDrawnFrame = viewModel.Frame.Left;
        });
    }

    /// <summary>V1 at its key passes, the values the plan writes: the frame 311 at the first drawn pass, 176 from the 15th, 320 off screen at the end of the exit.</summary>
    [Fact]
    public void V1_TheFrameSlidesIn_RestsAt176_ThenSlidesOutToThe320()
    {
        var frames = new Dictionary<int, int?>();
        var roots = new Dictionary<int, Visibility>();
        Run(0, (t, _, _, viewModel, _) =>
        {
            frames[t] = viewModel.Frame.Left;
            roots[t] = viewModel.RootVisibility;
        });

        Assert.Equal(Visibility.Collapsed, roots[1]); // the init pass draws nothing
        Assert.Null(frames[1]);
        Assert.Equal(new int?[] { 311, 301, 292, 282, 272, 263, 253, 244, 234, 224, 215, 205, 196, 186, 176, 176 }, Enumerable.Range(2, 16).Select(t => frames[t]).ToArray());
        Assert.Equal(176, frames[19]);
        Assert.Equal(new int?[] { 176, 185, 195, 204, 214, 224, 233, 243, 252, 262, 272, 281, 291, 300, 310, 320, 320 }, Enumerable.Range(20, 17).Select(t => frames[t]).ToArray());
        Assert.Equal(Visibility.Collapsed, roots[37]); // the close pass writes the result and draws nothing
        Assert.Equal(320, frames[37]); // nothing but the visibility is written
    }

    /// <summary>V2 (Right at N+19, Cross at N+22): the cursor goes to the NON label at the pass after Right (the selection changes at N+19), the image follows the counter.</summary>
    [Fact]
    public void V2_TheCursorFollowsTheSelection_AndTheImageFollowsTheCounter()
    {
        var cursor = new Dictionary<int, (int? Left, string? Source)>();
        Run(1, (t, _, _, viewModel, _) => cursor[t] = (viewModel.Cursor.Left, viewModel.Cursor.SourceName));

        Assert.Equal((196, "wind_173"), cursor[18]);
        Assert.Equal((244, "wind_173"), cursor[19]);
        Assert.Equal((244, "wind_201"), cursor[21]);
        Assert.Equal((244, "wind_201"), cursor[22]);
    }

    /// <summary>The events of each Apply on V1: the init pass writes nothing; the first drawn pass writes everything (visibility, frame, label text and x twice, cursor sprite and
    /// x = 8); the next one only the positions (frame, two labels, cursor = 4); a pass that moves nothing and keeps the cursor image (N+17) notifies nothing; the close pass
    /// only the visibility; and the same state applied twice notifies nothing the second time.</summary>
    [Fact]
    public void Apply_NotifiesOnlyWhatChanges()
    {
        var counts = new Dictionary<int, int>();
        Run(0, (t, row, box, viewModel, events) =>
        {
            counts[t] = events.Count;

            events.Clear();
            viewModel.Apply(box);
            Assert.True(events.Count == 0, $"pass N+{t}: a second Apply of the same state notified {string.Join(", ", events)}");
        });

        Assert.Equal((0, 8, 4, 0, 1), (counts[1], counts[2], counts[3], counts[17], counts[37]));
    }

    [Fact]
    public void Apply_CountsItsRuns()
    {
        var box = new AlundraChoiceBox();
        var viewModel = new AlundraChoiceViewModel();
        Assert.Equal(Visibility.Collapsed, viewModel.RootVisibility);
        viewModel.Apply(box);
        viewModel.Apply(box);
        Assert.Equal(2, viewModel.AppliedCount);
        Assert.Equal(Visibility.Collapsed, viewModel.RootVisibility);
    }
}
