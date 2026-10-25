# E20.g2c1 - T3 (phase C): the activation (2026-10-25)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c1" (revision 2, READY), task T3.

## The line of the phase C

- `PlayerManager.ProductionWiredLiftableTypes` = the 7 types of C23 plus `LiftableAi.C4ProductionTypes` (the 7 of C4: 290, 293, 363, 367, 368, 369, 373; the 11 shop articles 48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113; the ice block 304 is in both): **25 types**, i.e. 18 types added to the seven of C23. Every one of them holds `(C, 4)` or `(C, 23)` since the phase B.
- Presets `c4-m22` (map 22, tile (25, 20), level 3: the arrival of the author's save `66-crypte-de-lars.sav`; barrels recs 21 at (888, 464) and 33 at (1104, 240)) and `c4-m70` (map 70, tile (33, 29), level 3, the bank 24 px south-east of the trunk rec 8 at (780, 456); the 5 floating trunks to the north). Checked in the real world: on 70 the hero stands at (804, 472, 48), walks, the trunk rec 8 is at animation 0 / slope 0 and the five others at animation 5 / slope 4.
- `docs/test-saves.md`: a section for the two presets.

## Red before / green after (Release, neutralised copy)

| Test | Before the line | After |
|---|---|---|
| `BarrelLiftMap22ArcTests.A1_TheSquareAgainstABarrelOfTheMap22_LiftsIt` (a1: the Square against the barrel rec 21 of the map 22, from (37, 27, level 1): the barrel in the hands, animation 5) | red (`CarriedEntity` null: the sword) | green |
| `TestSavesTests.TheC4Map22Preset_...`, `TheC4Map70Preset_...` (a2) and the `EveryPresetName` theories on the two new names | not run red (written together with the table; they can only fail on an unknown name) | green |

(a1) is a new file: `LiftMap22ArcTests.cs` already exists (D5b), the new class is `BarrelLiftMap22ArcTests`. The start is next to the barrel because the preset's tile is far from the two barrels; the preset keeps the author's arrival.

## Displaced existing tests (predicted list of T0 (h): 7)

Phase B already moved 4 (the four registry pins). Phase C moves the 3 others of the list, all rewritten by derivation:
- `LiftPureTests.TheProductionSet_IsTheSevenTypesOfC23...` -> renamed `TheProductionSet_IsTheTwentyFiveTypesOfC23C4AndTheShopArticles_AndTheTestSeamReplacesIt` (the hand-written list of 25 types).
- `LiftWorldTests.M5_AnObjectOutsideTheSet_...`: `UnwiredType` 290 -> 393 (the torch, which stays outside; its scene takes the type as an argument). Name unchanged.
- `LiftMap72ArcTests.M5_TheBarrelOfTheFamilyC4_...` -> renamed `M5_AnObjectOutsideTheSet_GivesTheSword_TheJarOfTheSameSceneLifts`: the barrel rec 1 takes `SpriteType = 393` before the walk.
No test outside the list moved (the suites below are all green; nothing else was edited).

## Suites (zero failure)

Partitions disjoint by complete namespace with the final dot, the third the exact negation (`docs/plan-e20-annexe/e20-g1b/t4/run.sh` filters), `--blame-hang-timeout 60s`.

| Partition | Release (`-p:CustomAfterMicrosoftCommonTargets=...skipcopy.targets`) | Debug (last, copies the DLL) |
|---|---|---|
| P1 (`Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.`) | 1486 / 0 red | 1486 / 0 red |
| P2 (`Entities. Events. ExportGuards. Hero. Items.`) | 2956 / 0 red | 2956 / 0 red |
| P3 (the negation) | 1858 / 0 red | 1858 / 0 red |

(T2: 1485 / 2956 / 1850; +1 for (a1), +8 in P3 for the two preset facts and the `EveryPresetName` theories on the two new names.)

- The suites rewrote the line ends of `docs/hero-trace-389-*.txt`; `git diff --ignore-cr-at-eol --quiet` on the four files is clean; restored by `git checkout`, never indexed.
- `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning on `TextBoxGpu.cs`).
- The engine (`CasaEngineMonogame` gitlink `92d8e847`), the converter and the data: no diff since `09499204` (the working-tree modifications of `CasaEngine.Launcher/Program.cs` and `MGUI` in the engine and of `alundra-datas-analyser` are the author's, untouched).

## Deviations

- The wired set has 25 types, not "18": the plan's "18 types" are the ones added (7 + 11) to the seven of C23; the 304 was already in.
- Two renames of displaced tests (names that would have been false); the new (a1) lives in a new class because `LiftMap22ArcTests` exists.
- The preset `c4-m22` keeps the author's tile (25, 20, 3) with no flag; walkability of the route to the barrels was not walked in the preset itself (a1 starts next to the barrel).
