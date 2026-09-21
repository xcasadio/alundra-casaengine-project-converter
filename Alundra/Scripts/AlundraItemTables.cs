#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

/// <summary>
/// The three item tables the converter republishes raw under <c>Data/</c> (docs/plan-e13c-icones-hud.md,
/// slices S2 and S2.b), loaded once from <see cref="EngineEnvironment.ProjectPath"/> - the same project-root
/// resolution <see cref="AlundraSoundGroupIndexTable"/> and <see cref="SpriteRecordCatalog"/> use:
/// <list type="bullet">
/// <item><c>items-properties.json</c> - <c>g_itemsProperties</c> (StaticVariables.cs:737-838), 100 rows of
/// 5 columns: inventory slot, replacement flag, replacement priority, max count, icon. Exposed flattened as
/// <see cref="ItemsProperties"/>, so the ported code indexes it exactly as the original does,
/// <c>[itemId * 5 + column]</c>.</item>
/// <item><c>item-drop-properties.json</c> - <c>g_itemDropProperties</c> (StaticVariables.cs:841), 98
/// records. Only <c>Field3</c> is read by anything ported so far, the New Game's unlock loop
/// (GameInitializer.cs:402), so only it is kept, as <see cref="DropField3"/>.</item>
/// <item><c>item-icon-index.json</c> - each item that has a portrait, mapped to the asset id of the
/// <c>.sprite</c> the converter already emitted for it (88 items). Read through
/// <see cref="TryGetIconAssetId"/>.</item>
/// </list>
///
/// <b>Degraded mode</b>: a file that is missing, unparsable or shorter than its table logs one warning and
/// leaves the missing entries at zero - never an exception. Zeros are chosen on purpose rather than a
/// separate "not loaded" state: no item then has a slot, a count or an unlock bit, so every lookup the port
/// makes returns the original's own sentinel, and a broken export simply shows no icon.
/// </summary>
public sealed class AlundraItemTables
{
    private const string DataDirectoryName = "Data";
    private const string PropertiesFileName = "items-properties.json";
    private const string DropPropertiesFileName = "item-drop-properties.json";
    private const string IconIndexFileName = "item-icon-index.json";

    /// <summary>Rows of <c>g_itemsProperties</c>: 100, item ids 0..99 (StaticVariables.cs:737-838).</summary>
    public const int ItemRowCount = 100;

    /// <summary>Columns of <c>g_itemsProperties</c> (StaticVariables.cs:729-734).</summary>
    public const int ItemColumnCount = 5;

    /// <summary>Records of <c>g_itemDropProperties</c>: 98, the bound of the New Game's unlock loop
    /// (GameInitializer.cs:408, <c>0x62</c>).</summary>
    public const int DropRecordCount = 98;

    // The republished drop record holds four bytes (Field1, SoundSfxIndex, Field3, Field4); Field3 is the
    // third of them.
    private const int DropColumnCount = 4;
    private const int DropField3Column = 2;

    private readonly ushort[] _itemsProperties = new ushort[ItemRowCount * ItemColumnCount];
    private readonly byte[] _dropField3 = new byte[DropRecordCount];
    private readonly Dictionary<int, Guid> _iconAssetIdByItemId = new();

    /// <summary>Loads from <c>Data/</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public AlundraItemTables() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads from <c>Data/</c> under <paramref name="projectPath"/> - the overload tests use to point
    /// at a temporary fixture directory instead of the real project.</summary>
    public AlundraItemTables(string projectPath)
    {
        var dataPath = Path.Combine(projectPath, DataDirectoryName);

        LoadRowTable(
            Path.Combine(dataPath, PropertiesFileName), ItemRowCount, ItemColumnCount,
            (row, column, value) => _itemsProperties[row * ItemColumnCount + column] = (ushort)value,
            () => Array.Clear(_itemsProperties));

        LoadRowTable(
            Path.Combine(dataPath, DropPropertiesFileName), DropRecordCount, DropColumnCount,
            (row, column, value) =>
            {
                if (column == DropField3Column)
                {
                    _dropField3[row] = (byte)value;
                }
            },
            () => Array.Clear(_dropField3));

        LoadIconIndex(Path.Combine(dataPath, IconIndexFileName));
    }

