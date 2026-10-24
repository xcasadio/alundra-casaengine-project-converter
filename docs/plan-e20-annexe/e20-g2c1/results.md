# E20.g2c1 - C4, the liftable objects: T0 results (2026-10-25)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c1" (revision 2, READY). Branch of the parent `chantier/theme1-porter-plateforme`, start HEAD of the execution `09499204` (E20.g4b done); the branch
then received plan-only commits of the main session (`docs/plan-e20-heros.md`), no code. Binary: `ALUN_CD.EXE` (France).
T0 is read-only for the product: **no code was committed**. The throwaway tests and the temporary wiring were applied, measured, removed (`git status` clean on `Alundra/` and `Alundra.Tests/`).

## Verdict

**STOP on one point (P5, the numbering of the first tick), everything else is equal to the binary.** Details in "P5" below and in "Stop".

| Item | Result |
|---|---|
| (a) prerequisites | g1b closed (plan 1.24.4 "faite et CONFIRMED le 2026-10-24"), g4b closed (plan 1.29.2 "faite et CONFIRMED le 2026-10-24"), tree clean on `Alundra/`, `Alundra.Tests/`, `docs/plan-e20-annexe/` (only the author's untracked `tools/` files and the two modified submodule pointers, untouched) |
| (b) deposit | `docs/plan-e20-annexe/e20-g2c1-disc/` (`g2c1/`, `shop/`, `replay/`, README, SHA1SUMS.txt) |
| (c) replay | all identical (table below) |
| (d) HEAD re-read | wired set, pins, hook sites below |
| (e) probes P1..P5 | P1, P2, P2c, P3 (pierre), P4 (objet, pile) equal to the binary; P5 equal once the first C run is aligned (STOP point) |
| (f) purchase path on the 258 | all opcodes of B[2] and of the articles are in the runner; the three missing ones are not on the path (below) |
| (g) barrel of 113 rec 11 (C64) | measured: stays glued to the hands, as the report said |
| (h) displaced tests | **7** existing tests (limit 10), listed below |

## (c) Replay (from a copy of the scripts, `work/` = `docs/plan-e20-annexe/e20-g1a/work/` unchanged)

| Script | Outcome |
|---|---|
| `c4_full.py 20000 mut` | stdout byte-identical to `docs/plan-e20-annexe/e20-g2c/disc/c4_full_out.txt` (20 000 vectors, 0 mismatch, 157/157 instructions, 150 mutants, 134 killed, 16 survivors listed); `c4_goldens.json` SHA-256 `59167009656660d68b4847485b01f1b7a8426f914429525bcccb74d6b267fe90` = the plan's, and byte-equal to the 2026-10-22 file |
| `q8_p1.py` | `cmp` identical to `g2c1/out/q8_p1.txt` |
| `q3_corners.py` | output = `out/q3_wallrise.txt` + `out/q3_other.txt` (`cat | cmp`), 183 ticks of 183 equal |
| `q8_p4.py`, `q8_p5.py`, `q9_swaps.py` | `cmp` identical to `out/q8_p4.txt`, `out/q8_p5.txt`, `out/q9_swaps.txt` |
| `shop_replay.py` b, d, g, h | the four `shop_replay_*.txt` and `shop_b_table.txt`, `shop_h_table.txt` `cmp` identical |

(Remark: `python -I` drops the user site-packages and loses `capstone`; the scripts were run with `python -B`.) Outputs in `e20-g2c1-disc/replay/`.

## (d) Re-read at the start HEAD

- Wired set: `Alundra/Hero/PlayerManager.cs:970` `ProductionWiredLiftableTypes = new HashSet<int>(LiftableAi.SpriteTypes)` = `{234, 291, 292, 294, 297, 304, 438}`; seam `WiredLiftableTypesForTests`.
- Pins of the set: `Alundra.Tests/Hero/LiftPureTests.cs` `TheProductionSet_IsTheSevenTypesOfC23_AndTheTestSeamReplacesIt` (line 139).
- Witnesses (m5) of g1b that use the barrel (290, C4) as "outside the set": `Alundra.Tests/Arcs/LiftMap72ArcTests.cs` `M5_TheBarrelOfTheFamilyC4_GivesTheSword_TheJarOfTheSameSceneLifts` and `Alundra.Tests/Hero/LiftWorldTests.cs` `M5_AnObjectOutsideTheSet_GivesTheSword_NextToTheCrateOfTheSameSceneThatLifts`.
- Pins of the registry (count of keys of `NativeSpriteRegistry.CreateForProduction`, which gains the key `(C, 4)`): `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn`,
  `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses`, `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses`,
  `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem`. `CreateForProduction` is at `Alundra/Ai/NativeSpriteRegistry.cs:36` (last line before `return`: `LiftableAi.RegisterBreak(registry)`, line 47).
- Sites of the hook of f6a: `Alundra/Entities/EntityScriptProxy.cs:1824` `if (HasLogicalZ) { UpdateGroundSlope(); }` (the physics pass of an entity, after `TickScriptedNpc`, statuses 2 and 3), and `:1872` (the hero's probes); `:2612` is the root push of `PushLogicalPositionToRoot`, not a site.
- `LiftableAi.cs`: the comment that inverts barrel and stone is the one of the report (to be fixed by g2c1). No `FeedsGroundSlope` exists yet.
- Next free ADR number recounted on all branches: ADR-0091 is the highest, **0092** is free.

## (e) Probes of the DLL (throwaway test, not committed; sources in `probes/*.cs.txt`, outputs in `probes/out/`)

Bench: `C4ProbeRun` = a copy of `BreakRun` (real world of the exported map, hero at (109, 100), synthetic cell field, natives off except the families of the filter, Square at the tick 1 = T)
with the real records born by `SpawnEntityByRecordId` (barrel 22 rec 21, stone 24 rec 24, object 27 rec 5, trunk 70 rec 8), put at the head of the hero and in the hands by `PutInHands`. A throwaway port of C4
(`probes/C4Throwaway.cs.txt`: carried half of C23 with the flags policy of C4, free half, reader of the 4 corners with the geometry of `UpdateGroundSlope`, no qualification) is registered as `(C, 4)` for the 7 types.
The DLL has the same Z as the binary here (no offset to derive); the compared fields are x, z, `ForceAdjusted`, ground, target animation, target direction, lock, effect 9 and its position, sounds.

| Probe | Compared with | Result |
|---|---|---|
| P1 wall in column 5, barrel / stone / trunk / object, moving and still | `q8_p1.txt` (tonneau34, tronc117, etoile107, rows T-2..T+6 incl. effect 9 positions) | `cmp_p1.py`: **42 rows, 0 difference** (T+1: x=106, z=40, direction 8, lock 1, effect 9 at (109, 100, 44); objects one tick later, effect at (110, 100, 47.5)) |
| P1 wall in column 6 (no rebound), moving | `p1_col6.py` run on the binary (`replay/p1_col6.txt`), rows T..T+24 | `cmp_col6.py`: **75 rows, 0 difference**; the barrel stops at 133, the objects at 134, `ForceAdjusted` 1 from T+8, no rebound (direction 24, lock 0) |
| P2 band `0x1001` columns 6 to 9 (glide) | `q3_corners.txt` section "cells 0x1001 in columns 6 to 9" (x, z, adj, ground, anim, rows T+17..T+51) | `cmp_p2.py`: **35 ticks x 4 objects, 0 difference**: 3 px per tick from T+22 (x=178) to T+46 (x=250), animation 0 at T+47, status stays Normal (no break), sound 24 on each sliding tick |
| P2c water word `0x0800` | plan: barrel and objects status 4 at T+23 (FlagToDestroy in the DLL's enum), free at T+24; trunk animation 5 at T+23 | DLL: barrel, stone, object: FlagToDestroy at T+23, slot free at T+24; trunk: target animation 5 at T+23 (`Slope_18c` = 4 from the landing tick T+22). The attached effect 6 is not read by the bench (T2 reads it) |
| P3 touch, stone 24 rec 24 against the Beannoid (index 79 of the page of the map 24), target at x=165 | plan: T+21 `HitCounter` 1, T+22 status 3, flags `0x3323E4` -> `0x3323C4`, T+24 status 4, T+25 free | DLL: `HitCounter` 1 at T+21 (one spark), Deactivated at T+22 with flags `0x3323C4` and E2 calls from T+22, FlagToDestroy at T+24, slot free at T+25: **equal** |
| P3 touch, barrel, trunk, object | the Beannoid lives only on the pages of the maps 24 and 67, not on the pages of 22, 27 and 70 | the barrel (22) and the trunk (70) were thrown against the other hittable sprites of their own page (index 49, 142, 73, 193; 118, 175, 181): touch, Deactivated, flags `0x3323E4 -> 0x3323C4` (barrel) and `0x2323E4 -> 0x2323C4` (trunk, as the plan says); the times depend on the size of the target. **Not measured**: the exact chronology of the barrel, the trunk and the object against the Beannoid of the oracle (no page holds both). The object (363): no spark with the 14 sprites of its page, landing at T+22, flags `0x332384` unchanged (P1) |
| P4 plate (sprite 108 of the map 27, 48x32x16, at (178, 100)), object 27 rec 5 | `q8_p4.txt` | **equal**: ground 1 at T+20 (x=172, z=16), `RidingEntity` = the plate at T+21 (one tick later), stays |
| P4 pile of two (rec 5 on the plate, rec 31 put in the hands at the tick 80 and thrown at 90) | `s7_stack.txt` | **equal**: the second lands at x=163, z=30 (16 + 14), ground at the tick 107, support = the first at the tick 108 (one tick later), the first keeps the plate. The barrel on the plate is not measured (the page of the map 22 has no sprite 108); the binary says barrel and object are identical |
| P5 trunks on the water of the maps 70 (recs 9, 10, 11, 12, 21) and 73 (recs 11, 12, 36), and the trunk 70 rec 8 out of the water | `q8_p5.txt` | see below |

### P5, the one point that does not line up (STOP)

Binary (`q8_p5.txt`): the 8 trunks have `anim_t = 5`, `slope18C = 4`, ground 1, status 2 **at the end of the tick 1**; the trunk 70 rec 8 (out of the water) stays at animation 0, slope 0.

DLL, with the hook of f6a widened to the type 373 (`if (HasLogicalZ || SpriteType == 373)` at `EntityScriptProxy.cs:1824`, temporary, `probes/out/p5*`):
- Spawned during play by `SpawnEntityByRecordId` (`p5_after.txt`): `Slope_18c` 4 at the first tick, animation 5 at the **second** tick; before the widening (`p5_before.txt`): slope 0 and animation 0 for ever (red before, as the plan says).
- Real map load, hero arriving on the map (`p5b_after.txt`): frame 0 status Loaded; frame 1 (first Normal tick): `Slope_18c` 4, ground 1, animation 0; frame 2: animation 5, for the 5 trunks of the water (the 6th, 70 rec 8, stays 0 / 0).
  The counter of the runner shows why: the C program of a map-loaded entity runs for the first time at the **frame 2** (`C4 calls so far`: 0 at frame 1, 6 at frame 2), the frame 1 being the run of its Load program (A). A trial with the slope computed at the head of C4 (`p5b_after_preslope.txt`) does not change it: at the frame 1 C4 is simply not called.
- So the slope fed by the hook is equal to the binary at the tick 1; the animation 5 appears at the first run of C4, which is the tick 2 of the DLL and the "tick 1" of the oracle (whose harness `spawn_record_entities` places the entity directly in status 2, without a Load tick). This is the numbering convention of every native C program of a map-loaded entity in the DLL, not a defect of g2c1.
- What is NOT established: whether the binary's own map loader also spends the first tick of an entity on its Load program before C runs (then there is no difference at all). The oracle does not model the load.

The plan lists "dès le premier tick" for P5, so by the letter of the rule (a probe that differs from the binary at a listed tick) this is a stop. Recommendation: amend the text of (m7) of T2 to "animation 5 and slope 4 at the first run of C4 (the tick 2 of a map-loaded entity, the tick 1 of one born during play by a script)" and keep `FeedsGroundSlope` as designed (D-G2C1-1); the hook needs no other change. No code change is needed for the numbering.

## (f) The purchase path on the map 258 (static reading + HEAD runner)

Program B[2] (`shop/out/events258_all.txt`, `@116-241`), distinct opcodes: `0x37 Wait`, `0x07 Check entity in area`, `0x04/0x03 If false/true goto`, `0x43 Set LogicContextEntity`, `0x70 Is above ground`, `0x42 SetLogicContextEntity = PlayerEntity`, `0x10/0x11 Player lose/gain control`, `0x05/0x06 Flag on/off`, `0x59 Set entity anim`, `0x5C Dialog with entity`, `0x50 Set dialog choice`, `0x36 Wait until flag on`, `0x44 Wait dialog choice`, `0x51 Get dialog choice`, `0x39 Wait for dialog`, `0x2E Destroy entity`, `0x2D Activate entity`, `0x64 Set entities position`, `0x02 Goto`, `0x9A Enough money?`, `0x82 Give item`, `0x99 Try spend money`, `0xFF End script`, `0x00 Break`.
Programs of the articles (A: `0x41 [0,0]`, `0x65`; C[4]: `0x62 [128,128,3]`, `0x41 [2,4]`, `0x40 [2,0]`, `0x70`, `0xBD`, `0x25`, `0x03`): all the same kind.
Checked at the HEAD in `Alundra/Events/EventProgramRunner.cs`: every opcode above has a `case`. **Missing at the HEAD: `0x61` (Lurvy 398 B[1..7]), `0xB2` (258 and 298 B[6], the guard of the exit), `0xB9` (258/298/398 B[5], the jingle)** (`shop/out/opcov.txt`, confirmed by `rg "case 0x(61|B2|B9)"`: none). None is on the purchase path of the 258 (B[2]); `0xB2`/`0xB9` are in B[6]/B[5], so the purchase does not need them (the guard of the exit and the jingle of the life vessel do). So the path "lift the herb, throw it on the counter, the price dialogue, pay, give item 36, the article reappears" is runnable once the articles lift and release (g2c1), and (m10) of T2 can be written; the exit guard (`0xB2`) and Lurvy's loop (`0x61`) stay with theme 3 as the plan says (D-G2C1-9).

## (g) The barrel of the map 113 record 11 lifted, on the binary (C64)

`replay/c64_lift.py` (run on the oracle, `replay/c64_lift.txt`), real MovePlayer / TryLift / throw, the native C code of the barrel poked to 64 (the `0x41 [C,64]` of its A program), real C64 (`0x8007ADDC`, `replay/dis_c64.txt`).
C64 calls the search `0x8003C954(entity, 10)`; the oracle machine has not the table of the search groups, so the search is a harness hook (answers "nothing found", then "found = the barrel itself"). Result in both variants:
- the barrel is lifted at the tick 11 (carrier = the hero, `+0x28`), carried to (109, 100, 32);
- at the throw (tick 61) the hero writes `+0x24 = 2`, C64 never reads `+0x24` nor writes `+0x28` / `+0x2C`: **the barrel stays in the hands for ever** (tick 149: position (109, 100, 32), carrier = hero, `+0x24 = 2`, the hero in the carry stand 12);
- in the variant where the search finds the carried barrel, C64 clears Gravity (`flags 0x3322E4`).
The control run with C4 releases it at the tick 61 and lands it at (178, 100, 0) at the tick 83. So the plan's D-G2C1-10 is confirmed: the barrel of 113 rec 11 is liftable and sticks to the hands in the original (inference limit: the search is hooked, not run).

## (h) Predicted displaced tests (temporary wiring, then undone)

Temporary wiring (`temp-wiring.diff.txt`, `probes/TempC4.cs.txt`): C4 registered for the 19 types (the 7 + 304 + the 11 articles), the wired set widened to them, the hook widened to the type 373. Suites in Release with the neutralised copy, three disjoint partitions by complete namespace with the final dot (the third is the exact negation of the two others), `--blame-hang-timeout 60s`:

| Partition | Tests | Red |
|---|---|---|
| P1 (`Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.`) | 1349 | 5 |
| P2 (`Entities. Events. ExportGuards. Hero. Items.`) | 2956 | 2 |
| P3 (negation) | 1839 | 0 |

The 7 red tests (all green at the start HEAD: 24 test cases of the same filter green before the wiring):
1. `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (count of the registry)
2. `Alundra.Tests.Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses`
3. `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses`
4. `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem`
5. `Alundra.Tests.Arcs.LiftMap72ArcTests.M5_TheBarrelOfTheFamilyC4_GivesTheSword_TheJarOfTheSameSceneLifts` (witness m5 of g1b)
6. `Alundra.Tests.Hero.LiftPureTests.TheProductionSet_IsTheSevenTypesOfC23_AndTheTestSeamReplacesIt` (pin of the set)
7. `Alundra.Tests.Hero.LiftWorldTests.M5_AnObjectOutsideTheSet_GivesTheSword_NextToTheCrateOfTheSameSceneThatLifts` (witness m5 of g1b, the line of the type 290)

7 <= 10: no stop. The witnesses (m5) are rewritten by derivation on a type that stays outside the set (the torch, type 393, g2c2), as the plan says; the four registry pins by adding `ClassKeys` of the new class (`LiftableAi.C4Keys`); the set pin by the new list.
After the wiring the arcs of the maps 22, 72 and 392, the carry tests, the break tests and the traces of the map 389 stayed green. The suites rewrite the line ends of `docs/hero-trace-389-*.txt`: checked with `git diff --ignore-cr-at-eol --quiet -- docs/` (no real change), then restored.

## Stop

- Rule: "une sonde qui diffère du binaire à un tick listé" -> P5, the animation 5 of the floating trunks: tick 1 in the oracle, first run of C4 (tick 2) in the DLL for a map-loaded entity; the slope, ground and status are equal at the tick 1. Understood and without consequence for the design, but not established against the binary's own loader. The main session decides the wording of (m7); nothing else blocks T1.
- No need to touch the engine; 7 displaced tests (<= 10); no loop.

## State at the end of T0

`git status`: clean on `Alundra/`, `Alundra.Tests/`; only the two annex directories added (`docs/plan-e20-annexe/e20-g2c1/`, `docs/plan-e20-annexe/e20-g2c1-disc/`).
