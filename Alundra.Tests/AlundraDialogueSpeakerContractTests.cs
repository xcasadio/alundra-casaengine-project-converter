#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f4b F4B-1, T1 (docs/plan-e19-opcodes.md, "T1, contrat fermé"): the speaker name box and portrait at the level of the DIRECTOR, image by image, against the
/// value tables of the annex (<c>docs/plan-e19-f4-annexe/values.json</c> S1 to S7, <c>values-s8-72.json</c>, <c>values-s9-moving.json</c>; the model of
/// <c>model/f4_model.py</c> validated against the real binary code). Iteration f of the montage is image f of a table (n0 = 3): the test moves the world (S9 only),
/// runs <c>director.Pass(true, true)</c> (the pad of the model: held and just pressed at every frame), reads the draws of the name and of the portrait, the sound 7
/// (<c>close-trigger</c>) and the fall of <see cref="AlundraDialogueDirector.IsOpen"/> (<c>DialogClosed</c>), then, from f = 3, runs the opcode of the pending dialogue
/// on a fresh state through the real interpreter ("opened" when the box opened, else "retry (0)"), then reads the phase of the box. Compared: the phase (it pins the image
/// mapping), the name (drawn or not, x of its frame and of its text), the portrait (drawn or not, x, y, w, h, rgb, phase) and the events as a set per image; excluded:
/// the clip of the name (not ported, without effect), the position of the box (the column of E19.f2a) and <c>typing-done</c> (internal to the box, also E19.f2a).
/// </summary>
public sealed class AlundraDialogueSpeakerContractTests
{
    private sealed record Dlg(int Opcode, AlundraEntityScriptProxy Owner, int[] Codes);

    private sealed record Row(int Frame, string Phase, NameBoxDrawnState? Name, (int X, int Y, int W, int H, int Rgb, string Phase)? Portrait, HashSet<string> Events);

    private static string PhaseName(AlundraPortraitPhase phase) => phase switch
    {
        AlundraPortraitPhase.In => "in",
        AlundraPortraitPhase.Rest => "rest",
        AlundraPortraitPhase.Out => "out",
        AlundraPortraitPhase.Gone => "gone",
        _ => string.Empty,
    };

    private static List<Row> Run(SpeakerRig rig, IReadOnlyList<Dlg> dialogues, int frames, Action<int>? moveWorld = null)
    {
        var rows = new List<Row>();
        var next = 0;
        var director = rig.Director;
        for (var f = 0; f < frames; f++)
        {
            moveWorld?.Invoke(f);
            rig.Sound.Played.Clear();
            var wasOpen = director.IsOpen;
            rig.Pass();
            var events = new HashSet<string>();
            if (rig.Sound.Played.Contains(7))
            {
                events.Add("close-trigger");
            }

            if (wasOpen && !director.IsOpen)
            {
                events.Add("DialogClosed");
            }

            var name = director.NameBox.Drawn;
            var p = director.Portrait;
            (int, int, int, int, int, string)? portrait = p.DrawnThisStep ? (p.X, p.Y, p.DrawnWidth, p.DrawnHeight, p.Rgb, PhaseName(p.Phase)) : null;

            if (next < dialogues.Count && f >= 3)
            {
                var dialogue = dialogues[next];
                var opened = rig.Run(dialogue.Owner, dialogue.Codes);
                events.Add($"opcode 0x{dialogue.Opcode:x} -> {(opened ? "opened" : "retry (0)")}");
                if (opened)
                {
                    next++;
                }
            }

            rows.Add(new Row(f, director.Box.Phase, name, portrait, events));
        }

        return rows;
    }

