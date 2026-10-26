# E20.g2c3 - the water drops (A19, C71): T0 results (2026-10-26)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c3" (revision 2, READY). Branch of the parent `chantier/theme2-attaques-objets`, start HEAD `3d433de0` (E20.c2c done). Binary: `ALUN_CD.EXE` (France).
T0 is read-only for the product: **no code was committed**. The throwaway port (`probes/TempWaterDrop.cs.txt`), the throwaway probe tests (`probes/G2c3T0ProbeTests.cs.txt`) and the temporary wiring (`temp-wiring.diff.txt`) were applied, measured, then removed
(`git status` clean on `Alundra/` and `Alundra.Tests/`; the 274 tests of the moved classes are green again after the removal; `check_names.py --rules`: 0 finding).
Two commits: the first (`fd43a9f9`) holds the deposit, the replays, the vectors, `expected-ticks.md` and `probe-predictions.md`, **before any run of the DLL**; this one holds the probes and the rest.

## Verdict

**No stop.** Every probe S-D1 to S-D8 equals the binary at every tick listed in `expected-ticks.md`, at 1, 2 and 3 ticks per frame; the 603 ticks of B[1] are measured on the real map; 6 existing tests move (limit 10).
Three observations are written below (none is a difference of the DLL from the binary; D-G2C3-8 needs no pin).

| Item | Result |
|---|---|
| (a) prerequisites at the HEAD | g2c2 done: `TorchAi` has `SpriteType = 393`, `WaterDropSpriteType = 391`, `HasNoFlameMark(torch) => Bytes[0] != 0`, `Deactivate` reads `XCollisionEntity.SpriteType == 391`; `(E, 20)` and `(C, 70)` registered for {393} (`TorchAi.Register`); `TorchDataGuardTests.A5_A19AndC71_AreHeldByTheDropOfWaterAlone_TheType391_NotPortedHere` is there and green (A19 and C71 held by exactly {391}); the preset `torche-m92` (34, 11, 5) is the 32nd; no `WaterDropAi`, no `(A, 19)` / `(C, 71)` in production |
| (b) deposit | `docs/plan-e20-annexe/e20-g2c3-disc/` (`g2c3/`, `critic/`, README, SHA1SUMS.txt; 84 files, no `__pycache__`, no `replay/work/` copy (identical to `e20-g1a/work/`, `diff -rq` empty), no `critic/copy/`) |
| (c) replay | identical (below) + the vectors of (a1) (below) |
| (d) table of the ticks of the binary | `expected-ticks.md`, committed before the probes (`fd43a9f9`) |
| (e) probes S-D1 to S-D8 | equal (below), predictions in `probe-predictions.md` committed before |
| (f) cell of `gouttes-m92` | map 92, tile **(38, 10), level 5** (below) |
| (g) predicted list | 6 existing tests move (below) |

## (c) Replay (from a copy of the scripts, `replay/work` = `e20-g1a/work/` unchanged)

Commands (python without `-I`, the user site-packages hold `capstone`), outputs in `replay/`, all `cmp` identical to the files of `e20-g2c3-disc/g2c3/`:

| Command | Outcome |
|---|---|
| `python -B -X utf8 replay/c71_full.py 6000 mut gold` | identical to `out_c71_full.txt`: A19 6000 vectors 0 mismatches 3/3; C71 6000 vectors, 0 mismatches, 89/89; mutants A19 2/2, C71 89/90 (one equivalent `move a1,a0` / `subu a1,a0,zero`); goldens: 22 vectors over 11 classes, **SHA-256 `40071ab5e80cbf4dae6bf6d33bcf067958abb78cf918f0847f312c9ffac6214b`** (identical) |
| `s1_drop_cycle.py 330` | identical to `out_s1_native.txt` (restorations at t1, t89, t177, t265) |
| `s1_drop_cycle.py 300 30` | identical to `out_s1b_scripted_N30.txt` |
| `s2_drop_on_torch.py carry\|nocarry\|posed 149` | identical to `out_s2_carry.txt`, `out_s2_nocarry.txt`, `out_s2_posed.txt` |
| `s3_hit_window.py` | identical to `out_s3_hit_window.txt` |

