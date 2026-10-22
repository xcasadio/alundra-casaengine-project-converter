#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Items;

/// <summary>
/// Writes a temporary project directory holding the three <c>Data/</c> item tables, the way
/// <see cref="SoundGroupIndexTableTests"/> writes its own fixture. The default rows are the real ones
/// (alundra-datas-analyser ItemsProperties.csv and ItemDropProperties.csv) for every item the New Game
/// touches: item 0, the four slot-1 weapons 1..4, and the two other items it unlocks, 17 and 25. Every other
/// row is zero, which the loader and the ported code read as "no such item".
/// </summary>
internal static class ItemTablesFixture
{
    /// <summary>Item 1's real portrait asset id, from the exported <c>Data/item-icon-index.json</c>.</summary>
    public static readonly Guid SwordIconAssetId = Guid.Parse("aeebd7a0-faa7-57b0-a844-34470272a4eb");

    /// <summary>Arbitrary, distinct from the sword's - an icon for item 17, which is not a weapon.</summary>
    public static readonly Guid Item17IconAssetId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static int[][] RealProperties()
    {
        var rows = NewRows(ItemTables.ItemRowCount, ItemTables.ItemColumnCount);
        rows[0] = new[] { 0, 0, 0, 0, 65535 };
        rows[1] = new[] { 1, 1, 0, 1, 31 };
        rows[2] = new[] { 1, 1, 1, 1, 32 };
        rows[3] = new[] { 1, 1, 2, 1, 33 };
        rows[4] = new[] { 1, 1, 3, 1, 34 };
        rows[17] = new[] { 7, 1, 0, 1, 47 };
        rows[25] = new[] { 9, 1, 0, 1, 55 };
        return rows;
    }

    public static int[][] RealDrops()
    {
        var rows = NewRows(ItemTables.DropRecordCount, 4);
        rows[1] = new[] { 0, 0, 129, 1 };
        rows[17] = new[] { 0, 0, 130, 1 };
        rows[25] = new[] { 0, 0, 128, 1 };
        return rows;
    }

    public static Dictionary<int, Guid> RealIcons() => new()
    {
        [1] = SwordIconAssetId,
        [17] = Item17IconAssetId,
    };

    /// <summary>Writes the three files (a null table is simply not written) and returns the project path.
    /// The caller deletes it.</summary>
    public static string Write(int[][]? properties, int[][]? drops, Dictionary<int, Guid>? icons)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraItemTablesFixture_" + Guid.NewGuid());
        var dataPath = Path.Combine(projectPath, "Data");
        Directory.CreateDirectory(dataPath);

        if (properties != null)
        {
            File.WriteAllText(Path.Combine(dataPath, "items-properties.json"), ToJson(properties));
        }

        if (drops != null)
        {
            File.WriteAllText(Path.Combine(dataPath, "item-drop-properties.json"), ToJson(drops));
        }

        if (icons != null)
        {
            var entries = new List<string>();
            foreach (var (itemId, assetId) in icons)
            {
                entries.Add($"\"{itemId}\": \"{assetId}\"");
            }

            File.WriteAllText(Path.Combine(dataPath, "item-icon-index.json"), "{" + string.Join(",", entries) + "}");
        }

        return projectPath;
    }

    /// <summary>The real tables, loaded through <see cref="ItemTables"/> itself.</summary>
    public static ItemTables LoadReal()
    {
        var projectPath = Write(RealProperties(), RealDrops(), RealIcons());
        try
        {
            return new ItemTables(projectPath);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    private static int[][] NewRows(int rowCount, int columnCount)
    {
        var rows = new int[rowCount][];
        for (var i = 0; i < rowCount; i++)
        {
            rows[i] = new int[columnCount];
        }

        return rows;
    }

    private static string ToJson(int[][] rows)
    {
        var lines = new List<string>();
        foreach (var row in rows)
        {
            lines.Add("[" + string.Join(",", row) + "]");
        }

        return "[" + string.Join(",", lines) + "]";
    }
}
