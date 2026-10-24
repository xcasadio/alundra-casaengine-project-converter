# E20.g3 - execution record

Plan: `docs/plan-e20-heros.md` section 1.28 "E20.g3" (READY; Q-G3-a to Q-G3-d answered by the author on 2026-10-22, defaults (A) confirmed). Branch of the parent `chantier/theme1-porter-plateforme`.
Start HEAD of the execution: `73c05530` (E20.g5c closed). Discovery annex: `docs/plan-e20-annexe/e20-g3/` (`bin/`, `dll/`, `critic/`, `critic-recon/`, `critic-complet/`).

## T0 - annex, oracle, probes (no code commit)

### Sites of the plan re-located by content at the start HEAD (the plan dates from 2026-10-18; the line numbers moved, the substance is the plan's)

| Plan site | Found at the start HEAD |
|---|---|
| `MovementObstacleProbe.cs:51`, the only production site | still `EntityCollision.FindEntityCollisionCandidate(proxy, candidateX, candidateY, proxy.PosZ, _host.Collidables, EntityFlags.PickupKindMask)`, lines 49-51 |
| `EntityCollision.cs:32-39` (doc and parameter `skippedCandidateFlags`), `:53` | same text, the parameter at line 39, the skip at line 53 |
| `EntitySupport.cs` `BuildCollidables` filter `FlagToDestroy`, `IsEligibleSubject` (`Collidable`, `AnimFlags & 0x80`, `PlatformEntity == null`) | lines 55 and 73-76; the comments "always null" (lines 36, 68-70) are still there and become false with g1a (`PutInHands` writes `PlatformEntity`, `PlayerManager.cs:1310`) |
| `EntityContactArcTests` TH4 redundant line | `Alundra.Tests/Arcs/EntityContactArcTests.cs:488` (class renamed by R*) |
| `DialogueOpcodesProductionTests` mirror of the probe | `Alundra.Tests/Dialogues/DialogueOpcodesProductionTests.cs:392` |
| `MovementObstacleProbeTests` TR1 (`skippedCandidateFlags`), `TRLift` | `Alundra.Tests/Physics/MovementObstacleProbeTests.cs:133` (TR1) and `:339` (`TRLift_...`) |
| docs of the "known gap" | `ShipCorridorArcTests.cs:25` ("the crates ... do not block in the DLL, a known gap kept for E14"), `MovementObstacleProbe.cs:13,23`, `EntityCollision.cs:33`, `MovementObstacleProbeTests.cs:17,336,344`, `EntityDestructionTests.cs:19,46` |
| `WorldProxy.cs:2994-2995` "recomputes the carrier once per frame" | **no longer there**: D5b rewrote the text (`WorldProxy.cs:3034` says the carrier is recomputed at the lists step, per tick). Nothing to correct in `WorldProxy.cs` any more (a reduction of the scope of T1, not a conflict) |
| `XCollisionEntity` writer, `H2Obstacle ?? H1Obstacle` | `EntityScriptProxy.cs:2654-2660` |
| no slide after an entity contact | `ScriptedMotion.cs:891` (`forbidden = entity.XCollisionEntity != null || ...`) |

No difference of substance with the plan.

### (a) Replay of the scripts of `bin/` and `critic/` from a copy (the copy of `e20-g1a/work/` as `g1a/` next to them)

Scratch copy, outputs compared byte for byte with the committed ones (`cmp`). **Finding**: `docs/plan-e20-annexe/e20-g3/bin/` is matched by `.gitignore` line 21 (`[Bb]in/`): the 37 files of the discovery's binary reading exist on disk but are **not tracked** (`git ls-files` is empty for them; `bin/MANIFEST.txt` and its SHA-1 list verify on disk). The other four folders are tracked. Not changed here (flagged).

