# G1 (effects export): converter surface, discovery of 2026-10-06 (read only)

Scope read: parent repo branch `chantier/e19-suite` (HEAD `eeed643`), `data-extracted/` (the G0 re-extraction, 4537 files), the converter
(`alundra-casaengine-project-converter/`), `alundra-project/` (export, read only), the G0 annex `docs/plan-e19-g0-annexe/`. Nothing was built,
run, exported, edited, staged or committed inside the repository. Scripts and outputs: this folder (`census.py`, `census2.py`, `cells.py`,
`predict.py`) and `../dll/` (oracles). Labels: **[F]** fact read (address / file:line / script), **[H]** hypothesis, **[B]** proved on the binary by
running the real routines in the MIPS interpreter (`../dll/oracle*.py`).

## 1. What G0 already extracted, what the converter exports today

**[F] Data (data-extracted, JSON `map_N.json`, `SpriteInfo.*`)**, recounted today by `census.py` / `predict.py`:
- `MapEffectRecords` (12 bytes): X1,X2,Y1,Y2 (zone, inclusive tile box), Flags (0x80 map table, 0x40 spawn at load), EffectId (table index), X,Y,Z
  (half tiles, Z in 8 px), AnimId, U1,U2 (never read by the binary). **544 records on 157 maps; 251 with 0x40; 350 name the map table, 194 the global
  table; 0 unresolved** (every record names an existing table entry and animation).
- `SpriteEffectRecords` = the effect table: 136 effects on 86 maps (a subset of the 157) + 29 global (`map_alundra.json`) = **165 tables**.
  71 maps have records but no table (they only name the global bank).
- `AnimationOffsets[]` (u16 byte offsets from the record start) with `AnimationCount` = first offset / 2: **446 slots, 363 real, 83 zero-offset
  padding slots**. All 83 are TRAILING (0 holes: no zero offset before a non-zero one), so the animation index of a script/record is the slot
  position and dropping the padding changes no index. The padding slots hold 1-frame "animations" whose terminator byte (4, 8, 12) is the low byte of
  the first offset: the binary would halt on them; no record or checked script names one.
