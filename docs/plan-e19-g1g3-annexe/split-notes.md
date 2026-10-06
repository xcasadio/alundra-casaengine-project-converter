# G1 / G3: proposed split into executable slices (discovery of 2026-10-06, read only)

Inputs: `../converter/notes.md`, `../dll/notes.md`, `../engine/notes.md` and the proofs of `../dll/oracle*.py`. Parent branch `chantier/e19-suite`, engine pinned at `33324030`. Nothing was run inside the repository.
Plan vocabulary kept: G0 done (extractor, effect sheets), G2a/G2c/G2d done (per-texel STP, backdrops, overlay), G2b pending (free quads; O-E19-71), G1/G3/G4 to plan.

## 0. The five facts that shape the split

1. **The data side is finished and exact.** G0 already stamped every effect quad with its cell (20 269 / 20 269 references land on the annex cell) and alpha-coded the sheets; the converter needs no extractor change and no re-extraction.
2. **The rules are proven on the real binary**, not only read: 363 animations x every tick (20 227 ticks), 157 map entries, 16 610 emitted quads, the real opcode handlers (section 4 of `../dll/notes.md`). The DLL tests can therefore be written from binary-derived values BEFORE any DLL code.
3. **Effects have no gameplay effect in the binary** (no collision, no sound, nothing read back by scripts, no `Result`): G3a/G3b change no trace and no arc outcome; the only visible change is at the draw (G3c).
4. **The draw needs the engine's free four-corner quad (G2b-1)**, and 63 % of effect references are deformed: no workaround is allowed (rule: engine gaps are reported, never worked around). G1, G3a and G3b do not need it.
5. **The "native" producers of D-E19-54 are a different job**: the 80 native creation sites live in player/item/AI code the DLL does not have; the "warp" effects are item-use visuals keyed by `g_warpLockTimer = itemId`, not a map-transition effect (see `../dll/notes.md` section 7).

## 1. Slices, dependencies, acceptance

```
G1 (converter) ──► G3a (DLL reservoir) ──► G3b (opcodes + arcs) ─────────────────┐
        │                  │                                                      ▼
        │                  └──────────────────────────────┐                  G3d* (natives)
G2b-1 (engine free quad; needs O-E19-71) ─► G2e (effect service+component) ─► G3c (DLL push) ─► G4 (recipe)
```
G1 and G2b-1 are independent and can run in parallel; G3a needs G1's format (fixtures can start the day the format is fixed); G3c needs G3a + G2e. (*) G3d is optional/later.

