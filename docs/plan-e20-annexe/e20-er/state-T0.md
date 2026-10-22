# E20.eR T0: starting state (measured 2026-10-22, parent HEAD 83e990ac)

Facts only; every number below was measured in this session.

## Repositories
- Parent: branch `chantier/theme1-porter-plateforme`, HEAD `83e990ac`. `git status --short`: ` m CasaEngineMonogame`, ` m alundra-datas-analyser`,
  `?? tools/Convert-All-AlundraSaves.ps1`, `?? tools/Convert-AlundraSave.ps1`, `?? tools/Saves/` (author's files, never indexed).
- Analyser: HEAD `fc16872` (branch `chantier/e19-suite3` at start). `git status --short`: ` M MGUI`, `?? AlundraTools/log.txt` (author's local changes, never indexed).

## Extracted data
- `diff -rq "Alundra Remake/remaster-data-extracted" data-extracted`: empty. 4 536 files on each side.
- Free space on D: 80 GB (`df -h /d`).

## Project `alundra-project/` (before any change)
- `find alundra-project -type f`: 24 107 files; sum of sizes 1 955 372 250 bytes; `report.json` is 9 283 bytes.
- `report.json`: `OutputFileCount` 24 107, `OutputSizeBytes` 1 955 373 164, `Errors` empty, 6 warnings (3 EntityNames.csv, 2 maps.json, 1 Sprites).
  OutputSizeBytes - disk sum = 914 bytes (the value was measured while the previous, larger report.json was on disk).
- Manifest `m0` (`python -B manifest.py alundra-project m0.txt`): 24 104 files, manifest SHA-1 `b85441972ad31f638b253e23c85d09e178c330a4`.
- `alundra-project/Alundra.dll` SHA-1 `8034de825e133bd113822f1de44ff41fa7a8d1b5`; `Alundra.pdb` SHA-1 `db8a7cdec02014eb408c6d0b8008aae6b005e36b`
  (unchanged after the Release test runs below, copy neutralised).

## Tests (Release, `CustomAfterMicrosoftCommonTargets=skipcopy.targets`, `--blame-hang-timeout 60s`)
- `alundra-casaengine-project-converter.Tests`: 854 passed, 0 failed, 0 skipped (42 s).
- `Alundra.Tests`: 5 097 passed, 0 failed, 0 skipped (7 min 46 s).
- The runs rewrote the line endings of the four `docs/hero-trace-389-*.txt`; `git diff --ignore-cr-at-eol --quiet -- docs/` was clean; restored by `git checkout --`.

## Dry-run replay (copy of `dry_run.sh`, `S` = scratchpad `eR-dry2`)
- Build Release exit 0; extraction exit 0 in 86 s; 4 536 files; `diff -rq` against `data-extracted/`: 483 lines.
- The set of relative paths equals `expected_extract_changes.txt` (483 paths, `diff` empty).
- The console equals `extract_stdout_dry_run.log` once CR are stripped, except the output folder path (line 2).
