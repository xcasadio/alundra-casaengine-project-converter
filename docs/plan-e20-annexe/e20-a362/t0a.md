# E20.A362 T0a: the ticks of the scene written before the scene ran (2026-10-26)

Written by T0a of E20.A362 (`docs/plan-e20-heros.md` section 1.30.1a), branch `chantier/theme2-attaques-objets`. **Nothing in this file comes from running the scene in
the DLL**: the scene of the map 362 has not been loaded by `ArcRun` at the time of this commit (the smoke is T0b, a later commit). Sources: the bytecode of the map 362
(`../e20-a362-disc/discovery/out/map362_programs.txt`), the rules of `EventProgramRunner` read at HEAD `bf1dcef5` (the same as `8174c0cb` for the code of production lines
used here), the critic's keys replayed below, the data of the sprite records.

Convention (the one of E20.c1b-2): a "tick" is a logic tick, one frame of `ArcRun`; `FrameOf(slot, pc)` is the frame of the FIRST execution of the pc (a suspended
instruction counts from its first call); a gap is the difference of two `FrameOf`. Slots: A 0, B 1 (map programs), C 2, D 3, E 4, F 5. Status of a row:
**BYTECODE** (derived from the opcodes and `Wait n` = n + 1 alone), **KEY** (read in the oracle key of the critic, replayed in (b)), **RULE** (a rule of the DLL
tick order, written in the plan or in the code), **INFERENCE** (not asserted by the arc: recorded, never waited on).

## (a) The reading is paid

`../e20-a362-disc/`: the two scratchpad folders (`discovery/` = `a362-h3a`, `critic/` = `a362-h3a-critic`), README, `SHA1SUMS.txt`, `omitted.tsv`.

## (b) The outputs of the critic, replayed from a copy

The critic's `src/` was copied to a scratch folder and run with `runpy.py`, outputs written to `replay/`. The four arguments the outputs do not carry were found by
trying the neighbours until the file was identical (the first lines tell the T4 of the run): all five outputs are byte for byte identical.

| output | command (from the copy of `critic/src`) | result |
|---|---|---|
| `crit_olen_real_flat_T5-120.txt` | `c_olen_real.py 136 5 120 140` | identical (`cmp`) |
| `crit_olen_real_hover_T4-32_T5-140.txt` | `c_olen_real2.py 136 32 140 165` | identical |
| `crit_olen_lid_flatfield_passthrough.txt` | `c_olen_lid.py 212 100 204 136 32 5 120 174` | identical |
| `crit_olen_flight_slide_no_C62.txt` | `c_olen_flight2.py 32 0x10 160` | identical |
| `crit_w5_lid.txt` (= `discovery/out/k_w5_lid.txt`) | `k_w5_lid.py 5 20` | identical to both |

What the key says (and that the table below uses): with the real cells of the 362, the bomb born at (204, 136, 64) facing north, `Set anim 2` poked after tick T4 and
`Set anim 1` after tick T5: it lands about 24 ticks after T4, then slides one pixel per tick and stops at **(204, 103, 32)** against the north wall, inside the footprint
of the lid (y 100 to 108); `Set anim 1` is in force at T5 + 1 (**= A**; the sound 46 is requested at A), the frame box is absent up to A + 15 and present from
**A + 16**, and at that very tick the lid's `+0x224` (`TouchingEntity`) is set and the three effects 9 are emitted. `+0x224` is the same at T4 = 5 (flat) and T4 = 32
(hover), and the position of rest is the same.

## (d) The table of the ticks expected

`derive_ticks.py` (output `derive_ticks.out.txt`) walks the listing and gives the offsets of the first execution of each pc. S is the tick of the opcode `0x8B` of Olen
(`C[4]` `@569`); A the tick of the bomb's `Set anim 1` (`C[5]` `@641`); H the tick of the hit pass that touches the lid.

