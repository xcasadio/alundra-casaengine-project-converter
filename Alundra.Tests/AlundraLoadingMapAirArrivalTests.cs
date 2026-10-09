#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.v (docs/plan-e19-opcodes.md section 1.2z, O-E19-32): the LoadingMap case of the binary's MovePlayer (0x800325E8) gives 0x2D as soon as
/// the hero is in the air, then leaves the switch. Two real portal arrivals of the export (docs/plan-e19-v-annexe/, measured before the change):
/// through portal 4 of map 20 the hero arrives on the Well (311) 432 px above the ground; through portal 3 of map 6 he arrives on the ground of
/// map 10 (the witness). Real controller and prefabs, no input.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraLoadingMapAirArrivalTests
{
    private const uint LoadingMap = 0x36;
    private const uint AirStill = 0x2D;
    private const uint Idle = 0;

    private sealed record Sample(int Frame, uint Target, bool InTheAir, bool Unlocked);

    private static List<Sample> Run(string zone, string worldName, int tileX, int tileY, int zLevel, uint directionId, int frames)
    {
        var spec = new ArcSpec("E19v", zone, worldName, Array.Empty<int>(), 0, 0, 0, frames + 10, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((tileX * 24 + 12) << 16, (tileY * 16 + 8) << 16, zLevel << 20, AlundraGameState.ResetAnimationId, directionId));
        using var arc = new ArcRun(spec);
        var hero = arc.Hero;

        // The montage puts the hero at the end of the map-entry animation (Idle) because a bare montage never plays it; with the prefabs the
        // animation plays (its chain to Idle comes at the frame 25), so the arrival animation production keeps is given back.
        hero.TargetAnimationId = LoadingMap;
        var samples = new List<Sample>();
        for (var frame = 1; frame <= frames; frame++)
        {
            arc.OneFrame();
            samples.Add(new Sample(frame, hero.TargetAnimationId, hero.IsOnGround == 0 || hero.HeroAirborne,
                (ArcRun.State.PlayerControlFlags & AlundraGameState.PlayerControlBits.InputBlockedMask) == 0));
        }

        return samples;
    }

    [Fact]
    public void V1b_AnArrivalHighAboveTheWell_FallsIn0x2D_FromItsFirstFrameInTheAir_ThenIdleAfterTheLanding()
    {
        var samples = Run("Well", "Well (+Lurvy house unused_)-311", 33, 45, 28, 0, 60);

        Assert.All(samples, s => Assert.True(s.Unlocked, $"frame {s.Frame}: the hero is locked"));
        var first = samples.FirstOrDefault(s => s.Target == AirStill);
        // Measured after the change: the controller's contact is pulled before MovePlayer reads it, so 0x2D comes at the frame 1.
        Assert.True(first is not null && first.Frame == 1,
            $"0x2D first at frame {first?.Frame.ToString() ?? "never"}, expected at the frame 1 (in the air from frame {samples.First(s => s.InTheAir).Frame}); "
            + string.Join(" ", samples.Take(30).Select(s => $"{s.Frame}:0x{s.Target:X}")));

        // Measured before the change (docs/plan-e19-v-annexe/dynamic-traces.txt): the hero touches the ground at the frame 43, still in 0x2D,
        // and the common tail gives Idle at the frame 44.
        var landing = samples.First(s => s.Frame > first!.Frame && !s.InTheAir);
        Assert.Equal(43, landing.Frame);
        Assert.All(samples.Where(s => s.Frame >= first!.Frame && s.Frame <= landing.Frame), s => Assert.Equal(AirStill, s.Target));
        Assert.Equal(Idle, samples.Single(s => s.Frame == landing.Frame + 1).Target);
    }

    /// <summary>E19.v2 (V-R4): the first frame of a New Game in the real game (probe of 2026-10-09): the controller has not
    /// resolved the ground yet (IsOnGround 0) and the logical Z is still under the terrain. The hero is not in the air: the
    /// LoadingMap case leaves the arrival animation (it took the jump pose, which the intro's lock then froze).</summary>
    [Fact]
    public void V1d_LoadingMapWithAnUnresolvedGroundAndTheZUnderTheTerrain_StaysLoadingMap()
    {
        var rig = JumpHeroRig.Build(FlatCells.Create(cell: (_, _) => (0, 1)), z: 16f);
        var hero = rig.Hero;
        hero.TargetAnimationId = LoadingMap;
        hero.PosZ = 0;
        hero.IsOnGround = 0;
        Assert.Equal(1048576, hero.ComputeTerrainHeight());

        AlundraPlayerManager.MovePlayer(hero, default, new AlundraGameState(), host: null);

        Assert.Equal(LoadingMap, hero.TargetAnimationId);
    }

    /// <summary>E19.v2 (V-R4) guard: above its terrain and not on the ground, the hero in LoadingMap does take 0x2D.</summary>
    [Fact]
    public void V1e_LoadingMapAboveTheTerrainAndNotOnTheGround_Gives0x2D()
    {
        var rig = JumpHeroRig.Build(FlatCells.Create(cell: (_, _) => (0, 1)), z: 16f);
        var hero = rig.Hero;
        hero.TargetAnimationId = LoadingMap;
        hero.PosZ = 2097152;
        hero.IsOnGround = 0;

        AlundraPlayerManager.MovePlayer(hero, default, new AlundraGameState(), host: null);

        Assert.Equal(AirStill, hero.TargetAnimationId);
    }

    [Fact]
    public void V1c_AnArrivalOnTheGround_KeepsTheArrivalAnimationUntilItsChain_ThenIdle()
    {
        var samples = Run("Overworld", "Overworld 2,1-10", 0, 46, 0, 24, 30);

        Assert.DoesNotContain(samples, s => s.InTheAir);
        Assert.All(samples.Where(s => s.Frame <= 24), s => Assert.Equal(LoadingMap, s.Target));
        Assert.Equal(Idle, samples.Single(s => s.Frame == 25).Target);
    }
}
