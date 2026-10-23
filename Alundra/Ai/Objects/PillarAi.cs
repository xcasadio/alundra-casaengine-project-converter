#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g5a (docs/plan-e20-heros.md section 1.24.6, D-G5-1 to D-G5-3): the program C76 (<c>0x8007C024-0x8007C0D4</c>, 43 words, slot C, code 76 of the master table <c>0x80098F4C</c>) of the pushable pillar, the sprite type 430 of the global page, and its helper
/// <c>GetPushablePillarPushDirection</c> (<c>0x8003AC9C</c>). The pillar only decides WHEN and WHERE it is pushed; the motion itself is the generic one of the entities (the animation 1 of the pillar carries the speed, <c>ScriptedMotion</c>):
/// <list type="bullet">
/// <item><description>the hero is blocked by the pillar (<c>hero.+0x130 == pillar</c>, <see cref="EntityScriptProxy.XCollisionEntity"/>): a counter (<c>+0x274</c>, a signed 32-bit word) goes up to <see cref="PushTicks"/>; at that value, at every tick, the sound <see cref="PushSfx"/> is asked,
/// the animation 1 is posed (<c>+0x88</c>) and the direction (<c>+0x8C</c>) is the side of the hero plus half a turn (<see cref="Direction"/>);</description></item>
/// <item><description>otherwise, the stop of the sound is asked if the animation was 1 (<see cref="StopSoundEffect"/>, a named stub: <c>ISoundPlayer</c> has no stop of one sound yet, g5c), then the counter and the animation go back to 0.</description></item>
/// </list>
/// The decompilation renders that stop as a second <c>PlaySoundEffect</c>; the binary calls <c>StopSoundEffect</c> (<c>0x80049634</c>, see the discovery of 2026-10-23).
/// </summary>
internal static class PillarAi
{
    /// <summary>The sprite type of the pushable pillar (<c>430</c> = <c>0x100 + 174</c>).</summary>
    internal const int SpriteType = 430;

    /// <summary>The program code of C76 in slot C.</summary>
    internal const int Code = 76;

    /// <summary>The sound of the push (<c>PlaySoundEffect(0x19)</c>, 25).</summary>
    internal const int PushSfx = 0x19;

    /// <summary>The value of the counter at which the push starts (<c>slti 0x1F</c>: strictly under it the counter goes up).</summary>
    internal const int PushTicks = 31;

    /// <summary>The keys the class registers, for the pins of the production registry (D-G5-1): C76, for the type <see cref="SpriteType"/> only.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, Code) };

    /// <summary>Registers C76 for the type <see cref="SpriteType"/> (430) only: the other sprite types that carry the code 76 in their record are not ported yet.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, Code, new[] { SpriteType }, (entity, context, state) => Tick(entity, context));

    /// <summary>The animation asked while the pillar is pushed (<c>+0x88 = 1</c>): the one that carries the speed.</summary>
    internal const uint PushAnimationId = 1;

    /// <summary>The direction added to the side of the hero: half a turn of the 32 (<c>+ 0x10</c>, then <c>&amp; 0x1F</c>).</summary>
    private const int HalfTurn = 0x10;

    /// <summary>Test seam, per thread (the motif of <see cref="Alundra.Hero.PlayerAnimationEffects.StubCallsForTests"/>): when non-null, every call of the named stub <see cref="StopSoundEffect"/> appends the sound it was asked to stop.</summary>
    [ThreadStatic]
    internal static List<int>? StopSoundStubCallsForTests;

    /// <summary>What one tick of C76 does, as a function of the counter, the contact and the animation asked.</summary>
    /// <param name="Counter">The counter after the tick.</param>
    /// <param name="PlaySound">The sound of the push is asked (<see cref="PushSfx"/>).</param>
    /// <param name="Animate">The animation 1 is posed and the direction is written.</param>
    /// <param name="StopSound">The stop of the sound is asked (<see cref="StopSoundEffect"/>).</param>
    /// <param name="ClearAnimation">The animation asked goes back to 0.</param>
    internal readonly record struct CounterStep(int Counter, bool PlaySound, bool Animate, bool StopSound, bool ClearAnimation);

