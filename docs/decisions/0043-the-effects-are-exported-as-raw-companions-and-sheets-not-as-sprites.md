# ADR-0043: The effects are exported as raw companions and sheets, not as sprites

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G1, D-E19-96, D-E19-97); annex `docs/plan-e19-g1g3-annexe/` (`converter-notes.md`, `g1-export-prediction.md`)

## Context

The binary (ALUN_CD.EXE, France) draws the effects of maps and objects (the doors of Inoa, the aura of map 476, rays, sparks) as free quads: each image of an effect
is a window of an effect sheet drawn on four free corners with its own PS1 blend mode. The extraction (E19.g G0) already produced the 87 effect sheets and the JSON of the
544 map records and 165 effect tables, but the converter exported nothing of it. The entity path cannot carry an effect quad: a `.sprite` carries one PSX
semi-transparency for the whole cell (G2a), a rectangle and an origin, whereas the mode belongs to the quad (the cell key drops the ABR bits, one cell serves quads of
different modes) and the corners are free (4136 of the 20 315 references are not axis-aligned rectangles, 3762 are mirrored).

## Decision

- The effects are exported as raw companions (not CasaEngine assets, like `events.json` and the backdrop companion): one `effects.json` per map that has records or a table, in
  the map's `effects/` folder, and `Data/effects/effects-global.json` for the 29 global tables, in a new phase `Phase9.Effects` after `Phase9.Backdrops`. Format:
  `docs/formats/effects.md`.
- The sheets go through `TextureAssetWriter.EnsureTexture` (a raw PNG and a `.texture` wrapper, two catalog entries each), next to their companion; the phase saves the catalog.
- Animation cases are the first `AnimationCount` entries of the offsets (trailing padding dropped and counted, a hole is an error), delays are exported as the binary's 7-bit
  tick count, image sets are de-duplicated per table, degenerate images are dropped and counted, mirrors are not exported (the corner order carries them), no pointer-derived
  field is exported.
- The full-corpus counters are invariants of the phase (544 records, 165 tables, 363 animations, 12 307 images, 157 companions, 87 sheets).
- `Sprites/hero/hero_effects.json` stays until the DLL reads the global companion (E19.g G3).

## Consequences

- Nothing loads the companions yet: no effect is visible before E19.g G3 (the stage and the quads of the free quad service).
- The engine needs a free-quad path with a per-quad blend mode for these effects (E19.g G2); the export keeps what that path needs (window, four corners, `Semi`, `Abr`).
- A later promotion of the companions to engine assets stays possible: the format carries every value the binary reads.
