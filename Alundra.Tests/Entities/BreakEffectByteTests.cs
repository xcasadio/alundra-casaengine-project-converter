#nullable enable
using System;
using System.IO;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;
using Alundra.Entities;
using Alundra.Entities.Spawning;

namespace Alundra.Tests.Entities;

/// <summary>
/// E14.a1b T1 (docs/plan-e14-ia-native.md section 1.3b, D-E14-9, ADR-0060/0061): the break-effect byte of the sprite header (<c>+0x1E</c>, exported as <c>BreakEffect</c> by E14.a1a) travels from
/// <c>Data/sprite-records.json</c> to the header, to the entity at its apparition and to its clone. Absent key: 0 (an export that predates E14.a1a).
/// </summary>
public sealed class BreakEffectByteTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "BreakEffectByteTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private void Write(string json)
    {
        var data = Path.Combine(_projectPath, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "sprite-records.json"), json);
    }

    private static string Entry(Guid id, string breakEffect)
        => "{\n" + $"  \"{id}\": {{\n"
           + "    \"MoreFlags\": 0, \"CanPickup\": 0, \"FlagsPortraitShadowType\": 0,\n"
           + "    \"ProgramLoad\": 0, \"ProgramTick\": 0, \"ProgramTouch\": 0, \"ProgramDeactivate\": 0, \"ProgramInteract\": 0,\n"
           + "    \"OffsetX\": 0, \"OffsetY\": 0, \"OffsetZ\": 0, \"SizeX\": 0, \"SizeY\": 0, \"SizeZ\": 0,\n"
           + breakEffect
           + "    \"Contents\": 0\n  }\n}";

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(0)]
    public void TheCatalog_ReadsTheBreakEffectOfAnEntry(int value)
    {
        var id = Guid.NewGuid();
        Write(Entry(id, $"    \"BreakEffect\": {value},\n"));

        Assert.True(new SpriteRecordCatalog(_projectPath).TryGet(id, out var header));

        Assert.Equal(value, header.BreakEffect);
    }

    [Fact]
    public void TheCatalog_ReadsAnAbsentKeyAsZero()
    {
        var id = Guid.NewGuid();
        Write(Entry(id, string.Empty));

        Assert.True(new SpriteRecordCatalog(_projectPath).TryGet(id, out var header));

        Assert.Equal(0, header.BreakEffect);
    }

    private static TileMapObjectData Record(Guid prefabAssetId)
    {
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "6";
        record.CustomProperties["YPos"] = "40";
        record.CustomProperties["Height"] = "0";
        record.CustomProperties["SpriteDirection"] = "0";
        record.CustomProperties["SpriteTableIndex"] = "4";
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();
        return record;
    }

    [Fact]
    public void TheApparitionCopiesTheByteOfTheHeader_AndTheCloneCopiesIt()
    {
        var prefab = Guid.NewGuid();
        var proxy = new EntityScriptProxy();
        var record = Record(prefab);
        EntitySpawnFactory.ApplyRecord(record, proxy);

        EntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog().Add(prefab, new SpriteRecordHeader { BreakEffect = 12 }));

        Assert.Equal(12, proxy.BreakEffect);
        Assert.Equal(12, ((EntityScriptProxy)proxy.Clone()).BreakEffect);
    }

    [Fact]
    public void AnEntityWithoutAHeader_HasNoBreakEffect()
    {
        Assert.Equal(0, new EntityScriptProxy().BreakEffect);
    }

    private static string RealExport()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "Data", "sprite-records.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("no alundra-project/Data/sprite-records.json above the test binaries");
    }

    [Theory]
    [InlineData("e3a39ec4-45d5-55f2-85e9-894c7b6e4a08")]
    [InlineData("1ef8f16c-605f-58c3-af19-b6bb602fc40f")]
    public void TheRealExport_GivesTheMurutaAndTheBeannoideTheBreakEffect8(string prefab)
    {
        var catalog = new SpriteRecordCatalog(RealExport());

        Assert.True(catalog.TryGet(Guid.Parse(prefab), out var header));

        Assert.Equal(8, header.BreakEffect);
    }
}
