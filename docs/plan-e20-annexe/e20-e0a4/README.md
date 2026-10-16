# E20.e0a4 - annex of the oracle, the remeasure and the predicted lists (T0 of E20.e0a4)

Plan: `docs/plan-e20-heros.md`, section 1.16.4 (E20.e0a4, tasks T0 to T4). This folder holds the oracle of the slice (what an entity born from a RECORD by a script - opcodes `0x2D`, `0x8A`, `0x8B`, the real record
spawner `0x8003A1B8` - does in the tick of its birth and after), the remeasure of the existing suite at the exact placement of the slice, the list of the tests that move per phase, and the subjects of the new tests.
The oracle is real code of `ALUN_CD.EXE` (France) executed by the emulated machine of the E14/E20 oracles (the annex `e20-e0a5/oracle/tools`: the f-projectiles machine, real `MovePlayer`, real record spawner, real
`InitializeEntity` and its first switch, real animation, physics and hit passes, real opcode handlers `0x2D` `0x8003DCC4`, `0x8A` `0x80040284`, `0x8B` `0x8004033C`; effects, sounds and the other handlers are journalling
stubs). Nothing here knows an expected value of the DLL: every number of `oracle/out/` is read from the machine's RAM.

## Run

The five scripts are byte copies of the discovery files and import the annexes `e20-e0a`, `e20-e0a5` and `e20-e0k` from a `repo/` sub-folder of their own folder; they write into `out/` next to them. Run from a SCRATCH COPY,
foreground, about 20 seconds, `python -B`:

```
mkdir run run/out run/repo
cp oracle/*.py run/
git archive HEAD docs/plan-e20-annexe/e20-e0a docs/plan-e20-annexe/e20-e0a5 docs/plan-e20-annexe/e20-e0k | tar -x -C run/repo
cd run
python -B rec_tick.py            # out/rec_tick.json, out/rec_tick.txt (one row per birth, tick by tick)
python -B rec_tick_detail.py     # out/rec_tick_detail.txt (pass by pass)
python -B rec_birth_clock.py     # out/rec_birth_clock.json, out/rec_birth_clock.txt (the shape of e20-e0a5/oracle/out/birth_clock.json)
python -B box_table.py           # out/box_table.txt (box at the creation per row)
python -B cmp_v6.py              # out/cmp_v6.txt (REC-H1/REC-H2 against the interface rows BT-H1/BT-H2 of e20-e0a/oracle/out/v6.json)
```

## Determinism (T0 acceptance)

Two complete runs, each in its own scratch copy (`runA`, `runB`) with a fresh `git archive` of the three annexes at the HEAD `0ab44d0`, gave the same md5 for the seven outputs (`oracle/out/manifest.txt`, `diff` of the two
`md5sum` lists empty); the md5 of `rec_tick.json` (`c49310a0...`) and of `rec_birth_clock.json` (`7ff72238...`) are those of the discovery of 2026-10-16, and the discovery's `out/` folder is byte-identical to both runs.
No row contradicts the model of the plan: `cmp_v6.txt` says `REC-H1-SPR3` is IDENTICAL to `BT-H1` and `REC-H2-SPR3` to `BT-H2` on status, trigger, `+0x1C`, `+0xA8`, lists and counter for the five passes of S and S+1;
`rec_birth_clock.txt` says `REC-LOAD` is IDENTICAL to `LOAD27` of e20-e0a5 (k = 1 to 33) and `REC-ME` to `WAVE` relative to S (k = 0 to 27).

## Participation and clock, as the oracle gives them (k = tick - S)

