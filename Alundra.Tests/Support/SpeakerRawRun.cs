#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>One raw sequence of <c>raw_sequence</c> (gen_sequences.py), in the exact order of its draws, driving <paramref name="machines"/> and counting the same coverage.</summary>
internal static class SpeakerRawRun
{
    private static readonly int[] Ids = { 0x104, 0x10C, 0x1AB, 0x1AC, 0x0FF, 0x200, 0x1FD, 0x14B };
    private static readonly int[] Heights = { 56, 56, 72 };
    private const uint HasPortrait = 0x800000;

    /// <summary>The ETC the generator models: the 60 names, 0x1AB present but empty and 0x1AC absent (both refused).</summary>
    public static Func<int, int?> ModelWidths(IReadOnlyDictionary<int, (string Name, int Width)> names)
        => id => names.TryGetValue(id, out var n) ? n.Width : null;

    public static string[] CoverageKeys { get; } =
    {
        "opens", "name_open_ok", "name_refused_busy", "name_refused_range", "name_refused_empty", "portrait_open_ok", "portrait_open_ignored", "portrait_flagless",
        "name_closes", "name_close_while_sliding_in", "name_close_while_closing", "portrait_closes", "portrait_close_while_in", "portrait_close_while_out",
        "portrait_retarget_ignored", "frames_name", "frames_portrait", "frames_72", "speaker_moved_between_open_and_close",
    };

    public static (List<string> Lines, Dictionary<string, int> Coverage) Run(
        int seed, ISpeakerMachines machines, Func<int, int?> modelWidths, int frames = 400)
    {
        var rnd = new SplitMix64((ulong)seed);
        var pool = new List<RawEntity>();
        for (var i = 0; i < 4; i++)
        {
            var id = rnd.Choice(Ids);
            var flags = rnd.Choice(new uint[] { 0, HasPortrait, HasPortrait, HasPortrait });
            var x = rnd.RandInt(-100, 500);
            var y = rnd.RandInt(-100, 500);
            var z = rnd.RandInt(-20, 60);
            var h = rnd.Choice(Heights);
            pool.Add(new RawEntity { Id = id, Flags = flags, X = x, Y = y, Z = z, H = h });
        }

        var cam = (X: rnd.RandInt(0, 200), Y: rnd.RandInt(0, 200));
        RawEntity? speaker = null;
        var lines = new List<string>();
        var cov = CoverageKeys.ToDictionary(k => k, _ => 0);
        var openPos = new Dictionary<RawEntity, (int, int, int)>();
        for (var f = 0; f < frames; f++)
        {
            foreach (var e in pool)
            {
                if (rnd.Chance(500))
                {
                    e.X += rnd.RandInt(-6, 6);
                    e.Y += rnd.RandInt(-6, 6);
                    e.Z = Math.Max(-30, Math.Min(80, e.Z + rnd.RandInt(-2, 2)));
                }
            }

            if (rnd.Chance(500))
            {
                cam.X += rnd.RandInt(-5, 5);
                cam.Y += rnd.RandInt(-5, 5);
            }

            var cn = rnd.Chance(40);
            var cp = rnd.Chance(40);
            var attempt = rnd.Chance(50);
            var ent = attempt ? rnd.Choice(pool) : null;

            if (cn)
            {
                cov["name_closes"]++;
                if ((machines.NameFlags & 1) != 0)
                {
                    cov["name_close_while_sliding_in"]++;
                }

                if ((machines.NameFlags & 2) != 0)
                {
                    cov["name_close_while_closing"]++;
                }

                machines.NameClose();
            }

            if (cp && machines.PortraitState != 0 && speaker is not null)
            {
                cov["portrait_closes"]++;
                if ((machines.PortraitState & 1) != 0)
                {
                    cov["portrait_close_while_in"]++;
                }

                if ((machines.PortraitState & 2) != 0)
                {
                    cov["portrait_close_while_out"]++;
                }

                if (!openPos.TryGetValue(speaker, out var at) || at != (speaker.X, speaker.Y, speaker.Z))
                {
                    cov["speaker_moved_between_open_and_close"]++;
                }

                machines.PortraitClose(speaker, cam);
            }

            var nd = machines.NamePass();
            var pd = machines.PortraitPass();
            if (attempt)
            {
                cov["opens"]++;
                if ((ent!.Flags & HasPortrait) != 0)
                {
                    if (machines.PortraitOpen(ent, cam, ent.H))
                    {
                        speaker = ent;
                        openPos[ent] = (ent.X, ent.Y, ent.Z);
                        cov["portrait_open_ok"]++;
                        if (ent.H == 72)
                        {
                            cov["frames_72"]++;
                        }
                    }
                    else
                    {
                        cov["portrait_open_ignored"]++;
                        if (!ReferenceEquals(ent, speaker))
                        {
                            cov["portrait_retarget_ignored"]++;
                        }
                    }
                }
                else
                {
                    cov["portrait_flagless"]++;
                }

                var nid = ent.Id;
                if ((machines.NameFlags & 4) != 0)
                {
                    cov["name_refused_busy"]++;
                }
                else if (!(nid >= 0x100 && nid < 0x200))
                {
                    cov["name_refused_range"]++;
                }
                else if (modelWidths(nid) is null)
                {
                    cov["name_refused_empty"]++;
                }
                else
                {
                    cov["name_open_ok"]++;
                }

                machines.NameOpen(nid);
            }

            if (nd is not null)
            {
                cov["frames_name"]++;
            }

            if (pd is not null)
            {
                cov["frames_portrait"]++;
            }

            lines.Add($"{f}|{nd ?? "-"}|{pd ?? "-"}|{machines.NameFlags}|{(machines.NameSlot ? 1 : 0)}|{machines.PortraitState}");
        }

        return (lines, cov);
    }

    public static string Digest(IEnumerable<string> lines)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', lines)))).ToLowerInvariant();
}
