# E20.f4, T4 (2026-10-23): results (ADR, mutants of the DLL code, suites, checks)

HEAD at the start of T4: `cf5e5a3d` (phase B). Branch `chantier/theme1-porter-plateforme`. The two fresh verifiers are launched by the main session (not part of this pass).

## 1. ADR

`docs/decisions/0084-the-charged-arrow-of-william-is-born-in-production-with-c94-as-a-native-handler-e1-in-line-and-the-cut-of-the-grass-called-at-every-tick.md`, index line in `docs/decisions/README.md`. The number was recounted on all 37 branches (local and remote): the last taken is 0083 (the current branch), nothing above on any branch. The ADR is the one the plan names "common to f4, f5 and f6": it records f4; f5 and f6 extend it.

## 2. Mutants of the DLL code (applied by script, reverted, `git diff` empty on each mutated file afterwards)

Script: `t4/t4mut.py` (each mutant: exact one-occurrence text replacement, `dotnet test` Release with `skipcopy.targets` on the focused filter, revert in a `finally`). Log: `t4/mutants.txt`.
15 mutants (the 13 of the plan, the force shift split in X and Y, and the removal of the port call alone), **all KILLED**:

| Mutant | Edit | Killed (failed tests of the focused run) |
|---|---|---|
| M01 C94 absent | `Tick` does nothing | 26 |
| M02 period 8 | `& 3` -> `& 7` | 20 |
| M03 trail also at S | trail also made at counter 1 | 4 |
| M04 trail in status 3 | the status Deactivated allowed | 1 (after the fix below) |
| M05 wrong animation | direction index 0 | 14 |
| M06a / M06b forces not shifted | `-forceX >> 1` -> `-forceX` (X, then Y) | 12 / 4 |
| M07 Z offset 4 px | `0x80000` -> `0x40000` | 16 |
| M08 plug called every 4 ticks | call only when `(counter & 3) == 0` | 17 |
| M09 plug never | call removed | 23 |
| M09b port call removed, journal kept | `TileEffect.Trigger` removed | 3 |
| M10 7 forgotten in the registry | `ChargedArrowAi.Register` removed | 13 |
| M11 7 absent from the set | `{27, 28, 29}` | 4 |
| M12 4 present in the set | `{4, 7, 27, 28, 29}` | 2 |
| M13 E1 without break effect | `DestroyEntity(entity, 0)` in the inline branch | 4 |

**One survivor found and fixed.** M04 survived the first run: the theory `C1_TheTrailIsMadeInTheStatusNormalOnly_TheStubIsCalledAllTheSame` held only the statuses Normal and FlagToDestroy. T1 had fixed the status 3 case "by (c1)" in its notes but the case was not in the theory. Fix: `[InlineData(EntityStatus.Deactivated, 0)]` added (test file `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs`, +1 line, green on the unmutated code, red on M04). The plan's mutant list names "trail in status 3"; it is now killed.

## 3. Suites

- `Alundra.Tests` **Release** (built with `skipcopy.targets`): **5 369 passed, 0 failed** (5 368 after T3 + the one new theory case), 8 min 15 s.
- `Alundra.Tests` **Debug** (last; it drops the DLL into `alundra-project/`): **5 369 passed, 0 failed**, 8 min 23 s.
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 known warning (`TextBoxGpu.cs`).
- The four `docs/hero-trace-389-*.txt` rewritten by the suites: `git diff --ignore-cr-at-eol` empty, restored by `git checkout`, never indexed.
- Engine (`CasaEngineMonogame`), analyser, converter, `data-extracted/`, `alundra-project/` sources: no diff since `c7c77eed` (the submodule pointers are unchanged, `git diff c7c77eed HEAD --raw` empty on them; the two `m` flags are the author's earlier local modifications, untouched).

## 4. Moved tests, predicted list (`../results.md` section 4) against measured

Predicted (4): the two registry pins (`NativeSpriteRegistryTests.ProductionRunner_RegistryHolds...`, `WaveSwordLifeTests.V3_...`), `WeaponSpawnSeamTests.S5_...` (phase A clause, phase B equality), `BirthTickTests.TheSoundOfTheCreation_...` (to watch, stayed green unmodified).
Measured (T1 and T3, confirmed by the full suites above): the same three rewritten tests (registry pin, V3, S5 in both phases); `BirthTickTests` green without change. **Measured is included in predicted, nothing outside the list moved**; 4 distinct tests at most, under the stop threshold (10).
The only test touched in T4 is `ChargedArrowAiTests` (own test of f4), not an existing test of the previous list.
