#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;

namespace Alundra.Scripts;

/// <summary>One attack of a balance record: the attribute byte (low nibble = the attack type, bit 7 = the power adds the equipment's) and the power byte. A reference type on purpose: the binary's
/// <c>CurrentAttack</c> (<c>+0x1C8</c>) is a pointer to one of these two-byte entries, and "none" is a null pointer.</summary>
public sealed class AlundraBalanceAttack
{
    public AlundraBalanceAttack(int attribute, int power)
    {
        Attribute = attribute & 0xFF;
        Power = power & 0xFF;
    }

    /// <summary>The attribute byte (<c>AttackAttribute</c> of the export).</summary>
    public int Attribute { get; }

    /// <summary>The power byte.</summary>
    public int Power { get; }
}

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

/// <summary>
/// The 512 balance entries of the game (E20.c1a C1, docs/plan-e20-heros.md section 1.5): <c>Data/balance.json</c>, which the converter republishes from <c>BALANCE.BIN</c> (<c>EntriesBySpriteIndex</c>), loaded
/// once per project like <see cref="AlundraItemTables"/>. The record of an entity is chosen as <c>0x80044550</c> does: the sprite index (an index in <c>0x1F..0x7F</c> is read as <c>0x1E</c>), then the chain of
/// levels is walked while the level of the record is below the threshold of the map (<c>0x80044520</c>, the property <c>D</c> of the map header). Degraded mode: a file that is missing or unreadable logs one
/// warning and leaves every entry empty, so no entity gets a record (and so no attack, no response, no life from it).
/// </summary>
public sealed class AlundraBalanceTables
{
    private const string DataDirectoryName = "Data";
    private const string FileName = "balance.json";

    /// <summary>The number of entries of <c>BALANCE.BIN</c>.</summary>
    public const int EntryCount = 512;

    /// <summary>The property of the map header the threshold comes from (<c>lbu 0xB</c> of the header, <c>0x8002C39C</c>); the exported <c>BalanceLevel</c> is the byte 9, not this one.</summary>
    public const string ThresholdPropertyName = "D";

    private readonly AlundraBalanceRecord?[] _entries = new AlundraBalanceRecord?[EntryCount];

