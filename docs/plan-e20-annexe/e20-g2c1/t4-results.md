# E20.g2c1 - T4: the mutants and the ADR (2026-10-25)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c1" (revision 2, READY), task T4. Tools: `t4/mut.py` (25 real mutations, each pattern asserted to match exactly once, applied to the working tree), `t4/run.sh` (applies one, builds Release with the neutralised copy, runs the filter, undoes by `git checkout -- Alundra/`). Outputs: `t4/out/*.red`, `*.summary`. Table: `t4/mutants-table.txt`.

## Result: 25 mutants, 25 killed (the 14 of the plan are 01 to 14, plus 15 "a wired type without (C, 4)")

Filter `c4` (the C4 tests, the golden tests, the shared release, `BarrelLift*`, the registry pins, `LiftPureTests`, `CornerTileWordsTests`); base: 188 green + the corner tests, 0 red.

| # | Mutant | Red | First killer |
|---|---|---|---|
| 01 | `ForceZ > 0` -> `>= 0` | 4 | C4WorldTests M2 (wall, column 6, no rebound) |
| 02 | the lock not set | 15 | C4WorldTests M2 (column 5) |
| 03 | direction `+0x10` -> `+0x08` | 15 | C4WorldTests M2 (column 5) |
| 04 | effect Z 8 px -> 16 px | 15 | C4WorldTests M2 (column 5) |
| 05 | mask `0x1001` -> `0x1000` | 2 | LiftableC4GoldenTests (Free_AnimationIsReset...) |
| 06 | 3 corners instead of 4 | 3 | CornerTileWordsTests (the four corners; the band under one corner only) |
| 07 | `HitCounter` ignored | 1 | LiftableC4GoldenTests (the sound on the landing or the touch) |
| 08 | sound `0x18` -> `0x19` | 23 | C4WorldTests M1 |
| 09 | slope 4 -> 1 | 9 | C4WorldTests M4 (the deep water, the trunk floats) |
| 10 | float animation 5 -> 4 | 10 | C4WorldTests M4 |
| 11 | `+0x80 == 2` test removed | 4 | LiftableC4GoldenTests (Carried_Released) |
| 12 | `\| 0x10` -> `\| 0x30` | 2 | LiftableC4GoldenTests (Carried_Released) |
| 13 | C23 takes the C4 policy | 4 | LiftableSharedReleaseTests (C23 writes its flags whatever the native) |
| 14 | `FeedsGroundSlope` true for every entity | 1 | C4RegistrationTests (OnlyTheTrunk_FeedsItsGroundSlope) |
| 15 | a wired type (369) without the key (C, 4) | 8 | C4RegistrationTests (the registry holds C4 for the 19 types and no other) |
| 16 | `AnyHasMask`: `!= 0` instead of `== Mask` | 2 | LiftableC4GoldenTests (Free_AnimationIsReset...) |
| 17 | the trunk float exemption removed | 2 | LiftableC4GoldenTests (Free_AnimationIsReset...) |
| 18 | the status 3 not set | 1 | LiftableC4GoldenTests (Free_BlockedOnTheGroundOnABand_TheStatus3) |
| 19 | the carried half takes the C23 policy | 51 | C4WorldTests M1 (and 50 more) |
| 20 | the lock not cleared on the ground | 1 | LiftableC4GoldenTests (Free_TheSound24..._TheLockIsCleared) |
| 21 | the animation 0 not written when blocked on the ground | 9 | C4WorldTests M2 (column 6) |
| 22 | the glide ignored (animation 0 always) | 8 | C4WorldTests M3 (the band glide) |
| 23 | the trunk never leaves the float | 2 | LiftableC4GoldenTests (Free_AnimationIsReset...) |
| 24 | `FeedsGroundSlope` for the stone instead of the trunk | 6 | C4RegistrationTests (OnlyTheTrunk_FeedsItsGroundSlope) |
| 25 | effect 9 -> 8 | 14 | C4WorldTests M2 (column 5) |

Note on 06: the first run used a filter that left out `Alundra.Tests.Physics` (the reader's own tests live there) and reported 0 red; with the filter widened to `CornerTileWordsTests` the mutant is killed by 3 tests. No survivor, no equivalent mutant claimed. The 24 others ran with the filter before that widening (they do not touch the corner reader, except 05 and 16, which the golden tests kill through the injected corner words). Mutants 05 and 16 were killed by the goldens; their effect on the reader itself is exercised by `CornerTileWordsTests`, which the filter now also holds.

`git diff -- Alundra Alundra.Tests` is empty after the runs.

## The ADR

- ADR-0092 (new; the next free number counted over every branch: the highest on any branch was 0091), index updated.
- ADR-0089 amended: its status line reads "amended by ADR-0092" (the wired set of seven types is now 25), and the index row too.
