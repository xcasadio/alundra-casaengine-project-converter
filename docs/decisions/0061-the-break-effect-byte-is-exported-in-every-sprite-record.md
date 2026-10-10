# ADR-0061: The break-effect byte is exported in every sprite record

- **Status**: Accepted
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e14-ia-native.md`, section 1.3a (E14.a1a, revision 1, READY), D-E14-2 and D-E20-26; evidence in `docs/plan-e14-annexe/e14a/a1a-export-prediction.md`; ADR-0060 (the consumer)

## Context

- `DestroyEntity` (`0x8003A59C`) creates a break effect when its caller passes `fx == -1`: the effect id is the byte `+0x1E` of the entity's sprite header (`0x8003A5D8`-`0x8003A5E8`), 0 meaning no effect. ADR-0060 makes the DLL do this for every caller of `DestroyEntity(e, fx)`.
- The extraction already carries that byte as `SpriteInfo.SpriteRecords[i].Header.BreakEffect`, in all 2 658 records of the 484 map files, always equal to `Ubuff[14]`. The 395 banks have no conflict between maps on any header field, so the converter's "first record of the bank is canonical" rule is safe for this byte.
- The converter did not read it, so `Data/sprite-records.json` (one raw entry per prefab, read by the DLL's `SpriteRecordCatalog`) could not carry it. The DLL ignores an unknown key, so the export and the DLL can land in either order.

## Decision

- **Reader.** `SpriteBankReader` reads `BreakEffect` (default 0 when absent) into `SpriteRecordHeader`, between `SizeZ` and `Contents`.
- **Writer.** `SpriteWriter` writes `BreakEffect` in every entry of `Data/sprite-records.json`, between `SizeZ` and `Contents`, 0 included (never omitted), so the diff is a pure insertion of one line per entry. No new `report.json` counter.
- **Format.** `docs/formats/sprite-records.md` documents the field. The file stays outside the asset catalog.

## Consequences

- The export gains 395 lines (+9 132 bytes). Measured on 2026-10-10 by manifest, relative to a reference export at the previous HEAD: exactly `Data/sprite-records.json` and `report.json` change, 0 files added or removed, `AssetInfos.json` unchanged, the exported file byte-equal to the prediction written before the code (SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`), and a second export identical to the first except `report.json`.
- 111 of the 395 entries are non-zero, with the histogram {0: 284, 2: 1, 8: 63, 10: 30, 11: 15, 12: 1, 13: 1}; the Muruta (type 347) and the Beannoide (type 335) are 8.
- The DLL does not read the key until E14.a1b; nothing visible changes before then (Alundra.Tests 4 646 passed, Release and Debug).
- Within the same sprite-record bank the value is the canonical map's; a bank whose break effect varied between maps would need its own field, and the data has none.