- Frames: 5148 displayed frames (+363 real terminators: 233 loop (`Delay` raw 1), 130 destroy (raw 0)); displayed `Delay` raw = `0x80 | ticks`;
  ticks 1..127, **no frame has tick 0** (the binary's "0 shows 256 ticks" rule is never exercised by the corpus); longest animation/period 230 ticks;
  most frequent delays 2 (2530 frames) and 1 (1467).
- `ImageSetPointer` = BYTE offset of the image set from the record start (not words: the binary does `record + 2*u16 + 2` where the u16 is the word
  index, see `oracle_anims2.py`). 2832 distinct image sets (2316 shared references), 12 307 distinct images + 23 degenerate (all-zero, window 0x0,
  map 161 anim 3 effect 0: 46 references; never named by a record; the binary draws a degenerate polygon = nothing).
- Quad references: **20 315** (20 269 non-degenerate, 46 degenerate). Shapes: 7499 plain (source-size axis-aligned rectangle), 8680 axis-aligned scaled,
  4136 non-rectangular. Mirrors: X 3127, Y 282, both 353; **IsMirroredX == (X1 > X2) == (SourceX == Sx+1) on all 20 315, IsMirroredY == (Y1 > Y3)**
  (so the mirror flag is derivable from the corner order). Semi-transparent (Spritesheet bit 3): ABR1 14 945, ABR2 452, ABR0 371 (+ 4547 opaque), no ABR3.
- G0 effect sheets present: `data/map_<n>_effectsheet.png` x 86 (512 wide, 25..1105 high, 662 639 bytes in all) + `map_alundra_effectsheet.png`
  (512x237) = **87 files**. `AtlasX/AtlasY` of the effect quads point into them: checked today against the versioned annex
  `docs/plan-e19-g0-annexe/expected_effect_cells.tsv` (1435 cells, key = file, page, palette, SourceX, SourceY, Swidth, Sheight): **20 269 / 20 269
  non-degenerate references land on the annex cell, 0 mismatch** (the 13 images shared with entities of `map_alundra` included). The cell is the
  `SourceX/SourceY` window (the mirrored-quad shift is baked in); alpha code in the PNG: 255 opaque, 128 STP, 0 transparent.
- 748 of the 12 307 distinct images have an all-transparent cell in the effect sheet (e.g. the opaque palette-1 quads of the 476 aura: palette 1 is
  mostly 0x0000 entries). They are legitimate by G0's independent decode (a quad whose texels are all index-0/0x0000 draws nothing) but a plan should
  not be surprised: 7 maps sharing the 476 aura table (25, 76, 84, 95, 411, 432, 476) carry 97 each.

**[F] Converter today**: nothing useful. `SpriteWriter.PreserveHeroEffects` (`SpriteWriter.cs:1022-1047`, called at `:171`) copies the 29 global tables
raw to `Sprites/hero/hero_effects.json`; counter `Sprites.HeroEffectsPreserved` (29 on the real run, 2 in the test fixture
`SpriteWriterTests.cs:148`); consumed by nothing (rg `hero_effects`: writer, test, `docs/formats/misc-data.md:97-125`). That doc says the
`Spritesheet` indices "exceed 0-7": **refuted** (B notes of 2026-10-03: byte 0x18 etc. = bit 3 semi + ABR bits; page = low 3 bits 0..6).
Per-map: nothing (the 476 export folder has `backdrop/ dialogues/ events/ tilemap/` + `.world`).
The entity path cannot carry effect quads (a part is "sprite at the centre + flips", no scale/corners/blend: `SpriteWriter.cs:618-657`).

## 2. Proposed export (G1)

Closest models: `BackdropWriter` (per-map raw companion + textures via `TextureAssetWriter`, full-run invariants, loaded by the DLL through a loader
that resolves `Maps/world-index.json`), `ConvertInventoryPortrait`/`ConvertDialoguePortraits` (E19.f4a, ADR-0039: texture + `.sprite` + a `Data/*.json`
index read by the DLL).

**Recommendation: NO `.sprite` assets for effect quads, a raw companion instead.** Reasons: (1) the PSX mode (ABR) belongs to the quad, not to the cell
(G0-R2: the cell key drops the ABR bits, one cell serves quads of different modes), whereas a `.sprite` carries one `PsxSemiTransparency` (G2a-R4);
(2) quads have four free corners, a `.sprite` has a rectangle and an origin; (3) the DLL pushes quads, the engine service needs only
(texture id, source rectangle, corners, mode). So:

1. Textures: the 87 PNGs through `TextureAssetWriter.EnsureTexture` (raw PNG + `.texture` wrapper + 2 catalog entries each = 174 entries; sampler PointClamp
   as for every converter texture). Folder: `Sprites/Textures/` (precedent: spritesheets) or a new `Effects/Textures/` (cleaner manifest diff); either is
   free, ids are `Ids.For("texture-raw:..."/"texture-wrapper:...")` from the path, hence deterministic. `AssetInfos.json` changes (+174 entries): it must
   be saved (`EditorAssetCatalogService.Save()`, the Phase-9 backdrop trap of `BackdropWriter.cs:62-68`).
2. Per-map companion for each of the 157 maps that have records or a table: `Maps/<Zone>/<Name>-<id>/effects/<Name>-<id>.effects.json` (raw, not an
   asset, like `events`/`backdrop`) + global `Data/effects-global.json` (the 29 global tables; the 194 records that name them are in the per-map files).
   Proposed shape (names indicative; the DLL loader and the converter share one `docs/formats/effects.md`):
   - `MapIndex`, `SheetTextureAssetId` (null when the map has no table), `GlobalSheetTextureAssetId` is NOT repeated (one id in the global file);
   - `Records[]` (index = position): `Zone [X1,Y1,X2,Y2]`, `Flags`, `UsesMapTable` (0x80), `SpawnAtLoad` (0x40), `Effect`, `X`,`Y`,`Z` (half-tile units, as
     the binary reads them; the DLL computes `(X*12+12)<<16`, `(Y*8+8)<<16`, `Z<<19`), `Anim`;
   - `Effects[]` (index = table index; `null` for a -1 hole if any: none in the corpus, [H] the loader treats -1 as null like the binary): `Animations[]`
     (index = animation slot, trailing padding dropped, count reported) each `{ Frames:[[ticks, setIndex]...], End:"Destroy"|"Loop" }`, `ImageSets[]` (deduped by
     `ImageSetPointer`, order of first use) each `{ Idsv, Images:[...] }`;
   - image: `{ U,V,W,H (= AtlasX, AtlasY, Swidth, Sheight), C:[x1,y1,x2,y2,x3,y3,x4,y4] (signed bytes, PSX order TL,TR,BL,BR, Y down), Semi, Abr }`;
     `MirrorX/MirrorY` are redundant (corner order) but cheap to keep if the G2b sampling rule needs them.
   Delay stays the raw 7 bits (1..127); the "0 means 256" rule lives in the DLL (and in a synthetic test).
   Dropped and counted: 83 padding slots, 23 degenerate images (46 references).
3. Counters (`report.json`) and full-run invariants, in the style of `BackdropWriter.CheckInvariants` (only when the run is the full 483-map corpus):
   `Effects.Records` 544, `Effects.RecordsSpawnAtLoad` 251, `Effects.RecordsMapTable` 350, `Effects.RecordsGlobalTable` 194, `Effects.Tables` 165
   (136 map + 29 global), `Effects.Animations` 363, `Effects.AnimationSlotsDropped` 83, `Effects.ImageSets` 2832, `Effects.Images` 12 307,
   `Effects.ImagesDegenerateDropped` 23, `Effects.Companions` 157 (+1 global), `Effects.Sheets` 87, `Effects.UnresolvedRecords` 0.
   Full-run guards (the plan's "544 / 251 / 136 + 29 / 363 / 83" invariants) are exactly these.
4. Reading side in the converter: a new `EffectBankReader` (JSON `SpriteInfo.MapEffectRecords` / `SpriteEffectRecords`; do not reuse `SpriteBankReader`,
   whose bank dedupe and `SpriteQuad` fields are entity-oriented) and `EffectWriter`, a new phase after `Phase9.Backdrops` (or inside Phase 3: it needs
   no prefab id). `AssetVerifier` loads every `.texture` it finds (`AssetVerifier.cs:39` loads `.anim2d`; textures count in `Verify.Loaded`); raw companions are
   not loaded.
5. `hero_effects.json`: keep until G3 lands, then retire it with `Sprites.HeroEffectsPreserved`, its test (`SpriteWriterTests.cs:146-148`) and the
   `docs/formats/misc-data.md` section in one commit (also correct the refuted "indices exceed 0-7" sentence). Not to be retired in G1 (no consumer
   change yet = zero risk), but the global companion makes it redundant.

## 3. Predicting the export in advance (how, with the values)

Everything the export contains is a deterministic function of `data-extracted/` JSON + the sheets, so the export can be PREDICTED by an independent script
(no converter code), exactly like the G0 annex:
- **File-level prediction** (manifest diff, hash SHA-1 of 23 741 files hors DLL/pdb/`.casaeditor/` as in G0/G2a): new = 157 companions + 1 global companion
  + 87 PNG + 87 `.texture`; changed = `AssetInfos.json`, `report.json` (+ nothing else: `Alundra.dll` excluded by the manifest rule). Counters above.
- **Content-level prediction**: a script `predict.py`-like emits for each map the canonical content (records, image sets, frames, images with corner
  bytes) and its SHA-1; the proof = parse each exported companion and compare semantically (key order is the writer's, so compare parsed values, not
  bytes), plus a referential proof (every image window lies inside its sheet PNG, every `SheetTextureAssetId` resolves in `AssetInfos.json` to a `.texture`
  whose raw PNG's SHA-256 RGBA equals the annex `expected_effect_sheets.tsv` line for that sheet).
- The numbers in section 2.3 were produced by `predict.py` (`census.py` for the 544/251/350/194, 165 tables, 83/363).
- Per-file counts for the first tests: map 476 = 1 record (flags 0x80, effect 0, anim 1, tile (80,22,6)), 1 table, 4 animations (slots 0..3: 111, 16, 21, 32
  frames; ends Destroy, Loop, Destroy, Loop; periods 222, 32, 34, 64 ticks), IDSV 52, sheet 512x435; map 391 = 5 records (all 0xC0, effect 0, anims 0,0..3,3) 1 table,
  4 single-frame loop animations (delay 10, `[10, set]`); map 163 = 4 records, 1 animation `[10]` then Destroy; map 161 = the 23 degenerate images.
- Tests that must NOT move in G1: every existing converter test (the fixtures never carry effect data except `SpriteWriterTests` hero effects, kept) and
  `Alundra.Tests` (the new files are additive). A moving assertion is a stop (the precedent rule of G2a/G2c).

## 4. Risks / traps (converter)
- The 46 zero-size references (map 161) must be dropped without breaking the "reference count" invariant: count them (`ImagesDegenerateDropped`).
- Dedupe of image sets by `ImageSetPointer` per table, not globally (pointers are table-relative).
- Animation index = slot index: do NOT compact when dropping padding (they are trailing here, so truncating is exact; assert 0 holes at write time).
- Determinism: `ImageSetId`/`MemoryAddress` fields of the source JSON look pointer-derived (e.g. 140737488355880): never export them.
- Key order and number formatting of the writer must be stable for the double export (the converter already does this for backdrops).
- Do not stamp anything new in the extractor: G0 already did (no re-extraction in G1).
