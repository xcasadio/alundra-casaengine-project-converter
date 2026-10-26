# E20.g2c3 - T0 (d): the ticks the BINARY gives (written BEFORE any probe of the DLL)

Plan: `docs/plan-e20-heros.md` section 1.24.5, "E20.g2c3" (revision 2, READY), T0 (d) and (e). Written 2026-10-26 from the replays of `replay/` (identical to the reading of `../e20-g2c3-disc/`)
and the facts F1 to F28 of the reading and its critique. **No probe of the DLL had been run when this file was committed** (the commit precedes `probes/`).
These are predictions of the binary; the probes of the DLL confirm them or write the difference (D-G2C3-8: a one-tick difference with its cause read in the code, pinned by a named test; no cause, a stop; a missing engine feature, a stop).

Conventions. A tick `tN` of the replay is the tick whose END state is printed. `R` = the tick of the restoration (the state-4 handler writes state 0, `ProgramIndexes[2] = Bytes[1]`, `PosZ += 160 px`);
for the first cycle of a native drop `R` = t1 (the first pass of C71, state 0 -> the printed state 0 of t001; A19 ran before it). `S` = the tick whose end state is the first state 1 of a scripted drop. `d` = the tick whose end state is the first state 2 (animation 1, Gravity) of the drop in a contact run.
"State k at tN" = `Bytes[0]` (`+0x274`) reads k at the end of tN.

## 1. The native cycle (replay `out_s1_native.txt`, flat floor, the drop placed 160 px above it, gravity 128, viscosity 4096), three cycles

| What | Cycle 1 (R = t1) | Cycle 2 (R = t89) | Cycle 3 (R = t177) | Relative to R |
|---|---|---|---|---|
| state 0 (restored / armed) | t1 | t89 | t177 | R |
| state 1, counter `AIValues[1] = 40` | t2 | t90 | t178 | R+1 |
| state 2, animation 1, Gravity (`Flags` 0x32080 -> 0x32180), `ForceZ` -32768 | t42 | t130 | t218 | R+41 |
| landing: `IsOnGround = 1` and `CollidedWithEntityZ = 1` at the SAME tick, Z = 0 | t66 | t154 | t242 | R+65 (24 ticks of fall after R+41) |
| state 3, animation 2, sound 145 | t67 | t155 | t243 | R+66 |
| state 4, `Collidable` removed (`Flags` 0x32100) | t68 | t156 | t244 | R+67 |
| `ForceResetAnimationFlag` (`+0xAC`) = 1 (end of the `Hold` of 7 frames of 3 ticks = 21 ticks after R+66) | t88 | t176 | t264 | R+87 |
| restoration: state 0, animation 0, `ForceZ = 0`, `Flags = 0x32080` (Gravity out, `Collidable` back), `PosZ` + 160 px (Z = 160) | t89 | t177 | t265 | R+88 |

Period of the native cycle: **88 ticks**. Sound 145 once per cycle, at R+66. (The sound 0 printed at the restoration and at the state 2 in the replay is the animation-change sound slot of the machine, not a sound of the game: not asserted.)

## 2. The period of each family of scripted programs (the restoration re-arms the script from its first opcode)

Scripted drop with `37 [N] ; 40 [2,0]` (replay `out_s1b_scripted_N30.txt` for N = 30, the harness model of the Wait of F5): first state 1 at S = t33, state 2 at S+40, landing at S+64, state 3 at S+65, state 4 at S+66, `+0xAC` at S+86,
restoration at S+87, and the next opcode `40` runs at the restoration + N + 2, so the next state 1 is at **S + N + 90**.