| Script | Command (from the copy) | Result |
|---|---|---|
| `a1_vec.py` | `python -B -I a1_vec.py 6000` | identical (6 000 vectors, 39 657 calls per function, 0 mismatch) |
| `a1_mut.py`, `a1_mut_classify.py` | `a1_mut.py 300`, `a1_mut_classify.py` | identical (526 mutants, 438 killed, 88 survivors) |
| `a1_scen`, `a1_overlap`, `a1_destroyed`, `a2_contact`, `a2_cei`, `a3_contactmap`, `a3_hug`, `a3_r3b`, `a3_fullroute`, `a4_flight` | `python -B -I <name>.py` | identical |
| `a3_routes.py` (+ `a3_route_R2/R3.txt`), `a3_edges.py`, `a3_jumpx.py` | `python -B -I <name>.py` | identical |
| `a3_sweep.py` east | `a3_sweep.py east 294.5 299 0.25 8 26 720` | `a3_sweep_east_y295_299.json` identical (361 jobs); the committed `a3_sweep_east_summary.txt` is a post-processed text (other format), not the stdout |
| `a3_sweep.py` west | `a3_sweep.py west 293 299.5 0.25 8 26 560` (the committed file name says 299, the data say 299.5: 27 rows, 513 jobs) | `a3_sweep_west_y293_299.json` identical; stdout equals the committed summary |
| `a4_census.py` | `PYTHONIOENCODING=utf-8 python -B -I a4_census.py` | identical (the file is written before the print that fails on cp1252) |
| `xref.py`, `scan_off.py` | `python -B xref.py 80036f34 80036bfc 80036d94 800364c8 80036828 80037364`, `python -B scan_off.py 6c sw` (**without `-I`**: `capstone` lives in the user site) | identical |
| critic `w0_smoke`, `heroanim_raw`, `jt`, `flagwrites`, `m392_cells`, `a3_fullroute` | `python -B <name>.py` | identical |
| critic `w1_window`, `w3_door`, `w4_contact`, `w5_airlift`, `w6_westedge`, `w7_return` | `python -B <name>.py` (stdout) | identical (the committed file has no final blank line) |
| critic `w2_trace.py` | `west 294.5` and `west 298` | `294.5` identical; `298`: the 5 committed lines are a subset of the 19 lines (the committed run asked tj 8, 10, 12, 14, 16 only) |
| `dll/` (`census_liftables --json`, `chain_map_names`, `placement_overlaps`, `portal_arrivals`, `segment_check`, `spawn_overlaps`, `warp53_arrivals`, `walkers 390 392 163 164 165 172 178 179`, `reach_check` same maps) | `python -B <name>.py` | identical, up to the non-ASCII characters of the committed captures (cp1252) and the order of two lines of `reach_check`; `all_records.json` rebuilt: SHA-1 `39fcac7a90cbf5c0bbb40b08e5583aa6a4cccd65`, equal to `dll/MANIFEST.txt` |

Binary: `ALUN_CD.EXE` (France) `a705f8dab665645af5dfaf8fb348c7926b5caa58` (the oracle scripts read it at `D:\development\repo\Alundra Remake\...`).

**Stop check "a portal arrival of the chain overlaps a liftable"**: `portal_arrivals.py` replayed: 3 XY overlaps in the whole export (portals 397.0 and 380.0 -> map 3 rec12, portal 128.35 -> map 125 rec42), **none on a map of the story chain**; the nearest gap of the chain arrivals stays 33 px or more. No stop.

### (b) DLL probes on a working copy where the only change is the argument of line 51

`probes/line51.diff` (applied by `sed -i` to the working tree, never staged, restored by `git checkout -- Alundra/Physics/MovementObstacleProbe.cs`; `git diff` empty and SHA-1 `31d8d62faeaeba5e930dd719c1e73fd8d09781bf` equal to the start). The probes are a measurement: `probes/G3T0ProbeTests.cs.txt` (a temporary xunit class in `Alundra.Tests/Probes/`, removed after each run, never committed; 6 tests, they write lines to the file named by `G3T0_OUT`). World: the real map 392 (12 crates, real prefabs, real hero, `ArcRun`). B1 polls the pad every 16 frames from the frame 220 (a held direction with the hero on the ground rolls him), so every walk is a window of at most 14 ticks that starts the frame after a poll; a fractional start is written on the root (`Teleport` truncates to whole pixels). Outputs: `probes/dll-before-line-change.txt` (start code: the crates are traversed, as known) and `probes/dll-after-line-change.txt`.

