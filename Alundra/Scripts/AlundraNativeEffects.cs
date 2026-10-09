#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// The effects the binary's native code creates on the effect reservoir (E19.g G3d, docs/plan-e19-opcodes.md section 1.2o.6, parent ADR-0048). Pure functions of the reservoir and of a
/// position, so that the other native callers (the chests and the pickups, E14) reuse them: nothing here reads the world.
/// </summary>
public static class AlundraNativeEffects
{
    /// <summary>The sound effect of the life vessel (<c>PlaySoundEffect(0x31)</c>, <c>0x800333AC</c>).</summary>
    public const int LifeVesselSfx = 0x31;

    /// <summary>The global effect of the vessel (bank 0, sprite 14, animation 0): 18 frames, 27 ticks, ends with <c>Destroy</c>.</summary>
    private const int LifeVesselEffect = 14;

    /// <summary>The ring: entries 0, 4, 8, ... of the 32-direction tables of the binary (<c>0x80023654 + 8j</c> and <c>0x80023694 + 8j</c>), a 96 x 64 pixel ellipse.</summary>
    private static readonly int[] RingOffsetX = { 0, -543, -768, -543, 0, 543, 768, 543 };

    private static readonly int[] RingOffsetY = { 512, 362, 0, -362, -512, -362, 0, 362 };

    /// <summary>
    /// The effects of <c>IncreaseHpMaxAndCreateEffect</c> (<c>0x800333AC</c>) after its life and its sound: eight ring effects, then four sparks, all of the global effect 14 at the hero
    /// (<paramref name="heroX"/>, <paramref name="heroY"/>, <paramref name="heroZ"/>, 16.16) with z + 0x100000. A created ring slot is moved 16 pixels out on the ellipse and pulled back by its forces
    /// (it converges to the hero on the 16th update); a created spark draws two numbers from <see cref="AlundraRandom"/> for its horizontal forces and rises with a z force of 0x40000. A full reservoir
    /// creates fewer slots and draws fewer numbers (no number for a slot that was not created). Everything wraps in 32 bits.
    /// </summary>
    public static void CreateLifeVesselEffects(AlundraEffectReservoir reservoir, int heroX, int heroY, int heroZ)
    {
        unchecked
        {
            var z = heroZ + 0x100000;

            for (var j = 0; j < RingOffsetX.Length; j++)
            {
                var ring = reservoir.Create(false, LifeVesselEffect, 0, heroX, heroY, z);
                if (ring != null)
                {
                    ring.X += RingOffsetX[j] << 13;
                    ring.Y += RingOffsetY[j] << 13;
                    ring.ForceX = -(RingOffsetX[j] << 9);
                    ring.ForceY = -(RingOffsetY[j] << 9);
                    ring.ForceZ = 0;
                }
            }

            for (var k = 0; k < 4; k++)
            {
                var spark = reservoir.Create(false, LifeVesselEffect, 0, heroX, heroY, z);
                if (spark != null)
                {
                    var r1 = (uint)AlundraRandom.Next();
                    var r2 = (uint)AlundraRandom.Next();
                    spark.ForceX = (int)(((ulong)r1 * 0x20001) >> 32) - 0x10000;
                    spark.ForceY = (int)(((ulong)r2 * 0x30001) >> 32) - 0x18000;
                    spark.ForceZ = 0x40000;
                }
            }
        }
    }

    /// <summary>The global effect of a hit: the spark (<c>0x8003BDD8(0, 9, 0, x, y, z)</c>, bank 0, effect 9).</summary>
    private const int HitSparkEffect = 9;

    /// <summary>The number of sparks of one hit (<c>0x800391A8</c>, the loop of <c>0x80039188</c> counts down from 3).</summary>
    public const int HitSparkCount = 3;

    /// <summary>
    /// The sparks of a hit (<c>0x80039188</c>, called by the hit pass <c>0x80039300</c> at <c>0x80039740</c>): three global effects 9 at the centre of the overlap (<paramref name="centreX"/>, <paramref name="centreY"/>,
    /// <paramref name="centreZ"/>, 16.16, no offset). For each one that was created, three numbers are drawn from <see cref="AlundraRandom"/> in a row (<c>x' = x * 0x7D2B89DD + 0xE06A02E7</c>) and give its three forces,
    /// all with the same formula (<c>high word of r * 0x20001, minus 0x10000</c>, <c>0x80039290</c>, <c>0x800392A4</c>, <c>0x800392BC</c>): unlike the vessel's sparks, the three axes use <c>0x20001</c> and there is no
    /// vertical lift. A full reservoir creates fewer sparks and draws fewer numbers (none for a slot that was not created).
    /// </summary>
    public static void CreateHitSparks(AlundraEffectReservoir reservoir, int centreX, int centreY, int centreZ)
    {
        unchecked
        {
            for (var i = 0; i < HitSparkCount; i++)
            {
                var spark = reservoir.Create(false, HitSparkEffect, 0, centreX, centreY, centreZ);
                if (spark != null)
                {
                    var r1 = (uint)AlundraRandom.Next();
                    var r2 = (uint)AlundraRandom.Next();
                    var r3 = (uint)AlundraRandom.Next();
                    spark.ForceX = (int)(((ulong)r1 * 0x20001) >> 32) - 0x10000;
                    spark.ForceY = (int)(((ulong)r2 * 0x20001) >> 32) - 0x10000;
                    spark.ForceZ = (int)(((ulong)r3 * 0x20001) >> 32) - 0x10000;
                }
            }
        }
    }
}
