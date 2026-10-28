# E20.h4d1 T0 - results (annex, no code)

Run at the starting HEAD `a021b819` (branch `chantier/theme2-attaques-objets`, 2026-10-28). Plan: `docs/plan-e20-heros.md` section 1.31.5 (READY at the supplementary reading of 2026-10-22) and section 1.31 (facts 1 to 8, rules R-EL-1 to R-EL-7, R-EL-6 revised 2026-10-25). Binary
`ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read by the scripts at `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`. Python 3.14.2.
Nothing under `Alundra/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/`, `data-extracted/` was changed; the throwaway probe test was added to `Alundra.Tests/Probes/` for the runs and removed (the source is filed as `probes/H4d1T0ProbeTests.cs.txt`,
never committed as code). The line numbers of the plan date from 2026-10-18; the symbols were re-read at the HEAD.

## 0. Verdict: DONE, no stop of the plan reached; (a), (b), (c), (d), (e), (f) done; one clause of my own prediction was wrong (section 4, erratum), flagged

- (a): every replay is identical on two runs and identical to the filed files (section 2).
- (d) and (e): the life of the sprite 21 born by the interface, the generator, and the hit ticks of the targets equal the faithful key; all clauses of the predictions hold except the grain of the hit-spark TEST SEAM (section 4): the prediction (written before the run) said 3 calls per hit, the seam is called once per
  hit (`WorldProxy.EmitHitSparks` calls it before queuing the three sparks); the DLL itself draws the key's 9 numbers per hit (three sparks x three numbers) at each hit tick. This is an error of the prediction about the seam, not a difference between the DLL and the key; the plan's probe wording ("les écritures de coup aux ticks de la clé") holds.
  The main session decides whether it counts as the "sonde contredite" stop.
- (c): no word read by the vectors lacks a member; three members are created by the slice (T1) as D-H4D1-2 and D-H4D1-3 say (section 3).
- A finding for T1 (section 5): the discovery file `disc/keys/fire2_hurt60.json` differs from the faithful remake in 9 rows; the plan says "gardées" for hurt60 and the critic said "0 ligne différente": T1 must read `critic/keys_faithful/fire2_hurt60.json`.

## 1. (a0) The starting HEAD and the suite

`git rev-parse HEAD` = `a021b819`; build and suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets`), `--blame-hang-timeout 60s`, in seven parts (`suite/start.summary`):
**635 + 332 + 369 + 1 331 + 619 + 2 268 + 2 231 = 7 785 tests, 0 failure** (the total at the end of E20.h4c2). The traces `docs/hero-trace-389-*.txt` were rewritten with CR at the end of lines by the suites: `git diff --ignore-cr-at-eol --quiet -- docs/` true, restored by `git checkout`.

## 2. (a) The replays, from copies

All scripts read the binary at an absolute path and write next to themselves: copied to the scratchpad (`.../scratchpad/h4d1-t0/`) and run there with `python -B` (or `-I`).

| What | Command | Result |
|---|---|---|
| the ten faithful keys (`fire2_key_c.py --pf` with `FAITHFUL=1`: cast, preset block, preset limit 0, hurt 170, 60, 20, 20 zerobuf, 20 stale; `fire2_targets_c.py --pf` for the two target sets) | `t0/replay/replay_keys.sh <dir>` twice (25 s each) | `diff -rq run1 run2` empty; `replay/manifest_run1.sha1` = `manifest_run2.sha1`; each of the 20 files (`.json` and `.txt`) equal to `critic/keys_faithful/` (`sha1sum -c critic/SHA1SUMS_keys_faithful.txt`: 20 of 20 OK) |
| E28 vectors | `gen_e28_vectors.py` (default 200 + 2 sentinels, seed 20261018) twice | the two files `cmp` identical to each other and to `disc/keys/vec_e28_8007FB58.json` (202 vectors, model-vs-real mismatches 0) |
| E28 vectors plus the seed of the faithful key | `t0/vec/gen_e28_vectors_t0.py` (the same, a third sentinel `0xA468A517`, hero (100,100), Z raw 2) twice | `t0/vec/vec_e28_8007FB58_203.json` identical across the runs; the 203rd vector's 8 effect calls are the key's tick 154: X = 6553600, 3421033, 3294976, 3333067, 6553600, 7846483, 10161664, 7978432; Y = 9383424, 8641978, 6553600, 4406578, 5288960, 5691678, 6553600, 7503488; Z 2; `destroy_calls` `[ENT, 0xFFFFFFFF]`; mismatches 0 |
| the pilot vectors and the others of h4b | `t0/vec/c9_replay.py` (repointed to `docs/plan-e20-annexe/e20-h4b/bin/vec`) twice | **770 vectors, 0 failure** (`vec_fire2_80035C64.json` 60/0), logs identical (`vec/c9_run1.log` = `c9_run2.log`) |
| the cancel vector of the critic | `t0/vec/extra_replay.py` on `e20-h4b/critic/vec_extra/vec_extra_cancel_fire2.json` twice | 1 vector, 0 failure, logs identical |
| the critic's model of the pilot | `e20-h4b/critic/c6_fire2.py` twice | 4000 runs, 0 mismatch, coverage 144/146 (uncovered `0x80035C90`, `0x80035C94`: the same as the filed `e20-h4b/critic/run_summary.txt`), logs identical |
| the critic's models of the pilot and of E28 against the real code | `critic/c/crit_models.py 6000` twice | driver fuzz 6000 cases, 0 mismatch; E28 fuzz 0 mismatch; identical across the runs and to `critic/crit_models_run.txt` |