| # | event | tick | status |
|---|---|---|---|
| 1 | Olen `C[4]` `@558` (Wait 10), `@562` (Wait 30), `@569` (`0x8B`, S) | `@558` to `@562`: **11**; `@562` to `@569`: **31** | BYTECODE |
| 2 | `@569` to `@582` (Wait 30 at `@578`, then `0x1A`, T4 `Flag on`) | **31**: T4 is set at S + 31; `@582`, `@585`, `@588` run in that one tick | BYTECODE |
| 3 | the bomb (record 5) | absent at the end of frame S - 1, present in the world at the end of frame S; position = Olen's own (X, Y) at S, Z = Olen's Z + 32 px; target direction `0x10`; target animation 0 | RULE (`0x8B` of the DLL, ADR-0074) |
| 4 | Olen's Y at S | 136 to 138 (the walk of 16 px stops once beyond the radius, at 1.5 px per tick) | INFERENCE (recorded, not asserted) |
| 5 | the bomb's `C[5]` `@628`, `@629`, `@632` | S + 1: an entity born by the program of another entity takes part from the next tick; not asserted more finely than "from S + 1 to S + 3" | RULE (`EventProgramRunner.JoinTheBirthTick`, `WorldProxy.CollectTickEntities`) |
| 6 | the bomb sees T4: `@635`, `@637`, `@638` | **S + 31**, the same tick as T4: gap(`C` `@582` to `C` `@635`) = **0** (the bomb is born after Olen: its slot is higher, the programs run in slot order) | BYTECODE + RULE |
| 7 | the slide | from S + 31 (S + 32 at the latest) the bomb's Y decreases **one pixel per tick**; at rest at **X = Olen's X (204 +/- 1), Y = 103 +/- 1, Z = 32** (the north wall, rows above 96 are walls); at rest by S + 66 at the latest; no change of X, Y or Z from S + 70 to A | KEY (flat T4 = 5: Y 135 at the first tick, 103 from the 33rd tick; hover T4 = 32: the same) + INFERENCE of the correspondence oracle tick T4 + 1 to the DLL tick S + 31 |
| 8 | T5 and A | Olen sets T5 at `C[4]` `@608`; the bomb sees it in the same tick (slot order): gap(`C` `@608` to `C` `@641`) = **0**, A = `FrameOf(C, 641)`. The distance S to A is not fixed by the bytecode (a walk of 72 px, an animation of Olen): recorded near S + 140, never waited on | BYTECODE + RULE; the value INFERENCE |
| 9 | at A | the animation 1 of the bomb is in force, the sound **46** is requested once at A (and not before) | KEY |
| 10 | the frame box of the bomb | absent at A + 15, present at **A + 16** = H | KEY |
| 11 | H, at the end of the tick (the hit pass) | lid: `TouchingEntity` = the bomb, `FrameCollisionTickCounter` = **25**; bomb: `HitCounter` = 1; **three** effects 9 (sparks); the lid's status is still 2 (Normal) | KEY (`+0x224` set at T5 + 17 = A + 16, effects `(0, 9)` x 3) + RULE (HitPass) |
| 12 | the lid's `D[1]`: `@728`, `@731`, `@734`, `@737` | all four at **H + 1** (gap 0 between them); T6 (`0x8006`) set; the lid's status **3** (Deactivated) at the end of H + 1 | BYTECODE + RULE (the program D of the target runs at the tick after the hit) |
| 13 | `B[4]` sees T6: `@171` (Break) | **H + 2** (a map program sees at the tick after a flag set by an entity) | RULE (map events come first in a tick, c1b-2) |
| 14 | `@172` (`0x8E`) | **H + 3** (the Break ends the tick of `@171`; the critic's correction: H + 3, not H + 2) | BYTECODE |
| 15 | `@179` (`0x8F`) | **H + 64** (`@177` Wait 60 = 61 ticks from H + 3) | BYTECODE |
| 16 | the four births: `@182`, `@192`, `@202`, `@212` (`0x8A`) | **H + 125, H + 156, H + 187, H + 218** (Wait 60, then three Wait 30 = 31 ticks); the records 6, 7, 8, 9 are present from that tick, not before | BYTECODE |
| 17 | the birth positions | X, Y exact: (204, 104), (216, 104), (204, 104), (216, 104); Z bytes 32, 32, 35, 48 px: recorded, not asserted (the newborn joins the tick of the map events and the physics runs on it) | BYTECODE for X, Y; INFERENCE for Z |
| 18 | the end of the arc | after the fourth birth, at **H + 218**; the arc stops at H + 248 at the latest: `@222` Break runs at H + 249, `@223` (dialogue 133) at H + 250, T7 (`@228`) after it | BYTECODE |
| 19 | the lid, the pin (D-A362-1) | from H + 1 to the end: present, status **3**, position (212, 100, 32) unchanged; **no** sound 230 (`0xE6`) and **no** break effect 11 requested; not destroyed | KEY (the same state with the native E19 gives sound 230 and effect 11 one tick after the status 3: absent here because `(E, 19)` is not registered) |
| 20 | the bomb, the pin (D-A362-1) | status 2 until its animation ends (the end flag at A + 51 in the key); from **A + 53** to the end: still present in the world, status **3**, invisible; never destroyed. Not asserted: A + 51 to A + 52 | KEY (the native E12 would give status 4 at A + 52 and a free slot at A + 53) |
| 21 | the hero (a11) | `TouchingEntity` null and life unchanged for the whole run; the hero is walked 48 px south by `B[3]` and is more than 60 px from the explosion | KEY (hero thresholds) + BYTECODE |
| 22 | witness (n1), no G248 | after the talk: dialogue 131, `C[4]` loops back (`@532`); record 5 never present, T3 never set, for at least 300 frames after the talk | BYTECODE |

Notes. (i) Rows 6, 8 and 12 to 16 are the ones the plan names as the chain to compare "ticks written before the execution"; every gap is exact except rows 4, 7 and
17 (Z), which are INFERENCE and are only recorded. (ii) The bomb is not Collidable (flags `0x13234E`): it goes through the lid (key `crit_olen_lid_flatfield_passthrough`).
(iii) `0x8E` and `0x8F` have a degraded fallback when the world has no camera sway service: if `ArcRun` has none, `SkippedOrExceeded` lists them; T1 reads which.
(iv) A discrepancy between the arc and rows 1, 2, 6, 11 to 16 or 19 to 20 is a STOP of the plan, not a number to redo.

## (d2) The geometry of the witness (n2)

`sword_geometry.py` (output `sword_geometry.out.txt`) reads the boxes of the hero's basic sword (animation 0x10, direction right) from the decoded sprite record and applies
`HitPass.AxisOverlaps`; the model first reproduces the c1a oracle sweep (contact at the first tick for dx -9 to 38 against the Beannoid body).

- The stroke, facing east: ticks 1-4 box (0, 0, 0) 29 x 15 x 15; ticks 5-6 (21, -8, 0) 29 x 15 x 15; ticks 7-11 (22, -16, 0) 23 x 15 x 15; then (-5, -18, 0) 23 x 15 x 15.
- The lid's body, after the shift of `C[3]`: x 189 to 237, y 100 to 108, z 32 to 76 (header offset (-23, 0, 0), size 48 x 8 x 44, at (212, 100, 32)).
- At the first tick of the stroke the box overlaps that body for a hero at x 161 to 236, y 86 to 107, z 18 to 75.
- **Position and facing of (n2): hero at (176, 100, 32), `TargetDirection` 24 (east).** Margins: 15 px from the low edge in x, 14 and 7 px in y; z = 32 is the level 2 floor. The cell
  (7, 6) of the map 362 (x 168 to 191, y 96 to 111) is walkable at height 2. The overlap holds at ticks 1-4 and 5-6 of the stroke and not later.
- Expected at the press tick (the first tick of the stroke), from the pass: lid `TouchingEntity` null (the response `0x80` to the type 1 is immune), lid
  `FrameCollisionTickCounter` = 25, hero `HitCounter` = 1, three effects 9 centred at raw (12910591, 6815743, 2588671) = (196.99998, 103.99998, 39.49998) px.
- Controls that do not overlap at the first tick (a "does not touch" negative of the same bench): the hero 50 px further west (126, 100, 32), 20 px north (176, 80, 32),
  or at z 80.

## (e) The cell of the preset `olen-m362`

Map 362, tile **(8, 9, 2)**, flags to set `[248]` (G248 = word 7, bit 24: `GameFlags[7] == 0x01000000` on top of the new game), nothing to clear, no map table entry, no
item count, no weapon field: the hero stands on the cell (8, 9) of the map (x 192 to 215, y 144 to 159), walkable, height 2, the level 2 floor, two tiles below the
tile of Olen (8, 7) and at the pixel column of the arc's arrival (204). It is not the tile **(10, 16)** level 2 of `B[6]` (`0x3B [10,10,16,16,2,2]`: tiles
x 10 to 10, y 16 to 16, z 2 to 2, inclusive), the arrival from the map 185. Slot `test-olen-m362` (14 characters, `a-z0-9-`). The recipe: walk up to Olen and press the button
(his talk starts at the interaction; the preset does not set the facing).

