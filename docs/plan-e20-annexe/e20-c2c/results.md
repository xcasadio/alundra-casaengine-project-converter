# E20.c2c T1 - results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, start HEAD `651b5f42`. Commits: tests `5180f695`, code `de94189e`. Files: `Alundra.Tests/SaveGames/TestSavesTests.cs` (tests), `Alundra/SaveGames/TestSaves.cs` and `docs/test-saves.md` (code and doc). No other production file, no ADR.

## Count at the start HEAD

`AllPresetNames` held 28 presets (the test was `TheTwentyEightPresets_AreListed_InTheOrderOfTheTable`); with the four new ones it is 32, the test is renamed `TheThirtyTwoPresets_AreListed_InTheOrderOfTheTable`. The four presets are listed after `blessure-m14` (table and `AllPresetNames`).

## Red before the presets (Release, `--filter TestSavesTests`: 22 red of 186)

- (p1) `TheArmourPresets_AreTheCellOfBlessureM15_WithFullLife_TheKitAndTheOneArmourOfTheirName_NoBootsNoBraceletNoCharm`: 4 rows red (tissu 0x11, cuir 0x12, ancienne 0x13, argent 0x14; "unknown preset").
- (p2) `TheArmourPresets_WearTheArmourOfTheirName_AndAnOrcsBlowCostsFourThreeTwoOneHearts`: 4 rows red (6, 7, 8, 9).
- (p3) `TheThirtyTwoPresets_AreListed_InTheOrderOfTheTable`: red (the table had 28).
- The rows of `EveryPresetName` for the four new names: `ABuiltPreset_IsAcceptedByTheRealRules`, `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` x 4 = 12 red.
- `TheOtherPresets_LeaveTheDeathCounterAtZero` (a `[Fact]` that loops over `AllPresetNames`, unchanged body): red too. Not in the plan's list of moved tests (see deviations).

Green after the presets: `TestSavesTests` 186 / 186.

## What (p2) proves

The save built by the preset goes through `SaveGame.ApplyTo(GameState)`, then `PlayerManager.FillEquipmentRecords` (steps 2-5 of `UpdateItemEffectState`, real item and balance tables, threshold 0) and `ComputeHeroSheet` (step 6). The armour record is the item of the name (`ItemIdOf(Armour)` = 0x11, 0x12, 0x13, 0x14) and `Damage.ResolveBalanceTarget(sheet 341 `Attacks[0]` (attribute 2, power 4, asserted), hero sheet, 10, records, 0)` gives 6, 7, 8, 9: a blow of 4, 3, 2, 1 hearts. No stop condition met.

## Suites (zero failure)

Disjoint partitions by namespace prefix (`Alundra.Tests.`), `--blame-hang-timeout 60s`, `--no-build`, as `docs/plan-e20-annexe/e20-g2c2/t3.md`. Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug last.

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 567 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 195 | 0 | 0 |
| `Hero.Hero*` | 1299 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 | 0 |
| everything else | 2183 | 0 | 0 |
| total | 7334 | 0 | 0 |

7334 = 7314 (g2c2 T3) + 20: 8 new (p1, p2) and 12 rows of the three `EveryPresetName` theories. The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 existing warning (`TextBoxGpu.cs`).

## Deviations from the plan

- Besides the list test and the `EveryPresetName` rows, `TheOtherPresets_LeaveTheDeathCounterAtZero` also loops over `AllPresetNames`; it was red before the presets and is green after, with its body untouched (the plan's prediction missed this one).
- T2 (the two mutations) is the main session's.
