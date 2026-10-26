# E20.g1c - T0 results: STOP at (a0) (entries of the binary without an exact source in the DLL)

Plan: `docs/plan-e20-heros.md` section 1.24.7 (revision 2, READY). Branch of the parent `chantier/theme2-attaques-objets`, start HEAD `ecf64b3c`.
Binary: `ALUN_CD.EXE` (France). Nothing was changed in the DLL, the tests or the engine; this commit is documentation only.

Verdict of T0: **STOP** by the plan's own rule (T0 (a0): "une entree absente ou seulement approchee est un arret"; Arrets: "Une entree du binaire sans source dans la DLL").
(a) was done and is equal to the sources; (a0) found entries that are absent or only approximated; (b) was read; (c) and (d) were NOT run (they need the shadow wired, which (a0) forbids).

## (a) The replays: all identical

Every script ran from a copy in the scratch directory (never in place), `python -I` or `python -B` as the sources say. Capstone and numpy are present (Python 3.14.2).

| Script | Command (from the copy) | Result | SHA-1 of my output = SHA-1 of the stored output |
|---|---|---|---|
| `crit_shadow.py` | `python -I crit_shadow.py` | support F=159744: DLL-as-is 0.592 0.078 0 0, with shadow 0 0.078 0 0; F=196608: 0.499, shadow 0; F=393216: 0.660, shadow 0; ground: 0 everywhere. = 59 %, 50 %, 66 % to 0 %, and the 7.8 % left at tick 2 of the 2.4375 px step. No stored output existed: saved as `out/crit_shadow.txt` (SHA-1 `2957823d...`). | n/a (first record) |
| `xymodel.py` through `xyfuzz.py 8000 11 -v` | `python -I xyfuzz.py 8000 11 -v` | 8000 vectors, 0 mismatch, 449/449 instructions of `ComputeXY` | `8ea3328f...` identical |
| `xyfuzz.py 5000 41 focus -v` | idem | 5000 vectors, 0 mismatch | `c67ef7fa...` identical |
| `xymut.py` | `python -I xymut.py` (5 min 23 s) | the 14 mutants of the model, all KILLED | `8b83bd61...` identical |
| `studyB.py` (the contact against a crate, air / support / ground) | `python -I studyB.py` | air and support tick 1: 0.408 / 0.501 / 0.340, ground 1.000 | `d1f2a179...` identical |
| route R7 of g3: `e20-g3/bin/a3_routes.py` (copy of `e20-g3/bin`, `e20-g1a/work` as `./g1a`) | `python -B a3_routes.py` (65 s) | R7: contact nil at t038, `rec10` at t039; Square t043 lifts `rec10` | `a971af76...` identical |
| `e20-g1b/t0/og33_square.py` (same copy, `G1B_BIN` set) | `python -B og33_square.py` | Square at t037 to t039: sword; t040 and later: LIFT `rec10` | `aa215195...` identical |

The scripts of `e20-g3/g4b-route/` (the lift routes a1 to a5) were not replayed: the route R7 the plan names is the g3 / g1b one above, and they are not needed to settle (a0).

## (a0) The table of entries

Convention of the DLL: every height is the binary's minus one raw unit (ADR-0026); the DLL recomputes `Pos + Mod` on the fly (its `ModdedPos*` cache is written at the spawn only).
"Exact" means the member is the same rule as the binary's, read from the code at HEAD and, for the table, compared with the executable.

### The step search `ComputeXYPosition` `0x80037730`

