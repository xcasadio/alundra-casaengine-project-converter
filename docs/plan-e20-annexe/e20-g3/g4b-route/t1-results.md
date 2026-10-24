# E20.g4b - T1 results (the arcs (b1) to (b6))

Plan: `docs/plan-e20-heros.md` section 1.29.2 (revision 2, READY). Start HEAD of this pass: `2a8b60ce` (P0 and T0 done). Branch `chantier/theme1-porter-plateforme`. The values pinned are those of `README.md` (T0) and `dll-probes/*.txt`, written by hand into the tests; the ones read with four decimals at T0 are compared to 6e-5.

## What was added

`Alundra.Tests/Arcs/ShipBarricadeArcTests.cs` (the g4a file; its pilot `Pilot` gained `Place`, `Rec`, `Touches`, `Carried`, `InPoll`, `RollTicks` and a `Step(directions, button, obey)`; the g4a arcs are unchanged and still green) gets 21 tests, each a Theory over k = 1, 2, 3 ticks per image, under B1 with the 12 records:

| Test | What it pins |
|---|---|
| `B1_TheLiftRoute_...` | the route from the arrival of B1 to the portal 0 (departure to the 391 at the images 478, 252, 174; ticks 477, 503, 521): the 7 phases with image and place, rec5 carried **from the first tick of the Square image** (the lift images 314, 163, 113), the carry stand 36 ticks later, the throw images 369, 192, 133 (hands free in the very tick), rec5 broken 24, 24, 25 ticks after the throw, no roll, the polls predicted = the real ones |
| `B2_FromTheFaceOfRecSix_...` | rec6 carried (Square image 211, 106, 71 = 7 ticks after the pose), the hero flush at (657, 316), rec10 on the ground (z 64) from L + 7 |
| `QG3c_TheCrateAboveALiftedCrate_FallsWithoutTheHalfPixelOfTheFirstTick_TheGapToTheBinary` | **the gap Q-G3-c (A), named and pinned**: rec10 z at L-1..L+9 on the DLL = 80, 80, 78.5, 77, 75, 72.5, 69.5, 66, 64, 64, 64 (the binary: 79.5 at L); the only differing tick is L; rec6 rises 1 px per tick; same series at the three cadences |
| `B3_OnTopOfRecFive_...` | from (636, 296, 80): y = 297.0, rec10 carried (Square images 207, 104, 70) |
| `B4_InTheAir_...` | the lift in the air of g1b (m7): Cross at the first non-poll image whose first tick is >= 19 (images 223, 112, 75), Square in the air at the first non-poll image start with the contact rec10 (images 233, 120, 80; first ticks 29, 35, 34; z 91.5, 84.0, 86.5), rec10 carried |
| `B5_CaseC_...` | y = 295.0, contact rec5 (image 217, 109, 73 at 3 ticks: the Square comes at 75 after a poll image), rec5 carried |
| `B6_TheRollWhileCarrying_...` | the roll at the tick 268 at the three cadences; rec5 stays in the hands (z = 96, hero z 64) at the 13 ticks roll..roll+12; the path of the hero pinned (k = 1, 2: held still 5 ticks in state 12 then walking west; k = 3: the random draw moves him at once to y = 297.0); `Rolls == 1` |

`Alundra.Tests/Support/ArcRun.cs`: the only edit is `ArcsThatSlide`: `A5l-b1-k1..3` = 11, 13, 12; `A5l-b5-k1..3` = 1, 1, 5; `A5l-b6-k1..3` = 1, 1, 5 (T0 (d); b2, b3, b4 and the QG3c test do not slide and are absent = 0). The guard of `Dispose` checks them.

No code of the DLL touched in this pass (P0's preset is the only DLL line since `4c219541`). The engine, the converter and the data have no diff since `4c219541`.

## No stop reached

No scenario left its issue; no roll outside (b6); (b6): the object neither went down nor was lost (13 ticks read); nothing of the engine or the DLL touched.

## Verification

- `ShipBarricadeArcTests`: 30 tests (9 of g4a + 21 new), green, Release (neutralised copy).
- Mutants applied by script on the test file and undone (`git diff` empty of them): (1) the Cross tick of (b4) at 12 instead of 19: the three `B4` tests red (this is the T0 finding that the plan's tj = 12 does not open the gate under the polls); (2) the pilot ignoring the poll rule: `B1` x3, `B4` x3, `B5` k = 3 and `B6` k = 3 red (8 of 18).
- Full `Alundra.Tests`, three disjoint partitions by complete namespace with the final dot, the third the exact negation of the other two (run.sh of g1b): Release (neutralised copy) 1349 + 2956 + 1839 = 6144 tests, zero failure; then Debug (last) 1349 + 2956 + 1839 = 6144, zero failure.
- `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical. `docs/hero-trace-389-*.txt`: only line-ending rewrites (`git diff --ignore-cr-at-eol --quiet -- docs/` clean), restored by `git checkout`.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 existing warning, `TextBoxGpu.cs`).
- Existing tests moved: only the `ArcsThatSlide` table (as predicted by the plan). The tables of the preset of P0 were already moved by P0.

## Flagged

- The Cross tick of (b4) is 19 (T0, `pilot-rules.md` rule 5), not the plan's 12: the test pins 19 and carries the reason.
- At k = 3 rec5 is destroyed 25 ticks after the throw (24 elsewhere and in the binary): pinned as measured, still not investigated (T0 flag).
- O-G3-3 / D-E20-77 (the DLL posts the contact one tick earlier than the binary): (b3) checks the carried crate and the Square image after the pose, not the tick of the contact against the binary's tick 4 (T0: the Square image is one tick earlier at k = 1).
