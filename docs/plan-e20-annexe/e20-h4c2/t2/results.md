# E20.h4c2 T2 (phase B and production tests) - results (annex)

Run at the start HEAD `c186ea28` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.31.4 (revision 3.2, READY), T2; D-H4C2-1 to D-H4C2-5. Two commits: the tests (red) then the code. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `data-extracted/`, `tools/` was changed; `alundra-project/` only received the Debug DLL deposit.

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Hero/PlayerManager.cs` | `EarthTome2Item = 0x2C` and the three entries: start = `TomeStart` (h4a, same branch as the tome 1), driver = `EarthTome2Ai.Driver`, cancellation = `EarthTome2Ai.Cancel` (three branches of the existing ternaries / `if` chain, as h4c1) |
| `Alundra/Ai/NativeSpriteRegistry.cs` | `EarthTome2Ai.Register(registry)` in `CreateForProduction` (and the two doc comments that list the keys) |
| `Alundra.Tests/Ai/Items/EarthTome2ProductionTests.cs` (new) | (p1) to (p4) |
| `Alundra.Tests/Ai/Items/EarthTome2Run.cs` | `production: true` parameter: no seam and no registration by the bench; `EarthTome2WorldTests.Mid`, `Everything`, `SoundTicks`, `KickTicks` made `internal` for (p1) |
| five registry sums | `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`: `+ EarthTome2Ai.Keys.Count` and `EarthTome2Ai.Register(sum)` in the secondary registries (derived, not re-typed) |

(p1) Rond with the tome 44 at (600, 480): the whole key of (w1) (every tick, every field), 15 births at the ticks of the key, the sounds 55 / 45, the item freed at 309. (p2) the table of slots filled from the tick 2 to 260 with the inert filler type 5: no rock at any tick, the item in use at 308, freed at 309. (p3) `UnhandledWeaponSpriteCodes({17})` empty on the production registry, `[(E, 13)]` on an empty one. (p4) `cancel_2c_pf_t100` through the entries (cancellation key, sound 45 per rock in the air, hero sound 23 at 100, item freed at 100).

## 2. Red before, green after

Tests commit `b89811f2` against the HEAD without the phase B: **9 red of 89** (`red_before_phase_b.txt`) = (p1), (p2), (p3), (p4) and the five sums. Code commit `5153fdca`: the filter `EarthTome|CameraKick|NativeSpriteRegistryTests|LiftableAiRegistrationTests|ChargedArrowAiTests|WaveSwordLifeTests|BallFlamesTests|HeroItemUseTests|Tome` (401 tests) all green.
Moved tests: exactly the five predicted by T0 (`t0/results.md`); no other test moved.

## 3. Proof of removal (real mutations by script `removal_proof.py`, undone, `git diff` empty after each)

| Mutation | Red | Plan |
|---|---|---|
| the call `EarthTome2Ai.Register(registry)` removed | (p1), (p3), (p4) and the five sums: 8 tests | as the plan (the "two pins" are now five) |
| the driver entry back to the stub | (p1), (p2), (p4) | as the plan |
| the start entry back to the stub | (p1), (p2), (p4) | (not in the plan; the start is the first link) |
| the cancellation entry back to the stub | (p4) alone | as the plan |
| the filing of the kick removed in `WorldProxy.SetCameraKick` | K2, K3 (x2), W1, W2, W3 (x2 rows), W3b, W4, W6 (water band), W8, P1, P4 | superset of the plan (k2), (k3), (w1), (w2), (w4), (w8), (p1), (p4): (w3), (w3b) and (w6) compare the kicks too |
| the call `SetCameraKick` removed from E13 (extra) | E1, E2, E6, E7, K3 (no driver), W1 to W4, W6, W8, P1, P4 | (extra) |
| the body of `EarthTome2Ai.Cancel` neutralised | C1, T2 (the 604 cases include the guard's cancellation), W4, P4 | plan: (c1), (w4), (p4); T2 is a superset |
| the ground guard of E13 neutralised | E6, W2, W3b, W4 (the T = 170 case), W8, plus S10b (edge rock) and W6 (water band) | plan: exactly (e6), (w2), (w3b), (w8), (w4) at T = 170; the two others are superset (they use the edge rock key); (e1), (e7), (w1), (p1), (p4) stay green, as the plan |

## 4. Suites (all green)

Seven disjoint partitions by namespace prefix, `--blame-hang-timeout 60s`, `--no-build`; Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug (built last, copies the DLL). Runner: scratchpad `part.sh`.

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 635 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 369 | 0 | 0 |
| `Hero.Hero*` | 1331 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2268 | 0 | 0 |
| everything else | 2230 | 0 | 0 |
| total | **7784** | 0 | 0 |

7784 = 7780 (T1) + 4 new tests (P1 to P4). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).