## 3. (b) The keys, (c) the table of the words

(b): `keys.md`: the four discovery keys replaced by the faithful ones, `fire2_cast_nopf` retired, the kept ones, the two added. (c): `mapping.md` (and `mapping_scan.py` / `mapping_scan.txt`): the distinct (address, size) pairs of the 60 pilot vectors, the cancel vector and the 203 E28 vectors, each with its member.
Members created by the slice: `EntityScriptProxy.ItemSpawnedEntity` (D-H4D1-3), the nine-integer buffer and `CaptureToBuffer` / `RestoreFromBuffer` in `CameraSway` (D-H4D1-2), the creator of the effect 19 in `NativeEffects` (D-H4D1-4). No word without a member and none needed to be skipped: not a stop.

## 4. (d) and (e): the probes of the DLL

Predictions: `predictions.md` (written and kept before the first run of the probe; the expected values come from the faithful keys by `key_facts.py`, `key_facts.txt`, `expected.json`, none from the DLL). Bench: as described there (the real world of the map 14, the flat field, a throwaway port of the pilot and of E28, the blocks of the
sprite 25 as targets). Command: `H4D1_OUT=<dir> dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=<skipcopy.targets> --filter FullyQualifiedName~H4d1T0ProbeTests --blame-hang-timeout 60s` (5 tests, 5 passed; the rows are in `probes/out/`; two runs identical,
`diff -rq` empty); the comparison is `probes/cmp_probe.py` (output `probes/cmp_probe.out`).

| Probe | Result |
|---|---|
| (d1) | **holds**: one entity of the type 21, born at the driver call of tick 34 (counter 32, animation `0x33`), `Status` `Normal` at every sample from 34 to 153, exactly one alive entity of the type at every sample 34..154 |
| (d2) | **holds**: `PosX = PosY = 6553600`, `PosZ = 1` (the hero's `PosZ` is 0: the newborn is at the hero's Z plus one raw unit), the three forces 0, at every sample of its life; flags 8260 (`0x2044`), attack (10, 128), the evaluated box 30 x 18 x 127 px at the birth (1966079 x 1179647 raw units + 1) growing to 178 x 158 px by the sample 100 (`probes/cmp_probe.out`) |
| (d3) | **holds**: `ForceResetAnimationFlag` 0 at 34..152, 1 first at 153 (S + 119) |
| (d4) | **holds**: the throwaway E28 runs once, at the tick 154; `FlagToDestroy` at the sample 154, `Destroyed` at every sample from 155; the 8 effects (19, 2, X, Y, Z = 2) at the tick 154 equal the key's journal (and the vector of the seed `0xA468A517`) |
| (d5) | **holds**: `Flag`, `LimitX`, `LimitY`, `SpeedX`, `SpeedY` equal the key's block at every tick 1..154 (R-EL-7); the pilot finds the identity changed at the tick 155 (counter 153), restores the captured words (all 0 at the sample 155), the hero is in `0x34` and the phase 2; the hero's animation is 0 at the sample 181 and the item freed at the sample 182. Supplementary: the hero's target animation, item, phase and MP equal the key at all 200 ticks, `ItemUseCounter` equals the key's counter at all 200 ticks (0 difference each) |
| (e) | **holds**: after every frame t = 1..199 the state of `GameRandom.RandomSeed` equals `rng0(t+1)` of `fire2_cast_pf` (0 difference); 1 step per tick, 9 at the tick 154; the state after the frame 153 is `0xA468A517`, after the frame 154 `0xEC3BCBAA` |
| (d6) | **holds on the ticks**: the blocks at (140,100), (100,140), (180,100) are written by the hit pass (the `FrameCollisionTickCounter` reads 25) at 42, 67, 92, 117, 142 / 54, 79, 104, 129 / 72, 97, 122, 147 and the edge set at 87, 112, 137 for (200,100) and (100,186), never for (201,100) and (100,187): all equal to the keys; the hero's counter and `HitCounter` stay 0; the draws per tick (steps - 1) equal the key's in both sets (9 per hit, 18 for two hits) |
| **erratum of my prediction (d6)** | my prediction said the number of sparks recorded by `HitSparksForTests` is 3 per hit; it is **1 per hit** (`WorldProxy.EmitHitSparks`: the seam is told the centre of each hit "before the three sparks are queued"). The two clauses are reported FAIL by `cmp_probe.out`; the clause rewritten after reading the code ("the seam count per tick = the number of blocks hit that tick") holds, and the 9 draws per hit show the three sparks. The prediction file is left as written. |