    /// <summary>The step of C76 (<c>0x8007C024-0x8007C0D4</c>) without its reads and writes: a contact under <see cref="PushTicks"/> counts (signed, one more); at it the push is on and the counter stays; no contact resets the counter and the animation, and stops the sound if the animation was 1.</summary>
    internal static CounterStep Step(int counter, bool contact, uint animation)
    {
        if (contact)
        {
            return counter < PushTicks
                ? new CounterStep(unchecked(counter + 1), false, false, false, false)
                : new CounterStep(counter, true, true, false, false);
        }

        return new CounterStep(0, false, false, animation == PushAnimationId, true);
    }

    /// <summary>
    /// <c>GetPushablePillarPushDirection</c> (<c>0x8003AC9C</c>): the side the hero pushes from, 8 (west), 0x18 (east), 16 (north) or 0 (south). The axis is picked by the offset of the positions of the end of the previous tick
    /// (<see cref="EntityScriptProxy.ModdedPosX"/>, <c>+0x1D8</c>) against the width of the one the offset points away from ((size &lt;&lt; 16) - 1, <see cref="EntityScriptProxy.Width"/>, <c>+0x1F0</c>); with the columns apart, the sense is that of the X positions
    /// (<c>hero.X &lt; pillar.X</c> gives 8), otherwise that of the Y positions (<c>hero.Y &lt; pillar.Y</c> gives 16). All comparisons are signed, the subtraction and the negation wrap.
    /// </summary>
    internal static int Direction(EntityScriptProxy pillar, EntityScriptProxy hero)
    {
        var offset = unchecked(hero.ModdedPosX - pillar.ModdedPosX);
        var horizontal = offset < 0 ? hero.Width < unchecked(-offset) : pillar.Width < offset;
        if (!horizontal)
        {
            return hero.PosY < pillar.PosY ? 0x10 : 0;
        }

        return hero.PosX < pillar.PosX ? 8 : 0x18;
    }

    /// <summary><c>+0x274</c> of the pillar: the counter, a signed word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3 (D-G5-2).</summary>
    internal static int ReadCounter(EntityScriptProxy entity)
        => entity.Bytes[0] | (entity.Bytes[1] << 8) | (entity.Bytes[2] << 16) | (entity.Bytes[3] << 24);

    internal static void WriteCounter(EntityScriptProxy entity, int value)
    {
        entity.Bytes[0] = (byte)value;
        entity.Bytes[1] = (byte)(value >> 8);
        entity.Bytes[2] = (byte)(value >> 16);
        entity.Bytes[3] = (byte)(value >> 24);
    }

    /// <summary>One tick of C76 for <paramref name="pillar"/>: the contact is the hero's <see cref="EntityScriptProxy.XCollisionEntity"/> (no hero: no contact), the rest is <see cref="Step"/> with its reads and writes.</summary>
    internal static void Tick(EntityScriptProxy pillar, IEntityWorldContext context)
    {
        var hero = context.PlayerEntity;
        var contact = hero != null && ReferenceEquals(hero.XCollisionEntity, pillar);
        var step = Step(ReadCounter(pillar), contact, pillar.TargetAnimationId);

        if (step.StopSound)
        {
            StopSoundEffect(PushSfx);
        }

        if (step.Animate)
        {
            context.SoundPlayer?.PlaySfx(PushSfx);
            pillar.TargetAnimationId = PushAnimationId;
            pillar.TargetDirection = unchecked((uint)((Direction(pillar, hero!) + HalfTurn) & 0x1F));
        }
        else
        {
            WriteCounter(pillar, step.Counter);
            if (step.ClearAnimation)
            {
                pillar.TargetAnimationId = 0;
            }
        }
    }

    /// <summary>
    /// <c>StopSoundEffect</c> (<c>0x80049634</c>): a named stub. <see cref="Audio.ISoundPlayer"/> can stop every sound (<c>StopAllSfx</c>) but not one, so the stop at the end of the push is not made (the sound of the push is short); it is journalled
    /// (<see cref="StopSoundStubCallsForTests"/>) for the slice that gives the engine its stop of one sound (g5c, Q-G5-2).
    /// </summary>
    private static void StopSoundEffect(int sfxId) => StopSoundStubCallsForTests?.Add(sfxId);
}
