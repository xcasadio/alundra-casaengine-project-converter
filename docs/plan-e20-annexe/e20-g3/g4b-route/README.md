# E20.g4b - T0 record (the barricade by lifting)

Plan: `docs/plan-e20-heros.md` section 1.29.2 (revision 2, READY), with the facts of 1.29. Start HEAD of the execution: `2add401a` (the preset `porter-plateforme-m72`, D-E20-79, is already in it). Branch `chantier/theme1-porter-plateforme`. The code of the DLL and of the engine is unchanged by T0.
Binary: `ALUN_CD.EXE` (France). Binary harness: `docs/plan-e20-annexe/e20-g3/critic/my392.py` (the 12 records spawned in file order by the real map-record spawner, the real `MovePlayer`) with the oracle machine of `docs/plan-e20-annexe/e20-g1a/work` as `g1a/` next to it (the scratch copy of `critic/`, as for g4a); the scripts of this folder
are copied into that `critic/` and run from there with `python -B -I <script>`; their outputs land in its `out/` and are copied to `out/` here. Scripts: `g4b_lib.py` (a tick loop that reads the machine), `g4b_a1_route.py` (a1), `g4b_a2_a5.py` (a2 to a5), `g4b_a4_sweep.py` (the Cross tick of a4).

The DLL probes are throw-away tests (`dll-probes/G4bT0ProbeTests.cs.txt`, a temporary class of `Alundra.Tests/Probes/`, removed after the runs, never committed); they write their lines to the file named by `G4B_OUT` (`dll-probes/*.txt`; the order of the cadences inside a file is the order xUnit ran them).
World: the real map 392 (the 12 crates, the real prefabs, the real hero, B1 running), Release, the code of the DLL at the start HEAD. An **image** is one `OneFrame(k / 50f)` of k ticks (k = 1, 2, 3). The tick hook is `WorldProxy.TickTraceForTests` ("pad" opens a tick, "inventory" is the **end** of the tick: the entity block, the hero's `MovePlayer` and the physics ran before it).

## Sites of the plan re-located by content

| Plan | At the start HEAD |
|---|---|
| `AlundraShipBarricadeArcTests.cs` | `Alundra.Tests/Arcs/ShipBarricadeArcTests.cs` (class `ShipBarricadeArcTests`, the pilot `Pilot`, `Fly`, the tables `CalageRows`, `RouteRows`, `RefusalRows`) |
| `AlundraArcSupport.cs`, `ArcsThatSlide` | `Alundra.Tests/Support/ArcRun.cs`, `ArcsThatSlide` (line 311; the guard is in `Dispose`) |
| `AlundraHitArena.cs` (`:74`, `:235`) | `Alundra.Tests/Support/HitArena.cs` |
| `WorldProxy.cs:2589`, the "inventory" pass | `Alundra/Maps/WorldProxy.cs:2598` (`TickTraceForTests?.Invoke("inventory")`, after `RunEntityBlock`) |
| `LiftWorldTests.cs` (m7), `LiftMap72ArcTests.cs` (m2) | `Alundra.Tests/Hero/LiftWorldTests.cs`, `Alundra.Tests/Arcs/LiftMap72ArcTests.cs` (unchanged names) |

No difference of substance with the plan. One reading of the plan is changed by a measure: the Cross tick of (a4) (see (a4) and the report).

## (a) The routes and starts of the binary, with the 12 records

Every scenario below is a scripted player that reads the machine before each tick; the harness writes nothing after the start (position, and the height for a3). Traces in `out/`.

### (a1) the route "lift" (`g4b_a1_route.py`, `out/g4b_a1_route.txt`; the sweep of the throw threshold `out/g4b_a1_sweep.txt`)

Start (775.125, 216, z 64) = where B1 leaves the hero. Phases (state read before the tick):

| tick | phase | what ended the previous one | hero at that tick |
|---|---|---|---|
| 1 | `down` (Down) | | (775.125, 216.8) |
| 46 | `left` (Left) | `PosY >= 288.0` | (775.125, 288.312) |
| 108 | `gesture`: **Square** (the lift) with Left held | the contact `xcoll == rec5` at the end of the tick 107 (x = 657.0, the first blocked tick, y = 295.125 after the calage) | (657.0, 295.125) |
| 144 | the carry stand (state 12), then the walk (state 7) from the tick 145 | 36 ticks of gesture (the hero is still) | (657.0, 295.125) |
| 163 | `after_throw`: **Square** (the throw), the crate leaves the hands in that very tick | the first tick with `PosX <= 615.0` (613.125) | (613.125, 295.125) |
| 189 | `south` (Down) | `PosX <= 600.0` (the hero is held 20 ticks in the throw state, 163 to 182, walks again from 183; `rec9` stops him at 597.0, he slides to 598.5) | (599.719, 295.125) |
| 220 | `west` (Left) | `PosY >= 343.5` | (598.5, 344.0) |
| 245 | `down2` (Down) | `PosX <= 541.0` | (540.0, 344.0) |
| 260 | the hero is in the cell (22, 23) of the portal 0 | | (538.781, 369.188) |

The crate: carried at the tick 108 (it rises to the hero's head, +32); after the throw it breaks (animation 2) at the tick 184 (21 ticks after the throw) on the plateau of height 5 at (550.1, 295.1, z 80) and its slot is free at the tick 187 (24 ticks after the throw). The throw threshold is not critical: **every `XT` from 654 to 598 (step 2) gives the same arrival tick 260 and the portal cell**;
a threshold <= 596 never throws (`rec9` stops the hero at 597.0). The break is at x = 583.1 (XT 654) to 534.0 (XT 598); for XT <= 622 the crate lands on the plateau (z 80), for XT >= 624 on the ground (z 64). The pilot takes `XT = 615.0` (the hero is wholly west of the column, whose west face is 626 and 615 the flush position of the hero).
All 12 records matter: `rec9` stops the hero at 597.0 on the way, as in g4a.

### (a2) the start of (b2) (`out/g4b_a2.txt`)

Hero at (670, 316, z 64), facing west, Left until the contact `xcoll == rec6` (end of the tick 6, x = 657.0, y in [311 ; 327[), Square at the tick 7: `rec6` carried (z 64 -> 96 at one pixel per tick), **`rec10` falls**: z at the end of the ticks 6 to 14 = **80 ; 79.5 ; 78.5 ; 77 ; 75 ; 72.5 ; 69.5 ; 66 ; 64**
(L = 7 the tick of the lift: 80 at L-1, 79.5 at L, 64 at L+7). The series of the plan, exactly.

### (a3) the start of (b3) (`out/g4b_a3.txt`)

Hero at (636, 296), z 80 + 1 unit (standing on `rec5`, support = rec5), Down held: y = 296.8 at the tick 1, **y = 297.0** at the tick 2, contact `xcoll == rec10` at the end of the tick 3, Square at the tick 4: `rec10` carried, the hero stays at (636, 297, z 80).

### (a4) the start of (b4) (`out/g4b_a4.txt`, `out/g4b_a4_sweep.txt`)

Hero at (720, 298, z 64), facing west, Left held, Cross at the tick tj; Square at the first tick that follows a tick ended with a contact and the hero in the air (the gate of `critic/w5_airlift.py`). tj = 12 (the plan): contact `rec10` at the end of the tick 28 (z 81.0), Square at the tick 29, `rec10` carried, the hero lands at the tick 32 (z 64, at x = 657.0, carrying `rec10`).
The sweep of tj, y = 298 (`out/g4b_a4_sweep.txt`): **tj 12 to 24 -> `rec10` carried** (Square tick 28 to 30, z 81.0 to 91.5); tj 9, 10, 11 and 25, 26 -> `rec5` carried (z 69 to 77.5); tj 8 -> no contact in the air. No jump falls without an open door in the window [12 ; 24].
**The Cross tick of the DLL pilot is not 12**: see (b) (pilot-rules.md rule 5): the pilot's poll images break the flight at k = 2 and 3, the tick 19 is the first common one.

### (a5) the start of (b5) (`out/g4b_a5.txt`)

Hero at (657, 312, z 64) flush on `rec6`, facing north, Up held: y = 295.0 at the end of the tick 11 (the north wall of the door), contact `xcoll == rec5` at the end of the tick 12, Square at the tick 13: `rec5` carried. The hero stays at (657.0, 295.0).

## (b) The rules of the pilot: `pilot-rules.md` (written before the arcs).

## (c) The fall of `rec10` on the DLL, per tick, at the three cadences (`dll-probes/c.txt`, probe `C_FallOfRec10`, and `b2.txt`)

Start of (b2), pose at (670, 316, z 64) in the image after the anchor, Left to the contact `rec6` (Square image first tick 7 after the pose, lift in that tick), `rec10.PosZ` read at the end of **every** tick by `TickTraceForTests` ("inventory"), L = the tick where `rec6` is first carried:

| tick | L-1 | L | L+1 | L+2 | L+3 | L+4 | L+5 | L+6 | L+7 | L+8 |
|---|---|---|---|---|---|---|---|---|---|---|
| binary (a2) | 80 | **79.5** | 78.5 | 77 | 75 | 72.5 | 69.5 | 66 | 64 | 64 |
| DLL, k = 1, 2, 3 (identical) | 80 | **80** | 78.5 | 77 | 75 | 72.5 | 69.5 | 66 | 64 | 64 |

**Different**: the DLL has no 79.5 at L (the half pixel of the first tick of the fall); from L+1 to L+7 the series is the binary's, to the figure, and `rec10` rests at 64 at L+7 in both. The three cadences give the same series, tick by tick. This is the gap **Q-G3-c (A)** (the pile above the lifted crate follows the old fall rule of the entities of the map, D-E20-37): consigned; it is pinned in T1 by a
test named after it (`QG3c_...`), not by (b2)'s series. `rec6` rises 1 pixel per tick from L+1 (z 65, 66, ...), as the binary.

## (d) The slides of each new arc, measured and explained (`dll-probes/b*.txt`; pose rule and pilot as in `pilot-rules.md`; the guard of `Dispose` compares `TotalSlideCount`)

| arc | k = 1 | k = 2 | k = 3 | explanation |
|---|---|---|---|---|
| (b1) lift route | 11 | 13 | 12 | the calage at the north wall of the door: 10, 9, 12 second steps (the law of g4a `295 + ((y0 - 295) mod 0.5)`, y0 = 290.3438, 290.75, 289.125, ends at 295.3438, 295.25, 295.125, exactly the numbers of `A5j-a2`); plus the **mouth of the corridor**: 1, 4, 0 |
| (b2) | 0 | 0 | 0 | the hero walks straight to the crate in the door (y = 316, away from the walls) |
| (b3) | 0 | 0 | 0 | on `rec5`'s top, pushing south against the pile at y = 297.0 (the flat edge with 305): no wall, no slide |
| (b4) | 0 | 0 | 0 | in the air, then against the crate face |
| (b5) | 1 | 1 | 5 | Up against the north wall of the door at y = 295.0, flush on the crates: see below |
| (b6) | 1 | 1 | 5 | the same lift as (b5) (the roll adds none: nothing is pushed against a wall) |

**The mouth of the corridor (b1)**: the hero comes walking west and the pilot switches to Down at the first image where `PosX <= 541.0`; the first tick of Down still carries 1.2188 px of the west walk (the first-tick half step of the binary, `bin/report.md` A3.1; seen in the binary trace `out/g4b_a1_route.txt`, tick 245: 540.0 -> 538.78). The corridor is free for `PosX >= 538.0` (g4a `mouth_compare.txt`)
and aligns a hero at x < 538.0 by 0.75 px per tick. The pilot switches at x = 539.1094 (k = 1), 536.3438 (k = 2), 539.7188 (k = 3): minus 1.2188 that is 537.89 (one step of 0.75: 538.64, the slide measured at (538.6406, 344.0)), 535.125 (four steps: 535.875, 536.625, 537.375, 538.125, the four slides measured) and 538.50 (none). The counts are those of the law, and **they move with the throw and the
cadence** (the phase of x), unlike g4a's route (539.41, no slide): they belong to the pilot's thresholds and are pinned with them.

**The north wall of the door with the crates (b5, b6)**: the hero reaches y = 295.0 (the wall) flush on the crates (x = 657.0); the cardinal slide tries the second step east (0.75) when the first step made no progress and the contact is not yet held (`ScriptedMotion.SlideAlongWall`, `forbidden = XCollisionEntity != null`); `rec5`'s east face refuses it (x stays 657.0) and the step is counted. k = 1, 2: **one** slide, at the tick of the arrival at the wall (216, 217),
the contact is there at the end of that tick, the Square comes at the next image. k = 3: the image of the contact is followed by a poll image (Up let go), then Up is held again for three ticks: **five** slides (ticks 218, 219, 222, 223, 224, all at (657.0, 295.0)), per tick of Up pushed (or still requested, the tick 219 being the first of the poll image, the controller still holds the request of the tick before) before the Square of the image 75.
The residual (the slides at the ticks 223, 224 with the contact of the previous tick `rec5`) is pinned by the measure, not derived: `XCollisionEntity` is cleared and set again inside the tick.

## Other measures

- **(b1) on the DLL** (`dll-probes/b1.txt`): the lift route reaches the portal 0 (departure to the map 391) at the three cadences with `Rolls == 0` and the predicted polls equal to the real ones: k = 1 departure at the image 478 (tick 477); k = 2 image 252 (tick 503); k = 3 image 174 (tick 521). The lift in the first tick of the image `F` (the contact `rec5` at the start of `F`): Square image 314, 163, 113; carry stand 36 ticks later (350, 362, 375);
  throw at x = 614.34, 613.13, 613.13 (image 369, 192, 133), hands free in the same tick; `rec5` destroyed 24, 24, 25 ticks after the throw (binary 24; the 25 at k = 3 is flagged, not investigated: it is the tick of the status change read at the end of a tick).
- **(b3)** y = 297.0 at the Square, `rec10` carried; the Square image is the first image after the pose whose start state has the contact: the image 207, 104, 70 (the 3rd, 2nd, 2nd after the pose, one tick earlier than the binary's tick 4 at k = 1).
- **(b4)** with the plan's tj = 12 read as "the first image whose first tick is >= 12": k = 1 works (Square tick 29), **k = 2 (Cross at the tick 13) and k = 3 (Cross at the tick 16) never get an image start with `rec10` touched in the air** (`b4.txt`); with the tick 19: Square at the first tick 29, 35, 34, z 91.5, 84.0, 86.5, `rec10` carried (`b4.txt`, `b4_sweep.txt` for the Cross images 2 to 24 at each cadence).
- **(b5)** y = 295.0 at the Square, `rec5` carried; **(b6)** the roll in the carry stand (tick 268 at the three cadences): at k = 1, 2 the hero is held still five ticks (state 12, tick 268 to 272) then walks west (the verrou of 5 ticks, A5r); at k = 3 the random direction of the roll moves him (to (653.6, 296.5), then y = 297.0) during the same five ticks; in the three cases `rec5` stays in the hands (z = hero + 32 = 96) up to roll + 12 and the roll count is 1.
  A second roll (the next poll, 21 ticks later) was seen in the first version of the probe at the three cadences, with the object still in the hands (not retained: the arc ends 12 ticks after the first).

## Stops of the plan

None reached: all five scenarios of (a) reach their issue with the 12 records; (c) is different (the gap Q-G3-c (A), expected by the plan); no roll outside (b6); nothing of the DLL or the engine touched.

## Flagged

- **The Cross tick of (b4) is 19, not 12** (the plan's tj = 12 does not open the gate at k = 2, 3 under the poll images, measured). The binary window [12 ; 24] contains 19. The common set of the three cadences is {19, 25}.
- The pose of a start is "in the image after the anchor", not after a poll (g4a's probes posed after a poll to sweep many starts): the poll phase of a start is then fixed (first poll after the pose at the tick 16, 15, 14).
- The slide counts of (b1) depend on the pilot's thresholds (`YD = 288.0`, `XT = 615.0`, 600.0, 343.5, 541.0), like g4a's: a change re-measures them. The mouth of the corridor moves with the throw timing (1, 4, 0 slides here).
- `rec5` is destroyed 25 ticks after the throw at k = 3 (24 elsewhere and in the binary).
- The probes hold Up/Left/Down after the Square only where the scenario needs it (the first version of the probes counted 2 or 6 slides in (b5) because it kept pushing after the lift: the arcs stop pushing at the Square, as the rules say).
