# E20.g4a - T1 record (the arcs)

Start HEAD of T1: `eaad128e` (T0). Branch `chantier/theme1-porter-plateforme`. Code of the DLL changed: the preset `barricade-m392` only (`Alundra/SaveGames/TestSaves.cs`). Engine, converter and data: no diff since `cf56e063`.

## What was added

| Site | Change |
|---|---|
| `Alundra/SaveGames/TestSaves.cs` | preset `barricade-m392` (slot `test-barricade-m392`): map 392, tile (29, 13), z 4, flag G1641 (the `ArcSpec` of A5) |
| `Alundra.Tests/SaveGames/TestSavesTests.cs` | the table of names (`AllPresetNames`, + `BarricadePresetName`) and a new Fact `TheBarricadePreset_...` (hand-written: map, tile, word 51 = 0x200 and no other word, the kit's counters, accepted by the rules) |
| `docs/test-saves.md` | section "Barricade preset (E20.g4a)" |
| `Alundra.Tests/Arcs/ShipBarricadeArcTests.cs` | new: the pilot of `pilot-rules.md` (`Pilot`, `Fly`), arcs a1, a2, a3 at k = 1, 2, 3 |
| `Alundra.Tests/Support/ArcRun.cs` | `ArcsThatSlide`: `A5j-a1-k1/2/3` = 10, 9, 12 and `A5j-a2-k1/2/3` = 10, 9, 12 (T0 (d)); a3 absent (0); not in `ArcsWithoutEntityContact` |
| `Alundra.Tests/Arcs/ShipCorridorArcTests.cs` | doc of A5 only ("crossing them is E20.g4" -> A5 teleports past them, the jump is E20.g4a); A5 and A5r unchanged |

Sites of the plan re-located by content: see `README.md` of this folder (the plan's names became `ShipCorridorArcTests`, `TestSavesTests` under `SaveGames/`, `ArcRun`). The "two tables" of the presets test: in the current file the table of names is `AllPresetNames` (and `TheThirteenPresets_AreListed_InTheOrderOfTheTable` reads it); the weapon-expectations table (`WeaponRows`) is for weapon presets and is not touched (the barricade preset has no weapon); the new Fact carries the hand-written expectations of the preset.

## The arcs (9 tests: 3 theories x 3 cadences)

Names (the key of `ArcsThatSlide`): `A5j-a1-k{1,2,3}`, `A5j-a2-k{1,2,3}`, `A5j-a3-k{1,2,3}`. Every expected value is the T0 value of `README.md`/`dll-probes/r.txt`, written by hand in the tests (not read from the code under test).

- a1 (YD 288): the arc ends at the first image after a poll with the jump opportunity. frame/x/y/slides/y0: k1 317 / 657.0 / 295.34375 / 10 / 290.34375; k2 159 / 670.65625 / 295.25 / 9 / 290.75; k3 111 / 663.34375 / 295.125 / 12 / 289.125. The law `295 + ((y0 - 295) mod 0.5)` and `(y - y0) / 0.5 = slides` are asserted; no roll; the polls B1 made equal the polls the pilot predicted.
- a2: the phase changes (frame, x, y, z before the image) of `README.md` (b): left, after_jump (the Cross), south (stopped by rec9 at x = 597), west, down2; landing 18 ticks after the Cross at z 80 on the ground; departure towards map 391 at the final frames 427, 221, 155 and final positions (538.1953125, 376), (538.5, 376), (538.5, 376); slides 10, 9, 12; no roll; nothing skipped or exceeded.
- a3 (entry YD 304): the Cross + Left is pressed at the first opportunity (frames 301, 159, 111; y 304.96875, 307.0, 308.625); the hero never lands on the crate, ends flush at x = 657.0, same y, z 64, with 0 slide; no roll; no departure.

## Red before / green after

New tests (red before: they did not exist; the preset did not exist): `ShipBarricadeArcTests` (9) and `TestSavesTests.TheBarricadePreset_...`, plus the 3 theories of `TestSavesTests` over `EveryPresetName` for the new name (+3) = 13 new test cases, all green. Real mutations (applied by script, then restored by `cp`, `git diff` of the sites checked): (M1) `A5j-a1-k1` = 9 in `ArcsThatSlide` -> a1 k1 red (1 of 9); (M2) the pilot never lets go of the directions at a poll -> 9 of 9 red (rolls); (M3) the window of the jump widened to 700.0 -> 5 of 9 red.

## Suite

Release (copy neutralised, `skipcopy.targets` on build and test), partitions disjoint by full namespace (`FullyQualifiedName~Alundra.Tests.<Ns>.`; 1 = Arcs Ai Audio Cameras Cells Combat Dialogues, 2 = Entities Events ExportGuards Hero Items, 3 = the exact negation of the 12): 1 322 + 2 879 + 1 830 = **6 031 tests, 0 failure**. Debug last (no skipcopy), same partitions: 1 322 + 2 879 + 1 830 = **6 031, 0 failure**. `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning, `TextBoxGpu.cs`). The four `docs/hero-trace-389-*.txt` files: `git diff --ignore-cr-at-eol --quiet` rc 0 after each suite, then restored by `git checkout --`.

## Tests moved

Only the table of names of `TestSavesTests` (`AllPresetNames`), the doc of A5/A5r, and `ArcsThatSlide`: the predicted list. No existing test changed its expectation.

## Flagged

- Land detection and the slide `y0` are read at the `inventory` pass of each tick (state at the end of the previous tick), as in the T0 probes.
- The recipe for the author (acceptance): load `barricade-m392` (F9), hold Down then Left along the wall, jump (Cross + Left) when flush against the crates, reach the portal; do not hold a direction at the ship's poll (every 16 ticks).
