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
/// the life (600 ticks, the mark of the last 120 at 120), the wait for the end of the rise, the contact with the hero, <c>GiveItem</c>, the flag, the sound and the light destruction.</para>
/// <para>Phase D (T4): the PLACED object (<c>+0x274 == 0</c>, an object put on a map whose record has no script of its own at the slot): A255 clears <c>Collidable</c> and writes its words at the load; C255 takes it on contact with the hero - the message
/// "name gained!" (always, for a placed object), the sound, the effect, then either the state 3 (an ordinary object, message in mode 0) or the state 2 (a KEY object, byte 5 of the table zero: <c>Block</c> of the world, the fanfare, message in mode 1 closable
/// by script only), and finally <c>Unblock</c> and the light destruction once the box has closed. The chest (<c>+0x274 == 2</c>, E14) and the placed object that holds a flag (D-E0C-3) are NAMED STUBS; the fanfare is a journaled stub (D-E0C-7).</para>
/// </summary>
internal static class PickupAi
{
    /// <summary>The program code of both handlers: A255 (<c>0x80061BD4</c>, slot A) and C255 (<c>0x8007C174</c>, slot C).</summary>
    internal const int Code = 255;

    /// <summary>The value of <c>+0x274</c> of a dropped object.</summary>
    internal const uint KindDropped = 1;

    /// <summary>The sprite types of the objects of the global page that carry C255: 31 to 118 but 72 (87 types). The object is <c>type - 0x1E</c>. A255 is registered for these 87 and for the chest, type 30 (E20.e0d, <see cref="ChestAi"/>; it was not before: D-E0C-2).</summary>
    internal static readonly int[] SpriteTypes = Enumerable.Range(31, 118 - 31 + 1).Where(type => type != 72).ToArray();

    /// <summary>The (slot, code) keys this class registers: A255 and C255.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramALoad, Code), (ScriptHelper.ProgramCTick, Code) };

    /// <summary>
    /// Test seam, per thread (the motif of <see cref="Alundra.Hero.PlayerAnimationEffects.StubCallsForTests"/>): when non-null, the stub of the fanfare of the key objects appends "start" (<c>0x80049B7C</c> + <c>0x8005A9E0(0xB)</c>) and "end"
    /// (<c>StopAllSound</c>, <c>0x80049AF4</c>) to it when it is called.
    /// </summary>
    [System.ThreadStatic]
    internal static List<string>? FanfareStubCallsForTests;

    private const int LifeMarkTicks = 0x78;
    private const int LifeMarkValue = 0x78;
    private const int StateRise = 0;
    private const int StatePickup = 1;
    private const int StateFanfare = 2;
    private const int StateAfterTheFanfare = 3;

    /// <summary>The value of <c>+0x274</c> of an object placed on a map.</summary>
    internal const uint KindPlaced = 0;

    /// <summary>The control mode of the message of an ordinary object (the entities stop under it) and of a key object (the hero is held, the world goes on).</summary>
    private const int OrdinaryMessageMode = 0;
    private const int KeyMessageMode = 1;

    /// <summary>The masks of <c>0x8004507C</c>: 4 (the box is closed by the script only) from the pickup of a key object to the end of the fanfare, 3 (the default) after.</summary>
    private const int ScriptCloseOnlyMask = 4;
    private const int DefaultCloseMask = 3;

    /// <summary>Registers A255 and C255 for <see cref="SpriteTypes"/>.</summary>
    internal static void Register(NativeSpriteRegistry registry)
    {
        registry.Register(ScriptHelper.ProgramALoad, Code, SpriteTypes.Append(ChestAi.SpriteType).ToArray(), (entity, context, state) => LoadOfAnyType(entity));
        registry.Register(ScriptHelper.ProgramCTick, Code, SpriteTypes, Tick);
    }

    /// <summary>A255 is one function for 88 types (<c>0x80061BD4</c>): the chest (type <c>0x1E</c>, E20.e0d: <see cref="ChestAi.Load"/>) or a pickup object (<see cref="Load"/>).</summary>
    private static void LoadOfAnyType(EntityScriptProxy entity)
    {
        if (entity.SpriteType == ChestAi.SpriteType)
        {
            ChestAi.Load(entity);
            return;
        }

        Load(entity);
    }

    /// <summary>
    /// A255 (<c>0x80061BD4</c>). A dropped object (<c>+0x274 == 1</c>) returns at once; the chest (<c>+0x274 == 2</c>, E14) is a NAMED STUB (D-E0C-2). A PLACED object (<c>+0x274 == 0</c>, <c>0x80061C60-0x80061D00</c>): <c>+0x6C &amp;= ~0x80</c> first
    /// (it is no longer a support: the hero crosses it and takes it), then, when it holds a flag (<c>+0x40</c>, which the binary tests against the flag bank and may destroy the object for), a NAMED STUB (D-E0C-3: none of the 152 placed objects has one) that
    /// writes nothing more; otherwise the state <c>+0x27C = 0</c>, <c>+0x280 = -1</c>, <c>+0x284 = 0</c> and the life <c>+0x278 = +0x40</c> (0: no rule).
    /// </summary>
    internal static void Load(EntityScriptProxy entity)
    {
        if (ReadWord274(entity) != KindPlaced)
        {
            return;
        }

        entity.Flags &= ~EntityFlags.Collidable;
        if (entity.ContentsGameFlag != 0)
        {
            return; // NAMED STUB (D-E0C-3).
        }

        entity.ItemState = StateRise;
        WriteWord280(entity, uint.MaxValue);
        WriteWord284(entity, 0);
        entity.DelayOrAngleOrEntityId = entity.ContentsGameFlag;
    }

