#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4c1 phase A (docs/plan-e20-heros.md section 1.31.3, D-H4C1-1): the earth tome 1, the item <c>0x2B</c>: the driver <c>0x80035320</c> and the native handler E3 (<c>0x8007EDA0</c>, slot E) for the sprite type 16, the rocks. INERT in production: the entry of the
/// tome in the three seams of the item in use (<see cref="PlayerManager"/>) is the stub of h0a and the key of E3 is not registered by <see cref="NativeSpriteRegistry.CreateForProduction"/> until the phase B (D-H4C1-2); the tests install the seams and the key themselves.
/// <list type="bullet">
/// <item><description>The driver, called every tick of the item (the counter is 0 at its first call, the tick after the start; the wrapper <c>0x8003634C</c> increments it after the driver and frees the item when the driver answers non-zero). Phase 0: the guard of the slope
/// (<see cref="PlayerManager.TomeSlopeGuard"/>), then nothing before the counter 20; then, for each entry of the table whose tick is the counter, a rock is born at the hero by the interface (<c>SpawnWarpEntity</c>, the sprite 16 of the global page, the direction
/// <c>(hero direction + delta) &amp; 31</c>) and, if the birth gave an entity, its Z force is the speed of the entry shifted by 8; at the counter 95 (every tick of the table has passed) the phase is 1. Phase 1: the guard, then at the counter 100 the animation <c>0x34</c> (the end of the
/// cast) and the phase 2. Phase 2, with no guard: 0 while the animation is <c>0x34</c>, otherwise 1 (the item is freed). The hero is not moved.</description></item>
/// <item><description>E3, each tick of a rock in the status 3 (<c>DeactivateOnImpact</c>): the rock is destroyed with <c>DestroyEntity(rock, -1)</c> when the animation asked is 1 and its end is 1; otherwise the animation asked becomes 1 and the flags become <c>(flags | 0x40) &amp; ~0x30</c>.</description></item>
/// <item><description>The cancellation <c>0x800351A0</c> is neutral: the rocks already born live and die alone, the item is freed by <see cref="PlayerManager.CancelItemMode"/>.</description></item>
/// </list>
/// </summary>
internal static class EarthTome1Ai
{
    /// <summary>The sprite type of the rocks: 16.</summary>
    internal static readonly int[] SpriteTypes = { 16 };

    /// <summary>The program code of E3 in slot E.</summary>
    internal const int ImpactCode = 3;

    /// <summary>The keys this class registers (the pins of the registry follow them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ImpactCode) };

    private const int EntryCount = 32;
    private const int EntrySize = 6;

    /// <summary>The counter at which the phase 1 ends the cast (<c>0x34</c>).</summary>
    private const int EndCounter = 100;

    /// <summary>The animation a rock asks for once it has hit the ground (the impact, with the sound 46).</summary>
    private const uint ImpactAnimation = 1;

