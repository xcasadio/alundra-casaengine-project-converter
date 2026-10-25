#nullable enable
using System;
using CasaEngine.Engine.Environment;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Session;

namespace Alundra.Items;

/// <summary>
/// E20.e0c (docs/plan-e20-heros.md section 1.26, T1, phase A - nothing calls it yet): what a pickup hands to the hero. <see cref="GiveItem"/> is <c>0x80033F00</c>, the only caller of which is the pickup handler C255
/// (<c>0x8007C174</c>): a jump table of 0x57 entries (an item 0x57 or above goes to the simple addition). In the binary's order:
/// <list type="bullet">
/// <item><description>the item 0 gives nothing and answers 0;</description></item>
/// <item><description>the herb <c>0x24</c>, the large potion <c>0x25</c>, the magic extract <c>0x26</c>, the brew <c>0x27</c> and the small potion <c>0x29</c> go to the inventory first (<c>0x80033EC8</c>: the count before is below the count
/// after); an object that does not enter, because the inventory holds its maximum, is used on the spot (<see cref="HeroHeals"/>);</description></item>
/// <item><description>the money <c>0x45</c> to <c>0x48</c> (1, 5, 10, 30 gildas, capped at 9999), the beak <c>0x4F</c> (+1 on the counter that the text command folds into the falcon keys, capped at 50, and the flag 1450), the small
/// crystal <c>0x51</c> (the magic drop), the large crystal <c>0x52</c>, the life vessel <c>0x53</c> (the maximum of life + 1 with no cap, the life filled, the sound <c>0x31</c> and the effects of
/// <see cref="NativeEffects.CreateLifeVesselEffects"/>), the three hearts <c>0x54</c> to <c>0x56</c>;</description></item>
/// <item><description>the object <c>0x50</c> (<c>0x8003382C</c>) is a NAMED STUB: no placed object and no loot reaches it, its body is not read, it changes nothing;</description></item>
/// <item><description>every other object is added to the inventory by <see cref="PlayerManager.AddOneItemIfUnlocked"/>.</description></item>
/// </list>
/// The answer is 1 exactly when the column 0 of the table of the objects is zero (<c>0x800340D8-0x800340EC</c>): the objects that vanish (hearts, money, herb, potions, crystals) answer 0. The effects go to the queue of the effect
/// stage, so a pickup draws no number before the stage applies it (D-E0C-8, ADR-0077). <see cref="ComposePickupMessage"/> builds the text of the box the pickup opens.
/// </summary>
internal static class ItemEffects
{
    private const int GainedEtcIndex = 0x45;
    private const int ChestPrefixEtcIndex = 0x4C;
    private const int ChestGainedEtcIndex = 0x4D;
    private const int ChestTooManyEtcIndex = 0x4E;
    private const int ChestFullStopEtcIndex = 0x4F;
    private const int FalconTempCap = 50;
    private const uint BeakFlag = 1450;

    /// <summary>
    /// <c>0x80033F00</c>: hands <paramref name="item"/> to the hero and answers 1 or 0 (see the class). <paramref name="hero"/> is the entity that carries the life; the magic, the money and the inventory are the
    /// <paramref name="state"/>'s; the effects and the sound go through <paramref name="world"/>.
    /// </summary>
    public static int GiveItem(IEntityWorldContext world, GameState state, EntityScriptProxy hero, int item)
    {
        if (item == 0)
        {
            return 0;
        }

        var tables = world.ItemTables;
        switch (item)
        {
            case 0x24:
                if (!EntersTheInventory(state, tables, item))
                {
                    HeroHeals.Herb(world, state, hero);
                }

                break;
            case 0x25:
                if (!EntersTheInventory(state, tables, item))
                {
                    HeroHeals.LargePotion(world, state, hero);
                }

                break;
            case 0x26:
                if (!EntersTheInventory(state, tables, item))
                {
                    HeroHeals.MagicDrop(world, state, hero);
                }

                break;
            case 0x27:
                if (!EntersTheInventory(state, tables, item))
                {
                    HeroHeals.Brew(world, state, hero);
                }

                break;
            case 0x29:
                if (!EntersTheInventory(state, tables, item))
                {
                    HeroHeals.SmallPotion(world, state, hero);
                }

                break;
            default:
                ApplyItem(world, state, hero, item);
                break;
        }

        return tables != null && (uint)item < ItemTables.DropRecordCount && tables.DropField1[item] != 0 ? 0 : 1;
    }

    /// <summary>
    /// E20.e0d (docs/plan-e20-heros.md section 1.26.2): <c>CanTake</c> (<c>0x80033D34</c>), what F255 of the chest asks before the object rises: the object 0 can not be taken; the herb <c>0x24</c>, the large potion <c>0x25</c>, the magic extract <c>0x26</c>, the brew <c>0x27</c> and
    /// the small potion <c>0x29</c> can while the stock is below the cap of the table of the objects (<see cref="ItemTables.DropStockCap"/>: 9, 1, 1, 1, 1); every other object can, with no test of the stock.
    /// </summary>
    public static bool CanTake(GameState state, ItemTables? tables, int item)
    {
        if (item == 0)
        {
            return false;
        }

        if (item is 0x24 or 0x25 or 0x26 or 0x27 or 0x29)
        {
            return tables != null && PlayerManager.GetNumberOfItem(state, item) < tables.DropStockCap[item];
        }

        return true;
    }

