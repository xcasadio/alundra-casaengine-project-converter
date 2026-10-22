#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Session;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.e0c (docs/plan-e20-heros.md section 1.26): the native handlers of the pickup object - C255 (<c>0x8007C174</c>) and A255 (<c>0x80061BD4</c>) of the binary, phases B and D - and, since T1, the 32-bit words they keep on the
/// entity. The proxy holds those words in pieces (<c>+0x274</c> on the four bytes of <see cref="EntityScriptProxy.Bytes"/>, <c>+0x280</c> and <c>+0x284</c> on four halfwords of <see cref="EntityScriptProxy.AIValues"/>), and its
/// own file is not changed (D-E20-55, D-E0C-13: the 32-bit accessors are the pickup object's alone; the tomes and E14 make theirs). The words are little-endian, as the binary stores them: <c>+0x274</c> is 0 for an object placed on a
/// map, 1 for a dropped one and 2 for the chest (not ported); <c>+0x280</c> is the game flag the pickup raises (the container's flag, or -1 for a placed object); <c>+0x284</c> is the speed of the bounce.
/// <para>Phase B (T2): the registry holds A255 and C255 for the 87 sprite types 31 to 118 but the 72 (the object is <c>type - 0x1E</c>). C255 runs the branch of the DROPPED object (<c>+0x274 == 1</c>): the bounce on the ground, the countdown of
/// the life (600 ticks, the mark of the last 120 at 120), the wait for the end of the rise, the contact with the hero, <c>GiveItem</c>, the flag, the sound and the light destruction. The placed object (<c>+0x274 == 0</c>, phase D) and the chest
/// (<c>+0x274 == 2</c>, E14) are NAMED STUBS: C255 and A255 do nothing for them. A255 does nothing in this phase (the A255 of a dropped object returns at once; the placed object's is phase D).</para>
/// </summary>
internal static class PickupAi
{
    /// <summary>The program code of both handlers: A255 (<c>0x80061BD4</c>, slot A) and C255 (<c>0x8007C174</c>, slot C).</summary>
    internal const int Code = 255;

    /// <summary>The value of <c>+0x274</c> of a dropped object.</summary>
    internal const uint KindDropped = 1;

    /// <summary>The sprite types of the objects of the global page that carry C255: 31 to 118 but 72 (87 types). The object is <c>type - 0x1E</c>. A255 is registered for the same 87 (the chest, type 30, is not: D-E0C-2).</summary>
    internal static readonly int[] SpriteTypes = Enumerable.Range(31, 118 - 31 + 1).Where(type => type != 72).ToArray();

    /// <summary>The (slot, code) keys this class registers: A255 and C255.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramALoad, Code), (ScriptHelper.ProgramCTick, Code) };

    private const int LifeMarkTicks = 0x78;
    private const int LifeMarkValue = 0x78;
    private const int StateRise = 0;
    private const int StatePickup = 1;
    private const int StateAfterTheFanfare = 3;

    /// <summary>Registers A255 and C255 for <see cref="SpriteTypes"/>.</summary>
    internal static void Register(NativeSpriteRegistry registry)
    {
        registry.Register(ScriptHelper.ProgramALoad, Code, SpriteTypes, (entity, context, state) => Load(entity));
        registry.Register(ScriptHelper.ProgramCTick, Code, SpriteTypes, Tick);
    }

    /// <summary>
    /// A255 (<c>0x80061BD4</c>) in phase B: nothing. The binary's A255 returns at once for a dropped object (<c>+0x274 == 1</c>); the placed object's branch (<c>+0x6C &amp;= ~0x80</c>, the state, the flag and the life words) is phase D and the chest's is E14:
    /// both are NAMED STUBS here (D-E0C-2, D-E0C-3).
    /// </summary>
    internal static void Load(EntityScriptProxy entity)
    {
    }

    /// <summary>
    /// C255 (<c>0x8007C174</c>) for a dropped object, one tick (the branch <c>+0x274 != 2</c> of the binary; the state 2, the fanfare of the key objects, is phase D, and a key object is never dropped). The placed object and the chest are named stubs: nothing.
    /// </summary>
    internal static void Tick(EntityScriptProxy entity, IEntityWorldContext world, GameState state)
    {
        if (ReadWord274(entity) != KindDropped)
        {
            return;
        }

        // The bounce (0x8007C1B8-0x8007C240): on the ground with a speed left, the speed is multiplied by 12 / 16 and becomes the force of the rise, unless it does not exceed the gravity of the map << 8 (then it is 0).
        if (entity.IsOnGround != 0 && ReadWord284(entity) != 0)
        {
            var bounce = unchecked((int)(ReadWord284(entity) * 12u)) >> 4;
            WriteWord284(entity, unchecked((uint)bounce));
            if (!((entity.MapGravityRaw << 8) < bounce))
            {
                WriteWord284(entity, 0);
            }

            entity.ForceZ = unchecked((int)ReadWord284(entity));
        }

        // The life (0x8007C240-): -1 and 0 are no rule; otherwise one tick less, the light destruction at 0, the mark of the last 120 ticks at 120.
        var life = entity.DelayOrAngleOrEntityId;
        if (life != -1 && life != 0)
        {
            life--;
            entity.DelayOrAngleOrEntityId = life;
            if (life == 0)
            {
                world.DestroyEntity(entity);
                return;
            }

            if (life == LifeMarkTicks)
            {
                entity.DamagedTickCounter = LifeMarkValue;
            }
        }

        switch (entity.ItemState)
        {
            case StateRise:
                if (entity.ForceZ > 0)
                {
                    return;
                }

                entity.ItemState = StatePickup;
                return;
            case StatePickup:
                Pickup(entity, world, state);
                return;
            case StateAfterTheFanfare:
                if (DialogueIsOpen(world))
                {
                    return;
                }

                world.Unblock(entity);
                world.DestroyEntity(entity);
                return;
        }
    }

