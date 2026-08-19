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
