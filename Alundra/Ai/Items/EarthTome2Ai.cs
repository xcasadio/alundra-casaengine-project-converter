#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4c2 (docs/plan-e20-heros.md section 1.31.4, D-H4C2-1 to D-H4C2-3): the earth tome 2, the item <c>0x2C</c>: the driver <c>0x800354D0</c>, the native handler E13 (<c>0x8007F30C</c>, slot E) for the sprite type 17, the rocks, and the cancellation <c>0x80034BDC</c>. Wired in
/// production by the phase B (T2): the entries <c>0x2C</c> of the three seams of the item in use (<see cref="PlayerManager"/>) and the key of E13 in <see cref="NativeSpriteRegistry.CreateForProduction"/>; the tests of phase A install the seams and the key themselves.
/// <list type="bullet">
/// <item><description>The driver, called every tick of the item (the counter is 0 at its first call, the tick after the start; the wrapper <c>0x8003634C</c> increments it after the driver and frees the item when the driver answers non-zero). Phase 0: the guard of the slope
/// (<see cref="PlayerManager.TomeSlopeGuard"/>), then, for each entry of the table whose tick is the counter, TWO draws of the generator (<c>r1</c> then <c>r2</c>, below 32), BEFORE the birth and whatever its result, then the birth by the interface (<c>SpawnWarpEntity</c>, the sprite 17 of the global page, the
/// direction <c>r2</c>) at <c>hero + (X32[r1], Y32[r1]) * (p + 0xA00)</c> and at the Z of the hero plus half a meter (256 px). A newborn whose terrain is above that height (<see cref="ShouldDestroyAtBirth"/>) is destroyed on the spot (the destroy with one argument, the status 4); any
/// other gets the force of Z -8 px a tick (<c>0xFFF80000</c>). When every entry has a tick at most the counter the phase is 1. Phase 1: the guard, then at the counter <c>0x118</c> the animation <c>0x34</c> (the end of the cast) and the phase 2. Phase 2, with no guard: 0 while the
/// animation is <c>0x34</c>, otherwise 1 (the item is freed). The hero is not moved.</description></item>
/// <item><description>E13, each tick of a rock in the status 3 or 2 (the program of the deactivation): when the animation asked is 1, the rock is destroyed with <c>DestroyEntity(rock, -1)</c> if its animation has ended, and nothing else happens. Otherwise (the impact) the animation asked becomes 1, the flags
/// become <c>(flags | 0x40) &amp; ~0x30</c> and the camera kick is assigned 2 (<see cref="IEntityWorldContext.SetCameraKick"/>). <b>DEVIATION FROM THE BINARY (D-H4C2-3, the author's answer to Q-H4C2-3, D-E20-56)</b>: the impact happens only when the rock is on the ground; a rock that a wall, the edge of
/// the map or an entity stops in the air (the pass of the events puts it at the status 3 while the flag of the wall is raised) has its status put back to 2 and nothing else written, and waits for the ground. The binary plays the animation, the sound and the kick in the air. The guard reads the
/// ground contact, never the flag of the wall, so that any cause of a stop in the air is covered; it is on the first branch alone (a cancelled rock, already in the animation 1, waits for the end of its animation on the ground or not).</description></item>
/// <item><description>The cancellation <c>0x80034BDC</c>, called with the item still in use: each entity of the type 17 at the status 2 goes to the status 3, its blocker (<c>+0x20</c>) is cleared and its animation asked is 1 (E13 then waits for the end of the animation and destroys it); nothing else is written, the hero included.</description></item>
/// </list>
/// </summary>
internal static class EarthTome2Ai
{
    /// <summary>The sprite type of the rocks: 17.</summary>
    internal static readonly int[] SpriteTypes = { 17 };

    /// <summary>The program code of E13 in slot E.</summary>
    internal const int ImpactCode = 13;

    /// <summary>The keys this class registers (the pins of the registry follow them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ImpactCode) };

    private const int EntryCount = 15;
    private const int EntrySize = 4;

    /// <summary>The counter at which the phase 1 ends the cast (<c>0x34</c>).</summary>
    private const int EndCounter = 0x118;

    /// <summary>The animation a rock asks for once it has hit the ground (the impact, with the sound 45).</summary>
    private const uint ImpactAnimation = 1;

    /// <summary>The height above the hero at which the rocks are born: 256 px.</summary>
    private const int BirthHeight = 0x1000000;

    /// <summary>The distance added to every entry's <c>p</c> (<c>0xA00</c>).</summary>
    private const int ExtraDistance = 0xA00;

    /// <summary>The force of Z of a rock that falls: -8 px a tick.</summary>
    private const int FallForce = unchecked((int)0xFFF80000);

    /// <summary>The value the impact assigns to the camera kick (<c>[0x800DD874] = 2</c>).</summary>
    private const int KickScroll = 2;

    /// <summary>The 64 bytes of the table <c>0x80023504</c> of <c>ALUN_CD.EXE</c> (France): 15 entries of 4 bytes (u16 tick, s16 p) and the sentinel entry (<c>0xFFFF</c>, 0).</summary>
    internal static readonly byte[] TableBytes =
    {
        0x10, 0x00, 0x00, 0x02,
        0x20, 0x00, 0x00, 0x04,
        0x30, 0x00, 0x00, 0x06,
        0x40, 0x00, 0x00, 0x08,
        0x50, 0x00, 0x00, 0x0A,
        0x60, 0x00, 0x00, 0x0C,
        0x70, 0x00, 0x00, 0x0E,
        0x80, 0x00, 0x00, 0x10,
        0x90, 0x00, 0x00, 0x12,
        0xA0, 0x00, 0x00, 0x14,
        0xB0, 0x00, 0x00, 0x16,
        0xC0, 0x00, 0x00, 0x18,
        0xE0, 0x00, 0x00, 0x1A,
        0xE0, 0x00, 0x00, 0x1C,
        0xF0, 0x00, 0x00, 0x1E,
        0xFF, 0xFF, 0x00, 0x00,
    };