### G1 — Converter: effect export (parent only; no engine, no DLL code)
- **Content**: `EffectBankReader` + `EffectWriter` (new phase after `Phase9.Backdrops`), per-map companions (157) + `Data/effects-global.json` + 87 sheet textures through `TextureAssetWriter`, counters and full-run invariants; `docs/formats/effects.md` (+ index line, correct the refuted claim in `docs/formats/misc-data.md`); parent ADR (next free 0040; 0039 is f4a's). `hero_effects.json` kept.
- **Rules** (R1..R6): animation index = slot index (padding slots trailing: assert and drop, count); image sets deduped per table by `ImageSetPointer`; degenerate images (window 0x0, all corners 0) dropped and counted; delays exported raw (1..127); mode = bit 3 / bits 4-5 of `Spritesheet`; corners as read (signed bytes TL,TR,BL,BR); never export pointer-derived ids; unresolved record = error.
- **Values writable in advance** (all computed today by independent scripts from `data-extracted`, `../converter/predict.py`, `census.py`, `cells.py`): 157 companions + 1 global; 87 textures (87 PNG + 87 `.texture` + 174 catalog entries); records 544 (251 load, 350 map, 194 global); tables 165 (136 + 29); animations 363, padding dropped 83; frames 5148; image sets 2832; images 12 307; degenerate images dropped 23 (46 refs); unresolved 0; 20 269 refs on annex cells; per-map examples (476, 391, 163, 161).
  How: a `plan-e19-g1-annexe/` like the G0/G2b annexes (counters, per-map canonical content SHA-1, expected file list), written BEFORE any code; the proof = manifest SHA-1 before/after (23 741 files hors DLL/pdb/`.casaeditor/`: new = 158 json + 87 png + 87 texture; changed = `AssetInfos.json`, `report.json`), parsed-value comparison of every companion with the prediction, referential proof (window inside sheet, texture id -> `.texture` -> PNG SHA-256 equal to `expected_effect_sheets.tsv`), double export identical outside `report.json`.
- **Tests that move**: none expected (existing fixtures carry no effects except the hero-effects one, kept); `Alundra.Tests` additive only. New: reader/writer unit tests (synthetic table with padding, shared sets, degenerate image, mirrored quad) + real-data tests on 476/391/163/161 + the full-run guard.
- **Acceptance**: counters equal; manifest equal to the prediction; double export; converter tests green; `Alundra.Tests` Release then Debug on the new export unchanged; six traces byte-identical.
- **Needs no author input.**

### G3a — DLL: bank, reservoir, map-entry spawn, per-tick update (headless; no opcode, no rendering)
- **Content**: `EffectLoader`/`AlundraEffectBank`, `AlundraEffectReservoir`, the stage field on `AlundraWorldProxy` (field initializer, `Clone` safe), load + spawn at `InitializeWithWorld`, a per-tick loop after the map-events loop gated by `PlayerControlFlags & 0x48` and `IsTransitionInProgress`; creators API (mode 0 free; mode 2 as end state; modes 1/3 deferred to G3d); `IEntityWorldContext.EffectReservoir` seam (null default) so tests and headless arcs run.
- **Rules**: `../dll/notes.md` section 2 (128 slots, lowest free, silent full, status 0/2, destroy flag freed one tick later and NOT drawn that tick, frozen-but-drawn under 0x48, load clears all, load spawn = flag 0x40 and inclusive zone with the hero tile, position formulas, animation timeline, depth key).
- **Values in advance**: `anims_oracle.json` (363 rows: end kind, life, destroy tick = life+1, free tick = life+2, drawn ticks = life+1), `load_oracle.json` (per-map slot lists, 245 slots at hero (0,0), 6 zone cases), golden: 476 aura anim 0 destroyed at tick 223 / freed 224, anim 1 loop 32 ticks, 163 shafts drawn ticks 1..11 and freed at 12, 391 load slots at (996,504)... Generate a versioned TSV annex from the oracle (like the G0 annex); the unit tests assert the reservoir against it on the REAL export; a few synthetic tests for what the corpus never exercises (delay 0 = 256 ticks, unknown terminator, full pool, record >= count, anim past table).
- **Tests that move**: none (state is invisible); new tests as above; arcs untouched in G3a (they will simply carry slots).
- **Acceptance**: reservoir tests vs the oracle annex; `Alundra.Tests` Release then Debug; six traces byte-identical; hero/intro traces unchanged (proof that nothing observable moved).
- **Coordination**: touches `AlundraWorldProxy.cs` (field, init, Update loop): rebase/merge risk with f4b (dialogue/portrait flight) running in the same checkout; sequence it after f4b's DLL commits or rebase explicitly.

### G3b — DLL: the nine opcodes, the closed list, the arcs
- **Content**: handlers `0x90`-`0x94`, `0xA0`-`0xA3` in `AlundraEventProgramRunner.Dispatch` (degrade-skip with size when the seam is null); `0xA1/0xA3` through `EntitySearchService`; defensive rules (record >= count no-op like the binary, anim past table ignore + warn once).
- **Tests that move (same commit as the handlers, plan convention)**: `story-chain-skipped-opcodes.tsv` loses 99 EFFET rows (102 -> 3; map 10 keeps 3 `PREDICAT coup recu`), `AlundraStoryChainOpcodeAudit.MapsWithoutSkippedSite` 19 -> 28 (+135, 162, 169, 174, 176, 181, 183, 391, 476) and `AlundraStoryChainSkippedOpcodesTests.cs:83`; arcs A2/A4 (`AlundraVisionArcTests.cs:32`), A6 (`AlundraShipBlockArcTests.cs:33-37`), A20 (`AlundraInoaDayOneArcTests.cs:39,71-77`), A10J/T-A10v/T-B9 (`AlundraEntityContactArcTests.cs:171-174,290-292`), the hero-jump arc (`AlundraHeroJumpArcTests.cs:57-64`), A13/A15 (176) and A17 (135) via rule 5 of `AlundraDay3SceneArcTests.cs`; `AlundraEventProgramRunnerTests.UnknownOpcode_KnownSize_SkipsBySize` (uses 0x93; repoint). A10/A11 (Inoa 165/164) do NOT move.
- **Values in advance**: golden opcode sequences of `../dll/oracle_ops.py` (476 `0xA2/0x93/0x92`, 391 four `0xA2` + five `0x94`, 135 `0xA0`, 162 eleven `0xA2` -> 18 slots / record 5 = 12, 129th creation, `0x91` same instruction).
- **Acceptance**: skipped lists empty on the nine maps; pool assertions in the arcs; all existing assertions otherwise untouched (any other moved assertion is a stop); traces byte-identical.

### G2b-1 — Engine: the free-quad primitive (shared with entities)
- Owned by the G2b plan (it is G2b's engine half): `DrawQuad(texture, source rectangle, four corners, colour, key, scissor, PSX mode)`, PS1 split, no-cull state, the sampling contract. **Blocked by O-E19-71** (option C per-PSX-pixel shader vs option A resolution-independent). Effects add three requirements to its design: no dependency on `Sprite`/`SpriteData`, per-call PSX mode, 63 %-deformed performance (a frame is <= ~30 quads).
- Split G2b so effects do not wait for the `.anim2d` half: G2b-1 = engine primitive + demo + ADR; G2b-2 = corner track in `.anim2d` + converter entity export (entities only).

### G2e — Engine: effect quad service + component
- `EffectQuadService` + `EffectQuadComponent` (pattern of the cellular layers), registration in `CasaEngineGame` and `ComponentOrder`, demo (synthetic cases + the 476 aura reference frame of `../engine/ref_frame.py`, zoom 1 and 4), tests (`../engine/notes.md` section 6), docs, ADR (next engine number 0061 or later). Depends on G2b-1.

### G3c — DLL: the per-frame push
- `AlundraEffectStage.PushFrame`: for each active slot in slot order, the current image set's quads -> anchor `(x>>16, (y-z)>>16)`, corners, mode, sort key (`ComputeEntityElevation(y, idsv)`, coordinate of the entity-equivalent projection, localOffset `N-1-k`, stable id slot index), anchor culling [-128,448] x [-128,368] with the camera, frozen-but-pushed under 0x48, 0 ticks but pushed during a departure; world-load `Clear()`.
- Acceptance: tests against a real `EffectQuadService`; pixel proof (back-buffer, +-1) of the 476 aura frame against `ref_frame.py` values (160,120)=(216,248,255), (150,100)=(144,168,200), (170,140)=(136,168,224), (200,150)=(48,48,96); delivery locked (engine + converter + DLL together, guard test on the real export as G2c-R4).

### G3d — natives (optional, to be re-discovered when the item/HP layer exists)
- Creators for attached/detached modes (1, 3) and the scripted `0x82 0x53/0x54` effects after O-E19-39; the item-use `AnimateWarpEffect` (5 sites) and HP/MP `*AndCreateEffect` (14 sites) need `UpdateItemEffectState`, the HP system and `AlundraRandom` ordering; combat natives stay with E14 (D-E19-54).

### G4 — Recipe (author)
- 476 (O-E19-30), 391, 162/169/176/183, 10, 174/181, 135, 163 (the shafts go out 11 ticks after arrival, D-E19-55), with back-buffer captures; reference frames from `ref_frame.py` as the expected images.

## 2. Export changes (summary)
Additive only: 158 raw JSON companions, 87 sheet textures (+ wrappers, catalog), counters in `report.json`; no existing file changes except `AssetInfos.json`/`report.json`; `hero_effects.json` retires only at the end of G3. No extractor change.

## 3. Engine needs (summary)
Free four-corner quad with PS1 split, no-cull, sampling contract (O-E19-71); per-quad PSX mode (exists); effect service + component (pattern exists); sort key passthrough in the Y-sorted layer (exists); nothing in the shader beyond G2b-1.

## 4. Questions (only genuinely open product decisions)
1. **O-E19-71 (already pending, G2b)**: PSX-exact at any zoom (per-PSX-pixel evaluation, honours D-E19-60) or resolution-independent quads. It decides the engine primitive and blocks only G2b-1, G2e, G3c, G4; G1, G3a, G3b can proceed. Recommendation of the G2b discovery stands (C if cheap, else A with the deviation recorded); for effects (63 % deformed) the choice is more visible than for entities.
2. **Scope check of D-E19-54** (new, found today): the "warp" natives are item-use visuals and the "pickup" ones are HP/MP/max effects, in systems the DLL does not have yet. Proposal: E19.g closes with G3c plus the creator API; the natives (G3d) follow the item/HP layer, the scripted `0x82 0x53/0x54` after O-E19-39. Not blocking.
No other question: the corpus never exercises delay 0, unknown terminators or anim indices past a table; defensive rules are technical.

## 5. Risks
- `AlundraWorldProxy.cs` merge conflicts with f4b (same file, same checkout); ADR number collisions (parent 0040 may be taken by f4b, engine 0061).
- 748 of 12 307 images have an all-transparent cell (legitimate: all-0x0000 texels); a recipe viewer must not read "empty cell" as a missing export.
- The ordering within a row between effects and entities inherits the documented deviation (coordinate from the projected position instead of `z>>16`); invisible unless an effect and an entity of the same row overlap (the 391 pieces and the hull are the plausible case; to look at in the recipe).
- Hero tile at map entry for the 6 off-chain zone-restricted records: the DLL must choose when to read it; test synthetically.