(The tick counts `330`, `300`, `149` are the lengths that give the stored outputs; the scripts print on change, so the stored outputs end at the last change.)

### The vectors of (a1) (`vectors/`)

The goldens of `c71_goldens.json` hold, in the class `st2 contact`, the pairs (`CollidedWithEntityZ`, `IsOnGround`) = (0x9f5f30e3, 1) and (0x69af7566, 0): neither is the pair (1, 0) / (0, 1) of the plan. `a1_state2_flags.py` (run next to `work/`; the interpreter and the model of `c71_full.py`, imported, N = 1) produces on the REAL code of C71
four vectors of the state 2 with the pairs **(1, 0), (0, 1), (1, 1), (0, 0)** (`a1_state2_flags.json`, SHA-256 `b9f8585c363d619b5bf6dadaff41797674b5956b21e4880eec9aecb1e8f416ef`, same format as the goldens: every word read, every word changed, the global word). The model equals the real code on the four (the script asserts it).
Results: (1, 0) -> animation 2, state 3; (0, 1) -> animation 2, state 3; (1, 1) -> animation 2, state 3; (0, 0) -> nothing written. The vector (1, 0) kills "the state 2 ignores `CollidedWithEntityZ`", (0, 1) kills "ignores `IsOnGround`" (T1 (a1)).

## (e) The probes of the DLL (throwaway test, `probes/G2c3T0ProbeTests.cs.txt`; outputs `probes/out/`)

Bench: the real world of the map 92 (`ArcRun`, real prefabs and controllers) on the synthetic flat field, every entity but the hero removed, native handlers off but for the types 391, 393 and 12, a throwaway port of A19 and C71 (`probes/TempWaterDrop.cs.txt`: the drop's `Bytes`, `AIValues[1]`, `Flags`, `ResyncControllerFromFlags()` after every flag change, `ProgramIndexes[2] = Bytes[1]`, `PosZ += 160 << 16` then `PushLogicalPositionToRoot()`;
the torch hit through `RidingEntity`, the hero recognised by `PlayerEntity.LogicContextEntity`, `CarriedEntity`; status `Deactivated` and `Bytes[0] = 1`), registered in `CreateForProduction` (`temp-wiring.diff.txt`). The drops are born like the opcode `0x8A` (`SpawnEntityByRecordId`, absolute position, `PushLogicalPositionToRoot()`, `JoinsBirthTick`); the hero away at (100, 100) for the cycles, the drop above him for the contacts.