    /// <summary>
    /// E20.e0d: <c>ApplyItem</c> (<c>0x80033DBC</c>, one caller: C255 of the object of a chest, <c>0x8007C358</c>): the table <c>0x8002308C</c> - the money <c>0x45</c> to <c>0x48</c>, the beak <c>0x4F</c>, the objects <c>0x50</c> to <c>0x53</c> (the seed, the two crystals, the life vessel), the hearts
    /// <c>0x54</c> to <c>0x56</c> - and every other object added to the inventory by <see cref="PlayerManager.AddOneItemIfUnlocked"/>. It differs from <see cref="GiveItem"/> in one thing: it has no rule "the inventory first" for the five stacked objects - a chest never hands over what
    /// <see cref="CanTake"/> refused, so the herb is added and never drunk. <see cref="GiveItem"/> shares this body for all the other objects.
    /// </summary>
    public static void ApplyItem(IEntityWorldContext world, GameState state, EntityScriptProxy hero, int item)
    {
        if (item == 0)
        {
            return;
        }

        var tables = world.ItemTables;
        switch (item)
        {
            case 0x45:
                PlayerManager.AddMoney(state, 1);
                break;
            case 0x46:
                PlayerManager.AddMoney(state, 5);
                break;
            case 0x47:
                PlayerManager.AddMoney(state, 10);
                break;
            case 0x48:
                PlayerManager.AddMoney(state, 30);
                break;
            case 0x4F:
                state.PlayerStats.FalconTemp = (short)Math.Min(state.PlayerStats.FalconTemp + 1, FalconTempCap);
                state.AddFlag(BeakFlag, 1u << (int)(BeakFlag & 31));
                break;
            case 0x50:
                break; // NAMED STUB until E20.e0d phase C: 0x8003382C.
            case 0x51:
                HeroHeals.MagicDrop(world, state, hero);
                break;
            case 0x52:
                HeroHeals.LargeCrystal(world, state, hero);
                break;
            case 0x53:
                LifeVessel(world, state, hero);
                break;
            case 0x54:
                HeroHeals.SmallHeart(world, state, hero);
                break;
            case 0x55:
                HeroHeals.MediumHeart(world, state, hero);
                break;
            case 0x56:
                HeroHeals.LargeHeart(world, state, hero);
                break;
            default:
                if (tables != null)
                {
                    PlayerManager.AddOneItemIfUnlocked(state, tables, item);
                }

                break;
        }
    }

    /// <summary>
    /// E20.e0d (D-E0D-6): the text of the box a chest opens (<c>0x8007C210-0x8007C31C</c>), in the binary's order. An object the bag can take: ETC <c>0x4C</c> (empty), the name of the object (ETC <c>0x200 + item</c>) and ETC <c>0x4D</c> ("gained!"); an object it can not take:
    /// ETC <c>0x4E</c> ("Too many things to carry, to keep"), the name and ETC <c>0x4F</c> (the full stop). The strings of the resource keep the space that follows "gained" no more than the space that follows "to keep" - the Yarn compiler drops the one at the edge of a line (T0 (h) of
    /// E20.e0c) - so each is put back between the two parts. Empty when the ETC asset is not loaded (degraded: no message).
    /// </summary>
    public static string ComposeChestMessage(int item, bool canTake)
    {
        if (!EtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, item, out var name))
        {
            return string.Empty;
        }

        if (canTake)
        {
            return EtcStringTable.TryResolveText(ChestPrefixEtcIndex, out var prefix) && EtcStringTable.TryResolveText(ChestGainedEtcIndex, out var gained) ? prefix + name + " " + gained : string.Empty;
        }

        return EtcStringTable.TryResolveText(ChestTooManyEtcIndex, out var tooMany) && EtcStringTable.TryResolveText(ChestFullStopEtcIndex, out var fullStop) ? tooMany.TrimEnd(' ') + " " + name + fullStop : string.Empty;
    }

    /// <summary>
    /// The text of the box a pickup opens (<c>0x8007C5xx</c>): the name of the object (ETC <c>0x200 + item</c>) then the string "gained!" (ETC <c>0x45</c>), the binary's <c>strcpy</c> and <c>strcat</c>; empty when
    /// <c>GiveItem</c> answered 0 for a dropped object (<paramref name="word274"/> non-zero): no dropped object has a message. The ETC string in the resource is " gagné!", with a leading space that the Yarn compiler dropped
    /// (T0 (h)): it is put back between the two. Empty too when the ETC asset is not loaded (degraded: no message).
    /// </summary>
    public static string ComposePickupMessage(int item, uint word274, int giveReturn)
    {
        if (giveReturn == 0 && word274 != 0)
        {
            return string.Empty;
        }

        if (!EtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, item, out var name) || !EtcStringTable.TryResolveText(GainedEtcIndex, out var gained))
        {
            return string.Empty;
        }

        return name + " " + gained;
    }

    /// <summary>The inventory-first rule (<c>0x80033EC8</c>): the item is added; it entered when the count after is above the count before.</summary>
    private static bool EntersTheInventory(GameState state, ItemTables? tables, int item)
    {
        if (tables == null)
        {
            return false;
        }

        var before = PlayerManager.GetNumberOfItem(state, item);
        PlayerManager.AddOneItemIfUnlocked(state, tables, item);
        return before < PlayerManager.GetNumberOfItem(state, item);
    }

    /// <summary>The life vessel (<c>0x800333AC</c>) in the binary's order: the life, the sound at once, then the effects on the queue (the position of the hero captured now, with the binary's Z).</summary>
    private static void LifeVessel(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        PlayerManager.IncreaseHpMaxAndFill(state, hero);
        world.SoundPlayer?.PlaySfx(NativeEffects.LifeVesselSfx);
        if (world.EffectStage is { } stage)
        {
            var (x, y, z) = (hero.PosX, hero.PosY, unchecked(hero.PosZ + 1));
            stage.Emit(reservoir => NativeEffects.CreateLifeVesselEffects(reservoir, x, y, z));
        }
    }
}
