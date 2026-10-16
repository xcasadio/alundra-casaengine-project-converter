# E20.f3 - annex of the oracle (T0 of E20.f3a, common to E20.f3a, E20.f3b and E20.f3c)

Plan: `docs/plan-e20-heros.md`, section 1.19 (E20.f3) and 1.19.1 (E20.f3a, task T0). This folder is the oracle of the three sub-slices: the sword wave (types 27 to 29, handlers C61 and E10), the hit
effects 4 and 5 of the hit pass, and the direction of the hero's shot. It is real code of `ALUN_CD.EXE` (France) executed by the emulated machine of the E14/E20 oracles
(`oracle/tools/pworld.py`, the f-projectiles machine: real `MovePlayer`, real `SpawnWarpEntity` `0x80039F58`, real hit pass `0x80039300`, real projectile handlers; effects, sounds and every other
handler are journalling stubs). Nothing here knows an expected value of the DLL: every number is read from the machine's RAM.

## Run

From the repository root, foreground, about 9 minutes in all:

```
python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py text      # 19 discovery scripts -> out/*.txt          (3 min 40)
python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py mutants   # 12 one-word mutants  -> out/mutations.*   (30 s)
python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py flame     # flame probe          -> out/flame_probe.* (45 s)
python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py json      # JSON twins           -> out/*.json        (1 min 45)
python -B docs/plan-e20-annexe/e20-f3/oracle/run_f3.py checks    # out/checks.txt (21 facts, PASS/FAIL) and out/manifest.txt
```

Two complete runs (the whole `out/` folder deleted between them) gave an identical `out/manifest.txt` (45 files, md5) and `diff -rq` on the two folders was empty. The text outputs are byte-identical
to the outputs of the discovery (`critic/out` and `bin/out` of the session scratchpad of 2026-10-15) for every file they have in common. The tools are in `oracle/tools/` (56 files, byte copies of the
discovery, SHA-1 in `oracle/checksums.txt`; 25 are byte-identical to files of earlier annexes, `pworld.py` among them with `e20-e0a/oracle/tools/pworld.py`; `diff2.py` has the same name as
`d1-oracle-moveplayer/tools/diff2.py` with other bytes; 30 are new, the scripts of this discovery). The scripts of `tools/` write into `../out`, which is `oracle/out/`.

## Outputs

Every row of a `wave_*.json`, `wave_hit_*.json`, `wave_z.json`, `wave_grass.json`, `arena_probe.json` and `hero_fractional.json` is a row of the oracle: `{tick, hold, rng, rec_ptr, rec_cnt, charge, journal, slots}`
and `slots["1"].status`, `.cnt1c`, `.reset_AC`, `.px`, `.py`, `.pz`, `.fx`, `.fy`, `.fz`, `.adj_13C`, `.ground_144`, `.hbx`, `.hby`, `.hbz`, `.bw`, `.bd`, `.bh`, `.atk_attr`, `.atk_pow`, `.type68`, `.flags6C`,
`.loot3c`, `.c1d0`, `.touch_slot`, `.box1d4` (and the other keys of `pworld.FIELDS`). Positions and forces are raw 16.16 integers; `pz` is the binary's frame (the DLL's frame is this minus one raw unit, ADR-0026).
The release tick of the oracle is `R = 96`; the arena of `AlundraHeroChargeArenaTests` releases at 91, so an oracle tick minus 5 is an arena tick.

