using System;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraWorldProxy"/>'s per-record entity creation, split into
/// <see cref="AlundraWorldProxy.BuildEntityName"/>, <see cref="AlundraWorldProxy.ApplyRecord"/>,
/// <see cref="AlundraWorldProxy.TryGetPrefabAssetId"/> and the full
/// <see cref="AlundraWorldProxy.CreateEntityFromRecord"/> (which additionally exercises
/// <c>Entity.Initialize()</c> resolving <see cref="AlundraEntityScriptProxy"/> through <c>ElementFactory</c>
/// - this needs Alundra.Tests to carry its own MonoGame.Framework.DesktopGL reference, since
/// Alundra.csproj marks its own copy PrivateAssets="All" for game-folder deployment and it would
/// otherwise never flow into this test host).
///
/// Not covered: the live prefab loader (<c>World.Game.AssetContentManager.Load&lt;Entity&gt;</c>) wired in
/// <see cref="AlundraWorldProxy.InitializeWithWorld"/> - it needs a running
/// <see cref="CasaEngine.Framework.Application.CasaEngineGame"/> with a populated asset catalog, so tests
/// exercise <see cref="AlundraWorldProxy.CreateEntityFromRecord"/> directly with a fake loader instead.
/// Likewise not covered: the entity actually being added to and integrated by a live
/// <see cref="World"/> (<c>World.AddEntity</c> / <c>InternalAddEntities</c> calling
/// <c>Entity.InitializeWithWorld</c>).
/// </summary>
public class AlundraWorldProxyTests
{
    /// <summary>
    /// Builds a synthetic "Entities" object-layer record the same way the converter's
    /// TiledMapExporter emits one, reusing the map 389 / Entity_0 key set from
    /// <see cref="EntityRecordMapperTests"/>.
    /// </summary>
    private static TileMapObjectData BuildEntity0Record(string name = "Entity_0", string? entityName = "Bloc transparent (1x1x2)")
    {
        var record = new TileMapObjectData
        {
            Id = 0,
            Name = name,
            Type = null,
            X = 34,
            Y = 72,
            Width = 0,
            Height = 0,
        };

        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "34";
        record.CustomProperties["YPos"] = "72";
        record.CustomProperties["Height"] = "46";
        record.CustomProperties["SpriteTableIndex"] = "25";
        record.CustomProperties["EventCodesA_LoadIndex"] = "133";
        record.CustomProperties["EventCodesB_MapIndex"] = "5";
        record.CustomProperties["EventCodesC_TickIndex"] = "9";
        record.CustomProperties["EventCodesD_TouchIndex"] = "13";
        record.CustomProperties["EventCodesE_DeactivateIndex"] = "17";
        record.CustomProperties["EventCodesF_InteractIndex"] = "21";
        record.CustomProperties["_10"] = "0";

        if (entityName != null)
        {
            record.CustomProperties["EntityName"] = entityName;
        }

        return record;
    }

    [Fact]
    public void ApplyRecord_MapsFieldsAndSetsLoadedStatus()
    {
        var record = BuildEntity0Record();
        var proxy = new AlundraEntityScriptProxy();

        AlundraWorldProxy.ApplyRecord(record, proxy);

        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(25u, proxy.SpriteTableIndex);
        Assert.Equal(new[] { 133, 5, 9, 13, 17, 21 }, proxy.ProgramIndexes);
        Assert.Equal(0, proxy.ContentsGameFlag);
        Assert.Equal(420 * 0x10000, proxy.PosX);
        Assert.Equal(584 * 0x10000, proxy.PosY);
        Assert.Equal(46 << 19, proxy.PosZ);
    }

    [Fact]
    public void BuildEntityName_IncludesRecordNameAndEntityName()
    {
        var record = BuildEntity0Record();

        Assert.Equal("Entity_0 (Bloc transparent (1x1x2))", AlundraWorldProxy.BuildEntityName(record));
    }

    [Fact]
    public void BuildEntityName_NoEntityNameProperty_IsJustRecordName()
    {
        var record = BuildEntity0Record(entityName: null);

        Assert.Equal("Entity_0", AlundraWorldProxy.BuildEntityName(record));
    }

    [Fact]
    public void BuildEntityName_NullRecordName_FallsBackToGenericName()
    {
        var record = BuildEntity0Record(name: null!, entityName: null);

        Assert.Equal("Entity", AlundraWorldProxy.BuildEntityName(record));
    }

    [Fact]
    public void CreateEntityFromRecord_NoPrefabLink_ProducesInitializedBareEntityWithMappedProxy()
    {
        var record = BuildEntity0Record();

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, prefabLoader: null);