| Birth | Inherited trigger | Program A turns | Status 2 | `+0x1C` / `call_no` at S | Lists | First clock call | `end_AC` first k | Rows |
|---|---|---|---|---|---|---|---|---|
| Map-event pass (`mapev`) | no effect | k = 0 (once) | k = 0 | 1 / 1 | k = 0 | k = 0, counted | 25 | `REC-ME`, `REC-ME-8A` (`REC-ME-SW0`: 16) |
| Loop 2, trigger 0 | 0 | k = 0 (status 1) then k = 1: "twice" | k = 1 | 1 / 0 | k = 1 | k = 1, counted (`call_no` 1) | 26 | `REC-H1`, `-8A`, `-8B`, `-NAT`, `-SA`, `-H6`, `-S20` |
| Loop 2, trigger -1 | -1 | k = 1 | k = 1 | 1 / 0 | k = 1 | k = 1, counted | 26 | `REC-H2`, `-H3`, `-H5`, `-S20` |
| Map load, out of the slice | - | k = 1 (tick 1) | tick 1 | 1 / 1 | tick 1 | tick 1 | 26 (the DLL: 27, unchanged) | `REC-LOAD`, `HERO` |

Switch in the window, loop 2: first `end_AC` at k = 17 (`REC-SW0`, `-SW1`), 18 (`-SW2`), 21 (`-SW5`), 28 (`-SW1-2`), 27 (`-SWD1`), 28 (`-SWD2`); `REC-ME-SW0` 16. The box at the creation (`0x80039F30`) is computed at the position of the record for
every row: sprite 27 has_box 1, offset (-18, -12, 0) px, size 36 x 24 x 31; `0x8A`/`0x8B` write only `+0x114`, `+0x118`, `+0x11C` afterwards (no ground raise, no new box): `REC-H1-8A` shows the box at (-36, -44, +12) during S in loop 2
(the position of the record, not the written one), corrected by the physics at S+1; `REC-ME-8A` is corrected from S (the physics of the map-event birth runs at S). `rec_tick.json` rows carry `creation`, `creation_end` (the box and
`animflags_B4` = 193 for the sprite 27) and `ticks[]` (`k`, `call_no`, `cnt1c`, `end_AC`, `hbx - px`, `bw bd bh`).

## (1) Starting count

HEAD `0ab44d0` (branch `chantier/e20-heros`), `Alundra.Tests` in Release with the copy of the DLL neutralised: **5 065 tests, 0 failure, 5 m 38 s** (`BASE`). `alundra-project/Alundra.dll` md5 `03eee1b65dd479504a6d7ce2fac8f853`
unchanged by every build of this T0 (not locked).

## (2) Remeasure at the exact placement of the slice

Throwaway, never committed (`remeasure/patch.py`, applied by script to the working tree, suite run, restored with `git checkout --`, `git status` empty on the touched files; the diffs applied are `remeasure/*.patch.diff`,
the failing lists with their complete messages `remeasure/*.failed.txt`, the driver `remeasure/run_variant.sh`). Release, copy neutralised, `--blame-hang-timeout 300s`, 5 065 tests each.

- **PA** - phase A only: `SwitchedAtCreation = true` and `AnimFlags` loaded in `SpawnEntityByRecordId`, after `ScriptHost` and before `InitializeBalance`. **3 red**, 5 062 green:
  - `AlundraBirthBoxTests.B6_V_AnEntityBornFromARecord_HasNoBoxAtItsCreation_AndTheHostIsNeverAsked_AndAHostWithoutResourcesIsAskedAtMostOnce` - expected `(False, False, False, null, 0, True, False)`, got `(True, True, True, 0, 1, True, False)`: the record entity now has a box, the flag, the cursor 0, and the host is asked once (`ResolveAnimationData`).
  - `AlundraCreationSwitchClockTests.A6_V_AnEntityBornFromARecord_KeepsItsFirstClockTickUncounted_KnownDeviationQE0A5_1` - `Assert.False(born.SwitchedAtCreation)` gets True (the cursors `[0, 1, 2]` are not reached).
  - `AlundraHeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite(id: "s115_rec19")` - `(756, 1077, 321, 3145727)` instead of `(757, 1078, 321, 3145727)`: one image earlier.
