# E14.a1a - export prediction (T0)

Written and committed BEFORE any converter code of E14.a1a. Contract: `docs/plan-e14-ia-native.md`, section 1.3a (revision 1).
Prediction computed by `docs/plan-e14-annexe/e14a/a1a/predict_records.py` (read-only, independent of the converter: it reads
`data-extracted/data/map_*.json` and the current `alundra-project/Data/sprite-records.json`), output in `a1a/predict_records.out`.

## The change

The sprite header byte `+0x1E` (`BreakEffect`, read by `DestroyEntity` `0x8003A59C` when `fx == -1`; 0 = no effect) is written into
every entry of `Data/sprite-records.json`, between `SizeZ` and `Contents`, always (0 included). No other file changes.

## Starting state (T0 measures, 2026-10-10)

- Tree `alundra-project/` (without `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`): 24 103 files, manifest SHA-1
  `bc31b7a2055a1dd939ef3e4280203c94a3ef354e` (`m0`), as expected.
- `m0` against the committed `docs/plan-e19-loop-annexe/lb-1008/m7-manifest-after.txt` (`a1a/m0-vs-m7.txt`): 0 added, 0 removed, 48
  changed = the 47 paths of `docs/plan-e19-loop-annexe/data/predicted_export_changes.txt` plus `UI/Textures/font3.png`. Nothing else.
- `Data/sprite-records.json` before: 2 552 373 bytes, SHA-1 `772bb8b764568c448e546e1c38e912066431b07f`. `AssetInfos.json` SHA-1
  `a09917c31694925076d1fc1aba5fca773f6cada2`.
- `report.json` on disk before T1: 9 280 bytes (export of 2026-10-08 20:30): `Errors` 0, `Warnings` 6, `Verify.Assets` 22 616,
  `Verify.Loaded` 20 139, `Verify.ExistenceChecked` 2 477, `Phase1.StalePagesPurged` 2 415, `Entities.Prefabs` 395,
  `SpriteRecords.Exported` 395, `OutputFileCount` 24 106, `OutputSizeBytes` 1 955 170 647.
- `Alundra.dll` 600 576 bytes, `Alundra.pdb` 255 608 bytes before T1.
- `Alundra.Tests`: 4 646 tests (`--list-tests`, Release).

## Predicted `Data/sprite-records.json`

- 395 entries, each equal to the old entry plus one line `    "BreakEffect": N,` (CRLF, 4-space indent) after the `"SizeZ"` line:
  395 inserted lines, 2 561 505 bytes (+9 132), SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`.
- Key order of an entry: `MoreFlags` ... `SizeZ`, `BreakEffect`, `Contents`, `IdsvAnimDirs`, `AnimSets`[, `DialoguePortrait`].
- Histogram of `BreakEffect` over the 395 entries: {0: 284, 2: 1, 8: 63, 10: 30, 11: 15, 12: 1, 13: 1}, 111 non-zero.
- Muruta (type 347, bank 91) `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08` = 8; Beannoide (type 335, bank 79)
  `1ef8f16c-605f-58c3-af19-b6bb602fc40f` = 8.
- All 395 entries match their canonical bank (first record of the bank, converter order); 0 unmatched, 0 ambiguous; the 29 header
  keys shared by several banks agree on `BreakEffect`. The predicted JSON parses and equals the old object minus `BreakEffect`.

## Predicted manifests and report (all relative to the T1 reference export `m1`)

- T1 (`m1` against `m0`): `report.json` only; otherwise a stop (attribute to the engine pointer rise, rebase as L-b2).
- Export 1 (`m2` against `m1`): changed exactly `Data/sprite-records.json` and `report.json`; 0 added, 0 removed.
  `AssetInfos.json` unchanged (`sprite-records.json` is not catalogued). The file is byte-equal to the prediction.
- Export 2 (`m3` against `m2`): `report.json` only.
- `report.json` of export 1 against T1: same counters (no new one), same 6 warnings, `Errors` 0, same `Verify.*`;
  `OutputSizeBytes` = T1 value + 9 132 + size delta of `Alundra.dll` and `Alundra.pdb` + delta of the on-disk `report.json` size at
  the time of each measure (`MeasureOutput` counts the previous report); a remainder not explained by these three terms is a stop.
- Nothing under `alundra-project/UI/Screens/` changes (tracked by git).

## Open at T0

Nothing. The engine pointer rose since the last export (`781d3527`); T1 settles whether that changes any exported byte.
