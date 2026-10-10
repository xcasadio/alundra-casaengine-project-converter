# E20.e0x - export prediction (T0)

Written and committed BEFORE any converter code of E20.e0x. Contract: `docs/plan-e20-heros.md`, section 1.15 (revision 1) and D-E20-33.
Prediction computed by `predict_e0x.py` (read-only, independent of the converter: it reads `data-extracted/data/map_*.json`, the
entity folders of `alundra-project/Entities/` and the pre-change `Data/sprite-records.json`, whose SHA-1 it asserts), output in
`predict_e0x.out`. Run from the repo root: `python -B -I docs/plan-e20-annexe/e20-e0x/predict_e0x.py . <outdir> <pre-change sprite-records.json>`.
Manifest tools `manifest.py` and `diff_manifests.py` are copied from E14.a1a.

## The change

1. `Data/sprite-records.json`: every entry gets `"BankKey"` as FIRST member (`"alundra_N"` for the global table, `"N"` for a map bank).
2. New file `Data/map-sprite-indices.json` (not catalogued): per map `"N"`, the sorted indices 0..255 of the map's `SpriteRecords` whose
   record is not null; `[]` when none. Text fixed by the plan (CRLF, 2 spaces, one number per line, no BOM, no final line break).

## Starting state (T0 measures, 2026-10-10)

- Tree `alundra-project/` (without `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`): 24 103 files, manifest SHA-1
  `662056cebb0395f014099d1bd5ffc8f071a6fd58` (`m0`), as expected.
- `Data/sprite-records.json` before: 2 561 505 bytes, SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`.
- `report.json` on disk before T1: 9 284 bytes (export of 2026-10-10 11:39).
- Tests: converter 849 (`--list-tests`), `Alundra.Tests` 4 752 (`--list-tests`, Release).

## Predicted `Data/sprite-records.json`

- 395 entries; `"BankKey": "…",` inserted right after each `  "<guid>": {` line (395 lines, CRLF, 4-space indent): 2 571 585 bytes
  (+10 080), SHA-1 `88d723066fd2e6232a6f815dad8de9b0c05c64e4`. The predicted JSON parses, `BankKey` is the first member of every entry
  and each entry equals the old one minus `BankKey`.
- 151 `alundra_N` + 244 `N`, 395 distinct keys, every converter bank used exactly once; the key of each entry is found three independent
  ways (uuid5 of `entity:KEY/FOLDER` brute force, the `bank<KEY>_anim…` file names, the header content), 0 ambiguous, 0 unresolved.
- Witnesses: `"91"` -> `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08`, `"79"` -> `1ef8f16c-605f-58c3-af19-b6bb602fc40f`, `"alundra_7"` ->
  `62670cc6-953d-5fb2-ad61-8f130b96bf1f`, `"alundra_2"` -> `4d0c2294-87b6-50da-99ca-d789281cc580`, `"alundra_0"` ->
  `192c2eeb-bdda-5bb4-92d6-39a4ae298307`, `"255"` -> `14fa90f8-4764-531c-b751-091b644580d7`.

## Predicted `Data/map-sprite-indices.json`

- 483 keys (`"0"` to `"482"`), 2 507 indices, 65 empty arrays (`"0"` among them), 244 distinct indices, at most 22 per map.
- 30 913 bytes, SHA-1 `63a3e6189d1f1375232bb2c54e1d1dcb385e360d`.
- For the 483 maps, the non-null `SpriteRecords[i]` (i <= 255) and the `SpriteTable[i] != -1` coincide (0 mismatches).
- `"388"` = `"451"` = [121, 122, 237, 253, 254, 255]; `"432"` = [29, 253, 254, 255]; none contains 213; `"95"` and `"322"` contain 233 to 236.

## Predicted manifests and report (relative to the T1 reference export `m1`)

- T1 (`m1` against `m0`): `report.json` only; anything else is a stop.
- Export 1 (`m2` against `m1`): changed `Data/sprite-records.json` and `report.json`, added `Data/map-sprite-indices.json`, nothing removed.
  `AssetInfos.json` unchanged (`a09917c3…`). Both files byte-equal to the predictions.
- Export 2 (`m3` against `m2`): `report.json` only.
- `report.json` of export 1 against T1: `Errors` 0, same warnings, same `Verify.*`, same counters (none added or removed),
  `OutputFileCount` + 1; `OutputSizeBytes` = T1 + 10 080 + 30 913 + deltas of `Alundra.dll`, `Alundra.pdb` and the on-disk `report.json`.
- Nothing under `alundra-project/UI/Screens/` changes.