        Assert.Equal("Entity_0 (Bloc transparent (1x1x2))", entity.Name);
        Assert.Equal(nameof(AlundraEntityScriptProxy), entity.GameplayProxyClassName);
        Assert.True(entity.IsInitialized);
        Assert.Null(entity.RootComponent);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(25u, proxy.SpriteTableIndex);
        Assert.Equal(new[] { 133, 5, 9, 13, 17, 21 }, proxy.ProgramIndexes);
    }

    [Fact]
    public void CreateEntityFromRecord_LoaderReturnsNull_FallsBackToBareEntityWithMappedProxy()
    {
        var record = BuildEntity0Record();
        record.CustomProperties["PrefabAssetId"] = Guid.NewGuid().ToString();

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, _ => null);

        Assert.Equal("Entity_0 (Bloc transparent (1x1x2))", entity.Name);
        Assert.Equal(nameof(AlundraEntityScriptProxy), entity.GameplayProxyClassName);
        Assert.True(entity.IsInitialized);
        Assert.Null(entity.RootComponent);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(0, proxy.EntityRefId);
    }

    [Fact]
    public void CreateEntityFromRecord_LoaderThrows_FallsBackToBareEntityWithMappedProxy()
    {
        var record = BuildEntity0Record();
        record.CustomProperties["PrefabAssetId"] = Guid.NewGuid().ToString();

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, _ => throw new InvalidOperationException("asset not found"));

        Assert.Equal(nameof(AlundraEntityScriptProxy), entity.GameplayProxyClassName);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
    }

    [Fact]
    public void CreateEntityFromRecord_ValidPrefabLink_ClonesPrefabAndMapsProxy()
    {
        var record = BuildEntity0Record();
        var prefabAssetId = Guid.NewGuid();
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();

        var prefab = new Entity
        {
            Name = "BankPrefab",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = new AnimatedSpriteComponent(),
        };

        Guid? requestedId = null;
        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, id =>
        {
            requestedId = id;
            return prefab;
        });

        Assert.Equal(prefabAssetId, requestedId);
        Assert.Equal("Entity_0 (Bloc transparent (1x1x2))", entity.Name);
        Assert.Equal(nameof(AlundraEntityScriptProxy), entity.GameplayProxyClassName);
        Assert.True(entity.IsInitialized);

        var clonedComponent = Assert.IsType<AnimatedSpriteComponent>(entity.RootComponent);
        Assert.NotSame(prefab.RootComponent, clonedComponent);
        Assert.NotSame(prefab, entity);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(25u, proxy.SpriteTableIndex);
        Assert.Equal(new[] { 133, 5, 9, 13, 17, 21 }, proxy.ProgramIndexes);

        // Map 389 / Entity_0 anchor: root transform must match AlundraWorldProxy.ResolveWorldPosition
        // fed with the proxy's own PosX/PosY/PosZ - this is the clone path exercising the same
        // conversion covered directly by ResolveWorldPosition_* below.
        var expectedPosition = AlundraWorldProxy.ResolveWorldPosition(proxy.PosX, proxy.PosY, proxy.PosZ);
        Assert.Equal(expectedPosition, entity.RootComponent.LocalTransform.Position);
        Assert.Equal(new Vector3(420, -216, 0f), entity.RootComponent.LocalTransform.Position);
    }

    [Fact]
    public void CreateEntityFromRecord_ValidPrefabLink_RootComponentNull_DoesNotThrow()
    {
        var record = BuildEntity0Record();
        var prefabAssetId = Guid.NewGuid();
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();

        var prefab = new Entity
        {
            Name = "BankPrefab",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = null,
        };

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record, _ => prefab);

        Assert.Null(entity.RootComponent);
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(420 * 0x10000, proxy.PosX);
    }

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111", true)]
    [InlineData("not-a-guid", false)]
    public void TryGetPrefabAssetId_ParsesOrRejectsValue(string rawValue, bool expected)
    {
        var record = BuildEntity0Record();
        record.CustomProperties["PrefabAssetId"] = rawValue;

        var result = AlundraWorldProxy.TryGetPrefabAssetId(record, out var prefabAssetId);

        Assert.Equal(expected, result);
        if (expected)
        {
            Assert.Equal(Guid.Parse(rawValue), prefabAssetId);
        }
        else
        {
            Assert.Equal(Guid.Empty, prefabAssetId);
        }
    }

    [Fact]
    public void TryGetPrefabAssetId_MissingKey_ReturnsFalse()
    {
        var record = BuildEntity0Record();

        var result = AlundraWorldProxy.TryGetPrefabAssetId(record, out var prefabAssetId);

        Assert.False(result);
        Assert.Equal(Guid.Empty, prefabAssetId);
    }

    [Fact]
    public void ResolveWorldPosition_Map389Entity0Anchor_MatchesWorldWriterFrame()
    {
        // XPos=34/YPos=72/Height=46 -> PosX=420*0x10000, PosY=584*0x10000, PosZ=46<<19
        // (see EntityRecordMapperTests.Map_XPosYPosHeight_FillPositionAndTileFields). Pixel X stays as-is;
        // pixel Y is negated (Alundra Y-down -> CasaEngine Y-up) with the elevation (PosZ>>16 = 368 px)
        // added back in, projecting the sprite up the screen: -(584-368) = -216. World Z stays 0, exactly
        // like WorldWriter.ResolveTileCentreSpawn - it only orders render layers here.
        var position = AlundraWorldProxy.ResolveWorldPosition(420 * 0x10000, 584 * 0x10000, 46 << 19);

        Assert.Equal(new Vector3(420, -216, 0f), position);
    }

    [Fact]
    public void ResolveWorldPosition_ZeroHeight_ProjectsToUnshiftedNegatedPixelY()
    {
        // XPos=10/YPos=20/Height=0 -> PosX=132*0x10000, PosY=168*0x10000, PosZ=0. With no elevation the
        // projected Y is exactly the negated raw pixel Y: -(168-0) = -168.
        var position = AlundraWorldProxy.ResolveWorldPosition(132 * 0x10000, 168 * 0x10000, 0);

        Assert.Equal(new Vector3(132, -168, 0f), position);
    }
}
