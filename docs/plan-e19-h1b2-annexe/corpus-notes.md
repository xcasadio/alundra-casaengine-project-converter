# E19.h1b2 discovery - corpus census (read-only)

Export read: `alundra-project/Maps/*/*/events/*.events.json` + tilemap + `alundra-project/Data/sprite-records.json`, 483 maps, 9741 records (re-run today with `corpus/hcensus.py`, copy of the E19.h tool, outputs
`corpus/hcensus.json`, `corpus/sites-0x20-0x22-0x23.tsv`, `corpus/durations-0x20.tsv`). Static walk = every root of every map (record programs A/C/D/E/F, map events B, `0x40` roots), both branches of every conditional,
plus the four targets of `0x57`/`0x58` (superset). "Logic entity" classes are may-sets inferred from `0x42`/`0x43` (a program starts on the HERO for a map event B, on its own record for a record program).
Scripts: `corpus/census20.py` (classification), `corpus/noctrl.py` (controller-less prefabs), `corpus/precede20.py`, `corpus/durations20.py`, `corpus/around.py <map> <pc>` (disassembly window).

## 1. Sites

| opcode | sites | maps | slots | chain (30 maps) | combat maps (7) |
|---|---|---|---|---|---|
| 0x20 Wait Z distance | 266 | 78 | B 18, C 248 | 0 | 2 (115 B[2] @220, one on map 44) |
| 0x21 (ported by H1) | 16 | 8 | C 16 | 0 | 0 |
| 0x22 Wait height target | 16 | 5 (36, 89, 115, 127, 363) | C 15, B 1 | 0 | 1 (115 B[2] @367) |
| 0x23 Wait height target or Z contact | 1 | 1 (36 C[14] @1252) | C 1 | 0 | 0 |

(Matches the plan: 266 / 17.) Full list with operands and logic-entity classes: `corpus/sites-0x20-0x22-0x23.tsv` (283 rows).
Linear scan of the same files: 0x8D has 183 sites in 105 maps, 16 on the chain (maps 10, 162, 163, 183): relevant to R-TH only.

0x20 operand distribution (distance px = `v1 | v2 << 8`): 32 px x71, 64 x46, 16 x26, 48 x23, 160 x14, 80 x11, 12 x9, 24 x8, 96 x8, 40 x5, 56 x5, 128 x5, 1 x4, 216 x4, 144 x3, 224 x3, 8/14/88/240 x2, singles 15, 22, 34, 112, 172, 178; 256+ px (high byte used): 256 x2, 512 x3, 272 x1, 544 x1. Force that precedes
the wait (nearest `0x1B`/`0x5E` in the 6 instructions before): 249 `0x1B`, 1 `0x5E`, 14 `Walk` (maps 100, 102: 12 px / 1 px walks, other entity moves), 2 none (map 89 C[3] @522, C[21] @648: no 0x1B in reach). Forces: +65536 (+1 px/tick) x96, -32768 x29,
+131072 x22, -65536 x21, +32768 x21, -131072 x18, 0 x14 (gravity-driven fall or external mover: 28 C[14-17], 29 C[29], 72, 103, 108, 306, 441, 480 C[2] @783), then irregular ones (-33024, +212992, ...). Model durations (`n = ceil(D * 65536 / |F|)` passes after the `0x1B` pass):
min 8, median 48, max 640 ticks (`corpus/durations-0x20.tsv`).

Logic entity of the 0x20 sites (first-run may-set): 233 record-controller only; 4 hero only (map 1 B[2] @219; 480 B[1] @403/@409/@415); 25 hero + controller records (B programs that `0x43` onto a record); 1 controller-less only (47 C[4] @442);
1 controller-less + controller (260 C[5] @747); 2 hero + controller-less + controller (388 B[2] @183, @401 - persisted contexts of the same B program, see below).

## 2. Controller-less records (the population the new step would act on)

6 prefab ids, 67 records, ALL with a resolved header (0 records of 9741 fall back to the bare path in production). None on map 389. Prefab JSON: `tick_policy: EveryFrame`, components `DepthSortable2DComponent` only.

| prefab | records | maps | Gravity | Collidable | Size | notes |
|---|---|---|---|---|---|---|
| I33 broken armour | 3 | 11 rec33, 260 rec17, 398 rec9 | yes | yes | 0,0,0 | spawn z == T (flat) on all three |
| Laser (?) | 31 | 36 (13-41), 422 (17, 18) | no | no | 0,0,0 | |
| Sara (dream) | 4 | 41 rec1, 42 rec1, 47 rec0, 47 rec3 | yes | yes | 0,0,0 | spawn z == T (flat) on all four |
| Rancune de Melzas | 5 | 115 rec22, 117 rec18, 310 rec0, 368 rec14, 476 rec0 | no | no | 0,0,0 | |
| invisible key trigger (tiny) | 21 | 117, 135 rec26, 136, 137, 145, 166, 173 rec4, 180 rec4, 185 rec10, 196, 200, 229, 240, 375, 387, 396, 398 | no | yes | 1,1,1 | story-chain members: 135 rec26, 173 rec4, 180 rec4, 185 rec10 |
| Nirude (residual thought) | 3 | 388 rec0, 451 rec1, rec2 | no | no | 0,0,0 | |

