# E20.g2c1 - T2 (phase B): the world tests of C4 and the line of the registration (2026-10-25)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c1" (revision 2, READY), task T2. Bench: `Alundra.Tests/Ai/Objects/C4Run.cs` (a copy of `BreakRun`, no throwaway, nothing registered by the bench), tests:
`Alundra.Tests/Ai/Objects/C4WorldTests.cs` (109 cases), `C4RegistrationTests.cs` (2). Commits: the tests `fb6b885d`, then the code (this file is in the second one).

## The line of the phase B

- `NativeSpriteRegistry.CreateForProduction`: `LiftableAi.RegisterC4Production(registry)`: `(C, 4)` for the 19 types (`LiftableAi.C4ProductionTypes`: the 7 of `C4SpriteTypes` 290, 293, 363, 367, 368, 369, 373, the ice block 304 and the 11 shop articles 48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113).
- `LiftableAi.FeedsGroundSlope(type)` (the type 373 only), read by the hook of f6a in `EntityScriptProxy` (`HasLogicalZ || FeedsGroundSlope(SpriteType)`).
- The corner reader (`CornerTileWords`), the effect 9 and the sound 0x18 were already in `C4Tick` (phase A); the line only registers them.
- Two comments corrected (the plan lists them in the perimeter): the barrel/stone inversion in `LiftableAi.BreakSpriteTypes`, the inverted wording of `EntityFlags.Gravity`.

## Red before the line / green after (Release, the neutralised copy)

`C4WorldTests` run on the tree without the line (`NativeSpriteRegistry.cs` and the hook stashed): 109 cases, **63 red, 46 green**; with the line: **109 green**.

| Test | Cases | Before | After |
|---|---|---|---|
| M1 flat throw (7 types x moving/still) | 14 | red | green |
| M2 wall column 5, the rebound (7 x moving/still) | 14 | red | green |
| M2 wall column 6, no rebound | 7 | red | green |
| M3 glide on the band 0x1001 | 7 | red | green |
| M4 deep water (6 destroyed at T+23, the trunk floats) | 7 | red | green |
| M5 touch (barrel 22, stone 24, trunk 70) | 3 | red | green |
| M5 the star object touches nothing | 1 | red | green |
| M6 the object on the plate; the second object on the first | 2 | red | green |
| M7 the floating trunks (map 70: 2 arrivals, map 73: 3 trunks) | 5 | red | green |
| M9 the ice block of the map 103 | 1 | red | green |
| M10 the herb on the counter of Inoa: the dialogue of the price; thrown short: none | 2 | red | green |
| M8 witness: the stone of the map 444 on the trap door that goes down | 1 | green | green |
| M11 witness: the 117 native placements at rest, 6 ticks (44 maps) + the table pin | 45 | green | green |

## Deviations from the plan

- **(m11) count: 117, not 135.** The census of 2026-10-22 counted the 135 placements of the 7 types whose native C is C4 at the load (header C = 4, no event program C). The map-load pass (`EntitySpawnFactory.ShouldSpawnRecord`) only
  spawns the records whose `SpriteDirection` has the bit 0x40 set (the others are born by a script, `0x2D`): 18 of the 135 are not born at the load (direction 128 or 131: for example the barrel of the map 28 record 6, the stone of the map 473 record 0, the trunk of the map 70 record 15).
  The test pins the 117 that are born, per map, with their zone (table `NativePlacements`; 8 of them are the floating trunks of (m7), compared apart). No torch (the types are the 7 only). The 18 other placements are born by script and are not at rest at a load.
- (m5): the touch is measured against a hittable sprite of the page of each object (the barrel: sprite 49 of the map 22, the trunk: sprite 118 of the map 70), as T0 did; the Beannoid only for the stone (map 24). The times are the T0 probe P3's of the DLL.
- (m6): the Z of a thing resting on a plate or an object is one raw unit (1/65536 px) above the binary's: the comparison rounds to the millipixel.
- (m7) is read at "the first run of C4" (the frame 2 of a map-loaded entity) as the T0 disposition says; the map 73 trunks tested are the ones that keep C4 (396/728, 396/792, 996/712); the others of the map carry a script animation.
- (m10): the purchase path is run up to the dialogue of the price (program B[2] of the map 258: the herb at rest on a height-2 cell of the counter box, above ground); the payment (`0x9A`, `0x82`, `0x99`) is not exercised here.

## Displaced existing tests (predicted list of T0 (h): 7)

Displaced now: **4** (the four registry pins, by derivation: `+ LiftableAi.C4Keys.Count`, and `LiftableAi.RegisterC4Production(sum)` in the sum-based ones):
`NativeSpriteRegistryTests.ProductionRunner_...`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`. The three others of the list (`LiftPureTests.TheProductionSet_...`, `LiftWorldTests.M5`, `LiftMap72ArcTests.M5`) move with the wired set (phase C).
No test outside the predicted list moved.

## Suites (Release, `-p:CustomAfterMicrosoftCommonTargets=...skipcopy.targets`, `--blame-hang-timeout 60s`, partitions by complete namespace with the final dot, the third the exact negation)

| Partition | Tests | Red |
|---|---|---|
| P1 (`Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.`) | 1485 | 0 |
| P2 (`Entities. Events. ExportGuards. Hero. Items.`) | 2956 | 0 |
| P3 (the negation) | 1850 | 0 |

The suites rewrote the line ends of `docs/hero-trace-389-*.txt`; `git diff --ignore-cr-at-eol --quiet -- docs/` is clean, the files were restored. `python -B tools/dll-structure/check_names.py --rules`: 0 finding.
