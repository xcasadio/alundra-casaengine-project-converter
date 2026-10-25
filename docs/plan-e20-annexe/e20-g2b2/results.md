# E20.g2b2 - T0 (rejeu, recensement, sondes), 2026-10-25

Plan: `docs/plan-e20-heros.md` section 1.24.5, sub-section "E20.g2b2" (revision 2, READY; D-G2B2-1 to D-G2B2-4; D-E20-74). Start HEAD: `16b5659b` (E20.g2c1 done and CONFIRMED). Branch of the parent `chantier/theme1-porter-plateforme`.
Outcome: **no stop**. No existing test moves (the list of T0 (d) is empty), every chronology of the DLL equals the replay of the binary, no need to touch the engine or the pick phase.

T is the tick of the front of the Square (the tick 61 of the oracle, the tick 1 of the DLL runs); K = ticks since T. The binary rows are "after the tick t" (K = t - 61); the DLL rows `K=` are "after the tick K+1" of the run. Effects: the binary shows `fxattach` in the tick of the status 4; the DLL shows the effect on the stage at the END of the frame that holds that tick (the same one-tick shift as g2b, `FrameEndOf`).

## (a) The deposited annexe and the replay of `q3_pit.py`

- `docs/plan-e20-annexe/e20-g2c1-disc/`: `sha1sum -c SHA1SUMS.txt` all OK (the directory carries `g2c1/q3_pit.py`, `g2c1/out/q3_pit.txt`, `g2c1/out/dis_800386d0.txt`, `report-main.md`, `critic-main.md`).
- Replay from a copy (`g2c1/` copied next to a copy of `e20-g1a/work/` named `work/`): `python -B -X utf8 q3_pit.py` (19 s) gives an output **identical** (`diff` empty) to `g2c1/out/q3_pit.txt`; stored here as `replay/q3_pit_replay.txt`.

## (b) Replays of the binary (`replay/replay_b.py`, harness of `q3_pit.py` / `c4_scen.py`: REAL `MovePlayer`, REAL record spawner, REAL C4/C23/E2, REAL physics passes)

Objects: barrel = sprite 34 of map 22 (type 290), star object = sprite 107 of map 27 (type 363), crate = sprite 36 of map 392 (type 292) and sprite 35 of map 383 (type 291), jar = sprite 41 of map 70 (type 297). Thrown east from (109,100) at T=61 while the hero holds RIGHT (landing at x = 178, column 7, row 4/5), over the cells of the columns 6 to 9 (24 px cells).
Cases: `hole` (walkability 0x04, columns 6-9), `exit` (ground property 0x80, columns 6-9), `full` (flat, the contrast), `corner` (walkability 0x04 on the ONE cell (7, 5)), `cornerexit` (ground property 0x80 on the ONE cell (7, 5)). The corner cell was found by `replay/probe_cells.py`: it is the only single cell around the landing that gives `+0x180 = 4` and `+0x184 = 0` (a partial corner) at the landing.

| object | case | `+0x180` (OR) / `+0x184` (AND) at T+22 | status | free slot | effect | sounds after T |
|---|---|---|---|---|---|---|
| barrel 290, star 363 | hole / exit | 4 or 0x8000 / the same | 2 -> 4 at **T+23** (never 3), the animation stays 1 | **T+24** | 10 attached at T+23 | none (no landing sound) |
| barrel 290, star 363 | corner / cornerexit | 4 or 0x8000 / **0** | same as above | T+24 | same | none |
| barrel 290, star 363 | full | 0 / 0 | stays 2 (Normal), the animation 0 at T+23 | never | none | 24 at T+23 |
| crate 291, crate 292, jar 297 | hole / exit | 4 or 0x8000 / the same | 2 -> 4 at **T+23**, **E2 never called** (the animation 2 never asked, flags `0x332374` / `0x232374` unchanged) | **T+24** | 10 (crate) or 2 (jar) at T+23 | none (neither 27 nor 31) |
| crate 291, crate 292, jar 297 | corner / cornerexit | 4 or 0x8000 / 0 | same as above | T+24 | same | none |
| crate 291, crate 292, jar 297 | full (the contrast = the chronology of g2b) | 0 / 0 | 2 -> 3 at T+23 (E2, animation 2, flags `0x332344`), 3 -> 4 at **T+25** | **T+26** | 10 / 2 at T+25 | 27 (crate) / 31 (jar) at T+23 |