    /// <summary>Loads <c>Data/balance.json</c> from <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraBalanceTables() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads <c>Data/balance.json</c> under <paramref name="projectPath"/> (the overload tests point at the real project with).</summary>
    public AlundraBalanceTables(string projectPath)
    {
        var filePath = Path.Combine(projectPath ?? string.Empty, DataDirectoryName, FileName);
        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning($"AlundraBalanceTables: '{filePath}' not found; no entity gets a balance record (degraded mode).");
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            var entries = document.RootElement.GetProperty("EntriesBySpriteIndex");
            var count = Math.Min(entries.GetArrayLength(), EntryCount);
            for (var i = 0; i < count; i++)
            {
                _entries[i] = ReadRecord(entries[i]);
            }
        }
        catch (Exception ex)
        {
            Array.Clear(_entries);
            Logs.WriteWarning($"AlundraBalanceTables: failed to load '{filePath}' ({ex.Message}); no entity gets a balance record (degraded mode).");
        }
    }

    /// <summary>Builds tables from records a test wrote by hand (entry index to record).</summary>
    internal AlundraBalanceTables(IReadOnlyDictionary<int, AlundraBalanceRecord> records)
    {
        foreach (var (index, record) in records)
        {
            _entries[index] = record;
        }
    }

    private static AlundraBalanceRecord? ReadRecord(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var responses = new byte[11];
        var responseElements = element.GetProperty("DamageResponses");
        for (var k = 0; k < responses.Length && k < responseElements.GetArrayLength(); k++)
        {
            responses[k] = (byte)responseElements[k].GetInt32();
        }

        var attacks = Array.Empty<AlundraBalanceAttack>();
        if (element.TryGetProperty("Attacks", out var attackElements) && attackElements.ValueKind == JsonValueKind.Array)
        {
            attacks = new AlundraBalanceAttack[attackElements.GetArrayLength()];
            for (var k = 0; k < attacks.Length; k++)
            {
                attacks[k] = new AlundraBalanceAttack(attackElements[k].GetProperty("AttackAttribute").GetInt32(), attackElements[k].GetProperty("Power").GetInt32());
            }
        }

        AlundraBalanceRecord? next = null;
        if (element.TryGetProperty("NextLevel", out var nextElement))
        {
            next = ReadRecord(nextElement);
        }

        return new AlundraBalanceRecord(
            element.GetProperty("BalanceLevel").GetInt32(), element.GetProperty("RecordSize").GetInt32(), element.GetProperty("MaxHp").GetInt32(), responses, attacks, next);
    }

    /// <summary>
    /// The record of the sprite <paramref name="spriteIndex"/> (<c>EntityManager.SpriteTableIndex</c>: the record's index, plus <c>0x100</c> for a map sprite; 0 for the hero) at the level
    /// <paramref name="threshold"/> (<c>0x80044550</c>): an index in <c>0x1F..0x7F</c> is read as <c>0x1E</c> (<c>0x80044558-0x80044560</c>), then the chain advances while <c>record.Level &lt; threshold</c>
    /// (<c>0x8004458C</c>). Null when the entry is missing (degraded tables, an index outside the table).
    /// </summary>
    public AlundraBalanceRecord? Select(int spriteIndex, int threshold)
    {
        if (spriteIndex >= 0x1F && spriteIndex <= 0x7F)
        {
            spriteIndex = 0x1E;
        }

        if (spriteIndex < 0 || spriteIndex >= EntryCount)
        {
            return null;
        }

        return WalkChain(_entries[spriteIndex], threshold);
    }

    private static AlundraBalanceRecord? WalkChain(AlundraBalanceRecord? record, int threshold)
    {
        while (record != null && record.Level < threshold && record.Next != null)
        {
            record = record.Next;
        }

        return record;
    }

    /// <summary>
    /// E20.c1b K2 (docs/plan-e20-heros.md section 1.6): the record of the item <paramref name="itemId"/> as <c>0x800445C0</c> chooses it: entry <c>itemId + 0x1E</c> (the entries <c>0x1F-0x7F</c> are the 97 items), the chain
    /// walked up to <paramref name="threshold"/>. UNLIKE <see cref="Select"/> there is NO remapping of <c>0x1F-0x7F</c> to <c>0x1E</c> (that is <c>0x80044550</c>, the sprite lookup, which would collapse every item onto one
    /// entry). An item past <c>0x61</c> (the guard <c>n &lt; 0x62</c>; the binary prints and ends the program there) or below 0 gives null.
    /// </summary>
    public AlundraBalanceRecord? SelectEquipment(int itemId, int threshold)
    {
        if ((uint)itemId >= 0x62)
        {
            return null;
        }

        return WalkChain(_entries[itemId + 0x1E], threshold);
    }

    /// <summary>The raw entry (the first level of the chain) of an index, with no remapping; for the tests.</summary>
    internal AlundraBalanceRecord? Entry(int index) => index >= 0 && index < EntryCount ? _entries[index] : null;

    /// <summary>
    /// The balance threshold of a map: its custom property <see cref="ThresholdPropertyName"/> (a string, like <c>Gravity</c>), 0 when the map or the property is missing or not a number. The binary stores it once
    /// at the map load (<c>0x80044520</c>, called from <c>0x8002C3A0</c> with the byte <c>0xB</c> of the map header).
    /// </summary>
    public static int ResolveThreshold(TileMapData? tileMapData)
    {
        if (tileMapData != null && tileMapData.CustomProperties.TryGetValue(ThresholdPropertyName, out var raw) && int.TryParse(raw, out var threshold))
        {
            return threshold;
        }

        return 0;
    }

    private static readonly Dictionary<string, AlundraBalanceTables> SessionCacheByProjectPath = new();

    /// <summary>Returns the cached tables for <paramref name="projectPath"/>, loading them on the first request (the same session cache as <see cref="AlundraItemTables.GetOrCreate"/>).</summary>
    public static AlundraBalanceTables GetOrCreate(string projectPath)
    {
        var key = projectPath ?? string.Empty;
        if (!SessionCacheByProjectPath.TryGetValue(key, out var tables))
        {
            tables = new AlundraBalanceTables(key);
            SessionCacheByProjectPath[key] = tables;
        }

        return tables;
    }

    /// <summary>Test-only: clears the session cache.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();
}