| Probe | Binary (oracle) | DLL with the line changed | Verdict |
|---|---|---|---|
| **P-G3-1** east, (684, 296) LEFT | stops at x = 657.000, contact rec5 on the flush tick | x = 657.0000 at tick 12, `xcoll = rec5` at tick 12, `ForceAdjusted` 1 from tick 13 | equal |
| **P-G3-1** west, (600, 296) RIGHT | stops at x = 615.000 | x = 615.0000 at tick 7, rec5 | equal |
| **P-G3-2** ground lanes y = 296, 311, 312, 327, 328 | rec5, rec6, rec6, rec7, rec7 (`a3_contactmap`, `a3_routes` R1) | rec5, rec6, rec6, rec7, rec7, all at x = 657.0000, first contact tick 12 | equal (the lower crate of the pile, the first of the list) |
| **P-G3-2** from the top of rec5, (636, 296, z 80) DOWN | y limited to 297.0, contact rec10 (R7: y = 297.000 at t038 with contact 0, rec10 from t039) | y = 297.0000, contact rec10 **from the tick the step is shortened** (tick 2 here, the tick of arrival at 297.0), `ForceAdjusted` 1 from tick 3 | identity and limit equal; **the contact is one tick earlier than the binary** (see "Flagged") |
| **P-G3-3** sweep x0 = 684 + k/32, k = 0..78 (one step period) | contact on the flush tick for 78 of 79 starts, one tick later for 1 (the full step accepted and ending exactly flush) | **78 of 79 on the flush tick, 1 one tick later** (k = 33: flush at 657.0000 with no contact, contact the tick after) | equal |
| **P-G3-3** Square on the flush tick / one tick later (5 starts, x0 = 680 + k/32) | sword (anim 16) / lift (anim 5) | anim 16 / anim 16: the lift is g1b (`PlayerTryAction` is a stub in the DLL); only the contact of the tick before is comparable (above) | not measurable before g1b |
| **P-G3-4** hero inside the face by d px, RIGHT x12 | d = 0.5, 1.0, 1.2: out at tick 1 (657.719 / 657.219 / 657.019 ...); d = 1.3, 2.0: blocked at tick 1, out at tick 2 (655.7 -> 658.137, 655.0 -> 657.438), contact rec5; d = 5, 10: stuck at 652.0 / 647.0 for ever | the same values to the third decimal (657.719, 657.219, 657.019; 655.700 -> 658.138; 655.000 -> 657.438; 652.000 and 647.000 stuck), contact at tick 1 rec5 from d = 1.3 | equal (no exit rule in either) |
| **P-G3-5** a collidable walker against a crate (flat ground, real flags of the crate `0x3323E4`) / a non-collidable one | `a1_scen`: the walker with `0x80` stops at 657.0 and the contact is the crate; the Beannoid (header `0x67`, no `0x80`) crosses (x = 615.6 at tick 45) | the collidable walker stops flush at 120.000 (crate box from 130) with the crate as `XCollisionEntity`; the non-collidable one reaches 180.000, no contact | equal in rule (synthetic rig, not the map 14) |
| **P-G3-6** diagonal A, (664, 314) LEFT+UP | stops at (657.0000, 309.3333) at tick 5 and stays, `adj` 1, rec5 | x = 657.0000 from tick 5, `xcoll = rec5`, **y keeps going up to 298.4895 at tick 14** (the slide along the column) | the consigned gap (Q-G3-d, D-E19-58), measured |
| **P-G3-6** diagonal B, (664, 300) LEFT+DOWN | stays at (657, 304.6666) | x = 657.0000 from tick 5, y goes on to 315.5105 at tick 14, contact rec5 then rec6 from tick 12 | the same consigned gap |
| **P-G3-6** C, (700, 312) LEFT+UP x14 | stuck at the threshold (694.5, 295) after 55 ticks (a2 case 7) | window too short to reach the wall (676.7343, 296.4895 at tick 14, no contact); not conclusive | not conclusive (the same family as A and B) |

Before the change (`dll-before-line-change.txt`): the hero and the villager cross the crates (x = 651.09 at tick 14; the walker at 180.0), the P-G3-3 table has no contact, P-G3-4 shows the hero going on at 2.4375 px per tick from every overlap.

### (c) The arcs "to measure" on the same working copy

