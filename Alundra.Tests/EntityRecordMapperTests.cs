using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

public class EntityRecordMapperTests
{
    /// <summary>
    /// Full key set as emitted by AlundraDataExtractor.TiledMapExporter.CreateEntityProperties for one
    /// "Entities" object-layer record, using the map 389 / Entity_0 values documented in
    /// docs/demarrage-nouvelle-partie.md section 1.2 for the keys it lists, and internally consistent
    /// synthetic values for the remaining keys the writer also emits but the doc excerpt does not show.
    /// </summary>
    private static Dictionary<string, string> BuildFullMap389Entity0Properties() => new()
    {
        ["Index"] = "0",
        ["XMin"] = "10",
        ["YMin"] = "12",
        ["XMax"] = "30",
        ["YMax"] = "32",
        ["IsEnabled"] = "1",
        ["SpriteDirection"] = "64",
        ["SpriteTableIndex"] = "25",
        ["XPos"] = "34",
        ["YPos"] = "72",
        ["Height"] = "46",
        ["EventCodesA_LoadIndex"] = "133",
        ["EventCodesB_MapIndex"] = "5",
        ["EventCodesC_TickIndex"] = "9",
        ["EventCodesD_TouchIndex"] = "13",
        ["EventCodesE_DeactivateIndex"] = "17",
        ["EventCodesF_InteractIndex"] = "21",
        ["_10"] = "0",
        ["Contents"] = "0",
        ["DisplayX"] = "17",
        ["DisplayY"] = "36",
        ["DisplayHeight"] = "23",
        ["DisplayPixelX"] = "272",
        ["DisplayPixelY"] = "208",
        ["EntityName"] = "Bloc transparent (1x1x2)",
    };

    [Fact]
    public void Map_FullMap389Entity0Record_FillsAllMappedFields()
    {
        var properties = BuildFullMap389Entity0Properties();
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_0", properties, proxy);

        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(25u, proxy.SpriteTableIndex);
        Assert.Equal(133, proxy.ProgramIndexes[0]);
        Assert.Equal(5, proxy.ProgramIndexes[1]);
        Assert.Equal(9, proxy.ProgramIndexes[2]);
        Assert.Equal(13, proxy.ProgramIndexes[3]);
        Assert.Equal(17, proxy.ProgramIndexes[4]);
        Assert.Equal(21, proxy.ProgramIndexes[5]);
        Assert.Equal(0, proxy.ContentsGameFlag);
        Assert.Equal(420 * 0x10000, proxy.PosX);
        Assert.Equal(584 * 0x10000, proxy.PosY);
        Assert.Equal(46 << 19, proxy.PosZ);
        Assert.Equal(17, proxy.TileX);
        Assert.Equal(36, proxy.TileY);
        Assert.Equal(23, proxy.TileZ);
    }

    [Fact]
    public void Map_UnmappedKeys_DoNotAffectAnyProxyField()
    {
        var properties = BuildFullMap389Entity0Properties();
        var mapped = new AlundraEntityScriptProxy();
        EntityRecordMapper.Map("Entity_0", properties, mapped);

        // Same record but with every deliberately-unmapped key stripped: the mapped fields must be
        // unaffected, since the mapper never reads those keys. XPos/YPos/Height are mapped (see
        // Map_XPosYPosHeight_FillPositionAndTileFields above) so they stay out of this list.
        var unmappedKeys = new[]
        {
            "XMin", "YMin", "XMax", "YMax", "IsEnabled", "SpriteDirection",
            "Contents", "DisplayX", "DisplayY", "DisplayHeight", "DisplayPixelX", "DisplayPixelY", "EntityName",
        };
        foreach (var key in unmappedKeys)
        {
            properties.Remove(key);
        }

        var stripped = new AlundraEntityScriptProxy();
        EntityRecordMapper.Map("Entity_0", properties, stripped);

        Assert.Equal(mapped.EntityRefId, stripped.EntityRefId);
        Assert.Equal(mapped.SpriteTableIndex, stripped.SpriteTableIndex);
        Assert.Equal(mapped.ProgramIndexes, stripped.ProgramIndexes);
        Assert.Equal(mapped.ContentsGameFlag, stripped.ContentsGameFlag);
        Assert.Equal(mapped.PosX, stripped.PosX);
        Assert.Equal(mapped.PosY, stripped.PosY);
        Assert.Equal(mapped.PosZ, stripped.PosZ);
    }

