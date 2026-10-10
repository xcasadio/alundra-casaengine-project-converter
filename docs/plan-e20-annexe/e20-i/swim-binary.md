> Report returned by one agent of the read-only discovery for E20.i, swimming (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

**Things that change the plan**

1. **0x28 is a swim burst, not a dive.** FACT: nothing in the binary treats it as underwater. It lasts 42 ticks and chains to 0x1D. Speed is 176 against 144 for 0x0F, with instant acceleration (shift 0). The direction is frozen at the press tick. `MovePlayer` does nothing in 0x28 except `TryUseItem`. The animation byte 0xD is 0x00, so there is no invulnerability, and the attack table has no entry for the state. The row and recipe words "plongée / plonger" mislead. Say "élan de nage".
2. **The dive buttons are Triangle, Cross and Square.** FACT: the test is `pressed & 0xD0` (0x80032690 handler, test at 0x800326D0-0x800326E4). It is an edge on the tick pad. A held Triangle does nothing (oracle `R9-allwater-start-T`: 0x1D for 40 ticks). Circle (0x20) is not in the mask: it is item use. A Square press touching a talkable entity talks instead (`CheckEntityInteraction` returns 2, so the state becomes 0x1D).
3. **The exit from deep water is in the general table, not in the swim table.** FACT: the general dispatch 0x80022D3C sends 0x0F, 0x1D, 0x28, 0x39, 0x3C, 0x4E and 0x4F to 0x80031DD0, and 0x3A and 0x3B to 0x80031DB4. Both then write Idle (0) on the ground or 0x2D in the air, and call `TryUseItem`. The 0x80031DB4 route flips `+0x8C` by 0x10 first. If E20.i ports only the 0x8003260C branch, a swimmer who leaves slope 4 stays in a swim state forever.
4. **The row misses the water effects of the shadow pass (0x80038E84).** FACT: this pass runs every tick, after physics and before the hit pass (call site 0x8003B3E8). The decompilation `EntityManager.UpdateActiveEffects` agrees with my decode. For the hero it does three things:
   - It spawns effect 6 at (PosX, PosY, FloorZ) on any tick where `+0x18C` and `+0x190` differ and one of them is 4: both entering and leaving deep water.
   - It hides the shadow while the slope is 4.
   - It spawns effect 0x15 (21) when `FrameCounter & 7 == 0` and ForceX or ForceY is non-zero. This applies in any state while on slope 4.

   The E20.d oracle does not run this pass: `R9-deepwater-boots2` tick 89 shows only fx7. Expected values for these effects cannot come from the oracle.
5. **The 0x28 ripple is a separate effect.** FACT: in 0x28, every tick with `FrameCounter & 7 == 0` and a non-zero force, `CreatePlayerAnimationEffects` creates effect 6 at 0x8002FFE4-0x80030070. Its velocity is X = −ForceX/4 and Y = −ForceY/4, truncated toward zero. Landing from the air onto water belongs to E20.e: sound 21 (table 0x80022804) plus one effect 6 (0x80030260-0x800302AC).
6. **The branch is not reached in several cases.** Before the slope switch, the order is: frozen (`+0x20`), control lock (`[0x800DC4B8] & 0x34`), hurt entry, HP 0, then `+0x188 & 0x80`. That last test goes to 0x80032768 and sets 0x1D on slope 4 (Idle or 0x2D otherwise), with the pad ignored. 16 maps have deep cells carrying the belt bits (list in §7).
7. **U3 from `c2-binary.md` is resolved.** FACT (oracle C1, `e20-c2/oracle/out/C1.txt`): the hero translates during 0x3A and 0x3B at −3 px per tick, as in 0x31. The old "x constant" came from the 3×3-cell pool, not from the code. 0x800364C8 is the rider (`+0x12C`) pass, not slope-4 physics.
8. **The deep-water wall (O-E19-22 / Q-N3).** The swim branch itself never reads the boots level. Details and a recommendation are in §8.
9. **Hurt in water.** The pre-hurt slope `s2` is read at 0x80031B98, before 0x8002F120. 0x3A has accel byte 0x40 (hit pass skips the hero) and 0x3B has 0x44. I cross-checked the full timeline against C1: 0x3A at t, 0x3B at t+4, `+0xAC` set at t+12, 0x1D at t+13.

## 1. Method

- Decode: capstone on `ALUN_CD.EXE` (France), offset = 0x800 + addr − 0x80020000.
- Cross-reads: `AlundraEngine/Gameplay/PlayerManager.cs` (decompilation; it matched every handler I decoded), the exported hero records in `map_alundra.json`, `BALANCE.BIN.json`, and the cell data in `data-extracted/data/tiled/*.json`.
- Existing oracle traces re-read: `e20-d/oracle/out/g9/R9-*.txt` and `e20-c2/oracle/out/C1.txt`. I ran no oracle and did not touch the repository.
- Scratch: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-binary/`. Scripts there: `mdis.py`, `tab.py`, `scan.py`, `herodata.py`, `census.py`, `census2.py`.
- Pad bits (confirmed in `tools/world.py`): Triangle 0x10, Circle 0x20, Cross 0x40, Square 0x80. Direction nibble: Up 1, Right 2, Down 4, Left 8.

## 2. Entry: how the hero gets to slope 4

**The slope value.** FACT, the probe at 0x80038064, run for every entity at the end of the physics pass (0x800384BC):
- It loops over the four corner cells (pointers `+0x148`, levels `+0x158`, filled by 0x800370C4).
- A corner counts only if `level + 1 == +0x1E0`. A mismatching corner forces the result to 0.
- Otherwise the result is the minimum of `cellWord & 0xE00` over the four corners.
- `+0x18C = min >> 9` and `+0x190 =` the previous `+0x18C`. `+0x180` is the OR and `+0x184` the AND of the 32-bit cell words.
- A deep cell has bits 9-11 equal to 4 (`GroundProperty & 0xE` equals 8). One corner on land at the same level gives slope 0, which ends swimming.
- `MovePlayer` reads the slope of the previous tick's probe, so the first swim tick is one tick after the four corners are in.
- Evidence: `R9-deepwater-boots2`: tick 89 reaches slope 4 while the state is still 0x03; tick 90 is 0x1D.

**Order inside `MovePlayer` (0x80031B50).** FACT:
```
0x80031B98  s2 = +0x18C
0x80031B9C  0x8002F120()                 ; warp/exit-tile check, reads +0x184
0x80031BA4  +0x20 != 0  : [0x80126FFC]=0 ; [0x80127110]=0 ; END
0x80031BC4  locked (& 0x34) : CreatePlayerAnimationEffects(1) ; charge/pointer = 0 ; UPCE(1) ; 0x800350C0 ; END
0x80031C30  +0x6C |= 0x100 ; CreatePlayerAnimationEffects(0)
0x80031C44  s1 = 0x80031A68()            ; hurt entry; != -1 : hurt block -> +0x88 = (s2==4)?0x3A:0x31
0x80031CA8  +0x14 == 0 : death branch 0x800327A4
0x80031CB8  +0x188 & 0x80 : 0x80032768    ; forced 0x1D on slope 4 (0x80032780-0x80032834)
0x80031CC4  0x8003634C()                  ; item pilot
0x80031CD0  s3 = hold >> 12 ; s1 = 8way[s3] (table 0x80022C6C) or +0x8C when -1
0x80031CFC  s2 >= 8 : END ; else jr slopeTable[s2]    (slope 4 -> 0x8003260C)
END 0x80032914: 0x800307E8 (UpdateItemEffectState), 0x8004DC68(+0x18), 0x8004DD30(+0x14)
```

**What the slope-4 branch does and skips.** FACT:
- Every tick, before the state switch (0x8003260C-0x8003261C), it does `[0x80126FFC]=0` (charge), `[0x80127110]=0` (pending weapon pointer) and `UpdatePlayerCarriedEntity(2)`.
- Mode 2 (0x8002FAB4-0x8002FAEC) works on the carried entity (`[0x80127D5C]` = hero `+0x2C`). It writes `carried.+0x24 = −1`, copies the hero's `+0x8C` and position, and raises Z by 0x200000. It does not clear the hero's `+0x2C`. That ownership belongs to E20.g, where this call stays a named stub.
- It skips the general prologue at 0x80031D68-0x80031D7C: `UpdatePlayerWeaponEffect` (0x8002F49C), `UpdateWeaponStepProgression` (0x8002F768) and `UpdatePlayerCarriedEntity(0)`.
- It also skips the slope-6 climb gate and the common tail 0x80031EA8.
- So in water there is no `PlayerTryAttack`, no `PlayerTryAction`, no charge counting, and no Triangle wind-up.
- State ≥ 0x50 jumps to 0x80032758.

**Boots do not gate the branch.** FACT: the only readers of `[0x80127000]` are:
- 0x80031AD0: terrain damage.
- 0x800368BC: gravity.
- 0x800369E0: shallow-water slow.
- 0x800374FC: the deep-water wall (§8).

None is on the swim path. The boots level is set at 0x800307E8: 3 if the hero owns item 0x1C, 2 for 0x1B, 1 for 0x1A, else 0.

## 3. Swim table 0x80022E7C (index = state, `+0x88` before the tick)

| Handler | States | Behaviour |
|---|---|---|
| 0x80032674 | 00-0C, 0E, 10-19, 1F-26, 29-30, 32-36, 39, 3C, 3F-42, 44, 46, 49, 4B, 4E, 4F | `+0x88 = 0x1D`; `TryUseItem` (result ignored); END |
| 0x80032658 | 1C, 31, 3E | `+0x8C = (+0x8C + 0x10) & 0x1F`; `+0x88 = 0x1D`; `TryUseItem`; END |
| 0x80032680 | 28, 3A | `TryUseItem` only; END |
| 0x80032690 | 0F, 1D | steering handler (§4) |
| 0x800326FC | 3B | hurt-end handler (§6) |
| 0x80032758 | 0D, 1A, 1B, 1E, 27, 37, 38, 3D, 43, 45, 47, 48, 4A, 4C, 4D, and any state ≥ 0x50 | `0x800350C0` (cancel the active item mode), then END; state unchanged |

These 15 states are the sleeping, mine-cart, iron-flail-charge and similar states. The general table sends them to the same 0x80032758.

## 4. Per-state rules (what a port must reproduce)

The records below come from `map_alundra.json` (hero record). All four facings have identical timings.

| State | Speed | Shift | Sfx | Frames | End |
|---|---|---|---|---|---|
| 0x1D | 0 | 3 | 0 | 4×9 = 36 | loop (silent restart) |
| 0x0F | 144 | 4 | 22 | 6×7 = 42 | chain to 0x0F |
| 0x28 | 176 | 0 | 21 | 6×7 = 42 | chain to 0x1D |
| 0x3A | 256 | 0 | 23 | 4×1 = 4 | chain to 0x3B |
| 0x3B | 0 | 4 | 0 | 4×2 = 8 | hold, `+0xAC = 1` |

The loop and chain rules come from `UpdateAnimation` (0x80038AB4):
- A chain writes `+0x88 = n` and reloads, even when n equals the current animation. It replays the Sfx and increments `+0xB0`.
- A "loop" terminator restarts the frame list without reloading and without sound.

**0x1D and 0x0F, handler 0x80032690** (FACT, validated by `R9-allwater-start-R` rows 1, 2 and 44, and by `R9-deepwater-boots2` rows 89-106):
1. `+0x8C = s1` is written in the delay slot of the call to `TryUseItem`, so it happens before the call. It is written every tick, including when no direction is held (`s1` is then the old `+0x8C`).
2. `TryUseItem() == 0`: END with the state unchanged.
3. `c = CheckEntityInteraction()` (0x8002E910):
   - 0: next step.
   - 1: END with nothing changed. A touched talkable entity freezes the state, so a swimmer already in 0x0F keeps swimming whatever the pad does.
   - 2: `+0x88 = 0x1D`, END. This is a Square press on the entity (`[0x80127108]` set).
4. `pressed & 0xD0` non-zero: `+0x88 = 0x28`.
5. Otherwise `+0x88 = 0x0F` if `s3 != 0` (any direction bit, even contradictory ones), else `+0x88 = 0x1D`.

**Kinematics** (0x80036614, 0x800367E4; direction tables 0x80023654/0x80023694):
- Target = vector × Speed. Cardinals: X ±768, Y ±512. Diagonals: X ±543, Y ±362.
- Step = `|target − Force| >> shift`, computed only when Speed, `+0x8C` or the shift changes.
- Deep cells carry no ice or shallow flag, so the generic ice and shallow-water modifiers do not apply.
- The tick order is MovePlayer, then the animation pass, then physics. The state written at tick T already drives the physics of tick T.

| State | X target | Y target | Diagonal (X, Y) | Step rule |
|---|---|---|---|---|
| 0x0F | 110592 | 73728 | 78192, 52128 | from rest 6912 (X) / 4608 (Y); 16 ticks |
| 0x28 | 135168 | 90112 | 95568, 63712 | instant |
| 0x1D | 0 | 0 | | `Force >> 3` at the switch |
| 0x3A | 196608 | 131072 | 139008, 92672 | instant |

Measured on the oracle: tick 90, 0x1D, ForceX 225792 (= 258048 − 258048>>3). Tick 91, 0x0F, 218592 (step 7200), sound 22. Reaches 110592 at tick 106. Starting in water: 0x1D at tick 1; then 6912 at tick 2, +6912 per tick; sound 22 again at tick 44 with `b0 = 1`.

**0x28 timeline** (INFERENCE from the decode; no repository oracle trace has a dive). Let T be the press tick:
- At T: the state is 0x28, `+0x8C = s1`, sound 21 plays at load, and Force jumps to the 0x28 target for that direction.
- The state stays 0x28 through T+41.
- At T+42 the animation pass chains to 0x1D; MovePlayer of T+43 sees 0x1D.
- Pressing Triangle with no direction held bursts in the old direction (`s1` falls back to `+0x8C`).
- A new press at T+43 or later redives.
- ForceAdjusted (`+0x13C`) is never read: a swimmer against a wall just stops.

## 5. Effects and sounds

FACT, with `CreatePlayerAnimationEffects` at 0x8002FB14. It reads `+0x88` as left by the previous tick. Its dispatch tables are T1 0x800228AC, T2 0x800229CC and T3 0x80022A74; 0x0F, 0x1D, 0x28, 0x3A and 0x3B take the default entries.
- **0x0F and 0x1D:** no per-tick effect, only the animation-load sounds above.
- **0x28:** the effect-6 ripple of §0.5. The gate is non-zero ForceX or ForceY and `FrameCounter & 7 == 0`. Ripples fire on ticks T+1 to T+42, because the head of tick T still sees the old state.
- **0x3A, 0x3B:** no effect. Sound 23 plays at the 0x3A load.
- **Generic tail** (0x800303D0 onward, state-independent): the low-HP warning sound 0x190 once HP ≤ MaxHP/5. Not swim-specific.
- **Sound id 0 is silent:** 0x800490FC returns at once for `a0 <= 0`.
- **Shadow pass (0x80038E84), hero is included:** the three actions in §0.4. It requires the hero's `+0x1CC & 3 != 3`, `+0x6E & 7 != 0`, `+0xB4 & 0x10 == 0` and `+0x28 == 0`. Its slope-4 case sets the shadow entity's `+0x68 = 1`, then the 0x15 wake.

## 6. Hurt states

Entry (FACT, 0x80031C44-0x80031C9C, validated by C1):
- On a non-(−1) result of the hurt entry, it does: charge = 0, pointer = 0, `UpdatePlayerCarriedEntity(2)`, the pilot 0x8003634C.
- Then `+0x88 = 0x3A` if the slope read at 0x80031B98 is 4, else 0x31.
- Then `+0x8C = s1` and `+0x1CC = 0x78`.

Handlers:
- **0x3A** (0x80032680): `TryUseItem` only. The animation chains to 0x3B after 4 ticks.
- **0x3B** (0x800326FC):
  - `TryUseItem() == 0`: `+0x8C` flips by +0x10; END.
  - Otherwise, if `+0xAC == 1`: `+0x88 = 0x1D` and `+0x8C = (+0x8C + 0x10) & 0x1F`; else END.
- On slope ≠ 4 (0x80031DB4): `+0x8C` flips, then Idle or 0x2D, then `TryUseItem`. This is the exit when the knockback leaves the water.

C1 trace, Beannoid, boots 0x1B, hurt at t=2:

| Tick | State | ForceX |
|---|---|---|
| 2 | 0x3A | −196608 (direction 24→8) |
| 6 | 0x3B | −184320 |
| 14 | 0x3B | −86016 (`+0xAC` set) |
| 15 | 0x1D | −75264 (direction back to 24) |
| 22 | 0x1D | 0 |

The hero moves 35 px west. Total hurt length is 13 ticks, against 15 on the ground.

Death in water (E18's table 0x80022FBC): from a swim state, HP 0 gives 0x3C on slope 4 (0x800328F8) and 0x39 otherwise. 0x3B is the exception: its entry at 0x800328CC leaves the state unchanged if `+0xAC` is 0, then goes to 0x3C once `+0xAC` is set. The decompilation writes 0x39 there (a decompilation loss).

## 7. Exits, and what is possible in water

**Exits** (FACT):
- Slope ≠ 4: Idle or 0x2D via 0x80031DD0 (§0.3). Force is not reset, so Idle's shift-1 deceleration continues onto the shore.
- Slope < 4 happens as soon as one corner leaves the deep cells. A shore cell one height unit higher than the water is blocked by the mover (cell level ≥ `+0x1E0`). Census: 54 maps have 10644 deep cells. In maps 416, 5 and 14 most shore edges are 0 to 1 height units above the water, so which edges are actually walkable is a mover question (UNKNOWN).
- The slope-6 climb gate (0x80031D20) still runs before the slope-6 case. An adjacent ladder strip would give slope 6, not 4.

**Possible in water:**
- `TryUseItem` runs in every water state (even 0x28, 0x3A, 0x3B and the default). It does `if pressed & 0x20 (Circle)`: if `[0x800DC4B8] == 0`, it calls the item starter 0x8003499C; otherwise it plays sound 3. It returns 1 when no item was started.
- Item starters, from my scan of 0x80034224-0x800349A0. These belong to E20.h, listed here only for the interaction with swimming:

| Item | Rule |
|---|---|
| 0x1F (0x80034224) | refused on slope 4: sound 3, returns 1 |
| 0x20 (0x80034320) | needs slope 3 and `+0x88 < 2` |
| 0x23 (0x8003453C) | refused on slope 4 |
| 0x24, 0x25, 0x26, 0x27, 0x29 | no slope or state test; usable in water |
| 0x2B-0x32 spells (0x800348E0) | need `+0x88 < 2`, so impossible in 0x0F, 0x1D or 0x28 |
| 0x21, 0x22, 0x28, 0x2A | sound 3 |

- The spell and item drivers cancel at once on slope 4 (helper 0x800351B8 returns 1 and calls 0x800350C0 unless the state is 0x32 or 0x33 off water).
- `PlayerTryAttack` and `PlayerTryAction`: never called in water. The hero's attack table has zero attr for 0x0F, 0x1D, 0x28, 0x3A and 0x3B.
- `CheckEntityInteraction`: only in 0x0F and 0x1D.

**Currents (not in the row):**
- `+0x188` is computed at 0x80038224-0x80038338 from the cell under the hero's centre. The lock bit 0x80 comes from cell-word bit 0x800000, and the direction from bits 20-21.
- The generic drift tables 0x80028B94 (X) and 0x80028BD4 (Y) are read in 0x800366FC.
- Deep cells with belt bits (byte2 & 0xC0) exist in maps 3, 4, 5, 6, 8, 15, 19, 20, 49, 77, 78, 79, 335, 416, 435 and 479. A level-matched cell needs bit 0x40000 and an unmatched one needs 0x80000. The byte2/byte3 convention (belt cell uses `byte3<<20 + 1`, ordinary cells `(byte3−1)<<20 + 1`) is not resolved.
- UNKNOWN: whether any of this is ported in the DLL.

## 8. Deep-water wall without boots (O-E19-22 / Q-N3)

FACT:
- The mover picks its blocked-corner predicate at 0x80037764-0x80037774: the hero uses 0x80037488, others 0x800373E4.
- In 0x80037488, for each of the four corner cells of the candidate position (not just the hero's level):
  - It blocks the corner if `cell & {0x40 (+0x1000 if hero flag 1, 0x41 if flag 8)}`, or if the cell level ≥ `+0x1E0`.
  - It also blocks if `bootsLevel < 2 && (cell & 0xE00) == 0x800` (instructions 0x80037504, 0x8003753C-0x80037560). This test ignores levels.
- Mode `[0x80127164] == 0x20` adds a block on any slope other than 3 at the hero's level. That is unrelated to water.
- Oracle: `R9-deepwater-noboots` shows ForceAdjusted at tick 86, 0x3E at 87, x = 325.00. `R9-deepwater-boots2` swims.

**What E20.i needs.** Nothing for the state machine to work: it needs only `Slope_18c == 4`, which the DLL already computes. For fidelity it needs one predicate in the hero's corner-block test, fed by the boots level (items 0x1C, 0x1B, 0x1A, probably the value E20.c2a's `UpdateItemEffectState` already holds).

**Recommendation.** Fold O-E19-22 into E20.i as its last task. Without it, the starting hero (no boots) swims anywhere, which the original never allows. With it, E20.d's "le héros marche sur l'eau profonde" disappears and `R9-deepwater-noboots` becomes the oracle.

On "collisions" in Q-N3: D-E20-24 already reads it as E20.g (crates and jars). The deep-water wall is a terrain-cell collision and not covered by that reading, so the author should rule on it.

## 9. Port checklist and test values

1. In the slope-4 branch (§2-§3): reproduce the clears, the mode-2 stub, and the six handler groups.
2. In the general table: add the exit entries for 0x0F, 0x1D, 0x28, 0x3A, 0x3B (§0.3).
3. In the hurt block: choose 0x3A when the pre-hurt slope is 4 (E20.c2b).
4. Add clocks for 0x0F, 0x1D, 0x28, 0x3A, 0x3B, and tests for:
   - `R9-deepwater-boots2` ticks 89-110;
   - `R9-allwater-start-R` (ticks 1, 2, 44);
   - `R9-allwater-start-T` (no dive on a held Triangle);
   - C1 ticks 2-26.
5. Invert the E20.d1 tests that pin the stand-in guard (Idle after the corners reach slope 4).
6. A dive test needs a `TryUseItem` hook and a `CheckEntityInteraction` of 0 (as the E20.d1 hook does for `TryUseItem`).
7. The shadow-pass effects need an oracle that includes 0x80038E84. The existing oracle does not.

## 10. Unknowns

- UNKNOWN: whether the DLL already ports the shadow pass's effect 6 (transitions) and effect 0x15 (wake).
- UNKNOWN: the exact semantics of the belt cell word at 0x80038224-0x80038338 (level-match convention).
- UNKNOWN: the DLL's boots-level source.
- UNKNOWN: per-edge walkability of shores above water level.
- UNKNOWN: rendering, depth order and shadow draw while swimming.
- UNKNOWN: 0x28 numbers are decode-derived and not oracle-measured.
