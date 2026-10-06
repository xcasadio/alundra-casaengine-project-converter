#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.h1b3 (docs/plan-e19-opcodes.md §1.2n.1d, H1B3-2): the real sites of <c>0x22</c> / <c>0x23</c> of the export, the 33 instances of the 17 programs listed in
/// <c>docs/plan-e19-op22-annexe/site-table.txt</c>, run on the real DLL through <see cref="ArcRun"/> (real controller, real prefabs, the map's own program).
/// Each row is: the frame of the first call at the wait's pc, the frame of the instruction after the wait, the number of calls (the second difference; the
/// binary's count, <c>all17.py</c>) and the entity's <c>PosZ</c> at the end of the frame the wait ended in. The set-up of each site is the one of the
/// annex's <c>op22-probe-tests.cs.txt</c>: the hero on a tile of the program's gate, the arrival image; map 363: flag 32769 bit 1 set after the map load;
/// map 36 C[4]: flag 32774 bit 6 set after the load, then cleared at frame 3, the hero placed on the platform once it has landed; map 115: the hero in
/// the zone and the dialogue button pressed on every dialogue frame.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeightTargetSitesTests
{
    internal sealed record Site(string Id, int Map, int HeroX, int HeroY, int HeroZ, int Rec, int Slot, int Pc, int Opcode, int Frames,
        int FirstFrame, int EndFrame, int Calls, int EndPosZ)
    {
        public bool RideHero { get; init; }
        public bool Dialogue { get; init; }
        public uint? Flag { get; init; }
    }

    private static IEnumerable<Site> AllSites()
    {
        // 127: the twelve balls, the hero in the gate of the program.
        yield return new Site("s127_rec6", 127, 10, 48, 5, 6, 2, 280, 0x22, 2500, 20, 180, 160, 10485759);
        yield return new Site("s127_rec7", 127, 15, 48, 5, 7, 2, 316, 0x22, 2500, 20, 180, 160, 10485759);
        yield return new Site("s127_rec11", 127, 7, 50, 1, 11, 2, 460, 0x22, 2500, 20, 180, 160, 6291455);
        yield return new Site("s127_rec12", 127, 4, 50, 1, 12, 2, 424, 0x22, 2500, 20, 180, 160, 6291455);
        yield return new Site("s127_rec8", 127, 3, 44, 9, 8, 2, 352, 0x22, 2500, 22, 214, 192, 15728639);
        yield return new Site("s127_rec9", 127, 5, 44, 9, 9, 2, 388, 0x22, 2500, 22, 214, 192, 15728639);
        yield return new Site("s127_rec13", 127, 4, 44, 9, 13, 2, 496, 0x22, 2500, 22, 214, 192, 15728639);
        yield return new Site("s127_rec14", 127, 7, 44, 9, 14, 2, 532, 0x22, 2500, 22, 214, 192, 15728639);
        yield return new Site("s127_rec15", 127, 10, 44, 9, 15, 2, 568, 0x22, 2500, 22, 214, 192, 15728639);
        yield return new Site("s127_rec16", 127, 13, 44, 9, 16, 2, 604, 0x22, 2500, 22, 214, 192, 15728639);
        // 127 rec17, rec18: 304 px, above what a controller's float32 root can hold at an odd value: the target stays literal (guard).
        yield return new Site("s127_rec17", 127, 5, 40, 13, 17, 2, 640, 0x22, 2500, 22, 214, 192, 19922944);
        yield return new Site("s127_rec18", 127, 10, 40, 13, 18, 2, 676, 0x22, 2500, 22, 214, 192, 19922944);

        // 89: the six balls of one program.
        foreach (var rec in new[] { 20, 21, 22, 23, 24, 25 })
        {
            yield return new Site("s89_rec" + rec, 89, 20, 35, 3, rec, 2, 663, 0x22, 3000, 48, 116, 68, 7340031);
        }

        // 363: the iron grid, flag 32769 bit 1 set after the map load.
        yield return new Site("s363_rec0", 363, 10, 10, 3, 0, 2, 454, 0x22, 3000, 14, 62, 48, 5242879) { Flag = 32769 };

        // 36 C[4]: the floating platform, the hero rides it.
        yield return new Site("s36c4_rec3", 36, 30, 19, 1, 3, 2, 491, 0x22, 3000, 166, 326, 160, 11534335) { RideHero = true };

        // 36 C[14] (0x23): the twelve hanging ceilings, the contact is posted from the first call: no call after it (guard).
        foreach (var rec in Enumerable.Range(42, 12))
        {
            yield return new Site("s36c14_rec" + rec, 36, 5, 10, 1, rec, 2, 1252, 0x23, 5000, 910, 910, 0, 1048576);
        }

        // 115 B[2]: the block of the descent, the hero in the zone, the dialogue button pressed.
        yield return new Site("s115_rec19", 115, 10, 55, 0, 19, 1, 367, 0x22, 6000, 756, 1077, 321, 3145727) { Dialogue = true };
    }

    public static IEnumerable<object[]> SiteIds() => AllSites().Select(s => new object[] { s.Id });

    [Fact]
    public void TheTableHasThe33InstancesOfThe17Sites()
    {
        Assert.Equal(33, AllSites().Count());
        Assert.Equal(33, AllSites().Select(s => s.Id).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SiteIds))]
    public void TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite(string id)
    {
        var site = AllSites().Single(s => s.Id == id);
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var worldDir = Directory.GetDirectories(Path.Combine(root, "Maps")).SelectMany(Directory.GetDirectories)
            .First(d => Path.GetFileName(d).EndsWith("-" + site.Map, StringComparison.Ordinal));

        using var arc = new ArcRun(new ArcSpec("H1b3-" + site.Id, Path.GetFileName(Path.GetDirectoryName(worldDir)!), Path.GetFileName(worldDir), Array.Empty<int>(),
            site.HeroX, site.HeroY, site.HeroZ, site.Frames, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((site.HeroX * 24 + 12) << 16, (site.HeroY * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0)));

        // The map load clears the temporary flags (the 0x8000 banks): the flags the programs wait for are set after it.
        if (site.Flag is { } flag)
        {
            AlundraGameState.Instance.AddFlag(flag, 1u << 1);
        }

        if (site.RideHero)
        {
            AlundraGameState.Instance.AddFlag(32774, 1u << 6);
        }

        var framesAtPc = new List<int>();
        var endFrame = -1;
        var endPosZ = int.MinValue;
        var ridden = false;
        arc.OnInstruction = t =>
        {
            if (t.Slot == site.Slot && t.Pc == site.Pc && t.Opcode == site.Opcode)
            {
                framesAtPc.Add(t.Frame);
            }
            else if (t.Slot == site.Slot && t.Pc == site.Pc + 1 && framesAtPc.Count > 0 && endFrame < 0)
            {
                endFrame = t.Frame;
            }
        };
        arc.OnFrame = () =>
        {
            var entity = arc.EntityByRecord(site.Rec);
            if (site.RideHero && arc.Frame == 3)
            {
                AlundraGameState.Instance.SetFlag(32774, ~(1u << 6));
            }

            if (site.RideHero && !ridden && entity != null && entity.CollidedWithEntityZ != 0 && entity.PosZ <= (16 << 16) + 70000)
            {
                ridden = true;
                arc.PlaceHero(entity.PosX >> 16, entity.PosY >> 16, (entity.PosZ >> 16) + 8);
            }

            if (entity != null && framesAtPc.Count > 0)
            {
                endPosZ = entity.PosZ;
            }
        };

        if (site.Dialogue)
        {
            arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => endFrame >= 0, "the 0x22 wait ends");
        }
        else
        {
            arc.RunUntil(() => endFrame >= 0, "the 0x22 wait ends");
        }

        // (first call frame, end frame, calls after the first, PosZ at the end of the frame the wait ended in)
        Assert.Equal((site.FirstFrame, site.EndFrame, site.Calls, site.EndPosZ), (framesAtPc[0], endFrame, endFrame - framesAtPc[0], endPosZ));
    }
}
