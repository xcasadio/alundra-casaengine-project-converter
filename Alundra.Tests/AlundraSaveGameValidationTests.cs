#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.c T4 (docs/plan-e16-etat-partie.md, C3, C4, D-E16-32, SC2, SC6): <see cref="AlundraSaveGame.TryValidate"/>,
/// <see cref="AlundraSaveGameRules"/> and <see cref="AlundraMapSizeReader"/> on a temporary project folder (under
/// <see cref="Path.GetTempPath"/>, deleted by <see cref="Dispose"/>) holding a <c>world-index.json</c> and test
/// <c>.tileMap</c> files, a test item table and an injected catalog predicate. Each domain row is checked at its
/// minimum and maximum (accepted), then one past each end (refused, the message naming the field). The tests on
/// the real export are in <see cref="AlundraSaveGameProductionTests"/>. Nothing here reads or writes a save
/// file (D-E16-31).
/// </summary>
public sealed class AlundraSaveGameValidationTests : IDisposable
{
    // ---- Fixture maps: id -> (world path, what the .tileMap holds) -------------------------------------

    private const int Map389 = 389;          // 52 x 60, like every real map (F3).
    private const int Map7 = 7;              // 52 x 60, a second key for the map table.
    private const int BigMap = 100;          // 2000 x 3000: the int-overflow caps bind before the size.
    private const int NoTileMap = 101;
    private const int InvalidJson = 102;
    private const int NoMapSize = 103;
    private const int WidthAsText = 104;
    private const int WidthAsFloat = 105;
    private const int WidthTooBig = 106;
    private const int WidthZero = 107;
    private const int HeightNegative = 108;
    private const int FolderInPlace = 109;
    private const int NotInCatalog = 110;
    private const int MapSizeArray = 111;
    private const int HeightMissing = 112;
    private const int RootArray = 113;

    private const string NotInCatalogMarker = "NotInCatalog";

    private readonly string _projectPath;
    private readonly Dictionary<int, string> _worldPaths = new();
    private readonly AlundraItemTables _itemTables;
    private readonly AlundraSaveGameRules _rules;

