using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Writers;

// Disambiguates the Phase 5 writer from System.IO.TextWriter, which the implicit usings bring in.
using TextWriter = AlundraCasaEngineProjectConverter.Writers.TextWriter;

var options = CliOptions.Parse(args);
if (options is null)
{
    return 1;
}

if (!Directory.Exists(options.InputDirectory))
{
    Console.Error.WriteLine($"Input directory not found: {options.InputDirectory}");
    return 1;
}

Console.WriteLine("Start conversion...");
Console.WriteLine($"Input : {options.InputDirectory}");
Console.WriteLine($"Output: {options.OutputDirectory}");

var report = new ConversionReport();
var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

// maps.json groups map ids into named zones (Maps/{Zone}/{Name}-{id}.*); it ships with the
// converter itself, not with data-extracted.
var mapsJsonPath = Path.Combine(AppContext.BaseDirectory, "maps.json");
var mapLocations = (IReadOnlyDictionary<int, MapLocation>)new Dictionary<int, MapLocation>();
if (File.Exists(mapsJsonPath))
{
    var mapCatalog = MapCatalogReader.Read(mapsJsonPath);
    mapLocations = mapCatalog.Locations;
    foreach (var warning in mapCatalog.Warnings)
    {
        report.Warnings.Add(warning);
    }
}
else
{
    report.Errors.Add($"maps.json not found at '{mapsJsonPath}'; maps will be placed under 'Uncategorized'.");
}

// Phase 0: bootstrap an empty CasaEngine project.
if (options.Phase >= 0)
{
    report.RunPhase("Phase0.Project", () => ProjectWriter.CreateEmptyProject(options.OutputDirectory, report));
}

// Phase 1: textures, tilesets and tilemaps from the Tiled export.
if (options.Phase >= 1)
{
    report.RunPhase("Phase1.TileMaps", () =>
        TileMapWriter.ConvertMaps(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));
}

// Phase 2: per-cell gameplay metadata merged into TileMapData.CustomProperties.
if (options.Phase >= 2)
{
    report.RunPhase("Phase2.CellMetadata", () =>
        CellMetadataWriter.ConvertMaps(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));

    // E4.a (docs/plan-e4-deplacement-scripte.md, D5): a "Navigation" layer + its shared tileset, for
    // the same maps Phase 2 just wrote AlundraCells onto. Runs from the same phase gate - it
    // read-modify-writes the .tileMap files exactly like Phase 2 does and depends on nothing later.
    report.RunPhase("Phase2.Navigation", () =>
        NavigationWriter.ConvertMaps(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));
}

// Phase 3: sprite banks, their animations, and the per-bank entity prefab. Phase 3.5 then links
// every map's "Entities" object-layer records (already written by Phase 1) to the prefab of the
// bank their SpriteTableIndex resolves to - it needs the bank -> prefab id map Phase 3 produces, so
// it must run after it, and it read-modify-writes the .tileMap files the same way Phase 2 does.
var prefabAssetIdsByBankKey = (IReadOnlyDictionary<string, Guid>)new Dictionary<string, Guid>();
if (options.Phase >= 3)
{
    report.RunPhase("Phase3.Sprites", () =>
        prefabAssetIdsByBankKey = SpriteWriter.ConvertSprites(options.InputDirectory, options.OutputDirectory, report));
    report.RunPhase("Phase3.PrefabLinks", () =>
        EntityPrefabLinkWriter.LinkMaps(
            options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations,
            prefabAssetIdsByBankKey, report));
}

// Phase 4: BGM and SFX preserved as raw WAV assets plus their manifests.
if (options.Phase >= 4)
{
    report.RunPhase("Phase4.Audio", () =>
        AudioWriter.ConvertAudio(options.InputDirectory, options.OutputDirectory, report));
}

// Phase 5: string tables (global + per map), their control-code inventory, and the bitmap font.
if (options.Phase >= 5)
{
    report.RunPhase("Phase5.Text", () =>
        TextWriter.ConvertText(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));
    report.RunPhase("Phase5.Font", () =>
        FontWriter.ConvertFont(options.InputDirectory, options.OutputDirectory, report));
}

// Phase 6: the hero's PlayerStartupSettings (.gameMode) and input mapping (.buttonsMapping) - see
// PlayerSetupWriter - then one world per map (tilemap + camera + player start, each pointing at the
// gameMode above), the MapId -> world index, and the raw event bytecode companions. Map
// entities/portals/map events stay in the .tileMap object layers Phase 1 wrote them to - see
// WorldWriter's summary. The gameMode needs the hero prefab's asset id, which only Phase 3 knows, so
// it must run after Phase 3 and before the worlds that reference it.
var playerStartupSettingsAssetId = Guid.Empty;
if (options.Phase >= 6)
{
    report.RunPhase("Phase6.PlayerSetup", () =>
    {
        playerStartupSettingsAssetId =
            PlayerSetupWriter.WriteGameMode(options.OutputDirectory, prefabAssetIdsByBankKey, report);
        PlayerSetupWriter.WriteButtonsMapping(options.OutputDirectory, report);
    });
    report.RunPhase("Phase6.Worlds", () =>
        WorldWriter.ConvertWorlds(
            options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations,
            playerStartupSettingsAssetId, report));
    report.RunPhase("Phase6.Events", () =>
        EventCodeWriter.ConvertEvents(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));
}

// Phase 7: UI sprites, standalone screen textures and the BALANCE.BIN table.
if (options.Phase >= 7)
{
    report.RunPhase("Phase7.Ui", () => UiWriter.ConvertUi(options.InputDirectory, options.OutputDirectory, report));
}

// Phase 9: each map's scrolling background layers (the PSX parallax backdrop).
if (options.Phase >= 9)
{
    report.RunPhase("Phase9.Backdrops", () =>
        BackdropWriter.ConvertBackdrops(options.InputDirectory, options.OutputDirectory, options.MapFilter, mapLocations, report));
}

// Phase 8: load every generated asset back through its engine class. On by default - assets that
// only fail at load time are otherwise silently broken until the editor opens them.
var verificationPassed = true;
if (options.Verify)
{
    Console.WriteLine("Verifying generated assets...");
    report.RunPhase("Phase8.Verify", () => verificationPassed = AssetVerifier.Verify(options.OutputDirectory, report));
}

totalStopwatch.Stop();
report.TotalDurationSeconds = totalStopwatch.Elapsed.TotalSeconds;
report.MeasureOutput(options.OutputDirectory);

report.Save(Path.Combine(options.OutputDirectory, "report.json"));
report.PrintSummary();

if (!options.Verify)
{
    Console.WriteLine("  Verification: SKIPPED (--no-verify)");
}
else if (verificationPassed)
{
    Console.WriteLine(
        $"  Verification: PASSED ({report.Counters.GetValueOrDefault("Verify.Loaded")} loaded, " +
        $"{report.Counters.GetValueOrDefault("Verify.ExistenceChecked")} existence-checked).");
}
else
{
    Console.WriteLine(
        $"  Verification: FAILED ({report.Counters.GetValueOrDefault("Verify.Failed")} assets failed, " +
        $"see report.json).");
}

return report.Errors.Count > 0 ? 1 : 0;