## (f) The predicted list of the existing tests that move

At HEAD `bf1dcef5`, `TestSaves.Presets` lists **26** presets (recounted from `Alundra/SaveGames/TestSaves.cs` and from `AllPresetNames` of
`Alundra.Tests/SaveGames/TestSavesTests.cs`: day3-after-dream, day4-meeting, the six weapon presets, swim, pickup and loot, herbe, elevator, barricade, the two lifts, c4-m22
and c4-m70, the four chests, legende-m14 and legende-m7, blessure-m15 and blessure-m14). T1 adds `olen-m362` as the **last** of the table (27). Moves, and only these:

| test | move |
|---|---|
| `TestSavesTests.TheTwentySixPresets_AreListed_InTheOrderOfTheTable` | renamed `TheTwentySevenPresets_AreListed_InTheOrderOfTheTable` (the name follows the count, D-B2-6); its expected list `AllPresetNames` gains `olen-m362` at the end |

Not moves, but rows or loops that grow with the list (no edit of their bodies): the theory data of `ABuiltPreset_IsAcceptedByTheRealRules`,
`TwoConstructions_AreEqual_ByteForByte` and `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` (`EveryPresetName`) gain one row each, `olen-m362`;
`TheOtherPresets_LeaveTheDeathCounterAtZero` walks `AllPresetNames` and checks the new preset too. No other test of the suite reads `TestSaves.Presets` (grep at HEAD). Any
other existing test that changes is a STOP.
