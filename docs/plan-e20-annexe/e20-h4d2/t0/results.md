# E20.h4d2 T0 - results (annex, no code)

Run at the starting HEAD `785319c3` (branch `chantier/theme2-attaques-objets`, 2026-10-29). Plan: `docs/plan-e20-heros.md` section 1.31.6 (revision 2.1, READY) and section 1.31 (R-EL-1 to R-EL-7, R-EL-6 revised 2026-10-25). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read by the
scripts at `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`. Python 3.14.2. Nothing under `Alundra/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/`, `data-extracted/` was changed; the throwaway probe test was added to `Alundra.Tests/Probes/` for the runs and removed
(the source is filed as `probes/H4d2T0ProbeTests.cs.txt`, never committed as code). The line numbers of the plan date from 2026-10-18; the symbols were re-read at the HEAD.

## 0. Verdict: DONE, no stop of the plan reached; (a), (b), (c), (d) done; one clause of the PLAN is wrong (section 4, P5), flagged; no clause of my own predictions was contradicted

- (a): `qfix_make_keys.py` and its outputs versed; every replay is identical on two runs and identical to the filed files (section 2).
- (b): no word read by the vectors lacks a member outside the rules of the adapter; one observation on the `binary_only` vectors at the HEAD (section 3, `mapping.md`).
- (c): the 15 probes hold on the DLL (90 clauses, 0 FAIL, `probes/cmp_probe.out`); the whole row of every entity of the throwaway-port run equals the key row by row (cast 900 rows, three relief keys 875, 849, 932; 0 difference).
- (d): the predicted list, recounted (section 5), has ONE test more than the plan's list.

## 1. (a0) The starting HEAD and the suite

