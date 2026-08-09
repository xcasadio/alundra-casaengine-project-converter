# alundra-casaengine-project-converter

A command-line tool that converts an extracted PSX Alundra data set (`data-extracted/`, produced
by [`alundra-datas-analyser`](https://github.com/xcasadio/alundra-datas-analyser)) into a
loadable [CasaEngine](https://github.com/xcasadio/CasaEngineMonogame) project: maps, sprites,
audio, text, worlds, and everything else the engine can represent, plus a handful of JSON
"companion" formats for the data it cannot (see [`docs/formats/`](docs/formats/README.md)).

The converter is headless — it never starts a `CasaEngineGame` or opens a `GraphicsDevice` — so it
writes documents the engine's own `Load()` methods can read later, and (by default) verifies that
they actually do, in the same run.

## Prerequisites

- **.NET 9 SDK** (`net9.0-windows`).
- The **`CasaEngineMonogame`** submodule, initialized: `git submodule update --init CasaEngineMonogame`.
  The converter project-references `CasaEngine`, `CasaEngine.EditorServices` and the `MGUI.*`
  projects directly out of that checkout.
- The **`alundra-datas-analyser`** repository checked out as a **sibling** of this repo (i.e. next
  to it, not inside it): the csproj links `EntityNames.csv` from
  `..\alundra-datas-analyser\AlundraTools\AlundraTools\EntityNames.csv` rather than copying it, so
  the converter and the game engine's own `AlundraEngine.EntityNames` reader always agree on entity
  names. Without it, Phase 3 fails immediately (see below).
- An extracted **`data-extracted/`** folder (see `alundra-datas-analyser` for how to produce one)
  to pass as the input directory.

## Build and test

```
dotnet build alundra-casaengine-project-converter.slnx
dotnet test alundra-casaengine-project-converter.slnx
```

## CLI usage

```
alundra-casaengine-project-converter <inputDir> <outputDir> [--maps 0,4,10] [--phase N] [--no-verify]
```

- `<inputDir>` — path to `data-extracted/`.
- `<outputDir>` — path to the CasaEngine project to create (created if missing; existing files are
  overwritten in place).
- `--phase N` — run phases **0 through N inclusive**, then stop. It does *not* run phase `N` alone
  — there is no way to run a single phase in isolation, because later phases generally depend on
  the assets earlier ones wrote (e.g. Phase 2 merges into the `.tileMap` files Phase 1 produced).
  Omit the flag to run every phase (0–8, verification included).
- `--maps 0,4,10` — restrict every per-map phase (1, 2, 3's map-derived data, 5, 6) to this
  comma-separated list of Alundra map indices, instead of every map found in `data-extracted/`.
  Useful for fast iteration on one or a few maps. Phase 6's world-count invariant check (see below)
  only runs on a full, unfiltered run.
- `--no-verify` — skip Phase 8 (asset verification). Useful for fast iteration; the full run should
  always include verification before you trust the output.

Example invocations:

```
# Full conversion, all 8 phases, verification included
alundra-casaengine-project-converter data-extracted out\AlundraGame

# Iterate on just three maps, phases 0-3 only, no verification
alundra-casaengine-project-converter data-extracted out\AlundraGame --maps 0,4,10 --phase 3 --no-verify

# Full conversion but skip the verification pass
alundra-casaengine-project-converter data-extracted out\AlundraGame --no-verify
```

## The 8 phases

| # | Reads | Writes |
|---|---|---|
| 0 | — | An empty CasaEngine project: `<Project>.json`, empty `AssetInfos.json`, and the top-level content folders. |
| 1 | `data/tiled/map_N.tmj` (Tiled export) | `Maps/{Zone}/{Name}-{id}/tilemap/{Name}-{id}.tmj/.tileMap/.tileset/.texture` via the engine's own `TiledMapImporter`. |
| 2 | `data/tiled/map_N.alundra.json` + `data/map_N.json` | Merges per-cell gameplay metadata into each `.tileMap`'s `CustomProperties["AlundraCells"]`. |
| 3 | `data/map_N.json` `SpriteInfo.SpriteRecords`, `data/map_alundra.json`, `EntityNames.csv` | `Entities/<EntityName>/*.sprite` + `*.anim2d`, spritesheets under `Sprites/Textures/`, hero effects under `Sprites/hero/`. |
| 4 | `sound/bgm.json`, `sound/sfx.json`, `sound/bgm/*.wav`, `sound/sfx/*.wav` | Raw WAV copies under `Musics/` / `Sounds/`, plus their manifests. |
| 5 | `data/ETC_RES.R.json`, `data/map_N.json` `Strings`, `ui/font3.json`, `ui/font3.png` | `Dialogues/global-strings.json`, `Maps/{Zone}/{Name}-{id}/dialogues/{Name}-{id}.strings.json`, `Dialogues/control-codes.json`, `UI/font3.fnt` + `UI/font3-charset.json`. |
| 6 | `.tileMap` assets from Phase 1, `data/map_N.json` | One `.world` per map at the root of its map folder (`Maps/{Zone}/{Name}-{id}/{Name}-{id}.world`), `Maps/world-index.json`, the project's `FirstWorldLoaded`, and `Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json` (raw event bytecode). |
| 7 | `ui/wind.json` + `ui/wind.png`, `memorycard/`, `closing/`, `data/loading_screen.png`, `data/BALANCE.BIN.json` | `UI/*.sprite` + `UI/wind-sprites.json`, catalogued screen textures under `UI/Textures/`, `Data/balance.json`. |
| 8 | Every asset registered in `AssetInfos.json` | Nothing — loads each one back through its engine class (or existence-checks it) and records the result in `report.json`. Runs by default; `--no-verify` skips it. |

## Output layout

Top-level folders of a converted project (from a real full run):

```
<outputDir>/
  AlundraGame.json        project settings (FirstWorldLoaded points at map 389, the New Game map)
  AssetInfos.json         the asset catalog: every asset's id, name and file name
  report.json             counters, warnings, errors, messages, metrics (see below)
  Maps/                   everything belonging to a map lives in that map's own folder
    world-index.json       MapId -> relative path of that map's .world
    {Zone}/{Name}-{id}/    one folder per map, grouped by the zone its maps.json entry names
      tilemap/              {Name}-{id}.tmj / .tileMap / .tileset / .texture + map_{id}_tileset.png
      dialogues/            {Name}-{id}.strings.json — that map's 128 dialogue lines
      events/               {Name}-{id}.events.json — raw event bytecode (not a CasaEngine asset)
      {Name}-{id}.world     the map's world, at the root of its folder
  TileSets/               (present but currently empty — tilesets are written next to their map under Maps/)
  Textures/               (present but currently empty — map textures also live under Maps/)
  Entities/<EntityName>/  one folder per sprite bank, named by EntityNames.csv; holds that bank's
                           .sprite and .anim2d assets (NOT "Sprites/bank_<id>/" — banks are grouped
                           by the entity name the game itself uses, e.g. Entities/Alundra for the hero)
  Sprites/
    Textures/              spritesheet PNGs + .texture wrappers, shared across entities
    hero/hero_effects.json companion: hero SpriteEffectRecords, not converted to sprites in V1
  Animations/             (present but currently empty — animations live under Entities/<Name>/)
  Sounds/                 sfx_NNNN.wav + sfx-manifest.json
  Musics/                 bgm_NNN.wav + bgm-manifest.json
  Dialogues/              only the two tables that belong to no single map
    global-strings.json    the ETC_RES.R string table
    control-codes.json     inventory of control-code tokens found in the strings
  UI/
    font3.fnt, font3-charset.json     bitmap font + raw-code/codepoint mapping
    *.sprite, wind-sprites.json       UI element crops from ui/wind.png
    Textures/                         UI PNGs (wind.png, memory-card frames, ending screens, loading screen)
  Data/balance.json        BALANCE.BIN.json, recopied with unknown fields kept
```

See [`docs/formats/`](docs/formats/README.md) for the schema of every companion JSON format
(`AlundraCells`, the audio manifests, the text tables, the font charset, the event bytecode, the
world index, and the miscellaneous data files).

## `report.json`

Written to `<outputDir>/report.json` after every run (even a partial one via `--phase`). Shape:

- **`Counters`** — a flat `name -> int` map, one entry per thing the run counted (`Maps`,
  `Assets.Sprite`, `Worlds.Portals`, `Verify.Loaded.anim2d`, …). Printed sorted by name at the end
  of the run.
- **`Metrics`** — `TotalDurationSeconds`, a `PhaseDurationsSeconds` map (wall-clock per phase, e.g.
  `"Phase3.Sprites": 14.203`), `OutputSizeBytes` / `OutputSizeMegabytes`, `OutputFileCount`.
- **`WarningsByCategory`** — the flat `Warnings` list grouped by its message prefix up to the first
  `:` (digit runs normalised to `#`, so `map_4: ...` and `map_311: ...` land in the same bucket),
  each with its count and up to 3 example messages. A convenience summary; `Warnings` stays the
  authoritative list.
- **`Warnings`** — every warning, in the order it was raised. Non-fatal: missing optional source
  files, unreferenced WAVs, name collisions, font-table quirks, and so on.
- **`Errors`** — failures that made the run incomplete or wrong (a required source file missing, a
  write that threw, a verification failure, an invariant mismatch). **A non-zero error count makes
  the process exit with code 1.**
- **`Messages`** — informational notes worth surfacing once (e.g. confirming accented French
  round-tripped correctly, or that the font is monospaced).

## Verification (Phase 8)

`AssetVerifier` reads `AssetInfos.json` back and, for every registered asset, either:

- **Loads it through the actual engine class** that owns its format — the same `Load()` the editor
  calls — for extensions with a registered, GraphicsDevice-free `AssetLoader<T>`: `.tileMap`,
  `.tileset`, `.sprite`, `.anim2d`, `.texture`, `.world`. A malformed document throws here instead
  of silently failing later as a swallowed "IAssetLoader can't load ..." at runtime.
- **Only existence- and non-emptiness-checks it** for everything else, because no headless loader
  exists for it: `.wav` has no engine loader at all (`Sound` is a thin runtime wrapper with no
  serialized form), a raw `.png` needs a live `GraphicsDevice` to decode, and plain companion files
  (`.tmj`, `.fnt`, `.json` manifests) are not CasaEngine asset types in the first place.

It also checks catalog integrity independently of any loader: duplicate asset ids, duplicate file
names, and catalog entries pointing at files that don't exist — each is an `Error`, not a warning.

Because the pass is **catalog-driven**, it is measured against the loadable files actually on disk
so that it cannot be silently blind:

- An **empty catalog over a directory that contains asset files is an `Error`** ("nothing was
  verified"), not a serene pass. An empty catalog with no asset files — what `--phase 0` legitimately
  produces — is fine.
- A loadable file on disk with **no catalog entry** is a `Warning` (`Verify.UncataloguedFiles`).
  In practice that means stale output from a previous run into the same directory: the engine's
  Tiled importer renames rather than overwrites, leaving `map_N_tileset_2.png` behind. **Convert
  into an empty directory** if you want the catalog to describe exactly what is there. On a clean
  full run this count is 0 and `Verify.LoadableFilesOnDisk == Verify.Loaded`.

What the pass does **not** cover: the companion JSON files (the ~980 per-map `*.strings.json` and
`*.events.json` under `Maps/`, plus the audio manifests, `wind-sprites.json`, `font3-charset.json`, `balance.json`,
`world-index.json`). They are not CasaEngine asset types and are not catalogued, so nothing loads
them back — their formats are documented in [`docs/formats/`](docs/formats/README.md) and covered by
unit tests instead.

A **PASSED** verification reports how many assets were loaded vs. existence-checked; a **FAILED**
one reports how many failed, with the reasons in `report.json`'s `Errors`.

## Reference numbers (one full run)

From one full, unfiltered run (`--phase` omitted, verification on), on one development machine —
indicative, not a contract. Duration in particular varies with the machine; the counts should not.

- **483** maps converted
- **~21 000** catalog entries (`Verify.Assets`: 20 991)
- **~910 MB** total output (`OutputSizeMegabytes`: 909.665), **~22 000** files (`OutputFileCount`: 21 968)
- **~40 seconds** total duration (`TotalDurationSeconds`: 38.467)
- 9 620 `.anim2d` assets, 6 908 sprite-bank `.sprite` assets (deduplicated from 160 355 quads) plus
  277 UI ones — hence `Verify.Loaded.sprite`: 7 185 — and 395 sprite banks
- 1 041 WAVs copied (45 BGM + 996 SFX tones), 91 SFX records the extractor could not decode
- 9 741 map entities (9 631 enabled), 3 316 portals, 1 714 map events — counted from source, not
  duplicated into the worlds (see `docs/formats` and the `WorldWriter` doc comments for why)
- Verification: 18 860 assets loaded through their engine class, 2 131 existence-checked, 0 failed

## Known gaps

Taken from the writers' own doc comments — not new findings:

- **Text renders monospaced.** `UI/font3.fnt` uses a fixed 16px `xadvance` for every glyph.
  Alundra draws text proportionally via `g_fontCharWidthTable`, a table that lives in the game
  executable and is not part of `data-extracted`. Converted dialogue will be far wider and more
  loosely spaced than the original until that table is extracted.
- **Per-frame 3D sprite collision is dropped.** `SiFrame.CollisionData` doesn't fit `SpriteData`'s
  or `Animation2dData`'s schema and hitbox/physics handling is out of this converter's scope
  (gameplay-DLL territory). The raw data remains available in `data-extracted`.
- **Hero `SpriteEffectRecords` are preserved but not converted** to sprites/animations: their
  `Spritesheet` indices exceed the normal 0–7 range, suggesting a graphics source the atlas-packing
  fix doesn't cover.
- **Event bytecode is uninterpreted.** The six per-map event program tables (A–F) and their code
  blob are copied verbatim as JSON companions; the opcodes are not decoded.
- **Map entities, portals and map events are not duplicated into `.world` files.** They already
  live in the `.tileMap` object layers Phase 1 wrote (`Portals` / `MapEvents` / `Entities` layers);
  a future gameplay DLL instantiates them from there. `CasaEngine.Entity` also has no
  custom-property bag to hold their native fields.
- **`player_startup_settings_asset_id` and `gameplay_mode_asset_id` are left as `Guid.Empty`** in
  every world — they need a hero `.entity` and a gameplay DLL, both out of this converter's scope.
- **483 `PlayerStartComponent`s are mostly placeholders.** Only map 389 (the documented New Game
  spawn) gets a real spawn point; every other map's is a placeholder at the map's centre, since
  every other map is entered through a portal that carries its own destination tile.
- **Two `maps.json` ids are contested** (387, 388 — listed under two zones each); the converter
  keeps the first zone listed and warns.
