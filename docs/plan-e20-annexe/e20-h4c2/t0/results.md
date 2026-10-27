# E20.h4c2 T0 - results (annex, no code)

Run at the starting HEAD `45a1b50a` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.31.4 (revision 3.2, READY), section 1.31 (facts 1 to 8, rules R-EL-1 to R-EL-6, R-EL-6 revised 2026-10-25) and
the reading of 2026-10-25 (`../rockfix/lecture/`). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked by `probes/check_table.py`), read by the scripts at
`D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`. Python 3.14.2. Nothing under `Alundra/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/`, `data-extracted/` was changed: the throwaway probe test was
added to `Alundra.Tests/Probes/` for the runs and removed (the source is filed as `probes/H4c2T0ProbeTests.cs.txt`, never committed as code); one line of `WorldProxy.cs` was changed for the experiment of section 6 and restored (`git diff` empty after).
The line numbers of the plan date from 2026-10-18; the symbols were re-read at the HEAD (section 8).

## 0. Verdict: STOP - the probe (s2) is contradicted, and the table (b) finds a byte without a member; (a0), (a), (a2), (d) done; (s1), (s3) to (s12) hold

The plan says a contradicted probe is a stop, and "an octet lu par un vecteur n'a pas de membre" is a stop. Nothing is worked around; the facts and the options are below, the decision is the main session's.

