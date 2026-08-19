using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraWorldProxy"/>'s per-record entity creation, split into
/// <see cref="AlundraWorldProxy.BuildEntityName"/>, <see cref="AlundraWorldProxy.ApplyRecord"/> and the
/// full <see cref="AlundraWorldProxy.CreateEntityFromRecord"/> (which additionally exercises
/// <c>Entity.Initialize()</c> resolving <see cref="AlundraEntityScriptProxy"/> through <c>ElementFactory</c>
/// - this needs Alundra.Tests to carry its own MonoGame.Framework.DesktopGL reference, since
/// Alundra.csproj marks its own copy PrivateAssets="All" for game-folder deployment and it would
/// otherwise never flow into this test host).
///
/// Not covered: the entity actually being added to and integrated by a live <see cref="World"/>
/// (<c>World.AddEntity</c> / <c>InternalAddEntities</c> calling <c>Entity.InitializeWithWorld</c>), which
/// needs a running <see cref="CasaEngine.Framework.Application.CasaEngineGame"/>.
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
    public void CreateEntityFromRecord_ProducesInitializedEntityWithMappedProxy()
    {
        var record = BuildEntity0Record();

        var entity = AlundraWorldProxy.CreateEntityFromRecord(record);

        Assert.Equal("Entity_0 (Bloc transparent (1x1x2))", entity.Name);
        Assert.Equal(nameof(AlundraEntityScriptProxy), entity.GameplayProxyClassName);
        Assert.True(entity.IsInitialized);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        Assert.Equal(EntityStatus.Loaded, proxy.Status);
        Assert.Equal(0, proxy.EntityRefId);
        Assert.Equal(25u, proxy.SpriteTableIndex);
        Assert.Equal(new[] { 133, 5, 9, 13, 17, 21 }, proxy.ProgramIndexes);
    }
}