    private static JsonElement[] Table(string file, string titlePrefix)
    {
        var document = JsonDocument.Parse(File.ReadAllText(SpeakerAnnex.File_(file)));
        var property = document.RootElement.EnumerateObject().Single(p => p.Name.StartsWith(titlePrefix, StringComparison.Ordinal));
        return property.Value.EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    private static List<string> Diff(IReadOnlyList<Row> rows, JsonElement[] table)
    {
        var diffs = new List<string>();
        Assert.Equal(table.Length, rows.Count);
        for (var f = 0; f < table.Length; f++)
        {
            var row = rows[f];
            var t = table[f];
            var where = $"image {f} (N{f - 3:+0;-0;+0})";
            if (t.GetProperty("phase").GetString() != row.Phase)
            {
                diffs.Add($"{where}: box phase {row.Phase}, table {t.GetProperty("phase").GetString()}");
            }

            var tn = t.GetProperty("name");
            if (tn.ValueKind == JsonValueKind.Null)
            {
                if (row.Name is not null)
                {
                    diffs.Add($"{where}: name drawn ({row.Name.FrameX}, {row.Name.TextX}), table none");
                }
            }
            else if (row.Name is null)
            {
                diffs.Add($"{where}: name not drawn, table frame {tn.GetProperty("frame")[0]} text {tn.GetProperty("text")[0]}");
            }
            else
            {
                if (tn.GetProperty("frame")[0].GetInt32() != row.Name.FrameX || tn.GetProperty("frame")[1].GetInt32() != AlundraDialogueNameBox.FrameY)
                {
                    diffs.Add($"{where}: name frame x {row.Name.FrameX}, table {tn.GetProperty("frame")[0]},{tn.GetProperty("frame")[1]}");
                }

                if (tn.GetProperty("text")[0].GetInt32() != row.Name.TextX || tn.GetProperty("text")[1].GetInt32() != AlundraDialogueNameBox.TextY)
                {
                    diffs.Add($"{where}: name text x {row.Name.TextX}, table {tn.GetProperty("text")[0]},{tn.GetProperty("text")[1]}");
                }
            }

            var tp = t.GetProperty("portrait");
            if (tp.ValueKind == JsonValueKind.Null)
            {
                if (row.Portrait is not null)
                {
                    diffs.Add($"{where}: portrait drawn {row.Portrait}, table none");
                }
            }
            else if (row.Portrait is not { } p)
            {
                diffs.Add($"{where}: portrait not drawn, table ({tp.GetProperty("x")}, {tp.GetProperty("y")}, {tp.GetProperty("w")}, {tp.GetProperty("h")}, {tp.GetProperty("rgb")}, {tp.GetProperty("phase")})");
            }
            else
            {
                var expected = (tp.GetProperty("x").GetInt32(), tp.GetProperty("y").GetInt32(), tp.GetProperty("w").GetInt32(), tp.GetProperty("h").GetInt32(), tp.GetProperty("rgb").GetInt32(), tp.GetProperty("phase").GetString()!);
                if (expected != p)
                {
                    diffs.Add($"{where}: portrait {p}, table {expected}");
                }
            }

            var expectedEvents = t.GetProperty("events").EnumerateArray().Select(e => e.GetString()!).Where(e => e != "typing-done").ToHashSet();
            if (!expectedEvents.SetEquals(row.Events))
            {
                diffs.Add($"{where}: events [{string.Join("; ", row.Events.OrderBy(e => e))}], table [{string.Join("; ", expectedEvents.OrderBy(e => e))}]");
            }
        }

        return diffs;
    }

    private static void AssertTable(string scenario, List<Row> rows, JsonElement[] table)
    {
        var diffs = Diff(rows, table);
        Assert.True(diffs.Count == 0, $"{scenario}: {diffs.Count} images differ from the table; first ones:\n{string.Join("\n", diffs.Take(8))}");
    }

    private static Dlg D0D(AlundraEntityScriptProxy owner, int text) => new(0x0D, owner, SpeakerRig.Codes0D(text));

    [Fact]
    public void TheConstantsOfTheName_AreTheBinarys()
    {
        Assert.Equal(140, AlundraDialogueNameBox.FrameY);
        Assert.Equal(148, AlundraDialogueNameBox.TextY);
    }

    [Fact]
    public void S1_NameAndPortrait()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        AssertTable("S1", Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60), Table("values.json", "S1 "));
    }

    [Fact]
    public void S2_PortraitOnly()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0xFF, portrait: true);
        AssertTable("S2", Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60), Table("values.json", "S2 "));
    }

    [Fact]
    public void S3_NameOnly()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x10C, portrait: false);
        AssertTable("S3", Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60), Table("values.json", "S3 "));
    }

    [Fact]
    public void S4_Neither()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0xFF, portrait: false);
        AssertTable("S4", Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60), Table("values.json", "S4 "));
    }

    [Fact]
    public void S5_TwoDialoguesInARow_SameSpeaker()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        var dialogues = new[] { D0D(speaker, SpeakerRig.TextAb), D0D(speaker, SpeakerRig.TextCd) };
        AssertTable("S5", Run(rig, dialogues, 110), Table("values.json", "S5 "));
    }

    [Fact]
    public void S6_0xC4_ExplicitName()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        var dialogue = new Dlg(0xC4, speaker, SpeakerRig.CodesC4(0x80, 0x10C, SpeakerRig.TextAb));
        AssertTable("S6", Run(rig, new[] { dialogue }, 60), Table("values.json", "S6 "));
    }

    [Fact]
    public void S7_0x5C_NoMatch()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        var dialogue = new Dlg(0x5C, speaker, SpeakerRig.Codes5C(0x05, SpeakerRig.TextAb));
        AssertTable("S7", Run(rig, new[] { dialogue }, 60), Table("values.json", "S7 "));
    }

    [Fact]
    public void S8_TheTallPortrait_48x72()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true, height: 72);
        AssertTable("S8", Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60), Table("values-s8-72.json", "S8 "));
    }

    [Fact]
    public void S9_TheSpeakerWalksAndTheCameraScrolls()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        var table = Table("values-s9-moving.json", "S9 ");
        var rows = Run(rig, new[] { D0D(speaker, SpeakerRig.TextAb) }, 60, f =>
        {
            if (f >= 4)
            {
                speaker.PosX = (200 + 2 * (f - 3)) << 16;
                rig.Camera = (40 + (f - 3), 20);
            }
        });
        AssertTable("S9", rows, table);
    }
}