Information (not predicted, not a stop): at 2 and 3 ticks per frame (`cast_t2`, `cast_t3`) the sprite is first seen at tick 34, dies at 154 (E28 once, at 154) and the generator makes 208 steps over the 200 ticks (200 frame-end steps + the 8 draws of E28), as at one tick per frame; no sound is played in the run (`cast.sounds.csv` empty: the key's `sound 0` entries are the
silent id 0).

## 5. Findings carried to T1

1. **`fire2_hurt60`**: the discovery file `disc/keys/fire2_hurt60.json` differs from `critic/keys_faithful/fire2_hurt60.json` in 9 rows (ticks 34, 36, 39, 42, 45, 48, 51, 54, 57; only the `ents` field: the sprite's status 1 instead of 2 at the tick 34 and the pointer in `box[0]` one step behind afterwards). The critic's table ("identiques, 0 ligne différente") is inexact for it;
   `hurt20`, `hurt20_zerobuf` and `hurt20_stale` are identical row by row (the sprite is never born before the hurt). T1 reads all keys from `critic/keys_faithful/`.
2. The pilot port of the probe matches the key at every tick with the DLL as it is (h4a start, `TomeSlopeGuard`, the sway object, the spawn by the interface at the hero's `PosX`, `PosY`, `PosZ`): the T1 tests (w1) and (d1) have nothing to wait for from the engine side. The Z passed to `NativeEffects`/`EffectReservoir.Create` for E28 is `PosZ + 1` (the binary's frame).
3. At the tick 154 the pilot still sees the entity alive (`FlagToDestroy`, not recycled) and writes the block (c = 153); the recycling happens before the driver call of the tick 155 (key and DLL agree), as (d5) of T1 states.
4. The seam `HitSparksForTests` counts hits, not sparks (erratum above): the T1 test (w6) should count hits through the `FrameCollisionTickCounter` at 25 as `EarthTome2WorldTests.W5` does.

## 6. (f) The predicted list, recounted at the starting HEAD, and the stop test

- **The sums of keys of the production registry are FIVE** (not two); each is a sum of `<Class>.Keys.Count` that phase B raises by `FireTome2Ai.Keys.Count` (1; the registry holds 29 keys at the HEAD, 30 after): `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (the explicit sum, `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:60`),
  `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (and its secondary `sum` registry, `:84`, which must also register `FireTome2Ai`, else `sum.Count == production.Count` goes red),
  `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (same, its `sum`, `:244`), `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (the explicit sum in the tuple, `:187`),
  `BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` (the helper `Sum()`, `:235`, and the explicit sum, `:248`). In each, the list is derived (`+ FireTome2Ai.Keys.Count`, `FireTome2Ai.Register(sum)`), not re-typed.
- **Other candidates, to be checked by the suite at T2, not predicted here**: the tests that tick a world holding a global sprite 21 under the production registry (none is known: no test names the type 21; the 19 maps with the map index 21 are the type `0x115`, which the slice does not register); `PlayerManager.ProductionWiredWeaponTypes` does not hold 21;
  `HeroItemUseTests.A3` passes through the seams of h0a (not the production entries). The tests of h4a (`Tome*`) install their seams and are not moved by the phase B. **Predicted list for the phase B: the five tests above, and nothing else.**
- **The stop test**: `(E, 28)` is not registered for any type at the HEAD (the codes registered in slot E are 2, 3, 9, 10, 12, 13, 17, 19, 20) and `NativeSpriteRegistry.Register` is not cumulative (a second registration of a key throws); `Alundra/Ai/Items/` holds `BombAi.cs`, `EarthTome1Ai.cs`, `EarthTome2Ai.cs` (h0a, h4a, h4c1, h4c2 done); no need to touch the engine; no name refused (no file added to the DLL);
  the answers of the author are the defaults of the plan (Q-H4D1-1 A, Q-H4D1-2 A). **No stop.**
- **"The three seams of h0a"** are, as h4c1 and h4c2 found, three ternaries in `PlayerManager` (`StartItem`, `UpdateItemUseDriver`, `CancelItemMode`), already carrying the `0x2B` and `0x2C` branches; the `0x30` branch is the one to add to each (the plan's "entrées `0x30`").

## 7. Files

`predictions.md` (written before the probes); `key_facts.py`, `key_facts.txt`, `expected.json`; `keys.md`; `mapping.md`, `mapping_scan.py`, `mapping_scan.txt`; `replay/` (`replay_keys.sh`, the two manifests); `vec/` (the replay scripts and logs, `vec_e28_8007FB58_203.json`, `gen_e28_vectors_t0.py`, `c9_replay.py`, `extra_replay.py`);
`probes/` (`H4d1T0ProbeTests.cs.txt`, `cmp_probe.py` / `.out`, `out/` the rows); `suite/start.summary`; `SHA1SUMS`.