| Script (tools/) | Text output | JSON output |
|---|---|---|
| `wave_run.py dirs`, `summary.py` | `wave_{steel_east,steel_north,steel_west,steel_south,demon_east,holy_east}.txt`, `summary.txt` | `wave_<sword>_<direction>.json` (ticks 100 to 150) |
| `wave_hit.py east/north/sweep/balls` | `wave_hit_east.txt`, `wave_hit_north.txt`, `wave_reach_sweep.txt`, `balls_vs_target.txt` | `wave_hit_east.json`, `wave_hit_north.json` |
| `wave_only.py` | `wave_only_sweep.txt` | `wave_only_sweep.json` |
| `wave_z.py` | `wave_z.txt` | `wave_z.json` |
| `wave_mut.py` | `wave_mutations.txt` | `mutations.json` (W1 to W3) |
| `wave_grass.py`, `wave_grass2.py`, `wave_grass3.py` | `wave_grass.txt`, `wave_grass2.txt`, `wave_grass_zwindow.txt` | `wave_grass.json` |
| `hitfx.py`, `hitfx_mut.py` | `hitfx_table.txt`, `hitfx_mutations.txt` | `hitfx_table.json` (`attr`, `nib`, `resp`, `fx`, `sparks`, `c1d0`, `touch`, `hitc`, `rng_before`, `rng_after`), `mutations.json` (H1 to H4) |
| `multishot.py` | `multishot.txt` | `multishot.json` |
| `dirtest.py`, `dir_extra.py` | `dirtest.txt`, `dir_extra.txt` | `directions.json` (11 cases) |
| `arena_probe.py` | `arena_probe.txt` | `arena_probe.json` |
| `twoint.py` | `two_interpreters.txt` | - |
| `mutants_f3.py` | `mutations.txt` | `mutations.json` (12 rows) |
| `flame_probe.py` | `flame_probe.txt` | `flame_probe.json` |
| `export_json.py` (`hero_fractional`) | - | `hero_fractional.json` |
| `run_f3.py checks` | `checks.txt`, `manifest.txt` | - |

## Expected values (read from `out/`; the tests read the JSON by key)

- **Wave, steel east, hero (100, 100), release `R = 96`:** born at `S = R + 21 = 117`, at the hero's X and Y, raw Z 2 (the hero's 1 plus 1), forces 0, `ForceAdjusted` (`adj_13C`) 0, `ground_144` 0 for its whole life;
  flags `0x205C`; attack attribute `0x81`, power 10 (demon 20, holy 38); size 36x24x31 (raw 2359295, 1572863, 2031615); `+0x1C` is 1 at S and 26 at S+25; `+0xAC` is 1 from S+25; status 4 at S+26 (E10);
  the slot is free at S+27. The box moves 12 px east at S+1, S+3, S+5, S+7, S+9 (north: -8 in Y, west -12, south +8) and then stays. Sound 30 at S; `snd(0)` at S+25 is muted (`0x80049134`).
  Only the target classes of class A are touched; the hero is never touched. Reach, wave alone: targets at +32, +44, +56, +68, +80 px east (hero (100, 100), record row 11) are first touched at S+1, S+3, S+5, S+7, S+9; none beyond +92
  (north -60, west -88, south +60 are out of reach).
