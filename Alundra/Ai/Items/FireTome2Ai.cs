#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Cameras;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4d1 (docs/plan-e20-heros.md section 1.31.5, D-H4D1-1 to D-H4D1-4): the fire tome 2, the item <c>0x30</c>: the pilot <c>0x80035C64</c>, the native handler E28 (<c>0x8007FB58</c>, slot E) for the sprite type 21, the column of fire, and the cancellation <c>0x80034E08</c>. Wired in
/// production by the phase B (T2): the entries <c>0x30</c> of the three seams of the item in use (<see cref="PlayerManager"/>) and the key of E28 in <see cref="NativeSpriteRegistry.CreateForProduction"/>; the tests of phase A install the seams and the key themselves.
/// <list type="bullet">
/// <item><description>The pilot, called every tick of the item (the counter is 0 at its first call, the tick after the start; the wrapper <c>0x8003634C</c> increments it after the pilot and frees the item when the pilot answers non-zero). Phase 0: the guard of the slope
/// (<see cref="PlayerManager.TomeSlopeGuard"/>), then nothing until the animation is <c>0x33</c> (the end of the cast pose, 32 ticks); then the column of fire is born by the interface (<c>SpawnWarpEntity</c>, the sprite 21 of the global page, direction 0) at the hero's X, Y and Z, and the
/// pointer is kept BEFORE the test of nullity (a refused birth keeps null and the answer is 1, the item is freed with nothing captured). The nine words of the shake block are captured to the buffer (<see cref="CameraSway.CaptureToBuffer"/>, before the pilot writes the block), the phase is 1 and
/// the phase 1 runs in the same tick. Phase 1: the guard (non-zero: the buffer is restored, the answer is 1 and the phase does not advance; the guard has already cancelled the item); while the column lives (its slot is not recycled and not taken off the table: <see cref="ColumnChanged"/>)
/// the shake is armed from the counter <c>c</c> the pilot reads (the value of the tick, before the increment): <c>Flag = 1</c>, <c>LimitY = SpeedY = c &gt;&gt; 4</c>, <c>LimitX = SpeedX = c &gt;&gt; 6</c> (arithmetic shifts), the offsets and the reach left as they are, the answer 0; the tick the column is gone, the
/// buffer is restored (the nine words), the animation is <c>0x34</c> (the end of the cast) and the phase 2. Phase 2, with no guard: 0 while the animation is <c>0x34</c>, otherwise 1 (the item is freed). Any other phase answers 0 and writes nothing. No number is drawn, no sound is played, the hero is not moved.</description></item>
/// <item><description>E28, each tick of the column in the status 3 (the program of the deactivation, at the end of its 120 ticks of animation or after the cancellation): eight times a draw of the generator below <c>0x1801</c> (<c>r = draw + 0x320</c>, taken whether the effect can be created or not), the global effect 19 of the animation 2 at
/// <c>(X + TabX[4k] * r, Y + TabY[4k] * r, Z)</c>, <c>k</c> from 0 to 7, the tables being the 32-direction tables read every four entries (<see cref="NativeEffects.CreateFireColumnBlast"/>, queued on the stage like the other native effects); then <c>DestroyEntity(self, -1)</c>, which reads the break effect of the header.</description></item>
/// <item><description>The cancellation <c>0x80034E08</c>, called with the item still in use (only when it is <c>0x30</c>: the dispatcher of the binary): the nine words of the buffer are copied back to the block with no guard, then each entity of the type 21 at the status 2 goes to the status 3 and its blocker (<c>+0x20</c>) is cleared; nothing else
/// is written, the hero included (the item is freed by <see cref="PlayerManager.CancelItemMode"/>).</description></item>
/// </list>
/// </summary>
internal static class FireTome2Ai
{
    /// <summary>The sprite type of the column of fire: 21 (<c>0x15</c>, of the global page).</summary>
    internal static readonly int[] SpriteTypes = { 21 };

    /// <summary>The program code of E28 in slot E.</summary>
    internal const int ColumnEndCode = 28;

    /// <summary>The keys this class registers (the pins of the registry follow them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ColumnEndCode) };

    private const int ColumnType = 21;

    /// <summary>The number of explosions of the end of the column (E28's loop counts to 8).</summary>
    private const int BlastCount = 8;

    /// <summary>The bound of the draw of the radius of an explosion (<c>0x1801</c>); the radius is the draw plus <see cref="BlastRadiusBase"/>.</summary>
    private const int BlastRadiusBound = 0x1801;

    private const int BlastRadiusBase = 0x320;

