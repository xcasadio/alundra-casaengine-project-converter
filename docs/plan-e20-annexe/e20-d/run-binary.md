> Report returned by one agent of the read-only discovery for E20.d, the run (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

Things that change the plan

1. Deep water kills the run at once, and the plan row does not say how to keep "le héros marche encore". FACT (0x80031CFC, slope table 0x80022D1C, swim table 0x80022E7C): on slope 4, 0x8003260C zeroes `[0x80126FFC]` (charge) and `[0x80127110]` (weapon pointer) and calls `0x8002F884(2)`. It skips the prologue calls at 0x80031D68 and sends every run state to 0x1D (swim idle). 0x3E first flips the direction, then goes to 0x1D. Triangle in water is the dive: `press & 0xD0` gives 0x28. Measured on the oracle copy (flooded map, `SWIM40`): the sprint became 0x1D one tick after the slope read 4, then 0x0F. The DLL has `Slope_18c` (0 to 7, hero only) but no swim branch. Without a guard in E20.d, a hero holding Triangle in water would keep running through it, which the binary never does. Proposed default, a documented deviation to confirm with the author:
   - The tail does not start 0x29 on slope 4.
   - 0x29, 0x03, 0x2A, 0x04 and 0x3E write Idle (0) on slope 4, standing in for 0x1D until E20.i.
   - E20.i later replaces this with the real branch.
2. O-E20-12 is mostly solved already; one line is missing, and it fits in E20.d. FACT: the binary recomputes `+0x144` (IsOnGround), `+0x18C` (slope), `+0x180`/`+0x184` (VramOR/AND), `+0x188`, `+0x134` (floor) and the hit box together. It does so at the end of every tick's physics pass: `0x80038064` is called per entity at 0x800384BC, after the move. MovePlayer of the next tick reads them. In the DLL, `RunHeroProbes` already runs per tick after the move (`AlundraEntityScriptProxy.cs:1501-1510`, E20.a) for slope, VRAM flags and `FloorHeight`. But for a grounded hero `IsOnGround` is still the engine's `Controller.IsGrounded` read at the head of `Update` (`:1342-1352`). INFERENCE: setting `IsOnGround = (PosZ <= floor)` inside `RunHeroProbes` closes the gap. It only shows on frames with 2 or more ticks.
3. The resume window is bigger than the plan text suggests: 7 ticks, E+10 to E+16 inclusive (E = the tick MovePlayer writes 0x2A). Any direction bit at E+1 to E+9 kills it. MovePlayer still sees 0x2A at E+16 because the chain to Idle happens in the animation pass after it. A press at E+17 gives Idle, then a new wind-up. Oracle runs `RES9`, `RES10`, `RES11`, k=14, 15, 16, 17.
4. The wind-up is 25 ticks. If Triangle is pressed on tick T, 0x29 is the target on T to T+24 and the earliest 0x03 is T+25. The hero already moves on T+25: Force 64512, 129024, 193536, 258048 on T+25 to T+28. A facing change during the wind-up restarts the 24-tick loop, so turning delays the start. Run `TURN`: two turns at ticks 11 and 26 gave a start at tick 51.
5. The plan's "poussière toutes les 4 images" holds only for 0x03. The 0x29 dust is gated by completed loops and does not exist before the first loop ends. 0x04 and 0x2A dust has force +1/8 on land and 0 in water, and no step sound. See section 11.
6. 0x3E nearly always ends in the air, so the binary's "value 1 in the air" (§0.2.8 point 1) is the normal path, not a corner case. The hop lasts 16 ticks and the bounce 11. The tick after is state 1 (Moving) in the air, which runs the Idle/Moving case (TryUseItem, PlayerTryAction, CheckEntityInteraction), then the tail gives 0x2C or 0x2D. Oracle `WALL` ticks 102 to 104.
7. Unknown resolved (D8): the DLL's `AnimationTables.AnimationDirectionTable` (`AnimationTables.cs:30-36`) is byte-identical to the binary table at 0x800237F4. Facing indices are 0 down, 1 up, 2 left, 3 right. The cone rows index `AnimationDirection` directly.
8. The charge keeps counting in all five run states, but only 0x29 can fire a release. A Square release in 0x03, 0x2A, 0x04 or 0x3E is lost. Square held on 0x03 goes straight to the dash, because `0xE0` includes Square.
9. The wall bump has a 2-tick latency on real walls and 1 tick at the map clamp. Real wall: contact tick C moves the hero up to the wall with `fadj` 0. Tick C+1 makes no progress and sets `fadj`. The bump is on C+2. Map-edge clamp: `fadj` is set on the clamp tick and the bump is on the next tick.
10. A Square press while touching a liftable entity lifts it in 0x29, 0x03 and 0x2A, because PlayerTryAction runs before the attack and dash tests. That is E20.g. The E20.d stub returns 0.
11. The test "Triangle seul donne Idle" inverts to: 0x29, then it stays in 0x29, Force 0, until a cardinal direction arrives.

## 1. Method and evidence

- Decode: capstone on `ALUN_CD.EXE` (France), offset = 0x800 + addr − 0x80020000, in my scratch folder.
- Oracle: I copied the D1 oracle (`tools/`) into the scratch folder and ran the copy with new pad scripts, a custom cell map and a hook. It runs the real MovePlayer, animation, counters and physics passes on the emulated machine. The effect and sound calls are stubs that journal. The hit pass is stubbed.
- Oracle limits: flat synthetic field, one entity, no map events. The map-edge clamp is at x = 10 px and y = 7 px. It does not validate against a reference, so these are decode-consistent measurements, not an annex.
- Scratch root: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20d/run-binary/`
  - Traces for 19 scenarios are in `oracle/traces/*.txt`, and the driver is `oracle/tools/runs.py`. Run them with `python -B runs.py <name> [first] [last] [T]`.
  - `mdis.py` is the disassembler, `herodata.py` dumps the hero animation records, and `tables.py` dumps the jump tables.
  - The raw disassembly chunks (`mp1.txt` to `mp5.txt`, `ua.txt`, `cpae.txt` and others) are in the same folder.
- Repo untouched. The HEAD copy of `AlundraPlayerManager.cs` was read with `git show`, not from the working tree.

## 2. Inputs and tables

FACT (pad struct 0x80126F18):

| Word | Address | Used for |
|---|---|---|
| hold | 0x80126F28 | `>> 12` gives the direction nibble; Triangle `0x10`; the `& 0xE0` test; the Triangle hold tests |
| pressed | 0x80126F2A | Cross edge `0x40` (tail); Square edge `0x80` (PlayerTryAttack, PlayerTryAction); Circle edge `0x20` (TryUseItem) |
| released | 0x80126F2C | Square release (PlayerTryAttack, UpdatePlayerWeaponEffect) |

- Direction nibble bits: Up 1, Right 2, Down 4, Left 8. Button bits: Triangle 0x10, Circle 0x20, Cross 0x40, Square 0x80. The DLL constants match (`AlundraPlayerController.cs:27-30`).
- 8-way table 0x80022C6C, indexed by nibble: `[-1,0x10,0x18,0x14,0,-1,0x1C,-1,8,0xC,-1,-1,4,-1,-1,-1]`. Only read at 0x80031CD8. A −1 keeps `+0x8C` (TargetDirection).
- Dash table 0x80022CAC, indexed by nibble: nonzero entries are `[1]=0x10`, `[2]=0x18`, `[4]=0`, `[8]=8`, all others −1. The scan finds it read only at 0x80031FCC (start) and 0x8003219C (resume).
- Cone rows 0x80022CEC, 12 bytes per `AnimationDirection`. Read only at 0x800320A4.
  - down {0x1C,0,4}
  - up {0xC,0x10,0x14}
  - left {4,8,0xC}
  - right {0x14,0x18,0x1C}
- Facing table 0x800237F4 (32 words, 8 per row). Column is `((dir+2) & 0x1C) >> 2`.
  - Rows 0 and 1 give `[0,0,2,1,1,1,3,0]`.
  - Rows 2 and 3 give `[0,2,2,2,1,3,3,3]`.
  - Diagonals keep the previous axis. Each cone row is exactly the three sectors that keep that facing.
- Direction vectors, int16 tables at 0x80023654 (X) and 0x80023694 (Y): cardinals X ±768 and Y ±512; diagonals X ±543 and Y ±362.

## 3. Where the run sits in MovePlayer (0x80031B50)

FACT order for a free, undamaged tick:

1. 0x80031B70-0x80031B8C weapon fiche index to `[0x8012710C]`. `+0x20 != 0` ends the tick with `[0x80126FFC]` = 0 and `[0x80127110]` = 0.
2. Locked (`[0x800DC4B8] & 0x34`): `CreatePlayerAnimationEffects(1)`, charge and pointer cleared, then the end. No state logic runs. A run in progress keeps its animation and forces.
3. 0x80031C30-0x80031C40: `+0x6C |= 0x100`, then `CreatePlayerAnimationEffects(0)`. It reads the target animation of the previous tick.
4. 0x80031C44 damage entry. A hit gives 0x31 (0x3A on slope 4), so every run state except 0x3E is interruptible.
5. HP 0 goes to 0x800327A4. `+0x188 & 0x80` goes to 0x80032768. Then `0x8003634C` (0x80031CC4).
6. 0x80031CD0-0x80031CF8 compute `s3` = nibble and `s1` = 8-way direction (or `+0x8C` when −1). `s2` = `+0x18C` (slope).
7. Slope switch (table 0x80022D1C): slope 4 goes to 0x8003260C; slope 6 goes to the climb gate at 0x80031D20; any other slope goes to 0x80031D68.
8. 0x80031D68-0x80031D78: `UpdatePlayerWeaponEffect`, `UpdateWeaponStepProgression`, `UpdatePlayerCarriedEntity(0)`.
9. 0x80031D8C: switch on `+0x88` (TargetAnimationId, `sltiu 0x50`, table 0x80022D3C).
10. 0x80032910 stores the new target, then 0x80032914: `UpdateItemEffectState`, then the HP setters.

Climb gate (0x80031D20-0x80031D64), FACT and oracle-confirmed (`CLIMB` vs `NOCLIMB`). It writes `+0x88 = 0x0E` when all hold:
- slope is 6
- `s3 != 0`
- `s1 == 0x10`
- `+0x8C == 0x10`
- `+0x13C != 0` (ForceAdjusted)
- `+0x2C == 0` (nothing carried)

Because the dispatch reads `+0x88` after the gate, a sprint into a slope-6 wall climbs instead of bouncing. In `CLIMB` the sprint ended at tick 33 (`fadj`) and 0x0E (ForceZ 65536) came on tick 34. In `NOCLIMB` it was 0x3E.

## 4. Entry: the common tail (0x80031EA8)

Reached from Idle/Moving (0x80031E38: TargetDirection = s1, TryUseItem, PlayerTryAction, CheckEntityInteraction) and from jump states 2, 0x2B, 0x2C and 0x2D (0x80031E84: the same without CheckEntityInteraction). Order:

1. `PlayerTryAttack` (0x8002EAF4). A nonzero result ends the tick.
2. `+0x144 == 0` goes to 0x80031F44: `s3 ? 0x2C : 0x2D`.
3. Cross edge (`pressed & 0x40`). If `+0x180 & 0x4000` the tick ends with nothing changed. Otherwise `s3 ? 2 : 0x2B`.
4. Triangle held (`hold & 0x10`, no edge): target 0x29 and `+0xB0 = 0` (AnimCompleteCounter).
5. Otherwise `s3 ? 1 : 0`.

Consequences, all FACT in the code:
- Square beats Cross beats Triangle in the same tick.
- Holding Triangle through a landing, an attack end or a dialog starts the wind-up on the first tick the tail is reached. Oracle: after a bounce and landing, 0x29 comes on the landing tick + 1.
- Walking then Triangle: Force goes to 0 in the switch tick (0x29 has speed 0, shift 0). Run `WALKTRI`: the hero stops at once.
- Entry needs no cardinal direction. It needs only Triangle held and nothing returning early.

## 5. Per-state rules (target = `+0x88`; "end" = jump to 0x80032914)

### 0x29 PrepareSprint (0x80031F54)

1. TryUseItem == 0: end.
2. PlayerTryAction != 0: end.
3. PlayerTryAttack != 0: end (a Square press gives the swing; oracle `WSQ`: 0x10, sound 29).
4. `TargetDirection = s1` (0x80031F94, in the delay slot). This write also happens on the airborne tick.
5. `+0x144 == 0`: target 0x2D, end. This test comes after the three calls.
6. `!(hold & 0x10)`: target 0 (Idle), end.
7. `+0xB0 == 0`: end (stay).
8. `d = dash[s3]`. If d is −1: end (diagonals and no direction stay in 0x29).
9. Else `TargetDirection = d`, target 3, end.

Cross does nothing here (`WCRO`).

### 0x03 Sprint (0x80031FF4)

1. PlayerTryAction != 0: end. TryUseItem is not called, so a Circle press is swallowed.
2. `+0x144 == 0`: target 0x2D, end. This is before the ForceAdjusted test.
3. `+0x13C != 0`: `CreateEffectEntity(0, 9, 0, PosX, PosY, PosZ + 0x100000)` (call 0x80032048). Then target 0x3E and `TargetDirection = (dir + 0x10) & 0x1F`. End.
4. `s3 == 0`: target 0x2A, `[0x80126FF4] = 1`, end.
5. `!(hold & 0x10)`: target 0x2A, counter 0, end.
6. `s1` not in cone row `[AnimationDirection]`: target 0x2A, counter 0, end. An invalid nibble keeps `s1 = TargetDirection`, which is inside the cone.
7. `hold & 0xE0` (Circle, Cross or Square held): target 4, end.
8. Otherwise nothing is written. Sprint never writes TargetDirection.

### 0x2A StopSprint (0x80032118)

1. TryUseItem == 0: end.
2. PlayerTryAction != 0: end.
3. `+0x144 == 0`: target 0x2D, end.
4. `c = [0x80126FF4]`; `c == 0`: end.
5. `+0x13C != 0`: c = 0, end.
6. `!(hold & 0x10)`: c = 0, end.
7. `c += 1` (stored). If `c < 11`: `s3 == 0` ends; any direction bit sets c = 0 and ends.
8. `c >= 11`: `d = dash[s3]`. If d is −1: end. Else `TargetDirection = d`, target 3, end (also a new cardinal).

Only MovePlayer touches the counter. The scan for the immediate 0x6FF4 finds 0x80032088, 0x800320F8, 0x80032158, 0x8003218C and 0x800321D0.

### 0x04 SprintDash (group 0x80031E18, shared with all ground attack states)

- `+0x144 == 0`: target 0x2D.
- Then TryUseItem with its result ignored, then end.
- The pad is not read and there is no ForceAdjusted test. A dash into a wall just stops.
- The animation chains to 0 (Idle).

### 0x3E SprintAgainstWall and 0x1C (0x80032558)

1. TryUseItem == 0: `TargetDirection += 0x10`, end (0x8003270C). The oracle stub never gives 0.
2. `+0xAC != 1`: end.
3. `TargetDirection = (+0x8C + 0x10) & 0x1F`.
4. `+0x144 != 0`: target 0. Otherwise target = `+0xAC` = 1.

Two states share one handler.

## 6. Records and kinematics

Hero animation records (`data-extracted/.../map_alundra.json`, `herodata.py`). "Shift" is `byte 0xD & 0xF`. A frame lasts "delay" ticks, counted from the load tick.

| Anim | Speed | Shift | Sfx | Z impulse | Frames and end |
|---|---|---|---|---|---|
| 0x00 Idle | 0 | 1 | 0 | 0 | loop, 54 ticks |
| 0x01 Moving | 208 | 1 | 0 | 0 | loop |
| 0x29 | 0 | 0 | 0 | 0 | 6 × 4 = 24, loop |
| 0x03 | 336 | 2 | 13 | 0 | 6 × 5 = 30, loop |
| 0x04 | 0 | 6 | 83 | 0 | 8 × 4 = 32, chain 0; a box on every frame |
| 0x2A | 0 | 5 | 14 | 0 | 4 × 4 = 16, chain 0 |
| 0x3E | 256 | 0, byte 0x40 | 28 | 1024 | delays 1,2,2,2,2,1 = 10, hold (terminator 0,0x80); a box on every frame |

- The Z impulse is the signed halfword at record +0xA (Unknown | Flags << 8); for 0x3E the Flags byte is 0x04.
- Terminator semantics (0x80038C00-0x80038D88):
  - `(1,*)` is loop: `+0xB0++` and restart.
  - `(0,n<0x80)` is chain: `+0xB0++` and switch to n.
  - `(0,n>=0x80)` is hold: `+0xA8 = 0x7FFFFFFF`, `+0xAC = 1`.
- A frame byte with bit 7 set is a normal frame. A hold or chain happens in the animation pass after MovePlayer of that tick.
- Physics (0x80036614, 0x800367E4, hero branch 0x80036828):
  - Target = X/Y table × speed, recomputed only when `+0xF4 != speed`, or `+0x94 != TargetDirection`, or the shift changed.
  - Step = `|target − Force| >> shift`, computed at that moment and not each tick.
  - Each tick Force approaches the target by the step, clamped at the target, and the move uses the new Force.
  - Ice (`+0x180 & 0x20`): step / 16 (0x80036960-0x800369BC).
  - Shallow water (`+0x180 & 8`) with `[0x80127000] <= 0`: target × 0.5 (0x800369D0-0x80036A44).
  - Both are generic and already in the DLL.
- Values (X / Y):

| Item | X | Y |
|---|---|---|
| Walk | 159744 | 106496 |
| Sprint | 258048 | 172032 |
| Bounce | 196608 | 131072 |

  - Start step: 64512 (X) and 43008 (Y), from rest; 24576 (X) from the walk.
  - Stop step: 8064 (X). Dash step: 4032 (X).
- Z hop of 0x3E: ForceZ 262144 on the bump tick, minus 32768 per tick, apex 18 px, landing 16 ticks after the bump (oracle `WALL`).
- `ForceAdjusted` (+0x13C) is cleared at the start of each tick's physics pass (0x800383B8) and set by the map-edge clamp (0x8003679C, 0x800367D4), by the no-progress rule in the mover (0x80037D54) and by a rider copy (0x80037E78).

## 7. Timelines from the oracle (tick numbers from the press)

- Wind-up and start (`R1`, Triangle + Right from tick 1):
  - 0x29 target on ticks 1 to 25 (`b0` = 1 after tick 25's pass); 0x03 from tick 26.
  - Forces 64512, 129024, 193536, 258048 on ticks 26 to 29. Position +0.984 px on tick 26.
  - Sounds: S13 on tick 26 (animation change), S12 (step) at FrameCounter & 7 == 0.
- Stop (`R1`, Right released at tick 61): 0x2A on ticks 61 to 76, counter 1 → 12. Then Idle on 77 with Force 64512 (chain shift 1), stopped on 78. From the last sprint tick, total slide 47.25 px. Forces 249984 … 129024 (−8064 per tick).
- Dash (`DASHS`, `DASHX`, `DASHO` are identical): 0x04 on ticks 41 to 72, S83. Forces 254016 … −4032 per tick. Idle on 73, total slide 94.5 px from the last sprint position.
- Resume: E = 46. Press at 55 (E+9) gives no resume (`RES9`). Press at 56 (E+10) resumes with ForceX 197568 (momentum kept, step recomputed). A new direction at E+10 resumes in that direction (`RESKILL2`): ForceX 133056, ForceY −43008. A press at E+16 (62) resumes; at E+17 it is Idle then 0x29.
- Bump on a wall (`WALL`, wall of 48 px at x = 360): contact tick 90 (position 349.0, `fadj` 0), `fadj` 1 on tick 91, bump on tick 92. Effect 9 at (349, 100, z 16 above feet), S28. ForceX −196608 on ticks 92 to 102 (11 ticks, 33 px). `rst` = 1 on tick 102. Target 0x01 on tick 103 (airborne), 0x2C on 104, landing 108, 0x29 on 109 with S11 and 3 dust effects.
- Bump at the map edge (`EDGEL`): clamp tick 50 (x = 10.0), `fadj` 1, bump on 51.
- Ledge (`LEDGE`, drop of 48 px at x = 360): `gnd` = 0 at the end of tick 96, still 0x03 on 96. 0x2D on 97 (ForceX halved to 129024), 0x2C on 98 (ForceX 139776, then 150528), landing 110. Momentum is not kept.
- Diagonal start (`DIAG`): stays 0x29 facing right (`dir` 0x14, facing 3 kept). Releasing Up at tick 41 starts at once (counter ≥ 1).
- Cone (`CONE`): Right+Down, Right+Up and Up+Right stay in 0x03 (TargetDirection unchanged). Down alone is outside: 0x2A with counter 0 (no resume). `CONE2`: Up+Left gives the same.

## 8. Direction handling

- 0x29 writes TargetDirection from the 8-way table every tick (diagonals included). The animation facing follows the hysteresis table, and a facing change restarts the animation (new loop, Z impulse re-applied, Sfx replayed, `+0xAC` cleared), but `+0xB0` is not touched. `UpdateAnimation` 0x80038B18-0x80038BC8.
- 0x03 writes TargetDirection only at the bump flip. The cone is checked every tick against the current facing.
- The start and the resume write the dash-table direction (cardinal). That is the only place D-E20-15 acts.
- The bump flips the direction (+0x10), and the end flips it back. The hero faces away from the wall during 0x3E.

## 9. Wall, ledge, ladder, deep water

- Wall: see section 7. The check is `ForceAdjusted` read at the next tick. No bounce happens in 0x29, 0x04 or 0x2A (0x2A only clears the counter).
- Ledge: any state with `+0x144 == 0` goes to 0x2D at once (0x29, 0x03, 0x2A, 0x04). In 0x03 the airborne test comes before the ForceAdjusted test.
- Ladder: section 3.
- Deep water (slope 4): FACT, table 0x80022E7C.
  - 0x1C, 0x31, 0x3E: handler 0x80032658. Flip the direction, target 0x1D, TryUseItem (ignored).
  - 0x28 and 0x3A: TryUseItem only (0x80032680).
  - 0x0F and 0x1D: handler 0x80032690. TargetDirection = s1, TryUseItem, CheckEntityInteraction, then `pressed & 0xD0` gives 0x28, `s3` gives 0x0F, else 0x1D.
  - 0x3B: handler 0x800326FC.
  - All other states, 0x29, 0x03, 0x04, 0x2A included: target 0x1D, then TryUseItem (ignored), end. TargetDirection is not written.
  - No PlayerTryAttack is reachable in water.
- `Slope_18c` is the minimum slope over the four corner cells whose height + 1 equals `+0x1E0`. A corner at another height counts as 0 (0x80038110-0x80038348). So slope 4 needs all four corners on the water level.

## 10. Charge and attacks

- `UpdatePlayerWeaponEffect` (0x8002F49C), every tick on slopes other than 4. If `hold & 0x80` or `released & 0x80`: when the fiche byte 0 (chargeable) is nonzero and the byte `[0x80028D14 + 2 * TargetAnimationId]` is nonzero, `[0x80126FFC]` increments to 60. Otherwise it is set to 0. With no Square at all it is set to 0.
- The byte (named b0 by the lead) is 1 for animations 0, 1, 2, 3, 4, 0x0E, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x35 and 0x3E. All five run states count. The basic sword (fiche 0) has byte 0 = 0, so its counter stays 0.
- PlayerTryAttack is called only from the tail and from 0x29, so only there can a release fire. In 0x29 a release with counter ≥ 60 on the ground gives the charged attack (D-E20-9 air-loss rule applies).
- A release or hold in 0x03, 0x04, 0x2A or 0x3E: the counter drops to 0 the tick after, silently.
- The dash (0x04) is the only run state with an attack: balance record 0, `Attacks[5]` bytes (133, 2), attr 0x85 (type 5, bit 0x80 adds equipment), power 2 (from `BALANCE.BIN`, offset 0x19 of record 0). The other run states have attr 0. Frame boxes exist on all 8 frames of 0x04. `0x8003A7B0` (grass) is called for 0x04 each tick from the effects dispatch (0x8002FDC0).
- 0x3E carries `AnimFlags & 0x40`, so the hit pass skips the hero.

## 11. Sounds and effects (CreatePlayerAnimationEffects 0x8002FB14)

FACT; the function reads `+0x88` (the target of the previous tick). Dispatch tables:

| Table | Address | Index | Run entries |
|---|---|---|---|
| T1 | 0x800228AC | anim − 4 | 0x29 → 0x8002FB6C; 0x04 → 0x8002FDBC (grass, then falls to T2) |
| T2 | 0x800229CC | anim − 1 | 0x03 → 0x8002FE70; 0x04 and 0x2A → 0x8002FE68; gate: ForceX or ForceY ≠ 0 (0x8002FDD0) |

The E20.f2 skeleton (`AlundraPlayerAnimationEffects.cs`) already routes these correctly: `PlayerRunStepEffects` for 0x29, `PlayerWalkStepEffects` for T2.

Common parameters:
- Sound tables: run 0x80022814 = `[12,21,19,16,21,0,12,0]` by slope; walk 0x800227F4 = `[0,20,18,16,0,0,0,0]` for the first eight.
- Dust effect id: 6 on slopes 1, 2 and 4 (water table 0x80022864); otherwise the map header byte 0xA (7 on all maps in the corpus; 0 means no dust). Land table 0x80022824.
- Position: PosX, PosY, `+0x134` (floor Z). The pseudo-random state is `0x80098708`, LCG `seed * 0x7D2B89DD + 0xE06A02E7`.

Rules:
- 0x29: step sound S = table[slope] when `FrameCounter & 7 == 0`, with no force gate. Dust only if `+0xB0 − 1 >= 0`; `idx = min(+0xB0 − 1, 3)`; gate `FrameCounter & mask[idx]` where mask = `{7,7,3,3}` land and `{15,15,7,7}` water. Velocity uses `t = table[4 + idx]` and `f = AnimationDirection`:
  - X = `h[9+2f]*t + hi(seed1 * (h[8+2f]*t + 1))`
  - Y = `h[19+2f]*t + hi(seed2 * (h[18+2f]*t + 1))`
  - Z = `h[25]*t + hi(seed3 * (h[24]*t + 1))`
  - Three LCG draws, so the final state is seed3.
- 0x03: sound S when `FrameCounter & 7 == 0` (forces nonzero). Dust gate `FrameCounter & table[26]` (land 3, water 7). Velocity X, Y = `(Force * table[27]) >> 8` with `table[27]` = −128 land, −64 water (−0.5 and −0.25 of Force). Z = `table[30] + hi(seed * (table[29] + 1))`, which is 16384 + [0, 32767] on land and 0 in water. One draw.
- 0x04 and 0x2A: no sound (`s0 = 1`). Dust as 0x03 but multiplier `table[28]` = +32 land (+1/8 Force), 0 in water.
- The oracle journal: sound 12 in 0x29 on ticks 9, 17, 25 and in 0x03 on ticks 33, 41, 49; dust E7 on ticks 29, 33, 37 …; E6 in water.
- The effect code runs in the locked branch too (mode 1), so the scripted sprint of map 392 produces steps and dust once E20.e ports it. This changes that map when it lands.

## 12. O-E20-12 in detail

- Source: `0x80038064` (called from the pass at 0x800384BC). It sets `+0x1FC`-`+0x204` (hit box), `+0x120`-`+0x128` (column, row, level), `+0x134` (floor, via `0x80037F28`), and `+0x144 = !(floor < PosZ)` (0x800380F8-0x8003810C).
- For entities with `+0x6C & 0x100` (the hero) it reads the four corner cells (arrays at `+0x148` and `+0x158`) and sets `+0x180` (OR), `+0x184` (AND), `+0x188` and `+0x18C`/`+0x190`.
- Order inside the tick: MovePlayer, animation pass, physics (clear flags, forces, move, probes). So the contact MovePlayer reads at tick N is the one computed after the move of tick N−1.
- Run-specific consequence: leaving a ledge on tick N sets `gnd` = 0 at the end of N, and N+1 gives 0x2D.
- DLL (HEAD): `:1346-1351` takes `IsOnGround` from the engine at the head of `Update` for a non-airborne hero. `RunHeroProbes` (`:1501-1510`) is per tick and already refreshes slope, VRAM flags and `FloorHeight`. See change 2.

## 13. Stick rule (D-E20-15)

- Sites: only 0x80031FCC (start) and 0x8003219C (resume) read the dash table. The cone and the exits use the raw nibble.
- During the wind-up `TargetDirection` is the 8-way diagonal (facing keeps the axis by hysteresis). At the start tick the dash direction overwrites it.
- A stick below 0.5 on every axis gives `s3 = 0`: StopSprint with counter 1. A stick that crosses 0.5 on a second axis stays in the cone for the two neighbouring diagonals. The early resume window still clears on any bit, so stick jitter can kill the resume.
- No binary evidence for ε. The rule stays a port addition, measured in E20.d.

## 14. Port checklist (E20.d)

- Tail: add the Triangle hold test after the Cross edge and before the Idle/Moving write; set `AnimCompleteCounter = 0`.
- The five states as in section 5, with the real tail and the run states using the E20.a stubs (TryUseItem 1, PlayerTryAction 0, CheckEntityInteraction as today).
- The StopSprint counter is a hero-level counter in the proxy (not per tick stamped: the tick pad already ticks).
- `0x3E` flip and the value-1 exit; `0x1C` shares the handler.
- Slope-4 guard (change 1) and `IsOnGround` per tick (change 2).
- Arcs to pin, from the oracle: `R1` (start tick T+25, forces), `RES9`/`RES10`/`RES11` (window), `DIAG`, `TURN`, `CONE`, `DASHS`, `WALL`, `EDGEL`, `LEDGE`, `CLIMB`, `WSQ`, `WCRO`, `SWIM40` (the water guard replaces the 0x1D result).
- The drop of the HEAD "Triangle alone gives Idle" pin (`AlundraHeroJumpStatesTests.cs:216-219`).

## 15. Unknowns

- UNKNOWN: whether any map event sets 0x29, 0x2A, 0x04 or 0x3E on the hero. The survey found only the scripted anim 3 on map 392; I did not re-scan the corpus.
- UNKNOWN: corner sliding while running (E19.h). Oracle worlds are straight walls.
- UNKNOWN: the exact dust velocities in the oracle (the effect stub writes into a dummy slot; the code ran but I did not compare the slot).
- UNKNOWN: whether the engine's `IsGrounded` differs from the floor rule on stairs for the grounded hero (not measured).
- INFERENCE: the frame-count gap for O-E20-12 appears only with 2 or more ticks per frame, i.e. below about 50 fps.
