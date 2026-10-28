# E20.h4c2 T3 (mutants, suites, ADR) - results (annex)

Run at the start HEAD `1ecc35b8` (branch `chantier/theme2-attaques-objets`, 2026-10-27 and 28), after T2. Plan: `docs/plan-e20-heros.md` section 1.31.4 (revision 3.2, READY), T3. The two fresh verifiers of T3 are the orchestrator's, not run here. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `data-extracted/`, `tools/` was changed; `alundra-project/` only received the Debug DLL deposit.

## 1. Method

`t3/mutants.py` applies a mutant to the source by text replacement (the pattern must match exactly once), runs the targeted suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, `--blame-hang-timeout 60s`), restores the file in a `finally` and checks `git diff --quiet` on the six mutable files (`git_diff_empty=True` after every run). The mutations are real (never simulated in a test). The suite of each run: filter `EarthTome | CameraKick | NativeSpriteRegistryTests | LiftableAiRegistrationTests | ChargedArrowAiTests | WaveSwordLifeTests | BallFlamesTests | HeroItemUseTests | Tome` (401 tests, 402 after the survivor's test K2), about 93 s a run. Baseline without mutation: 401 green (402 with the new test). `t3/run.log` is the raw log (the console encoding garbles the accents of the xUnit summary line; the counts are right). Only the first five red names of each run are printed, so the "red" column below lists a prefix of the red cases, not all of them.

## 2. The 46 mutants (plus the declared equivalent and one that did not compile)

| Mutant | Plan item | Result | Red cases | First red tests |
|---|---|---|---|---|
| m01 `r1` and `r2` draws swapped | r1 and r2 swapped | KILLED | 11 | T2, P1, S10b, W1, W2, W5, W6 |
| m02 direction taken on `r1` | direction on r1 | KILLED | 11 | T2, P1, S10b, W1, W2.. |
| m03 `p + 0xA00` to `p` | `p + 0xA00` | KILLED | 11 | T2, P1, S10b, W1, W2.. |
| m04 `X32` and `Y32` swapped | X32/Y32 | KILLED | 11 | T2, P1, S10b, W1, W2.. |
| m05 birth height 128 px | Z + 128 px | KILLED | 18 | T2, T4, P1, P4, S10b, W1, W2 |
| m06 fall force -4 px | `ForceZ` -4 px | KILLED | 20 | T2, T3, P1, P4, S10b |
| m07 destruction comparison `<=` | `<=` | KILLED | 1 | T4 |
| m08 terrain without the `+ 1` | terrain without +1 | KILLED | 1 | T4 |
| m09 a null birth destroys (the hero) | null birth that destroys | KILLED | 2 | T3, P2 |
| m10 entry 224 moved to 208 | entry 224 shifted | KILLED | 10 | T1, T2, T3, T5, P1.. |
| m11 one draw only (`r2 = r1`) | one draw | KILLED | 12 | T2, T3, P1, S10b |
| m12 guard of the phase 1 removed | guard absent in phase 1 | KILLED | 3 | T2, T6 |
| m13 threshold `0x119` | threshold `0x119` | KILLED | 7 | T2, T5, P1, P2, W1 |
| m14 phase 2 answers 1 at once | phase 2 answers 1 | KILLED | 8 | T2, T5, P1, P2 |
| m15 phase 1 never reached (`index > EntryCount`) | phase 1 not chained | KILLED | 7 | T2, T5, P1, P2, W1 |
| m16 E13 without the kick | E13 without kick | KILLED | 17 | E1, E2, E6, E7, P1.. |
| m17 kick added (`+=`) | kick added | KILLED | 9 | P1, W1, W2, W3, W3b.. |
| m18 E13 without `\| 0x40` | without `0x40` | KILLED | 14 | E1, E2, E5, E7, P1.. |
| m19 E13 without clearing `0x30` | `0x30` not cleared | KILLED | 14 | E1, E2, E5, E7, P1.. |
| m20 `DestroyEntity(e, 0)` instead of `-1` | destruction without the break effect (`+0xAC`) | KILLED | 2 | E1, E3 |
| m20b single-argument `DestroyEntity(e)` | (extra) | BUILD ERROR, dropped | | the script host exposes the two-argument overload only |
| m21 a cancelled rock (animation 1) kicks at its first view (early return removed) | kick at first view of a cancelled rock | KILLED | 17 | E1, E3, E4, P1, P4.. |
| m22 cancellation of every type | cancel all types | KILLED | 6 | C1, T2, P4, W4 |
| m23 cancellation touches the status 3 | touches the status 3 | KILLED | 2 | C1, T2 |
| m24 cancellation forgets the highest slot | forgets the highest slot | KILLED | 5 | C1, T2, P4, W4 |
| m25 kick of inverted sign | inverted sign | KILLED | 20 | K1 (x20) |
| m26 kick applied at the snap tick | applied at the snap tick | KILLED | 1 | K1 (snap frame) |
| m27 kick filed by image (index 0) | filed by image | KILLED | 3 | W3, W3b, K2 |
| m28 kick consumed one tick late | consumed one tick late | KILLED | 20 | K1 (x20) |
| m29 index shift at the snap tick | index shift at the snap tick | KILLED | 1 | K1 |
| m30 `(E, 13)` also for 469 | `(E, 13)` for 469 | KILLED | 2 | R1, P3 |
| m31 rule D-F5-5 removed (`ScriptedMotion.KeyAnimationOf`) | D-F5-5 removed | KILLED | 33 | P1, W1, W2, W3.. |
| m32 speed target forgotten for the type 17 | target forgotten at the tick of E13 | KILLED | 8 | P1, S10b, W1, W2, W5 |
| m33 E13 without the ground guard | no ground guard | KILLED | 9 | E6, S10b, W2, W3b, W4 (as the plan: (e6), (w2), (w8) and (w4)) |
| m34 guard read on `ForceAdjusted` | `ForceAdjusted` instead of `IsOnGround` | KILLED | 11 | E6, E7, S10b, W2 (as the plan: (e7), (w2)) |
| m35 status left at 3 in the air | status left at 3 | KILLED | 8 | E6, W2, W3b, W4, W6 (as the plan: (e6), (w4)) |
| m36 guard applied to the second branch too | guard on the second branch | KILLED | 2 | E3, R1 (as the plan: (e3)) |
| m37 kick value 3 | (extra) | KILLED | 17 | E1, E2, E6, E7, P1.. |
| m38 phase 1 never enters phase 2 | (extra) | KILLED | 7 | T2, T5, P1, P2, W1 |
| m39 cancellation keeps the blocker | (extra) | KILLED | 2 | C1, T2 |
| m40 cancellation without the animation 1 | (extra) | KILLED | 6 | C1, T2, P4, W4 |
| m41 kick list not emptied after the camera step | (extra) | KILLED | 19 | P1, P4, W1, W2, W3.. |
| m42 `_cameraKickTick = 0` removed after the entity block | (extra) | **SURVIVED at first**, then KILLED | 1 | K2 (the new test) |
| m43 the tick index not set at the head of the entity block | (extra) | KILLED | 3 | W3, W3b, K2 |
| m44 cancellation entry back to the stub | (extra) | KILLED | 1 | P4 |
| m45 driver entry back to the stub | (extra) | KILLED | 3 | P1, P2, P4 |
| m46 registration of E13 removed | (extra) | KILLED | 8 | P1, P3, P4, R1 (and the sums) |
| e01 declared equivalent: `ResyncControllerFromFlags()` removed from E13 | equivalent | SURVIVED (402 green) | 0 | proof below |

46 real mutants killed (the 36 of the plan's list that apply to the code, with the four of revision 3, plus ten extras), one more dropped for a build error, one declared equivalent. The plan asked for at least 32.

## 3. The survivor and the equivalent

- **m42** (the reset `_cameraKickTick = 0` removed after the entity block): the world tests drove either a frame whose kicks were written inside the entity block or a headless world, so the stale index of the last tick was never seen by a kick filed outside the block after a driven frame of several ticks. Fix: `K2_AfterADrivenFrameOfThreeTicks_AKickFiledOutsideTheEntityBlock_GoesToTheIndex0_NotToTheLastTickOfTheFrame` (`Alundra.Tests/Maps/WorldProxyCameraKickTests.cs`, commit `56ee4d6b`): a driven arena, one frame of three ticks, then `SetCameraKick(2)` must file at the index 0. Killed, 1 red. Green on the real code.
- **e01** (the declared equivalent, `entity.ResyncControllerFromFlags()` in E13): equivalent by reading. `ResyncControllerFromFlags` does nothing without a controller; with one it re-applies `ApplyGravitySettingsToController` (constant: `Gravity = 0`, `MaxFallSpeed = 0`, whatever the flags) and recomputes `Settings.WalkabilityMask` from `CellsCollisionField.WalkabilityMaskFor(Flags)`, which reads the bits `ClassA` (0x1) and `ClassB` (0x8) alone, keeping the deep-water pseudo-bit. E13 only sets `0x40` and clears `0x30`: neither is a class bit, so the recomputed mask equals the one the controller already holds (every other writer of the flags resyncs itself). The mutant survives all 402 tests, as the plan expects.

## 4. Suites (all green)

Seven disjoint partitions by namespace prefix (runner: scratchpad `h4c2-part.sh`), `--blame-hang-timeout 60s`, `--no-build`; Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug (built last, copies the DLL).

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 635 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 369 | 0 | 0 |
| `Hero.Hero*` | 1331 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2268 | 0 | 0 |
| everything else | 2231 | 0 | 0 |
| total | **7785** | 0 | 0 |

7785 = 7784 (T2) + 1 (K2 reset test). `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet` clean, restored by `git checkout`, never indexed. The code of the DLL is unchanged by T3 (one test added), so the oracle replays of T0 and the traces are untouched.

## 5. ADR

`docs/decisions/0109-...` (ADR-0109, the next free number counted on every branch: the highest was 0108), index updated. It writes the deviation D-E20-56 (the rock stopped in the air waits for the ground; the earth tome 1 and the fire balls keep the original, Q-H4C2-6 A) and the disposition D-H4C2-5 (the probe (s2) replayed after E20.e0a6; the 22 camera vectors that read the X kick are "binary only", O-H4C2-1).