    /// <summary>
    /// The state 1 (<c>0x8007C488-0x8007C6EC</c>) of a dropped object: nothing while a dialogue is open; the overlap of the boxes with the hero's on the three axes; then <c>GiveItem</c>, the game flag of the container and, the byte 5 of the table
    /// not being zero (which no dropped object has), the sound and the jump from the state 1 to 3: a dropped object has no message (its <c>GiveItem</c> answers 0) and never passes by the state 2. The key objects (byte 5 zero: message, fanfare, block of the
    /// world) are phase D and never dropped: this branch does nothing for them, before the object is given.
    /// </summary>
    private static void Pickup(EntityScriptProxy entity, IEntityWorldContext world, GameState state)
    {
        if (DialogueIsOpen(world) || world.PlayerEntity is not { } hero || !Overlaps(entity, hero))
        {
            return;
        }

        var item = entity.SpriteType - 0x1E;
        var tables = world.ItemTables;
        var sound = tables != null && (uint)item < ItemTables.DropRecordCount ? tables.DropSoundSfxIndex[item] : 0;
        if (sound == 0)
        {
            return; // NAMED STUB (phase D): the key objects.
        }

        ItemEffects.GiveItem(world, state, hero, item);
        SetGameFlag(state, ReadWord280(entity));
        world.SoundPlayer?.PlaySfx(sound);
        entity.ItemState += 2;
    }

    /// <summary><c>0x80045004</c>: a dialogue box is open (or waits for a choice).</summary>
    private static bool DialogueIsOpen(IEntityWorldContext world) => world.DialogueDirector is { } director && (director.IsOpen || director.IsAwaitingChoice);

    /// <summary>The overlap of the boxes of the object and of the hero on the three axes, as the binary tests it: the positions are the ones the end of the previous tick left (<see cref="EntityScriptProxy.ModdedPosX"/>), the sizes are the boxes'.</summary>
    private static bool Overlaps(EntityScriptProxy entity, EntityScriptProxy hero)
        => OverlapsOnAxis(entity.ModdedPosX, hero.ModdedPosX, entity.Width, hero.Width)
           && OverlapsOnAxis(entity.ModdedPosY, hero.ModdedPosY, entity.Height, hero.Height)
           && OverlapsOnAxis(entity.ModdedPosZ, hero.ModdedPosZ, entity.Depth, hero.Depth);

    /// <summary>One axis: with <c>a = object - hero &gt;= 0</c> the hero's size plus one must exceed it; otherwise (the object is behind) the object's size plus one must exceed <c>hero - object</c>.</summary>
    private static bool OverlapsOnAxis(int objectPosition, int heroPosition, int objectSize, int heroSize)
    {
        var distance = unchecked(objectPosition - heroPosition);
        return distance >= 0 ? distance < unchecked(heroSize + 1) : unchecked(heroPosition - objectPosition) < unchecked(objectSize + 1);
    }

    /// <summary><c>SetGameFlag</c> (<c>0x80032B28</c>): nothing for the flag 0, otherwise the bit <c>flag mod 32</c> of the word <c>flag &gt;&gt; 5</c> of the bank (the temporary one when the bit 15 is set) is raised.</summary>
    private static void SetGameFlag(GameState state, uint flag)
    {
        if (flag != 0)
        {
            state.AddFlag(flag, 1u << (int)(flag & 31));
        }
    }

    /// <summary><c>+0x274</c>: the kind of the object (0 placed, 1 dropped, 2 chest).</summary>
    internal static uint ReadWord274(EntityScriptProxy entity)
        => (uint)(entity.Bytes[0] | (entity.Bytes[1] << 8) | (entity.Bytes[2] << 16) | (entity.Bytes[3] << 24));

    internal static void WriteWord274(EntityScriptProxy entity, uint value)
    {
        entity.Bytes[0] = (byte)value;
        entity.Bytes[1] = (byte)(value >> 8);
        entity.Bytes[2] = (byte)(value >> 16);
        entity.Bytes[3] = (byte)(value >> 24);
    }

    /// <summary><c>+0x280</c>: <see cref="EntityScriptProxy.AIValues"/> 0 (low half) and 1 (high half).</summary>
    internal static uint ReadWord280(EntityScriptProxy entity) => ReadWord(entity, 0);

    internal static void WriteWord280(EntityScriptProxy entity, uint value) => WriteWord(entity, 0, value);

    /// <summary><c>+0x284</c>: <see cref="EntityScriptProxy.AIValues"/> 2 (low half) and 3 (high half).</summary>
    internal static uint ReadWord284(EntityScriptProxy entity) => ReadWord(entity, 2);

    internal static void WriteWord284(EntityScriptProxy entity, uint value) => WriteWord(entity, 2, value);

    private static uint ReadWord(EntityScriptProxy entity, int index)
        => (ushort)entity.AIValues[index] | ((uint)(ushort)entity.AIValues[index + 1] << 16);

    private static void WriteWord(EntityScriptProxy entity, int index, uint value)
    {
        entity.AIValues[index] = unchecked((short)(value & 0xFFFF));
        entity.AIValues[index + 1] = unchecked((short)(value >> 16));
    }
}