Reading: the pick phase destroys at the tick AFTER the landing tick that wrote the word (T+22 writes, T+23 destroys), for every object including the crate and the jar; a crate on a hole never reaches E2. The word is the OR (one corner is enough): D-G2B2-1's OR is confirmed at the binary. The "contrast" row for the crate and the jar is exactly the g2b chronology (E2 at T+23, status 4 at T+25, effect at T+25, slot free at T+26).
D-G2B2-3 (the branch `CombinedVramFlagsAND & 4` of E2): the case `hole` has the four corners at 4 (`+0x184 = 4`) and the crate dies by the pick phase before E2 runs, so on the binary that branch of E2 is not reached by a thrown crate; the DLL after the line does the same (E2 calls = 0), see (d). No difference: not a stop.
Full per-tick rows: `replay/replay_b.txt` (ticks 81 to 89 of every run).

Deviation from the plan's wording: the plan names "la caisse (291)"; the oracle's default crate is type 292 (sprite 36), and 291 is sprite 35 of the maps that hold it (map 383). Both were replayed with the same result; the DLL runs use the index 36 of the map page (`Coal Mine-67`, the bench of g2b) and the jar 41 of `Coastal Cave-70`.

## (c) Census from the export (`census_c.py`, `census_c.txt`)

Flags = `MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16`; type = index | 0x100 on the map page, index on the global page (`WorldProxy.SpawnEntityBySpriteIndex`); source `alundra-project/Data/sprite-records.json` (the export), placements from `data-extracted/data/map_*.json`.
Types with the gravity (0x100) and DestroyOnVramFlags (0x200000): **106** = 16 on the map page + 90 on the global page.

- **Map page (16)**: the 7 of C4 (290, 293, 363, 367, 368, 369, 373), the 6 of C23 (291, 292, 294, 297, 304, 438), the torch (393), the sprites 390 (C69) and 430 (C76, the pillar): the plan's list, exactly. Placed by the maps: 390, 393, 430 and all of the 13 liftable ones (never marked when placed: D-T-12 kept).
- **Global page (90)**: the sprite 234 (the common C23), the sprite 1 (C73/E21), the 88 pick-up objects 31 to 119 except 72 (`0x200180` or `0x230180`, no program, the objects that `LootDrop.Spawn` makes: global index = item + 0x1E), which include the **11 shop articles** (48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113).
- **Can be marked by the hands** (`PlayerManager.ProductionWiredLiftableTypes`, 25 types: `LiftableAi.SpriteTypes` + `C4ProductionTypes`): all 25 are in the 106 (the 7 of C4, the 6 of C23 + 234, the 11 shop articles; 304 is in both lists). So the line changes the life of every liftable object, the shop articles included (the shop articles are global-page objects with the flags `0x230180` / `0x200180`; they were not replayed at the binary here: the rule is the flag test of the pick phase, the same code as the objects replayed).
- **Can be marked by their birth** (`WorldProxy.SpawnEntityBySpriteIndex`, the marker block; callers: `LootDrop.Spawn` with global index item + 0x1E; `PlayerManager.SpawnWeaponProjectile` with the types 27, 28, 29): the loot objects (global 31 to 119) are in the 106; the weapon projectiles (27 to 29) are NOT (no 0x200000). So the entities born in game that the line can destroy over a hole or an exit are the **loot objects** (a drop above a hole disappears at its landing, as the binary does by D-G2B2-2). Records spawned by `SpawnEntityByRecordId` (event scripts) are not marked.
- The sprite 1 (global) and 234 are not born by any caller above; 234 is wired for the hands only.

## (d) Working copy where only the line of the hook changes (never committed)

The line (`Alundra/Entities/EntityScriptProxy.cs`, the f6a hook, after `UpdateGroundSlope();`): `if (HasLogicalZ) { UpdateVramFlags(); }` (`probe-dll/hook-temp.diff.txt`). The probe is a scratch test (`probe-dll/G2b2ProbeTests.cs.txt`, never committed: the objects put in the hands by `PutInHands` through `C4Run`, thrown with Square | Right at the tick 1, same cells as (b)); the comparison script is `probe-dll/cmp.py`.
Objects: barrel (record 21 of map 22), star (record 5 of map 27), stone (record 24 of map 24), trunk (record 8 of map 70), crate (index 36), jar (index 41); cases of (b) plus the two cases of `q3_pit.py` (0x1001 band, walls in column 8 and 9). Outputs: `probe-dll/dll_before_*.txt` (HEAD) and `dll_after_*.txt` (with the line).

