using System.Drawing;
using System.Drawing.Imaging;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// D5 (docs/plan-e4-deplacement-scripte.md E4.a): appends a "Navigation" layer to every map's
/// .tileMap whose <see cref="CellMetadataReader"/> source exists - the same companion/native pair
/// <see cref="CellMetadataWriter"/> reads - so the DLL's future pathfinding (E4.d,
/// NavigationGrid2D.TryCreateFromTileMap, "navigation.role" = "grid") has a walkable/blocked grid to
/// build from. Runs after Phase 1 (the .tileMap must already exist) - independent of Phase 2's own
/// write, it re-derives the per-cell data itself the same way Phase 2 does.
///
/// Mask fact (2026-08-24, pre-check executed before this tranche): the plan originally assumed the
/// intro's four scripted walkers (map 389 records 11/12 bank 146, 15 bank 161, 18 bank 25) were
/// ClassB, justifying M = 0x41 (0x40 | ClassB). The real header data refutes that - all three banks'
/// SpriteRecord.Header.MoreFlags is 0x80 (Collidable only; full Flags 0x3A180/0x83A180), so their
/// actual mover mask (Alundra.Scripts.AlundraCellsCollisionField.WalkabilityMaskFor) is 0x40, not
/// 0x41. The plan was revised to M = 0x40 before this tranche started: only bit 6 of Walkability
/// blocks a cell in the grid (GroundProperty &lt;&lt; 8 never intersects 0x40), class restrictions
/// stay entirely at the per-entity mover (see WalkabilityMask on the CharacterControllerComponent
/// this writer's sibling, SpriteWriter, now adds to every body-having prefab) - a future ClassA/
/// ClassB entity can have a stricter per-entity mask than the grid encodes; the grid only proposes a
/// path, 0x1E's contournement (E4.d) re-navigates on an actual block.
///
/// The shared "Navigation" tileset declares exactly 2 tiles - W (id 0, navigation.walkable = "true")
/// and B (id 1, navigation.walkable = "false") - over one trivial 24x16 solid-color texture: see
/// TileMapComponent.Initialize/CreateRuntimeTile (pre-check 2), which builds a runtime Tile for every
/// non-empty cell of every layer regardless of TileMapDepthSettings.Role, so a CollisionOnly layer
/// still needs real TileData entries and a loadable tileset texture at Initialize time even though it
/// is never drawn (TileMapDepthSettings.ShouldRenderTiles is false for CollisionOnly, so the shared
/// texture costs nothing at Draw time). The tileset's own TileSize must equal 24x16 - the map's native
/// tile size - because TileMapComponent.LoadTileSets requires every tileset used by a map to share one
/// tile size.
/// </summary>
public static class NavigationWriter
{
    private const string RoleKey = "navigation.role";
    private const string DefaultWalkableKey = "navigation.defaultWalkable";
    private const string WalkableKey = "navigation.walkable";
    private const string LayerName = "Navigation";
    private const string TileSetName = "Navigation";
    private const string DataRelativeDirectory = "Data";

    // M = 0x40 (see the class doc's mask fact). Only Walkability bit 6 blocks a cell; GroundProperty
    // (shifted 8 bits) never intersects 0x40, so it is folded into the formula for fidelity with the
    // original mask expression even though it can never change the result (revised plan's own note).
    private const int WalkabilityMask = 0x40;

    private const int TileSetTileWidth = 24;
    private const int TileSetTileHeight = 16;

    public static void ConvertMaps(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
    {
        var tileSetAssetId = WriteNavigationTileSet(outputDirectory, report);
        if (tileSetAssetId == Guid.Empty)
        {
            return;
        }

        var mapIndices = mapFilter is { Count: > 0 } ? mapFilter : MapDiscovery.DiscoverMapIndices(inputDirectory);

        foreach (var mapIndex in mapIndices.OrderBy(index => index))
        {
            ConvertMap(inputDirectory, outputDirectory, mapIndex, mapLocations, tileSetAssetId, report);
        }

        EditorAssetCatalogService.Save();
    }

    /// <summary>
    /// Bakes the one shared 24x16 solid white texture (System.Drawing, same temp-bake-then-import
    /// pattern as <see cref="BackdropWriter"/>), then hand-builds the .tileset JSON (TileSetData.Load's
    /// exact shape - see TileData.Load/TileSetData.Load) and loads it back through TileSetData.Load so
    /// the id can be deterministic (Ids.For - ObjectBase.Id's setter is private, only Load(JObject) can
    /// assign it, same constraint PlayerSetupWriter's class doc documents) while still going through
    /// EditorAssetWriterService.SaveAsset's own serializer for the actual file (SaveTileSetData -
    /// EditorAssetJsonSerializer.cs) rather than a hand-written document, since TileSetData has no
    /// engine-side round-trip surprises the way PlayerStartupSettings/ButtonsMapping's misspelled keys
    /// do.
    /// </summary>
    private static Guid WriteNavigationTileSet(string outputDirectory, ConversionReport report)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineNavigationBake", Guid.NewGuid().ToString("N"));
        Guid textureAssetId;
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var tempPngPath = Path.Combine(tempDirectory, "Navigation.png");
            using (var bitmap = new Bitmap(TileSetTileWidth, TileSetTileHeight, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                }

                bitmap.Save(tempPngPath, ImageFormat.Png);
            }

            var textureCache = new Dictionary<string, Guid>();
            textureAssetId = TextureAssetWriter.EnsureTexture(tempPngPath, DataRelativeDirectory, outputDirectory, textureCache);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"Navigation: failed to bake the shared tileset texture - {exception.Message}");
            return Guid.Empty;
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        var tileSetAssetId = Ids.For("navigation:tileset");
        var tileSetNode = new JObject
        {
            ["id"] = tileSetAssetId.ToString(),
            ["name"] = TileSetName,
            ["sprite_sheet_asset_id"] = textureAssetId.ToString(),
            ["tile_size"] = new JObject { ["w"] = TileSetTileWidth, ["h"] = TileSetTileHeight },
            ["tiles"] = new JArray
            {
                BuildTileNode(id: 0, walkable: true),
                BuildTileNode(id: 1, walkable: false),
            },
        };

