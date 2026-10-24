# E20.g4a - T0 record (the barricade by the jump)

Plan: `docs/plan-e20-heros.md` section 1.29.1. Start HEAD of the execution: `cf56e063`. Branch `chantier/theme1-porter-plateforme`. The code of the DLL and of the engine is unchanged by T0.
Binary: `ALUN_CD.EXE` (France). Binary harness: `docs/plan-e20-annexe/e20-g3/critic/my392.py` (the 12 records spawned in file order by the real map-record spawner, the real `MovePlayer`) with the oracle machine of `docs/plan-e20-annexe/e20-g1a/work` as `g1a/` next to it (scratch copy of `critic/`; the scripts of this folder are copied into that `critic/` and run from there with `python -B -I <script>`; their outputs land in its `out/` and are copied here). `w3_door.py` replayed first from the copy: `out/w3_door.txt` identical (`cmp`) to the committed one.

The DLL probes are throw-away tests (`dll-probes/G4aT0ProbeTests.cs.txt`, a temporary class of `Alundra.Tests/Probes/`, removed after the runs, never committed); they write their lines to the file named by `G4A_OUT` (`dll-probes/*.txt`). World: the real map 392 (the 12 crates, the real prefabs, the real hero, B1 running), Release, the code of the DLL at the start HEAD. An **image** is one `OneFrame(k / 50f)` of k ticks (k = 1, 2, 3). The poll images of B1 are those of `pilot-rules.md`.

## Sites of the plan re-located by content (the plan dates from 2026-10-18)

| Plan | At the start HEAD |
|---|---|
| `AlundraShipCorridorArcTests.cs` (A5, A5r) | `Alundra.Tests/Arcs/ShipCorridorArcTests.cs` (class `ShipCorridorArcTests`; the doc of A5/A5r still says "crossing them is E20.g4" after g3) |
| `AlundraTestSavesTests.cs` | `Alundra.Tests/SaveGames/TestSavesTests.cs` |
| `AlundraArcSupport.cs`, `ArcsThatSlide` (`:342`) | `Alundra.Tests/Support/ArcRun.cs`: `ArcsThatSlide = new() { ["A10J"] = 1, ["A14"] = 2 }` (line 311); the guard is in `ArcRun.Dispose` (it throws when `TotalSlideCount` differs from the entry, 0 for an absent arc) |
| the file of the new arcs | `Alundra.Tests/Arcs/ShipBarricadeArcTests.cs`, class `ShipBarricadeArcTests` (no Alundra prefix, folder of the arcs) |

No difference of substance with the plan.

## (a) The cadence of B1 on the DLL (`dll-probes/a.txt`, probe `A_Cadence`)

No direction held, one `OneFrame(k/50f)` per image; the tick of each instruction is counted by the `pad` pass of the tick (`WorldProxy.TickTraceForTests`).

| k | ticks per image | `0x11 @104` | `0x2F @107` (image / tick) | images between two polls | roll |
|---|---|---|---|---|---|
| 1 | 1 (always) | image 204, tick 204 | 220/220, 236/236, 252/252, ... every 16 | C = 16 | none |
| 2 | 2 (always) | image 102, tick 204 | 110/220, 118/236, 126/252, ... every 8 | C = 8 | none |
| 3 | 3 (always) | image 68, tick 204 | 73/220, 78/236, 84/252, 89/268, 94/284, 100/300, 105/316, 110/332, 116/348 | C = 5 or 6 (5, 6, 5, 5, 6, 5, 5, 6) | none |

The wait `0x37 [15]` counts **ticks**: the polls are 16 ticks apart whatever k (at k = 1 the figures are those of A5r: 204, then 220, then every 16). The result of every poll is 0 (no direction held). The poll m of an arc is in the image `A + floor(16 m / k)` (A = the image of `0x11 @104`, which opens at the tick 204 for k = 1, 2, 3): **predicted = actual in every later probe run** (`PollCheck`, 2 121 to 2 273 polls per run). No roll, no irregular cadence: no stop.

## (b) The route of the binary with the 12 records (`g4a_route.py`, `out/g4a_route.txt`)

Binary harness, the 12 records (rec9 included), start (775.125, 216, z 64) = where the DLL's B1 leaves the hero (`50798592 / 65536`), a scripted player reading the machine before each tick (nothing written after the start), the transitions decided before the tick as in `pilot-rules.md`. The binary has no B1 (no poll, no roll). Phases and thresholds (first tick of the phase, state read):

| tick | phase | threshold that ended the previous one | hero at that tick |
|---|---|---|---|
| 1 | `down` (Down) | - | (775.125, 216) |
| 46 | `left` (Left) | `PosY >= 288.0` | (775.125, 288.312) |
| 95 | `after_jump` (Left + Cross at the first tick with `PosY` in [295, 297] and `PosX <= 687.5`) | the calage gave `PosY = 295.125` | (686.5, 295.125) |
| 132 | `south` (Down) | `PosX <= 600.0` (the hero is stopped by rec9 at 597.0 and slides to 598.3) | (599.477, 295.125, z 69.5) |
| 163 | `west` (Left) | `PosY >= 343.5` (the hero stops at 344.0) | (598.328, 344.0) |
| 188 | `down2` (Down) | `PosX <= 541.0` | (539.828, 344.0) |
| 203 | the hero is in the cell (22, 23) of the portal 0 | | |

