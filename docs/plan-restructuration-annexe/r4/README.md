# R4 - export prediction and reference export (T0 (a))

Written and committed BEFORE any code of slice R4. Contract: `docs/plan-restructuration-dll.md`, section 8 (revision 2), T0 (a), and D-R-5.
Protocol: R0 (`docs/plan-restructuration-annexe/r0/README.md`), itself E20.e0x. This annex is frozen (D-R-6).

## Tools (copied from `r0/`, with checksums)

| file | SHA-1 |
|---|---|
| `predict_names.py` | `2b180fad60a03155898dd0226df1744107b629a1` |
| `manifest.py` | `cd4c6d7adad4b41a85d078cdab27bfcc2ced9913` |
| `diff_manifests.py` | `6ca1ecb212c2ed5315783a65a99b1c0841dd95d7` |

Byte-identical to the `r0/` copies (same sums as the R0 README).

## The change (export 1 of R4)

The names written in the data become the final full names (`final` of `tools/dll-structure/table.json`):

- `script_class_name` of the 395 `.entity`: `Alundra.Scripts.AlundraEntityScriptProxy` -> `Alundra.Entities.EntityScriptProxy` (-6 bytes);
- `script_class_name` of the 483 `.world`: `Alundra.Scripts.AlundraWorldProxy` -> `Alundra.Maps.WorldProxy` (-10 bytes);
- `player_controller_class` of `Entities/Alundra/Alundra.gameMode`: `Alundra.Scripts.AlundraPlayerController` -> `Alundra.Hero.HeroController` (-12 bytes);
- `view_model_type` of the six `UI/Screens/*.design.json` (versioned by hand): Choice -> `Alundra.UI.Choice.ChoiceViewModel` (-5), Hud ->
  `Alundra.UI.Hud.HudViewModel` (-8), Inventory -> `Alundra.UI.Inventory.InventoryViewModel` (-2), SaveScreen ->
  `Alundra.UI.Saving.SaveScreenViewModel` (-5), SubInventory -> `Alundra.UI.SubInventory.SubInventoryViewModel` (+1), TextBox ->
  `Alundra.UI.Messages.TextBoxViewModel` (-3).

Command (from the annex folder `docs/plan-restructuration-annexe/r4/`, tree = the reference export below):

```text
python -B -I predict_names.py ../../../alundra-project predicted \
  entity=Alundra.Scripts.AlundraEntityScriptProxy:Alundra.Entities.EntityScriptProxy \
  world=Alundra.Scripts.AlundraWorldProxy:Alundra.Maps.WorldProxy \
  controller=Alundra.Scripts.AlundraPlayerController:Alundra.Hero.HeroController \
  vm=Alundra.Scripts.AlundraChoiceViewModel:Alundra.UI.Choice.ChoiceViewModel \
  vm=Alundra.Scripts.AlundraHudViewModel:Alundra.UI.Hud.HudViewModel \
  vm=Alundra.Scripts.AlundraInventoryViewModel:Alundra.UI.Inventory.InventoryViewModel \
  vm=Alundra.Scripts.AlundraSaveScreenViewModel:Alundra.UI.Saving.SaveScreenViewModel \
  vm=Alundra.Scripts.AlundraSubInventoryViewModel:Alundra.UI.SubInventory.SubInventoryViewModel \
  vm=Alundra.Scripts.AlundraTextBoxViewModel:Alundra.UI.Messages.TextBoxViewModel
```

## Prediction (`predicted/`)

- `predicted/allowed-paths.txt`: **886 paths** = 879 converter outputs (395 + 483 + 1) + 6 `design.json` + `report.json`. Passed as the
  allowed-path list to `diff_manifests.py` for export 1.
- `predicted/predicted-changes.tsv`: path, old size, new size, predicted SHA-1 of each of the 885 changed files (without `report.json`).
  `PROBLEMS: 0` (each file carries the old string exactly once; counts 395 / 483 / 1 / 6 as expected).
- Byte deltas (computed by the script): **-2 370** (entity: 395 x -6), **-4 830** (world: 483 x -10), **-12** (gameMode), so **-7 212** on the 879
  converter outputs; **-22** on the six `design.json`. File count unchanged (24 104 in the manifest, 24 107 in `OutputFileCount`).
  `AssetInfos.json` unchanged (`a09917c31694925076d1fc1aba5fca773f6cada2`). Export 2: `report.json` only.
- `report.json` of export 1 against `report-t0.json` (the reference below): `Errors` 0, same 6 warnings in the same 3 categories, same counters,
  `OutputFileCount` 24 107 unchanged, `OutputSizeBytes` = 1 955 380 389 - 7 212 - 22 + deltas of `Alundra.dll`, `Alundra.pdb`, `.casaeditor`
  and the on-disk `report.json` (measured; an unexplained remainder is a stop).

## Starting state (T0 measures, 2026-10-17, HEAD `7ce82c2`, branch `chantier/restructuration-dll-r3`)

- Commits: parent `7ce82c2`; engine `92d8e847` (the pointer; its local change `CasaEngine.Launcher/Program.cs` is the author's and never staged);
  analyser untouched.
- Tests: `Alundra.Tests` **5 097** passed, 0 failed in Release (copy neutralised with `skipcopy.targets`, 6 min 34 s) and **5 097** passed in
  Debug (normal build; `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` equal; 7 min 28 s); converter **854**
  passed (49 s). Trace guard (`git diff --ignore-cr-at-eol`) clean after each Alundra suite; EOL-only noise on four `hero-trace-389-*` files
  restored with `git checkout --`.
- Calm window: no `CasaEngine.Launcher`, `CasaEngine.Editor` or game process (`tasklist`), `alundra-project/Alundra.dll` opened `r+b` without error.

## Reference export at HEAD, before any code

- `m0` (before; `m0.txt`): **24 104 files**, manifest SHA-1 `01e37b688bf6a1d4c47dc7347342f51fea6b8ae6`.
- Export: `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project`, exit 0,
  `Verification: PASSED (20139 loaded, 2477 existence-checked)`, 1 min 23 s, `OutputFileCount` 24 107, `OutputSizeBytes` 1 955 380 389.
- `m1` (after; `m1.txt`): 24 104 files, manifest SHA-1 `fe367de0070edf24df4311e9c3a767c9dc60399d`.
- `diff_manifests.py m0 m1` (`diff-m0-m1.txt`): ADDED 0, REMOVED 0, CHANGED 1 (`report.json`), VERDICT within the allowed set. Exit 0.
  `AssetInfos.json` `a09917c31694925076d1fc1aba5fca773f6cada2` in both. `report.json`: `de644257b6a8cc4018ab388a1c5924bdf282122b` after.
- `report-t0.json`: the reference `report.json` (Errors 0, 6 warnings in 3 categories, counters) against which export 1 is compared.

Reading the manifests and `report.json` on this machine needs `python -X utf8` (the default code page cannot print the diamond of some messages).