**STOP 1 - (s2) as predicted (`predictions.md`, written before the first run: the x and y of the rock at its birth tick equal the key's) is contradicted.** The z, the terrain word, the destruction by the comparison, the status, the slots, the ticks of birth and the two draws r1, r2 of all the births
equal the keys (section 5). **Only x and y differ, for 13 births out of 15 in each of `mid` and `edge`** (and 12 of 14, 12 of 14, 9 of 14 in `s9a`, `hb16`, `hb17`, 13 of 15 in `water`): the DLL puts the rock at `floor(spawn position to the pixel) + force of the tick`, the binary at
`spawn position + force of the tick` (the fraction of the pixel of the spawn position is lost). The 2 births per key whose spawn position is a whole number of pixels (the rock 11 of `mid`, the second rock of the tick 226) are exact. Example (`mid`, tick 18, rock 2): spawn x 37653504 (574.53125 px), force 113600; key 37767104 = 37653504 + 113600; DLL 37731264 = `(37653504 >> 16 << 16)` 37617664 + 113600. `probes/cmp_probe.out`, section "(s2) diagnostic": **the sample equals floor(spawn) + force for 15 of 15 births of `mid` and 14 of 14 not clamped of `edge`** (the rock 11 of `edge` is clamped to 10 px by the map edge, as (s3) predicts).
The loss is permanent: the difference is constant over the whole life of the rock (1053 rows of x and of y differ in each of `mid`, `edge`, `water`, no other field).
The cause, read in the code (not a fix): `WorldProxy.SpawnEntityBySpriteIndex` ends its creation with `spawnedProxy?.PushLogicalPositionToRoot();` (`Alundra/Maps/WorldProxy.cs:3629`) **without `keepSubPixel`**, so the root of the entity is placed at `ResolveLogicalPosition` (`posX >> 16`, whole pixels,
`EntitySpawnFactory.cs:566-573`); the first kinematic step of the birth tick moves that root by the force and pulls the logical position back from it. An existing test states the same rule as intended behaviour: `LiftableBreakWorldTests.B5` (`Alundra.Tests/Ai/Objects/LiftableBreakWorldTests.cs:271`, "the interface of the apparition takes whole pixels: 143.5 becomes 143").
The rocks of h4c1 never showed it: they are born at the hero's position, a whole number of pixels. The plan's test (w1) (`cast_2c_pf_mid_x600y480`, "sans tolerance", position included) and (w2), (w3b), (w4), (w8) cannot pass as the code is.
Options (the fork is the main session's; the file is outside the perimeter of 1.31.4, which lists `WorldProxy.cs` for the kick only): (A) keep the sub-pixel of the spawn position in the interface of the apparition (`PushLogicalPositionToRoot(keepSubPixel: true)` at that one call, or the same in the driver of the slice); section 6 measures it:
**the whole suite stays green but 3 test rows (`LiftableBreakWorldTests.B5`, 1 test x 3 ticks per frame), whose expectation is the whole-pixel rule**, and the probe with the sub-pixel push reproduces `mid` and `edge` with zero difference over 330 ticks; (B) leave the interface and give the tests of h4c2 a position tolerance of one pixel
(the plan says "sans tolerance" for (w1)); (C) ask the author whether the binary's creation really keeps the fraction for the crate drop (the B5 rule) - the key of the rocks says it does for `SpawnWarpEntity` of `0x800354D0`.

**STOP 2 - (b): a byte read by the vectors has no member.** `[0x800DD870]`, the camera kick X, is read with a non-zero value (+3 or -3) by 22 of the 100 vectors of `vec_camera_8002CDA0.json` (ids in `mapping.md`) and added to the scroll X; the DLL has no X kick and D-H4C2-2 adds the Y kick only (the X register has no writer in the game).
The h4b1 precedent (vectors of a byte the DLL cannot carry classified "binary only" and skipped by their flag) is not written in 1.31.4. All the other bytes of E13, of the camera and of the sway have a member (`mapping.md`); the Y kick is the member D-H4C2-2 creates.

No other stop of the plan is reached: no clé diffère entre deux exécutions (section 2, 3), no need to touch the engine, no name refused (no file added to the DLL), f5, h0a, h4a, c2b done, the answers of the author are the defaults of the plan (the register `(E, 13)` is not registered, section 7).

## 1. (a0) The starting HEAD and the suite

`git rev-parse HEAD` = `45a1b50a` (parent), branch `chantier/theme2-attaques-objets`; build and suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets`), `--blame-hang-timeout 60s`, in four parts
(`scratchpad/part.sh`: A `Ai.Projectiles`, B the other `Ai`, `Arcs`, `Audio`, `Cameras`, `Cells`, `Combat`, `Dialogues`, C `Entities`, `Events`, `ExportGuards`, `Hero`, `Items`, D the rest): **332 + 2 011 + 3 340 + 2 036 = 7 719 tests, 0 failure**
(`suite/start.{A,B,C,D}.summary`, `.red` files empty). The traces `docs/hero-trace-389-*.txt` were rewritten with CR at the end of lines by the suites: `git diff --ignore-cr-at-eol --quiet -- docs/` true, restored by `git checkout`.

## 2. (a) The replays, from copies

All scripts read the binary at an absolute path and write next to themselves: copied to the scratchpad and run there (`scratchpad/h4c2-t0/wd`, `wc`, `wf`, `wv`). `replay/replay_keys.sh <out>` runs the key scripts; two runs, `diff -rq` empty (`replay/keys/run1_vs_run2.txt`).

| Keys | Command (from the copy of `disc/h4copy/oracle` + `disc/tools`, `critic/work/oracle`) | Result |
|---|---|---|
| `disc/keys/cast_2c_pf.json` | `cast_rich.py --pf --out` | identical |
| `cancel_2c_pf_t100`, `_t60` (`.json`, `.txt`) | `cancel_pf.py T --out` | identical (4 files) |
| `targets_2c_pf_inv0`, `_inv30` (`.json`, `.txt`) | `targets_pf.py 0x2C --slotinit --pf --inv N --out` (`targets_pf2.py` prints two more lines) | identical (4 files) |
| `driver_2c_cases.json` (609 cases) | `driver_keys.py OUT.json` | identical |
| `critic/keys/cast_2c_pf_mid_x600y480` (`.json`, `.txt`) | `cast_rich.py --pf --hero-x 600 --hero-y 480` | identical |
| `critic/keys/cast_2c_pf_plateau_hb16`, `_hb17` (`.json`, `.txt`) | `my_plateau.py --pf --plateau-hb N --plateau-col 7 --hero-x 100 --hero-y 100` | identical (4 files) |
| `critic/keys/cancel_2c_pf_t100_sounds.txt`, `_t60_sounds.txt` | `my_cancel_snd.py T` (stdout) | identical |
| the vectors of h4b | `c9_replay.py` repointed to `docs/plan-e20-annexe/e20-h4b/bin/vec` (`replay/vectors/c9_replay.py`): **770 vectors, 0 failure** (`vec_e13` 40, `vec_camera` 100, `vec_sway` 80); `c5_camera.py`: 3 000 trials, 0 mismatch, 184/185; `h4_vec_drivers.py 1500`: 0 mismatch; `h4_vec_handlers.py 1500`: 0 mismatch (E13 27/27) | identical on two runs (`replay/vectors/*.log`) |

`replay/keys/cmp_with_filed.txt` lists the 18 files compared with the filed ones (`cmp`): all identical.

## 3. (a2) The reading of 2026-10-25 and the "corrected" keys

Filed in `../rockfix/` (README, `proof.md`, `MANIFEST.md5`, `replay/`): the two folders of the reading as `lecture/h4-rev/` and `lecture/h4-rev-critic/` (without `__pycache__`), `fixpatch.py`, `cast_rich_fix.py`, `my_plateau_fix.py`, `cancel_fix.py`, the four keys `cast_2c_pf_rockfix.json`,
`cast_2c_pf_plateau_hb16_rockfix.json`, `cast_2c_pf_plateau_hb17_rockfix.json`, `cancel_2c_pf_t170_rockfix.json` and the sound journal `cancel_2c_pf_t170_rockfix_sounds.txt` (tick 170: the sound 23, then two sounds 45). Replayed twice from a copy: the manifests are identical (`replay/rockfix/MANIFEST_run1.md5` = `MANIFEST_run2.md5`),
the three casts equal `lecture/h4-rev/h4c2/keys_rockfix/MANIFEST.md5` and the T = 170 key and journal equal `lecture/h4-rev/h4c2/out/cancel_t170_FIX.json` / `.txt` byte for byte. **Proof (`rockfix/proof.md`)**: `cast_2c_pf_mid_x600y480` (`877bb812...`), the interruption T = 60 (`dc136586...`) and T = 100 (`f927708b...`) are
identical with and without the patch (and equal to the filed keys of the unmodified binary); the other rows differ as the plan says (the edge rock, the rock 8, T = 170 and 190). The README marks the four keys "indicative, not faithful to the byte".

## 4. (b) The table of the bytes read by the vectors

`mapping.md` (and `mapping_scan.py` / `mapping_scan.txt`, the distinct (address, size) pairs of the three vector files): E13 reads and writes `+0x6C` (`Flags`), `+0x88` (`TargetAnimationId`), `+0xAC` (`ForceResetAnimationFlag`) and writes the kick Y (the member of D-H4C2-2); the camera step reads the followed point, the first-call flag (`needsSnap`),
the scroll X and Y, the kick Y, the sway block (`CameraSway`) and **the kick X, which has no member (STOP 2)**.

## 5. (c) The probes (s1) to (s12)

Predictions: `predictions.md` (written and kept before the first run of the probe; the expected values come from the keys by `key_facts.py`, `key_facts.txt`, `expected.json`, none from the DLL). Bench and throwaway ports as described there.
Command: `H4C2_OUT=<dir> dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=<skipcopy.targets> --filter FullyQualifiedName~H4c2T0ProbeTests --blame-hang-timeout 60s` (15 tests, 15 passed; the rows are in `probes/out/`, two runs identical: `diff -rq` empty); the comparison is
`probes/cmp_probe.py` (output `probes/cmp_probe.out`). The table of the driver and the 32-direction tables are checked against the executable (`probes/check_table.out`: the 15 entries and the sentinel, X32 and Y32).

| Probe | Result |
|---|---|
| (s1) | **holds**: after each of the 330 frames of `mid` and `edge` (240 of `s9a`, `hb16`, `hb17`) the state of the generator equals `rng0(t+1)` of the key and the number of steps in the frame equals `draws + 1` (1, 3 at the births, 5 at the tick 226). |
| (s2) | **CONTRADICTED** (section 0, STOP 1): x and y at the birth tick; every other item of the prediction holds (ticks, r1, r2, `PosZ` = key z - 1 = 16252929, status 2, slots, terrain word 0 / 16777216 / 17825792, destruction of exactly the rocks 9, 13, 14 in `hb17` and none in `hb16`). |
| (s3) | **holds**: the rock 11 of `edge`, requested at x = -20 px, is born without exception at the tick 162 at x = 655360 (10 px), y = the key's, `ForceAdjusted` = 1, status 2. |
| (s4) | **holds**: for the 15 rocks of `mid` at 1, 2 and 3 ticks per frame: E13 at birth + 33, `ForceResetAnimationFlag` at E13 + 46, status 4 at E13 + 47 (the key: (46, 47, 33, 32)). |
| (s5) | **holds**: the sounds 55 and 45 (tick, id) equal the key's list (30 sounds) in `mid` and in `edge` (with the sound 45 of the rock 11 at 195); no other sound id is played in these runs. |
| (s6) | **holds**: the 1 215 rows of the 15 rocks of `mid` (statuses 2, 3 and 4, animations 0, 1, 2) have `CurrentAttack` (8, 64) and the evaluated box 4718591 x 3145727 x 2555903 (72 x 48 x 39 px minus one raw unit). |
| (s7) | **holds**: `Slope_18c` = 0 in every row of `mid`, `edge` and `water` (rocks 9 and 13 fall on the deep water); in `water` the last tick of each rock equals the `fix` key's `st4` (98, 114, ..., 306, 306, 322). |
| (s8) | **holds**: in the run with six inert Beannoids as targets, 30 hits; at every one of the 330 ticks, steps - 1 - 2 x births = 9 x hits (the key: 9 draws per hit, drawn the tick BEFORE the hit is recorded). |
| (s9a) | **holds**: without E13, the rock 11 (born 162): `IsOnGround` = 0 at the 32 ticks 162 to 193 and 1 at 194; the rock 8 (born 114) in `hb16` and in `hb17`: 0 at the 32 ticks 114 to 145 and 1 at 146. (Information: `ForceAdjusted` held from 162 / 142; with no E13 the rock stays in the status 3 after its landing.) |
| (s10a) | **holds** (values read in the keys, `cmp_probe.out`): rock 8 steps 102240 / 45440 (ticks 138 to 140), 53472 / 23764 at 141, 0 from 142, ground at 146 at x 10289152, y 4719956 (`hb16fix` = `hb17fix`); rock 11 x 655360 constant, dy 80320, ground at 194 at y 9204160. **Bounds: 7 x 102240 = 715680 raw units (10.92 px) on each axis for the rock 8, 0 on each axis for the rock 11**, as the plan. |
| (s11) | **holds**: with the animation 0x31 posed before the tick 100: end of tick 99 hero animation 0x33 and item 44; end of tick 100 hero animation 0x31 and item 0; the cancel port is called once, at tick 100; the sound 23 once, at tick 100. (Information: the hero animation 0x31 at 100 to 103, 0x1C at 104 to 114, 0 from 115, as the key.) |
| (s12) | **holds**: on the real map 14 the hero at (100, 100): `CombinedVramFlagsOR` = 0 (`& 0x180` = 0); in `mid`, `edge`, `water` and `cancel100` (to the tick 99) it stays 0 at every tick and the hero is never in 0x31, 0x3A or 0x1C. |

Supplementary (information, carried to T1; `cmp_probe.out`): **the rocks with the E13 port of D-H4C2-3 (mode Fix), tick by tick against the keys: 15 lives, status, target animation, z - 1, `+0xAC`, ground flag: no difference in `mid`, `edge`, `water`; x and y differ by the lost fraction only (STOP 1)**; with the probe-only sub-pixel push
(section 6) `mid_sub` and `edge_sub` have zero difference over the 330 ticks, and `probes/sub_rock8_positions.txt` gives the positions of the rocks 8 and 11 against the bounds of (s10a): rock 11 exact (0 on each axis, the bound 0 holds), rock 8 at the wall of `hb16` / `hb17`: dx 0, dy up to 248876 raw units (3.8 px) over its life,
inside the bound 715680; its status 3 and its E13 tick equal the key's (147) and the rock 11's (195). The registry check: `(E, 13)` is not registered for the types 16, 17, 469 (`probes/out/registry.txt`).

## 6. Experiment (not part of the plan): the sub-pixel at the spawn of the interface, one line, restored

`WorldProxy.cs:3629` changed to `spawnedProxy?.PushLogicalPositionToRoot(keepSubPixel: true);` (the one call of `SpawnEntityBySpriteIndex`), build, the four parts of the suite in Release: **7 734 tests (7 719 + the 15 probes), 3 failures**, all `LiftableBreakWorldTests.B5_TheContentsOfTheCrateAreDroppedAtItsBreak_WhereAContentOfZeroDropsNothing`
(`ticksPerFrame` 1, 2, 3; expected `(9371648, 6553600)`, actual `(9404416, 6553600)`: the object dropped by a crate at 143.5 px is 0.5 px further). Files: `spawn-subpixel/sub.{A,B,C,D}.summary`, `.red`. The change was restored by `git checkout -- Alundra/Maps/WorldProxy.cs`; `git diff -- Alundra Alundra.Tests` is empty.

## 7. (d) The predicted list, recounted at the starting HEAD

- **The sums of keys of the production registry are FIVE** (not two); each is a sum of `<Class>.Keys.Count` that phase B raises by `EarthTome2Ai.Keys.Count` (1; the registry holds 28 keys at the HEAD, `probes/out/registry.txt`, 29 after): `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (the explicit sum, `:60`),
  `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (and its secondary `sum` registry, `:83`, which must also register `EarthTome2Ai`, else `sum.Count == production.Count` goes red),
  `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (same, `:243`), `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (the explicit sum in the tuple, `:187`),
  `BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` (the helper `Sum()` and the explicit sum, `:234`, `:247`). In each, the list is derived (`+ EarthTome2Ai.Keys.Count`, `EarthTome2Ai.Register(sum)`), not re-typed.
- **Not predicted, to be checked by the suite at T2**: the tests that tick a map holding a sprite 17 or the E program 13 under the production registry (none is known; the sprite 213 / type 469 of the map 321 is not registered by the slice); the invariant tests of `WeaponSpawnSeamTests.S5` do not name 17 (`ProductionWiredWeaponTypes` does not hold it), so (p3) of the slice is new.
  The tests of h4a (`Tome*`) install their seams. **Predicted list for the phase B: the five tests above, and nothing else.**
- **The stop test**: `(E, 13)` is not registered for any type (`registry.txt`: `TryGet(E, 13, 16/17/469)` false; the codes registered in slot E are 2, 3, 9, 10, 12, 17, 19, 20) and `Register` is not cumulative (a second registration of a key throws).
- **"The three seams of h0a"** are, as h4c1 found, three ternaries in `PlayerManager` (`StartItem`, `UpdateItemUseDriver`, `CancelItemMode`), already carrying the `0x2B` branch; the `0x2C` branch is the one to add to each.

## 8. The line numbers of the plan, re-read by symbol at the HEAD

`_effectFrozenOfTick` `WorldProxy.cs:772` (the plan's `:767`), `EffectFrozenOfTickForTests` `:781` (`:776`), cleared at `:2529` (`:2519`), `RunEntityBlock` `:2915`, `TickPrograms` loop `:2951`, `_effectStage.RunFrame` `:2702`, `UpdateCameraFollow` call `:2726` (`:2716`), `CameraDirector` calls
`CameraMath.AdvanceCameraSmoothing` at `:268`, the sway sign `CameraMath.cs:223` (unchanged), `TerrainProbe.cs:36` (unchanged), `EnemyHelpers.Below` `:35` (unchanged). These recounts are not stops.

## 9. Files

`predictions.md` (written before the probes); `key_facts.py`, `key_facts.txt`, `expected.json`; `mapping.md`, `mapping_scan.py`, `mapping_scan.txt`; `replay/` (the scripts, the key manifests, the comparison with the filed keys, the vector logs, the rockfix manifests); `probes/` (`H4c2T0ProbeTests.cs.txt`, `check_table.py` / `.out`,
`cmp_probe.py` / `.out`, `sub_rock8_positions.txt`, `out/` the rows); `suite/` (the starting suite); `spawn-subpixel/` (section 6); `SHA1SUMS`; and `../rockfix/`.
