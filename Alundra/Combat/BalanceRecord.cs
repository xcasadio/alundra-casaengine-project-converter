#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;

namespace Alundra.Combat;

/// <summary>
/// One balance record of <c>BALANCE.BIN</c> (E20.c1a C1, docs/plan-e20-heros.md section 1.5): the binary's layout is byte 0 the level, byte 1 the size of the record (the step to the next level of the
/// chain), byte 2 the maximum life, bytes 3 to 13 the eleven damage responses, byte 14 the number of attacks, then two bytes per attack (<c>0x0F + 2k</c>).
/// </summary>
public sealed class AlundraBalanceRecord
{
    public AlundraBalanceRecord(int level, int recordSize, int maxHp, byte[] damageResponses, AlundraBalanceAttack[] attacks, AlundraBalanceRecord? next)
    {
        Level = level;
        RecordSize = recordSize;
        MaxHp = maxHp;
        DamageResponses = damageResponses;
        Attacks = attacks;
        Next = next;
    }

    /// <summary>
    /// E14.a1b: the offset of the record in <c>BALANCE.BIN</c> (the exported <c>FileOffset</c>), -1 for a record written by hand. The records are contiguous: the byte after a record's last is the first byte of the next one.
    /// </summary>
    internal int FileOffset { get; set; } = -1;

    /// <summary>
    /// E14.a1b: the record stored right after this one in <c>BALANCE.BIN</c> (<see cref="FileOffset"/> + <see cref="RecordSize"/>), null for the last record of the file and for a record written by hand. The hit pass reads the byte
    /// <c>2 + (attribute &amp; 0xF)</c> of the sheet whatever the size of the record is, so an attribute nibble of 14 or 15 reads the bytes of the next record (<see cref="ByteAt"/>).
    /// </summary>
    internal AlundraBalanceRecord? Following { get; set; }

    /// <summary>Byte 0, the level of the record (the chain stops at the first record whose level reaches the threshold of the map; the last one is 255).</summary>
    public int Level { get; }

    /// <summary>Byte 1.</summary>
    public int RecordSize { get; }

    /// <summary>Byte 2, what <c>InitializeEntity</c> copies to the life and the maximum life (<c>0x80039E3C</c>).</summary>
    public int MaxHp { get; }

    /// <summary>Bytes 3 to 13: the response to the attack attribute nibble k is the byte at <c>k + 2</c>, so <c>DamageResponses[k - 1]</c>.</summary>
    public IReadOnlyList<byte> DamageResponses { get; }

    /// <summary>The attacks, in the order of the record.</summary>
    public IReadOnlyList<AlundraBalanceAttack> Attacks { get; }

    /// <summary>The next level of the chain, null at its end.</summary>
    public AlundraBalanceRecord? Next { get; }

    /// <summary>
    /// The attack the switch block of <c>UpdateAnimation</c> loads in <c>CurrentAttack</c> for an animation (<c>0x80038B74-0x80038BAC</c>): <c>Attacks[animation + 1]</c> when that index is below the
    /// number of attacks, else <c>Attacks[0]</c>, null when the record has no attack. It depends on the animation, not on the direction.
    /// </summary>
    public AlundraBalanceAttack? AttackForAnimation(uint animation)
    {
        if (Attacks.Count == 0)
        {
            return null;
        }

        var index = (long)animation + 1;
        return index < Attacks.Count ? Attacks[(int)index] : Attacks[0];
    }

    /// <summary>
    /// The byte at <paramref name="offset"/> of the binary's record, for the response read by the hit pass (<c>lbu 2(sheet + nibble)</c>, <c>0x800395D4</c>): the attribute nibble 0 reads the maximum life,
    /// 1 to 11 the responses, 12 the number of attacks, and the larger ones the bytes of the attacks (the data has no such attack, the read is exact anyway). 0 beyond the record.
    /// </summary>
    public int ByteAt(int offset)
    {
        if (Following != null && offset >= RecordSize)
        {
            // E14.a1b (the D1 vectors of the E14.a1 oracle with the attribute nibble 15): the real memory goes on with the next record of the file.
            return Following.ByteAt(offset - RecordSize);
        }

        if (offset == 0)
        {
            return Level;
        }

        if (offset == 1)
        {
            return RecordSize;
        }

        if (offset == 2)
        {
            return MaxHp;
        }

        if (offset >= 3 && offset <= 13)
        {
            return DamageResponses[offset - 3];
        }

        if (offset == 14)
        {
            return Attacks.Count;
        }

        var attackByte = offset - 15;
        if (attackByte < 0 || attackByte >= Attacks.Count * 2)
        {
            return 0;
        }

        var attack = Attacks[attackByte / 2];
        return (attackByte & 1) == 0 ? attack.Attribute : attack.Power;
    }
}
