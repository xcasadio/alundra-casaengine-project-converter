#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using Alundra.Scripts;

namespace Alundra.Combat;

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
            var byFileOffset = new Dictionary<int, AlundraBalanceRecord>();
            for (var i = 0; i < count; i++)
            {
                _entries[i] = ReadRecord(entries[i], byFileOffset);
            }

            // E14.a1b: the records are contiguous in the file; link each one to the record stored right after it.
            foreach (var record in byFileOffset.Values)
            {
                if (byFileOffset.TryGetValue(record.FileOffset + record.RecordSize, out var following))
                {
                    record.Following = following;
                }
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

    private static AlundraBalanceRecord? ReadRecord(JsonElement element, Dictionary<int, AlundraBalanceRecord> byFileOffset)
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
            next = ReadRecord(nextElement, byFileOffset);
        }

        var record = new AlundraBalanceRecord(
            element.GetProperty("BalanceLevel").GetInt32(), element.GetProperty("RecordSize").GetInt32(), element.GetProperty("MaxHp").GetInt32(), responses, attacks, next);
        if (element.TryGetProperty("FileOffset", out var offsetElement) && offsetElement.ValueKind == JsonValueKind.Number)
        {
            record.FileOffset = offsetElement.GetInt32();
            byFileOffset[record.FileOffset] = record;
        }

        return record;
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
