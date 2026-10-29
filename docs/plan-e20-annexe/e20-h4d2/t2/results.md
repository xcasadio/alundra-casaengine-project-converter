# E20.h4d2 T2 (phase B and production tests) - results (annex)

Run at the start HEAD `de86d3d9` (branch `chantier/theme2-attaques-objets`, 2026-10-29). Plan: `docs/plan-e20-heros.md` section 1.31.6 (revision 2.1, READY), T2; D-H4D2-1 to D-H4D2-7. Two commits: the tests (red) `21fe4b68`, then the code `473f7ea0`. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `data-extracted/`, `tools/` was changed; `alundra-project/` only received the Debug DLL deposit (`cmp` of `Alundra.dll` and `.pdb` identical).

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Hero/PlayerManager.cs` | `FireTome1Item = 0x2F` and the three entries: start = `TomeStart` (h4a), driver = `FireTome1Ai.Drive`, cancellation = `FireTome1Ai.Cancel` (three branches of the existing ternaries / `if` chain, as h4c1, h4c2 and h4d1) |
| `Alundra/Ai/NativeSpriteRegistry.cs` | `FireTome1Ai.Register(registry)` in `CreateForProduction` (and the two doc comments that list the keys) |
| `Alundra.Tests/Ai/Items/FireTome1ProductionTests.cs` (new) | (p1) to (p4) |
| `Alundra.Tests/Ai/Items/FireTome1Run.cs` | `production: true` parameter (no seam and no registration by the bench); and, for the other runs, an EMPTY registry put in place of the runner's (see section 5, note 1) |
| five registry sums | `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`: `+ FireTome1Ai.Keys.Count` and `FireTome1Ai.Register(sum)` in the secondary registries (derived, not re-typed) |

(p1) Rond with the tome 47 alone, magic 4/4, no seam: the whole key of (w1) (`fire1_cast_pf`, every tick, every column), the column born at 34, eleven balls at 34 + 8k, the item freed at 149, the magic 3 throughout. (p2) the table of slots full (63 decoys): the key `fire1_table_full_pf` holds, nothing born, the item `0x2F` at 33 and freed at 34, the magic 3 throughout (lost). (p3) Rond interrupted at the tick 60 by the production path (R-EL-6 revised, the hero's columns after T compared): the key `fire1_hurt60_pf`, the effect `(19, 2, 6553600, 6553600, 1179650)` at the tick 60, the item freed, one column and four balls. (p4) `UnhandledWeaponSpriteCodes({20, 23}, catalogue, production registry)` empty; an empty registry lacks `(C, 63)` and `(E, 3)`.

## 2. Red before, green after

Tests commit `21fe4b68` against the HEAD without the phase B: **9 red of 168** in the filter (`red_before_phase_b.txt`) = (p1), (p2), (p3), (p4) (the unhandled codes are exactly `[(C, 63)]`) and the five sums. Code commit `473f7ea0`: the filter (`FireTome1|FireTome2|EarthTome|NativeSpriteRegistryTests|LiftableAiRegistrationTests|ChargedArrowAiTests|WaveSwordLifeTests|BallFlamesTests|HeroItemUse|Tome`, 517 tests) all green after the bench fix of section 5, note 1.
Moved tests: exactly the five sums predicted by T0 (`t0/predictions.md`, section (d)); no other existing test moved (the bench change of note 1 is a change of the helper, not of a test's expectation).

## 3. Proof of removal (real mutations by script `removal_proof.py`, undone, `git status` unchanged after each; outputs `removal_*.txt`)

| Mutation | Red | Plan |
|---|---|---|
| the three entries `0x2F` back to the stub (`entries_stub`) | (p1), (p2), (p3): 3 tests | exactly as the plan ((p4) green) |
| the cancellation entry alone back to the stub (`cancel_stub`) | (p3) alone | exactly as the plan |
| the call `FireTome1Ai.Register(registry)` removed (`noreg`) | (p1), (p3), (p4) and the five sums: 8 tests | exactly as the plan ((p2) and (e2) green) |
| `Cancel` neutralised (`cancel_neutral`) | (c1) C1, (c2) the six tests C2, (c3) C3, (d1) D1, (p3), (w6) for T in 35, 60, 100, 121, 122: 14 tests | exactly as the plan; (w6) for T in 10, 33, 34, 124, 140 and (w8) stay green |
| `Drive` neutralised (`drive_neutral`) | (d1), (d2) (four tests), (c3), (w1) (two), (w2) (eight), (w3) (two), (w3c) (two), (w4), (w5), (w6) (ten), (w7) (two), (w8) (three), (w9), (w10), (w11), (p1), (p2), (p3): 43 tests | as the plan; (t1), (c1), (c2), (e1), (e2), (k1), (k2), (k4), (k5) and (p4) stay green |
| `BallTick` (the four-argument one) neutralised (`balltick_neutral`) | (k1), (k2), (k5), (w1) to (w5) (w3c included), (w6) for T >= 60, (w8) (the case of 1 tick a frame), (w9) to (w11), (p1), (p3): all planned. Superset, by definition of the tests: the hand tests of C63 (`C63_*`), (k4) (two: the default call uses the settings; the registered ball computes the count), (e2) (the registered handler is the ball's tick): 58 tests | the plan names no test for these; (d1), (c1), (w6) for T <= 35, (w7), (p2) stay green as the plan says |
| `EarthTome1Ai.SpriteTypes` back to `{16}` (`types16`) | (e2) of h4c1 and of h4d2 (two), (p1), (p3), (p4), (w1) (two), (w2) (eight), (w3) (two), (w3c) (two), (w4), (w5), (w6) for T in 35, 60, 100, 121, 122, 124, 140, (w8) (1 tick a frame), (w9) to (w11): 33 tests | as the plan: every world test where a ball reaches the status 3 except (w6) for T in 10, 33, 34 and (w7); (p2), the (k) and the (d) stay green |

The (w8) cases at 2 and 3 ticks a frame stay green under `BallTick` and `types16`: T1 bis wrote them as bounds on the births, the end of the column, the release and the number of draws (note 5 of `t1bis/results.md`), which the ball's tick does not change.

## 4. Suites (all green)

Seven disjoint partitions by namespace prefix (`part.sh`), `--blame-hang-timeout 60s`, `--no-build`; Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug (built last, copies the DLL). Details in `suite.summary`: total **7930** = 7926 (T1 bis) + 4 (P1 to P4), 0 failure in Release and in Debug (partition 3: 507 to 511). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).

## 5. Notes

1. A flaw of the bench, found by the first green run of the phase B: `FireTome1Run` in its binary mode registered its own `(C, 63)` handler (settings of the binary) only if the key was absent from the runner's registry; the runner's registry IS the production one, which now holds `(C, 63)`, so the binary-mode handler was no longer registered and (w3) failed (two cases), and (w2) in the binary mode would have run silently at the production settings (D-H4D2-7 says those four scenarios give the same rows at both settings; (w3) is where they differ). Fix: the non-production runs put an empty `NativeSpriteRegistry` in the runner's backing field (reflection, as the bench already does for the sound player), so that they register exactly what they did before the phase B. With it (w2) and (w3) in the binary mode compare again at the settings of the binary; the production runs ((p1) to (p4)) use the registry of production alone. This is a change of the helper, not of a test: it is in the code commit.
2. (p2): the key `fire1_table_full_pf` is compared in full (the decoys are the blocks of the sprite 25 of the bench, as in (w7)); the plan's claims (the magic lost, the item freed at the tick 34, nothing born) are asserted next to it.
3. (p4) also asserts on an empty registry that `(C, 63)` and `(E, 3)` are both listed; the plan's `[(C, 63)]` before the phase B is the production registry without the call, which the red run shows.
