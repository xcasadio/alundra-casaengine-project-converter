using System;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers the spawn-time entity initialization port added to <see cref="AlundraWorldProxy"/>:
/// <see cref="AlundraWorldProxy.ShouldSpawnRecord"/> (the map-load gating ported from
/// <c>GameEngine.SpawnEntity</c>/<c>InitializeEntitySlots</c>) and
/// <see cref="AlundraWorldProxy.ApplySpawnInitialization"/>/<see cref="AlundraWorldProxy.SetEntityDimensions"/>
/// (ported from <c>EntityManager.InitializeEntity</c>/<c>SetEntityDimensions</c>).
/// </summary>
public class AlundraWorldProxySpawnInitializationTests
{
    private static TileMapObjectData NewRecord() => new() { Id = 0, Name = "Entity_0" };

    // -----------------------------------------------------------------------------------------
    // ShouldSpawnRecord
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ShouldSpawnRecord_IsEnabledZero_ReturnsFalse()
    {
        var record = NewRecord();
        record.CustomProperties["IsEnabled"] = "0";
        record.CustomProperties["SpriteDirection"] = "64";

        Assert.False(AlundraWorldProxy.ShouldSpawnRecord(record, out var reason));
        Assert.Contains("IsEnabled", reason);
    }

    [Theory]
    [InlineData("0")]   // no bits set
    [InlineData("128")] // isMapSprite bit (0x80) only, 0x40 clear
    public void ShouldSpawnRecord_SpriteDirectionBit0x40Clear_ReturnsFalse(string spriteDirection)
    {
        var record = NewRecord();
        record.CustomProperties["IsEnabled"] = "1";
        record.CustomProperties["SpriteDirection"] = spriteDirection;

        Assert.False(AlundraWorldProxy.ShouldSpawnRecord(record, out var reason));
        Assert.Contains("0x40", reason);
    }