    /// <summary><c>g_itemsProperties</c> flattened, read as <c>[itemId * 5 + column]</c> exactly like the
    /// original (e.g. PlayerManager.cs:4384, GraphicManager.cs:1913). 500 entries, zero where degraded.</summary>
    public IReadOnlyList<ushort> ItemsProperties => _itemsProperties;

    /// <summary><c>g_itemDropProperties[itemId].Field3</c> for items 0..97: its high bit unlocks the item at
    /// the start of a New Game, its low seven bits mean something else to the game. Zero where degraded.</summary>
    public IReadOnlyList<byte> DropField3 => _dropField3;

    /// <summary>The asset id of the <c>.sprite</c> that is <paramref name="itemId"/>'s portrait, or false for
    /// an item without one (item 42, the eleven items whose icon column is <c>0xFFFF</c>) or when the index
    /// failed to load.</summary>
    public bool TryGetIconAssetId(int itemId, out Guid assetId) => _iconAssetIdByItemId.TryGetValue(itemId, out assetId);

    private static void LoadRowTable(
        string filePath, int rowCount, int columnCount, Action<int, int, int> store, Action clear)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning($"AlundraItemTables: '{filePath}' not found; its entries read as zero (degraded mode).");
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            var rows = document.RootElement;
            var readRows = Math.Min(rows.GetArrayLength(), rowCount);

            for (var row = 0; row < readRows; row++)
            {
                var values = rows[row];
                for (var column = 0; column < columnCount && column < values.GetArrayLength(); column++)
                {
                    store(row, column, values[column].GetInt32());
                }
            }

            if (rows.GetArrayLength() != rowCount)
            {
                Logs.WriteWarning(
                    $"AlundraItemTables: '{filePath}' holds {rows.GetArrayLength()} rows, {rowCount} expected; "
                    + "missing rows read as zero, extra rows are ignored.");
            }
        }
        catch (Exception ex)
        {
            clear();
            Logs.WriteWarning(
                $"AlundraItemTables: failed to load '{filePath}' ({ex.Message}); its entries read as zero "
                + "(degraded mode).");
        }
    }

    private void LoadIconIndex(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning($"AlundraItemTables: '{filePath}' not found; no item has an icon (degraded mode).");
                return;
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (parsed == null)
            {
                Logs.WriteWarning($"AlundraItemTables: '{filePath}' parsed to nothing; no item has an icon (degraded mode).");
                return;
            }

            foreach (var (key, value) in parsed)
            {
                if (int.TryParse(key, out var itemId) && Guid.TryParse(value, out var assetId))
                {
                    _iconAssetIdByItemId[itemId] = assetId;
                }
            }
        }
        catch (Exception ex)
        {
            _iconAssetIdByItemId.Clear();
            Logs.WriteWarning(
                $"AlundraItemTables: failed to load '{filePath}' ({ex.Message}); no item has an icon (degraded mode).");
        }
    }

    /// <summary>
    /// Session-scoped cache keyed by project path - same shape and rationale as
    /// <see cref="AlundraSoundGroupIndexTable"/>'s own: two consecutive worlds over the same project read the
    /// three files only once, while two different projects never share cached tables.
    /// </summary>
    private static readonly Dictionary<string, AlundraItemTables> SessionCacheByProjectPath = new();

    /// <summary>Returns the cached tables for <paramref name="projectPath"/>, loading them on the first
    /// request.</summary>
    public static AlundraItemTables GetOrCreate(string projectPath)
    {
        if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var tables))
        {
            tables = new AlundraItemTables(projectPath);
            SessionCacheByProjectPath[projectPath] = tables;
        }

        return tables;
    }

    /// <summary>Test-only: clears the session cache so tests do not leak tables into each other through
    /// <see cref="GetOrCreate"/>.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();
}