- **PAB** - PA plus the marker `JoinsBirthTick` posed LAST by the three opcodes (D-E0A4-3: `0x2D` right after the non-null return, `0x8A` after its push, `0x8B` after its push when the search finds a target and after the search when not). **7 red**, 5 058 green:
  - the three of PA, with `s115_rec19` now `(755, 1076, 321, 3145727)` (two images earlier: the participation and the counted clock);
  - `AlundraInoaDayOneArcTests.A10_TheFirstVisitOnMap165_TheActorsPassTheSceneOnByFlags_ThePlayerGetsTheHandBack` - the list of image numbers `[596, 597, ...]` instead of `[597, 598, ...]`;
  - `AlundraShipBlockArcTests.A6_TheCameraBlockLandsAndWalksSouth_ThenTheWarpToMap416Departs` - `[180, 643, 1332]` instead of `[181, 644, 1333]`;
  - `AlundraHeightTargetSitesTests...(id: "s36c4_rec3")` - `(165, 325, 160, 11534335)` instead of `(166, 326, 160, 11534335)`;
  - `AlundraBirthTickTests.TheCreation_NeverReadsTheCollisionFieldOfTheHostByTheFallback_TheBirthTickDoes_AndAnEntityFromARecordNever` - "4 out of 23" entities with `EntityRefId >= 0` carry `JoinsBirthTick` (the four born by script; the pin of e20-e0a3b at line 422).
- **PANOAF** - PA without the `AnimFlags` load. **The same 3 red** as PA (identical messages): the load of `AnimFlags` moves no existing test. No stop.
- **PABEARLY** - PA plus the marker posed BEFORE the push of `0x8A` and `0x8B` (and right after the spawn for `0x2D`). **The same 7 red** as PAB (identical messages): no existing test sees the position written by `0x8A`/`0x8B`
  between the two placements; the difference is only killed by the new case (b4) of T2.

The four tests of the gull (`GullEntitySix_*`), every pin of the map load and every test of the hero are green in all four variants (they create by `CreateEntityFromRecord` directly or by the load, which the slice does not touch).
No stop of the plan is reached: 7 moved tests (limit 10), none outside the lists, none of the gull or of the map load or of the hero.

## (3) For each moved test: the pass and opcode of the birth, the record, the mechanism

A throwaway reflection probe (never committed) ran the four arc tests with `AlundraWorldProxy.CreationFaultHookForTests` and read the call stack of each `kind == "record"` creation: **every birth of the four existing tests is
`0x2D` (`ActivateEntity`) in the map-event pass (`RunMapEventsPass`)**; none is a loop-2 birth.

| Test | Birth (pass, opcode, record) | Phase of the move | Mechanism |
|---|---|---|---|
| `A10_TheFirstVisitOnMap165...` | map-event pass, `0x2D`; records 0 (sprite 282), 1 (280), 2 (278), 3 (279), 5 (25) of map 165 | B | participation: the actors run in the tick S of their birth instead of S+1; every image number of the list shifts by one |
| `A6_TheCameraBlockLandsAndWalksSouth...` | map-event pass, `0x2D`; record 0 (sprite 25) of map 391 | B | participation (the block after the `0x2D` at 265 reads one image earlier) |
| `...(id: "s36c4_rec3")` | map-event pass, `0x2D`; record 3 (sprite 313, creation animation byte `0x40`) of map 36, then records 13 to 26 (sprite 251) | B | participation (the platform acts at S) |
| `...(id: "s115_rec19")` | map-event pass, `0x2D`; record 19 (sprite 25) and 22 (sprite 426) of map 115 | A, then B | A: counted first clock tick (one image earlier: 756/1077); B: participation as well (755/1076) |
| `B6_V_...` | `SpawnEntityByRecordId` from the test on the first record of map 14 (the pin of the known deviation of e0a5) | A | `SwitchedAtCreation` raises the box rule at the creation: the box exists, the flag is set, the host is asked once |
| `A6_V_...` | same, in the programs pass | A | same flag: `Assert.False(SwitchedAtCreation)` fails; the probe of the discovery showed the cursors stay `[0, 1, 2]` because the first program of that entity (sprite 335) switches the animation (D-E0A5-2) |
| `AlundraBirthTickTests...:422` | the four script births among the 23 entities of the map | B | the marker is now posed on the entities born by script; the pin is rewritten (script births carry it, map load does not) |

`AnimFlags`: no existing test reads it for a script birth (PANOAF).

## (4) Predicted lists per phase (written before the code)

