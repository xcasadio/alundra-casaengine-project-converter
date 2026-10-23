#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, phase B, test (w5)): what stops a pushed pillar, on a bench built in the map 26 (<c>Ancient Shrine-26</c>): the real pillar (record 29) carried to (260, 424) so that it has room to run, the hero west of it, and
/// at (310, 424) either a stepping switch (map page, sprite 45, a collidable slab 24 wide, the kind of the plate of the puzzle rooms) or a second pillar (map page 174, spawned by the production call, with its program C76). The pillar stops against the slab
/// (310 - 12 - 10 = 288) and against the second pillar (310 - 20 = 290), and the second pillar is never pushed: the hero is blocked by the FIRST one, so the second one sees no contact.
/// The plate of the map 27 (sprite 108) is not in the page of the map 26 and the map 27 holds no pillar: the switch stands for it (the machine gives the same stop for both, <c>disc/out/p8_*.txt</c>).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PillarAiBenchWorldTests
{
    private const int PillarRecord = 29;
    private const int SwitchIndex = 45;
    private const int PillarIndex = 174;

    private static PillarRun Bench(string name, int? otherIndex, out EntityScriptProxy pillar, out EntityScriptProxy? other)
    {
        var run = new PillarRun(name, "Ancient Shrine", "Ancient Shrine-26", 236, 424, 16);
        try
        {
            pillar = run.Record(PillarRecord);
            pillar.PosX = 260 << 16;
            pillar.PosY = 424 << 16;
            pillar.TileX = 260 / 24;
            pillar.TileY = 424 / 16;
            pillar.PushLogicalPositionToRoot();
            other = otherIndex is { } index
                ? ((IEntityWorldContext)run.Arc.Proxy).SpawnEntityBySpriteIndex(run.Hero, true, index, 310 << 16, 424 << 16, 0, 0)
                : null;
            if (otherIndex is not null)
            {
                Assert.NotNull(other);
            }

            run.Hold(0, 4);
            run.Watched = pillar;
            return run;
        }
        catch
        {
            run.Dispose();
            throw;
        }
    }

    [Fact]
    public void W5_WithNothingOnItsPath_TheRelocatedPillarRunsToTheWall_TheControlOfTheTwoTestsBelow()
    {
        using var run = Bench("w5c", null, out var pillar, out _);

        run.Hold(PadState.Right, 200);

        Assert.Equal((350.0, 424.0, 16.0), PillarRun.PixelOf(pillar));
    }

    [Fact]
    public void W5_ASteppingSwitchOnTheGroundBlocksThePillar_ItStopsAgainstTheSlab_AndDoesNotClimbOnIt()
    {
        using var run = Bench("w5s", SwitchIndex, out var pillar, out var slab);

        Assert.Equal((310.0, 424.0, 16.0), PillarRun.PixelOf(slab!));
        Assert.Equal(301, slab!.SpriteType);
        Assert.NotEqual(0u, slab.Flags & EntityFlags.Collidable);

        run.Hold(PadState.Right, 200);

        Assert.Equal((288.0, 424.0, 16.0), PillarRun.PixelOf(pillar)); // 310 - 12 - 10.
        Assert.Equal((310.0, 424.0, 16.0), PillarRun.PixelOf(slab)); // the slab did not move.
        Assert.Equal(1u, pillar.TargetAnimationId); // the hero goes on pushing: the animation stays 1 against the slab, and the sound is asked.
        Assert.Equal(1, run.At(200).PushSounds);
    }

    [Fact]
    public void W5_TwoAlignedPillarsAreNotPushedTogether_TheFirstStopsAgainstTheSecond_TheSecondSeesNoContactAndStaysInert()
    {
        using var run = Bench("w5p", PillarIndex, out var pillar, out var second);

        Assert.Equal(430, second!.SpriteType);
        Assert.Equal(PillarAi.Code, second.SpriteProgramIndexes[ScriptHelper.ProgramCTick]); // the second one runs C76 too: it is a pillar like the first.
        Assert.Contains(second, run.Arc.Entities);

        run.Hold(PadState.Right, 200);

        Assert.Equal((290.0, 424.0, 16.0), PillarRun.PixelOf(pillar)); // 310 - 20.
        Assert.Equal((310.0, 424.0, 16.0), PillarRun.PixelOf(second));
        Assert.Equal((0u, 0), (second.TargetAnimationId, PillarAi.ReadCounter(second)));
        Assert.Equal(PillarRecord, run.At(200).BlockedBy); // the hero is blocked by the first pillar only.
    }
}
