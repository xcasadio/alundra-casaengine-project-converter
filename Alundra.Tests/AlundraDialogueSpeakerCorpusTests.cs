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
/// E19.f4b F4B-1, "corpus" (docs/plan-e19-opcodes.md): the speaker of each site of the real export. <c>docs/plan-e19-f4-annexe/speaker_sites.tsv</c> lists the reachable
/// <c>0x0D</c>, <c>0x5C</c> and <c>0xC4</c> of the 483 maps with the speaker the static analysis finds (the record that owns the program, the hero, or nobody) and what the
/// binary does with it; the 574 sites of <c>speaker_sites_retarget.tsv</c> (programs that contain a <c>0x43</c>, and three sites whose owner has one in another program) are
/// not decidable statically and are left out. The 2273 others go through the real interpreter, one after the other, and must give the expected class: a name box for the id
/// of the sprite type (or the operands of <c>0xC4</c>) when the ETC holds a name for it, a portrait when the record carries the flag, nothing else.
/// </summary>
public sealed class AlundraDialogueSpeakerCorpusTests
{
    private sealed record Site(string Map, string Op, string Pc, string Kind, int? SpriteType, bool HasPortrait, int? C4Name)
    {
        public string Key => $"{Map}|{Op}|{Pc}";
    }

    private static List<Site> Sites()
    {
        var retarget = File.ReadLines(SpeakerAnnex.File_("speaker_sites_retarget.tsv")).Skip(1).Select(l => l.Split('\t')).Select(c => $"{c[0]}|{c[1]}|{c[2]}").ToHashSet();
        var sites = new List<Site>();
        foreach (var line in File.ReadLines(SpeakerAnnex.File_("speaker_sites.tsv")).Skip(1))
        {
            var c = line.Split('\t');
            int? Number(string s) => s == "None" ? null : int.Parse(s);
            var site = new Site(c[0], c[1], c[2], c[6], Number(c[8]), c[10] == "True", Number(c[11]));
            if (!retarget.Contains(site.Key))
            {
                sites.Add(site);
            }
        }

        return sites;
    }

    [Fact]
    public void TheDecidableSites_AreTheTwoThousandTwoHundredAndSeventyThree()
    {
        var sites = Sites();

        Assert.Equal(2273, sites.Select(s => s.Key).Distinct().Count());
        Assert.Equal(2276, sites.Count); // five sites are shared by several records
    }

    [Fact]
    public void EachDecidableSite_GivesTheExpectedSpeakerClass()
    {
        var names = SpeakerAnnex.Names().Keys.ToHashSet();
        using var rig = new SpeakerRig();
        var failures = new List<string>();
        foreach (var site in Sites())
        {
            rig.Director.InstallForMapEntry();
            rig.World.Spawned.Clear();
            rig.World.PlayerEntity = null;

            // the speaker the analysis found: a record (its sprite type, its flag), the hero (type 0, no flag), or nobody (an entity with no sprite type)
            var speaker = site.Kind switch
            {
                "rec" => SpeakerRig.Speaker(site.SpriteType!.Value, site.HasPortrait, refId: 5),
                "player" => SpeakerRig.Speaker(site.SpriteType!.Value, portrait: false, refId: 5),
                _ => SpeakerRig.Speaker(-1, portrait: false, refId: 5),
            };

            AlundraEntityScriptProxy owner;
            int[] codes;
            if (site.Op == "0x0D")
            {
                owner = speaker;
                codes = SpeakerRig.Codes0D(SpeakerRig.TextAb);
            }
            else
            {
                owner = SpeakerRig.Speaker(-1, portrait: false);
                var search = site.Kind switch
                {
                    "player" => 0x81,
                    "self-unowned" => 0x80,
                    _ => 5,
                };
                if (site.Kind == "player")
                {
                    rig.World.PlayerEntity = speaker;
                }
                else if (site.Kind == "self-unowned")
                {
                    owner = speaker;
                }
                else
                {
                    rig.World.Spawned.Add(speaker);
                }

                codes = site.Op == "0x5C"
                    ? SpeakerRig.Codes5C(search, SpeakerRig.TextAb)
                    : SpeakerRig.CodesC4(search, site.C4Name ?? 0, SpeakerRig.TextAb);
            }

            var expectedNameId = site.Op == "0xC4" ? site.C4Name : site.Kind is "rec" or "player" ? site.SpriteType : null;
            var expectName = expectedNameId is { } id && names.Contains(id);
            var expectPortrait = site.Kind == "rec" && site.HasPortrait;

            var opened = rig.Run(owner, codes);
            var box = rig.Director.NameBox;
            var gotName = box.IsSlotOpen;
            var gotPortrait = rig.Director.Portrait.State != AlundraInventoryPortrait.StateIdle;
            if (!opened || gotName != expectName || gotPortrait != expectPortrait || (gotName && box.NameId != expectedNameId))
            {
                failures.Add($"map {site.Map} {site.Op} @{site.Pc} ({site.Kind}, type {site.SpriteType}): opened {opened}, name {(gotName ? box.NameId : -1)}/{(expectName ? expectedNameId : -1)}, portrait {gotPortrait}/{expectPortrait}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} sites differ; first ones:\n{string.Join("\n", failures.Take(10))}");
    }

    [Fact]
    public void ThePortraitBanksOfTheExport_AreTwentyFive_AllOf48Pixels()
    {
        var path = Path.Combine(FindProjectRoot(), "Data", "sprite-records.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var flagged = 0;
        var portraits = new List<(string Id, int Width, int Height)>();
        foreach (var record in document.RootElement.EnumerateObject())
        {
            if ((record.Value.GetProperty("FlagsPortraitShadowType").GetInt32() & 0x80) != 0)
            {
                flagged++;
            }

            if (record.Value.TryGetProperty("DialoguePortrait", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                portraits.Add((p.GetProperty("SpriteAssetId").GetString()!, p.GetProperty("Width").GetInt32(), p.GetProperty("Height").GetInt32()));
            }
        }

        Assert.Equal(25, portraits.Select(p => p.Id).Distinct().Count());
        Assert.Equal(flagged, portraits.Count); // the field is present exactly where the header's bit 0x80 is
        Assert.All(portraits, p => Assert.Equal(48, p.Width));
        Assert.All(portraits, p => Assert.Contains(p.Height, new[] { 56, 72 }));
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "Data", "sprite-records.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AlundraDialogueSpeakerCorpusTests: no 'alundra-project/Data/sprite-records.json' found above '{AppContext.BaseDirectory}' (the real converter export is needed).");
    }
}