| Probe | Result (files `probes/out/`) |
|---|---|
| S-D1 A19 | **equal**: the drop born by the `0x8A` (record 15, no C program): `Bytes[1] = 0` and `ProgramIndexes[2] = 0` at the first tick (`sd1_born.txt`); the placed drop of the record 28 (C program 157 = 0x9D): `Bytes[1] = 157` from the first tick, before any pass of C71, and the script runs (`sd1_scripted.txt`) |
| S-D2 native cycle, 3 cycles, 1, 2, 3 ticks per frame | **equal to the table, tick for tick** (`sd2_rec15_{1,2,3}.txt`): state 1 at R+1 (t2, t90, t178), state 2 + animation 1 + Gravity (`0x32180`) + `ForceZ` -32768 at R+41 (t42, t130, t218), landing at R+65 (t66, t154, t242: `IsOnGround = CollidedWithEntityZ = 1` at the same tick, Z 0), state 3 + animation 2 + sound 145 once at R+66, state 4 + `0x32100` at R+67, `+0xAC` = 1 at R+87 (21 ticks of animation 2), restoration at R+88 (t89, t177, t265): period **88**; sounds 145 at t67, t155, t243 |
| S-D3 `IsOnGround` / `CollidedWithEntityZ`, `RidingEntity` of the drop | **equal** (`sd3_*`): both flags 0 in the fall and 1 at the landing (d+22 on the head, d+23 on the posed torch); the drop's `RidingEntity` = the hero at d+23 (carry and no torch) / the torch at d+24 (posed), one tick after the landing, read by C71 at that tick (state 3 with the sound 145 in the same row); in the carry pose (animation 12) the hero is still the support |
| S-D4 hero after the destruction of the carried torch | **equal**: at d+28 the torch is `FlagToDestroy` and `CarriedEntity` is null, at d+29 the hero asks the animation 0 (`anim_t` 0; `anim_c` 0 at d+30), hp 10, no entity of type 12 in the room |
| S-D5 posed torch | **equal**: status 3 + `Bytes[0] = 1` at d+25, animation 2 + sound 142 at d+26, `+0xAC` at d+28, `FlagToDestroy` at d+29, freed at d+30, `flames = 0` (`sd3_posed_*`, `sd3_events.txt`) |
| S-D6 `PosZ += 160 px` + `PushLogicalPositionToRoot()` | **equal and without recalage**: at each restoration (t89, t177, t265) the logical Z is 160, the root `{X:300 Y:100 Z:160}` the same tick (no snap to the floor, no tick without the root), `ForceZ` 0, `Flags` back to `0x32080` (Gravity out, `Collidable` in); the fall then restarts from 159.5 at R+41. The largest |root Z - logical Z| over every tick is 0.5 px, in the fall (the controller moves after the logic step), never at a restoration |
| S-D7 restart of the script | **equal**: the record 28 (`37 [30]; 40 [2,0]`), `prog2` back to 157 at the restoration (t121), `40` at t153 = restoration + N + 2, state 1 at t154: **S + N + 90** (S = t34, S' = t154) |
| S-D8 the drop passes through the hero | **equal**: after the state 3 `RidingEntity` is null, the drop falls through the hero from Z 32 (31.5 at d+24, 0 at d+34 with the torch carried or without; from Z 15 to 0 at d+32 on the posed torch); the profile `Pawn` does not hold it; the restoration at d+45 (carry, no torch) / d+46 (posed). **No missing feature of the engine** |
| S-D9 (extra) B[1] of the map 92 in the real world, arrival tile (38, 10) | the four torches 43 to 46 are born at (756..900, 168, Z 80) (the load at the tile (38, 10) is inside their box); the four drops are born at **tick 603 of the trace** (the first B[1] call is the tick 0, the frame before the trace), at (756, 168), (804, 168), (852, 168), (900, 168), Z 240, `prog2` 0 (native); the four torches are `Deactivated` + `Bytes[0] = 1` at t669 (birth + 66), sounds 142 at t670, `FlagToDestroy` at t673, freed at t674, no flame, hp 10; the same at 1 and 3 ticks per frame (`sd9_b1_{1,3}.txt`) |

### Observations (not differences of the DLL; no pin needed by D-G2C3-8)

1. **First cycle of a scripted drop: the replay is one tick early.** `s1b` gives the first state 1 at t33 (N = 30); the DLL gives t34. The cause is read in the replay: `s1_drop_cycle.py`, the harness model of the wait (`pre`/`post`, `sc['seen']`) counts the pass of the tick t1 (the tick of A19, status Loaded -> Normal) as a call of the script, while in the machine the C pass first runs at t2 (the native drop of `s1_native` has its state 0 -> 1 at t2). So the binary's first scripted cycle is S = t34 (the DLL's); the restarts after a restoration are exact (restoration + N + 2 = t153, state 1 at t154); the periods **N + 90**, **1 + 88**, **N + 91** of `expected-ticks.md` stand.
   (w2) therefore asserts, for the record 28 born at the tick 1: state 1 at t34, then S + 120 = t154.
2. **State 3 tick, `CollidedWithEntityZ`**: in the DLL the drop reads `collz = 0` (and `IsOnGround = 1`) at the state-3 tick on the head of the hero (the replay: 1); state 3 reads neither flag. Not asserted.
3. **Torch `+0xAC` at 3 ticks per frame**: the torch's `ForceResetAnimationFlag` is seen at t70 (not t69) at 3 ticks per frame, `FlagToDestroy` and the freeing are at t70 / t71 at every cadence (an animation-clock sampling of the intermediate flag; the events of the table are cadence-independent). The hero's `CurrentAnimationId` is one tick later at 2 and 3 ticks per frame in the carry run. Not asserted.

## (f) The cell of the preset `gouttes-m92`

