#nullable enable
namespace Alundra.Scripts;

/// <summary>
/// The three equipment balance records of the binary (E20.c1b K2, docs/plan-e20-heros.md section 1.6): the pointers at <c>0x80146E00</c> (armour), <c>0x80146E08</c> (weapon) and <c>0x80146E10</c> (accessory), each with
/// the item word that follows it (<c>item id + 0x1E</c>, 0 for none). They are cleared at every map load (<c>InitializeItems</c> <c>0x80044520</c>), filled at every normal tick of the hero by
/// <see cref="AlundraPlayerManager"/> (<c>UpdateItemEffectState</c> <c>0x800307E8</c>, steps 2 to 5) and read only by the damage calculation (<see cref="AlundraDamage"/>, <c>0x8004464C</c>) and by the step 6 of
/// <c>UpdateItemEffectState</c> (E20.c2). One instance per world, reached through <see cref="IAlundraScriptHost.EquipmentRecords"/> and <see cref="IEntityWorldContext.EquipmentRecords"/>.
/// </summary>
public sealed class AlundraEquipmentRecords
{
    /// <summary>The slot of the armour record (<c>0x80146E00</c>).</summary>
    public const int Armour = 0;

    /// <summary>The slot of the weapon record (<c>0x80146E08</c>).</summary>
    public const int Weapon = 1;

    /// <summary>The slot of the accessory record (<c>0x80146E10</c>).</summary>
    public const int Accessory = 2;

    /// <summary>The number of records.</summary>
    public const int Count = 3;

    private readonly AlundraBalanceRecord?[] _records = new AlundraBalanceRecord?[Count];
    private readonly int[] _itemIds = new int[Count];

    /// <summary>The record of a slot (<see cref="Armour"/>, <see cref="Weapon"/>, <see cref="Accessory"/>), null when the slot is empty.</summary>
    public AlundraBalanceRecord? this[int slot] => _records[slot];

    /// <summary>The item the record of a slot describes (the item word of the binary minus <c>0x1E</c>), 0 when the slot is empty.</summary>
    public int ItemIdOf(int slot) => _itemIds[slot];

    /// <summary>Empties the three slots (the six words of the binary).</summary>
    public void Clear()
    {
        for (var slot = 0; slot < Count; slot++)
        {
            _records[slot] = null;
            _itemIds[slot] = 0;
        }
    }

    internal void Set(int slot, AlundraBalanceRecord? record, int itemId)
    {
        _records[slot] = record;
        _itemIds[slot] = itemId;
    }
}