| Binary | Meaning | DLL member | Verdict |
|---|---|---|---|
| `+0x114/118/11C` | Pos X, Y, Z | `PosX/PosY/PosZ` | exact (Z in the DLL's frame) |
| `+0xE4/+0xE8` | `FinalForceX/Y` (halved by the loop) | `FinalForceX/FinalForceY` | exact |
| `+0x8C` | `TargetDirection` (`& 7 == 0` cardinal, `d < 0x20` slide table) | `TargetDirection` | exact |
| `+0x6C` | `0x100` Gravity, `0x2000` NoObstacleSlide, `0x80` Collidable, `0x8`/`0x1` wall masks | `Flags` (`EntityFlags.Gravity`, `NoObstacleSlide`, `Collidable`, `ClassB`, `ClassA`) | exact |
| `+0xB8` | `ForceZ` (the "walk on the ground" branch needs 0) | `ForceZ` | exact |
| `+0x1D8/1DC/1E0`, `+0x1E4/1E8/1EC` | Modded position, box offsets | `PosX + ModX` etc. recomputed; `ModX/Y/Z` | exact (recomputed, not the cache) |
| `+0x130`, `+0x13C`, `+0x138` | outputs: contact, `ForceAdjusted`, terrain height | `XCollisionEntity`, `ForceAdjusted`, `TerrainHeight` | exact (the write point is the question of D-G1C-1, not the member) |
| `0x80127164` | object in use (`== 0x20`, the cape) | `EntityScriptProxy.ItemInUse` (E20.h0a) | member present. At HEAD every item start is a stub answering 1 (`PlayerManager.ItemStartStub`; the cape is h2), so `ItemInUse` is 0 in production and is set only by the test seam: the entry is written as 0 until h2 |
| `0x80127000` | boots level (`< 2`) | `EquipmentRecords.BootsLevel` (`UpdateItemEffectState` step 1; the value of the previous tick, as the binary) | exact |

### The entity test `FindEntityCollisionCandidate` `0x80036F34`

| Binary | Meaning | DLL member | Verdict |
|---|---|---|---|
| `+0x6C & 0x80`, `+0xB4 & 0x80`, `+0x28` | subject gate | `EntitySupport.IsEligibleSubject` (Collidable, `AnimFlags` NoEntityCollision, `PlatformEntity == null`) | exact |
| `+0x1F0/1F4/1F8` | box size | `Width/Height/Depth` | exact (same semi-open rule, `delta < size + 1`) |
| list `0x80127C28...` | the collidables, first hit in list order | `EntityCollision.FindEntityCollisionCandidate(entity, x, y, z, collidables)` with `ScriptHost.Collidables` | rule exact with the subject position given. **The list is approximated for entities born or moved by script** (one tick late for `0x62`/`0x63` and creations by script, order of update per entity instead of per slot: O-E19-28 (e)/(f), accepted deviations in `docs/plan-e19-opcodes.md`). Exact for crates present from the load of the map (the case of O-G3-3) |
| `0x800DC058` (sign) and `0x800DC05C` (bit 31) | the early exit for the hero (the debug "no collision" mode, set from the pad; the plan calls it "the transition") | none | **absent**: decided not ported (`docs/plan-e20-heros.md` section 1.14.2 (E20.i2a), "n'est pas porte, pour les murs ordinaires comme pour l'eau"; `docs/plan-e20-annexe/e20-0-rapports/D1-oracle-moveplayer.md`: the oracle holds it at 0, its role an inference) |

### The ground height `ComputeEntityGroundHeight` `0x800370C4` and the corner heights

| Binary | Meaning | DLL member | Verdict |
|---|---|---|---|
| `0x80127168` table | pixel to column | `CellsCollisionField` `Math.Clamp(x / 24, 0, width - 1)` | equal to `min(x / 24, 51)` for `x >= 0`, which is what the model, the MIPS oracles and the plan assume. The table lives in RAM (no writer found in the text of the executable by a scan of the immediate `0x7168`: its content is an inference, shared with every earlier oracle); the binary reads before the table for `x < 0`, where the DLL gives column 0 |
| row `y >> 4` clamped 0..`0x3B` | row | `Math.Clamp(y / 16, 0, height - 1)` | exact for `y >= 0` (every map is 52 x 60) |
| cell at `[0x800DD86C] + 0x604 + row * 416 + col * 8`: word `+0` (16 bits) | walkability byte, ground property byte | `SampleRawWalkability`, `SampleGroundProperty` (`CornerTileWords`, `ProbeVramFlagsCorner` rebuild `walk | prop << 8`) | exact |
| cell `+2` slope byte (`& 3`) | slope kind | `CellsRecords.Slope` (private `_slope` of the field) | **absent as a reader**: the data exist, no public accessor reads it (only through `TrySampleGround`, already folded into a height) |
| cell `+3` height byte | units of 16 px | `SampleRawCellHeight` | exact |
| table `0x800236D4` (24 words) | stairs | `AnimationTables.HeightsTable_800236d4` | exact: compared word by word with the executable, `1 2 2 3 4 4 5 6 6 7 8 8 9 10 10 11 12 12 13 14 14 15 16 16` |
| the `slopesHit` bump (`+16` for a later corner of a multi-cell sloped footprint) | in the 4-corner loop | `CellsCollisionField.TrySampleGround` | **approximated, by its own documentation**: "samples a single point and never applies that +16 bump ... Documented deviation, not reproduced" |
| `+0x158..+0x164` | the four corner heights the hero's wall test compares with `ModZ` | `EntityScriptProxy.MapHeights` | **absent for the hero**: `UpdateEnemyGroundHeights` fills it for the entities other than the hero only (E14.a0b: "the hero's are the probes of RunHeroProbes", which compute the slope, the vram flags and the floor, not these four words). The DLL's own corner test (`ScriptedMotion.BlockedCorners`) is the engine's rule (ground height above `PosZ + StepHeight`), not the binary's `corner height < ModZ` |

### The wall test of the hero `0x80037488`

The cells are read through the pointers `+0x148` the ground height just wrote (the attempted position): the DLL reader `CornerTileWords.Read` has the same corner geometry but reads the CURRENT position, with no height qualification; a variant at a given position would be new. The tests `(attr & 0x41/0x40/0x1000) != 0`, `(attr & 0xE00) == 0x800` with boots `< 2` and `(attr & 0xE00) != 0x600` with the cape are expressible on `walk | prop << 8`.

## Why T0 stops

By the letter of the plan the entries below are absent or approximated:
1. `[0x800DC058]` / `[0x800DC05C]`: absent (a decision not to port exists, but the plan lists the exit in the copy and the rule says absent is a stop).
2. The four corner heights `+0x158` of the hero: absent for the hero; the DLL's ground field is approximated (`slopesHit`) and its wall rule is the engine's.
3. The slope byte of a cell: no reader.
4. The collidables list: approximated for entities moved or born by script (accepted deviation, exact for the crates of O-G3-3).
5. `0x80127168` at `x < 0`: no source (inference elsewhere).

None needs the engine. Items 2 and 3 are the new code of a faithful copy (a reader of the slope byte, the corner heights computed in the pure function from the raw cell bytes, the stairs table and the `slopesHit` rule of the model `xymodel.py:ground_height`); item 1 and the `x < 0` corner are decisions. They are for the main session to dispose of: accept the omission of item 1 (the decision of section 1.14.2) and of the `x < 0` corner as written deviations, and allow the reader of the slope byte, or amend the plan; then T0 resumes at (b), (c), (d).

## (b) The readers of `XCollisionEntity` at HEAD (read, by symbol)

- `PlayerManager.PlayerTryAction` (the contact of the previous tick with a front of the Square: `TryLift`, else the sword) : the reader O-G3-3 is about; reads the hero's contact outside the ground branch.
- `PlayerManager` `CheckEntityInteraction` (the interactions, `InteractLatchEntity` and friends): reads the hero's contact.
- `EventProgramRunner` opcode `0xA9` (`XCollisionEntity` of the logic entity in a group; or of another entity equal to the logic entity, with the player: "the hero is blocked in X by me"), and `UpdateWalkDetour` (a walk blocked by an entity waits).
- `EntitySearchService` modes 7 and 8 (entities the reference points at / pointing at the reference).
- `PillarAi` (`hero.XCollisionEntity == pillar`: the push counter): the pillar is pushed on the ground, where the copy gives the engine's contact (0 % mismatch measured).
- `ScriptedMotion.SlideAlongWall` reads `XCollisionEntity` of the engine (`forbidden`); the plan writes the shadow AFTER it, so it must not see it.
- Writers: `EntityScriptProxy.MoveControllerAndPullPosition` (`H2Obstacle ?? H1Obstacle`, rewritten at every `Move`, the second one of a slide included), `XCollisionEntity = null` at the carried hero (`:1877`) and at the head of each `Move`.

## Does the shadow rest on `HalvingStepSearch` (E20.e0k5, ADR-0097)? No, not as it is

Read at HEAD (`Alundra/Physics/HalvingStepSearch.cs`, `HalvingStepResult.cs`, 57 + 6 lines). Same skeleton as `xymodel.py:compute_xy`: the whole step first and a whole accepted step ends the search, `Half` = `sra 1` with -1 to 0, the loop ends on "both halves zero" (cardinal) or "either zero" (otherwise), accepted offsets accumulate. But it cannot give the verdict the shadow needs:
1. It returns `(X, Y, Accepted)`; the shadow needs `[sp+0x34]`, the entity found by the test of the SECOND-TO-LAST attempt (`s4` as it was at the top of the last attempt), which the `bool` predicate hides.
2. It has no ground branch (`0x80037834`-`0x80037884`: the snapped Z, the candidate test at the snapped Z, `s4` left untouched when that test is clear).
3. It has no slide restart (`sp28`, the table `0x80023734`, the edited forces and the re-entry at `0x80037798`) and no `NoObstacleSlide` exit: it was written for a projectile, where those do not exist.
4. Its predicate is the field's `BlockedCorners` (engine rule), not the binary's wall test.
So the shadow is a new pure function (the copy of `compute_xy`), and may share the loop skeleton or `Half` only by extracting them, which touches e0k5's code and is for the main session to decide. e0k5 stays as it is.

## What the tranches done since g1c changed at the same place

- `git log` of `Alundra/Physics/ScriptedMotion.cs`: the last commits are E20.e0k5 T2 `8bfcd5e5` (the bound by the halving search, before the `Move`, for an entity not yet in the world, `HasLogicalZ`, not the hero) and T3 `f6b1ae4d` (`BlockedCorners` made internal); E19 d5b before them. In `RunOneKinematicTick` the order at HEAD is: forces planned, carriers' forces added, edge clamp (`HasLogicalZ`), e0k5 bound (never the hero), `startX/startY`, `MoveControllerAndPullPosition`, `SlideAlongWall`, the flag of the clamp, `LastTickDelta`. The shadow's write point stays after `SlideAlongWall`, before the flag of the clamp.
- E20.c2b (the hurt block, ADR-0096): no commit of the series touches `Alundra/Physics/ScriptedMotion.cs`; the hurt block is not in `RunOneKinematicTick`.
- E20.h0a: only `ItemInUse` (see the table).
- The hero's Z at the moment of `RunOneKinematicTick` is already the one of the tick (`RunHeroVerticalTick` runs before it, the binary's order Z then XY).

## (c), (d): not run

The probe of the DLL with the shadow wired temporarily, and the predicted list of moved tests, need the shadow, hence the entries above. Predicted list: none produced. Expected, from the plan: the pin `CrateSolidityMap392Tests.OG33_..._PinnedNotFollowed`; the arcs of g4b (b3) and g1b (m7) to watch.

## Files

- `out/crit_shadow.txt`: the output of `crit_shadow.py` (new record).
- The other replays equal the stored outputs byte for byte (SHA-1 above), so nothing else is stored.