    [Theory]
    [InlineData("64")]  // 0x40 alone
    [InlineData("192")] // 0x40 | 0x80
    [InlineData("193")] // 0x40 | 0x80 | facing 1
    [InlineData("195")] // 0x40 | 0x80 | facing 3
    public void ShouldSpawnRecord_SpriteDirectionBit0x40Set_ReturnsTrue(string spriteDirection)
    {
        var record = NewRecord();
        record.CustomProperties["IsEnabled"] = "1";
        record.CustomProperties["SpriteDirection"] = spriteDirection;

        Assert.True(AlundraWorldProxy.ShouldSpawnRecord(record, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void ShouldSpawnRecord_MissingKeys_DefaultsToSpawn()
    {
        var record = NewRecord();

        Assert.True(AlundraWorldProxy.ShouldSpawnRecord(record, out _));
    }

    /// <summary>
    /// Map 389's own "Entities" layer: 19 records, all IsEnabled=1, SpriteDirection values
    /// {0,0,128,128,128,64,64,64,64,64,64,192,192,192,192,193,194,194,195} (read from the real converter
    /// export). 5 of them (the two 0s and three 128s) have bit 0x40 clear, so the map-load gate keeps 14 -
    /// down from the pre-gating count of 19. See the ShouldSpawnRecord doc comment for why the player-tile
    /// spawn-zone box is not part of this count.
    /// </summary>
    [Fact]
    public void ShouldSpawnRecord_Map389EntitiesLayer_Keeps14Of19()
    {
        var spriteDirections = new[]
        {
            "0", "0", "128", "128", "128", "64", "64", "64", "64", "64", "64",
            "192", "192", "192", "192", "193", "194", "194", "195",
        };

        var keptCount = 0;
        foreach (var spriteDirection in spriteDirections)
        {
            var record = NewRecord();
            record.CustomProperties["IsEnabled"] = "1";
            record.CustomProperties["SpriteDirection"] = spriteDirection;

            if (AlundraWorldProxy.ShouldSpawnRecord(record, out _))
            {
                keptCount++;
            }
        }

        Assert.Equal(19, spriteDirections.Length);
        Assert.Equal(14, keptCount);
    }

    // -----------------------------------------------------------------------------------------
    // SetEntityDimensions
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SetEntityDimensions_NonZeroSizes_MatchesExactFixedPointFormula()
    {
        var proxy = new AlundraEntityScriptProxy();

        // Bloc transparent (1x1x2) real header (Data/sprite-records.json, prefab fd375feb-...).
        AlundraWorldProxy.SetEntityDimensions(proxy, offsetX: -12, offsetY: -8, offsetZ: 0, sizeX: 24, sizeY: 16, sizeZ: 32);

        Assert.Equal(-12 << 16, proxy.ModX);
        Assert.Equal(-8 << 16, proxy.ModY);
        Assert.Equal(0, proxy.ModZ);
        Assert.Equal(12 << 16, proxy.NegModX);
        Assert.Equal(8 << 16, proxy.NegModY);
        Assert.Equal(0x4e00000 - (12 << 16), proxy.ScreenClipX);
        Assert.Equal(0x3c00000 - (8 << 16), proxy.ScreenClipY);
        Assert.Equal(0x7800000 - (32 << 16), proxy.ScreenClipZ);
        Assert.Equal((24 << 16) - 1, proxy.Width);
        Assert.Equal((16 << 16) - 1, proxy.Height);
        Assert.Equal((32 << 16) - 1, proxy.Depth);
    }

    [Fact]
    public void SetEntityDimensions_ZeroSizes_ProduceZeroWidthHeightDepth()
    {
        var proxy = new AlundraEntityScriptProxy();

        AlundraWorldProxy.SetEntityDimensions(proxy, offsetX: 0, offsetY: 0, offsetZ: 0, sizeX: 0, sizeY: 0, sizeZ: 0);

        Assert.Equal(0, proxy.Width);
        Assert.Equal(0, proxy.Height);
        Assert.Equal(0, proxy.Depth);
    }

    // -----------------------------------------------------------------------------------------
    // ApplySpawnInitialization
    // -----------------------------------------------------------------------------------------

    private static TileMapObjectData BuildRecordWithPrefabLink(Guid prefabAssetId, string spriteDirection = "64")
    {
        var record = NewRecord();
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "34";
        record.CustomProperties["YPos"] = "72";
        record.CustomProperties["Height"] = "46";
        record.CustomProperties["SpriteDirection"] = spriteDirection;
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();
        return record;
    }

    [Fact]
    public void ApplySpawnInitialization_NoCatalog_OnlySetsLogicContextEntity()
    {
        var record = BuildRecordWithPrefabLink(Guid.NewGuid());
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        var rawPosZ = proxy.PosZ;
        var entity = new Entity();

        AlundraWorldProxy.ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog: null);

        Assert.Same(entity, proxy.LogicContextEntity);
        Assert.Equal(0u, proxy.Flags);
        Assert.Equal(rawPosZ, proxy.PosZ); // untouched: no header, no -ModZ+1 adjustment
    }

    [Fact]
    public void ApplySpawnInitialization_PrefabIdNotInCatalog_OnlySetsLogicContextEntity()
    {
        var record = BuildRecordWithPrefabLink(Guid.NewGuid());
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        var entity = new Entity();
        var catalog = new FakeSpriteRecordCatalog(); // empty: lookup misses

        AlundraWorldProxy.ApplySpawnInitialization(record, entity, proxy, catalog);

        Assert.Same(entity, proxy.LogicContextEntity);
        Assert.Equal(0u, proxy.Flags);
    }

    /// <summary>
    /// Full anchor test: map 389 / Entity_0 ("Bloc transparent (1x1x2)", SpriteTableIndex 25,
    /// SpriteDirection 64) fed through its own real header from Data/sprite-records.json (prefab
    /// fd375feb-2f77-447e-aedb-c3fa44c64edd - read from the actual converter export).
    /// </summary>
    [Fact]
    public void ApplySpawnInitialization_KnownHeader_PortsFlagsProgramsDimensionsPosZAndAnimation()
    {
        var prefabAssetId = Guid.Parse("fd375feb-2f77-447e-aedb-c3fa44c64edd");
        var record = BuildRecordWithPrefabLink(prefabAssetId, spriteDirection: "64");
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        // Pre-adjustment PosZ, exactly as EntityRecordMapper leaves it (46 << 19, Height=46).
        Assert.Equal(46 << 19, proxy.PosZ);

        var entity = new Entity();
        var header = new SpriteRecordHeader
        {
            MoreFlags = 128,
            CanPickup = 96,
            FlagsPortraitShadowType = 0,
            ProgramLoad = 0,
            ProgramTick = 0,
            ProgramTouch = 0,
            ProgramDeactivate = 0,
            ProgramInteract = 0,
            OffsetX = -12,
            OffsetY = -8,
            OffsetZ = 0,
            SizeX = 24,
            SizeY = 16,
            SizeZ = 32,
            Contents = 0,
        };
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);

        AlundraWorldProxy.ApplySpawnInitialization(record, entity, proxy, catalog);

        Assert.Same(entity, proxy.LogicContextEntity);

        // Flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16 = 128 | 96<<8 | 0 = 0x6080.
        Assert.Equal(0x6080u, proxy.Flags);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, proxy.SpriteProgramIndexes);

        Assert.Equal(-12 << 16, proxy.ModX);
        Assert.Equal(-8 << 16, proxy.ModY);
        Assert.Equal(0, proxy.ModZ);

        // PosZ = rawZ - ModZ + 1 = (46<<19) - 0 + 1.
        Assert.Equal((46 << 19) + 1, proxy.PosZ);

        Assert.Equal(proxy.PosX + proxy.ModX, proxy.ModdedPosX);
        Assert.Equal(proxy.PosY + proxy.ModY, proxy.ModdedPosY);
        Assert.Equal(proxy.PosZ + proxy.ModZ, proxy.ModdedPosZ);

        // SpriteDirection=64 (0x40): facing index (64 & 3) = 0 -> g_cardinalDirectionTable[0] = 0.
        Assert.Equal(0u, proxy.TargetAnimationId);
        Assert.Equal(0u, proxy.TargetDirection);
        Assert.Equal(~0u, proxy.CurrentAnimationId);
        Assert.Equal(~0u, proxy.CurrentDirection);
    }