The hero lands on rec5 at z 80 (18 ticks after the Cross), walks off its west edge, is **stopped by rec9 at x = 597.0** (so the 12 records matter: `a3_fullroute.py` started from a world without rec9), goes round it by the south (y 344.0, the row 21 of the map), then west to the mouth of the corridor of the portal (the plateau of height 5 of the columns 20 to 22 is not crossed). Free window of the mouth of the corridor (binary, `g4a_mouth.py`): a hero at x in [538.0 ; 541.0] goes down without a slide (`out/g4a_mouth_binary.txt`).

## (c) The rules of the pilot: `pilot-rules.md` (written before the arcs).

## (d) The slides (`out/slides_law.txt`, `dll-probes/r.txt`, `dll-probes/e.txt`, `dll-probes/m.txt`)

The slide count of an arc is the **calage** law: a hero entering the door from the east with Left held ends at `PosY = 295 + ((y0 - 295) mod 0.5)` through `(PosY - y0) / 0.5` second steps of half a pixel along the wall (`SlideCount`). Measured on the arcs as the pilot designs them (T1 (a1), (a2), (a3); probe `R_Route`; YD = 288 for a1 and a2):

| arc | k = 1 | k = 2 | k = 3 | other slides |
|---|---|---|---|---|
| a1 (calage, ends at the jump opportunity) | 10 (y0 290.3438 -> 295.3438) | 9 (290.75 -> 295.25) | 12 (289.125 -> 295.125) | 0 |
| a2 (the whole route to the portal) | 10 | 9 | 12 | 0 |
| a3 (cardinal entry at y > 297, jump refused; YD 300, 304, 320 measured) | 0 | 0 | 0 | 0 |

Every count equals the law at the `y0` the arc reaches (`out/slides_law.txt`: EQUAL for the 6 arcs with a calage); a3 has no calage (no slide). The law on 145 starts per cadence is in (e). **The mouth of the corridor** is the one other place of the route where a slide can happen (a hero at x < 538.0 or > 541.0 is aligned 0.75 px per tick): measured on the binary and on the DLL for 121 starts x 3 cadences, the DLL equals the binary (steps, x, tick) 121 of 121 at each k and `SlideCount` equals the number of steps (`out/mouth_compare.txt`); the pilot (west until `PosX <= 541.0`, then Down in the same image) enters at x = 539.4 / 539.7 / 539.7 and adds none. **To pin in `ArcsThatSlide` (T1)**: the a1 and a2 arcs of each cadence (names per cadence): a1 = 10, 9, 12 and a2 = 10, 9, 12 at k = 1, 2, 3; a3 absent (0). These values depend on `y0` at the door, hence on `YD = 288.0` and on the pilot; a change of a threshold re-measures them.

## (e) The calage on the DLL against the binary (`out/e_compare.txt`, `dll-probes/e.txt`, probe `E_Calage`)

Hero placed at (720, y0) at the image after a poll, Left held (let go in the poll images), y0 = 286.0000 to 295.0000 by 0.0625 (145 starts), PosY read at the first tick with x <= 690, against `critic/out/w3_door.txt`: at k = 1, 2, 3, **PosY equal to the binary for 145 of 145**, equal to the law for 145 of 145, `SlideCount` = `(PosY - y0) / 0.5` for 145 of 145 (0 to 18 slides). No contradiction of the binary.

## (f) The X window of the jump under the pilot's rule (`out/f_summary.txt`, `dll-probes/f.txt`, probe `F_Window`)

Hero placed idle at (x, y, z 64) after a poll image, x = 650.0 to 700.0 by 0.5, y = 295.0, 296.0, 297.0; the first image after the poll presses Cross + Left; Left held then except in the poll images; 110 ticks. Success = `PosZ` = 80 with `IsOnGround` (on rec5) **and** `PosX <= 615` at the end (past the column); the landing is always 18 ticks after the Cross. First failure from the face of the crates (x = 657.0): k = 1 at 693.0; k = 2 at 690.0; k = 3 at 688.0 (y 296) or 688.5 (y 295, 297); at k = 3 the successes above the first failure are not contiguous. **Retained window = the intersection of the 9 cases, contiguous from the face: x in [657.0 ; 687.5], included in the binary's [657.0 ; 694.4]** (not empty). Control `F0` (`dll-probes/f0.txt`: Left held through the flight, no release): success from 657.0 to 694.0 at k = 1, 2, 3, first failure 694.5 = the binary (694.406 succeeds, 696.844 fails in `bin/out/a3_jumpx.txt`). The samples below 657.0 are inside the crates (not a legal position, ignored).

## Stops of the plan

None reached: no roll, regular cadence (in ticks); the window is not empty and lies inside the binary's; every slide count is explained by the law; the calage equals the binary; nothing of the engine touched.

## Flagged

- The window is on the plan's 0.5 px grid; the exact upper edge lies in (687.5 ; 688.0) at k = 3.
- The window and mouth probes reuse one arc per (k, y) for their starts (hero re-placed after a poll, nothing held); the route arcs start from the real arc state.
- The order of the cadences inside a `dll-probes/*.txt` file is the order xUnit ran them.
- The pilot's thresholds (YD = 288.0, 600.0, 343.5, 541.0) and the pinned counts belong together: the T1 arcs must use the same rules.
