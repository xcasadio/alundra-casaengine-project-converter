# E20.h4c1 T0 - results (annex, no code)

Run at the starting HEAD `1c750d30` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.31.3 (revision 2, READY) and section 1.31 (facts 1 to 8, rules R-EL-1 to R-EL-6, R-EL-6 revised
after E20.c2b). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read by the scripts at
`D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`. Python 3.14.2, capstone 5.0.7. Nothing under `Alundra/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/`, `data-extracted/`
was changed; the throwaway probe test was added to `Alundra.Tests/Probes/` for the runs and removed (the source is filed as `probes/H4c1T0ProbeTests.cs.txt`, never committed as code). The line numbers of the plan date from 2026-10-18; the symbols were re-read at the HEAD.

## 0. Verdict: STOP at the probe P2 (contradicted); P1, P3 to P9 hold; (a), (b), (d) done

The plan says a contradicted probe is a stop. **P2 as predicted (`predictions.md`, written before the run: the row `AnimationDirection` of each rock equals the key's at the birth tick) is contradicted**: the DLL holds the row 0 at the birth tick of
every rock (and the previous value `~0` in `CurrentAnimationId`), and takes the key's row **one tick later** when read at the end of the frame (`probes/cmp_endframe.out`), **two ticks later** when read at the `inventory` pass of the tick
(`probes/cmp_probe.out`, the pass the keys' tick corresponds to for everything else). From then on the row equals the key's at every tick of every life of the six keys (P2b holds: no difference from the third tick of the life). The behaviour that
depends on the row (the box of P3, the sound, `+0xAC`, the death) matches the key. Cause read in the code (not a fix): the DLL validates the switch of animation at the end of the frame (`Alundra/Maps/Loop/FrameSyncPasses.cs`, `TryResolveAnimationTarget`,
`proxy.AnimationDirection = newAnimationDirection`), the binary's `UpdateAnimation` (`0x80038AB4`) runs in the tick of the birth. Consequence for T1 (to decide, not done here): (w1) "sans tolerance" cannot compare `AnimationDirection` and
`CurrentAnimationId` at the birth tick (and, sampled mid-frame, at `S3`); either the test compares them from the tick after the birth (end of frame), or the plan words P2 as "from the tick after the birth". The 13 other fields of the rocks, the item, the phase, the
counter, the hero's animation and the sounds are equal to the keys at every tick (section 3).

No other stop of the plan is reached: no octet of the table differs from the executable (`probes/check_table.out`), no need to touch the engine, no name refused (no file added to the DLL), f4, f5, f6a and h4a are done (sections 3 and 4),
`(E, 3)` is not registered and `Register` is not cumulative at the starting HEAD (section 4).

## 1. (a) The replays, from copies

All scripts read the binary at an absolute path and write next to themselves: they were copied to the scratchpad and run there (`.../scratchpad/h4c1-t0/`). Commands run in the copy of `disc/fcopy/` (plus the copies of `e20-h4/work/oracle/` for the vectors):

| Command | Result |
|---|---|
| `python -B c1_make_keys.py OUT1` and `... OUT2` (65 s each) | the 18 keys of the manifest; `manifest.txt` of the two runs identical (`replay/manifest_run1.txt`, `manifest_run2.txt`, logs `replay/c1_make_keys_run*.log`) and identical to the filed `disc/keys/manifest.txt`; `diff -rq OUT1 OUT2` empty; each of the 18 JSON `cmp` identical to the filed ones |
| `python -B c1_wall.py` twice | `earth1_wall_col9.json` identical across the two runs and to the filed one (`replay/wall_col9_md5.txt`: `bfd668d57665204c8be8e396a1829f4e`) |
| `python -B crit_relief.py` + `python -B crit_pointblank.py` twice, in a copy of `critic/work/fcopy/` | the five relief keys (`crit_flat`, `crit_pointblank_wall_col5`, `crit_slope_gp2_cols7_9`, `crit_step16_cols7_9`, `crit_step32_cols7_9`) identical across the runs and `cmp` identical to `critic/work/fcopy/_crit_out/` (`replay/crit_relief_keys_md5.txt`, logs `replay/relief1.log`, `pb1.log`) |
| `python -B h4_vec_drivers.py` (default 2000 vectors, seed 20261017) | `vectors 2000 seed 20261017 mismatches 0` (`replay/h4_vec_drivers.log`; the filed `e20-h4d2` replay log ran 1500) |
| `python -B h4_vec_handlers.py` (default 2000 vectors) | `vectors 2000 seed 20261017 mismatches 0` (`replay/h4_vec_handlers.log`) |

Total: the 19 keys of the plan and the 5 relief keys are reproduced byte for byte, twice.

## 2. (b) The vectors

- `disc/h4copy/vec/vec_driver_2B.json` (689) and `vec_e3.json` (224): regenerated twice by `c1_vec.py` (copy in `disc/h4copy/oracle/`), the two runs `cmp` identical to each other and to the filed files (coverage printed: driver 104/108 words, guard 19/19, E3 25/25).
- **Completed with the base directions 4, 12, 20 and 28** (the filed set holds 0, 5, 8, 16, 24, 31): `vec/c1_vec_dirs.py` (a copy of the driver part of `c1_vec.py`) writes `vec/vec_driver_2B_dirs.json`, **384 vectors** = 4 directions x 16 ticks of the table (20, 25 .. 95) x 3 hero places x the birth answered or refused, phase 0, animation `0x33`, slope 0. Two runs `cmp` identical. Independent check in the script: for every vector the directions of the spawns equal `(base + delta) & 31` for the entries of the tick, the table being read from the executable (`directions differing ... : 0`). The refused birth writes nothing (`force_z_written` empty), as in the filed vectors. `vec_e3.json` does not read a direction: unchanged.

## 3. (c) The probes P1 to P9 of the DLL

Predictions: `predictions.md` (written and kept before the first run of the probe). Bench and throwaway ports as described there; the table of the driver is checked against the executable (`probes/check_table.out`: 32 entries and the sentinel, 0 difference).
Command: `H4C1_OUT=<dir> dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=<skipcopy.targets> --filter FullyQualifiedName~H4c1T0ProbeTests --blame-hang-timeout 60s` (6 scenarios, 6 passed); the rows are in `probes/out/`; the comparison is `probes/cmp_probe.py` (output `probes/cmp_probe.out`) and, sampled at the end of the frame, `probes/cmp_endframe.py` (output `probes/cmp_endframe.out`).
Scenarios: `earth1_flat_east`, `_south`, `_west`, `_north`, `_east_x200`, `earth1_deepwater_cols7_9` (192 lives).

| Probe | Result |
|---|---|
| P1 | **holds**: the step at the E3 tick is (0, 0) for all 178 lives that reach the status 3 (also the key's). f5 (D-F5-5) is therefore in the DLL. |
| P2 | **CONTRADICTED** (section 0): the row at the birth tick is 0 for the 32 rocks of each key; it is the key's (`table[0][col]`) from the third tick of the life (mid-frame sample) / the tick after the birth (end-of-frame sample), for the 112 lives of 192 whose row is not 0; the row-0 lives are unaffected. |
| P3 | **holds**: the box is 42 x 28 x 11 (minus 1 in 16.16) at every sampled tick of every life and 23 x 23 x 15 at `S3 + 10 .. S3 + 13` for the lives of row 0 (east/south/x200 16 lives each, west/north 8, deep water 6), nowhere else; the fields as the hit pass left them equal the fields after `EvaluateFrameBox()` (0 difference); the life ends at `S3 + 13`: the box of the last image of `Hold` is not an issue. |
| P4 | **holds**: 14 lives touch the deep water; `Slope_18c` 0 then 4 from `L` (the key's tick, also 4 at `L + 1` where the status is 4); the 18 dry lives keep 0; the status 4 is at `L + 1`; `+0xAC` and `TargetAnimationId` stay 0 (no E3 ran), no sound 46. The DLL needs no code of this tranche for the deep-water death of the type 16 (f6a). |
| P5 | **holds**: status 3 of the lives (birth, slot) (77, 12), (87, 3), (92, 14) at **94, 103 and 109**; the status 3 and the position at it equal the key's for all 192 lives (`S3 - birth` is 16, 17 or 20; the three edge rocks are 17, 16 and 17). |
| P6 | **holds**: the sounds played (tick, id) equal the key's list in the six keys (32 sounds 46 in each flat key, 18 in the deep-water key), one per life at its `S3`. |
| P7 | **holds**: `ForceResetAnimationFlag` 0 up to `S3 + 11`, 1 from `S3 + 12` (= `L + 13`), status 4 at `S3 + 13` (= `L + 14`) and no row after it, for all 178 lives. |
| P8 | **holds**: at the birth tick of every life (first-use slots 1 to 14 included) the rock is at the status 2 with one step taken (`X` = hero `X` + `ForceX`; `Z` = key `pz` - 1; `ForceZ` = the key's; counter 0), and at the next tick the Z has moved as in the key. |
| P9 | **holds**: the sequence of the slots of the 32 births equals the key's in the six keys (east: 1 2 3 4 5 6 7 8 9 10 11 12 1 2 3 4 5 6 7 8 9 10 11 12 1 2 3 4 13 14 5 6); the slots 1 and 2 are at the status 4 at the tick 51 and born again at the tick 52. |

Supplementary (information, carried to T1): the whole comparison, tick by tick, of the 5 908 rows: **no difference** in the status, `X`, `Y`, `Z - 1`, the three forces, the direction, `TargetAnimationId`, `+0xAC`, the flags, `ForceAdjusted`, `IsOnGround`, `Slope_18c`, `+0x1D0`, the evaluated box,
the sounds, the item in use, its phase, its counter and the hero's animation (140 ticks per key, the item freed at the tick 129, the hero's animation 50, 51 then 0 as the key). Only two fields differ: `AnimationDirection` (112 lives, the birth tick and the next one, P2) and
`CurrentAnimationId` (`0xFFFFFFFF` at the birth tick, 0 in the key: 192 lives; and, sampled mid-frame, 0 instead of 1 at `S3` for the 178 lives that reach it; sampled at the end of the frame this second difference disappears). The DLL's Z is the binary's minus 1, as the benches of f4 to f6 state.

## 4. (d) The predicted list, recounted at the starting HEAD, and the stop test

- **Stop test**: `(E, 3)` is not registered (the `DeactivateCode` of the registered classes are 2, 9, 10, 12, 17, 19, 20; none is 3) and `NativeSpriteRegistry.Register` is not cumulative (a second registration of a key throws `InvalidOperationException`, `NativeSpriteRegistry.cs`); `Alundra/Ai/Items/` holds only `BombAi.cs`; E14.b0 is not done. **No stop.**
- **The sums of keys of the production registry are FIVE** (not two), each a sum of `<Class>.Keys.Count` that phase B raises by `EarthTome1Ai.Keys.Count` (1): `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn`,
  `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (and its secondary `sum` registry, which must also register `EarthTome1Ai`, else `sum.Count == production.Count` goes red),
  `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (same, its `sum`), `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (the count in the tuple),
  `BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` (the helper `Sum()` and the explicit sum). In each, the list is derived (`+ EarthTome1Ai.Keys.Count`, `EarthTome1Ai.Register(sum)`), not re-typed.
- **Other candidates, to be checked by the suite at T2, not predicted here**: (1) the tests that tick a map holding a sprite 16 under the production registry (the four rocks of the map 388 are Q-H4C1-2's accepted change); (2) none of the `UnhandledWeaponSpriteCodes` tests names the type 16 (`WeaponSpawnSeamTests`, `BallProductionTests`, `WaveSwordLifeTests.V3` use other types), and `ProductionWiredWeaponTypes` does not hold 16, so the invariant of D-F3-4 for {16} is the new (p4). The tests of h4a (`Tome*`) install their seams and are not moved by the phase B.
  Predicted list for the phase B: the five tests above, and nothing else.
- **The "tables of h0a"** are three ternaries on the bomb at the HEAD, not tables: `PlayerManager.StartItem` (`ItemStartForTests ?? (item == BombItem ? BombAi.Start : ItemStartStub)`), `UpdateItemUseDriver` (`... BombAi.Drive : ItemDriverStub`), `CancelItemMode` (`... BombAi.Cancel : ItemCancelStub`); h4a's `TomeStart` and `TomeSlopeGuard` exist and are inert (reached by the seams only). D-H4C1-2's "three `0x2B` entries" are therefore three new branches of these ternaries (a wording gap of the plan, no stop; h4c2 and the others will have to add theirs to the same three sites).

## 5. Files

`predictions.md` (written before the probes); `key_facts.py`, `key_facts.txt` (the facts read from the keys, before the probes); `replay/`; `vec/`; `probes/` (`H4c1T0ProbeTests.cs.txt`, `check_table.py`/`.out`, `cmp_probe.py`/`.out`, `cmp_endframe.py`/`.out`, `out/` the rows); `SHA1SUMS`.
