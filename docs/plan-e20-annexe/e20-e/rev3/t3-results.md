# E20.e, T3 (phase B): measured results (2026-10-23)

Start HEAD `881bf7c5` (T2), branch `chantier/theme1-porter-plateforme`. Phase B: the two production calls and the preset.

## Code

- `Alundra/Hero/PlayerAnimationEffects.cs`: `PlayerGrassCut` records its call (the journal of the named stubs, kept) then calls `TileEffect.Trigger(player)`. The doc of the class and the three stubs left (steps, walk cycle, landing) now say E20.e2.
- `Alundra/Ai/Projectiles/WaveSwordAi.cs`: `CheckAndTriggerTileEffect` appends to `StubCallsForTests` (kept) then calls `TileEffect.Trigger(entity)`. The same two lines are what E20.f4 (`ChargedArrowAi`) and E20.h3 (`BombAi`) reproduce.
- `Alundra/SaveGames/TestSaves.cs`: the preset `herbe-m14` (map 14, tile (31, 43, 1), the weapon loadout, the Steel Edge in the slot 1), the last of the table. `docs/test-saves.md`: its section.

## Tests

- T2 world tests: 10 of 10 green (were 7 red, 3 green twins), the export guard 3 of 3 (green throughout). Red before and green after, by name: W1, W2, W5, W6 (sword), W6 (wave), W7, W8.
- Existing tests that moved, against the predicted list of T0 (e): exactly the 2 predicted, none outside the list.
  1. `Alundra.Tests.Hero.HeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle`: the sounds after the swing on the ground `[29]` become `[29, 31]` (binary: `rev3/oracle/out/heroattack_m10.txt`, cut of (10, 56) at the tick 2, sounds 29 then 31). Failed on the unchanged test, then rewritten by derivation.
  2. `Alundra.Tests.SaveGames.TestSavesTests`: the test that pins the list of presets (`TheElevenPresets_AreListed_InTheOrderOfTheTable`, renamed `TheTwelvePresets_AreListed_InTheOrderOfTheTable`; the plan said "Nine/Ten", the actual names were eleven then twelve). `herbe-m14` joins `AllPresetNames`, the by-hand weapon rows (`WeaponRows`) and the theories over the presets (5 more cases).
  The first full Release run after the wiring failed on `HeroAttackTests` only (5 328 passed, 1 failed); `TestSavesTests` had been updated with the preset in the same step.
- Count: 5 329 tests = 5 311 (end of T1) + 10 world + 3 guard + 5 preset cases.

## Suites

| suite | tests | failures |
|---|---|---|
| Release, neutralised copy (build then test, `--blame-hang-timeout 60s`), after the fix of `HeroAttackTests` | 5 329 | 0 |
| Debug, last | 5 329 | 0 |

`python -B tools/dll-structure/check_names.py --rules`: 0 findings, 1 known warning (`TextBoxGpu.cs:266`). `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical.
The traces `docs/hero-trace-389-*.txt` were rewritten with CRLF by the suites (no content change with `--ignore-cr-at-eol`, the 389 has no cuttable cell) and restored by `git checkout --`; never indexed. The engine, the converter, the data and `alundra-project` have no diff from this task.

## Notes

- The generator and the sounds of the hero's swing on map 10 (the existing `HeroAttackTests`) now include one draw and the sound 31 at the tick 2; no other existing test observes them.
- The preset arrives facing south (a save carries no direction); the recipe in `docs/test-saves.md` says to walk west first.