| object | case | before the line (HEAD) | after the line | binary (b) |
|---|---|---|---|---|
| barrel, star, stone, trunk | hole / exit / corner / cornerexit | stays posed, never destroyed (the sound 24 at T+23) | OR written at T+22 (`or=4` or `0x8000`; `and=0` in the corner cases), FlagToDestroy at **K=23**, out of the world at **K=24**, effect 10 on the stage at tick 24, no sound | status 4 at T+23, free T+24, effect 10, no sound |
| barrel, star, stone, trunk | full | stays posed | unchanged | stays posed |
| crate, jar | hole / exit / corner / cornerexit | E2 at K=23 (animation 2, sound 27 / 31), FlagToDestroy K=25, absent K=26, effect at tick 26 | **E2 never called** (`E2 = 0`), FlagToDestroy **K=23**, absent **K=24**, effect (10 crate, 2 jar) at tick 24, no sound | status 4 at T+23, free T+24, no E2, no sound |
| crate, jar | full | E2 K=23, FlagToDestroy K=25, absent K=26 | unchanged | the chronology of g2b |
| barrel, stone, trunk | 0x1001 band + wall column 8 / 9 | FlagToDestroy K=28 / 36, absent 29 / 37, E2 first K=26 / 34 (barrel, stone, trunk) | unchanged (the OR now reads 0x1001, which has no bit of 0x8004) | tonneau: status 4 at t89 / t97, free t90 / t98 |
| star | 0x1001 band + wall column 8 / 9 | FlagToDestroy K=27 / 35, absent 28 / 36 | unchanged | status 4 at t88 / t96, free t89 / t97 |

At 2 and 3 ticks per frame (`dll_*_tpf123.txt`): the same order of events (the word is seen at K=22 or 23 depending on the frame boundary, FlagToDestroy at K=23, absent at K=24; the crate on a flat ground still at K=25 / 26).
**Every chronology of the DLL equals the replay of the binary** for the K, the animation (never asked on a hole), the effect id, the sounds and the absence of E2.