`git rev-parse HEAD` = `785319c3a2d2f47efb48c8b8ea00e70f0285cbea`; build and suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets`), `--blame-hang-timeout 60s`, ONE run of the whole assembly (`dotnet test Alundra.Tests/Alundra.Tests.csproj`), which the harness
moved to the background at the 600 s limit and which ended with exit code 0: **7 828 tests, 0 failure, 0 skipped, 24 min 51 s** (`suite/start.summary`; 7 828 = the total at the end of E20.h4d1). The suite rewrote the four `docs/hero-trace-389-*.txt` with CR line ends: `git diff --ignore-cr-at-eol --quiet -- docs/` true, restored by `git checkout`, never indexed.

## 2. (a) The replays, from copies

The scripts read the binary at an absolute path and write next to themselves: copied to the scratchpad (`.../scratchpad/h4d2-t0/copy/`, `rel_their/`, `rel_my/`) and run there with `python -B`.

| What | Command (from the copy) | Result |
|---|---|---|
| the versed script and its outputs | `cp scratchpad/t2-next/h4-rev/h4d2/h4o/qfix_make_keys.py disc/h4o/`; `out_qfix/` to `disc/out_qfix/` (10 keys + `manifest.txt`); `turn_law.py` of the same folder to `disc/h4o/` | `SHA1SUMS` of the annex updated; the other 39 scripts of `h4o/` of the scratchpad are byte-identical to the filed ones |
| the 30 world keys | `python -B fire1_make_keys.py OUT` twice (4 min 15 s each) | the two manifests identical to each other and to `disc/keys/manifest.txt` (`replay/keys_manifest_run1.txt`, `_run2.txt`) |
| the `_qfix` keys (revision 2) | `python -B qfix_make_keys.py OUT` twice | the two manifests identical to each other and to `disc/out_qfix/manifest.txt`; the four scenarios `tgt_one`, `tgt_three`, `tgt_vanish75`, `col_hit` and the bare cast: the `_pf` file and the `_qfix` file have identical `rows` (checked by loading the JSON), the MD5 of the five `_pf` equal the ones of `disc/keys/` |
| the four vector files | `python -B vec_fire1.py OUT` twice (2 min 13 s each) | four MD5 identical on the two runs and equal to `disc/vec_manifest_run1.txt` (`84ef923f...`, `45a91611...`, `a961dcdd...`, `d99223e9...`); "TOTAL problems 0": 3 270, 400, 288, 3 524 vectors replayed from an empty memory, 0 model mismatch, the coverages of `disc/report.md` 3.4 (driver 120/120, guard 19/19, cancel 55/55, DestroyNoFx 15/15, E3 25/25, C63 95/95, query 94/94, sort 91/91, dirto 35/35) |
| the models against the vectors | `python -B vec_selftest.py VEC` | 36 mutants, "ALL MUTANTS KILLED" (`replay/vec_selftest_run.txt`); the corrected mode differs from the binary on 1 433 of 3 524 C63 vectors |
| the stored count | `python -B probe_count.py` | 494 queries, 0 difference between the stored count and the highest live slot; the variant `replay/probe_count_dump.py` (same code plus a dump of every call as `[tick, caller, stored count, on-demand count]`, run twice: identical files) is the oracle of P13 |
| the h4 vectors of the tome wrappers and handlers | `python -B h4_vec_drivers.py 1500 20261017`, `python -B h4_vec_handlers.py 1500 20261017` | "vectors 1500 seed 20261017 mismatches 0" each; the logs equal `disc/h4_vec_drivers_replay.log` and `disc/h4_vec_handlers_replay.log` |
| the three relief keys | `python -B make_relief_keys.py OUT` run twice on each of two interpreters: the critic's script needs `fire1_lib.py` and a machine next to it, so it was run in a copy of `disc/h4o/` (the discovery's `emu.py`) AND in a copy where `emu.py` is the critic's own `myemu.py` | the four manifests identical; the three MD5 (`67573c59...`, `ae285b07...`, `d777cef6...`) equal the critic's table (`critic/results/relief_keys_manifest.txt`) |

## 3. (b) The table of the words and the rules of the adapter

`mapping.md` (with `mapping_scan.py` and `mapping_scan.txt`): the distinct (address, size) pairs of the four vector files, each with its member of the DLL, and the rules of the adapter (pointer <-> `Slot`; an identity read different from the stored one -> a dead reference `Destroyed`, `Slot -1`; the DLL's Z is the binary's minus 1; the `binary_only` vectors skipped; the output arrays of the query ignored; the executable's tables not mapped).
**No entity word without a member outside the rules: no stop.** Observation (not a stop, the plan's rule is kept): since E20.g1a W5 the one-argument `DestroyEntity(e)` clears the carrier (`+0x28` -> `+0x2C`), so 37 driver vectors and 23 cancellation vectors whose only non-zero `binary_only` word is a `+0x28` could be compared by T1 (a possible extension); the `+0x1B8` has no member.
Members that exist at the HEAD and that the plan says to create "if h4d1 did not": `EntityScriptProxy.ItemSpawnedEntity` and `NativeEffects.CreateFireColumnBlast` (D-H4D1-3, D-H4D1-4) exist; **the function of the count of `TargetQuery` does not** (`TargetQuery.Find` takes the count as a parameter; h4e1 is not done): h4d2 poses it (D-H4D2-5).

## 4. (c) The probes of the DLL

Predictions: `predictions.md` (written and filed BEFORE the first run of the probes; the expected values come from the keys by `key_facts.py`, `key_facts.txt`, `expected.json`, none from the DLL). Bench: as `predictions.md` says (the real world of the map 14, the flat field, throwaway ports of the driver `0x80035A84`, the cancellation `0x80034D2C` and C63 `0x8007AC60`
written from the plan's facts only, E3 for `{16, 23}` registered on the registry of the runner by taking off its `{16}` entry and registering it again, E1 in line). Command: `H4D2_OUT=<dir> H4D2_VEC_C63=<vec_c63 file> dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=<skipcopy.targets> --filter FullyQualifiedName~H4d2T0ProbeTests --blame-hang-timeout 60s`
(39 tests, 39 passed in 27 s; the rows are in `probes/out/`; two runs identical, `diff -rq` empty); the comparison is `probes/cmp_probe.py` (output `probes/cmp_probe.out`: **90 clauses, 0 FAIL**). The comparator was checked by corrupting one value of the DLL rows in a copy: it reports the difference.

| Probe | Result |
|---|---|
| P1 | **holds**: one column (type 20), born at the tick 34 in the slot 1, `Normal` at the sample of 34, Z 1 179 649, flags 8 260, attack (10, 64) |
| P2 | **holds**: eleven balls at 34 + 8k; slots 2, 3, 4, 5, 6, 7, 8, 9, 2, 10, 11; directions 4, 27, 18, 1, 4, 22, 29, 5, 13, 26, 21; `ForceZ` 524 288, Z 1 572 865, `+0x274` 16 at S, 15 at S+1, 0 first at S+16 |
| P3 | **holds**: at t = 1 .. 199 the state after the frame equals `rng0(t+1)` and the steps equal `draws + 1`; 315 steps over the 200 ticks (the key's 115 draws + 200 frame-end steps); `seedB` is checked by P13 (its 107 calls) |
| P4 | **holds** exactly (no one-tick early status of R-EL-5): the balls stopped in the air go to the status 3 at 86, 109, 118, 135, 162 |
| P5 | **holds**: the landing ticks (`IsOnGround`) 109, 117, 125, 141, 149, 165, 173, 181 (109 for the ball born at 42), the status 3 at L+1 with `TargetAnimationId` 1 and flags `0x102044`. **A clause of the plan is wrong, about the key, not about the DLL**: the plan writes "fx decreasing by 8 688 per tick" next to the ball of `id` 4; the key gives 10 224 per tick for the ball born at 42 (`id` 4, direction 27) and 8 688 for the balls of direction 4 (born at 34 and 66, stopped in the air at 86 and 118); the DLL equals the key ball by ball |
| P6 | **holds**: `+0xAC` at (status 3) + 10 and `FlagToDestroy` at (status 3) + 11 for the eleven balls; `Destroyed` and `Slot < 0` at + 12 |
| P7 | **holds**: the column's `+0xAC` at 120, `FlagToDestroy` at 121 by the in-line E1 (no effect created), `Destroyed` with `Slot < 0` at 122 |
| P8 | **holds**: the evaluated box of the column 1 507 327 x 1 048 575 x 3 080 191 raw units (offsets -786 432, -524 288, -1 179 648), of a ball 1 507 327 x 983 039 x 983 039; the twelve edge pairs equal the critic's sweep (first hit at 34 for x = 76, 77, 121, 122 and y = 85, 86, 114, 115; none for x = 75, 123 and y = 84, 116) |
| P9 | **holds**: ball 1 at the sample of 51: attribute 10, power 32; the column (10, 64) |
| P10 | **holds** for the ten interruptions: the effect `(19, 2, 6553600, 6553600, 1179650)` at the tick T for T in {35, 60, 100, 121} and none for T in {10, 33, 34, 122, 124, 140}; the (type, status) of the entities at T+3 and the item (in use, phase, counter, MP) at T+2 equal the key |
| P11 | **holds**: `Slope_18c` 0 for every ball at every sample on deep water (columns 6 to 12); every entity row equals the cast row |
| P12 | **holds**: the cells of the cast; `table_one_free` the column in the slot 63, no ball; `table_full` the birth refused at the tick 34, the item freed (MP 3) |
| P13 | **holds** in the binary mode (`inclusiveBound` false, `sortThreshold` 3): the count of D-H4D2-5 (the highest `Slot` of the entities not `Destroyed` nor `FlagToDestroy`) equals the stored count at each of the 494 calls (tick and caller included), and the target slot kept at each tick equals the key's `b278_slot` in `tgt_one`, `tgt_three`, `tgt_two_wrong_order`, `tgt_topslot`, `tgt_vanish75` |
| P14 | **holds**: the landing ticks and heights on the plateaux (16 px = 1 048 576 on hb1, 32 px = 2 097 152 on hb2, 0 off the plateau, 0 for the hero on the plateau) and the status 3 ticks equal the three relief keys |
| P15 | **holds**: `ScriptHelper.GetDirectionToTarget` equals the binary on the 1 024 raw pairs of the grid, and the numeric turn model equals the binary's `+0x8C` write on the 1 024 |

Information (not predicted, not a stop): at 2 and 3 ticks per frame the ball births are the same (34 + 8k) and the column's last status is at 121; the 12 entity rows of the cast equal the key's on 900 rows.
A note on the bench: the probes' C63 is the plan's, ported before T1; T1's own tests compare the production `FireTome1Ai`, not this port.

## 5. (d) The predicted list, recounted at the starting HEAD, and the stop test

- **The sums of keys of the production registry are FIVE** (not "two pins"); each is a sum of `<Class>.Keys.Count` that the phase B raises by `FireTome1Ai.Keys.Count` (1; the registry holds 30 keys at the HEAD, 31 after): `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (`Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:60`),
  `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (`:88` and its secondary `sum` registry, `:83-85`, which must also register `FireTome1Ai`), `ChargedArrowAiTests.C2_...` (`:250`, its `sum`, `:243-245`), `WaveSwordLifeTests.V3_...` (`:187`),
  `BallFlamesTests.M3_...` (`:249`, its `Sum()` helper `:234-236`). In each, the list is derived (`+ FireTome1Ai.Keys.Count`, `FireTome1Ai.Register(sum)`), not re-typed.
- **(e2) of h4c1**, `EarthTome1AiTests.E2_E3_IsRegisteredForTheType16Alone_NotThe17_NorThe23_NorAnyOtherType_...` (`Alundra.Tests/Ai/Items/EarthTome1AiTests.cs:394`): E3 for `{16, 23}`; its loop of "other types" (`:403`) must lose the 23, its name changes; it moves in the PHASE A (the widening of `EarthTome1Ai.SpriteTypes`).
- **ONE TEST MORE THAN THE PLAN'S LIST (measured by this T0, `list_d.txt`)**: `EarthTome1ProductionTests.P4_TheProductionRegistryHandlesTheCodesOfTheType16_AnEmptyOneLacksOnlyE3` (`Alundra.Tests/Ai/Items/EarthTome1ProductionTests.cs:143`) reads `EarthTome1Ai.SpriteTypes`: widened to `{16, 23}`, `UnhandledWeaponSpriteCodes` on the production registry answers
  `[(C, 63)]` until the phase B (its first assertion, "empty", is red in the phase A) and, on an empty registry, `[(E, 3), (C, 63), (E, 3)]` instead of `[(E, 3)]` (its second assertion). It moves in the phase A and is rewritten there (derived from the sheets of the types 16 and 23), green after the phase B. The plan's (p4) `UnhandledWeaponSpriteCodes({20, 23}, ...)` is `[(C, 63)]` at the HEAD
  (`list_d.txt`), empty after the phase B, as the plan says; `{16}` and `{20}` alone are empty now.
- The tests of the loops v1 that make the types 20 and 23 born without a tick (`SpawnBySpriteIndexInterfaceTests` B2 `:92`, `SpawnBySpriteIndexCoreTests` `:199`, `BirthBoxTests`) do not tick them: they do not move (to be confirmed by the suites of T1 and T2). `PlayerManager.ProductionWiredWeaponTypes` (3 4 7 11 12 14 15 27 28 29) holds neither 20 nor 23.
- **The stop test**: `(C, 63)` is not registered at the HEAD (the production registry, 30 keys, lacks it: `unhandled {23}: (2, 63) (4, 3)`), `NativeSpriteRegistry.Register` is not cumulative (a second registration of a key throws, `NativeSpriteRegistry.cs:81`; the probe had to take the E3 entry off to widen it, the production widening is `EarthTome1Ai.SpriteTypes` alone);
  `Alundra/Ai/Items/` holds `BombAi.cs`, `EarthTome1Ai.cs`, `EarthTome2Ai.cs`, `FireTome2Ai.cs` (h0a, h4a, h4b1, h4c1, h4c2, h4d1 done, f4, f5, f6a before them); no need to touch the engine; no name refused (no file added to the DLL by T0); the answers of the author are the defaults of the plan (Q-H4B-2, Q-H4B-3, Q-H4C1-1, Q-H4D2-1 B, Q-H4B-5).
  **No stop.**

## 6. Files

`predictions.md` (before the probes); `key_facts.py`, `key_facts.txt`, `expected.json`; `mapping.md`, `mapping_scan.py`, `mapping_scan.txt`; `replay/` (the manifests of the two runs of each replay, the logs, the dump of the queries and its script); `probes/` (`H4d2T0ProbeTests.cs.txt`, `cmp_probe.py`, `cmp_probe.out`, `out/` the rows); `list_d.txt`; `suite/start.summary`; `SHA1SUMS`.
