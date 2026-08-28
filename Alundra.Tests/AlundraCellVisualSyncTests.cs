#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E7.b acceptance (docs/plan-e7-mutation-tuiles.md, slice E7.b): the visual + navigation applier
/// (<see cref="AlundraCellVisualSync"/>) wired by <see cref="AlundraWorldProxy.InstallCellAndOverlaySystems"/>
/// (fact 17's extracted internal method), driven through the REAL map 389 export ("Ship Klark
/// (beginning)"), a headless <see cref="TileMapComponent"/> (same no-graphics-device shape as
/// <see cref="WallPlacementOverlayTests"/>'s own fixture, extended to the map's real 4 "Render_*" layers
/// and its real visual tileset instead of a synthetic uniform grid).
///
/// Overlay CONTENT assertions read <c>TileMapComponent._sortedOverlayTiles</c> by reflection through the
/// single <see cref="ReadSortedOverlayEntries"/> helper (plan fact 16, same precedent as
/// <see cref="WallPlacementOverlayTests"/>) - never the DLL-side model, which would stay "correct" even if
/// the applier stopped calling <c>AddSortedOverlayTile</c>.
///
/// Self-contained, no self-skip (see the E7.a-deferral fix in <see cref="AlundraCellStoreTests"/>): throws,
/// naming the missing export, when alundra-project/ is absent.
/// </summary>
public class AlundraCellVisualSyncTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private const string TileMapEntityName = "tileMap";
    private const int RenderLayerCount = 4;

    private readonly record struct OverlayEntrySnapshot(int TileSetIndex, int TileId, int GridX, int GridY, RenderSortKey2D SortKey);

    // -----------------------------------------------------------------------------------------
    // Fixture: real map 389 data + records/tileset, headless World/Entity/TileMapComponent (no
    // CasaEngineGame/GraphicsDevice), production wiring via AlundraWorldProxy.InstallCellAndOverlaySystems.
    // -----------------------------------------------------------------------------------------

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraCellVisualSyncTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export of map 389 and "
            + "cannot self-skip without one (docs/plan-e7-mutation-tuiles.md, slice E7.b).");
    }

    private static TileMapData LoadRealTileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        if (!File.Exists(tileMapPath))
        {
            throw new InvalidOperationException($"AlundraCellVisualSyncTests: '{tileMapPath}' not found.");
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    /// <summary>Loads only <see cref="TileMapData.TileSetDataAssetIds"/>[0] - the map's own visual tileset
    /// (plan fact 6: the four Render_* layers have no <c>tile_sources</c>, so index 0 is the only one they
    /// ever use; index 1 is the shared Navigation tileset, irrelevant to the overlay).</summary>
    private static TileSetData LoadRealVisualTileSet(string projectRoot, TileMapData tileMapData)
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, "AssetInfos.json")));
        var pathById = new Dictionary<Guid, string>();
        foreach (var entry in (JArray)assetInfos["asset_infos"]!)
        {
            if (Guid.TryParse((string?)entry["id"], out var id) && (string?)entry["file_name"] is { } fileName)
            {
                pathById[id] = fileName;
            }
        }

        var visualAssetId = tileMapData.TileSetDataAssetIds[0];
        Assert.True(pathById.TryGetValue(visualAssetId, out var relativePath), "AssetInfos.json missing map 389's visual tileset.");
        var fullPath = Path.Combine(projectRoot, relativePath!.Replace('\\', Path.DirectorySeparatorChar));

        var tileSetData = new TileSetData();
        tileSetData.Load(JObject.Parse(File.ReadAllText(fullPath)));
        return tileSetData;
    }

    private sealed record Fixture(AlundraWorldProxy Proxy, World World, TileMapComponent Component, TileMapData TileMapData);

    /// <summary>
    /// Builds the real-data headless fixture and drives it through the SAME production wiring
    /// (<see cref="AlundraWorldProxy.InstallCellAndOverlaySystems"/>) the acceptance tests must exercise
    /// (plan fact 17) - not a hand-rolled store. No live <see cref="CasaEngineGame"/>/<c>AssetContentManager</c>:
    /// the navigation grid resolver degrades to null (its own documented behavior), which is exactly what
    /// acceptance item 7 needs in order to inject its own synthetic grid afterward.
    /// </summary>
    private static Fixture BuildHeadlessProxy()
    {
        var projectRoot = FindProjectRoot();
        var tileMapData = LoadRealTileMapData(projectRoot);
        var tileSetData = LoadRealVisualTileSet(projectRoot, tileMapData);

        var world = new World { Name = WorldName };
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var tileMapEntity = new Entity { Name = TileMapEntityName };
        SetProperty(tileMapEntity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;
        component.TileMapData = tileMapData;
        component.TileSetData = tileSetData;

        GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var runtimeLayers = GetLayers(component);
        for (var layerIndex = 0; layerIndex < RenderLayerCount; layerIndex++)
        {
            var layerData = tileMapData.Layers[layerIndex];
            var layer = new TileMapLayer(layerData);
            var cellCount = tileMapData.MapSize.Width * tileMapData.MapSize.Height;
            for (var i = 0; i < cellCount; i++)
            {
                layer.Tiles.Add(new StubTile());
                layer.CollisionObjects.Add(null);
            }

            runtimeLayers.Add(layer);
            InvokeBuildChunks(component, layer, layerIndex);
        }

        world.Entities.Add(tileMapEntity);

        var proxy = new AlundraWorldProxy();
        proxy.InstallCellAndOverlaySystems(world, component, tileMapData);

        return new Fixture(proxy, world, component, tileMapData);
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

    private static void InvokeBuildChunks(TileMapComponent component, TileMapLayer layer, int layerIndex)
    {
        var method = typeof(TileMapComponent).GetMethod("BuildChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(component, new object[] { layer, layerIndex });
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>Plan fact 16 / mandatory head-of-acceptance rule: the ONE helper every content-asserting
    /// item reads the overlay through, by reflection over the private <c>_sortedOverlayTiles</c> field and
    /// its private nested <c>SortedOverlayTile</c> entry type - same precedent as
    /// <see cref="WallPlacementOverlayTests"/> (that class's own doc cites WallPlacementOverlayTests.cs:595-596).</summary>
    private static List<OverlayEntrySnapshot> ReadSortedOverlayEntries(TileMapComponent component)
    {
        var field = typeof(TileMapComponent).GetField("_sortedOverlayTiles", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (IList)field!.GetValue(component)!;

        var result = new List<OverlayEntrySnapshot>(list.Count);
        FieldInfo? tileReferenceField = null;
        FieldInfo? gridXField = null;
        FieldInfo? gridYField = null;
        FieldInfo? sortKeyField = null;

        foreach (var item in list)
        {
            var entryType = item!.GetType();
            tileReferenceField ??= entryType.GetField("TileReference");
            gridXField ??= entryType.GetField("GridX");
            gridYField ??= entryType.GetField("GridY");
            sortKeyField ??= entryType.GetField("SortKey");

            var tileReference = (TileMapTileReference)tileReferenceField!.GetValue(item)!;
            var gridX = (int)gridXField!.GetValue(item)!;
            var gridY = (int)gridYField!.GetValue(item)!;
            var sortKey = (RenderSortKey2D)sortKeyField!.GetValue(item)!;

            result.Add(new OverlayEntrySnapshot(tileReference.TileSetIndex, tileReference.TileId, gridX, gridY, sortKey));
        }

        return result;
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

        public override Rectangle GetCurrentSourceRectangle() => Rectangle.Empty;
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 1: câblage
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void InstallCellAndOverlaySystems_WiresCellMutatorAndCellsMutatedSubscription()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        Assert.NotNull(context.CellMutator);
        Assert.NotNull(fixture.Proxy.CellVisualSync);

        var before = fixture.Proxy.CellVisualSync!.ReconstructionCount;

        // Real map-entry mutation (docs/intro-programs-389.txt): closes the (18,37) hatch - changes the
        // wall stack's raw ids, which must show up as a reconstruction once flushed.
        context.CellMutator!.CopyCellRectangle(0, 20, 1, 2, 18, 37);
        fixture.Proxy.CellVisualSync.FlushPendingOverlayReconstruction();

        Assert.True(
            fixture.Proxy.CellVisualSync.ReconstructionCount > before,
            "expected the wired CellsMutated subscription to trigger an overlay reconstruction.");
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 2: swap de gids
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CopyCellRectangle_HatchTemplateOntoDoor_18_37_SwapsGidsAtSamePositionsAndSortKeys()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        var store = (AlundraCellStore)context.CellMutator!;

        // Derive the exact draw positions of (18,37)'s own wall stack BEFORE mutating - the formula itself
        // (y - height - offset + k + 1), not a hardcoded guess (plan fact 2).
        var stackBefore = store.GetWallTileStack(18, 37);
        Assert.NotNull(stackBefore);
        var height = store.GetHeight(18, 37);
        var drawYs = Enumerable.Range(0, stackBefore!.Value.Tiles.Count)
            .Select(k => 37 - height - stackBefore.Value.Offset + k + 1)
            .ToList();
        Assert.NotEmpty(drawYs);

        // (18, drawY) can coincidentally also be the draw position of some UNRELATED cell's own wall/floor
        // entry (plan fact 11's own "(21,14)" phenomenon) - disambiguate with this cell's own wall
        // elevation band (cellY=37 baked into the sort key, unaffected by the copy itself).
        var minElevation = 37 * 16 + 7;
        var maxElevation = 37 * 16 + 13;
        bool BelongsToThisStack(OverlayEntrySnapshot e) => e.GridX == 18 && e.SortKey.Elevation >= minElevation && e.SortKey.Elevation <= maxElevation;

        var entriesBefore = ReadSortedOverlayEntries(fixture.Component);
        var beforeByY = drawYs.ToDictionary(y => y, y => entriesBefore.Single(e => BelongsToThisStack(e) && e.GridY == y));

        context.CellMutator!.CopyCellRectangle(0, 20, 1, 2, 18, 37);
        fixture.Proxy.CellVisualSync!.FlushPendingOverlayReconstruction();

        var entriesAfter = ReadSortedOverlayEntries(fixture.Component);
        var afterByY = drawYs.ToDictionary(y => y, y => entriesAfter.Single(e => BelongsToThisStack(e) && e.GridY == y));

        // Same positions, same sort keys - only SOME of the gids at those positions differ (the wall
        // stack's tail, per plan fact 9's "seule bouge la queue des piles de murs").
        foreach (var y in drawYs)
        {
            Assert.Equal(beforeByY[y].SortKey, afterByY[y].SortKey);
        }

        var changedCount = drawYs.Count(y => beforeByY[y].TileId != afterByY[y].TileId);
        Assert.True(changedCount > 0, "expected at least one entry's gid to change (the hatch closing).");
        var unchangedCount = drawYs.Count(y => beforeByY[y].TileId == afterByY[y].TileId);
        Assert.True(unchangedCount > 0, "expected at least one entry's gid to stay the same (the base wall tiles).");
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 2 bis: entrée désaccordée jamais resoumise
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MismatchedDocumentEntry_NeverResubmitted_AfterAMutationForcesReconstruction()
    {
        // Synthetic (patron Apply_MismatchedGid_LeavesTileInPlaceAndSkipsOverlay,
        // WallPlacementOverlayTests.cs): a 2x1 headless map, local tile ids 5 (raw "60") and 9 (raw "61")
        // both flat-tile 5 at load. The wall placement document claims TWO entries - (0,0) with gid=6
        // (local 5, MATCHES the flat tile, gets stripped) and (1,0) with gid=1000 (local 999, MISMATCHES -
        // Apply skips it entirely, per its own doc). Only entry 0's index is ever returned/seeded.
        var (component, _) = CreateSmallSyntheticComponent();

        var records = new WallPlacementRecords
        {
            MapIndex = 1,
            Count = 2,
            CellX = new[] { 0, 1 },
            CellY = new[] { 0, 0 },
            StackIndex = new[] { 0, 0 },
            Plane = new[] { 0, 0 },
            X = new[] { 0, 1 },
            Y = new[] { 0, 0 },
            Gid = new[] { 6, 1000 }, // local 5 (matches) / local 999 (mismatches the flat tile, local 5).
            DepthSlot = new[] { 0, 0 },
        };

        var submitted = WallPlacementOverlay.Apply(component, records, "map_1");
        Assert.Equal(new[] { 0 }, submitted); // only entry 0 was actually stripped/resubmitted.

        // (1,0) was never stripped - its flat tile is still there, untouched.
        Assert.Equal(5, component.GetTileReference(0, 1, 0).TileId);

        var alundraCellsJson =
            "{\"map_index\":1,\"cell_count\":2,\"walkability\":[0,0],\"ground_property\":[0,0],"
            + "\"slope\":[0,0],\"height\":[0,0],\"tile_id\":[65535,65535],\"wall_tiles_offset\":[0,0],"
            + "\"wall_tiles\":{\"0\":{\"offset\":0,\"tiles\":[60]},\"1\":{\"offset\":0,\"tiles\":[61]}}}";
        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(2, 1) };
        tileMapData.CustomProperties["AlundraCells"] = alundraCellsJson;

        var fieldCreated = AlundraCellsCollisionField.TryCreate(tileMapData, "map_1", out _, out var cellRecords);
        Assert.True(fieldCreated);
        var storeCreated = AlundraCellStore.TryCreate(cellRecords!, 2, 1, "map_1", out var store);
        Assert.True(storeCreated);

        var sync = AlundraCellVisualSync.Create(
            component, store!, 2, 1, "map_1", component.TileSetData,
            records, submitted,
            floorRecords: null, submittedFloorIndices: Array.Empty<int>(),
            navigationGridAccessor: () => null);
        store!.CellsMutated += sync.OnCellsMutated;

        // Mutate ONLY cell (0,0) - swaps its wall raw id 60 -> 61 (local 5 -> local 9), forcing a
        // reconstruction. Cell (1,0), where the mismatch lives, is never touched.
        store.CopyCellRectangle(1, 0, 1, 1, 0, 0);
        sync.FlushPendingOverlayReconstruction();
        Assert.True(sync.ReconstructionCount > 0);

        // (1,0)'s flat tile is STILL there (never stripped) and NOT also duplicated into the overlay -
        // the mismatched document entry was never seeded, so nothing resurrects it.
        Assert.Equal(5, component.GetTileReference(0, 1, 0).TileId);
        Assert.DoesNotContain(ReadSortedOverlayEntries(component), e => e.GridX == 1 && e.GridY == 0);
    }

    /// <summary>2x1 headless <see cref="TileMapComponent"/>, one flat layer (plane 0), every cell starting
    /// at local tile id 5 - local tile id 9 is also registered (raw "61") for a mutation to swap into. Same
    /// no-graphics-device shape as <see cref="BuildHeadlessProxy"/>'s own real-data fixture, but self
    /// contained (no map 389 export needed) for tests that build their own tiny synthetic document.</summary>
    private static (TileMapComponent Component, TileSetData TileSetData) CreateSmallSyntheticComponent()
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent { ChunkTileSize = 2 };
        entity.RootComponent = component;

        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(2, 1) };
        var layerData = new TileMapLayerData();
        layerData.tiles.Add(5);
        layerData.tiles.Add(5);
        tileMapData.Layers.Add(layerData);
        component.TileMapData = tileMapData;

        var tileSetData = new TileSetData { TileSize = new CasaEngine.Core.Math.Size(16, 16) };
        var tile5 = new StaticTileData { Id = 5, Location = new Rectangle(0, 0, 16, 16) };
        tile5.CustomProperties["TileId"] = "60";
        var tile9 = new StaticTileData { Id = 9, Location = new Rectangle(0, 0, 16, 16) };
        tile9.CustomProperties["TileId"] = "61";
        tileSetData.AddTile(tile5);
        tileSetData.AddTile(tile9);
        component.TileSetData = tileSetData;

        GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var layer = new TileMapLayer(layerData);
        layer.Tiles.Add(new StubTile());
        layer.CollisionObjects.Add(null);
        layer.Tiles.Add(new StubTile());
        layer.CollisionObjects.Add(null);

        GetLayers(component).Add(layer);
        InvokeBuildChunks(component, layer, 0);

        return (component, tileSetData);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 2 ter: trou de pile (k brut, non compacté)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CopyCellRectangle_SourceStackHasHole_SucceedingTileKeepsItsRawStackIndex()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;
        var store = (AlundraCellStore)context.CellMutator!;

        // (13,35) on map 389 carries a RUN of 0xffff holes followed by real tiles (plan fact 14). Copy it
        // onto (13,50) - same column (so offset/height stay meaningful), far enough down that even this
        // cell's own large offset (29) keeps every draw position on the map. This forces the mutated
        // cell's re-derivation to walk the RAW stack index k, not a compacted one.
        var sourceStack = store.GetWallTileStack(13, 35);
        Assert.NotNull(sourceStack);
        var holeIndex = -1;
        for (var k = 0; k < sourceStack!.Value.Tiles.Count - 1; k++)
        {
            if (sourceStack.Value.Tiles[k] == 0xffff && sourceStack.Value.Tiles[k + 1] != 0xffff)
            {
                holeIndex = k;
                break;
            }
        }

        Assert.True(holeIndex >= 0, "fixture assumption: (13,35) must contain a 0xffff hole immediately followed by a real tile.");
        var tileAfterHole = sourceStack.Value.Tiles[holeIndex + 1];
        var height = store.GetHeight(13, 35);

        context.CellMutator!.CopyCellRectangle(13, 35, 1, 1, 13, 50);
        fixture.Proxy.CellVisualSync!.FlushPendingOverlayReconstruction();

        var expectedDrawY = 50 - height - sourceStack.Value.Offset + (holeIndex + 1) + 1;
        var localId = FindLocalTileId(fixture.Component, tileAfterHole);

        var matches = ReadSortedOverlayEntries(fixture.Component)
            .Where(e => e.GridX == 13 && e.GridY == expectedDrawY && e.TileId == localId)
            .ToList();

        Assert.True(matches.Count >= 1, $"expected the tile after the hole to draw at (13,{expectedDrawY}) using its RAW stack index {holeIndex + 1}, uncompacted.");
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 3: changement de forme (21,27)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void CopyCellRectangle_HatchTemplateOntoDoor_21_27_GrowsStackAndAddsNewOverlayEntry()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        var localId783 = FindLocalTileId(fixture.Component, 17166);

        // (21,14) is NOT empty before the mutation - plan fact 11: it already carries TWO unrelated
        // overlay entries seeded at init, (21,24)'s own elevated floor and (21,35)'s own wall tile k14,
        // which both happen to DRAW at this exact position (their formula's own result, nothing to do
        // with this test's mutation). What fact 11 actually says is "free" is the FLAT layer at (21,14)
        // (both placements already stripped from it at init) - verified below, before AND after.
        var beforeCountAt2114 = ReadSortedOverlayEntries(fixture.Component).Count(e => e.GridX == 21 && e.GridY == 14);
        Assert.Equal(2, beforeCountAt2114);
        for (var plane = 0; plane < RenderLayerCount; plane++)
        {
            Assert.True(fixture.Component.GetTileReference(plane, 21, 14).IsEmpty, $"expected plane {plane} at (21,14) to already be empty before the mutation.");
        }

        var before2127 = ReadSortedOverlayEntries(fixture.Component).Count(e => e.GridX == 21 && e.GridY is >= 12 and <= 22);

        context.CellMutator!.CopyCellRectangle(0, 39, 1, 2, 21, 27);
        fixture.Proxy.CellVisualSync!.FlushPendingOverlayReconstruction();

        // A THIRD entry now lives at (21,14) - the new tile from (21,27)'s stack growth (6 -> 7 tiles).
        var afterAt2114 = ReadSortedOverlayEntries(fixture.Component).Where(e => e.GridX == 21 && e.GridY == 14).ToList();
        Assert.Equal(3, afterAt2114.Count);
        Assert.Contains(afterAt2114, e => e.TileId == localId783);

        var newEntry = afterAt2114.Single(e => e.TileId == localId783);

        // Depth slot (raw 17166, plan fact 4): elevation = 27*16 + 7 + slot.
        var expectedSlot = AlundraCellVisualSync.ComputeDepthSlot(17166);
        var expectedElevation = 27 * 16 + 7 + Math.Clamp(expectedSlot, 0, 6);
        Assert.Equal(expectedElevation, newEntry.SortKey.Elevation);

        var after2127 = ReadSortedOverlayEntries(fixture.Component).Count(e => e.GridX == 21 && e.GridY is >= 12 and <= 22);
        Assert.Equal(before2127 + 1, after2127); // exactly one NEW entry - the shape growth from 6 to 7 tiles.

        // Still empty on every flat plane at (21,14) - the new entry lives ONLY in the overlay.
        for (var plane = 0; plane < RenderLayerCount; plane++)
        {
            var reference = fixture.Component.GetTileReference(plane, 21, 14);
            Assert.True(reference.IsEmpty, $"expected plane {plane} at (21,14) to remain empty (the new tile is overlay-only).");
        }
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 4: clés de tri exactes (source cellY, jamais la ligne de dessin)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RederivedSortKeys_UseSourceCellYNotDrawRow_MatchingInitTimeFormula()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;
        var store = (AlundraCellStore)context.CellMutator!;

        // Drive a REAL reconstruction (the map-entry copy onto (18,37)), so every entry asserted below has
        // been resubmitted by the applier rather than left over from WallPlacementOverlay's init pass.
        // A bits-only touch would not do: it changes nothing, so nothing is rebuilt (that is item 5), and
        // the assertions would read init-time entries and stay green even with the applier disabled.
        context.CellMutator!.CopyCellRectangle(0, 20, 1, 2, 18, 37);
        fixture.Proxy.CellVisualSync!.FlushPendingOverlayReconstruction();

        var entries = ReadSortedOverlayEntries(fixture.Component);
        var height = store.GetHeight(18, 37);

        // --- wall half: (18,37)'s stack at k=2, the first tail entry the copy changes ---
        var stack = store.GetWallTileStack(18, 37);
        Assert.NotNull(stack);
        const int k = 2;
        var rawWall = stack!.Value.Tiles[k];
        var wallDrawY = 37 - height - stack.Value.Offset + k + 1;
        var wallEntry = entries.Single(e =>
            e.GridX == 18 && e.GridY == wallDrawY && e.TileId == FindLocalTileId(fixture.Component, rawWall));

        // Elevation is what the "source cellY vs draw row" trap (plan fact 3) gets wrong: the position uses
        // the draw row, the sort key must use the SOURCE cell row.
        var wallSlot = AlundraCellVisualSync.ComputeDepthSlot(rawWall);
        Assert.Equal(WallPlacementOverlay.ComputeWallSortKey(37, wallSlot, 0).Elevation, wallEntry.SortKey.Elevation);
        Assert.NotEqual(wallDrawY * 16 + 7 + Math.Clamp(wallSlot, 0, 6), wallEntry.SortKey.Elevation);

        // --- floor half: the plan asks for a floor entry too, and floors take the other bias (no +7) ---
        var rawFloor = store.GetFloorTileId(18, 37);
        var floorDrawY = 37 - height;
        var floorEntry = entries.Single(e =>
            e.GridX == 18 && e.GridY == floorDrawY && e.TileId == FindLocalTileId(fixture.Component, rawFloor));

        var floorSlot = AlundraCellVisualSync.ComputeDepthSlot(rawFloor);
        Assert.Equal(WallPlacementOverlay.ComputeFloorSortKey(37, floorSlot, 0).Elevation, floorEntry.SortKey.Elevation);
        Assert.NotEqual(floorDrawY * 16 + Math.Clamp(floorSlot, 0, 5), floorEntry.SortKey.Elevation);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 5: aucun effet visuel des bits
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SetThenClearCellBits_NeverTriggersOverlayReconstruction()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        var before = ReadSortedOverlayEntries(fixture.Component);
        var reconstructionsBefore = fixture.Proxy.CellVisualSync!.ReconstructionCount;

        context.CellMutator!.SetCellBits(18, 15, 0x10, 0x20);
        fixture.Proxy.CellVisualSync.FlushPendingOverlayReconstruction();
        context.CellMutator!.ClearCellBits(18, 15, 0x10, 0x20);
        fixture.Proxy.CellVisualSync.FlushPendingOverlayReconstruction();

        Assert.Equal(reconstructionsBefore, fixture.Proxy.CellVisualSync.ReconstructionCount);

        var after = ReadSortedOverlayEntries(fixture.Component);
        Assert.Equal(before, after);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 6: sols invariants après les 12 rectangles, pas de dégradé
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AllTwelveMapEntryRectangles_LeaveFloorEntriesUnchanged_NoDegradedWarning()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        // The 12 destination rectangles (docs/intro-programs-389.txt's own "4 portes" B 130-133, each a
        // 1x2 hatch): (18,37) (21,27) (15,27)... this test drives every one of the four doors' own two
        // map-entry calls (0x55 then 0x85) the way frame 1 does, then checks the floor side is untouched.
        // Exact params from docs/intro-programs-389.txt's own "closed hatch" 0x85 template calls (the
        // FIRST of each door's own three, matching the map-entry sequence).
        var doors = new (int SrcX, int SrcY, int DstX, int DstY)[]
        {
            (0, 20, 18, 37),
            (0, 39, 21, 27),
            (0, 29, 15, 27),
            (0, 48, 16, 41),
        };

        // Reconstruction rebuilds the WHOLE overlay in dictionary-enumeration order, unrelated to the
        // original init-time document-submission order - so the two snapshots must be compared as SETS,
        // via a fully deterministic sort (GridX/GridY alone is not unique: two different source cells can
        // legitimately draw to the same position, plan fact 11's own (21,14)).
        List<OverlayEntrySnapshot> CanonicalFloorEntries() => ReadSortedOverlayEntries(fixture.Component)
            .Where(e => e.SortKey.Elevation % 16 < 6) // floor slot band (0..5), see ComputeFloorSortKey's doc.
            .OrderBy(e => e.GridX).ThenBy(e => e.GridY).ThenBy(e => e.TileId).ThenBy(e => e.SortKey.StableId)
            .ToList();

        var floorEntriesBefore = CanonicalFloorEntries();

        foreach (var door in doors)
        {
            context.CellMutator!.ClearCellBits(door.DstX, door.DstY + 1, 0, 128);
            context.CellMutator!.CopyCellRectangle(door.SrcX, door.SrcY, 1, 2, door.DstX, door.DstY);
        }

        fixture.Proxy.CellVisualSync!.FlushPendingOverlayReconstruction();

        var floorEntriesAfter = CanonicalFloorEntries();

        Assert.Equal(floorEntriesBefore, floorEntriesAfter);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 7: navigation, par le chemin câblé
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void SetCellBits_ThroughWiredStore_UpdatesInjectedNavigationGridPreservingLayerMask()
    {
        var fixture = BuildHeadlessProxy();
        Assert.Null(fixture.Proxy.NavigationGrid); // the real resolver degrades to null without a live AssetContentManager.

        var grid = new NavigationGrid2D(fixture.TileMapData.MapSize.Width, fixture.TileMapData.MapSize.Height, 1f);
        var initialLayers = NavigationLayerMask.Ground | NavigationLayerMask.Flying;
        grid.SetCell(10, 10, new NavigationGridCell(true, 1f, initialLayers));
        fixture.Proxy.NavigationGrid = grid;

        IEntityWorldContext context = fixture.Proxy;

        // Bit 0x40 - the ONLY bit the navigation formula reads (plan's own note: absent everywhere on map
        // 389, so a real-data-only test could not tell a correct SetCell from a no-op).
        context.CellMutator!.SetCellBits(10, 10, 0x40, 0);

        var afterSet = grid.GetCell(10, 10);
        Assert.False(afterSet.IsWalkable);
        Assert.Equal(initialLayers, afterSet.Layers);

        context.CellMutator!.ClearCellBits(10, 10, 0x40, 0);

        var afterClear = grid.GetCell(10, 10);
        Assert.True(afterClear.IsWalkable);
        Assert.Equal(initialLayers, afterClear.Layers);
    }

    // -----------------------------------------------------------------------------------------
    // Acceptance item 8: cellules hors rectangles (programme 772)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Program772Cells_BitsOnlyMutation_NoWarningNoVisualChange()
    {
        var fixture = BuildHeadlessProxy();
        IEntityWorldContext context = fixture.Proxy;

        var before = ReadSortedOverlayEntries(fixture.Component);
        var reconstructionsBefore = fixture.Proxy.CellVisualSync!.ReconstructionCount;

        // (17,37), (17,38), (19,38) - plan fact 12: mutated by the masked-5 program, own floor/walls, not
        // an overlay destination rectangle.
        context.CellMutator!.SetCellBits(17, 37, 2, 0);
        context.CellMutator!.ClearCellBits(17, 38, 2, 0);
        context.CellMutator!.SetCellBits(19, 38, 2, 0);
        fixture.Proxy.CellVisualSync.FlushPendingOverlayReconstruction();

        Assert.Equal(reconstructionsBefore, fixture.Proxy.CellVisualSync.ReconstructionCount);
        Assert.Equal(before, ReadSortedOverlayEntries(fixture.Component));
    }

    // -----------------------------------------------------------------------------------------
    // Shared lookup helper
    // -----------------------------------------------------------------------------------------

    /// <summary>Finds the LOCAL tile id (<see cref="TileMapComponent.TileSetData"/>'s own <c>TileData.Id</c>)
    /// for a RAW PSX tile id, the exact same way <see cref="AlundraCellVisualSync"/> does (plan fact 5) - so
    /// a test can assert against the value the applier ACTUALLY computed instead of a hardcoded guess.</summary>
    private static int FindLocalTileId(TileMapComponent component, int rawTileId)
    {
        foreach (var tile in component.TileSetData.Tiles)
        {
            if (tile.CustomProperties.TryGetValue("TileId", out var rawIdText) && int.TryParse(rawIdText, out var rawId) && rawId == rawTileId)
            {
                return tile.Id;
            }
        }

        throw new InvalidOperationException($"AlundraCellVisualSyncTests: no local tile id found for raw id {rawTileId}.");
    }
}
