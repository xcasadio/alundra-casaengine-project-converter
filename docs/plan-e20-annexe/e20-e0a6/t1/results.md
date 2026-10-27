# E20.e0a6 T1 - results

Plan: `docs/plan-e20-heros.md` section 1.16.6, D-E0A6-1 and D-E0A6-2. Starting HEAD of the pass: `ef0c28dc` (T0 done, `../t0/results.md`). Branch `chantier/theme2-attaques-objets`.

## Tests first (commit "test(e20-e0a6)")

- (s1) `SpawnBySpriteIndexInterfaceTests.S1_AnEntityBornAtAFractionalPosition_KeepsItAtTheRoot_AndInItsLogicalPositionAtTheNextTick`: a rock (index 17) born by the interface at x = 143.5 px, y = 101.25 px (committed values) keeps the 16.16 position at birth (already true) and at the root (red: root (143, 101)).
- (s2) `WorldProxySpawnSubPixelTests.S2_TheRocksOfTheEarthTome2_BornByTheInterface_AreWhereTheKeyOfTheBinaryPutsThem_AtTheTickOfTheirBirthAndTheNext` (new file `Alundra.Tests/Maps/WorldProxySpawnSubPixelTests.cs`): the probe bench of h4c2 (phase 0 births of the driver only, no E13, no cancellation; hero at (600, 480), seed 0x12345678) against the first two births of `critic/keys/cast_2c_pf_mid_x600y480.json` (slots 1 and 2, ticks 18 and 34) at the birth tick and the next. Red: slot 1, tick 18, key (37767104, 30376576), DLL (37731264, 30374528) = floor(spawn) + force, as T0 of h4c2 found.

## The code (commit "fix(e20-e0a6)")

- `Alundra/Maps/WorldProxy.cs`, `SpawnEntityBySpriteIndex`: `PushLogicalPositionToRoot(keepSubPixel: true)` (one argument; the two other call sites are untouched).
- `LiftableBreakWorldTests.B5`: the expectation by derivation, `(broken.X, broken.Y) == (drop.PosX, drop.PosY)` (the 16.16 position of the crate, T0 section 2 of the binary).
- Deviation: (s1) first used (143.5, 101.25) near the hero (150, 100): after one frame the rock is pushed by the hero (Y 100.25 -> 99.0 in a first try, X unchanged), unrelated to the birth. The position of (s1) was moved to (230.5, 160.25), away from the hero; this edit is in the code commit, not in the test commit (the test commit still holds 143.5 / 101.25 and would stay red with the code at its final state for that reason only).

## Red before / green after (Release, copy neutralised)

Mutant "`keepSubPixel: false`" (applied by `sed`, then undone; final `git diff` shows only the real change): of the 32 tests of the filter `WorldProxySpawnSubPixelTests | S1_AnEntityBorn | LiftableBreakWorldTests`, 5 red: S1, S2, B5 (ticksPerFrame 1, 2, 3); with the argument at `true`: 32 of 32 green.

## Entire suite, Release (`suite/*.summary`, `.red` empty), partitions disjoint

| Part | Namespaces | Tests | Failures |
|---|---|---|---|
| A | `Ai.Projectiles` | 332 | 0 |
| B1 | `Ai.Objects` | 635 | 0 |
| B2 | other `Ai`, `Arcs`, `Audio`, `Cameras`, `Cells`, `Combat`, `Dialogues` | 1 376 | 0 |
| C | `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 3 341 (+1: S1) | 0 |
| D | the rest | 2 037 (+1: S2) | 0 |

Total 7 721 = 7 719 + 2 new tests, 0 failure. Tests moved: B5 alone (its three rows), as predicted. The Debug suite, `cmp`, the ADR-0103 amendment belong to T2.
The traces `docs/hero-trace-389-*.txt` were rewritten with CR at line ends and restored by `git checkout`.
