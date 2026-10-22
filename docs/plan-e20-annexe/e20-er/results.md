# E20.eR measured results (2026-10-22)

All values measured in this session. Scratch folder `<scratch>` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad`.

## Commits
- Parent (branch `chantier/theme1-porter-plateforme`): `9beac3ca` docs(plan-annex) T0 starting state; `4adf9393` chore(analyser) pointer `fc16872` -> `d687972`;
  `ce7e1ac7` test(maps) real-map test + two comments + `docs/formats/cells-companion.md`; then this file.
- Analyser (branch `chantier/e20er-tilecuteffectid` from `fc16872`): `d687972` fix(extractor) = `TiledMapExporter.cs:275` (+3 bytes) and
  `AlundraTools/docs/datas-bin-structure.md:78` (+55 bytes, text "(exported as the Tiled map property `TileCutEffectId`)"); 2 files, 1 line each;
  the committed blob keeps its 5 lines containing 0xD7; edits done by byte replacement (Python, binary), CRLF kept in the working copy.

## T1 reference export (HEAD 83e990ac, before any change)
- `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project`: exit 0, `Verification: PASSED (20139 loaded, 2477 existence-checked)`, 24 107 files, Duration 80,33 s.
- `m1` vs `m0`: ADDED 0, REMOVED 0, CHANGED 1 = `report.json`. Manifest SHA-1: m0 `b85441972ad31f638b253e23c85d09e178c330a4`, m1 `cc027af6c00a5f4e9255c98144d632420056b50a` (24 104 files each).
- After T1: sum of all file sizes 1 955 372 246, `report.json` 9 279 bytes, `OutputSizeBytes` 1 955 372 250, 6 warnings, 0 errors.
  The T1 `report.json` itself was not kept (overwritten by export 1); its counters are therefore compared only E1 vs E2.

## T2 controls
- `rg -l BalanceLevel alundra-datas-analyser`: the 10 expected files. In `TiledMapExporter.cs`: `"BalanceLevel"` 0 occurrences, `"TileCutEffectId"` 1.
- Release build of `AlundraDataExtractor.csproj`: 0 errors (610 warnings, pre-existing).

## T3 re-extraction (fresh Release build of `d687972`, run from `<scratch>/eR-run/` with `EntityNames.csv` copied there)
- Output `D:/development/repo/Alundra Remake/remaster-data-extracted-er` (later renamed `remaster-data-extracted`): exit 0, 79 s, 4 536 files.
- Console lines present: `BGM 44: rendered SILENT (123 frames, peak 0/0) - not exported`, `Extracted BGM: rendered=45 silent=1 failed=0 (of 46)`, `pitch guard hits: sequencer=18 sfx=0 exporter=0`;
  equal to `extract_stdout_dry_run.log` except the output path (line 2).
- `diff -rq` against `data-extracted/`: 483 lines, 0 "Only in"; path set equal to `expected_extract_changes.txt`.
- `verify_extraction_diff.py` (run on the diff with the quotes `'` stripped, because `diff` quotes paths containing a space): 483 of 483 maps differ only by the renamed property, 483 distinct (0..482), total delta +1 449 bytes (3 per map), values `{2}`.
- `diff -rq` against the dry-run folder `<scratch>/eR-dry2/extract-new`: 0 lines.
- Dry-run replay of T0 (`<scratch>/eR-dry2`): 86 s extraction, 483 lines, same path set.

## T4 mirror (PowerShell)
- `robocopy data-extracted -> "Alundra Remake/data-extracted.bak-er" /E`: 4 536 files, rc 1. `diff -rq` backup vs `data-extracted/` before the mirror: empty.
- `remaster-data-extracted` renamed `remaster-data-extracted.bak-er` (`diff -rq` against `data-extracted/` before the mirror: empty); `remaster-data-extracted-er` renamed `remaster-data-extracted`.
- `robocopy remaster-data-extracted -> data-extracted /MIR`: 4 536 files, rc 1. Final `diff -rq`: empty; 4 536 files on each side.
- `rg -l --no-ignore TileCutEffectId data-extracted`: 483 files; `BalanceLevel`: 484 files (483 native `data/map_N.json` + the `balance` data, unchanged by design).
- The eight earlier backups (data-extracted.bak-e19g0, -e19g0b, -lb, -x2; remaster-data-extracted.bak-2026-10-03, -e19g0b, -lb, -x2) are present. Nothing deleted.

