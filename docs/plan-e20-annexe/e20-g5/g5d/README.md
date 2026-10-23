# E20.g5d: the scripted pushable objects (tests only), measured results

Plan: `docs/plan-e20-heros.md` section 1.24.6, sub-section "E20.g5d" (revision 1, READY). Discovery annex: `../disc/`; annexes of the previous slices: `../g5a/`, `../g5b/`. Start HEAD: `a27c4ffb` (branch
`chantier/theme1-porter-plateforme`, after E20.g5b). This file is written task by task; this first version is T0 (the commit of docs).

## T0 - replays, freeze, montage, static test at the start HEAD

### The probes replayed on the real DLL of the start HEAD

How: the discovery's probe (`../disc/probe_G5ProbeTests.cs.txt`) was put for the time of the measure in `Alundra.Tests/Probes/G5ProbeTests.cs` (untracked, removed before the commit; it compiles unchanged against the
HEAD), built in Release with the neutralised copy, and run with `G5_CASES`, `G5_OUT` and `G5_ONLY`. Deviation from the plan's wording ("dans une copie"): the real tree was used instead of a copy of the tree (a copy holds the 1.9 GB of
`alundra-project/`); the neutralised copy keeps the DLL of the project untouched, and `git status` shows nothing of the probe after its removal. The runs are the real DLL with g5a and g5b in it.

| probe | case | reference | result |
|---|---|---|---|
| `cases1.json` | `statue2_east` | `../disc/probe_out/statue2_east.txt` | equal (see the format note), trace equal |
| `cases10.json` | `statue352_east` | `../disc/probe_out_proto/statue352_east.txt` | equal, trace equal |
| `cases3.json` | `block23_from_east` | `../disc/probe_out_proto/block23_from_east.txt` | equal, trace equal |
| `cases3.json` | `block23_from_east_waited` (extra) | `../disc/probe_out_proto/block23_from_east_waited.txt` | equal, trace equal |

Format note: `probe_out/statue2_east.txt` was written by an older version of the probe, which printed no `d`, `f` and `ctl` columns for the hero. The comparison is made after projecting those three columns out of the new
output (`sed -E 's/ d[0-9]+ f[-0-9.]+,[-0-9.]+ ctl[0-9A-F]+//'`): equal line for line (320 frames). The others are equal byte for byte (line endings aside).

No difference is to be explained by g5a or g5b: none of the three entities uses the pillar handler C76 or `0x72`, `0xA9`, `0xAA`. The outputs are in `t0-out/` (`statue2_east.txt`, `statue352_east.txt`,
`block23_from_east.txt`, `block23_from_east_waited.txt`).

Gap with the plan's "Faits": the plan says of the block of the map 23 "contact à l'image 31". That is the **waited** variant (`block23_from_east_waited`: the hero idles 30 frames, then holds Left). Without the wait, the hero starts
blocked and the contact is at the pad frame 1 (`xc=12` in `block23_from_east`). The block then starts 3 frames later in both runs (animation 3 at the contact + 2, first step at the contact + 3: frames 4 for the first,
34 for the second). The tests pin both.

### The values (the pad frame n is the n-th frame a direction is held after the hero is placed)

Read from the probes above and from the in-repo run of `ScriptedPushRun` (`t0-out/dump-s1-s3.txt`, a scratch dump, not kept as a test):

| scenario | contact (hero blocked by the record) | program F (slot 5) | first / last frame the object moves | end |
|---|---|---|---|---|
| statue of the map 2, record 9, hero (396, 392, 208) holds Right 260 frames | frame 2 | `0x05` at @1392 on the frame 3 | 5 / 21 (17 steps of 1.5 px, x 420 to 445.5; the script asks for 24 px, the last step overshoots) | x 445.5, animation 0, the flag 889 (3 << 8 \| 121) on, the transient flag 0x8064 back off |
| statue of the map 352, record 8, hero (168, 808, 32) holds Right 50, idles 3, holds Right 120 | frame 7 | `0x05` at @1900 on the frame 8 | 10 / 26 (17 steps, x 204 to 229.5) | x 229.5, the flags 230 \| 3 << 8 and 90 \| 128 << 8 on |
| block of the map 23, record 12, hero (800, 832, 32) holds Left 200 frames | frame 1 | `0x05` at @1336 on the frame 2 | x: 4 / 27 (24 moves: 2.0625 px per frame, x 768 to 720); z: 28 to 35 (31.5, 30.5, 29, 27, 24.5, 21.5, 18, 16) | x 720, z 16, on the ground at the frame 35; the flag 133 on |