    /// <summary>The 194 bytes of the table <c>0x8002343C</c> of <c>ALUN_CD.EXE</c> (France): 32 entries of 6 bytes (u16 tick, s16 speed of Z, s16 delta of direction) and the sentinel word <c>0xFFFF</c>.</summary>
    internal static readonly byte[] TableBytes =
    {
        0x14, 0x00, 0x00, 0x04, 0x03, 0x00,
        0x14, 0x00, 0x00, 0x04, 0xFA, 0xFF,
        0x19, 0x00, 0x00, 0x04, 0x00, 0x00,
        0x19, 0x00, 0x00, 0x04, 0x06, 0x00,
        0x1E, 0x00, 0x00, 0x04, 0x03, 0x00,
        0x1E, 0x00, 0x00, 0x04, 0x02, 0x00,
        0x23, 0x00, 0x00, 0x04, 0x08, 0x00,
        0x23, 0x00, 0x00, 0x04, 0x0E, 0x00,
        0x28, 0x00, 0x00, 0x04, 0x03, 0x00,
        0x28, 0x00, 0x00, 0x04, 0x0A, 0x00,
        0x2D, 0x00, 0x00, 0x04, 0x10, 0x00,
        0x2D, 0x00, 0x00, 0x04, 0x16, 0x00,
        0x32, 0x00, 0x00, 0x04, 0x03, 0x00,
        0x32, 0x00, 0x00, 0x04, 0x12, 0x00,
        0x37, 0x00, 0x00, 0x04, 0x18, 0x00,
        0x37, 0x00, 0x00, 0x04, 0x1E, 0x00,
        0x3C, 0x00, 0x00, 0x05, 0x03, 0x00,
        0x3C, 0x00, 0x00, 0x05, 0xFA, 0xFF,
        0x41, 0x00, 0x00, 0x05, 0x00, 0x00,
        0x41, 0x00, 0x00, 0x05, 0x06, 0x00,
        0x46, 0x00, 0x00, 0x05, 0x03, 0x00,
        0x46, 0x00, 0x00, 0x05, 0x02, 0x00,
        0x4B, 0x00, 0x00, 0x05, 0x08, 0x00,
        0x4B, 0x00, 0x00, 0x05, 0x0E, 0x00,
        0x50, 0x00, 0x00, 0x05, 0x03, 0x00,
        0x50, 0x00, 0x00, 0x05, 0x0A, 0x00,
        0x55, 0x00, 0x00, 0x05, 0x10, 0x00,
        0x55, 0x00, 0x00, 0x05, 0x16, 0x00,
        0x5A, 0x00, 0x00, 0x05, 0x03, 0x00,
        0x5A, 0x00, 0x00, 0x05, 0x12, 0x00,
        0x5F, 0x00, 0x00, 0x05, 0x18, 0x00,
        0x5F, 0x00, 0x00, 0x05, 0x1E, 0x00,
        0xFF, 0xFF,
    };

    /// <summary>The table decoded: (tick, speed of Z, delta of direction).</summary>
    internal static readonly IReadOnlyList<(int Tick, int Speed, int Delta)> Table = Decode();

    private static (int Tick, int Speed, int Delta)[] Decode()
    {
        var entries = new (int, int, int)[EntryCount];
        for (var i = 0; i < EntryCount; i++)
        {
            var at = i * EntrySize;
            entries[i] = (BitConverter.ToUInt16(TableBytes, at), BitConverter.ToInt16(TableBytes, at + 2), BitConverter.ToInt16(TableBytes, at + 4));
        }

        return entries;
    }

    /// <summary>
    /// The driver <c>0x80035320</c> of the item <c>0x2B</c> (0: still in use; 1: finished, or cancelled by the guard); see the class. The counter is <see cref="EntityScriptProxy.ItemUseCounter"/>, the phase <see cref="EntityScriptProxy.ItemUsePhase"/>.
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
        if (counter < Table[0].Tick)
        {
            return 0;
        }

        var world = hero.ScriptHost as IEntityWorldContext;
        var index = 0;
        while (index < EntryCount && counter >= Table[index].Tick)
        {
            var (tick, speed, delta) = Table[index];
            if (tick == counter)
            {
                var rock = world?.SpawnEntityBySpriteIndex(hero, false, SpriteTypes[0], hero.PosX, hero.PosY, hero.PosZ, (int)(unchecked(hero.TargetDirection + (uint)delta) & 31));
                if (rock is not null)
                {
                    rock.ForceZ = speed << 8;
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

    /// <summary>E3 (<c>0x8007EDA0</c>), one tick of a rock in the status 3: see the class. The destruction is the one of the world the rock belongs to (<see cref="EntityScriptProxy.ScriptHost"/>); a rock without a host is not destroyed.</summary>
    internal static void ImpactDeactivate(EntityScriptProxy entity)
    {
        if (entity.TargetAnimationId == ImpactAnimation && entity.ForceResetAnimationFlag == 1)
        {
            entity.ScriptHost?.DestroyEntity(entity, -1);
            return;
        }

        entity.TargetAnimationId = ImpactAnimation;
        entity.Flags = (entity.Flags | 0x40u) & ~0x30u;
        entity.ResyncControllerFromFlags();
    }

    /// <summary>The cancellation <c>0x800351A0</c> of the item <c>0x2B</c>: neutral, the rocks already born stay (the item is freed by <see cref="PlayerManager.CancelItemMode"/>).</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
    }

    /// <summary>Registers E3 for the type 16 alone; the disposable takes the key off (for the tests that register it themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, ImpactCode, SpriteTypes, (entity, context, state) => ImpactDeactivate(entity));
}