        var tileSetData = new TileSetData();
        tileSetData.Load(tileSetNode);

        var relativePath = Path.Combine(DataRelativeDirectory, $"{TileSetName}.tileset");
        Directory.CreateDirectory(Path.Combine(outputDirectory, DataRelativeDirectory));
        EditorAssetWriterService.SaveAsset(relativePath, tileSetData);
        EditorAssetCatalogService.Add(new AssetInfo(tileSetAssetId)
        {
            Name = TileSetName,
            FileName = relativePath,
        });

        report.Increment("Navigation.TileSets");
        return tileSetAssetId;
    }

    private static JObject BuildTileNode(int id, bool walkable)
    {
        return new JObject
        {
            ["type"] = nameof(TileType.Static),
            ["id"] = id,
            ["collision_type"] = nameof(TileCollisionType.None),
            ["is_breakable"] = false,
            ["custom_properties"] = new JObject { [WalkableKey] = walkable ? "true" : "false" },
            // TileData.Load only skips CollisionShape when the raw token stringifies to the literal
            // "null" - a JSON string, not a JSON null (see the shipped .tileset files this converter
            // already produces via the engine's own TiledMapImporter, e.g. any Static tile's
            // "collision":"null").
            ["collision"] = "null",
            ["location"] = new JObject { ["x"] = 0, ["y"] = 0, ["w"] = TileSetTileWidth, ["h"] = TileSetTileHeight },
        };
    }

    private static void ConvertMap(
        string inputDirectory,
        string outputDirectory,
        int mapIndex,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        Guid tileSetAssetId,
        ConversionReport report)
    {
        var companionPath = Path.Combine(inputDirectory, "data", "tiled", $"map_{mapIndex}.alundra.json");
        var nativeMapPath = Path.Combine(inputDirectory, "data", $"map_{mapIndex}.json");

        if (!File.Exists(companionPath) || !File.Exists(nativeMapPath))
        {
            report.Increment("Navigation.MapsWithoutCells");
            return;
        }

        var location = TileMapWriter.ResolveLocation(mapIndex, mapLocations, report);
        var tileMapRelativePath = location.TileMapRelativePath;
        var tileMapFullPath = Path.Combine(outputDirectory, tileMapRelativePath);

        if (!File.Exists(tileMapFullPath))
        {
            report.Warnings.Add($"map_{mapIndex}: tileMap asset not found at '{tileMapFullPath}' (run Phase 1 first).");
            return;
        }

        CellMetadataDocument cellMetadata;
        try
        {
            cellMetadata = CellMetadataReader.Read(companionPath, nativeMapPath, mapIndex);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"map_{mapIndex}: failed to read cell metadata for navigation - {exception.Message}");
            return;
        }

        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapFullPath)));

        var mapWidth = tileMapData.MapSize.Width;
        var mapHeight = tileMapData.MapSize.Height;
        if (mapWidth * mapHeight != cellMetadata.CellCount)
        {
            report.Errors.Add(
                $"map_{mapIndex}: navigation cell count mismatch - tileMap is {mapWidth}x{mapHeight} " +
                $"({mapWidth * mapHeight} cells), AlundraCells has {cellMetadata.CellCount}.");
            return;
        }

        tileMapData.TileSetDataAssetIds.Add(tileSetAssetId);
        var tileSetSourceIndex = tileMapData.TileSetDataAssetIds.Count - 1;

        var tilesArray = new JArray();
        var tileSourcesArray = new JArray();
        var walkableCells = 0;
        var blockedCells = 0;

        for (var index = 0; index < cellMetadata.CellCount; index++)
        {
            var maskedValue = (cellMetadata.Walkability[index] | (cellMetadata.GroundProperty[index] << 8)) & WalkabilityMask;
            var isWalkable = maskedValue == 0;
            tilesArray.Add(isWalkable ? 0 : 1);
            tileSourcesArray.Add(tileSetSourceIndex);

            if (isWalkable)
            {
                walkableCells++;
            }
            else
            {
                blockedCells++;
            }
        }

        var layerNode = new JObject
        {
            ["name"] = LayerName,
            ["z_offset"] = 0f,
            ["custom_properties"] = new JObject
            {
                [RoleKey] = "grid",
                [DefaultWalkableKey] = "false",
                [TileMapDepthSettings.RoleKey] = nameof(TileMapDepthRole.CollisionOnly),
            },
            ["tiles"] = tilesArray,
            ["tile_sources"] = tileSourcesArray,
        };

        var layer = new TileMapLayerData();
        layer.Load(layerNode);
        tileMapData.Layers.Add(layer);

        EditorAssetWriterService.SaveAsset(tileMapRelativePath, tileMapData);

        report.Increment("Navigation.Layers");
        report.Increment("Navigation.WalkableCells", walkableCells);
        report.Increment("Navigation.BlockedCells", blockedCells);
    }
}