### The freeze of the hero after a program F (Q-G5-A2, D-G5D-3)

Measured on the statue of the map 2 (`cases_gel.json`, `t0-out/statue2_gel.txt`): the hero holds Right at (396, 392, 208); the contact is at the frame 2 (the hero at x 399, `xc=9`); from the frame 3 the hero's forces are 0
and he stays at x 399 for all the 38 frames Right is still held (frames 3 to 40), including after the statue stopped (frame 22). Right released for 5 frames (41 to 45) then held again from the frame 46: the hero leaves at once
(x 400.219 at 46, 402.656 at 47, then 2.438 px per frame) and walks to x 424.5 (21 px from the statue at 445.5, as at the first contact), where he is blocked again. The same run with the statue not touched (hero at x 380, nothing
held, `statue2_nopush`): nothing moves for 60 frames. This is the binary's behaviour (`0x80042150` zeroes `+0xD8`, `+0xD4`, `+0xC8` and `+0xC4` of the hero at the start of a program F), kept by the DLL
(`EventProgramRunner.RunScript`, the `ProgramFInteract` case, four assignments to `ForceStepY`, `ForceStepX`, `ForceY`, `ForceX` of the player).

### D-G5D-4: the blocks of the maps 102 and 382 HAVE a montage

The blocks hit by an attack of index 1 (the program D: `0x87 [128, 1]`, nibble 2) can be driven without the attack system: the test seam `WorldProxy.HitPassHookForTests` (internal, E20.a A8) runs at the hit-pass stage
of each tick, where the production hit pass writes `TouchingEntity`. A made-up attacker (an `Entity` with an `EntityScriptProxy` whose `CurrentAttack` is `BalanceAttack(0x02, 1)` and whose `HasFrameBox` is true; the nibble of
`TypeNibbles[1]` is 2) is written into the block's `TouchingEntity` by the hook for one tick. The production counters pass clears it after the programs, so the block's program D runs exactly once, on the next tick (the pad
frame 7 when the hook fires on the frame 6). Measured (`t0-out/dump-s6-montage.txt`, the real maps, `ScriptedPushRun`, no production code touched):

| case | hero | block | result |
|---|---|---|---|
| map 102, record 0, hero in the east zone (tiles 18-21 x 47-51, level 4-5) | (456, 792, 64) | (360, 792, 64) | D at the frame 7: the bit 0x1 of the entity flags (`ClassA`, 0x36081 -> 0x36080) is cleared; C walks it west: x 360 to 310.5 (49.5 px), then z 64 to 53.5 to 0 (it falls) |
| map 102, record 0, hero in the north zone (tiles 15-17 x 46) | (372, 744, 64) | (360, 792, 64) | after D the block walks south, y 792 to 865 (1 px per frame, then it falls to z 32) |
| map 102, record 0, hero in no zone | (456, 700, ~32) | (360, 792, 64) | D runs (`ClassA` cleared then set again by C); the block does not move |
| map 382, record 7, hero in the zone (tiles 29-30 x 40-41, level 3-10) | (710, 655, 48), (708, 650, 48), (720, 660, 48) | (720, 704, 48) | D at the frame 7, the block slides south from the frame 8 to y ~737 (32 px, 1 px then 0.5 per frame), animation 2 then 0 |
| map 382, record 7, hero outside the zone | (696, 700, 80) | (720, 704, 48) | D runs, `ClassA` set again by C, nothing moves |