    /// <summary>
    /// The pilot <c>0x80035C64</c> of the item <c>0x30</c> (0: still in use; 1: finished, or cancelled by the guard, or the birth was refused); see the class. The counter is <see cref="EntityScriptProxy.ItemUseCounter"/>, the phase <see cref="EntityScriptProxy.ItemUsePhase"/>,
    /// the pointer of the column <see cref="EntityScriptProxy.ItemSpawnedEntity"/>.
    /// </summary>
    internal static int Driver(EntityScriptProxy hero)
    {
        switch (hero.ItemUsePhase)
        {
            case 0:
                return Birth(hero);
            case 1:
                return Burn(hero);
            case 2:
                return hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? 0 : 1;
            default:
                return 0;
        }
    }

    private static int Birth(EntityScriptProxy hero)
    {
        var guard = PlayerManager.TomeSlopeGuard(hero);
        if (guard != 0)
        {
            return guard;
        }

        if (hero.TargetAnimationId != PlayerManager.TomeHoldAnimationId)
        {
            return 0;
        }

        var world = hero.ScriptHost as IEntityWorldContext;
        var column = world?.SpawnEntityBySpriteIndex(hero, false, ColumnType, hero.PosX, hero.PosY, hero.PosZ, 0);

        // The pointer is kept before the test of nullity.
        hero.ItemSpawnedEntity = column;
        if (column is null)
        {
            return 1;
        }

        world!.CameraSway?.CaptureToBuffer();
        hero.ItemUsePhase = 1;
        return Burn(hero);
    }

    private static int Burn(EntityScriptProxy hero)
    {
        var sway = (hero.ScriptHost as IEntityWorldContext)?.CameraSway;
        var guard = PlayerManager.TomeSlopeGuard(hero);
        if (guard != 0)
        {
            sway?.RestoreFromBuffer();
            return 1;
        }

        if (!ColumnChanged(hero.ItemSpawnedEntity))
        {
            if (sway is not null)
            {
                var counter = hero.ItemUseCounter;
                sway.Flag = 1;
                sway.LimitY = sway.SpeedY = counter >> 4;
                sway.LimitX = sway.SpeedX = counter >> 6;
            }

            return 0;
        }

        sway?.RestoreFromBuffer();
        hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;
        hero.ItemUsePhase = 2;
        return 0;
    }

    /// <summary>
    /// The identity test of the pilot: the column kept at its birth is no longer the entity of its slot once the slot was recycled (<see cref="EntityStatus.Destroyed"/>, the pass of the recycling frees it the tick after its destruction) or taken off the table (<see cref="EntityScriptProxy.Slot"/> negative).
    /// An entity to destroy that is not recycled yet is still the column, and a missing pointer is no column.
    /// </summary>
    private static bool ColumnChanged(EntityScriptProxy? column) => column is null || column.Status == EntityStatus.Destroyed || column.Slot < 0;

    /// <summary>
    /// E28 (<c>0x8007FB58</c>), one tick of the column in the status 3: see the class. The eight draws are made at once (a full reservoir creates none of the explosions and the draws are made all the same); the explosions are queued on the effect stage of the world in this order, and the
    /// destruction is the one of the world the column belongs to (<see cref="EntityScriptProxy.ScriptHost"/>); a column without a host is not destroyed and gives nothing.
    /// </summary>
    internal static void ColumnEnds(EntityScriptProxy entity)
    {
        var stage = entity.ScriptHost?.EffectStage;
        var z = entity.PosZ + 1; // the binary's frame of Z (ADR-0026).
        for (var k = 0; k < BlastCount; k++)
        {
            var radius = EnemyHelpers.Below(BlastRadiusBound) + BlastRadiusBase;
            var x = unchecked(entity.PosX + AnimationTables.OffsetXList[4 * k] * radius);
            var y = unchecked(entity.PosY + AnimationTables.OffsetYList[4 * k] * radius);
            stage?.Emit(reservoir => NativeEffects.CreateFireColumnBlast(reservoir, x, y, z));
        }

        entity.ScriptHost?.DestroyEntity(entity, -1);
    }

    /// <summary>The cancellation <c>0x80034E08</c> of the item <c>0x30</c>: see the class (the item is freed by <see cref="PlayerManager.CancelItemMode"/>).</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
        var world = hero.ScriptHost as IEntityWorldContext;
        world?.CameraSway?.RestoreFromBuffer();
        if (world is null)
        {
            return;
        }

        foreach (var entity in world.SpawnedEntities)
        {
            if (entity.SpriteType == ColumnType && entity.Status == EntityStatus.Normal)
            {
                entity.Status = EntityStatus.Deactivated;
                entity.BlockedByEntity = null;
            }
        }
    }

    /// <summary>Registers E28 for the type 21 alone; the disposable takes the key off (for the tests that register it themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, ColumnEndCode, SpriteTypes, (entity, context, state) => ColumnEnds(entity));
}