No controller-less prefab carries a Z impulse animation (`IsZForceApplied` empty on every AnimSet): `StepAnimationClock`'s impulse never fires for them.
Where a controller-less entity actually receives a Z force (`0x1B`, may-set, persisted contexts included): 47 C[4] @439 (`[128,255]` = -32768, then 0x20 `[96,0]`; `0x17` low gravity at @426; `0x64 [128, 264, 192, z 160]` at @427, terrain under it 16 px: the fall of 96 px to 64 px never lands; model: wait ends at pass 192);
260 C[5] @744 (`0x63` clears 0x180 = Gravity + Collidable at @740, `[128,0]` = +32768, `0x20 [32,0]` @747: 64 passes, then `0xBD` sound @750); 115 B[2] @217 (`[128,0]`, `0x20 [160,0]` @220: 320 passes, only if the preceding `0x43 [19]`/`0x43 [22]` picks rec22), @364 (`[128,255]`, `0x22` @367);
388 B[2] @180 (`[0,255]` = -65536, `0x20 [160,0]` @183) and @398 (`[0,1]` = +65536, `0x20 [48,0]` @401) - first-run context is REC1 (a controller record after `0x43 [1]`), rec0 (controller-less) only through the persisted may-set: not certain;
stops only (`[0,0]`): 117 C[22], 136/137 A[1], 396 C[4]. `0x70` has 0 site with a controller-less context; `0x24`, `0x25`, `0x26`, `0x6F`, `0x21`, `0x5E` also 0.
So the only certain controller-less 0x20 waits are 47 C[4] @442 and 260 C[5] @747, as the plan says; both stall forever if 0x20 is ported without the controller-less step (their `0x1B` does nothing today).

The seven gravity controller-less records all spawn exactly at their terrain (flat cells, `slope & 3 == 0`, ModZ 0): with the step they rest (`T + F <= T` every tick, `ForceZ` zeroed, `IsOnGround 1`), nothing visible changes at load. Rancune 476 rec0 after `0x8A @553` floats at 48 px over T = 0 (no gravity): unchanged.
The tiny key trigger 185 rec10 floats at 32 px over T = 16 px (no gravity, F 0): unchanged, `IsOnGround 0`. 173 rec4, 180 rec4, 135 rec26: z == T.

## 3. The 17 sites of 0x22 / 0x23 (all on maps off the chain; `z22terrain.txt` of the 2026-10-03 discovery holds the same rows)

Logic entities are controller prefabs without gravity (`OffsetZ` 0), literal targets `Height << 19`: 36 rec3 176 px (0x22 @491, may also be the HERO if `0x43 [3]` fails), 36 rec42-53 240 px (0x23 @1252), 89 rec20-25 112 px, 115 rec19 48 px / rec22 (controller-less Rancune, 64 px) at @367, 127 rec6/7 160, rec8/9/13-16 240, rec11/12 96, rec17/18 304 px, 363 rec0 80 px.
Targets are multiples of 8 px (524288 units): representable in float32 at any height; the 0x1B forces of these programs are multiples of 256 units, terrain heights are whole pixels (`AlundraCellsCollisionField.ComputeGroundHeight`, `Alundra/Scripts/AlundraCellsCollisionField.cs:382-...`: integers), so the clamp step `ForceZ = diff` is a multiple of 256 units (> 66 units, the engine's
`Move` dead zone, `MinMoveDistanceSquared` 1e-6): reachable exactly, 304 px included (see `engine/notes.md`). Model of the rise from the landing: `n = (target - T) / |F|` (127 rec17/18: 96 px at +32768 -> 192 ticks after the landing).

## 4. Arcs

- NO arc crosses a 0x20/0x22/0x23 site: the 20 arc maps (390, 476, 478, 416, 392, 391, 163, 164, 165, 172, 176, 178, 179, 135, 185, 162, 10, 346, 61, 15; `Alundra.Tests` ArcSpec census, 28 specs) have none (the 81 maps with a site are listed in the TSV). Consequently nothing is pinned around them.
- Arcs that tick controller-less entities in a world with a field (the new step would run; expected unchanged): `A1c` (`AlundraShipArcTests.cs:194`, `RealController: true` WITHOUT `Prefabs`: the 16 NPCs of map 390 are bare entities integrated by `world.Update`; after the gate lift they hold gravity: 4 sailors/passengers and 12 crates, all at rest: z == T = 64 px, or stacked z = 80 px on a 64 px crate whose top + 1 is 80 px; recs 6, 7, 12-15 at 80), `A4p` (476 rec0), `A17`/`TH4-A17` (135 rec26), `T-A19`/`TH4-A19` (185 rec10). A1c, A4p are in `ArcsWithoutEntityContact` (`AlundraArcSupport.cs:276`), checked at dispose: blocked controller steps must be 0 (the new step is not a controller step).
- Bare-mode arcs (`A0`, `A0b`, `A1`, `A2`, `A4`, `RandomReset`, `HeldDirections`, `PrefabBare`): entities never integrated, `Owner.World == null` -> the step does not run (criterion false).
- `A4`/`A4p` read Rancune at the instruction `0x8A @553` only (`AlundraVisionArcTests.cs:276-277`, `(792 << 16, 176 << 16, 48 << 16)`), before any tick.
- Off-chain behaviour that changes (for the author's recipe, not pinned): every lift/trapdoor/ball of the 233 controller sites starts to WAIT its distance instead of running on (e.g. map 22 rec2: after riding 64 px the platform warps; today it warps at once), the Sara fall of 47, the armour lift of 260, the ball cycles of 36/89/115/127/363.

## 5. Story chain skipped-opcode list

`Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`: 102 data lines, none for 0x20/0x22/0x23 (opcodes 0x87, 0x90-0x95, 0xA0, 0xA2, 0xA3). No line to add or remove.