    /// <summary>The table decoded: (tick, p).</summary>
    internal static readonly IReadOnlyList<(int Tick, int P)> Table = Decode();

    private static (int Tick, int P)[] Decode()
    {
        var entries = new (int, int)[EntryCount];
        for (var i = 0; i < EntryCount; i++)
        {
            var at = i * EntrySize;
            entries[i] = (BitConverter.ToUInt16(TableBytes, at), BitConverter.ToInt16(TableBytes, at + 2));
        }

        return entries;
    }

    /// <summary>
    /// The driver <c>0x800354D0</c> of the item <c>0x2C</c> (0: still in use; 1: finished, or cancelled by the guard); see the class. The counter is <see cref="EntityScriptProxy.ItemUseCounter"/>, the phase <see cref="EntityScriptProxy.ItemUsePhase"/>.
    /// </summary>
    internal static int Driver(EntityScriptProxy hero)
    {
        switch (hero.ItemUsePhase)
        {
            case 0:
                return Spawn(hero);
            case 1:
                return Hold(hero);
            default:
                return hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? 0 : 1;
        }
    }

    private static int Spawn(EntityScriptProxy hero)
    {
        var guard = PlayerManager.TomeSlopeGuard(hero);
        if (guard != 0)
        {
            return guard;
        }

        var counter = hero.ItemUseCounter;
        var world = hero.ScriptHost as IEntityWorldContext;
        var index = 0;
        while (index < EntryCount && counter >= Table[index].Tick)
        {
            var (tick, p) = Table[index];
            if (tick == counter)
            {
                // The two draws come before the birth, whatever its result.
                var place = EnemyHelpers.Below(32);
                var direction = EnemyHelpers.Below(32);
                var distance = p + ExtraDistance;
                var rock = world?.SpawnEntityBySpriteIndex(hero, false, SpriteTypes[0], hero.PosX + AnimationTables.OffsetXList[place] * distance, hero.PosY + AnimationTables.OffsetYList[place] * distance, hero.PosZ + BirthHeight, direction);
                if (rock is not null)
                {
                    if (ShouldDestroyAtBirth(hero.PosZ, rock.ComputeTerrainHeight()))
                    {
                        world!.DestroyEntity(rock);
                    }
                    else
                    {
                        rock.ForceZ = FallForce;
                    }
                }
            }

            index++;
        }

        if (index >= EntryCount)
        {
            hero.ItemUsePhase = 1;
        }

        return 0;
    }

    private static int Hold(EntityScriptProxy hero)
    {
        var guard = PlayerManager.TomeSlopeGuard(hero);
        if (guard != 0)
        {
            return guard;
        }

        if (hero.ItemUseCounter < EndCounter)
        {
            return 0;
        }

        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;
        hero.ItemUsePhase = 2;
        return 0;
    }

    /// <summary>
    /// A rock born below the terrain it would be born in is destroyed on the spot (a plateau above the birth height): the terrain word of the newborn is above the Z of the hero plus half a meter, signed and STRICT (the equality keeps the rock). The Z of the hero is the DLL's, one raw unit
    /// below the binary's (the rest height), hence the <c>+ 1</c>; the sum wraps as the 32-bit register does.
    /// </summary>
    internal static bool ShouldDestroyAtBirth(int heroPosZ, int terrain) => unchecked(heroPosZ + 1 + BirthHeight) < terrain;

    /// <summary>E13 (<c>0x8007F30C</c>), one tick of a rock in the status 2 or 3: see the class. The destruction is the one of the world the rock belongs to (<see cref="EntityScriptProxy.ScriptHost"/>); a rock without a host is not destroyed and gives no kick.</summary>
    internal static void ImpactDeactivate(EntityScriptProxy entity)
    {
        if (entity.TargetAnimationId == ImpactAnimation)
        {
            if (entity.ForceResetAnimationFlag == 1)
            {
                entity.ScriptHost?.DestroyEntity(entity, -1);
            }

            return;
        }

        // D-H4C2-3 (the deviation from the binary): in the air, the rock waits for the ground. The pass of the events of the next tick puts it back at the status 3 while the flag of the wall is raised; this puts it back at 2.
        if (entity.IsOnGround == 0)
        {
            entity.Status = EntityStatus.Normal;
            return;
        }

        entity.TargetAnimationId = ImpactAnimation;
        entity.Flags = (entity.Flags | 0x40u) & ~0x30u;
        entity.ResyncControllerFromFlags();
        (entity.ScriptHost as IEntityWorldContext)?.SetCameraKick(KickScroll);
    }

    /// <summary>The cancellation <c>0x80034BDC</c> of the item <c>0x2C</c>: see the class (the item is freed by <see cref="PlayerManager.CancelItemMode"/>).</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
        if (hero.ScriptHost is not IEntityWorldContext world)
        {
            return;
        }

        foreach (var entity in world.SpawnedEntities)
        {
            if (entity.SpriteType == SpriteTypes[0] && entity.Status == EntityStatus.Normal)
            {
                entity.Status = EntityStatus.Deactivated;
                entity.BlockedByEntity = null;
                entity.TargetAnimationId = ImpactAnimation;
            }
        }
    }

    /// <summary>Registers E13 for the type 17 alone; the disposable takes the key off (for the tests that register it themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, ImpactCode, SpriteTypes, (entity, context, state) => ImpactDeactivate(entity));
}
