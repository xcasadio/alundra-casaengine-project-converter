# E20.h4c1 T2 (phase B and production tests) - results (annex)

Run at the start HEAD `6db0e710` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.31.3 (revision 2, READY), T2; D-H4C1-1 to D-H4C1-3. Two commits: the tests (red) then the code. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `data-extracted/`, `tools/` was changed.

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Hero/PlayerManager.cs` | `EarthTome1Item = 0x2B` and the three entries: start = `TomeStart` (h4a), driver = `EarthTome1Ai.Driver`, cancellation = `EarthTome1Ai.Cancel` (the named no-op); three branches of the existing ternaries/`if` chain (the "tables" of h0a are branches, T0 section 4) |
| `Alundra/Ai/NativeSpriteRegistry.cs` | `EarthTome1Ai.Register(registry)` in `CreateForProduction` |
| `Alundra.Tests/Ai/Items/EarthTome1ProductionTests.cs` (new) | (p1) to (p4) |
| `Alundra.Tests/Ai/Items/EarthTome1Run.cs` | `production: true` parameter: no seam and no registration by the bench; `EarthTome1WorldTests.Everything`, `Lives`, `SoundTicks` made `internal` for (p1) |
| five registry sums | `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`: `+ EarthTome1Ai.Keys.Count` and `EarthTome1Ai.Register(sum)` in the secondary registries (derived, not re-typed) |

## 2. Red before, green after

Tests commit against the HEAD without the phase B: **9 red** = (p1), (p2), (p3), (p4) and the five sums. After the code: the 27 tests of the filter `EarthTome1|NativeSpriteRegistryTests|LiftableAiRegistrationTests|ChargedArrowAiTests|WaveSwordLifeTests|BallFlamesTests|HeroItemUseTests|Tome` (336 tests) all green.
Moved tests: exactly the five predicted (T0 `results.md`); no other test moved.

- (p1) red before: the Circle starts nothing (the stub), no rock; (p2) red before: the object is never in use (the stub answers 1); (p3) red before at the sound 46 (the rocks reach the status 3 and 4 by the old rule, E3 is absent); (p4) red before: `[(E, 3)]`.
- (p3) deviation from the plan text: the four rocks of the map 388 (records 18 to 21, `SpriteDirection` 0, bit `0x40` clear) are NOT made by the map load (`EntitySpawnFactory.ShouldSpawnRecord` skips them: the load of the map 388 gives no sprite 16); the test makes them by the opcode `0x2D` path (`SpawnEntityByRecordId`, no zone check), as a script would. They are not marked `HasLogicalZ`; the status 3 then 4 and the four sounds 46 are checked, no exact tick (D-E20-37). Whether any script of the story activates them is not established here (the same INFERENCE as Q-H4C1-2: outside the story).
- (p2) fills the table of slots at every tick from 2 to 97 with the inert filler type 5.

## 3. Proof of removal (real mutations by script `removal_proof.py`, undone, `git diff` empty after each)

| Mutation | Red (exactly) |
|---|---|
| the call `EarthTome1Ai.Register(registry)` removed | (p1), (p3), (p4) and the five sums: 8 tests, as the plan |
| the driver entry back to the stub | (p1), (p2): as the plan |
| the start entry back to the stub | (p1), (p2) (the start is the first link) |
| the body of E3 neutralised | (e1) x2, (e2), (w1) x2, (w2) to (w6), (p1), (p3): 31 cases, 14 tests; (c1) stays green. Superset of the plan list: (e2) (E3 registered, called) and the second (e1) (no host) also go red |
| the cancellation entry back to the stub | none: the cancellation is the neutral no-op in both, so this mutation is equivalent (as (c1) says) |

## 4. Suites (all green)

Disjoint partitions by namespace prefix, `--blame-hang-timeout 60s`, `--no-build`; Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug (built last, copies the DLL). The two `Ai.` partitions other than `Ai.Objects.` are run in one call (`Ai.Projectiles.` + the rest of `Ai.`).

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 634 | 0 | 0 |
| `Ai.Projectiles.` + `Ai.` apart from `Ai.Objects.` | 649 | 0 | 0 |
| `Hero.Hero*` | 1331 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2236 | 0 | 0 |
| everything else | 2206 | 0 | 0 |
| total | 7675 | 0 | 0 |

7675 = 7671 (T1) + 4 new tests. The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).