    [Fact]
    public void Map_MissingKeys_LeaveProxyFieldsAtDefault()
    {
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_missing", new Dictionary<string, string>(), proxy);

        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(0u, proxy.SpriteTableIndex);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, proxy.ProgramIndexes);
        Assert.Equal(0, proxy.ContentsGameFlag);
        Assert.Equal(0, proxy.PosX);
        Assert.Equal(0, proxy.PosY);
        Assert.Equal(0, proxy.PosZ);
    }

    [Theory]
    [InlineData("SpriteTableIndex")]
    [InlineData("EventCodesA_LoadIndex")]
    [InlineData("_10")]
    public void Map_MalformedValue_ThrowsFormatExceptionNamingRecordAndKey(string key)
    {
        var properties = new Dictionary<string, string> { [key] = "not-an-int" };
        var proxy = new AlundraEntityScriptProxy();

        var exception = Assert.Throws<FormatException>(
            () => EntityRecordMapper.Map("Entity_7", properties, proxy));

        Assert.Contains("Entity_7", exception.Message);
        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void Map_ContentsGameFlag_ZeroedWhenMaskedValueOutOfRange()
    {
        var properties = new Dictionary<string, string> { ["_10"] = "2048" }; // (2048 & 0x7fff) = 2048 > 0x7ff
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_0", properties, proxy);

        Assert.Equal(0, proxy.ContentsGameFlag);
    }

    [Fact]
    public void Map_ContentsGameFlag_KeptWhenMaskedValueInRange()
    {
        var properties = new Dictionary<string, string> { ["_10"] = "42" };
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_0", properties, proxy);

        Assert.Equal(42, proxy.ContentsGameFlag);
    }

    [Fact]
    public void Map_XPosYPosHeight_FillPositionAndTileFields()
    {
        // Map 389 / Entity_0 anchor (docs/demarrage-nouvelle-partie.md section 1.2), confirmed against
        // GameEngine.SpawnEntity (GameEngine.cs:743-751) and PhysicsEngine.cs:1698-1700:
        // PosX = (34*12+12)*0x10000 = 420*0x10000, TileX = 420/24 = 17
        // PosY = (72*8+8)*0x10000  = 584*0x10000, TileY = 584/16 = 36
        // PosZ = 46<<19 = 23*0x100000,             TileZ = 23
        var properties = new Dictionary<string, string>
        {
            ["XPos"] = "34",
            ["YPos"] = "72",
            ["Height"] = "46",
        };
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_0", properties, proxy);

        Assert.Equal(420 * 0x10000, proxy.PosX);
        Assert.Equal(584 * 0x10000, proxy.PosY);
        Assert.Equal(46 << 19, proxy.PosZ);
        Assert.Equal(17, proxy.TileX);
        Assert.Equal(36, proxy.TileY);
        Assert.Equal(23, proxy.TileZ);
    }

    [Fact]
    public void Map_MissingXPosYPosHeight_LeavePositionFieldsAtDefault()
    {
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_missing", new Dictionary<string, string>(), proxy);

        Assert.Equal(0, proxy.PosX);
        Assert.Equal(0, proxy.PosY);
        Assert.Equal(0, proxy.PosZ);
        Assert.Equal(0, proxy.TileX);
        Assert.Equal(0, proxy.TileY);
        Assert.Equal(0, proxy.TileZ);
    }

    [Fact]
    public void Map_EventCodeSlots_LandInAFOrder()
    {
        var properties = new Dictionary<string, string>
        {
            ["EventCodesA_LoadIndex"] = "10",
            ["EventCodesB_MapIndex"] = "11",
            ["EventCodesC_TickIndex"] = "12",
            ["EventCodesD_TouchIndex"] = "13",
            ["EventCodesE_DeactivateIndex"] = "14",
            ["EventCodesF_InteractIndex"] = "15",
        };
        var proxy = new AlundraEntityScriptProxy();

        EntityRecordMapper.Map("Entity_0", properties, proxy);

        Assert.Equal(new[] { 10, 11, 12, 13, 14, 15 }, proxy.ProgramIndexes);
    }
}
