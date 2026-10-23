#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, phase B, test (w7), D-G5-5, Q-G5-4): the 13 pillars of the map 423 (<c>Nirude (inner)-423</c>, records 12 to 24, the sprite type 430) carry a program C of the map (the records 12 to 23 a program that does nothing, the record 24
/// a script of its own), and a program C of the map goes before the native one, as in the original: the native C76 never runs for them. The hero pushes each of them, from a side where it can reach the contact (all the contacts are checked), for 70 ticks: the twelve are
/// not moved, not animated, never counted and silent; the thirteenth moves only by its own script.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarAiInertWorldTests
{
    [Theory]
    [InlineData(12, -23, 0, PadState.Right)]
    [InlineData(13, -23, 0, PadState.Right)]
    [InlineData(14, -23, 0, PadState.Right)]
    [InlineData(15, -23, 0, PadState.Right)]
    [InlineData(16, 0, -23, PadState.Down)]
    [InlineData(17, 0, -23, PadState.Down)]
    [InlineData(18, 0, -23, PadState.Down)]
    [InlineData(19, 0, -23, PadState.Down)]
    [InlineData(20, 23, 0, PadState.Left)]
    [InlineData(21, 23, 0, PadState.Left)]
    [InlineData(22, 23, 0, PadState.Left)]
    [InlineData(23, 23, 0, PadState.Left)]
    public void W7_ThePillarsOfTheMap423_StayInert_TheHeroIsBlockedByEachOfThem_NothingIsCountedAnimatedMovedOrHeard(int record, int dx, int dy, uint key)
    {
        using var run = new PillarRun("w7-" + record, "Nirude", "Nirude (inner)-423", 372, 184, 64, true);
        var pillar = run.Record(record);
        Assert.Equal(430, pillar.SpriteType);
        Assert.Equal(76, pillar.SpriteProgramIndexes[ScriptHelper.ProgramCTick]);          // the sprite carries C76 ...
        Assert.NotEqual(0, pillar.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f);        // ... and the record a program C of the map, which goes first.
        var (x, y, z) = PillarRun.PixelOf(pillar);
        run.Arc.PlaceHero((int)x + dx, (int)y + dy, (int)z);
        run.Watched = pillar;
        run.Hold(0, 3);

        run.Hold(key, 70);

        Assert.True(run.Samples.Count(s => s.BlockedBy == record) >= 60, "the hero is blocked by the pillar (a real push)");
        Assert.All(run.Samples, s => Assert.Equal((x, y, z, 0u, 0, 0), (s.PillarX, s.PillarY, s.PillarZ, s.PillarAnimation, s.PillarCounter, s.PushSounds)));
        Assert.Equal(0, run.Arc.Runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, PillarAi.Code)));
        Assert.Empty(run.Stops);
    }

    [Fact]
    public void W7_ThePillar24OfTheMap423_HasItsOwnScript_TheNativeC76NeverRuns_ItIsMovedByTheScriptOnly()
    {
        using var run = new PillarRun("w7-24", "Nirude", "Nirude (inner)-423", 372, 184, 64, true);
        var pillar = run.Record(24);
        Assert.NotEqual(0, pillar.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f);
        run.Arc.PlaceHero(372, 161, 64);
        run.Watched = pillar;
        run.Hold(0, 3);

        run.Hold(PadState.Down, 70);

        Assert.True(run.Samples.Count(s => s.BlockedBy == 24) >= 60);
        Assert.Equal(0, run.Arc.Runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramCTick, PillarAi.Code)));
        Assert.Equal(0, run.Arc.Runner.NativeHandlerRunCount);
        Assert.All(run.Samples, s => Assert.Equal((0, 0), (s.PillarCounter, s.PushSounds))); // the word +0x274 is never touched, the sound 25 never asked.
        Assert.Empty(run.Stops);
        Assert.True(run.At(70).PillarY > 184.0); // the script of the record moves it (its own motion, not the native push).
    }
}
