# R0 - export prediction and reference export (parent T0)

Written and committed BEFORE any code of slice R0 (engine or parent). Contract: `docs/plan-restructuration-dll.md`, section 4 (revision 4),
"T0 de la partie parent", and D-R-5 / D-R-9. Protocol: E20.e0x (`docs/plan-e20-annexe/e20-e0x/README.md`). This annex is frozen (D-R-6).

## Tools (copied, with checksums)

| file | SHA-1 | origin |
|---|---|---|
| `predict_names.py` | `2b180fad60a03155898dd0226df1744107b629a1` | scratchpad `dll-structure/r0-data/predict_names.py` (read-only, never writes the tree) |
| `manifest.py` | `cd4c6d7adad4b41a85d078cdab27bfcc2ced9913` | copy of `docs/plan-e20-annexe/e20-e0x/manifest.py` |
| `diff_manifests.py` | `6ca1ecb212c2ed5315783a65a99b1c0841dd95d7` | copy of `docs/plan-e20-annexe/e20-e0x/diff_manifests.py` |

## The change (export 1 of R0)

The names written in the data become full names of the CURRENT types, prefix `Alundra.Scripts.`:

- `script_class_name` of the 395 `.entity`: `AlundraEntityScriptProxy` -> `Alundra.Scripts.AlundraEntityScriptProxy`;
- `script_class_name` of the 483 `.world`: `AlundraWorldProxy` -> `Alundra.Scripts.AlundraWorldProxy`;
- `player_controller_class` of `Entities/Alundra/Alundra.gameMode`: `AlundraPlayerController` -> `Alundra.Scripts.AlundraPlayerController`;
- `view_model_type` of the six `UI/Screens/*.design.json` (versioned by hand, not written by the converter): `AlundraChoiceViewModel`,
  `AlundraHudViewModel`, `AlundraInventoryViewModel`, `AlundraSaveScreenViewModel`, `AlundraSubInventoryViewModel`,
  `AlundraTextBoxViewModel`, each prefixed by `Alundra.Scripts.`.

Command (from the repo root, tree = the reference export of T1):

```text
python -B -I docs/plan-restructuration-annexe/r0/predict_names.py alundra-project <out> \
  entity=AlundraEntityScriptProxy:Alundra.Scripts.AlundraEntityScriptProxy world=AlundraWorldProxy:Alundra.Scripts.AlundraWorldProxy \
  controller=AlundraPlayerController:Alundra.Scripts.AlundraPlayerController vm=<old>:<new> (x6)
```

## Prediction (`predicted/`)

- `predicted/allowed-paths.txt`: **886 paths** = 879 converter outputs (395 + 483 + 1) + 6 `design.json` + `report.json`. Passed as the
  allowed-path list to `diff_manifests.py` for export 1.
- `predicted/predicted-changes.tsv`: path, old size, new size, predicted SHA-1 of each of the 885 changed files (the ones above, without
  `report.json`). Every exported file is CRLF and carries the old string once (checked by the script, `PROBLEMS: 0`).
- Byte deltas: **+16 bytes per file** (`Alundra.Scripts.` is 16 characters); **+14 064** on the 879 converter outputs, **+96** on the six
  `design.json`. File count unchanged (24 104 in the manifest). `AssetInfos.json` unchanged (`a09917c3...`). Export 2: `report.json` only.
- `predicted-changes.tsv` lists the six `design.json` in the order of the `vm=` arguments; the discovery run listed `ChoiceScreen` and
  `SaveScreen` last (different argument order). Same set of lines, same SHA-1 (checked line by line).
- `report.json` of export 1 against the T1 reference below: `Errors` 0, same `Warnings` (6) and `WarningsByCategory`, same counters,
  `OutputFileCount` unchanged (the files are changed, not added), `OutputSizeBytes` = T1 value + 14 064 + 96 + deltas of
  `Alundra.dll`, `Alundra.pdb`, `.casaeditor` and the on-disk `report.json` (measured; an unexplained remainder is a stop).

## Starting state (T0 measures, 2026-10-16)

- Commits: parent `3f28554` (branch `chantier/restructuration-dll`), engine `781d3527` (the submodule pointer), analyser untouched.
- Tests: converter **854** passed (`dotnet test alundra-casaengine-project-converter.Tests --blame-hang-timeout 120s`); `Alundra.Tests`
  **5 092** passed, 0 failed (Release, copy neutralised with `skipcopy.targets`, 5 min 38 s). Trace guard (`git diff --ignore-cr-at-eol`)
  clean; the four `hero-trace-389-*` files showed EOL-only noise after the suite and were restored with `git checkout --`.
- Calm window: no `CasaEngine.Launcher`, `CasaEngine.Editor` or game process (`tasklist`; only idle MSBuild node-reuse `dotnet.exe`
  processes and the Serena language server, none on `alundra-project/`); `alundra-project/Alundra.dll` opened `r+b` without error.

## Reference export at HEAD (T1), before any code

- `m0` (before; `m0.txt`): **24 104 files**, manifest SHA-1 `0b9a343fdce6cfcdb9d31b892f694a6c98e788f9` (24 103 of E20.e0x + the
  `Data/map-sprite-indices.json` it added). Taken twice, before and after the test runs: identical.
- Export: `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project`, exit 0,
  `Verification: PASSED (20139 loaded, 2477 existence-checked)`, 56.22 s, `OutputFileCount` 24 107, `OutputSizeBytes` 1 955 358 035.
- `m1` (after; `m1.txt`): 24 104 files, manifest SHA-1 `c2db80dda86493cc1889be625dbc14e563d19794`.
- `diff_manifests.py m0 m1` (`diff-m0-m1.txt`): ADDED 0, REMOVED 0, CHANGED 1 (`report.json`), VERDICT within the allowed set. Exit 0.
  `AssetInfos.json` `a09917c31694925076d1fc1aba5fca773f6cada2` in both. `report.json`: `c4495678...` -> `d14d7f19ba4c445b79725574ac8efbdb0866e4e0`.
- `report-t1.json`: the reference `report.json` (Errors 0, 6 warnings in 3 categories, counters) against which export 1 is compared.
