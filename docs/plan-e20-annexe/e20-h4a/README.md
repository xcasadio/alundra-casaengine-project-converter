# E20.h4a T0 — results (annex, no code)

Run at the starting HEAD `372d7e1e` (branch `chantier/theme2-attaques-objets`). Plan: `docs/plan-e20-heros.md` section 1.31.1 (revision 2, READY) and
section 1.31 (facts 1 to 8). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked at the path the oracle scripts read).
Nothing in the DLL, the engine, the converter or the data was touched. Line numbers of the plan date from 2026-10-18; the symbols are re-read at the HEAD
in section 5.

## 0. Verdict

No stop of the plan is reached: (a) the three oracle scripts give the filed outputs byte for byte (modulo the CR of the filed CRLF files); (b) the keys
of the tests are written down below; (c) the predicted list is still empty at the HEAD, with two existing tests flagged as sentinels for T1; (d) the
exported animations 50, 51 and 52 equal the oracle in every direction (32 ticks then `0x33`; loop; 26 ticks then 0). One reading to note for T1:
`DecreaseMp` does not exist yet in the DLL (section 2).

## 1. (a) The oracle scripts, replayed from a copy

The scripts were copied from `docs/plan-e20-annexe/e20-h4/work/oracle/` to a scratch directory and run there (they import each other and read the binary
and `data-extracted/data/map_alundra.json` at absolute paths). Python 3.14.2.

| Command (in the copy) | Result | Filed output | Compared |
|---|---|---|---|
| `python -B h4_start.py 5000 20261017` (the filed seed, `e20-h4/work/results/vec_start.txt:1`) | 5 000 vectors, 0 mismatch, 104 unmodelled, 773 casts started (other weapon), 164 (staff), 3 959 refused; instructions 142/142; exit 0 | `rerun/vec_start.txt` | identical to `e20-h4/work/results/vec_start.txt` |
| `python -B staff_check.py` | the four lines of section 2, exit 0 | `rerun/staff_check.txt` | identical to `e20-h4/work/results/staff_check.txt` |
| `python -B trace_cast.py 0x2B` | the eight lines of section 3 | `rerun/trace_cast_2B.txt` | identical to the first 9 lines of `e20-h4/work/results/trace_summaries.txt` |

(The filed files have CRLF line ends; the copies in `rerun/` are LF. The comparison was made with the CR stripped.)

## 2. (b) The keys of the tests

**The four cases of the staff** (`rerun/staff_check.txt`; hero MP max 4; the staff = item 7 owned and the weapon of slot 6 equipped, `Equip(6)`):

| Case | Weapon id | MP before -> after | Cast started | Hero animation | Sounds |
|---|---|---|---|---|---|
| staff, MP 0 | 7 | 0 -> 0 | yes | `0x32` | none |
| sword, MP 0 | 1 | 0 -> 0 | no | `0x00` | `[3]` |
| sword, MP 2 | 1 | 2 -> 1 | yes | `0x32` | none |
| staff, MP 2 | 7 | 2 -> 2 | yes | `0x32` | none |

**The chronology of the start of the spell** (`rerun/trace_cast_2B.txt`, item `0x2B`, MP 4): MP 4 -> 3; tick 1 animation `0x32`, item in use `0x2B`,
phase 0, counter 0 (the driver is first called at the tick after the start, counter 0); tick 33 animation `0x33`, counter 32 (so `0x33` at T0 + 32);
tick 102 animation `0x34`; tick 128 animation 0; tick 129 item freed. Only the first three rows are the keys of (o3); the rest belongs to h4c1.

**The eight traces** `docs/plan-e20-annexe/e20-d/oracle/out-swim/w9/W9-item2B` to `W9-item32` (`rerun/w9_traces.txt`): the same shape in the eight, 60 ticks,
the hero floating in deep water (`0x1D` target and current animation on every row), the Circle pressed at tick 10, the only sound row is `snd3` at tick 10, no
effect, no spawn, no movement. They differ only in the object equipped (the SHA-1 of each file is in `rerun/w9_traces.txt`). At the HEAD no test replays them
(`HeroSwimTests` replays the four holes `W9-item21`, `22`, `28`, `2A`); the T0 of h0a classed the eight "waiting for the slice of their object" (`e20-h0a/results.md`
section 2).

## 3. (c) The predicted list, recounted at the HEAD

Plan expectation: empty. Recount (read of the tests, no code written):

- No existing test puts the hero in the animation `0x32`, `0x33` or `0x34`, and none replays a trace of a tome (`rg` in `Alundra.Tests`: the matches of `0x32`
  to `0x34` are life/HP ceilings, opcodes, cells, text and the lists below).
