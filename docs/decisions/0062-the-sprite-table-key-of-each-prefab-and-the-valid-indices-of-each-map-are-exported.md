# ADR-0062: The sprite-table key of each prefab and the valid indices of each map are exported

- **Status**: Accepted
- **Date**: 2026-10-15
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.15 (E20.e0x, revision 1, READY), D-E20-33; evidence in `docs/plan-e20-annexe/e20-e0x/README.md`; ADR-0061 (same file, same proof regime)

## Context

- `SpawnWarpEntity` (`0x80039F58`) calls `0x80039B28(a1, a2, &pal, &sheet)`: `a1 == 0` reads the global sprite table, otherwise the table of the current map; it needs `0 <= a2 < [block+0x10]` and an entry other than -1, else nothing appears (return 0 before any case). 102 call sites (21 on the global page, 81 on the map page). The same number exists on both pages (146 numbers are valid on both), so the page is part of the key.
- The converter already forms the key of each bank (`SpriteBank.BankKey`: `alundra_{id}` for the global table, `{id}` for a map's), derives the prefab id from it (`entity:{BankKey}/{folder}`), and keeps the key only in memory. `Data/sprite-records.json` was keyed by prefab id alone, so the DLL could not turn a sprite-table index into a prefab.
- The validity of an index is per map: an entry of a map's table is -1 exactly when its `SpriteRecords[i]` is null (checked on all 483 maps). The header and animations of an index of the map page are identical on every map where it exists, so one index-to-prefab table per page is enough. Example of a silent failure to reproduce: Zorgia, index 213, on maps 388, 432 and 451 (the index is valid on none).

## Decision

- **`BankKey`** is the first member of every entry of `Data/sprite-records.json`, always written: `"alundra_N"` (151) or `"N"` (244, no leading zeros), exactly `SpriteBank.BankKey`. An entry without a key is a hard error of the report. `SpriteRecordHeader` does not change.
- **`Data/map-sprite-indices.json`** is a new companion, not catalogued: a root object with one key per native map (the number alone as a string, `"0"` to `"482"`, numeric order), whose value is the ascending array of the indices 0..255 of the map's `SpriteRecords` that are not null (`[]` when none). The text is `JsonSerializer.Serialize` with the options of `sprite-records.json` (UTF-8 without BOM, CRLF, 2 spaces, one number per line, no final line break). The converter also checks the rule against `SpriteTable[i] != -1`; a disagreement is a hard error of the report.
- No new `report.json` counter. The DLL ignores both until E20.e0a.

## Consequences

- Measured on 2026-10-15 by manifest, relative to a reference export at the previous HEAD: exactly `Data/sprite-records.json` and `report.json` change and `Data/map-sprite-indices.json` is added; nothing removed; `AssetInfos.json` unchanged; both files byte-equal to the predictions written before the code (`sprite-records.json` 2 571 585 bytes, +10 080, SHA-1 `88d723066fd2e6232a6f815dad8de9b0c05c64e4`; `map-sprite-indices.json` 30 913 bytes, SHA-1 `63a3e6189d1f1375232bb2c54e1d1dcb385e360d`: 483 keys, 2 507 indices, 65 empty arrays); a second export identical to the first except `report.json`.
- E20.e0a can translate the index of a `SpawnWarpEntity` call into a prefab (page + index -> `BankKey` -> prefab) and refuse an index absent from the current map, as the binary does.
- The key is the canonical bank's; the prefab of an index does not depend on the map (identical header and animations), but its validity does, hence the second file.
- Page palette and sheet (`0xB`/`0x60` global, `0`/`0x20` map) are not exported here; E20.e0a decides.
