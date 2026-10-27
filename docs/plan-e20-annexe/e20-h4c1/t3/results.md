# E20.h4c1 T3 (mutants, ADR) - results (annex)

Run at the HEAD `5c5e6bc0` (branch `chantier/theme2-attaques-objets`, 2026-10-27), after T2. Plan: `docs/plan-e20-heros.md` section 1.31.3 (revision 2, READY), T3. The two fresh verifiers of T3 are the orchestrator's, not run here.

## 1. Method

`t3/mutants.py` applies a mutant to the source by text replacement (the pattern must match exactly once), runs the targeted suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, `--blame-hang-timeout 60s`), restores the file in a `finally` and checks `git diff --quiet` on the two mutated files (`git_diff_empty=True` after every one). The mutations are real (never simulated in a test). The suite of each run: filter `EarthTome1 | NativeSpriteRegistryTests | LiftableAiRegistrationTests | ChargedArrowAiTests | WaveSwordLifeTests | BallFlamesTests | HeroItemUseTests | Tome` (336 tests, 337 after the survivors' test D5), about 80 s a run. The baseline without mutation: 337 green.

## 2. The 29 mutants (28 plus the declared equivalent)

| Mutant | Plan item | Result | Red cases (first names) |
|---|---|---|---|
| m01 speed of the second group to 1024 (`speed > 1024 ? 1024`) | speed | KILLED, 13 | D1 x2, D2, P1, W1.. |
| m02 boundary 60 to 65 | boundary | KILLED, 12 | D1 x2, D2, P1, W1.. |
| m03 one birth a tick | one birth | KILLED, 14 | D1 x2, D2, P1, C1, W1.. |
| m04 tick 20 to 25 (first entry) | tick 20 | KILLED, 14 | D1 x2, D2, T1, P1, W1.. |
| m05 tick 95 to 90 (last entry) | tick 95 | KILLED, 13 | D1 x2, D2, T1, P1, W1.. |
| m06 delta of the opposite sign (`- delta`) | delta sign | KILLED, 10 | D1 x2, P1, W1, W2, W3.. |
| m07 two deltas permuted (the first pair) | delta permuted | KILLED, 12 | D1 x2, T1, P1, W1, W2.. |
| m08 base `BoxDirection` instead of `TargetDirection` | base direction | KILLED, 12 | D1 x2, P1, W1 x2, W2.. |
| m09 `& 31` removed | `& 31` | KILLED, 12 | D1 x2, P1, W1 x2, W2.. |
| m10 `<< 8` to `<< 7` | shift | KILLED, 13 | D1 x2, D2, P1, W1 x2.. |
| m11 null parent | null parent | KILLED, 2 | D1 x2 |
| m12 Z speed written on the hero for a null birth | ForceZ on a null birth | **SURVIVED at first**, then KILLED, 1 (D2, completed) | D2 |
| m13 a null birth dereferenced | null deref | KILLED, 4 | D1 x2, D2, P2 |
| m14 phase threshold 99 | threshold 99 | KILLED, 10 | D1, D4, P1, P2, W1, W2.. |
| m15 phase threshold 101 | threshold 101 | KILLED, 11 | D1, D3, D4, P1, P2, W1.. |
| m16 guard accepts `0x34` | guard 0x34 | KILLED, 4 | D1 x2, D1 counter, D3, T4 (h4a) |
| m17 guard ignores the slope | guard slope | KILLED, 3 | D1, D3, T4 (h4a) |
| m18 guard of the phase 1 removed | phase 1 guard | KILLED, 4 | D1 x2, D3, W6 (the interruptions 98 and 101) |
| m19 phase 2 never ends | phase 2 | KILLED, 11 | D1, D1 counter, D4, P1, P2, W1.. |
| m20 phase 2 ends at once | phase 2 | KILLED, 11 | D1, D1 counter, D4, P1, P2, W1.. |
| m21 table read as 32-bit words | table words | KILLED, 17 | D1 x2, D2, D4, T1, P1.. |
| m22 E3 without `\| 0x40` | E3 `0x40` | KILLED, 10 | E1 x2, P1, W1, W2, W3.. |
| m23 E3 with `~0x10` instead of `~0x30` | E3 mask | KILLED, 1 | E1 (the 224 vectors) |
| m24 E3 without the test of the end of the animation | E3 end test | KILLED, 13 | E1 x2, P1, C1, W1 x2.. |
| m25 E3 registered for `{16, 17}` | registration 17 | KILLED, 2 | E2, P4 |
| m26 E3 registered for `{16, 23}` | registration {16, 23} | KILLED, 2 | E2, P4 |
| m27 the cancellation not neutral (phase and animation written) | cancellation | KILLED, 2 | D5, W6 |
| m28 the cancellation writes the hero's Z force | cancellation | **SURVIVED at first**, then KILLED, 1 (D5, new) | D5 |
| e01 equivalent: `DestroyEntity(e, 0)` instead of `-1` | declared equivalent | KILLED, 1 by **(e1) alone** (`E1_E3_TheTwoHundredAndTwentyFourVectors...`) | E1 |

The declared equivalent is killed by (e1) alone, as the plan says: no other test went red (the sprite 16 has no break effect, so the world tests do not see the code).

## 3. The two survivors

- **m12** (a refused birth writes `hero.ForceZ`): (d2) snapshotted the hero's animation, item, X and Z but not `ForceZ`. Fix: `ForceZ` added to the snapshot of `D2_ARefusedBirth_WritesNothing...` (commit `abed9330`). Killed, 1 red.
- **m28** (the cancellation writes `hero.ForceZ`): m27 was killed only by W6, no test pinned the neutral cancellation on the hero. Fix: `D5_TheCancellation_0x800351A0_IsNeutral_ItWritesNothingOnTheHero` (a snapshot of ten hero fields over four phases, plus no spawn call). Killed, with m27 now also killed by D5 (2 red). Both in `abed9330`.

The test count of the filter goes from 336 to 337; the 25 mutants run before the fix are unchanged by it (the added assertions can only add red cases; m12, m27 and m28 were rerun with the new tests).

## 4. After the mutations

`git diff` of `Alundra/` empty; the targeted filter 337 green in Release; `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`). The full partitions of T2 (7675) were not rerun: T3 changed one test file and added one test (7676 with D5), no DLL code.