    /// <summary>
    /// The fanfare of the key objects (<c>0x80049B7C(0)</c> then <c>0x8005A9E0(0xB)</c>, the CD track 11). <c>0x80049B7C</c> is <c>LoadBgm</c>: <c>LoadBgm(0)</c> stops the music, as the opcode <c>0xA6</c> does (D-E0C2-1, D-E20-65).
    /// The track 11 stays a journaled STUB (D-E0C-7, ADR-0076) until E19.sm7 plays the CD tracks. A host without a director of the music does nothing more (D-E0C2-2).
    /// </summary>
    private static void FanfareStart(IEntityWorldContext world)
    {
        world.BgmFadeDirector?.LoadBgm(0);
        FanfareStubCallsForTests?.Add("start");
    }

    /// <summary><c>CdBusy()</c> (<c>0x8005A7D4</c>): the stub of the fanfare is never busy, so the state 2 lasts one tick.</summary>
    private static bool FanfareIsBusy() => false;

    /// <summary><c>StopAllSound</c> (<c>0x80049AF4</c>, as the opcode <c>0xA5</c>), which restarts the loaded music from the top after the fanfare (D-E0C2-1); the journal of the stub keeps its "end".</summary>
    private static void FanfareEnd(IEntityWorldContext world)
    {
        world.BgmFadeDirector?.StopAllSound();
        FanfareStubCallsForTests?.Add("end");
    }

    /// <summary>
    /// C255 (<c>0x8007C174</c>), one tick of the branch <c>+0x274 != 2</c> of the binary: a dropped or a placed object. The chest (<c>+0x274 == 2</c>) is a named stub: nothing.
    /// </summary>
    internal static void Tick(EntityScriptProxy entity, IEntityWorldContext world, GameState state)
    {
        if (ReadWord274(entity) is not (KindPlaced or KindDropped))
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
            case StateFanfare:
                // 0x8007C6F0: wait for the end of the fanfare (CdBusy), then the box may be closed by the player again and the music comes back.
                if (FanfareIsBusy())
                {
                    return;
                }

                entity.ItemState += 1;
                world.DialogueDirector?.SetCloseMask(DefaultCloseMask);
                FanfareEnd(world);
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
    /// The state 1 (<c>0x8007C488-0x8007C6EC</c>): nothing while a dialogue is open; the overlap of the boxes with the hero's on the three axes; then <c>GiveItem</c>, the text "name gained!" (always for a placed object, never for a dropped one:
    /// <see cref="ItemEffects.ComposePickupMessage"/>), the game flag of the container (a dropped object; the stray <c>SetGameFlag(-1)</c> of a placed one is not carried, Q-E0C-5, D-E0C-12) and, by the byte 5 of the table of the objects:
    /// not zero (the sounds 52, 53, 54): the sound, the message in mode 0 and the jump from the state 1 to 3; zero (a KEY object): the state 2, <c>Block(self)</c>, the fanfare (a stub), the message in mode 1 and the mask 4.
    /// </summary>
    private static void Pickup(EntityScriptProxy entity, IEntityWorldContext world, GameState state)
    {
        if (DialogueIsOpen(world) || world.PlayerEntity is not { } hero || !Overlaps(entity, hero))
        {
            return;
        }

        var item = entity.SpriteType - 0x1E;
        var tables = world.ItemTables;
        if (tables == null || (uint)item >= ItemTables.DropRecordCount)
        {
            return; // degraded: no table of the objects, nothing to read the byte 5 from.
        }

        var kind = ReadWord274(entity);
        var giveReturn = ItemEffects.GiveItem(world, state, hero, item);
        var message = ItemEffects.ComposePickupMessage(item, kind, giveReturn);
        if (kind != KindPlaced)
        {
            SetGameFlag(state, ReadWord280(entity));
        }

        var sound = tables.DropSoundSfxIndex[item];
        if (sound != 0)
        {
            world.SoundPlayer?.PlaySfx(sound);
            OpenMessage(world, message, OrdinaryMessageMode);
            entity.ItemState += 2;
            return;
        }

        entity.ItemState += 1;
        world.Block(entity);
        FanfareStart(world);
        OpenMessage(world, message, KeyMessageMode);
        world.DialogueDirector?.SetCloseMask(ScriptCloseOnlyMask);
    }

    /// <summary><c>0x800450F0</c> on the text buffer: opens the box on the composed text, unless the buffer is empty (a dropped object).</summary>
    private static void OpenMessage(IEntityWorldContext world, string message, int controlMode)
    {
        if (message.Length != 0)
        {
            world.DialogueDirector?.OpenComposed(message, controlMode);
        }
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