| Family | Count | Period (restoration to restoration, steady state) |
|---|---|---|
| simple wait `37 [N] ; 40 [2,0]`, N from 5 to 240 | 33 | **N + 90** (state 1 to state 1) |
| flag programs of the maps 87, 88, 92 (6 of the 8: wait A then set the flag, or wait B when set) | 6 | N + 90 with the wait the flag selects |
| flag programs that skip their waits once the flag is set (map 87 record 53 / C[54]; map 92 record 25 / C[26]) | 2 | first cycle as above, then **1 + 88** (= 89) |
| the 14 programs of the map 158 (`00 ; 3B [box] ; 04 -8 ; 37 [N] ; 40 [2,0]`), the hero staying inside the box | 14 | **N + 91** |
| the native template (map 92 record 15, no C program), born by the `0x8A` of B[1] | 1 (x4 born) | **88** (the table of section 1) |

For the map 158 with the hero outside the box: the program loops on `3B` and the drop does not leave state 0 (hangs motionless); it starts when the hero enters the box.

## 3. The three contacts (replays `out_s2_carry.txt`, `out_s2_nocarry.txt`, `out_s2_posed.txt`; `d` = the tick of the state 2)

| Event | Hero carries a torch (`carry`, d = t42) | Torch posed (`posed`, d = t42) | Hero without a torch (`nocarry`, d = t42) |
|---|---|---|---|
| landing, `IsOnGround = CollidedWithEntityZ = 1` | d+22 (t64, on the head, Z 32) | d+23 (t65, on the torch, Z 15) | d+22 (t64, on the head) |
| the DROP's `RidingEntity` (+0x12C) = hero / torch, state 3, animation 2, sound 145 | d+23 (t65) | d+24 (t66) | d+23 (t65) |
| torch status 3 and `Bytes[0] = 1`; the drop in state 4 with `Collidable` out | d+24 (t66) | d+25 (t67) | no torch: nothing written; the drop in state 4 at d+24 |
| torch animation 2, sound 142 | d+25 (t67) | d+26 (t68) | none |
| torch `ForceResetAnimationFlag = 1` | d+27 (t69) | d+28 (t70) | none |
| torch status 4, effect 10 attached, the hero's carried pointer cleared (`ClearCarrierOf`) | d+28 (t70) | d+29 (t71) | none |
| torch freed (status 0), no flame (no entity of type 12) | d+29 (t71) | d+30 (t72) | none |
| the hero: state 0 (standing), hands empty, hp unchanged (10) | d+29 (t71) | unchanged (state 0) | unchanged (state 0), hp 10 |
| the drop reaches Z = 0 (it falls through the hero) | d+34 (t76) | d+32 (t74) | d+34 (t76) |
| the drop's restoration (Z = 160) | d+45 (t87) | d+46 (t88) | d+45 (t87) |

The window (replay `out_s3_hit_window.txt`): the drop puts a carried torch out when its centre falls at dx from -11 to +12 px and dy from -8 to +8 px of the hero's centre (24 x 17 offsets).
The thrown torch bumping into a drop is not in the replays (E20's `+0x130` read, the branch of g2c2): the torch is put out without a flame.

## 4. The program B[1] of the map 92 (event 0, `EventCodesBIndex` 129, tiles x 26 to 42, y 8 to 28)

The first call of B[1] is at `z0` (the first tick the hero is in the zone; outside the zone the program is reset every tick). `37 [240]`, `37 [240]`, `37 [120]`: each wait advances at its first call + N + 1 and the next wait's first call is the same call,
so the four `0x8A [138, 15, x, y, 240 px]` run at **z0 + 603** (603 = 241 + 241 + 121 ticks elapsed since the first call). The drops are born at (756, 168), (804, 168), (852, 168), (900, 168), Z 240 px, over the torches 43 to 46 (Z 80).
The torches go out about 67 ticks after the birth (state 3 at R+66, the torch written at R+67 for a drop falling from the torch's own height; here the fall is longer: about 670 ticks after z0 in the critique; **not tick-exact**, no replay has this height). The test (w8) pins the birth tick z0 + 603 and "all four torches out without a flame before z0 + 603 + 90".
Leaving the zone before z0 + 603: no drop (the program is reset every tick outside). Coming back: every re-entry makes four more drops at the same points (O-G2C3-2, out of the slice).
