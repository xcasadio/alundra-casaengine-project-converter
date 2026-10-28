# E20.h4d1 T2 (phase B and production tests) - results (annex)

Run at the start HEAD `145b8463` (branch `chantier/theme2-attaques-objets`, 2026-10-28). Plan: `docs/plan-e20-heros.md` section 1.31.5 (READY), T2; D-H4D1-1 to D-H4D1-4. Two commits: the tests (red) `302aced3`, then the code `bac14971`. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `data-extracted/`, `tools/` was changed; `alundra-project/` only received the Debug DLL deposit (`cmp` of `Alundra.dll` and `.pdb` identical).

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Hero/PlayerManager.cs` | `FireTome2Item = 0x30` and the three entries: start = `TomeStart` (h4a, same branch as the tomes `0x2B` and `0x2C`), driver = `FireTome2Ai.Driver`, cancellation = `FireTome2Ai.Cancel` (three branches of the existing ternaries / `if` chain, as h4c1 and h4c2) |
| `Alundra/Ai/NativeSpriteRegistry.cs` | `FireTome2Ai.Register(registry)` in `CreateForProduction` (and the two doc comments that list the keys) |
| `Alundra.Tests/Ai/Items/FireTome2ProductionTests.cs` (new) | (w8) to (w10) |
| `Alundra.Tests/Ai/Items/FireTome2Run.cs` | `production: true` parameter: no seam and no registration by the bench |
| five registry sums | `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`: `+ FireTome2Ai.Keys.Count` and `FireTome2Ai.Register(sum)` in the secondary registries (derived, not re-typed) |

(w8) Rond with the item `0x30`, no seam: the whole key of (w1) (`fire2_cast_pf`, every tick, every field, the nine words of the block where the step of the shake leaves them), the column born at 34, its eight effects at 154, the item freed at 182, the magic 3 throughout. (w9) the table of slots filled from the tick 2 to 60 with the inert filler type 5: no column, no effect, the item `0x30` at 33 and freed at 34, the magic 3 throughout (lost), the animation never `0x34` and Idle at the end, the block and the buffer all zero (nothing captured). (w10) Rond with the item `0x30` interrupted at the tick 60 by the production path (R-EL-6), compared as (w2): the key `fire2_hurt60`, the column at the status 3 and destroyed at the tick 60, eight effects, the item freed, the block restored.

## 2. Red before, green after

Tests commit `302aced3` against the HEAD without the phase B: **8 red of 128** in the filter (`red_before_phase_b.txt`) = (w8), (w9), (w10) and the five sums. Code commit `bac14971`: the filter (`FireTome2|NativeSpriteRegistryTests|LiftableAiRegistrationTests|ChargedArrowAiTests|WaveSwordLifeTests|BallFlamesTests|CameraSwayBuffer|HeroItemUse|Tome`, 418 tests) all green.
Moved tests: exactly the five predicted by T0 (`t0/predictions.md`, the five sums); no other test moved.

## 3. Proof of removal (real mutations by script `removal_proof.py`, undone, `git diff` empty after each; outputs `removal_*.txt`)

| Mutation | Red | Plan |
|---|---|---|
| the three entries `0x30` back to the stub (`entries_stub`) | (w8), (w9), (w10): 3 tests | exactly as the plan |
| the cancellation entry alone back to the stub (`cancel_stub`) | (w10) alone | exactly as the plan |
| the call `FireTome2Ai.Register(registry)` removed (`noreg`) | (w8), (w10) and the five sums: 7 tests | as the plan (the "two pins" are now five) |
| E28 neutralised: no effect, no draw, no destruction (`e28_neutral`) | E1, E2, E3, E4, E6, W1, W2, W4, W4b, W5, W7, W7b, (w8), (w10): 15 tests | plan: (e1) to (e4), (w1), (w2), (w4), (w5), (w7), (w8), (w10); superset by E6 (the tables, which recompute the radii), W4b and W7b (the same worlds as W4 and W7). (e5), (w3), (w6) and (w9) stay green, as the plan |
| the capture and the restoration made inert (`buffer_inert`) | B1, B2, D1, D4, C1, C2, D5, W1, W2, W3, W4, W4b, W5, W7, W7b, (w8), (w10): 17 tests | plan: (b1), (b2), (d1), (d4), (w1) to (w5), (w8), (w10); superset by C1, C2, D5 (the cancellation, the guard and the death restore the buffer), W7 and W7b |

## 4. Suites (all green)

Seven disjoint partitions by namespace prefix (`part.sh`), `--blame-hang-timeout 60s`, `--no-build`; Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug (built last, copies the DLL). Details in `suite.summary`: total **7828** = 7825 (T1) + 3 (W8, W9, W10), 0 failure in Release and in Debug. The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).

## 5. Notes

1. (w9): the plan says "Idle sans `0x34`": measured Idle (animation 0) at the tick 80 and never `0x34`; the item is freed at the tick 34 (the driver answers 1 at the refused birth) and the magic is not given back.
2. A Debug run of the partition 4 was launched twice by accident (the harness moved the first loop to the background at 600 s); both runs 1331 green.