- `HeroSwimTablesTests` pins the swim table (`0x32` to `0x34` in the Float group of the executable's table): the slice does not change `SwimTable`.
- `HeroItemUseTests.A3_EveryItemOfTheTableThatIsNotAHole_ReachesItsStart_NoSound` (`0x2B` to `0x32`) goes through `ItemStartForTests`, a seam; the slice keeps
  the tome entries of the production table as stubs, so it does not move. `A3_..._TheSameFrontWithAnItemOfTheTableStarts` and the `0x33` refusal likewise.
- `InventoryDirectorTests.Trigger_Refused_WhileAnItemIsInUse_...` sets `ItemInUse = 0x2B` on a bare proxy: no hero chain, it does not move.
- `HeroItemUseTests.A6_AStateThatIsNotInTheTableOfTheStatesWithoutHandler_DoesNotCancelTheItem_...` has the state `0x32u` in its theory data (on the ground and in
  deep water, with the bomb in use). **Sentinel for T1**: once `0x32` has its handler the test must still see no cancellation and the bomb still in use
  (the handler of `0x32` calls `TryUseItem` and leaves the item alone; with the bomb in use the "a tome in progress" test is false and the state goes to Idle,
  `hero_states_32_34.txt:1-9`). The plan's list stays empty; if this case moves at T1 it is a stop of the plan, as the plan says for any existing test.
- `HeroItemUseTests.A7_*` enumerates the 51 states with an item call and the 17 without: `0x32` to `0x34` are in neither list (the table has 63 + 17 = 80 states; the
  three tome states are the "NOT in DLL; slice h4a" rows of `e20-h0a/results.md` section 1). The slice adds the `TryUseItem` call of these three states but no test of the list
  enumerates them. Also a sentinel: if the 51 or the 17 are made to include them by the slice, the A7 data must grow with the slice (an expected change of a
  fixed list, to be named in the report, not an unexplained move).

Count: **0 tests predicted to move; 2 sentinels** (`A6` for `0x32u`, `A7` lists).

## 4. (d) The hero animations 50, 51 and 52, the export against the oracle

`probes/anims_50_52.py` (output `probes/anims_50_52.out`, exit 0, 0 difference): the hero record of the oracle (`data-extracted/data/map_alundra.json`,
`SpriteRecords` with `Sector5Id` 0; the binary's reading of an animation: `Delay & 0x7F` ticks per frame, a final frame of delay 0 = chain to its
`TransformIndexLow`, delay 1 = loop) against the export (`alundra-project/Entities/Alundra/bankalundra_0_anim{50,51,52}_{down,left,right,up}.anim2d`: last keyframe time / 0.02 s,
and `alundra-project/Data/sprite-records.json`, the hero record `192c2eeb-bdda-5bb4-92d6-39a4ae298307`, `IdsvAnimDirs` and `AnimSets`).

| Animation (state) | Oracle | Export (DLL), the four directions | Animation set |
|---|---|---|---|
| 50 (`0x32`) | 32 ticks, then chain to 51 (`0x33`) | 32 ticks (`Once`), `End` = `Chain`, `ChainTo` = 51 | Speed 0, acceleration 64, sound 0, flags 0 |
| 51 (`0x33`) | 36 ticks, loop | 36 ticks (`Loop`), `End` = `Loop` | Speed 0, acceleration 64, sound 0, flags 0 |
| 52 (`0x34`) | 26 ticks, then chain to 0 | 26 ticks (`Once`), `End` = `Chain`, `ChainTo` = 0 | Speed 0, acceleration 64, sound 0, flags 0 |

The same lengths as the emulated game of `trace_cast.py` (tick 1 to tick 33: 32 ticks of `0x32`; tick 102 to tick 128: 26 ticks of `0x34`). Values for the bench of
T1 (`Alundra.Tests/Support/JumpNpcRig.cs`, `Table`: `new(id, speed, acceleration byte, sfx, chain, ticks)`): `0x32` = (0, 64, 0, 0x33, 32), `0x33` = (0, 64, 0, -1, 36),
`0x34` = (0, 64, 0, 0, 26). Note: `IdsvAnimDirs[].Frames` holds the per-frame counts of the export (7, 9 and 5 frames), not tick lengths; the length is the last key time.

## 5. Symbols of the plan re-read at the HEAD (no stop: the plan's line numbers are of 2026-10-18)

| Plan | At the HEAD (`Alundra/Hero/PlayerManager.cs` unless stated) |
|---|---|
| `SetPlayerMp` `:1970` | `SetPlayerMp(GameState, short)` (clamped to [0, MpMax], `0x8004DEBC`), line ~2405 |
| `DecreaseMp` (périmètre, t2) | **does not exist at the HEAD** (`rg DecreaseMp|8004DF3C` finds only the plan): it is a function h4a writes (`0x8004DF3C(1)`: MP - 1 through `SetPlayerMp`). Not a stop: the plan lists it in the perimeter. |
| `SetItemIdFromCurrentItemId` `:2210-2231` | `SetItemIdFromCurrentItemId(GameState, ItemTables)`, line ~2652 (promotes the Circle's item); `StartItem` (line ~1092) calls it first |
| the start `0x8003499C` | `StartItem` (line ~1092) with the table of the starts: `ItemStartForTests` -> `BombAi.Start` for `0x23` -> `ItemStartStub` for the rest (a named stub answering 1) |
| the driver `0x8003634C` `:1474` | `UpdateItemUseDriver` (line ~1901), `ItemDriverForTests` / `BombAi.Drive` / `ItemDriverStub` |
| the cancellation `0x800350C0` | `CancelItemMode` (`internal`, line ~836; E20.h3b), `ItemCancelForTests` / `BombAi.Cancel` / `ItemCancelStub` |
| the states `0x32`-`0x34` `:620-621`, `:178-186` | in the swim table (`SwimTable`, `BuildSwimTable`, lines ~183-201): Float group, so no cancellation; no `case` in the state chain of `MovePlayer` |
| walkability of the hero (E20.i2a) | `RefreshHeroWalkabilityMask` (line ~2264), called from the hero tick (`WorldProxy.cs` ~2272 and `PlayerManager` ~2253) before the move: the pseudo-bit of the deep water; relevant to the slope guard of h4a (`Slope_18c` 4) |
| `Equip(6)` `:2077` | the test helper `Equip(Rig, int)` of `HeroItemUseTests` (line ~87) and the montage of `RunMontage` (`E20.h0a`: the boots `0x1B` and the object owned and equipped) |

## 6. Files

- `probes/anims_50_52.py`, `probes/anims_50_52.out`: section 4.
- `rerun/vec_start.txt`, `rerun/staff_check.txt`, `rerun/trace_cast_2B.txt`: section 1 (LF copies of the replayed outputs).
- `rerun/w9_traces.txt`: section 2, the eight traces.
