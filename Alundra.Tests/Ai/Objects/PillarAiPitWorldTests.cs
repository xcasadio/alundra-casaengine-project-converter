#nullable enable
using System.Linq;
using Xunit;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, phase B, test (w6)): the pillar of the map 347 (<c>Lake Shrine (inner)-347</c>, record 16) pushed along the row 14 of the plateau (height 48) until it falls into a pit of one cell (height 16), where it settles; the hero
/// then crosses the pit on top of it. The records 6 and 11 are the entities of the puzzle that stand on the pillar's way: the harness removes them by a write (<c>DestroyEntity</c>) as the discovery's probe did (the way they are opened in the game is not ported yet), and one test
/// leaves them. The machine (<c>disc/out/p11_*.txt</c>) has the same contact (the frames 7 and 8), the same animation 1 (39 and 40), the same landing cell; the DLL moves one tick later at the first step, and lands one tick later (D-G5-4, Q-G5-3).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarAiPitWorldTests
{
    private const string Zone = "Lake Shrine";
    private const string World = "Lake Shrine (inner)-347";
    private const int PillarRecord = 16;

    private static PillarRun Run(string name, int heroX, params int[] destroy) => new(name, Zone, World, heroX, 232, 48, true, destroy);

    [Fact]
    public void W6_PushedEast_ThePillarFallsIntoThePitAndSettlesAt278_Z16_TheHeroCrossesOnItAtTheOldFloor_ItsSupportIsThePillar()
    {
        using var run = Run("w6e", 168, 6, 11);
        var pillar = run.Record(PillarRecord);
        Assert.Equal((204.0, 232.0, 48.0), PillarRun.PixelOf(pillar));
        run.Watched = pillar;

        run.Hold(PadState.Right, 260);

        Assert.Null(run.At(6).BlockedBy);
        Assert.Equal(PillarRecord, run.At(7).BlockedBy);
        Assert.Equal(0u, run.At(38).PillarAnimation);
        Assert.Equal((1u, 24u, 1), (run.At(39).PillarAnimation, run.At(39).PillarDirection, run.At(39).PushSounds));
        Assert.Equal(204.0, run.At(39).PillarX);
        Assert.Equal(204.75, run.At(40).PillarX);

        // along the plateau the pillar stays at the floor (z 48); it leaves the edge of the pit at the frame 134 and lands at the frame 144, at (278, 232, 16).
        Assert.All(run.Samples.Take(133), s => Assert.Equal(48.0, s.PillarZ));
        Assert.Equal((275.25, 47.5), (run.At(134).PillarX, run.At(134).PillarZ));
        Assert.True(run.At(143).PillarZ > 16.0);
        Assert.All(run.Samples.Skip(143), s => Assert.Equal((278.0, 232.0, 16.0), (s.PillarX, s.PillarY, s.PillarZ)));

        // it stays there: the animation goes back to 0 when the hero loses the contact (the frame 146), and the hero walks over the pit at the floor of the plateau, to the other side.
        Assert.Equal(0u, run.At(146).PillarAnimation);
        Assert.Equal(0u, run.At(260).PillarAnimation);
        Assert.All(run.Samples.Skip(143), s => Assert.Equal(48.0, s.HeroZ));
        Assert.Equal(373.0, run.At(260).HeroX);
        // over the pit (the pillar's own column, x 259 to 296), the hero's support is the pillar, at every frame.
        var over = run.Samples.Skip(144).Where(s => s.HeroX >= 259.0 && s.HeroX <= 296.0).ToList();
        // E19.d2c2 D5b T5 (D-D5B-11): 15 frames, was 16. The binary resets the push animation of the pillar at tick 145, the DLL one frame later (146, the accepted one-tick delay, D-E19-13, D-G5-4); during that frame the hero rests on
        // the pillar, which still carries its planned push of 0.75 px per tick, and the binary's rule (the planned force of the carrier, D-D5B-7) hands it over: the hero gains 0.75 px once, enters the column one frame earlier. The final x (373) is unchanged.
        Assert.Equal(15, over.Count);
        Assert.All(over, s => Assert.Equal(PillarRecord, s.SupportedBy));
    }

    [Fact]
    public void W6_PushedWest_ThePillarFallsIntoTheOtherPitAndSettlesAt130_Z16_TheHeroCrossesOnIt()
    {
        using var run = Run("w6w", 240, 6, 11);
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Left, 260);

        Assert.Null(run.At(7).BlockedBy);
        Assert.Equal(PillarRecord, run.At(8).BlockedBy);
        Assert.Equal((1u, 8u, 1), (run.At(40).PillarAnimation, run.At(40).PillarDirection, run.At(40).PushSounds));
        Assert.Equal(204.0, run.At(40).PillarX);
        Assert.Equal(203.25, run.At(41).PillarX);
        Assert.Equal(47.5, run.At(135).PillarZ);
        Assert.All(run.Samples.Skip(144), s => Assert.Equal((130.0, 232.0, 16.0), (s.PillarX, s.PillarY, s.PillarZ)));
        Assert.True(run.At(144).PillarZ > 16.0);
        Assert.All(run.Samples.Skip(144), s => Assert.Equal(48.0, s.HeroZ));
        Assert.Equal(34.0, run.At(260).HeroX);
        Assert.Contains(run.Samples, s => s.HeroX is >= 120.0 and <= 140.0 && s.SupportedBy == PillarRecord);
    }

    [Fact]
    public void W6_WithoutThePillarInThePit_TheHeroFallsIn_ThePillarIsWhatCarriesIt()
    {
        using var run = Run("w6c", 168, 6, 11);
        run.Watched = run.Record(PillarRecord);
        run.Hold(PadState.Right, 146); // the pillar has landed (the frame 144) and the hero is still west of the pit.

        ((IScriptHost)run.Arc.Proxy).DestroyEntity(run.Record(PillarRecord), -1);
        run.Hold(PadState.Right, 60);

        Assert.Equal(16.0, run.At(206).HeroZ); // the hero drops into the pit at x 277 ...
        Assert.Equal(277.0, run.At(206).HeroX);
        Assert.Null(run.At(206).SupportedBy);
    }

    [Fact]
    public void W6_WithTheBlockersOfThePuzzleLeftInPlace_ThePillarStopsAgainstThemAt254_AndNeverReachesThePit()
    {
        using var run = Run("w6b", 168);
        var pillar = run.Record(PillarRecord);
        run.Watched = pillar;

        run.Hold(PadState.Right, 260);

        Assert.All(run.Samples.Skip(105), s => Assert.Equal((254.0, 232.0, 48.0), (s.PillarX, s.PillarY, s.PillarZ)));
        Assert.True(run.At(104).PillarX < 254.0);
        Assert.Equal(1u, run.At(260).PillarAnimation); // the hero goes on pushing: the animation stays 1 against the blockers.
        Assert.True(run.Samples.All(s => s.PillarZ == 48.0));
    }
}
