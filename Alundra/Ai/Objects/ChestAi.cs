#nullable enable
using System.Collections.Generic;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Transitions;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.e0d (docs/plan-e20-heros.md section 1.26.2): the treasure chest (global sprite type 30, flags <c>0x8180</c>: Collidable, Gravity, <c>InteractRequiresButton</c>). The binary gives it the programs A255 (<c>0x80061BD4</c>, shared with the 87 pickup
/// objects, registered in <see cref="PickupAi.Register"/>) and F255 (<c>0x8007FC88</c>, the chest alone). Phase A: <see cref="Load"/>, the branch of A255 for the chest. Phase B: <see cref="Interact"/>, F255 - the opening - and the branches of A255 and C255 for the
/// object it gives birth to, which are in <see cref="PickupAi"/> (<c>+0x274 == 2</c>).
/// </summary>
internal static class ChestAi
{
    /// <summary>The global sprite type of the chest.</summary>
    internal const int SpriteType = 30;

    /// <summary>The animation of the chest open (<c>+0x88 == 1</c>, the end of the chain of the opening animation 2).</summary>
    internal const uint OpenAnimation = 1;

    /// <summary>The animation of the chest opening (<c>+0x88 == 2</c>: 61 ticks, the sound 51, then the chain to the animation 1).</summary>
    internal const uint OpeningAnimation = 2;

    /// <summary>The (slot, code) key this class registers: F255.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramFInteract, PickupAi.Code) };

    private const int TypeOfItemZero = 0x1E;
    private const int RisePerTick = 0x8000;
    private const int RiseTicks = 0x40;
    private const int RaisedAboveTheChest = 0x100000;
    private const int EmptyChestEtcIndex = 0x48;
    private const int BirthFailedEtcIndex = 0x49;
    private const uint CollidableAndGravity = EntityFlags.Collidable | EntityFlags.Gravity;

    /// <summary>Registers F255 for the chest. (A255 of the chest is registered by <see cref="PickupAi.Register"/>, with the one of the pickup objects: the registry keeps one handler per key.)</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramFInteract, PickupAi.Code, new[] { SpriteType }, Interact);

    /// <summary>
    /// A255 on the chest (<c>0x80061BD4</c>, the branch of the type <c>0x1E</c>): when the contents (<c>+0x3C</c>) are zero - the record holds none, or the flag of the chest is raised and the roll of the creation (<c>InitializeContents</c>,
    /// <c>0x80032A40</c>) gave it none - the chest is drawn open (<c>+0x88 = 1</c>); a chest that holds something is left as it is (closed). Nothing else is written.
    /// </summary>
    internal static void Load(EntityScriptProxy chest)
    {
        if (chest.ContentsItemId == 0)
        {
            chest.TargetAnimationId = OpenAnimation;
        }
    }

    /// <summary>
    /// F255 (<c>0x8007FC88-0x8007FE1C</c>, replayed on 12 000 vectors of the binary): the hero pushes against the chest and presses Square. Four guards leave without a trace: a control bit of the player raised (<c>[0x800DC4B8]</c>), a map change requested
    /// (<c>[0x800DC4C4]</c>: <see cref="WarpDirector.IsTransitionInProgress"/>), an object in the hands, a dialogue open. A chest with no contents, or already opened (<c>+0x88 != 0</c>), opens the box on ETC <c>0x48</c> ("it stays empty") in mode 0. Otherwise
    /// <c>+0x88 = 2</c> (the animation of the opening: the sound 51 comes with it), and the object is born (type <c>contents + 0x1E</c>, at the chest, 16 pixels above, the chest as its parent - D-E0D-9); a table of entities that is full opens ETC <c>0x49</c> and leaves the
    /// chest at 2 (the binary's own outcome). <c>Block(chest)</c> freezes the world but for the chest; the object, born after it, is not blocked (<c>+0x20 = 0</c>) and rises by 0x8000 a tick for 64 ticks with no gravity and no collision; it is of the kind 2, in the state 0,
    /// and carries the answer of <see cref="ItemEffects.CanTake"/> (<c>+0x284</c>) and, for a key object the bag can take (the byte 5 of the table of the objects is zero), the mark <c>+0x288</c> and the end of the music (<c>LoadBgm(0)</c>).
    /// </summary>
    internal static void Interact(EntityScriptProxy chest, IEntityWorldContext world, GameState state)
    {
        if (state.PlayerControlFlags != 0 || WarpDirector.Instance.IsTransitionInProgress || world.PlayerEntity?.CarriedEntity != null || PickupAi.DialogueIsOpen(world))
        {
            return;
        }

        var contents = unchecked((int)chest.ContentsItemId);
        if (contents == 0 || chest.TargetAnimationId != 0)
        {
            OpenEtc(world, EmptyChestEtcIndex);
            return;
        }

        chest.TargetAnimationId = OpeningAnimation;
        var born = world.SpawnEntityBySpriteIndex(chest, false, contents + TypeOfItemZero, chest.PosX, chest.PosY, unchecked(chest.PosZ + RaisedAboveTheChest), 0);
        if (born == null)
        {
            OpenEtc(world, BirthFailedEtcIndex);
            return;
        }

        world.Block(chest);
        if (born.OwnerEntity is { } drawn)
        {
            drawn.IsVisible = false; // born Loaded, as the binary's: not drawn until A255 lets it go (D-E0D-5).
        }

        born.ForceZ = RisePerTick;
        born.DelayOrAngleOrEntityId = RiseTicks;
        born.Flags &= ~CollidableAndGravity;
        born.BlockedByEntity = null;
        PickupAi.WriteWord274(born, PickupAi.KindChest);
        born.ItemState = 0;
        var take = ItemEffects.CanTake(state, world.ItemTables, contents);
        PickupAi.WriteWord284(born, take ? 1u : 0u);
        var key = take && world.ItemTables is { } tables && (uint)contents < ItemTables.DropRecordCount && tables.DropSoundSfxIndex[contents] == 0;
        PickupAi.WriteWord288(born, key ? 1u : 0u);
        if (key)
        {
            world.BgmFadeDirector?.LoadBgm(0);
            PickupAi.FanfareStubCallsForTests?.Add("stop");
        }
    }

    /// <summary><c>0x800816D4</c> then <c>0x800450F0(text, 0)</c>: the box of an ETC string, in mode 0 (the entities stop under it).</summary>
    private static void OpenEtc(IEntityWorldContext world, int etcIndex)
    {
        if (EtcStringTable.TryResolveText(etcIndex, out var text))
        {
            PickupAi.OpenMessage(world, text, 0);
        }
    }
}