- **Wave on level ground and on ledges (`wave_z`, four variants: flat height 2, three ledge positions):** at S the wave's raw Z is the hero's raw Z plus 1 (2097154, 1048578, 2, 2), it stays constant to S+26, with `adj_13C` 0 and `ground_144` 0 (the wave never lands).
- **Grass (`wave_grass`):** with every cell grass, the steel wave cuts 3 cells (the first at S+10, east); each cut draws at least one stream word and may drop an item.
- **Hit effects 4 and 5 (`hitfx_table`):** 160 pairs (attribute nibble 0..15 times ten response bytes), 160 match: gate `(resp & 0xC0) == 0x80` -> no effect and no `TouchingEntity` store (still `c1d0 = 25`, `hitc` 1, three sparks); otherwise
  nibbles 6 and 10 -> effect 4, nibbles 7 and 9 -> effect 5, through `0x8003BE74(0, id, 0, target, 1, 0, 0, 0)`; no stream word is drawn by the effect call (the stream step is the sparks').
  Fire rod ball on a Beannoid: `fxattach(id=4, target=slot 1)` at the hit tick 20 and again at 45; ice rod: id 5 at 21 and 46.
- **Charge arena pin:** with E10 running the target is touched twice (`(5, hero)`, `(118, hero)`); without E10 two more contacts by the wave appear at oracle ticks 143 and 168 (arena ticks 138 and 163; the third contact
  falls inside the 140 ticks of the arena).
- **Two arrows live together** (`multishot`): the hunter's bow fires at ticks 10 and 34 (Square at t1 and t25), nothing counts the live projectiles.
- **Direction keys of E20.f3a (`directions.json`, hunter's bow, item 5; `arrow.dir_t` is the direction word of the real shot):**
  (d1) `dirtest_air_cross_t1_square_left_t8`: Cross at t1, Square + Left at t8, shot at t9, `dir_t = 8`;
  (d2) `dir_extra_air_square_t8_left_only_from_t9`: Square alone at t8, Left only from t9, shot at t9, `dir_t = 24` (`0x18`; the turn of the shot tick is ignored);
  (d3) `dirtest_ground_square_up_t1` 16 (`0x10`), `dir_extra_ground_square_down_t1` 0, `dir_extra_ground_square_left_t1` 8, `dir_extra_ground_square_right_t1` 24 (`0x18`), ground, shot at t10;
  the ground facing cases (`dir_extra_ground_facing_*`) give 16, 8, 0 for north, west, south.
- **Fractional hero position (O-F3-2):** `hero_fractional.json`: the hero poked at X = 100.5 and Y = 100.25 px (raw `0x648000`, `0x644000`), the wave is born at the same raw X and Y (no rounding in the binary).

## Mutants (`mutations.txt`, `mutations.json`): 12 of 12 killed

Real one-word patches of the emulated RAM after the world is built (never simulated in a test, never committed): W1 (`0x80039EA4`, spawn Z without the +1: the wave dies at S+1), W2 (`0x8007A960`, C61 call removed: 1 grass cut instead of 3),
W3 (`0x8007F244`, E10 call removed: the wave stays in status 3), H1 to H4 (`hitfx_mut.py`: gate mask, nibble of effect 4, effect id, gate branch: 32, 10, 10 and 128 of 128 pairs differ), D1 (direction table swapped: facing north gives 8
instead of 16), D2 (`0x8002F7C8`, delay comparison inverted: the shot leaves at tick 2 instead of 10), D3 (`0x8002F7E8`, z byte shift 16 to 17: raw Z doubles), D4 (`0x8002F828`, weapon pointer not cleared: 15 arrows instead of 1),
D5 (`0x8002F7B4`, animation flag b1 ignored). D5 needs a harness stimulus, said here: the hero's target animation (`+0x88`) is overwritten with 0 at tick 3, as a hurt or fall animation would do; control then loses the shot (the pointer is
cleared), the mutant still shoots at tick 10. Five pad histories (a second Cross, a second Square, a held direction, Triangle) do not change the animation during the delay and do not kill D5 (all listed in `mutations.txt`).
No mutant is equivalent.

## Flame probe (`flame_probe.json`, `flame_probe.txt`, recette of E20.f3b, O-F3-5)

Map 14, entity record 17: global sprite 238, cell (14, 28), Height 18 (raw Z 9437185), spawned by the real map-record spawner on a flat field at that height. Header: programs A 0, C 0, D 34 (touch), body 40x24x30, break effect 8;
balance record: attacks `[attribute 6 power 1, attribute 1 power 1]`, MaxHp 1. **Animation 0 attacks, with the nibble 1** (attack index 1), box 42x26x31 at the flame; the hero, standing at the flame's Z with an offset between -30 and +30 px in X and between -20 and +10 px in Y (grid of 10 px, `flame_probe.txt`), is hit at the first tick and every 120 ticks (`c1d0 = 25`, HP - 1) and **no effect is attached** (sparks only). The nibble 6 (effect 4, `fxattach(id=4, target=slot 0)`) belongs to the animation set 1 (attack index 0);
the oracle forces that set (its touch program D34 is not run and not decoded here): the hero is hit at tick 1 with effect 4 attached and the flame ends at tick 48. Answer for the recette: **no for effect 4 in animation 0**; whether D34
selects the set 1 when the hero touches the flame is NOT established, so the recette of f3b may have to take another of the 85 maps (a "no" blocks nothing, as the plan says).

## Prediction (qualitative) and starting count

- E20.f3a: no existing test moves. Phase A changes nothing observable (the set of wired types is empty, the direction stays `AnimationDirection`); phase B changes only the `Direction` field of the weapon journal for a turn
  during the swing in the air (the eight existing journal cases stay at `0x18`). Both suites (Release and Debug) keep their count plus the new tests.
- E20.f3b: no existing test moves (the effect call is added behind a nullable delegate); the visible change is real-game only (85 maps place an attacker of nibble 6, 7, 9 or 10).
- E20.f3c: the production registry pin goes from 6 to 7 keys (C61 added); the arena pin stays at 2 contacts (E10 handled inline).
- **Starting count: 4852 tests, Release and Debug, 0 failures** (re-measured at the HEAD `d0e91db` on 2026-10-15, after E20.e0b1).

## E20.f3c - predicted list of the existing tests that move (written before any code, at HEAD 59605d3)

Starting count re-measured at 59605d3 (Release, 2026-10-16): 4926 tests, 0 failures. Search done: `ProductionWiredWeaponTypes` (only `AlundraWeaponSpawnSeamTests` and `AlundraPlayerManager`), `Give(`/`Equip(` of a sword followed by a release of the Square in a world (`ArcRun` or `HitArena`), `SpawnEntityBySpriteIndex`/`WeaponSpawnBirthHookForTests` users, and the montages that are not an `IEntityWorldContext` (no entity is born there, so they cannot move).

Certain:
- `AlundraNativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheSixHandlersOfTheBeannoidAndTheMuruta_AndTheSwitchIsOn` (line 51): 6 keys become 7, by derivation 6 + (C, 61). Phase A.
- `AlundraWeaponSpawnSeamTests.S5_TheProductionSetPassesTheInvariant_TheFakeSetWithTheArrowFailsOnE17`: it asserts `Assert.Empty(ProductionWiredWeaponTypes)`; phase B makes the set {27, 28, 29}, so the assertion is rewritten as `Equal({27, 28, 29})` and `UnhandledWeaponSpriteCodes(production)` stays empty (C61 registered, E10 handled in line). Phase B.

Possible (a sword charge released past R + 21 in a world; the wave is born there from phase B on: sound 30, one more entity):
- `AlundraHeroChargeArenaTests` (`HitArena`, steel sword, Square held 90 ticks, release at 91, wave at S = 112): `TheChargedSwing0x18_OfTheSteelSword_TouchesABeannoidOfMap14` runs 140 ticks and counts the contacts of a Beannoid 40 px east (2 contacts today; with E10 in line the arena pin stays at 2, the wave of the target at +40 touches it only if E10 does not run); `TheEffectsOfTheCharge_AreCreatedInTheWorldsReservoir_TheBurstAtTheRelease` and the reservoir-by-grouping theory stop at tick 95 and 96 (before S): not expected to move.
- `AlundraHeroChargeTests`: montages (`Montage`, hosts that are not an `IEntityWorldContext`): the shot is counted and journalled, nothing is born; not expected to move.
- `AlundraHeroAuraWorldTests` (`HitArena`, steel sword): the Square is held to the end of every scenario (no release), so no wave is born; not expected to move.
- `AlundraRandomPerTickTests.TheChargeSparks_DrawBeforeTheAdvanceOfTheirTick_WhateverTheGrouping` (`HitArena`, steel sword, 96 ticks, release at 91, S = 112): stops before S; not expected to move.
- `AlundraBeannoideGrateArcTests` and `AlundraMurutaHallArcTests` (`Give(1)`/`Equip(1)`, the item 1, presses of the Square of a few ticks): a press is a swing 0x3F, never a charge; not expected to move.
No other test asserts the empty production set (`ProductionWiredWeaponTypes` is read only by `AlundraWeaponSpawnSeamTests.S5`); the tests that fire a bow (`AlundraWeaponSpawnSeamTests` S1 to S4, S6, `AlundraShotDirectionTests`, `AlundraBirthTickTests`) use the type 3 or a test set and the hook, which replaces the production call: not expected to move. The measured list (an empirical run with the wiring, throwaway) is compared with this one in the results of the slice.

## O-F3-6 - the release tick of the hit effects 4 and 5, settled on the binary (2026-10-16)

`oracle/of36/` holds, copied byte for byte from the session scratchpad, the probe that settled O-F3-6 and its independent check:
`probe/` (the harness on this annex's MIPS machine, imported by path from `oracle/tools`, with its summaries in `probe/out/`; the
per-case traces are not kept, the scripts write them again), `check/` (a second harness written from scratch by another agent, and its
summary `chk_bin_summary.json`), and the two reports `probe-report.md` and `check-report.md`. The scripts carry the absolute paths of
the run (`OUT` next to the script); run them only from a scratch copy, with `python -B`.

Result: the binary frees effect 4 at hit + 41 and effect 5 at hit + 96 (the sparks at hit + 26), the same ticks as the DLL. The pass that
steps the effect slots is `0x8003C410`, called at `0x8002E110` by the frame function `0x8002E058` right after the world tick `0x8003B388`;
`0x80038E84` (the DLL's `RunActiveEffectsPass`) never steps a slot. The f3b executor's inference (hit + 42 and + 97) does not hold;
no DLL change.

## E20.f3c - recount of the predicted list at the resumption HEAD c2f4a65 (2026-10-16, before any code)

The list above (written at 59605d3) is not rewritten. Re-searched at c2f4a65, after E20.e0a5 (ADR-0071). Starting count to re-measure before any change (the plan says 5010).

Searches: `ProductionWiredWeaponTypes` (still only `AlundraWeaponSpawnSeamTests` and `AlundraPlayerManager`); `WeaponSpawnBirthHookForTests` / `WiredWeaponTypesForTests` / `SpawnEntityBySpriteIndex` users (`AlundraBirthBoxTests`, `AlundraBirthTickTests`, `AlundraBirthTickProbePaTests`, `AlundraCreationSwitchClockTests`, `AlundraShotDirectionTests`, `AlundraWeaponSpawnSeamTests`, the slot, logical-Z and map-state tests, `AlundraSpawnBySpriteIndexInterfaceTests`); `Give(` / `Equip(` of an item in a world (`ArcRun`, `HitArena`): `AlundraHeroChargeArenaTests`, `AlundraHeroAuraWorldTests`, `AlundraRandomPerTickTests.Advance`, `AlundraShotDirectionTests` (hunter's bow), the flail tests (flail slot, no wave), `AlundraBeannoideGrateArcTests` and `AlundraMurutaHallArcTests` (item 1).

Result: same certain and possible lists, with these precisions.
- Certain (unchanged): `AlundraNativeSpriteRegistryTests` pin 6 to 7 (phase A, 6 + (C, 61)); `AlundraWeaponSpawnSeamTests.S5` (phase B).
- New tests of E20.e0a5 (`AlundraCreationSwitchClockTests`, `AlundraBirthBoxTests`, `AlundraBirthTickTests`): they birth a wave (type 27 and others) through the test hook, which replaces the production call of the shot (D-F3-3, hook first): the production set of wired types does not take part, so they do not move in phase A nor in phase B. Their registry is the production one: C61 now exists for the types 27 to 29, so a wave born there calls the named stub from S+1 (a journal only, no state change) and E10 (in line) destroys it at S+26. Possible effect to watch in the empirical run: a test of those files that runs a wave past S+25 and asserts it alive, or asserts the slot table or the entity count after S+26 (the wave used to live for ever in a test without E10). Listed as possible, not expected to move: `AlundraCreationSwitchClockTests` (window of the wave, A6), `AlundraBirthBoxTests` (boxes up to the last oracle tick of the wave), `AlundraBirthTickTests`.
- `AlundraRandomPerTickTests.Advance` (steel sword, `Give(2)`): possible, stops before S (see the list above), not expected to move.
- `AlundraHeroChargeArenaTests`, `AlundraHeroAuraWorldTests`, `AlundraHeroChargeTests`, `AlundraBeannoideGrateArcTests`, `AlundraMurutaHallArcTests`: as in the list above.
The measured list stays to be compared in the results of the slice.
