#nullable enable
using System;
using Alundra.Maps;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Physics;

namespace Alundra.Items;

/// <summary>
/// The three equipment balance records of the binary (E20.c1b K2, docs/plan-e20-heros.md section 1.6): the pointers at <c>0x80146E00</c> (armour), <c>0x80146E08</c> (weapon) and <c>0x80146E10</c> (accessory), each with
/// the item word that follows it (<c>item id + 0x1E</c>, 0 for none). They are cleared at every map load (<c>InitializeItems</c> <c>0x80044520</c>), filled at every normal tick of the hero by
/// <see cref="PlayerManager"/> (<c>UpdateItemEffectState</c> <c>0x800307E8</c>, steps 2 to 5) and read only by the damage calculation (<see cref="AlundraDamage"/>, <c>0x8004464C</c>) and by the step 6 of
/// <c>UpdateItemEffectState</c> (E20.c2). One instance per world, reached through <see cref="IScriptHost.EquipmentRecords"/> and <see cref="IEntityWorldContext.EquipmentRecords"/>.
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

    /// <summary>
    /// The map load (<c>0x80044520</c>, <c>InitializeItems</c>): empties the three POINTERS only (<c>0x80146E00</c>, <c>0x80146E08</c>, <c>0x80146E10</c>); the item words that follow them are left as they are
    /// (E20.c2a T2, the P4 of E20.c1b-1; the next <c>UpdateItemEffectState</c> rewrites all six words). <see cref="ClearAll"/> is the six-word clear of that step.
    /// </summary>
    public void Clear()
    {
        for (var slot = 0; slot < Count; slot++)
        {
            _records[slot] = null;
        }
    }

    /// <summary>Empties the six words (the three pointers and the three item words): the first thing steps 2 to 5 of <c>UpdateItemEffectState</c> do (<c>0x800308A4-0x800308BC</c>).</summary>
    public void ClearAll()
    {
        for (var slot = 0; slot < Count; slot++)
        {
            _records[slot] = null;
            _itemIds[slot] = 0;
        }
    }

    /// <summary>
    /// E20.c2a T2: the boots level of the hero (<c>[0x80127000]</c>), written by step 1 of <c>UpdateItemEffectState</c> (3, 2 or 1 for the objects <c>0x1C</c>, <c>0x1B</c>, <c>0x1A</c>, else 0). The binary caches it
    /// at the end of every tick; the physics of the DLL (<see cref="AlundraScriptedMotion"/>) recompute it on demand through <see cref="PlayerManager.BootsLevelOf"/>, the same function, so the value read there
    /// is the cached one of this tick at the latest (the one-tick lag of the binary is immaterial).
    /// </summary>
    public int BootsLevel { get; internal set; }

    private byte[]? _sheetResponses;
    private AlundraBalanceRecord? _sheetSource;

    /// <summary>
    /// E20.c2a T2: the hero's defence sheet (<c>0x80127008</c>, 255 bytes in the binary), the one <c>ComputeNewHp</c> reads when the hero is hit (E20.c2b). A sheet is the record of the armour (or the hero's own
    /// <c>+0x1C4</c> record without armour) whose eleven responses are combined with the weapon's and the accessory's by step 6 of <c>UpdateItemEffectState</c>. One object for the whole life of the world: the same record
    /// is rewritten in place (<see cref="StoreHeroSheet"/>) and only rebuilt when the record it copies changes. Null until a first step 6 ran (the binary's BSS zero before the first tick).
    /// </summary>
    public AlundraBalanceRecord? HeroSheet { get; private set; }

    /// <summary>The bytes 3 to 13 of the sheet written by step 6 (the byte 2 and the attacks are those of <paramref name="source"/>, copied as they are).</summary>
    internal void StoreHeroSheet(AlundraBalanceRecord source, ReadOnlySpan<byte> responses)
    {
        if (HeroSheet == null || !ReferenceEquals(_sheetSource, source))
        {
            _sheetResponses = new byte[responses.Length];
            HeroSheet = new AlundraBalanceRecord(source.Level, source.RecordSize, source.MaxHp, _sheetResponses, System.Linq.Enumerable.ToArray(source.Attacks), null);
            _sheetSource = source;
        }

        responses.CopyTo(_sheetResponses);
    }

    internal void Set(int slot, AlundraBalanceRecord? record, int itemId)
    {
        _records[slot] = record;
        _itemIds[slot] = itemId;
    }
}