    public AlundraSaveGameValidationTests()
    {
        AlundraGameState.Instance.ResetForTests();

        _projectPath = Path.Combine(Path.GetTempPath(), "AlundraSaveGameValidationTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_projectPath);

        AddMap(Map389, "Ship-389", SizeJson(52, 60));
        AddMap(Map7, "Other-7", SizeJson(52, 60));
        AddMap(BigMap, "Big-100", SizeJson(2000, 3000));
        AddMap(NoTileMap, "NoTileMap-101", null);
        AddMap(InvalidJson, "InvalidJson-102", "{ \"map_size\": { \"w\": 52, ");
        AddMap(NoMapSize, "NoMapSize-103", "{ \"name\": \"x\" }");
        AddMap(WidthAsText, "WidthAsText-104", "{ \"map_size\": { \"w\": \"52\", \"h\": 60 } }");
        AddMap(WidthAsFloat, "WidthAsFloat-105", "{ \"map_size\": { \"w\": 52.5, \"h\": 60 } }");
        AddMap(WidthTooBig, "WidthTooBig-106", "{ \"map_size\": { \"w\": 3000000000, \"h\": 60 } }");
        AddMap(WidthZero, "WidthZero-107", SizeJson(0, 60));
        AddMap(HeightNegative, "HeightNegative-108", SizeJson(52, -1));
        AddMap(FolderInPlace, "FolderInPlace-109", null);
        Directory.CreateDirectory(AlundraMapSizeReader.GetTileMapPath(_projectPath, _worldPaths[FolderInPlace])!);
        AddMap(NotInCatalog, NotInCatalogMarker + "-110", SizeJson(52, 60));
        AddMap(MapSizeArray, "MapSizeArray-111", "{ \"map_size\": [52, 60] }");
        AddMap(HeightMissing, "HeightMissing-112", "{ \"map_size\": { \"w\": 52 } }");
        AddMap(RootArray, "RootArray-113", "[ { \"map_size\": { \"w\": 52, \"h\": 60 } } ]");

        var index = _worldPaths.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value);
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(Path.Combine(_projectPath, "Maps", "world-index.json"), JsonSerializer.Serialize(index));

        _itemTables = LoadItemTables(TestItemProperties());
        _rules = new AlundraSaveGameRules(_projectPath, IsInCatalog, _itemTables);
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();

        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private static bool IsInCatalog(string worldPath) => !worldPath.Contains(NotInCatalogMarker, StringComparison.Ordinal);

    private static string SizeJson(int w, int h) => "{ \"name\": \"m\", \"map_size\": { \"w\": " + w + ", \"h\": " + h + " } }";

    /// <summary>Adds a world-index entry, <c>Maps\&lt;name&gt;\&lt;name&gt;.world</c>, and (unless null) its
    /// <c>.tileMap</c> at the path <see cref="AlundraMapSizeReader.GetTileMapPath"/> derives.</summary>
    private void AddMap(int mapId, string name, string? tileMapJson)
    {
        var worldPath = "Maps\\" + name + "\\" + name + ".world";
        _worldPaths[mapId] = worldPath;

        var tileMapPath = AlundraMapSizeReader.GetTileMapPath(_projectPath, worldPath)!;
        Directory.CreateDirectory(Path.GetDirectoryName(tileMapPath)!);
        if (tileMapJson != null)
        {
            File.WriteAllText(tileMapPath, tileMapJson);
        }
    }

    /// <summary>The real rows of <see cref="ItemTablesFixture.RealProperties"/> (item 1 max 1), plus item 61
    /// with max 99 and item 98, the last id, with max 5.</summary>
    private static int[][] TestItemProperties()
    {
        var rows = ItemTablesFixture.RealProperties();
        rows[61] = new[] { 0, 0, 0, 99, 0 };
        rows[98] = new[] { 0, 0, 0, 5, 0 };
        return rows;
    }

    private static AlundraItemTables LoadItemTables(int[][]? properties)
    {
        var path = ItemTablesFixture.Write(properties, null, null);
        try
        {
            return new AlundraItemTables(path);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>A valid save on the fixture's map 389: identity map table (the New Game's, F1), tile (33, 59, 0),
    /// the New Game stats with 2 max MP, no item.</summary>
    private static AlundraSaveGame ValidSave()
    {
        var save = new AlundraSaveGame
        {
            InitialMapId = Map389,
            CameraTileX = 33,
            CameraTileY = 59,
            CameraTileZ = 0,
            Hp = 10,
            HpMax = 10,
            Mp = 0,
            MpMax = 2,
            WeaponId = 1,
        };

        for (var i = 0; i < save.MapIdToInternalMapIndexTable.Length; i++)
        {
            save.MapIdToInternalMapIndexTable[i] = (ushort)i;
        }

        return save;
    }

    private static void Set(AlundraSaveGame save, string field, long value)
    {
        switch (field)
        {
            case "gameTime": save.GameTime = (uint)value; break;
            case "cameraTileX": save.CameraTileX = (int)value; break;
            case "cameraTileY": save.CameraTileY = (int)value; break;
            case "cameraTileZ": save.CameraTileZ = (int)value; break;
            case "playerStats.hpMax": save.HpMax = (short)value; save.Hp = Math.Min(save.Hp, (short)Math.Max(0, value)); break;
            case "playerStats.hp": save.Hp = (short)value; break;
            case "playerStats.mpMax": save.MpMax = (short)value; save.Mp = Math.Min(save.Mp, (short)Math.Max(0, value)); break;
            case "playerStats.mp": save.Mp = (short)value; break;
            case "playerStats.money": save.Money = (short)value; break;
            case "playerStats.weaponId": save.WeaponId = (short)value; break;
            case "playerStats.itemId": save.ItemId = (short)value; break;
            case "playerStats.falconTemp": save.FalconTemp = (short)value; break;
            case "playerStats.falcon": save.Falcon = (short)value; break;
            case "deathRetryCount": save.DeathRetryCount = (byte)value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "unknown field");
        }
    }

    private void AssertAccepted(AlundraSaveGame save)
    {
        var valid = save.TryValidate(_rules, out var error);

        Assert.True(valid, error);
        Assert.Equal(string.Empty, error);
    }

    private void AssertRefused(AlundraSaveGame save, string expectedPrefix)
    {
        var valid = save.TryValidate(_rules, out var error);

        Assert.False(valid);
        Assert.StartsWith(expectedPrefix, error);
    }

    // ---- The baseline -------------------------------------------------------------------------------

    [Fact]
    public void ValidSave_IsAccepted()
    {
        AssertAccepted(ValidSave());
    }

    // ---- Each row of the domain table: minimum and maximum accepted --------------------------------

    [Theory]
    [InlineData("gameTime", 0L)]
    [InlineData("gameTime", (long)AlundraGameState.GameTimeMax)]
    [InlineData("cameraTileX", 0L)]
    [InlineData("cameraTileX", 51L)]
    [InlineData("cameraTileY", 0L)]
    [InlineData("cameraTileY", 59L)]
    [InlineData("cameraTileZ", 0L)]
    [InlineData("cameraTileZ", 256L)]
    [InlineData("playerStats.hpMax", 0L)]
    [InlineData("playerStats.hpMax", 50L)]
    [InlineData("playerStats.hp", 0L)]
    [InlineData("playerStats.hp", 10L)] // = hpMax
    [InlineData("playerStats.mpMax", 0L)]
    [InlineData("playerStats.mpMax", 4L)]
    [InlineData("playerStats.mp", 0L)]
    [InlineData("playerStats.mp", 2L)] // = mpMax
    [InlineData("playerStats.money", 0L)]
    [InlineData("playerStats.money", 9999L)]
    [InlineData("playerStats.weaponId", -1L)]
    [InlineData("playerStats.weaponId", 1L)]
    [InlineData("playerStats.weaponId", 6L)]
    [InlineData("playerStats.itemId", 0L)]
    [InlineData("playerStats.itemId", 98L)]
    [InlineData("playerStats.falconTemp", 0L)]
    [InlineData("playerStats.falconTemp", 50L)]
    [InlineData("playerStats.falcon", 0L)]
    [InlineData("playerStats.falcon", 50L)]
    [InlineData("deathRetryCount", 0L)]
    [InlineData("deathRetryCount", 255L)]
    public void FieldAtItsBound_IsAccepted(string field, long value)
    {
        var save = ValidSave();
        Set(save, field, value);

        AssertAccepted(save);
    }

    // ---- ... and one past each end refused, naming the field ------------------------------------------

    [Theory]
    [InlineData("gameTime", (long)AlundraGameState.GameTimeMax + 1)]
    [InlineData("gameTime", (long)uint.MaxValue)]
    [InlineData("cameraTileX", 52L)]
    [InlineData("cameraTileX", -1L)]
    [InlineData("cameraTileY", 60L)]
    [InlineData("cameraTileY", -1L)]
    [InlineData("cameraTileZ", 257L)]
    [InlineData("cameraTileZ", -1L)]
    [InlineData("playerStats.hpMax", 51L)]
    [InlineData("playerStats.hpMax", -1L)]
    [InlineData("playerStats.hp", 11L)] // hpMax + 1
    [InlineData("playerStats.hp", -1L)]
    [InlineData("playerStats.mpMax", 5L)]
    [InlineData("playerStats.mpMax", -1L)]
    [InlineData("playerStats.mp", 3L)] // mpMax + 1
    [InlineData("playerStats.mp", -1L)]
    [InlineData("playerStats.money", 10000L)]
    [InlineData("playerStats.money", -1L)]
    [InlineData("playerStats.weaponId", 0L)]
    [InlineData("playerStats.weaponId", 7L)]
    [InlineData("playerStats.weaponId", -2L)]
    [InlineData("playerStats.itemId", 99L)]
    [InlineData("playerStats.itemId", -1L)]
    [InlineData("playerStats.falconTemp", 51L)]
    [InlineData("playerStats.falconTemp", -1L)]
    [InlineData("playerStats.falcon", 51L)]
    [InlineData("playerStats.falcon", -1L)]
    public void FieldOutsideItsDomain_IsRefused_NamingTheFieldAndTheValue(string field, long value)
    {
        var save = ValidSave();
        Set(save, field, value);

        AssertRefused(save, field + " = " + value + ", outside ");
    }

    [Fact]
    public void RefusalMessage_NamesFieldValueAndDomain()
    {
        var save = ValidSave();
        save.Money = -1;

        Assert.False(save.TryValidate(_rules, out var error));
        Assert.Equal("playerStats.money = -1, outside 0..9999", error);
    }

    [Fact]
    public void Hp_IsBoundedByTheSavesOwnHpMax()
    {
        var save = ValidSave();
        save.HpMax = 50;
        save.Hp = 50;
        AssertAccepted(save);

        save.HpMax = 20;
        AssertRefused(save, "playerStats.hp = 50, outside 0..20");
    }

    [Fact]
    public void Mp_IsBoundedByTheSavesOwnMpMax()
    {
        var save = ValidSave();
        save.MpMax = 4;
        save.Mp = 4;
        AssertAccepted(save);

        save.MpMax = 1;
        AssertRefused(save, "playerStats.mp = 4, outside 0..1");
    }

    // ---- Tile: the int-overflow caps of C3 --------------------------------------------------------------

    [Theory]
    [InlineData(1364, 2047)]
    [InlineData(0, 0)]
    public void BigMap_TileAtTheOverflowCaps_IsAccepted(int x, int y)
    {
        var save = ValidSave();
        save.InitialMapId = BigMap;
        save.CameraTileX = x;
        save.CameraTileY = y;

        AssertAccepted(save);
    }

    [Fact]
    public void BigMap_TileXPastTheOverflowCap_IsRefused_ThoughInsideTheMap()
    {
        var save = ValidSave();
        save.InitialMapId = BigMap;
        save.CameraTileX = 1365; // < 2000, but (1365 * 24 + 12) << 16 overflows an int.

        AssertRefused(save, "cameraTileX = 1365, outside 0..1364");
    }

    [Fact]
    public void BigMap_TileYPastTheOverflowCap_IsRefused_ThoughInsideTheMap()
    {
        var save = ValidSave();
        save.InitialMapId = BigMap;
        save.CameraTileY = 2048; // < 3000, but (2048 * 16 + 8) << 16 overflows an int.

        AssertRefused(save, "cameraTileY = 2048, outside 0..2047");
    }

    [Fact]
    public void TileCaps_KeepTheSpawnArithmeticInsideAnInt()
    {
        // AlundraWorldProxy's spawn: (tile * TileWidth + TileWidth / 2) << 16, TileWidth 24, TileHeight 16.
        Assert.True((AlundraSaveGame.MaxCameraTileX * 24L + 12) << 16 <= int.MaxValue);
        Assert.True(((AlundraSaveGame.MaxCameraTileX + 1) * 24L + 12) << 16 > int.MaxValue);
        Assert.True((AlundraSaveGame.MaxCameraTileY * 16L + 8) << 16 <= int.MaxValue);
        Assert.True(((AlundraSaveGame.MaxCameraTileY + 1) * 16L + 8) << 16 > int.MaxValue);
        Assert.True((long)AlundraSaveGame.MaxCameraTileZ << 20 <= int.MaxValue);
    }

    // ---- Map table (C3, F1) ----------------------------------------------------------------------------

    [Fact]
    public void MapTable_NewGameIdentity_IsAccepted_EvenBeyondTheIndexKeys()
    {
        var save = ValidSave();
        Assert.Equal((ushort)499, save.MapIdToInternalMapIndexTable[499]); // 499 is no key of this index.

        AssertAccepted(save);
    }

    [Fact]
    public void MapTable_ValueThatIsAKey_IsAccepted()
    {
        var save = ValidSave();
        save.MapIdToInternalMapIndexTable[5] = Map7;
        save.MapIdToInternalMapIndexTable[439] = Map389;

        AssertAccepted(save);
    }

    [Theory]
    [InlineData(5, 450)]
    [InlineData(0, 65535)]
    [InlineData(499, 498)]
    public void MapTable_ValueNeitherAKeyNorItsOwnIndex_IsRefused(int index, int value)
    {
        var save = ValidSave();
        save.MapIdToInternalMapIndexTable[index] = (ushort)value;

        AssertRefused(save, $"mapIdToInternalMapIndexTable[{index}] = {value}, ");
    }

    // ---- initialMapId (C3, C4, D-E16-32, SC6) -------------------------------------------------------

    [Theory]
    [InlineData(9999)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void InitialMapId_NotAKeyOfTheIndex_IsRefused(int mapId)
    {
        var save = ValidSave();
        save.InitialMapId = mapId;

        AssertRefused(save, $"initialMapId = {mapId}, not a key of world-index.json");
    }

    [Theory]
    [InlineData(NotInCatalog)]
    [InlineData(NoTileMap)]
    [InlineData(InvalidJson)]
    [InlineData(NoMapSize)]
    [InlineData(HeightMissing)]
    [InlineData(MapSizeArray)]
    [InlineData(RootArray)]
    [InlineData(WidthAsText)]
    [InlineData(WidthAsFloat)]
    [InlineData(WidthTooBig)]
    [InlineData(WidthZero)]
    [InlineData(HeightNegative)]
    [InlineData(FolderInPlace)]
    public void InitialMapId_WhoseWorldOrSizeIsUnusable_IsRefused_WithoutThrowing(int mapId)
    {
        var save = ValidSave();
        save.InitialMapId = mapId;
        save.CameraTileX = 0;
        save.CameraTileY = 0;

        AssertRefused(save, $"initialMapId = {mapId}, ");
    }

    [Fact]
    public void CatalogPredicateThatThrows_IsARefusal()
    {
        var rules = new AlundraSaveGameRules(_projectPath, _ => throw new InvalidOperationException("catalog down"), _itemTables);

        var valid = ValidSave().TryValidate(rules, out var error);

        Assert.False(valid);
        Assert.StartsWith("initialMapId = 389, the catalog lookup", error);
        Assert.Contains("catalog down", error);
    }

    [Fact]
    public void CatalogPredicate_GetsTheWorldIndexPath_AndIsNotAskedForAnAbsentKey()
    {
        var asked = new List<string>();
        var rules = new AlundraSaveGameRules(_projectPath, path => { asked.Add(path); return true; }, _itemTables);

        var absent = ValidSave();
        absent.InitialMapId = 9999;
        Assert.False(absent.TryValidate(rules, out _));
        Assert.Empty(asked);

        Assert.True(ValidSave().TryValidate(rules, out var error), error);
        Assert.Equal(new[] { _worldPaths[Map389] }, asked);
    }

    [Fact]
    public void MissingWorldIndex_RefusesEverySave()
    {
        var emptyProject = Path.Combine(Path.GetTempPath(), "AlundraSaveGameValidationTests_Empty_" + Guid.NewGuid());
        Directory.CreateDirectory(emptyProject);
        try
        {
            var rules = new AlundraSaveGameRules(emptyProject, _ => true, _itemTables);

            Assert.False(ValidSave().TryValidate(rules, out var error));
            Assert.StartsWith("initialMapId = 389, not a key", error);
        }
        finally
        {
            Directory.Delete(emptyProject, recursive: true);
        }
    }

    // ---- The map-size reader ------------------------------------------------------------------------------

    [Fact]
    public void MapSizeReader_ReadsTheFixturesMapSize()
    {
        Assert.True(AlundraMapSizeReader.TryRead(_projectPath, _worldPaths[Map389], out var w, out var h, out var error), error);
        Assert.Equal(52, w);
        Assert.Equal(60, h);
    }

    [Fact]
    public void MapSizeReader_DerivesTheTileMapPathLikeTheEventLoader()
    {
        var path = AlundraMapSizeReader.GetTileMapPath(_projectPath, "Maps\\The Klark\\Ship-389\\Ship-389.world");

        Assert.Equal(Path.Combine(_projectPath, "Maps", "The Klark", "Ship-389", "tilemap", "Ship-389.tileMap"), path);
    }

    [Theory]
    [InlineData(NoTileMap)]
    [InlineData(InvalidJson)]
    [InlineData(NoMapSize)]
    [InlineData(WidthAsText)]
    [InlineData(WidthAsFloat)]
    [InlineData(WidthTooBig)]
    [InlineData(MapSizeArray)]
    [InlineData(RootArray)]
    [InlineData(HeightMissing)]
    [InlineData(FolderInPlace)]
    public void MapSizeReader_Unreadable_ReturnsFalseWithAReason_WithoutThrowing(int mapId)
    {
        var read = AlundraMapSizeReader.TryRead(_projectPath, _worldPaths[mapId], out var w, out var h, out var error);

        Assert.False(read);
        Assert.Equal(0, w);
        Assert.Equal(0, h);
        Assert.False(string.IsNullOrEmpty(error));
    }

    // ---- Data version (SC2) -------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void LoadedDataVersionOtherThan1_IsRefused_NamingTheVersion(int version)
    {
        var save = ValidSave();
        save.LoadedDataVersion = version;

        AssertRefused(save, $"data version {version} is not supported");
    }

    [Fact]
    public void DataVersion_IsCheckedFirst()
    {
        var save = ValidSave();
        save.LoadedDataVersion = 0;
        save.Money = -1; // also invalid, but the version comes first.

        AssertRefused(save, "data version 0");
    }

    // ---- Item counters (the last field checked) -----------------------------------------------------------

    [Theory]
    [InlineData(3, 1)]    // item 1, max 1.
    [InlineData(123, 99)] // item 61, max 99.
    [InlineData(197, 5)]  // item 98, the last id, max 5.
    [InlineData(3, 0)]
    public void ItemCounter_UpToTheTablesMax_IsAccepted(int index, short count)
    {
        var save = ValidSave();
        save.NumberOfItems[index] = count;

        AssertAccepted(save);
    }

    [Theory]
    [InlineData(3, 2, 1)]     // item 1: max + 1.
    [InlineData(3, -1, 1)]    // min - 1.
    [InlineData(123, 100, 99)]
    [InlineData(197, 6, 5)]
    [InlineData(101, 1, 0)]   // item 50, max 0 in the table.
    [InlineData(0, 1, 0)]     // even indices stay 0.
    [InlineData(2, 1, 0)]
    [InlineData(196, 1, 0)]
    [InlineData(199, 1, 0)]   // odd, but item 99 is past ItemsCount.
    [InlineData(198, 1, 0)]
    [InlineData(255, 1, 0)]
    [InlineData(255, short.MinValue, 0)]
    public void ItemCounter_OutsideItsDomain_IsRefused_NamingTheIndex(int index, short count, int max)
    {
        var save = ValidSave();
        save.NumberOfItems[index] = count;

        AssertRefused(save, $"numberOfItems[{index}] = {count}, outside 0..{max}");
    }

    [Fact]
    public void DegradedItemTable_RefusesEveryNonZeroCounter()
    {
        var degraded = LoadItemTables(null); // no items-properties.json: every ceiling is 0 (F11).
        var rules = new AlundraSaveGameRules(_projectPath, IsInCatalog, degraded);
        Assert.All(degraded.ItemsProperties, value => Assert.Equal((ushort)0, value));

        Assert.True(ValidSave().TryValidate(rules, out var error), error); // all counters zero.

        var save = ValidSave();
        save.NumberOfItems[3] = 1;
        Assert.False(save.TryValidate(rules, out error));
        Assert.StartsWith("numberOfItems[3] = 1, outside 0..0", error);
    }

    // ---- A refusal leaves the live state untouched ------------------------------------------------------

    [Fact]
    public void InvalidLastCheckedField_IsRefused_AndTheLiveStateIsUnchanged()
    {
        var live = AlundraGameState.Instance;
        live.GameFlags[3] = 0xABCD;
        live.GameFlags[700] = 0x1;
        live.TemporaryFlags[2] = 0x55;
        live.MapIdToInternalMapIndexTable[12] = 7;
        live.NumberOfItems[3] = 1;
        live.PlayerStats.Hp = 7;
        live.PlayerStats.Money = 1234;
        live.PlayerStats.Falcon = 3;
        live.PlayerControlFlags = AlundraGameState.PlayerControlBits.ControlLocked;
        live.GameTime = 4242;
        live.DeathRetryCount = 2;
        var before = Snapshot(live);

        // A save that differs from the live state in every field, valid except its last checked one.
        var save = ValidSave();
        for (var i = 0; i < save.GameFlags.Length; i++)
        {
            save.GameFlags[i] = 0xFFFFFFFF;
        }

        save.MapIdToInternalMapIndexTable[12] = Map389;
        save.HpMax = 50;
        save.Hp = 49;
        save.MpMax = 4;
        save.Mp = 3;
        save.Money = 9999;
        save.WeaponId = 6;
        save.ItemId = 98;
        save.FalconTemp = 50;
        save.Falcon = 50;
        save.GameTime = 99;
        save.DeathRetryCount = 200;
        save.NumberOfItems[255] = 1; // the last field checked.

        // E16.d's order: validate, and apply only on success.
        if (save.TryValidate(_rules, out var error))
        {
            save.ApplyTo(live);
        }

        Assert.StartsWith("numberOfItems[255] = 1", error);
        Assert.Equal(before, Snapshot(live));
    }

    /// <summary>Every array, the nine stats, the control flags, the game time and the retry counter, as one
    /// comparable string.</summary>
    private static string Snapshot(AlundraGameState state)
    {
        var stats = state.PlayerStats;
        return string.Join(
            "|",
            string.Join(",", state.GameFlags),
            string.Join(",", state.TemporaryFlags),
            string.Join(",", state.MapIdToInternalMapIndexTable),
            string.Join(",", state.NumberOfItems),
            string.Join(",", state.GameVariables),
            state.TextCategoryIndex,
            stats.Hp, stats.HpMax, stats.Mp, stats.MpMax, stats.Money, stats.WeaponId, stats.ItemId, stats.FalconTemp, stats.Falcon,
            state.PlayerControlFlags,
            state.GameTime,
            state.DeathRetryCount);
    }
}