The portal `Portal_5` of `Magyscar-91.tileMap` (box tiles (40, 43) to (41, 43), `Flags` 2, `ZLevel` 0) leads to **map 92, tile (38, 10)**. The cell (38, 10) of `data-extracted/data/map_92.json` is walkable (`Walkability` 0) at `Height` 5, `GroundProperty` 128; the pixel centre is (38 * 24 + 12, 10 * 16 + 8) = **(924, 168)**; the look is not carried by a save (a preset has no direction field, `Preset(Name, Slot, MapId, TileX, TileY, TileZ, ...)`; the portal's arrival animation is 0x36).
The preset: `new Preset("gouttes-m92", "test-gouttes-m92", 92, 38, 10, 5, [], [], [], [])` (slot of 16 characters, rule `[a-z0-9_-]{1,32}`). With the arrival at that tile the four torches of the records 43 to 46 are born (S-D9) and B[1] runs from the first tick (the tile is in its zone x 26 to 42, y 8 to 28): the four drops fall at tick 603 and the torches go out at tick 669 to 674, no flame. Waiting about **13.5 s** (674 ticks at 50 Hz) at the arrival is the whole recipe.
Note for the plan: `torche-m92` (34, 11, 5) of g2c2 lies in the same zone and the same torch box, so it shows the same four drops after 603 ticks; D-G2C3-6 keeps the new preset because it is the exact arrival of the portal (the only arrival from the 91 that fires the trap) and because `torche-m92` is the recipe of the lever, which the drops would spoil after 13 s. (The plan's condition "sauf si `torche-m92` arrive déjà là" is not met: it arrives at (34, 11), not at (38, 10).)

## (g) Predicted list of the existing tests that move (temporary wiring, then undone)

Temporary wiring (`temp-wiring.diff.txt`): `(A, 19)` and `(C, 71)` for {391} registered in `CreateForProduction` (**24 keys become 26**), the preset `gouttes-m92` added after `torche-m92` (**32 presets become 33**). 391 is not liftable: `ProductionWiredLiftableTypes` is untouched.
Suites in Release with the neutralised copy, partitions disjoint by whole namespace with the final dot (as g2c2), `--blame-hang-timeout 60s`; summaries in `runs/`, driver `runs/run_part.sh.txt`:

| Partition | Tests | Red |
|---|---|---|
| `Ai.` | 1094 | 5 |
| `Hero.Hero*` | 1299 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 |
| everything else (negation of the three above; the throwaway probe class excluded) | 2183 | 1 |
| total | **7334** (= the suite at the HEAD) | **6** |

The 6 red tests (all green at the start HEAD):
1. `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (count 24 -> 26)
2. `Alundra.Tests.Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (24 -> 26)
3. `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (24 -> 26)
4. `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (the tuple 24 -> 26)
5. `Alundra.Tests.Ai.Projectiles.BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` (24 -> 26)
6. `Alundra.Tests.SaveGames.TestSavesTests.TheThirtyTwoPresets_AreListed_InTheOrderOfTheTable` (renamed `TheThirtyThreePresets_...`, `AllPresetNames` gains `gouttes-m92` at the end)

Not moves but rows that grow with the list (no body edit): `ABuiltPreset_IsAcceptedByTheRealRules`, `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` (`EveryPresetName`: one row each), `TheOtherPresets_LeaveTheDeathCounterAtZero` (walks the list; green).
6 <= 10: no stop. **None of the g2c2 tests that keep Alundra in the zone of B[1] more than 603 ticks is red** (the four extra drops there change nothing they assert); `C4WorldTests` (the map 87 and its six drops, now falling) and `C4RegistrationTests` stay green; the g2c2 witnesses on the type 393 stay green. The suites rewrite the line ends of `docs/hero-trace-389-*.txt` (checked with `git diff --ignore-cr-at-eol --quiet -- docs/`: no real change, then restored by `git checkout`).

## Next free ADR number

Recounted on all the 38 branches (`git ls-tree` of each): the highest is 0101 (E20.g2c2); **0102 is free**.

## State at the end of T0

`git status`: only the two modified submodule pointers (the author's), the untracked `tools/` files of the author, and this annex. Nothing under `Alundra/` or `Alundra.Tests/` (the Debug build, which deposits the DLL in `alundra-project/`, was not run: no code changed).
