using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Size = CasaEngine.Core.Math.Size;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="WallPlacementOverlay"/>: parsing the "AlundraWallPlacements" custom property
/// (tolerant degraded mode included), the wall/entity depth key formulas ported from
/// <c>GraphicManager.DepthWallBlock</c>/<c>DepthEntity</c>, and the strip/resubmit wiring over a
/// headless <see cref="CasaEngine.Framework.Scene.Entities.Components.TileMapComponent"/> (built the same
/// way <c>CasaEngine.Tests.TileMap.TileMapSurfaceInvalidationTests</c> does, since it needs no graphics
/// device - see that class for the pattern this reuses).
/// </summary>
public class WallPlacementOverlayTests
{
    private const int MapSize = 4;
    private const int TilePixelSize = 16;
    private const int TileId = 5;

    // -------------------- Parsing --------------------

    [Fact]
    public void TryParse_MissingProperty_ReturnsFalseAndDisablesFeature()
    {
        var customProperties = new Dictionary<string, string>();

        var result = WallPlacementOverlay.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsFalse()
    {
        var customProperties = new Dictionary<string, string>
        {
            [WallPlacementOverlay.CustomPropertyKey] = "{not valid json",
        };

        var result = WallPlacementOverlay.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryParse_ColumnLengthMismatch_ReturnsFalse()
    {
        // "count" says 2 but the columns only carry 1 entry each - a malformed document the same way a
        // truncated/corrupted custom property would be.
        var customProperties = new Dictionary<string, string>
        {
            [WallPlacementOverlay.CustomPropertyKey] =
                "{\"map_index\":389,\"count\":2,\"cell_x\":[17],\"cell_y\":[17],\"stack_index\":[0],"
                + "\"plane\":[0],\"x\":[17],\"y\":[6],\"gid\":[613],\"depth_slot\":[3]}",
        };

        var result = WallPlacementOverlay.TryParse(customProperties, "map_1", out var records);

        Assert.False(result);
        Assert.Null(records);
    }

    [Fact]
    public void TryParse_WellFormedDocument_ParsesEveryColumn()
    {
        // The single-entry example from docs/formats/cells-companion.md.
        var customProperties = new Dictionary<string, string>
        {
            [WallPlacementOverlay.CustomPropertyKey] =
                "{\"map_index\":389,\"count\":1,\"cell_x\":[17],\"cell_y\":[17],\"stack_index\":[0],"
                + "\"plane\":[0],\"x\":[17],\"y\":[6],\"gid\":[613],\"depth_slot\":[3]}",
        };

        var result = WallPlacementOverlay.TryParse(customProperties, "map_1", out var records);

        Assert.True(result);
        Assert.Equal(389, records.MapIndex);
        Assert.Equal(1, records.Count);
        Assert.Equal(17, records.CellX[0]);
        Assert.Equal(17, records.CellY[0]);
        Assert.Equal(0, records.StackIndex[0]);
        Assert.Equal(0, records.Plane[0]);
        Assert.Equal(17, records.X[0]);
        Assert.Equal(6, records.Y[0]);
        Assert.Equal(613, records.Gid[0]);
        Assert.Equal(3, records.DepthSlot[0]);
    }

    // -------------------- Key formulas --------------------

    [Fact]
    public void ComputeWallSortKey_MatchesDepthWallBlockFormula()
    {
        // GraphicManager.DepthWallBlock(tileY, wallSlot) = tileY * 16 + 7 + clamp(wallSlot, 0, 6).
        var key = WallPlacementOverlay.ComputeWallSortKey(cellY: 17, depthSlot: 3, stableId: 0);

        Assert.Equal((int)RenderPass2D.YSortedWorld, key.RenderPass);
        Assert.Equal((int)RenderPass2D.YSortedWorld, key.SortingLayer);
        Assert.Equal(0, key.OrderInLayer);
        Assert.Equal(17 * 16 + 7 + 3, key.Elevation);
        Assert.Equal(282, key.Elevation);
    }

    [Fact]
    public void ComputeWallSortKey_ClampsDepthSlotToSixMax()
    {
        var key = WallPlacementOverlay.ComputeWallSortKey(cellY: 0, depthSlot: 99, stableId: 0);

        Assert.Equal(7 + 6, key.Elevation);
    }

    [Fact]
    public void ComputeEntityElevation_UsesBlocTransparentAnchor()
    {
        // Reuses the "Bloc transparent (1x1x2)" map-389 record anchor from EntityRecordMapperTests /
        // AlundraWorldProxyTests: PosY = 584 * 0x10000 (584 pixels), tile row 584 >> 4 = 36.
        const int posY = 584 * 0x10000;

        var elevation = WallPlacementOverlay.ComputeEntityElevation(posY, idsv: 0);

        Assert.Equal(36 * 16 + 6, elevation);
        Assert.Equal(582, elevation);
    }

    [Fact]
    public void ComputeEntityElevation_ZeroPosY_IsRowZeroEntitySlot()
    {
        Assert.Equal(6, WallPlacementOverlay.ComputeEntityElevation(0, idsv: 0));
    }

    [Fact]
    public void ComputeEntityElevation_IdsvBias_CanCrossARowBoundary()
    {
        // PosY = 575 pixels (16.16 fixed): row bucket alone is 575 >> 4 = 35. Folding in an IDSV bias
        // of 6 (EntityManager.cs:1049's sortValue = PosY + (ImageDepthSortValue << 16)) pushes the
        // biased pixel value to 581, whose row bucket is 581 >> 4 = 36 - one row further back, exactly
        // the kind of miss the bug report measured (anim 1 direction 0 on map 389 banks 146/161).
        const int posY = 575 * 0x10000;

        var withoutBias = WallPlacementOverlay.ComputeEntityElevation(posY, idsv: 0);
        var withBias = WallPlacementOverlay.ComputeEntityElevation(posY, idsv: 6);

        Assert.Equal(35 * 16 + 6, withoutBias);
        Assert.Equal(36 * 16 + 6, withBias);
    }

    // -------------------- Entity-vs-wall ordering (RenderSortKey2D comparisons) --------------------

    [Fact]
    public void Ordering_EntityRowBelowWallRow_EntityDrawsBehindWall()
    {
        var entityKey = EntityKeyForRow(tileRow: 5);
        var wallKey = WallPlacementOverlay.ComputeWallSortKey(cellY: 6, depthSlot: 0, stableId: 0);

        // Same render pass/layer/order - only Elevation differs at this point, and a smaller Elevation
        // sorts first (drawn behind, per RenderSortKey2D.CompareTo field order).
        Assert.True(entityKey.CompareTo(wallKey) < 0);
    }

    [Fact]
    public void Ordering_EntityRowEqualsWallRow_WallDrawsAboveEntity()
    {
        // Same tile row: the wall's own bias (slot 7+) always outranks the entity's fixed slot 6, per
        // GraphicManager's DepthWallBlock/DepthEntity constants (WALL_BIAS=7 > ENTITY_SLOT=6).
        var entityKey = EntityKeyForRow(tileRow: 6);
        var wallKey = WallPlacementOverlay.ComputeWallSortKey(cellY: 6, depthSlot: 0, stableId: 0);

        Assert.True(entityKey.CompareTo(wallKey) < 0);
        Assert.True(wallKey.CompareTo(entityKey) > 0);
    }

    [Fact]
    public void Ordering_EntityRowAboveWallRow_EntityDrawsInFrontOfWall()
    {
        var entityKey = EntityKeyForRow(tileRow: 8);
        var wallKey = WallPlacementOverlay.ComputeWallSortKey(cellY: 6, depthSlot: 5, stableId: 0);

        Assert.True(entityKey.CompareTo(wallKey) > 0);
    }

    private static RenderSortKey2D EntityKeyForRow(int tileRow)
    {
        var posY = tileRow * 16 * 0x10000;
        var elevation = WallPlacementOverlay.ComputeEntityElevation(posY, idsv: 0);
        return new RenderSortKey2D((int)RenderPass2D.YSortedWorld, (int)RenderPass2D.YSortedWorld, 0, elevation, 0, 0, 0);
    }

    // -------------------- Strip/resubmit accounting --------------------

    [Fact]
    public void Apply_MatchingPlacement_StripsTileAndAddsOverlayEntry()
    {
        var component = CreateHeadlessTileMapComponent();
        var records = new WallPlacementRecords
        {
            MapIndex = 1,
            Count = 1,
            CellX = new[] { 1 },
            CellY = new[] { 2 },
            StackIndex = new[] { 0 },
            Plane = new[] { 0 },
            X = new[] { 1 },
            Y = new[] { 2 },
            Gid = new[] { TileId + 1 }, // firstgid = 1, so local tile id = gid - 1 = TileId
            DepthSlot = new[] { 3 },
        };

        WallPlacementOverlay.Apply(component, records, "map_1");

        Assert.Equal(TileMapData.EmptyTileId, component.GetTileId(0, 1, 2));
        Assert.Equal(1, component.SortedOverlayTileCount);
    }

    [Fact]
    public void Apply_MismatchedGid_LeavesTileInPlaceAndSkipsOverlay()
    {
        var component = CreateHeadlessTileMapComponent();
        var records = new WallPlacementRecords
        {
            MapIndex = 1,
            Count = 1,
            CellX = new[] { 1 },
            CellY = new[] { 2 },
            StackIndex = new[] { 0 },
            Plane = new[] { 0 },
            X = new[] { 1 },
            Y = new[] { 2 },
            Gid = new[] { TileId + 2 }, // wrong: local tile id gid-1 = TileId+1, not the TileId actually there
            DepthSlot = new[] { 3 },
        };

        WallPlacementOverlay.Apply(component, records, "map_1");

        Assert.Equal(TileId, component.GetTileId(0, 1, 2));
        Assert.Equal(0, component.SortedOverlayTileCount);
    }

    [Fact]
    public void Apply_OutOfRangePlacement_IsSkippedNotThrown()
    {
        var component = CreateHeadlessTileMapComponent();
        var records = new WallPlacementRecords
        {
            MapIndex = 1,
            Count = 1,
            CellX = new[] { 99 },
            CellY = new[] { 99 },
            StackIndex = new[] { 0 },
            Plane = new[] { 0 },
            X = new[] { 99 },
            Y = new[] { 99 },
            Gid = new[] { TileId + 1 },
            DepthSlot = new[] { 0 },
        };

        var exception = Record.Exception(() => WallPlacementOverlay.Apply(component, records, "map_1"));

        Assert.Null(exception);
        Assert.Equal(0, component.SortedOverlayTileCount);
    }

    /// <summary>
    /// Builds a tile map component without a graphics device: same shape as
    /// <c>CasaEngine.Tests.TileMap.TileMapSurfaceInvalidationTests.CreateTileMapComponent</c>, with a
    /// single registered static tile (<see cref="TileId"/>) so <c>AddSortedOverlayTile</c>'s tile-id
    /// validation succeeds.
    /// </summary>
    private static TileMapComponent CreateHeadlessTileMapComponent()
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent { ChunkTileSize = MapSize };
        entity.RootComponent = component;

        var tileMapData = new TileMapData { MapSize = new Size(MapSize, MapSize) };
        var layerData = new TileMapLayerData();
        for (var tileIndex = 0; tileIndex < MapSize * MapSize; tileIndex++)
        {
            layerData.tiles.Add(TileId);
        }

        tileMapData.Layers.Add(layerData);
        component.TileMapData = tileMapData;

        var tileSetData = new TileSetData { TileSize = new Size(TilePixelSize, TilePixelSize) };
        tileSetData.AddTile(new StaticTileData { Id = TileId, Location = new Rectangle(0, 0, TilePixelSize, TilePixelSize) });
        component.TileSetData = tileSetData;

        GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var layer = new TileMapLayer(layerData);
        for (var tileIndex = 0; tileIndex < MapSize * MapSize; tileIndex++)
        {
            layer.Tiles.Add(new StubTile());
            layer.CollisionObjects.Add(null);
        }

        GetLayers(component).Add(layer);
        InvokeBuildChunks(component, layer);

        return component;
    }

    private static List<TileMapLayer> GetLayers(TileMapComponent component)
    {
        var property = typeof(TileMapComponent).GetProperty("Layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (List<TileMapLayer>)property!.GetValue(component)!;
    }

    private static List<T> GetPrivateList<T>(TileMapComponent component, string fieldName)
    {
        var field = typeof(TileMapComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (List<T>)field!.GetValue(component)!;
    }

    private static void InvokeBuildChunks(TileMapComponent component, TileMapLayer layer)
    {
        var method = typeof(TileMapComponent).GetMethod("BuildChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(component, new object[] { layer, 0 });
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class StubTile : Tile
    {
        public StubTile() : base(null)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw(float x, float y, float z, Vector2 scale)
        {
        }

        public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
        {
        }
    }
}