The whole suite with the line, Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets`), `--blame-hang-timeout 60s`, three disjoint partitions by whole namespace (the filters of `docs/plan-e20-annexe/e20-g2c1/t4/run.sh`, partition 3 the exact negation of the other two): partition 1 (`Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.`) 1 486 passed, 5 min 52 s; partition 2 (`Entities. Events. ExportGuards. Hero. Items.`) 2 956 passed, 3 min 54 s; partition 3 (the negation) 1 859 passed, 51 s; total **6 301 tests, 0 failed** (`suite/p*.summary`).
**The list of the tests that move is empty**: no existing test moves with the line, none for an entity that was never in the hands (D-G2B2-2 stop condition not met), none for a loot object. The traces of the 389 were only rewritten in their line ends (`git diff --ignore-cr-at-eol` empty) and restored.

## Predictions for T1 (the tics of the tests; K as in the DLL runs)

- (p1), (p2), (p3): barrel / stone / trunk / star over a hole or an exit: `or180` = 4 or 0x8000 after K=22, Status FlagToDestroy at K=23, not present at K=24, effect 10 anchored on the stage at the end of the frame of K=23 (tick 24 at one tick per frame), no sound 24. Red before (the object stayed Normal with the sound 24 at K=23).
- (p3b): corner (one cell (7, 5)): the same as (p1) with `and184 = 0`: red before.
- (p4): barrel on a full cell: Normal, sound 24 at K=23: green before and after.
- (p6): crate / jar over a hole: FlagToDestroy K=23 (not 25), absent K=24 (not 26), E2 calls 0 (not 3), the animation never 2, no sound 27 / 31, the effect at tick 24 (not 26): red before.
- (p7): crate on a full cell: the g2b chronology (E2 K=23, FlagToDestroy K=25, absent K=26): green before and after.
- (p8) (D-G2B2-3): the crate over a hole has `and184 = 4` and E2 is never called (equal to the binary).
- (p5): not measured here (an entity placed by the map stays unmarked, `HasLogicalZ` false: the line is behind that gate); to be written in T1.

## Files

`census_c.py`, `census_c.txt`; `replay/` (`replay_b.py`, `replay_b.txt`, `probe_cells.py`, `q3_pit_replay.txt`); `probe-dll/` (`G2b2ProbeTests.cs.txt`, `hook-temp.diff.txt`, `cmp.py`, the four outputs); `suite/` (the three summaries); `SHA1SUMS.txt`.
To replay `replay/*.py`: put `docs/plan-e20-annexe/e20-g2c1-disc/g2c1/` copied as `g2c1/` next to a copy of `docs/plan-e20-annexe/e20-g1a/work/` named `work/` and the scripts beside `g2c1/` (the scripts add `g2c1/` to the path; `c4_scen.py` reads `g2c1/work/orc`, so `work/` must also be reachable as `g2c1/work`), then `python -B -X utf8 replay_b.py [object ...]` (about 3 min). The scripts read the binary `ALUN_CD.EXE` (France) and `data-extracted/`.

---

# T1 and T2 (tests first, then the line), 2026-10-25

Tests: `Alundra.Tests/Ai/Objects/PitFallWorldTests.cs` (22 cases, 8 test methods + theories), committed first (`a8ee1413`), then the line. Expected ticks are the T0 rows (D-G2B2-4): the objects of C4 and the crate/jar each for their own replay rows.

## Red before the line, green after (named)

| test | red before the line, on | green after |
|---|---|---|
| `P1_TheBarrelThrownOnAHole_...` | T+23: status Normal (not FlagToDestroy), word 0 instead of 4 | yes |
| `P2_TheBarrelThrownOnAnExitFloor_...` | same, word 0 instead of `0x8000` | yes |
| `P3_TheStoneTheStarAndTheTrunk...(1, 2, 6)` | same on the stone, the star, the trunk | yes |
| `P3b_ASingleCornerOn...(0/2, corner/cornerexit)` | same; AND 0, OR 4 / `0x8000` expected (the replay makes them disappear at T+23: so red) | yes |
| `P6_TheCrateThrown...(hole, exit, corner, cornerexit)` | T+23: status Deactivated, Target 2, E2Calls 1 (E2 at T+23, destroyed at T+25 by g2b) instead of FlagToDestroy at T+23 with E2 never called | yes |
| `P6_TheJarThrown...(4 grounds)` | same | yes |
| `P8_TheCrateOnAHole_HasTheFourCorners...` | words (0,0) instead of (4,4) at T+22 | yes |

Green before AND after (witnesses): `P4_TheSameBarrelThrownOnAFullCell_...`, `P5_AnEntityPlacedByTheMap_OnAHoleCell_...` (a map record, `HasLogicalZ` false, placed on the hole at x = 178: alive 40 ticks, words 0), `P7_...FollowTheChronologyOfG2b(crate, jar)`.
Counts: before the line 18 red / 4 green of the 22; after 22 green.

## Suites with the line (partition by whole namespace, partition 3 the exact negation, filters of `e20-g2c1/t4/run.sh`; `--blame-hang-timeout 60s`)

| config | p1 (Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.) | p2 (Entities. Events. ExportGuards. Hero. Items.) | p3 (negation) | total |
|---|---|---|---|---|
| Release, neutralised copy | 1 508 passed (6 min 2 s) | 2 956 (4 min) | 1 858 (51 s) | 6 322, 0 failed |
| Debug (last) | 1 508 (6 min 14 s) | 2 956 (4 min 2 s) | 1 858 (51 s) | 6 322, 0 failed |

Against T0 (6 301): +22 new tests in p1 (`Alundra.Tests.Ai.`), -1 in p3 (the scratch probe `G2b2ProbeTests` of T0 (d) is not part of the tree). 6 301 + 22 - 1 = 6 322. The summaries are in `suite2/`.
**Tests moved: none** (the list of T0 (d) was empty, and no existing test turned red in any partition: `suite2/*.red` empty). Subset of the T0 list: yes (empty).

## Other checks

- Traces `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet` on the four files: unchanged (only line ends rewritten by the suites); restored by `git checkout --`.
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical (the Debug build deposited it).
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning, `TextBoxGpu.cs`).
- The engine (`CasaEngineMonogame`), the converter (`alundra-datas-analyser`) and `data-extracted`: no diff between `16b5659b` and HEAD.

## The change

`Alundra/Entities/EntityScriptProxy.cs`: in the hook of f6a of `Update`, after `UpdateGroundSlope();`, `if (HasLogicalZ) { UpdateVramFlags(); }` (the trunk of `FeedsGroundSlope` gets the slope only). Docs corrected: the RESTRICTION paragraph of `UpdateVramFlags` and D-T-12 of `docs/plan-transitions-carte.md` (amendment). Mutants and ADR are T3.
