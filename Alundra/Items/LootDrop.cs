#nullable enable
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Session;

namespace Alundra.Items;

/// <summary>
/// E20.e0c (docs/plan-e20-heros.md section 1.26, T1, phase A - nothing calls it yet): the drop of the loot of ALUN_CD.EXE (France). <see cref="SpawnRandomLoot"/> is <c>0x80032C7C</c> (the cut grass, one caller),
/// <see cref="SpawnContents"/> is <c>0x80032B90</c> (<c>DestroyEntity</c>, the recursion, the mimic) and <see cref="Veto"/> is <c>0x80032A00</c>. Both make the object appear through the interface of the apparition by sprite index
/// (<c>SpawnWarpEntity</c>, <c>0x80039F58</c>: no parent, global page, type <c>item + 0x1E</c>, direction 0) and then write, on the entity they got, the seven things the binary writes: the rise (<c>+0xB8</c>, 10 pixels a tick),
/// the kind "dropped" (<c>+0x274 = 1</c>), the loss of <c>Collidable</c> (<c>+0x6C &amp;= ~0x80</c>), the life (<c>+0x278</c>: 600 ticks for the objects whose column 0 of the object table is set, -1 for the others), the state of the
/// pickup (<c>+0x27C = 0</c>), the flag to raise (<c>+0x280</c>: the container's, or 0) and the speed of the bounce (<c>+0x284 = 0xA0000</c>); then the sound 84. A full table of entities (the interface answers null) gives no
/// writes and no sound; the generator has moved all the same (the roll came first). The words of 32 bits go through <see cref="PickupAi"/>.
/// <para>Z: <c>SpawnRandomLoot</c> receives a Z of the binary and passes it minus one raw unit (the interface adds one, ADR-0026); <c>SpawnContents</c> passes the <c>PosZ</c> of the container, which the interface raises to the
/// binary's <c>+0x11C</c>. An item the table of the objects does not hold (98 and over, which no roll produces) lives forever.</para>
/// </summary>
internal static class LootDrop
{
    private const int TypeOfItemZero = 0x1E;
    private const int RisePerTick = 0xA0000;
    private const int DropLifeTicks = 600;
    private const int DropSound = 0x54;

    /// <summary><c>0x80032A00</c>: the three objects of the magic (<c>0x26</c>, <c>0x51</c>, <c>0x52</c>) are not dropped when the hero has no magic at all (<c>MpMax == 0</c>).</summary>
    public static bool Veto(int item, GameState state)
        => (item is 0x26 or 0x51 or 0x52) && state.PlayerStats.MpMax == 0;

    /// <summary>
    /// <c>0x80032C7C</c>: rolls <paramref name="code"/> (the generator moves, 0 to 2 steps), and drops what it gives at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="binaryZ"/>) of the binary. True when an object
    /// appeared.
    /// </summary>
    public static bool SpawnRandomLoot(IEntityWorldContext world, GameState state, int code, int x, int y, int binaryZ)
    {
        var item = (int)LootRoll.Choose(code, static () => (uint)GameRandom.Next(), out _);
        if (item == 0 || Veto(item, state))
        {
            return false;
        }

        return Spawn(world, item, x, y, unchecked(binaryZ - 1), flag: 0);
    }

    /// <summary>
    /// <c>0x80032B90</c>: drops the contents the roll of the creation put on <paramref name="container"/> (<see cref="EntityScriptProxy.ContentsItemId"/>), at its position, with its game flag. The generator is not touched.
    /// True when an object appeared.
    /// </summary>
    public static bool SpawnContents(IEntityWorldContext world, GameState state, EntityScriptProxy container)
    {
        var item = unchecked((int)container.ContentsItemId);
        if (item == 0 || Veto(item, state))
        {
            return false;
        }

        return Spawn(world, item, container.PosX, container.PosY, container.PosZ, container.ContentsGameFlag);
    }

    private static bool Spawn(IEntityWorldContext world, int item, int x, int y, int interfaceZ, int flag)
    {
        var born = world.SpawnEntityBySpriteIndex(null, false, item + TypeOfItemZero, x, y, interfaceZ, 0);
        if (born == null)
        {
            return false;
        }

        var tables = world.ItemTables;
        born.ForceZ = RisePerTick;
        PickupAi.WriteWord274(born, 1);
        born.Flags &= ~EntityFlags.Collidable;
        born.DelayOrAngleOrEntityId = tables != null && (uint)item < ItemTables.DropRecordCount && tables.DropField1[item] != 0 ? DropLifeTicks : -1;
        born.ItemState = 0;
        PickupAi.WriteWord280(born, unchecked((uint)flag));
        PickupAi.WriteWord284(born, RisePerTick);
        world.SoundPlayer?.PlaySfx(DropSound);
        return true;
    }
}
