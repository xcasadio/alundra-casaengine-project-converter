#nullable enable
using System;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Physics;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1): the scenes and the constants the world tests of the carrying share, on <see cref="CarryRig"/>: the hero at the oracle's (109, 100) with the crate that stands on his head put in his hands (the carrying stand
/// 0x0C and the offset (0, 0, 32), so that the walks, the jumps, the lock and the water do not depend on the series of the lift, which is (w2)'s: the lift from 21 px east at the tick L, the stand at L + 36).
/// </summary>
internal static class CarryScenes
{
    /// <summary>One pixel in raw units (16.16).</summary>
    public const int Px = CarryRig.Px;

    public const uint Right = PadState.Right;

    public const uint Cross = PadState.Cross;

    /// <summary>2.4375 px: the pace of the walk of the sheets 1, 7 and 47 (speed 208).</summary>
    public const int StepPerTick = 159744;

    /// <summary>The jump of the sheets 2, 6 and 46 (impulse 1280, gravity 128): the height of the hero at the ticks T to T + 19 (T + 20 is the landing, 0).</summary>
    public static readonly int[] JumpHeights =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
        1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680,
    };

    /// <summary>The offset of <paramref name="crate"/> from <paramref name="hero"/>, in whole pixels.</summary>
    public static (int X, int Y, int Z) Offset(EntityScriptProxy hero, EntityScriptProxy crate)
        => ((crate.PosX - hero.PosX) / Px, (crate.PosY - hero.PosY) / Px, (crate.PosZ - hero.PosZ) / Px);

    /// <summary>
    /// The montage of the walks and the jumps: the hero at (<paramref name="x"/>, <paramref name="y"/>) with the pad word of each tick given by <paramref name="hold"/>, the movement obstacle probe when <paramref name="probe"/>, and - when
    /// <paramref name="carry"/> - a crate that stands on his head (<paramref name="z"/> is his height; see <see cref="StandWithTheObject"/>).
    /// </summary>
    public static CarryRig Carrying(int ticksPerFrame, Func<int, uint> hold, bool probe = false, bool carry = true, float x = 109f, float y = 100f, int attackCount = 1, int z = 0)
    {
        var rig = CarryRig.Build(ticksPerFrame, hold, probeFactory: probe ? host => new MovementObstacleProbe(host) : null, heroX: x, heroY: y, heroZ: z);
        if (carry)
        {
            rig.AddCrate((int)x, (int)y, z + 32, attackCount: attackCount); // on the head of the hero.
        }

        return rig;
    }

    /// <summary>
    /// Settles and puts the crate that stands on the head of the hero in his hands (the tick L, <see cref="CarryRig.LiftNow"/> from the head: the carrying stand 0x0C at once, the offset (0, 0, 32), nothing to draw in), then plays 3 more ticks. Without a crate it only
    /// plays the same number of ticks. The lift series from the ground is (w2)'s.
    /// </summary>
    public static void StandWithTheObject(CarryRig rig)
    {
        rig.Run(3);
        if (rig.Crate != null)
        {
            rig.LiftNow(fromTheHead: true);
            rig.Run(3);
            Assert.Equal(12u, rig.Hero.TargetAnimationId);
            Assert.Equal((0, 0, 32), Offset(rig.Hero, rig.Crate));
        }
        else
        {
            rig.Run(7);
        }
    }
}
