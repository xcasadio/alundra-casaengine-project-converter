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

    /// <summary>The global effect of the charge: the hold spark and the 16 effects of the burst (<c>0x8003BDD8(0, 0x10, 0, x, y, z + 0x100000)</c>, bank 0, effect 16: 7 frames, 14 ticks, ends with <c>Destroy</c>).</summary>
    public const int ChargeEffect = 0x10;

    /// <summary>The sound of the hold spark when <c>+0x1C &amp; 7</c> is 0 (<c>0x8002F634</c>); the other ticks of the cadence play <see cref="ChargeSparkOddSfx"/>.</summary>
    public const int ChargeSparkEvenSfx = 0x1AD;

    /// <summary>The sound of the hold spark when <c>+0x1C &amp; 7</c> is not 0 (<c>0x8002F638</c>).</summary>
    public const int ChargeSparkOddSfx = 0x1AC;

    /// <summary>The number of effects of the burst of a released charge (<c>0x8002EC48</c>: the loop counts to 16).</summary>
    public const int ChargeBurstCount = 16;

    /// <summary>The 32-direction table <c>0x80023654</c> (halfwords): the x of the burst ring and, read 24 entries on, the x of the force; 768 times the cosine.</summary>
    private static readonly short[] BurstTableX =
    {
        0, -150, -294, -422, -543, -639, -710, -753, -768, -753, -710, -639, -543, -422, -294, -150,
        0, 150, 294, 422, 543, 639, 710, 753, 768, 753, 710, 639, 543, 422, 294, 150,
    };

    /// <summary>The 32-direction table <c>0x80023694</c> (halfwords): the y of the burst ring and, read 24 entries on, the y of the force; 512 times the sine.</summary>
    private static readonly short[] BurstTableY =
    {
        512, 502, 473, 426, 362, 284, 196, 100, 0, -100, -196, -284, -362, -426, -473, -502,
        -512, -502, -473, -426, -362, -284, -196, -100, 0, 100, 196, 284, 362, 426, 473, 502,
    };

    /// <summary>
    /// E20.f1a test seam, per thread: told each effect of the charge (the hold spark, the effects of the burst) once it is created and its fields are written, before the update of its tick (the stage applies the queued
    /// operation, then updates the reservoir): the place to read the identifier, the position and the forces the binary's <c>0x8003BDD8</c> and the code after it leave, the oracle's "fields at the end of the logic".
    /// </summary>
    [System.ThreadStatic]
    internal static System.Action<AlundraEffectSlot>? CreatedForTests;

    /// <summary>
    /// The hold spark of a full charge (<c>UpdatePlayerWeaponEffect</c>, <c>0x8002F5EC-0x8002F71C</c>), at the tick where the charge counter is 60 and <c>+0x1C &amp; 3</c> is 0: a global effect 16 at the hero
    /// (<paramref name="heroX"/>, <paramref name="heroY"/>, <paramref name="heroZ"/> in the binary's frame of Z, so the DLL's <c>PosZ + 1</c>, ADR-0026) with z + 0x100000. ONLY IF it was created: the sound
    /// (<see cref="ChargeSparkEvenSfx"/> when <paramref name="frameCounter"/> &amp; 7 is 0, else <see cref="ChargeSparkOddSfx"/>), then two numbers <c>r1</c>, <c>r2</c> from <see cref="AlundraRandom"/> (the state is left on
    /// <c>r2</c>), <c>jx = hi(r1 * 0x60001) - 0x30000</c>, <c>jy = hi(r2 * 0x40001) - 0x20000</c> (unsigned products), x += 20 jx, y += 20 jy, forces = the hero's forces minus (jx, jy). A full reservoir creates
    /// nothing: no sound, no number drawn. Queued by the hero on the stage, so the sound and the draws happen when the stage applies the operation, after the sounds of the tick (ADR-0048 deviation 2, plan section 1.9).
    /// </summary>
    public static void CreateChargeSpark(AlundraEffectReservoir reservoir, int heroX, int heroY, int heroZ, int heroForceX, int heroForceY, int frameCounter, IAlundraSoundPlayer? soundPlayer)
    {
        unchecked
        {
            var spark = reservoir.Create(false, ChargeEffect, 0, heroX, heroY, heroZ + 0x100000);
            if (spark == null)
            {
                return;
            }

            soundPlayer?.PlaySfx((frameCounter & 7) == 0 ? ChargeSparkEvenSfx : ChargeSparkOddSfx);
            var r1 = (uint)AlundraRandom.Next();
            var r2 = (uint)AlundraRandom.Next();
            var jitterX = (int)(((ulong)r1 * 0x60001) >> 32) - 0x30000;
            var jitterY = (int)(((ulong)r2 * 0x40001) >> 32) - 0x20000;
            spark.X += 20 * jitterX;
            spark.Y += 20 * jitterY;
            spark.ForceX = heroForceX - jitterX;
            spark.ForceY = heroForceY - jitterY;
            CreatedForTests?.Invoke(spark);
        }
    }

    /// <summary>
    /// The burst of a released charge (<c>PlayerTryAttack</c>, <c>0x8002EB6C-0x8002EC48</c>): 16 global effects 16 at the hero (<paramref name="heroX"/>, <paramref name="heroY"/>, <paramref name="heroZ"/> in the binary's
    /// frame of Z) with z + 0x100000, in this order, no number drawn. Effect <c>i</c>, if created, is moved by (<c>BurstTableX[2i]</c>, <c>BurstTableY[2i]</c>) times 3072 and given the forces
    /// (<c>BurstTableX[k]</c>, <c>BurstTableY[k]</c>) times 448, <c>k = (2i + 24) &amp; 31</c> (the ring is 96 x 64 pixels and the effects fly outwards). One that was not created (a full reservoir) skips its own writes
    /// only (<c>0x8002EBBC</c>), the others are not shifted.
    /// </summary>
    public static void CreateChargeBurst(AlundraEffectReservoir reservoir, int heroX, int heroY, int heroZ)
    {
        unchecked
        {
            for (var i = 0; i < ChargeBurstCount; i++)
            {
                var slot = reservoir.Create(false, ChargeEffect, 0, heroX, heroY, heroZ + 0x100000);
                if (slot == null)
                {
                    continue;
                }

                var force = (2 * i + 24) & 31;
                slot.X += BurstTableX[2 * i] * 3072;
                slot.Y += BurstTableY[2 * i] * 3072;
                slot.ForceX = BurstTableX[force] * 448;
                slot.ForceY = BurstTableY[force] * 448;
                CreatedForTests?.Invoke(slot);
            }
        }
    }

    /// <summary>
    /// E20.d1 T2: the effect of the bounce of the run (<c>0x80032018-0x80032068</c>, state 0x03 curtailed by the mover): one global effect 9 at the hero (<paramref name="heroX"/>, <paramref name="heroY"/>,
    /// <paramref name="heroZ"/> in the binary's frame of Z, so the DLL's <c>PosZ + 1</c>, ADR-0026) with z + 0x100000 (16 pixels up): <c>0x8003BDD8(0, 9, 0, x, y, z + 0x100000)</c>. Nothing else: no force written by the caller and
    /// no number drawn (the effect's own program runs on the stage).
    /// </summary>
    public static void CreateSprintBounce(AlundraEffectReservoir reservoir, int heroX, int heroY, int heroZ)
    {
        var slot = reservoir.Create(false, HitSparkEffect, 0, heroX, heroY, unchecked(heroZ + 0x100000));
        if (slot != null)
        {
            CreatedForTests?.Invoke(slot);
        }
    }

    /// <summary>
    /// E14.a1b (D-E14-9, ADR-0060): the break effect <c>DestroyEntity</c> (<c>0x8003A59C</c>) creates for a non-zero effect <paramref name="fx"/> (<c>0x8003A5F8-0x8003A618</c>): <c>0x8003BE74(0, fx, 0, e ; 1, 0, 0, 0)</c>, a global
    /// effect of animation 0 attached to <paramref name="anchor"/> (mode 1), starting at its position (X, Y, and the binary's Z, <see cref="IAlundraEffectAnchor.PosZ"/>), its depth key one above the anchor's, no offset, no sound and
    /// no number drawn. The lowest free slot, nothing when the reservoir is full (the destruction goes on all the same). The anchor is the snapshot of <see cref="AlundraFrozenEffectAnchor"/>, taken by the caller at the destruction.
    /// </summary>
    public static AlundraEffectSlot? CreateBreakEffect(AlundraEffectReservoir reservoir, int fx, IAlundraEffectAnchor anchor)
        => reservoir.CreateAttached(false, fx, 0, anchor, BreakEffectDepthOffset, 0, 0, 0);

    /// <summary>
    /// E20.f3b (D-F3-9): the effect 4 or 5 a hit that is not immune attaches to its target (<c>0x800395E8-0x80039668</c>): <c>0x8003BE74(0, id, 0, target ; 1, 0, 0, 0)</c>, the creator of the break effect with the live target as the anchor,
    /// its depth key one above the target's, no offset, no sound and no number drawn. The effect follows the target afterwards.
    /// </summary>
    public static AlundraEffectSlot? CreateHitAttachedEffect(AlundraEffectReservoir reservoir, int effect, IAlundraEffectAnchor target)
        => reservoir.CreateAttached(false, effect, 0, target, BreakEffectDepthOffset, 0, 0, 0);

    /// <summary>The depth offset of the break effect, the first of the four stack arguments of <c>0x8003BE74</c> that <c>DestroyEntity</c> writes (<c>0x8003A5F8-0x8003A618</c>; the three offsets after it are 0): one above the key of the destroyed entity.</summary>
    public const int BreakEffectDepthOffset = 1;

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
