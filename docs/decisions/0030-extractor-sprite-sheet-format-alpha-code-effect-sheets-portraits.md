# ADR-0030: The extractor writes a per-texel alpha code, effect sheets, dialogue portraits and the odd last column

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.g G0" (decisions D-E19-49, D-E19-51, D-E19-52, same file `:186-193`); value annex `docs/plan-e19-g0-annexe/`

## Context

- The analyser extractor (`alundra-datas-analyser`, `AlundraDataExtractor`) wrote sprite sheets through `Graphics.DrawImage` with colours from `ImageHelper.FromPsxColor`, which keeps only RGB and an alpha of 0 or 255: bit 15 of the CLUT word (STP, PSX semi-transparency) was lost. Raw CLUT words plus the RGBA atlas cannot rebuild the per-texel mask (301 quads are ambiguous).
- Effect quads (`SpriteInfo.SpriteEffectRecords`) belong to no entity animation: no pixel of them was ever exported and their `AtlasX/AtlasY` were 0 (20 315 references).
- The dialogue portrait of a sprite record (`SpriteRecord.GetPortraitImageset`) was in no sheet and no JSON.
- A quad with an odd `SourceX` and an odd `Swidth` lost its last texel column (`GameMap` crop, 2646 texels in 265 sheets).
- D-E19-52 (author): per-texel semi-transparency also applies to entity sprites. D-E19-49: the 48x72 portraits are shown whole. D-E19-51: the re-extraction is done by the session, with a `diff -rq` proof before the mirror.

## Decision

- **Alpha code (G0-R1)**: every sheet written by the extractor carries, per texel, alpha 255 (opaque), 128 (bit 15 of the CLUT word set, `0x8000` included) or 0 (word `0x0000`). RGB is unchanged. Sheets are built from raw CLUT words (`SpriteInfo.PaletteWords`, `[JsonIgnore]`, and `GameMap.GetSpriteWords`), the last non-zero texel drawn winning, alpha included. `FromPsxColor` and the JSON palettes are untouched.
- **Effect sheets (G0-R2)**: one `data/map_<n>_effectsheet.png` per map that has effect cells, plus `data/map_alundra_effectsheet.png`; 512 wide, compact layout (tallest first, then `Signature`, 1 px padding), one cell per (VRAM page, palette, source region) (1435 cells, not per `Signature`: the semi/ABR bits belong to the quad). `AtlasX/AtlasY` are written on effect quad instances only. The 46 quads of size 0x0 of map 161 get no cell and stay at (0,0).
- **Dialogue portraits (G0-R3)**: `SpriteRecord.DialoguePortrait` (same shape as `GameMap.InventoryPortrait`, omitted when null) for each record with the portrait bit (`FlagsPortraitShadowType & 0x80`); its image gets a cell in the entity sheet (Original layout) after all animation images and the inventory portrait. 331 fields in 184 map JSON.
- **Odd column (G0-R4)**: the last column of an odd-`SourceX`, odd-`Swidth` quad is kept (a known defect of the port, corrected).
- The consumers (converter, engine) ignore the alpha code and the new fields until E19.g G1/G2.

## Consequences

- A re-extraction changes exactly 700 files of `data-extracted/` (613 modified, 87 added), listed in `docs/plan-e19-g0-annexe/predicted_changes.txt`; the next export changes 103 sprite textures, `Sprites/hero/hero_effects.json` and `report.json`.
- Entities are still drawn opaque by the engine, so the only visible change in game is the restored odd column; semi-transparent rendering is left to G2.
- A change to `FromPsxColor`, to `BitmapFromPsxBuff` or a new serialized public field on the map classes would alter every map JSON, tilesheet, `ui/` and `tiled/`: forbidden here.
- Extraction must run from the submodule build (Release, fresh), with a forward-slash game path and `--no-launch-profile`, into a new folder.