So (s6) is written: the montage exists with no production change. (The map 102 case from the north falls from z 64 to z 32: the geometry is the map's own, pinned as measured.)

### The static test at the start HEAD

`PushObjectProgramsOpcodesTests` (new, `Alundra.Tests/Events/`, still untracked at this commit): the same walk and oracle as `StoryChainSkippedOpcodesTests`, restricted to the programs of the placements of the types 229, 298, 345,
430 and 433. At the start HEAD it finds **nothing**: 139 placements on 26 maps (229: 9 on 4 maps, 298: 62 on 11, 345: 16 on 2, 430: 24 on 8, 433: 28 on 5), 75 programs on 19 maps, 890 sites, 56 distinct opcodes
(`t0-out/static-corpus.txt`), every one ported (`unported:` empty). The 111 placements of the four first families are the census of the discovery; the 28 Mini-Nilude blocks are new in this count. No stop.

### Files

`t0-cases/` (`cases_gel.json`, `cases_list.json`), `t0-out/` (the outputs above).

## T1 - the tests

Two new files in the mirror folder `Alundra.Tests/Events/` (ADR-0080; the plan says `Arcs/` for the arcs, but g5b settled that a file that drives `ArcRun` through `ScriptedPushRun` stays in the domain of the scripts it
exercises, and `ScriptedPushRun` is in `Events/`; `check_names.py --rules`: 215 DLL files, 467 test files, 0 finding, the same 1 warning). `ScriptedPushRun` itself is not touched.

| file | content |
|---|---|
| `ScriptedPushObjectsTests.cs` | (s1) the statue of the map 2, (s2) the statue of the map 352, (s3) the block of the map 23 (plus the waited variant and the fall tick by tick), (s4) the freeze of the hero after a program F, (s6) the blocks of the maps 102 (records 0 and 9, three hero zones) and 382 through the montage of the hit pass |
| `PushObjectProgramsOpcodesTests.cs` | (s5) the static test (D-G5D-2): the corpus test, the guard of the counts (9 + 62 + 16 + 24 + 28 placements, 26 maps, 19 maps with programs, 75 programs), the rule on made-up input, the recognition of a placement by type and map-page bit |

Names (6 + 4 = 10 tests):
- `ScriptedPushObjectsTests.Map2_AStatuePushedFromTheWest_WalksSeventeenStepsOfOnePointFivePixels_AndRaisesItsFlag_WithoutThePushNothingMoves` (s1)
- `ScriptedPushObjectsTests.Map352_AStatuePushedFromTheWestWithTheCrossHeld_WalksTo229Point5_AndRaisesItsFlag_WithoutThePushNothingMoves` (s2)
- `ScriptedPushObjectsTests.Map23_ABlockPushedFromTheEast_SlidesFortyEightPixelsWest_ThenFallsOneFloor_AndTheContactInstantOnlyShiftsIt_WithoutThePushItStays` (s3)
- `ScriptedPushObjectsTests.Map2_TheHeroWhoKeepsPushingAfterTheContactIsFrozen_UntilHeReleasesAndPressesAgain` (s4)
- `PushObjectProgramsOpcodesTests.EveryProgramOfAPushableObject_ReachesOnlyOpcodesTheRunnerPorts`, `TheCorpusIsTheFiveFamilies_...`, `TheRule_FlagsASiteWhoseOpcodeTheOracleSkips_AndOnlyThat`, `ThePlacementsOfATileMap_AreRecognisedByTheirTypeAndTheMapPageBit` (s5)
- `ScriptedPushObjectsTests.Map102_ABlockHitByAnAttackOfIndexOne_WalksAccordingToTheHeroZone_AndWithoutTheHitNothingHappens` and `Map382_ABlockHitByAnAttackOfIndexOne_SlidesSouthWithTheHeroInTheZone_AndWithoutTheHitOrOutsideTheZoneItStays` (s6)

Green at the start HEAD (Release, the neutralised copy, `--filter` on the two classes): **10 tests, 0 failures**. They pin a behaviour that already works (no red phase, as the plan says); each is made red once by a real mutation
of the production code (applied by `t1/g5d-mut.py`, undone by `git checkout`, `git diff -- Alundra` empty after each; full output `t1/mutants.log`):

| mutation | file | tests red |
|---|---|---|
| M1 the flag of a program F not raised (`case 0x05` skipped while the program running is the F slot) | `EventProgramRunner.cs` | (s1), (s2), (s3), (s4) |
| M2 the opcode `0x3A` (used by the statues and the buttons) removed from the dispatch (`case 0x13A`) | `EventProgramRunner.cs` | (s5) corpus test alone |
| M3 the start of the program F at the contact removed (`false && ...` on the pick) | `EntityScriptProxy.cs` | (s1), (s2), (s3), (s4) |
| M4 the four force words of the hero no longer zeroed at the start of a program F | `EventProgramRunner.cs` | (s4), and (s1), (s2), (s3) |
| M5 `0x87` never answers 1 | `EventProgramRunner.cs` | both (s6) tests |

(The first four are the four mutations of the plan's T2; they are run here for T1 and counted there. T2 runs them again on the final tree only if the code changed, which it does not: `Alundra/` has no diff.)

Facts measured while writing, not in the plan:
- the statue of the map 352 program F runs a second time, on the frame 65, in the sequence of the probe (the hero, who followed the statue after the release, touches it again): pinned (`FramesOf(5, 1900, 0x05)` is `[8, 65]`);
- the block of the map 102 with the hero in the east zone falls to z 0 (the floor of the map there), the one with the hero in the north zone to z 32; the block of the record 9 walks east.

## T2 - mutations, suites, proofs

### The four mutations of the plan, and the fifth of (s6)

Run in T1 (above, `t1/mutants.log`), real edits of `Alundra/` applied by `t1/g5d-mut.py` then undone by `git checkout`; the production code did not change between that run and the suites below (`git diff a27c4ffb -- Alundra` is empty),
so they are not run again.

| mutation of the plan | test the plan expects | red (measured) |
|---|---|---|
| the flag of the program F not raised (M1) | (s1) | (s1), (s2), (s3), (s4) |
| an opcode of the families removed from the dispatch (M2, `0x3A`) | (s5) | (s5) corpus test |
| the start of the program F at the contact removed (M3) | (s1), (s2), (s3) | (s1), (s2), (s3), (s4) |
| the reset of the four words of the hero removed (M4) | (s4) | (s4), (s1), (s2), (s3) |
| added: `0x87` never touched (M5) | (s6) | both (s6) tests |

### Suites (foreground, `--blame-hang-timeout 60s`, three partitions as in g5a and g5b)

| configuration | Arcs, Ai, Audio, Cameras, Cells, Combat, Data, Dialogues | Entities, Events, ExportGuards, Hero, Items | the rest | total |
|---|---|---|---|---|
| Release (neutralised copy) | 1 210 / 0 failed (4 min 23) | 2 607 / 0 (3 min 52) | 1 789 / 0 (45 s) | 5 606, 0 failed |
| Debug (last, copies the DLL) | 1 210 / 0 (4 min 39) | 2 607 / 0 (4 min 07) | 1 789 / 0 (46 s) | 5 606, 0 failed |

5 606 = 5 596 (g5b) + 10 new tests. The partition "Entities ... Items" went from 2 597 to 2 607 (the 10 new tests are in `Events.`).

- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 215 DLL files, 467 test files, 0 finding, 1 warning (`TextBoxGpu.cs:266`, the one at the start).
- `git diff a27c4ffb -- Alundra` empty; `git diff --name-only a27c4ffb HEAD` lists only `Alundra.Tests/Events/` (2 new files) and `docs/plan-e20-annexe/e20-g5/g5d/`. The engine, the analyser, `data-extracted/`,
  `alundra-project/` and `tools/`: nothing committed (the two submodule entries `CasaEngineMonogame` and `alundra-datas-analyser` were already modified in the working tree at the start of the session and are not touched).
- The four `docs/hero-trace-389-*.txt` rewritten by each suite are equal but for the line endings (`git diff --ignore-cr-at-eol --quiet -- docs/` clean after each run) and were restored by `git checkout`; never indexed.

### Existing tests that moved: measured against the predicted list

Predicted (the plan: "Aucun test existant ne bouge"): none. Measured: `git diff --name-status a27c4ffb HEAD -- Alundra.Tests` shows two added files and no modified one; no existing test failed in any run. Equal to the prediction.

### Deviations from the plan

- The probes of T0 were replayed in the real tree through a temporary untracked probe file instead of "in a copy" (see T0).
- The plan's "contact à l'image 31" for the block of the map 23 is the waited variant; the unwaited probe has the contact at the frame 1. Both are pinned (T0).
- The tests are in `Alundra.Tests/Events/`, not `Arcs/` (the precedent of g5b, ADR-0080: the scripts' domain).
- Nothing else; no `Alundra/` file, no stop.
