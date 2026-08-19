using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="SpriteRecordCatalog"/>'s file-backed loading (see its class doc for the degraded
/// mode contract) using a temporary directory instead of a real converted project.
/// </summary>
public class SpriteRecordCatalogTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraSpriteRecordCatalogTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private void WriteSpriteRecords(string json)
    {
        var dataDirectory = Path.Combine(_projectPath, "Data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "sprite-records.json"), json);
    }

    [Fact]
    public void TryGet_FileMissing_ReturnsFalse_DegradedMode()
    {
        var catalog = new SpriteRecordCatalog(_projectPath);

        Assert.False(catalog.TryGet(Guid.NewGuid(), out _));
    }

    [Fact]
    public void TryGet_FileUnreadableJson_ReturnsFalse_DegradedMode()
    {
        WriteSpriteRecords("{ not valid json");

        var catalog = new SpriteRecordCatalog(_projectPath);

        Assert.False(catalog.TryGet(Guid.NewGuid(), out _));
    }

    [Fact]
    public void TryGet_KnownPrefabId_ReturnsHeaderFieldsVerbatim()
    {
        var prefabAssetId = Guid.Parse("fd375feb-2f77-447e-aedb-c3fa44c64edd");
        WriteSpriteRecords(
            "{\n"
            + $"  \"{prefabAssetId}\": {{\n"
            + "    \"MoreFlags\": 128,\n"
            + "    \"CanPickup\": 96,\n"
            + "    \"FlagsPortraitShadowType\": 0,\n"
            + "    \"ProgramLoad\": 0,\n"
            + "    \"ProgramTick\": 0,\n"
            + "    \"ProgramTouch\": 0,\n"
            + "    \"ProgramDeactivate\": 0,\n"
            + "    \"ProgramInteract\": 0,\n"
            + "    \"OffsetX\": -12,\n"
            + "    \"OffsetY\": -8,\n"
            + "    \"OffsetZ\": 0,\n"
            + "    \"SizeX\": 24,\n"
            + "    \"SizeY\": 16,\n"
            + "    \"SizeZ\": 32,\n"
            + "    \"Contents\": 0\n"
            + "  }\n"
            + "}");

        var catalog = new SpriteRecordCatalog(_projectPath);

        Assert.True(catalog.TryGet(prefabAssetId, out var header));
        Assert.Equal(128, header.MoreFlags);
        Assert.Equal(96, header.CanPickup);
        Assert.Equal(0, header.FlagsPortraitShadowType);
        Assert.Equal(-12, header.OffsetX);
        Assert.Equal(-8, header.OffsetY);
        Assert.Equal(0, header.OffsetZ);
        Assert.Equal(24, header.SizeX);
        Assert.Equal(16, header.SizeY);
        Assert.Equal(32, header.SizeZ);

        // Tolerance: the fixture above omits "IdsvAnimDirs" entirely (a pre-IDSV export, or a record
        // whose bank had none) - System.Text.Json ignores unknown/missing fields, and ToHeader defaults
        // this one to empty rather than null (see SpriteRecordHeader.IdsvAnimDirs's doc comment).
        Assert.NotNull(header.IdsvAnimDirs);
        Assert.Empty(header.IdsvAnimDirs!);
    }

    [Fact]
    public void TryGet_KnownPrefabId_ParsesIdsvAnimDirs()
    {
        var prefabAssetId = Guid.Parse("fd375feb-2f77-447e-aedb-c3fa44c64edd");
        WriteSpriteRecords(
            "{\n"
            + $"  \"{prefabAssetId}\": {{\n"
            + "    \"MoreFlags\": 0, \"CanPickup\": 0, \"FlagsPortraitShadowType\": 0,\n"
            + "    \"ProgramLoad\": 0, \"ProgramTick\": 0, \"ProgramTouch\": 0, \"ProgramDeactivate\": 0,\n"
            + "    \"ProgramInteract\": 0,\n"
            + "    \"OffsetX\": 0, \"OffsetY\": 0, \"OffsetZ\": 0, \"SizeX\": 0, \"SizeY\": 0, \"SizeZ\": 0,\n"
            + "    \"Contents\": 0,\n"
            + "    \"IdsvAnimDirs\": [\n"
            + "      { \"Anim\": 1, \"Direction\": 0, \"Frames\": [6, 3, 6] },\n"
            + "      { \"Anim\": 10, \"Direction\": 2, \"Frames\": [0] }\n"
            + "    ]\n"
            + "  }\n"
            + "}");

        var catalog = new SpriteRecordCatalog(_projectPath);

        Assert.True(catalog.TryGet(prefabAssetId, out var header));
        Assert.Equal(2, header.IdsvAnimDirs!.Count);

        var first = header.IdsvAnimDirs[0];
        Assert.Equal(1, first.Anim);
        Assert.Equal(0, first.Direction);
        Assert.Equal(new[] { 6, 3, 6 }, first.Frames);

        var second = header.IdsvAnimDirs[1];
        Assert.Equal(10, second.Anim);
        Assert.Equal(2, second.Direction);
        Assert.Equal(new[] { 0 }, second.Frames);
    }

    [Fact]
    public void TryGet_UnknownPrefabId_ReturnsFalse()
    {
        WriteSpriteRecords("{}");

        var catalog = new SpriteRecordCatalog(_projectPath);

        Assert.False(catalog.TryGet(Guid.NewGuid(), out _));
    }

    [Fact]
    public void FakeSpriteRecordCatalog_AddThenTryGet_Roundtrips()
    {
        var prefabAssetId = Guid.NewGuid();
        var header = new SpriteRecordHeader { MoreFlags = 1, OffsetZ = 3 };
        var fake = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);

        Assert.True(fake.TryGet(prefabAssetId, out var found));
        Assert.Equal(1, found.MoreFlags);
        Assert.Equal(3, found.OffsetZ);
        Assert.False(fake.TryGet(Guid.NewGuid(), out _));
    }
}