    [Fact]
    public void ApplySpawnInitialization_SpriteDirectionFacingBits_SelectCardinalDirectionTableEntry()
    {
        var prefabAssetId = Guid.NewGuid();
        var header = new SpriteRecordHeader();
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);

        // 0x40 | facing=2 -> g_cardinalDirectionTable[2] = 0x08.
        var record = BuildRecordWithPrefabLink(prefabAssetId, spriteDirection: (0x40 | 2).ToString());
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);

        AlundraWorldProxy.ApplySpawnInitialization(record, new Entity(), proxy, catalog);

        Assert.Equal(0x08u, proxy.TargetDirection);
    }

    [Fact]
    public void ApplySpawnInitialization_NonZeroOffsetZ_AdjustsPosZByModZPlusOne()
    {
        var prefabAssetId = Guid.NewGuid();
        var header = new SpriteRecordHeader { OffsetZ = 5 };
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);

        var record = BuildRecordWithPrefabLink(prefabAssetId);
        record.CustomProperties["Height"] = "100"; // rawZ = 100 << 19
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);
        var rawZ = proxy.PosZ;
        Assert.Equal(100 << 19, rawZ);

        AlundraWorldProxy.ApplySpawnInitialization(record, new Entity(), proxy, catalog);

        var expectedModZ = 5 << 16;
        Assert.Equal(expectedModZ, proxy.ModZ);
        Assert.Equal(rawZ - expectedModZ + 1, proxy.PosZ);
    }

    // -----------------------------------------------------------------------------------------
    // IdsvByAnimDirection (see WallPlacementOverlay.ApplyEntitySortKey's frame-0-only deviation note)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BuildIdsvByAnimDirection_EmptyList_ReturnsNull()
    {
        Assert.Null(AlundraWorldProxy.BuildIdsvByAnimDirection(Array.Empty<AnimDirIdsv>()));
    }

    [Fact]
    public void BuildIdsvByAnimDirection_KeepsOnlyFrameZeroPerAnimDirection()
    {
        var idsvAnimDirs = new[]
        {
            new AnimDirIdsv { Anim = 1, Direction = 0, Frames = new[] { 6, 3, 6 } },
            new AnimDirIdsv { Anim = 10, Direction = 2, Frames = new[] { 0 } },
        };

        var table = AlundraWorldProxy.BuildIdsvByAnimDirection(idsvAnimDirs);

        Assert.NotNull(table);
        Assert.Equal(6, table![1 * 4 + 0]);
        Assert.Equal(0, table[10 * 4 + 2]);
        Assert.Equal(2, table.Count);
    }

    [Fact]
    public void ApplySpawnInitialization_KnownHeaderWithIdsvAnimDirs_StashesTheFrameZeroTableOnTheProxy()
    {
        var prefabAssetId = Guid.NewGuid();
        var header = new SpriteRecordHeader
        {
            IdsvAnimDirs = new[]
            {
                new AnimDirIdsv { Anim = 1, Direction = 0, Frames = new[] { 6, 3, 6 } },
                new AnimDirIdsv { Anim = 10, Direction = 0, Frames = new[] { 0 } },
            },
        };
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);
        var record = BuildRecordWithPrefabLink(prefabAssetId);
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);

        AlundraWorldProxy.ApplySpawnInitialization(record, new Entity(), proxy, catalog);

        Assert.NotNull(proxy.IdsvByAnimDirection);
        Assert.Equal(6, proxy.IdsvByAnimDirection![1 * 4 + 0]);
        Assert.Equal(0, proxy.IdsvByAnimDirection[10 * 4 + 0]);
    }

    [Fact]
    public void ApplySpawnInitialization_HeaderWithNoIdsvAnimDirs_LeavesTheTableNull()
    {
        var prefabAssetId = Guid.NewGuid();
        var header = new SpriteRecordHeader(); // IdsvAnimDirs left unset (null, see its doc comment)
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);
        var record = BuildRecordWithPrefabLink(prefabAssetId);
        var proxy = new AlundraEntityScriptProxy();
        AlundraWorldProxy.ApplyRecord(record, proxy);

        AlundraWorldProxy.ApplySpawnInitialization(record, new Entity(), proxy, catalog);

        Assert.Null(proxy.IdsvByAnimDirection);
    }

    // -----------------------------------------------------------------------------------------
    // Creation-flow integration: header init flows into CreateEntityFromPrefab's world transform too.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CreateEntityFromRecord_WithSpriteRecordCatalog_AdjustsRootTransformByHeaderPosZ()
    {
        var prefabAssetId = Guid.Parse("fd375feb-2f77-447e-aedb-c3fa44c64edd");
        var record = BuildRecordWithPrefabLink(prefabAssetId, spriteDirection: "64");
        var header = new SpriteRecordHeader
        {
            MoreFlags = 128,
            CanPickup = 96,
            OffsetX = -12,
            OffsetY = -8,
            OffsetZ = 0,
            SizeX = 24,
            SizeY = 16,
            SizeZ = 32,
        };
        var catalog = new FakeSpriteRecordCatalog().Add(prefabAssetId, header);

        var prefab = new Entity
        {
            Name = "BankPrefab",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = new AnimatedSpriteComponent(),
        };

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, _ => prefab, catalog);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        var expectedPosition = AlundraWorldProxy.ResolveWorldPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
        Assert.Equal(expectedPosition, entity.RootComponent!.LocalTransform.Position);
        // PosZ carries the header's -ModZ+1 adjustment ((46<<19) - 0 + 1), unlike the no-catalog case
        // covered by AlundraWorldProxyTests.CreateEntityFromRecord_ValidPrefabLink_ClonesPrefabAndMapsProxy.
        Assert.Equal((46 << 19) + 1, proxy.PosZ);
    }
}