Run on the working copy with the line changed (Release, neutralised copy): `InoaDayOneArcTests.A10_...` and `A11_...`, `Day3SceneArcTests.A14_...` and `A18_...`, `MurutaHallArcTests.H115_...`, plus `EntityContactArcTests.TH4_TheArrivalOfTheChainOverlapsNoEntity` (13 arrivals), `BeannoideGrateArcTests.G116_...`, `ShipCorridorArcTests` A5 and A5r, `LeverArcTests` map 115: **20 of 20 passed**, no pin moved.

### (d) The whole suite with the only line changed (P-G3-7)

`--list-tests` at the start HEAD: **5 994** tests (the 5 966 of g5c at its T0, plus the 24 + 4 of its phases; the probe class is not in this count: it was removed from the tree for the suite). Partitions by the filter of `part.sh` of g5c (Release, `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, `--blame-hang-timeout 60s`):

| partition | tests run | failures |
|---|---|---|
| 1 (`Tests.Arcs`, `Tests.Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues`) | 1 316 | 0 |
| 2 (`Entities`, `Events`, `ExportGuards`, `Hero`, `Items`) | 2 888 | 0 |
| 3 (the negation of both) | 1 798 | **1** |

The sum 6 002 is 5 994 + **8 tests counted twice**: the substring filters `Tests.Ai.` / `Tests.Hero` / `Tests.Events` also match a class name ending in `Tests.` followed by a method name starting with `Hero`/`Events` (for instance `EnemyGroundTests.Hero_FloorHeight_...`). The 5 994 unique tests were checked by name (`--list-tests` of each filter: 1 316 + 2 888 + 1 798 - 8 = 5 994, `uniq -d` finds the 8, all in partitions 1 and 2). T1 and T2 should use filters with the full namespace (`FullyQualifiedName~Alundra.Tests.Hero.`) to avoid it.

**Predicted list of the existing tests that move with the one line changed: exactly one, red:**

1. `Alundra.Tests.Physics.MovementObstacleProbeTests.TRLift_ACrateOnThePathDoesNotBlock_AnIronBallWallDoes` (`Expected: 160, Actual: 120,000008`: the walker is stopped flush against the crate). Plan: "rouge certain".

Nothing else is red. The tests and texts that the plan lists as "green but to rewrite" stay green with the line alone and move only with the removal of the parameter (T1): `MovementObstacleProbeTests.TR1_SkippedCandidateFlags_LeaveTheSoulevablesOutOfTheSearch` (removed with the API), `EntityContactArcTests` TH4 line 488 (the redundant `Assert.Null(... EntityFlags.PickupKindMask)` removed), `DialogueOpcodesProductionTests` line 392 (the mirror of the probe aligned), and the texts: `ShipCorridorArcTests.cs` doc (line 25), `MovementObstacleProbeTests` doc and the section header of TRLift (lines 17, 336, 344), `MovementObstacleProbe.cs` doc (lines 13, 23-24), `EntityCollision.cs` doc (lines 32-33), `EntitySupport.cs` comments (lines 36, 68-70), `EntityDestructionTests.cs` comments (lines 19, 46). At most 1 existing test moves (limit of the plan: 10).

The four `docs/hero-trace-389-*.txt` were rewritten by the suites (line endings): `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, restored by `git checkout`, never indexed. The engine, the converter, the data and `alundra-datas-analyser` are untouched; no engine need appeared.

### Flagged (not a stop)

1. **Contact one tick earlier from the top of a crate (P-G3-2).** The binary (R7 of `a3_routes`) gives the contact `rec10` one tick *after* the hero reached y = 297.000 when he stands on rec5; the DLL gives it on the tick that shortens the step, as on the ground (where the binary does the same, `critic/` R1, 160 of 160 runs). This is the "previous attempt" semantics that `critic/` R1 gave as an INFERENCE for a mover in the air or on a crate, and `critic-recon/` Unknown 4 (the DLL returns the obstacle of the last blocked attempt). Identity (rec10) and the limit (297.0) are equal. It matters only for a lift from the top of a crate (g1b, Square on the very tick of arrival). Proposal: the T1 test (s2) pins the identity and the limit from the top and leaves the tick out, and the ADR records the one-tick gap. To be confirmed by the main session.
2. `bin/` of this annex is not tracked (`.gitignore` line 21).
3. The comment of `WorldProxy.cs` named by the plan no longer exists (D5b); the scope of T1 loses that file.
4. The filter overlap of `part.sh` (8 tests counted twice), see (d).