## T6 red test
- New test `BalanceTablesTests.TheMapHeaderByte9_IsExportedAsTileCutEffectId_AndTheOldNameIsGone_OnTheRealMaps14_389_And_390` (the key at line 155 renamed in the same edit): `Alundra.Tests` Release run before the export had exactly one `[FAIL]`, that test (line 176, `TileCutEffectId` missing).

## T7 exports (calm window, no test process on the tree; a probe `dotnet test` of the main session ran from a scratch copy and was waited out before export 1)
- Export 1: exit 0, `Verification: PASSED (20139 loaded, 2477 existence-checked)`, 24 107 files, Duration 63,733 s. Export 2: same verdict, Duration 86,531 s.
- `m2` vs `m1`: CHANGED 967 = the 966 predicted paths + `report.json`; ADDED 0, REMOVED 0. SHA-1 before and after of each of the 966 equal to `predicted_export_changes.tsv`: 0 mismatches. Size delta total +2 898.
- `m3` vs `m2`: CHANGED 1 = `report.json`. Manifest SHA-1: m2 `0107ac083a5ff64a2e3267d2d14df9b818be5ac5`, m3 `74cb6b7c7a9695956543a8bd848cb17468de8f61`.
- `AssetInfos.json` identical in m1, m2, m3.
- `report.json`: Errors 0, Warnings 6 (equal), Counters and Messages equal E1 vs E2, `OutputFileCount` 24 107.
- `OutputSizeBytes`: E1 = 1 955 375 144. Calculation: others(T1) = 1 955 372 250 - 9 283 (the T0 `report.json` on disk during T1) = 1 955 362 967; + 2 898 + 9 279 (the T1 `report.json` on disk during E1) = 1 955 375 144. OK.
  E2 = 1 955 375 145 = 1 955 362 967 + 2 898 + 9 280 (the E1 `report.json`). OK. (Equivalent to the plan's "T1 + 2 898 + difference of the report.json on disk": 1 955 372 250 + 2 898 - 4.)
- `rg -l --no-ignore BalanceLevel alundra-project`: `Data/balance.json` only. `rg -l --no-ignore TileCutEffectId alundra-project`: 966 files.
- `Alundra.dll` SHA-1 `8034de825e133bd113822f1de44ff41fa7a8d1b5` and `Alundra.pdb` `db8a7cdec02014eb408c6d0b8008aae6b005e36b` unchanged until T8.

## T8 suites
- Converter tests (Release): 854 passed, 0 failed (31 s) = same count as T0.
- `Alundra.Tests` Release (skipcopy): 5 098 passed, 0 failed (7 min 29 s) = 5 097 + the new test.
- `Alundra.Tests` Debug (last build, no skipcopy): 5 098 passed, 0 failed (6 min 55 s). It deposited `alundra-project/Alundra.dll` SHA-1 `a4bf95f39450cca5abb57da5cdfd6593cae9ba7e` and `Alundra.pdb` `69d50ea3941955c54b087a07eaf142279ec38f8a`;
  `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 known warning (`Session` / `TextBoxGpu.cs:266`).
- The runs rewrote the line endings of the four `docs/hero-trace-389-*.txt` (content unchanged with `--ignore-cr-at-eol`); restored by `git checkout --`, never indexed.

## Outside the repository
- `D:/development/repo/Alundra Remake/`: `data-extracted.bak-er` (copy of `data-extracted/` before the mirror), `remaster-data-extracted.bak-er` (previous remaster), `remaster-data-extracted` (the new extraction).
- `<scratch>/eR-run/` (m0..m3, export logs, extraction console, diffs, robocopy logs, `check_t7.py`, `report-after-export1.json`, `report-after-export2.json`), `<scratch>/eR-dry2/` (dry-run replay).

## Not touched, flagged
- `docs/demarrage-nouvelle-partie.md:67` still lists `BalanceLevel` among the map `custom_properties` (not in the plan's perimeter); historical docs untouched (R7).