- **Phase A (T1)** - exactly: `B6_V`, `A6_V` (both rewritten as D-E0A4-6 says: the pin of the deviation goes on an entity of the map load), `AlundraHeightTargetSitesTests...(s115_rec19)` rewritten to `(756, 1077, 321, 3145727)` by derivation of the rule
  (counted first clock tick). Nothing else may move.
- **Phase B (T2)** - on top of A: `A10`, `A6` (ship), `...(s36c4_rec3)` to `(165, 325, ...)`, `...(s115_rec19)` to `(755, 1076, ...)`, `AlundraBirthTickTests...:422` (the pin of the marker). The other tests stay green. A10 and A6 are
  rewritten by derivation of the rule (every image number of the list shifts by one), not by a value of the oracle: the image numbers of the arcs are not binary (only the number of calls, 321 and 160, is).

## (5) Subjects of the new tests

- **Record whose creation animation has non-zero `AnimFlags`** (a4): the real record 3 of map 36 (`Nirude's Lair-36`, sprite 313, animation 0 byte `Acceleration` = 64 = `0x40`), born by `0x2D` in the real site `s36c4_rec3`;
  and the sprite 27 test record (below), whose oracle byte is `animflags_B4` = 193 (`creation_end` of `REC-H1` in `rec_tick.json`). The HEAD gives 0 for both.
- **Test record of the sprite 27** (a1, a2, a3, b1, b2, b3): the record is a copy of a record of the map 14 world (`arc.Entities`) with its `PrefabAssetId` set to the prefab of the global sprite 27
  (`SpriteRecordCatalog.TryGetPrefab(false, 27, ...)`) and a new index in the record table of the proxy; its box and its clock are read by key from `oracle/out/rec_tick.json` (`creation_end`: `hbx - px`, `hby - py`, `hbz - pz`, `bw`, `bd`, `bh`;
  `ticks[]`: `call_no`, `cnt1c`, `end_AC`) and `rec_birth_clock.json` (`REC-H1-SW*` rows).
- **Record whose creation animation has a non-zero speed** (b6, phase B): a throwaway probe that spawned the first 60 records of every map world of the export found, among others, `Arena Zorgia (Boss)-321` record 1 (sprite 469, animation 0 speed 160, byte `0x40`),
  `Murgg Caves-382` record 39 (sprite 351, speed 224), `The Klark - inner-388` records 2 to 12 (sprite 510, speed 256) and `Meia Dream (flashback)-309` records 6 to 8 (sprite 12, speed 512). The subject of (b6) is the record 1 of map 321, born
  by `SpawnEntityByRecordId` in the arc world of its map.
- **A `0x8A` site and a `0x8B` site where the written position is under the terrain** (b4, b4b): no real site is used. The real `0x8A` sites that the existing tests run (map 476 `@63` and `@553`, map 179 `@206`) are not under the terrain (the discovery
  of 2026-10-16 measured the ground raise inert on the two of map 476), and no census of the 684 `0x8A` sites of 69 maps was run in T0 to look for one. The subjects are **test programs**: an `EventProgramDocument` (as
  `AlundraEventProgramRunnerTests` builds them) run by the real `AlundraEventProgramRunner` in the arc world, `0x8A [record, X, Y, Z]` with a Z under the height of the cell at (X, Y), and `0x8B [search, record, dx, dy, dz]` with
  an offset that puts the new entity under the terrain, with and without a target found.
- **The shape of a birth by map event and by loop 2 in the montage**: the arc world of map 14 (`ArcRun`, real prefabs, `RealController`), the hook on `AlundraWorldProxy.TickTraceForTests`: a call of
  `SpawnEntityByRecordId(arc.Hero, record)` at the pass `"mapEvents"` (just before `RunMapEventsPass`: the entity is born before the entity block of the tick, as a map-event program does) for the map-event birth, and at the pass
  `"programs"` (inside the entity block, after the entities of the tick are collected: the entity joins the next tick) for the loop-2 birth. T2 replaces these calls by the real opcodes in test programs run by the runner.

## Tools of the discovery that are not copied

`remeasure/` holds the patch script, the driver and the results of this T0 only. The earlier P-B variants V1 to V3 of the discovery of 2026-10-16 are summarised in the plan (section 1.16.4, "Mesure P-B refaite").
