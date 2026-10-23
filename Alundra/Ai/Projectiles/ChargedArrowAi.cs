#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Rendering.Effects;

namespace Alundra.Ai.Projectiles;

/// <summary>
/// E20.f4 (docs/plan-e20-heros.md section 1.20.1, D-F4-1 to D-F4-6): the native handler C94 (<c>0x8007C0D8</c>, slot C, code 94 of the master table <c>0x80098F4C</c>) of the charged arrow of William, the sprite type 7 of the global page. At each tick of its life C94
/// calls <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>, the cut of the grass, <see cref="Alundra.Cells.TileEffect"/>, E20.e) first, then, when the counter <c>+0x1C</c> is a multiple of four and the arrow is in the status Normal, leaves a trail: the global effect
/// <see cref="NativeEffects.ChargedArrowTrailEffect"/> of the animation <c>+0x98</c> (the index of the direction of the arrow), at the position the arrow had at the end of the previous tick with the Z of the binary plus 8 pixels, given half the opposite of the arrow's forces
/// (<see cref="NativeEffects.CreateChargedArrowTrail"/>). It draws no number. The death of the arrow (E1, <c>0x8007ED10</c>: <c>DestroyEntity(e, -1)</c>, so the break effect 8 of the header) is handled in line by the runner (<see cref="EventProgramRunner.IsInlineDeactivateCode"/>, D-F4-4),
/// and the program A0 of the type only writes <c>AIValues[0]</c>, which no code of the arrow reads: it is not registered (D-F3-7).
/// </summary>
internal static class ChargedArrowAi
{
    /// <summary>The sprite type of the charged arrow.</summary>
    internal static readonly int[] SpriteTypes = { 7 };

    /// <summary>The program code of C94 in slot C.</summary>
    internal const int TickCode = 94;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, TickCode) };

    /// <summary>The table <c>0x80023644</c> read backwards: the direction words the shot passes (<see cref="Alundra.Hero.PlayerManager.SpawnWeaponProjectile"/>) in the order of the direction index (down 0, up 1, left 2, right 3), which is <see cref="EntityScriptProxy.BoxDirection"/> and <see cref="EntityScriptProxy.AnimationDirection"/>.</summary>
    private static readonly uint[] DirectionWords = { 0, 0x10, 8, 0x18 };

    /// <summary>Test seam, per thread: when non-null, every call of the named <see cref="CheckAndTriggerTileEffect"/> appends the entity it was called for (the motif of <see cref="WaveSwordAi.StubCallsForTests"/>).</summary>
    [ThreadStatic]
    internal static List<EntityScriptProxy>? StubCallsForTests;

    /// <summary>Registers C94 for the type 7.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity));

    /// <summary>C94 (<c>0x8007C0D8</c>), one tick of the charged arrow: the cut of the grass, then the trail.</summary>
    internal static void Tick(EntityScriptProxy entity)
    {
        CheckAndTriggerTileEffect(entity);
        CreateTrail(entity);
    }

    /// <summary>
    /// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) for the charged arrow, called at EVERY call of C94 (<c>0x8007C0E4</c>): the journal of the call (<see cref="StubCallsForTests"/>, kept from the time it was a stub), then the port (E20.e, docs/plan-e20-heros.md section 1.27,
    /// <see cref="Alundra.Cells.TileEffect.Trigger"/>): the bushes under the box of the arrow are cut, with the effect, the loot and the sound of each cut. The same call as <see cref="WaveSwordAi.CheckAndTriggerTileEffect"/>.
    /// </summary>
    internal static void CheckAndTriggerTileEffect(EntityScriptProxy entity)
    {
        StubCallsForTests?.Add(entity);
        Alundra.Cells.TileEffect.Trigger(entity);
    }

    /// <summary>
    /// The trail of C94 (<c>0x8007C0EC-0x8007C15C</c>): when <c>(+0x1C &amp; 3) == 0</c> and the arrow is in the status Normal (the program C only runs there), one global effect with the index of the direction as its animation, at (X, Y, Z + <c>0x80000</c>) of the arrow, the Z being the binary's
    /// (<c>PosZ + 1</c>, ADR-0026); the values are captured now and the effect is created when the stage applies its queue (<see cref="NativeEffects.CreateChargedArrowTrail"/>). No number is drawn. Without a world context nothing is made.
    /// </summary>
    private static void CreateTrail(EntityScriptProxy entity)
    {
        if (entity.Status != EntityStatus.Normal || (entity.FrameCounter & 3) != 0 || entity.ScriptHost is not IEntityWorldContext world)
        {
            return;
        }

        var direction = DirectionIndex(entity.TargetDirection);
        var (x, y, z) = (entity.PosX, entity.PosY, unchecked(entity.PosZ + 1));
        var (forceX, forceY) = (entity.ForceX, entity.ForceY);
        world.EffectStage?.Emit(reservoir => NativeEffects.CreateChargedArrowTrail(reservoir, direction, x, y, z, forceX, forceY));
    }

    /// <summary>
    /// The index of the direction of the arrow: the table <c>0x80023644</c> read backwards (down 0, up 1, left 2, right 3), the value of <c>+0x98</c> that <c>UpdateAnimation</c> derives from the same word; the probe P-F4-1 (docs/plan-e20-annexe/e20-f4f6/f4/results.md) holds it equal to
    /// <see cref="EntityScriptProxy.BoxDirection"/> at the four directions. The shot passes only the four words of the table, so another word is not reachable; it reads as down.
    /// </summary>
    internal static int DirectionIndex(uint targetDirection)
    {
        var index = Array.IndexOf(DirectionWords, targetDirection);
        return index < 0 ? 0 : index;
    }
}
