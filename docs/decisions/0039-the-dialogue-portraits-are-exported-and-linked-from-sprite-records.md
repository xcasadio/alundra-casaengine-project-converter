# ADR-0039: The dialogue portraits are exported as sprites and linked from sprite-records.json

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, "E19.f4a — Export des portraits de dialogue" (review n°2 READY), with the evidence in `docs/plan-e19-f4-annexe/` (`dll-notes.md` section 2, `dll-verify.md`, `portraits_table.tsv`)

## Context

- Three event opcodes (`0x0D`, `0x5C`, `0xC4`) open the speaker's name box and, when the speaker's entity flags carry `0x800000` (bit `0x80` of the header byte `FlagsPortraitShadowType`), the speaker's portrait. The extractor already writes the portrait image of each such sprite record as `SpriteRecords[i].DialoguePortrait` and places it in the Compact sprite sheet (ADR-0030, ADR-0031). It is present exactly where the bit is set: 331 records in 184 maps, 25 banks, 324 images of 48x56 and 7 of 48x72 (banks 122 and 162), with the same pixels in every map of a bank.
- The converter did not read the field: `SpriteBankReader` and `SpriteWriter` only read the inventory's portrait (ADR-0005), so the DLL had no way to name a speaker's portrait.
- One portrait cell, that of bank 15 (Bonaire), is also a quad of its own animation and is already exported as an entity sprite; a second file with the same deterministic id would collide.
- `Data/sprite-records.json` already carries one raw entry per prefab, keyed by the prefab's asset id, which the DLL reads at spawn through `SpriteRecordCatalog`; no document described it before `docs/formats/sprite-records.md`.

## Decision

- **Reader.** `SpriteBankReader` reads `DialoguePortrait` of the canonical record of each bank (the first map that carries it) into `SpriteBank.DialoguePortrait`. It warns, once per bank, only when the header bit `0x80` is set and the field is missing; a bank with the bit clear and no field is silent.
- **Writer.** `SpriteWriter` runs one pass after all the banks and the inventory portrait, before the catalog is saved: one `.sprite` per portrait bank under `UI/Portraits/` (the folder of the inventory portrait), named `sprite_<signature>`, with the deterministic id `SpriteAssetId(sheet, signature)` and a catalog entry. A portrait whose id already exists (bank 15) reuses that sprite and writes no file; the folder is created only when there is a file to write. The true size is kept (48x72 for banks 122 and 162).
- **Link.** `Data/sprite-records.json` gains an optional `DialoguePortrait { SpriteAssetId, Width, Height }` on the speaker's prefab entry, omitted when null, so the other 370 entries keep their bytes. The DLL reads it into the nullable `SpriteRecordHeader.DialoguePortrait` (`DialoguePortraitRef(Guid SpriteAssetId, int Width, int Height)`), tolerating its absence.
- **Report.** The counter `Sprites.DialoguePortrait` counts the linked portraits (25).

## Consequences

- The export gains 24 `.sprite` files (25 portraits, bank 15 reused), 24 catalog entries and 25 `DialoguePortrait` fields. No texture, map, Yarn or entity prefab file changes: the portraits are cells of the sheets already exported. Measured by manifest on 2026-10-06: exactly the 27 paths predicted before any code, and a second export identical to the first except `report.json`.
- The DLL resolves a portrait by id, never by name or path, as for the inventory's. The first speaker of a sheet loads that sheet on demand (13 sheets carry the portraits); preloading is a later decision.
- The portrait pixels are those of the canonical map, which are identical in every map of a bank. A variant of a portrait with another palette would need its own record and field; none exists in the data.
- The speaker, the name box and the flight of the portrait are not covered here: they are E19.f4b and E19.f4c.
