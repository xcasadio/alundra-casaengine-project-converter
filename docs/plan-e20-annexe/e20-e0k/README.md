# E20.e0k - annex of the oracle (T0 of E20.e0k1, common to E20.e0k1, E20.e0k2 and E20.e0k3)

Plan: `docs/plan-e20-heros.md`, section 1.18 (E20.e0k) and 1.18.1 (E20.e0k1, task T0). This folder is the oracle of the three sub-slices: the vertical step and the flight of the entities that are not the hero
(arrows, balls, the wave, a thrown crate, a walking monster). It is real code of `ALUN_CD.EXE` (France) executed by the emulated machine of the E14/E20 oracles (`oracle/oracle/`, the same machine files as the
earlier annexes: real `MovePlayer`, real `SpawnWarpEntity` `0x80039F58`, real map-record spawner `0x8003A1B8`, real physics pass `0x80038364`; effects, sounds and every handler not named in `k0lib.py` are
journalling stubs). Nothing here knows an expected value of the DLL: every number is read from the machine's RAM. The DLL is not run by this folder.

## Run

From `docs/plan-e20-annexe/e20-e0k/oracle/`, foreground, every command writes into `out/`:

```
python -B -I run_all.py          # 10 scenario modules, each on the two interpreters (emu.py and mips.py); out/*.txt and out/*.json; out/manifest.txt = md5 of the 88 deterministic files   (3 min 35)
python -B -I mutants_e0k.py      # 18 one-word mutants of the emulated binary, 11 scenarios each -> out/mutations.txt, out/mutations.json                                        (2 min 10)
python -B -I residual_stats.py   # model of the halving refinement (20 000 raw gaps) -> out/residual_stats.txt (redirect the output)
python -B -I census_header.py    # the map header words of the 483 maps -> out/census_header.txt (redirect)
python -B -I census_flags.py     # the flags of the sprite records -> out/census_flags.txt (redirect)
```

`k0_noriders.py` and `k0_slide.py` and the folder `scripts/` are the discovery's one-off tools (not part of the manifest). Python 3.14 with `capstone` in the user site (see `exe.py`, `oracle/lib.py`).

## Determinism (T0 acceptance)

Two complete runs of `run_all.py` (the whole `out/` folder deleted between them) gave an identical `out/manifest.txt` of **88 files** (md5) and `diff -rq` on the two folders was empty. The manifest is also identical,
line for line, to the one of the critic's re-run of the discovery (`critic/binrun/out/manifest.txt`). Two runs of `mutants_e0k.py` gave byte-identical `mutations.txt` and `mutations.json`. The outputs are
in text **and** JSON (36 JSON files in the manifest, plus `mutations.json`: `arrows-*.json`, `arrows2-*.json`, `crate-*.json`, `walker-*.json`, `carry-*.json`, `drift-*`, `muruta-*`, `mutations.json`).

## Tools

The tools are byte copies of the discovery (SHA-1 in `oracle/checksums.txt`, with the origin of each: "= path" is the same bytes as a file of an earlier annex, "NEW" has no earlier copy of that name,
"DIFFERENT" has other bytes than the earlier copies of that name; `cmp` at the copy against the discovery folders `bin/` and `critic/` of the session scratchpad of 2026-10-15). Of the 75 python files, 39 are
byte-identical to files of earlier annexes (the machine: `bank.py`, `c1world.py`, `pworld.py`, `world.py`...), 34 are new (the `k0_*.py` scenarios, `k0lib.py`, `exe.py`, `scripts/*`, the three critic scripts,
`mutants_e0k.py`), and 2 have the name of an earlier file with other bytes: `oracle/lib.py` (the capstone path of the user site) and `run_all.py` (the E20.e0k driver). `mutants_e0k.py` is new, written for this
slice (T0); it is the only file that is not a discovery file.

## Vectors (the keys the tests read) - positions and forces are raw 16.16 integers; `pz` is the binary's frame (the DLL's frame is this minus one raw unit, ADR-0026)

| Vector | File of `oracle/out/` | Key (JSON: `rows[].slots["<slot>"].<field>`; text: the same columns) | Value |
|---|---|---|---|
| Fire ball (11) born at Z + 32 px, flat field | `arrows-ball-fire.json` / `.txt` | `pz` per tick, `ground_144`, `collz_140` | 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5 px at ticks 1 to 10, then 0 at tick 11 with `ground_144` = 1 and `collz_140` = 1 |
| Same ball, step of 16 px (height byte 1, columns 6 to 11) | `arrows-ball-step16.json` | `pz`, `adj_13C`, `ground_144` | lands on the step at tick 8 |
| Same ball, step of 32 px (height byte 2) | `arrows-ball-step32.json` | `adj_13C`, `pz` | `ForceAdjusted` at tick 7, falls to the ground at tick 11 |
| Arrow (3), Z + 16 px, wall column 15 (mask `0x40`, height 0) | `arrows-arrow-wall-xy.json`, `arrows2-summary.txt` (C1) | `adj_13C`, `px` | X = 349 px exactly (22 872 064 raw); `ForceAdjusted` at tick 43 or 44 according to the start |
| Arrow, step of 16 px / of 32 px (columns 15 to 29) | `arrows-arrow-h1.json`, `arrows-arrow-h2.json` | `px`, `adj_13C`, `pz` | goes over the 16 px step (Z + 16 px), stops against the 32 px step at tick 43; Z constant |
| Arrow, map edge from x = 1200 | `arrows2-map-edge.json`, `arrows2-summary.txt` (C3) | `adj_13C`, `px` | tick 7, X = 1 237.0 px, flag the same tick (bound `0x4E00000 - ((offX + sizeX) << 16)`) |
| Arrow leaving a 32 px plateau | `arrows2-plateau-drop.json` (C4) | `pz` | constant for the whole flight (no gravity), `ground_144` = 0 |
| Arrow, slide forbidden / allowed (`0x2000`) | `arrows2-slide-forbidden.json`, `arrows2-slide-allowed.json` (C2) | `adj_13C`, `status`, `px`, `py` | forbidden: `ForceAdjusted` at tick 43, dead; allowed: slides, X = 493, Y = 104 |
| Spawn heights | `arrows2-spawn-height0.json`, `-height-1.json`, `-height-20.json` (C6) | `pz` at tick 0 and per tick | hero Z: raw 2; hero Z - 1 raw and 20 px below the ground: raw 1 (T + 1) |
| Thrown crate (map 392, sprite 36, impulse 1 024 from 32 px) | `crate-flat-state1.json` ..., `crate-summary.txt` | `pz`, `collz_140`, `adj_13C` | lands at tick 23 flat, 21 on a 16 px step, 18 on 32 px; wall or 48 px step: `ForceAdjusted` at tick 4 |
| Walking monster (Beannoid, map 14, sprite 79) | `walker-*.json`, `walker-summary.txt` | `pz`, `terr_138`, `ground_144`, `adj_13C` | ramp descents, plateau drop, hop on flat ground and onto a 16 px step, pit cell |
| Header words | `header-summary.txt`, `census_header.txt` | `Gravity`, `ZViscosity`, byte `+8` | 481 maps at (128, 4096, 0); maps 159 and 160 at (3, 256, 1) |
| Fuzz of the horizontal step | `xyfuzz-summary.txt` | mismatches | 20 000 cases, 0 mismatches (`ForceAdjusted` expected in 2 358) |
| Halving residual | `residual_stats.txt` | flag tick minus the first tick whose full step no longer fits | random raw gaps (20 000, 6 px per tick): +1 tick in 13 359 cases (66.8 %), +2 in 6 641 (33.2 %), never 0; the integer-pixel start family of C1 (10 starts): +1 six times, +2 four times |

Note on a number of the plan: the T2 probe P4 of the plan speaks of "the 12 starts of the oracle (C1)"; the family C1 of `arrows2-summary.txt` has **10** starts (the plan's own vector line says "6 fois +1 et
4 fois +2 sur 10 départs"). The probe P4 runs these 10 starts.

## Mutants of the oracle (T0 acceptance: at least 12, each killed or proven equivalent in writing)

`mutants_e0k.py`: 18 real patches of one word of the emulated binary, applied to RAM right after the world is built (before the spawn), never simulated; each runs the same 11 scenarios (S1 wall, S2 step32,
S3 ball, S4 ballstep, S5 slide, S6 edge, S7 spawn, S8 marker, S9 ceiling, S10 magnet, S11 hop) as its control; KILLED means at least one scenario key differs from the control's. **18 of 18 KILLED, none
equivalent** (`out/mutations.txt` lists the control and the mutant value of every killing scenario). A first run left four survivors (M04, M05, M09, M17 at its first address): the scenarios injected the marker
and the ceiling by a poke that the animation start overwrites; they were killed by writing the 0x8000 impulse into the real animation record, and by starting a hop at 1 905 px; M17 had the wrong address (a
different branch) and was moved to the landing site `0x800376F4` (the old address became M18).

| Mutant | Address | Patch | Killed by |
|---|---|---|---|
| M01 | `0x80037440` | `slt v0,v0,t0` to `slt v0,t0,v0` (height test of the non-hero blocking function `0x800373E4` reversed) | S1 S2 S4 S5 S6 S10 |
| M02 | `0x80037430` | `bnez v0` to `nop` (the cell mask no longer blocks) | S1 S5 |
| M03 | `0x800373F4` | mask word `0x40` to `0x80` | S1 S5 |
| M04 | `0x80036AE0` | gravity test `0x100` to `0x200` inside the `0x8000`-marker branch | S8 |
| M05 | `0x80036860` | marker constant `0x8000` to `0x8001` | S8 |
| M06 | `0x80036B10` | gravity test `0x100` to `0x200` in the force law | S3 S4 S8 S9 S10 S11 |
| M07 | `0x80036C24` | landing `slt` operands swapped (`T < ModZ + F`) | ten of the eleven |
| M08 | `0x80036C30` | landing height `T + 1` to `T` | S3 S4 |
| M09 | `0x80036DC4` | ceiling `lui v0,0x780` to `0x700` | S9 |
| M10 | `0x80039EA4` | the `+1` of the creation | S3 S4 S7 |
| M11 | `0x800379C4` | the `NoObstacleSlide` test `0x2000` to `0x1000` | S5 |
| M12 | `0x8003786C` | magnet window 3 px to 2 px | S10 |
| M13 | `0x80036788` | clipping bound comparison reversed (`0x800366FC`) | S1 S2 S4 S5 S6 S10 |
| M14 | `0x80037850` | `bnez v0` to `nop` (magnet also with `ForceZ` non zero) | S4 S10 |
| M15 | `0x80036B48` | `+-(V << 8)` clamp comparison reversed | S3 S4 S9 S10 S11 |
| M16 | `0x80036B30` | gravity term added instead of subtracted | S3 S4 S8 S9 S10 S11 |
| M17 | `0x800376F4` | gravity test after a landing (`ForceZ` cleared) `0x100` to `0x200` | S8 S10 S11 |
| M18 | `0x80037674` | gravity test after a blocked rise `0x100` to `0x200` | S9 |

## Counts at the starting HEAD `9d5f5f2` (after E20.f3a and E20.f3b)

`dotnet test Alundra.Tests/Alundra.Tests.csproj` without filter, `--no-build --blame-hang-timeout 300s`: **4 886 passed, 0 failed, 0 skipped, in Release and in Debug** (the plan quoted 4 852 after
E20.e0b1; the difference of 34 comes from the slices between, not attributed here). `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` after the Debug build: identical. The five hero traces are unchanged
(`git diff --ignore-cr-at-eol --exit-code`; the four `hero-trace-389-*` files are rewritten with line-ending noise by the suite and restored).

## The recounted predicted list of tests that can move (written before any code; replaces the "56 methods of six files" of the discovery for e0k1, e0k2 and e0k3)

The tests that can reach `AlundraWorldProxy.SpawnEntityBySpriteIndex` or `AlundraEntitySpawnFactory.CreateEntityFromSpriteIndex`, directly or through a support, found by `rg` over `Alundra.Tests/` for
`SpawnEntityBySpriteIndex`, `CreateEntityFromSpriteIndex`, `BySpriteIndex`, `SpawnWarp`, `WiredWeaponTypesForTests`, `WeaponSpawnBirthHookForTests`, `SpawnWeaponProjectile`, `WeaponSpawnCall`, and then read.
The production set of wired weapon types is empty (`AlundraPlayerManager.ProductionWiredWeaponTypes`), so the shot of the hero reaches the interface only in the tests that wire a type
(`WiredWeaponTypesForTests`, set by `AlundraWeaponSpawnSeamTests` only). `AlundraHeroAttackTests`, `AlundraHeroChargeTests` and `AlundraShotDirectionTests` mention the shot seam but never wire a type (the hook
replaces the production call): they do not reach it. The list is closed by file; a test of these files that never reaches the interface (record-path facts, data facts) is still counted, which only makes the
bound larger. The cases are those listed by `dotnet test --list-tests` (a theory counts one case per data row).

| File (`Alundra.Tests/`) | Class | Methods | Cases |
|---|---|---|---|
| `AlundraBirthTickProbePaTests.cs` | `AlundraBirthTickProbePaTests` | 5 | 5 |
| `AlundraBirthTickTests.cs` | `AlundraBirthTickTests` | 11 (one theory of 8 rows) | 18 |
| `AlundraEntitySlotV5RowsTests.cs` | `AlundraEntitySlotV5RowsTests` | 3 | 3 |
| `AlundraEntitySlotWorldTests.cs` | `AlundraEntitySlotWorldTests` | 14 (one theory of 2 rows) | 15 |
| `AlundraSpawnBySpriteIndexInterfaceTests.cs` | `AlundraSpawnBySpriteIndexInterfaceTests` | 11 | 11 |
| `AlundraSpawnBySpriteIndexTests.cs` | `AlundraSpawnBySpriteIndexCoreTests` | 12 | 12 |
| `AlundraWeaponSpawnSeamTests.cs` | `AlundraWeaponSpawnSeamTests` | 6 | 6 |
| **Total** | 7 classes | **62** | **70** |

(`AlundraSpawnIndexOracle.cs` is a data helper, not a test class.) The discovery counted 56 methods in six files; the difference is `AlundraWeaponSpawnSeamTests` (the 6 methods of E20.f3a). The tests of E20.f3b
(`CreateHitAttachedEffect` through the effect stage) and E20.f3a's other tests do not create an entity by sprite index. The tests that run entities **from a record** through `ApplySpawnInitialization`
(the phase A extraction touches that method) are the whole rest of the suite: they are guarded by "the suite does not move" and by the mutant "one line lost from the record path", not by this list.
The names of the 70 cases are in the plan results of E20.e0k1 and can be recomputed with `dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --list-tests`.

## The probes of T2 (predictions written before any probe is run)

| Probe | Prediction at the starting HEAD (before the phase B wiring) |
|---|---|
| P1 - fire ball (11) born at Z + 32 px on a flat field | the ball stays at 32 px (gravity word 0 so `ForceZ` is bounded to 0 every tick); after the state, Z equals the oracle minus one raw unit until the landing |
| P2 - arrow (3) against the wall column 15 | without the mask the arrow crosses the wall; with it, it stops at X = 349 |
| P3 - arrow at Z + 16 px above a 16 px step | `IsOnGround` = 1 at the first step (the raw unit is lost through the root): the red test of e0k2 |
| P4 - the tick of the wall flag over the 10 integer starts of C1 | always "+1" (the DLL rule of E19.a3); the count of one-tick gaps with the oracle (4 of 10 are "+2" in the oracle) feeds Q-K2 |
| P5 - rising entity (impulse 1 024) with and without the vertical ownership | cancelled by the engine without it, not cancelled with it |
| P6 - gap between the root and the logical Z of a ball finishing its fall | the engine anchors the root to the logical Z within 4 px |

The results of the probes are appended below by T2 (before the phase B wiring).

## Results of the probes at the phase A commit `b4f3938` (the starting behaviour of an entity born by the interface; T2, first half)

Throwaway tests (never committed), world of map 14 (`ArcRun`, real prefabs and controllers) over a synthetic field of cells. Each probe ran twice: **A0** as the interface creates the entity (the starting behaviour) and **A1** with
the map state applied by hand right after the birth (`ApplyMapDependentState`, a rehearsal of the one wiring line of phase B). Raw outputs: the session scratch folder (`probes_out_phaseA.txt`). Positions in px, raw 16.16 in brackets.

| Probe | Prediction | Measured | Verdict |
|---|---|---|---|
| P1 - ball (11), Z + 32 px, flat | A0: stays at 32 px | A0: `PosZ` 2 097 153 at birth, 2 097 152 from tick 1 to tick 16 (X flies on at 6 px per tick, `IsOnGround` 0, `ForceZ` 0) | confirmed |
| P1 with the state | Z = oracle minus one raw unit until the landing | A1: 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5 px at ticks 1 to 10, 0 at tick 11 with `IsOnGround` 1, then status 3: the shape of the oracle. In raw units the DLL is the oracle **minus two** during the fall (2 064 384 against 2 064 386 at tick 1, ..., 294 912 against 294 914 at tick 10: difference 2 at ticks 1 to 10) and minus one at rest (0 against 1 at tick 11) | **CONTRADICTED** (two units, not one, while falling) |
| P2 - arrow (3), wall column 15 (mask `0x40`, height 0) | A0 crosses; A1 stops at X = 349 | A0: X = 517 at tick 70 (a start at 100), status 2, `ForceAdjusted` never raised. A1: X = 349.0 exactly (22 872 064) for the six distinct starts, status 3 | confirmed |
| P3 - arrow (3) born at 16 px above a field flat at 16 px | `IsOnGround` 1 from the first step | A0 and A1 the same: `PosZ` 1 048 577 at birth, 1 048 576 from tick 1, `IsOnGround` 1 from tick 1 (the binary has the floor plus 2 raw units and never lands) | confirmed; the red test of e0k2 |
| P4 - tick of the wall flag over the ten starts of C1 | always "+1" | A1: the flag is raised at tick **43** for the ten starts, the arrow having reached 349.0 at tick 42 by a shortened step (flag = arrival + 1, always). The fractions of the starts (12 345, 1, 40 000, 65 535) are lost by the root (X is pulled back in whole pixels): the ten starts are six integer starts. The oracle flags at 43 for the starts 100, 102, 103, 105 and 100 + 12 345/65 536, and at 44 for 101, 104, 100 + 1, 100 + 40 000 and 101 + 65 535 | confirmed ("+1" always); gap of one tick against the oracle in **5 of the 10** starts as given (2 of the 6 integer starts: 101 and 104) |
| P5 - rising entity (the bomb, global 2, flags `0x13234E`, animation 2, impulse 1 024; no entity born by the interface carries it as animation 0, the Beannoid hop of the plan's wording is reset to animation 2 at once) | cancelled without the vertical ownership, not with it | A0 (`IsVerticalOwnedExternally` false): `PosZ` 4 px at tick 1 (`ForceZ` 262 144) then **0 and `IsOnGround` 1 at tick 2**: the rise is cancelled. A1 (owned): 4, 7.5, 10.5, 13, 15, 16.5, 17.5, 18, 18 px, then the descent, landing at tick 17 | confirmed |
| P6 - gap between the root and the logical Z of a ball finishing its fall | the engine anchors the root within 4 px | `PosZ` px minus the root Z is **0.0000 at each of the 30 ticks**, in A0 and in A1: the logical Z is pulled from the root every tick, so there is nothing to anchor (the first push to the root truncates to whole pixels, which is the lost raw unit of P1) | not decidable here (no gap exists before e0k2); not contradicted |

The contradiction of P1 is explained by the root, not by the state: the creation pushes the position to the root in whole pixels (`PushLogicalPositionToRoot`, `>> 16`), the first tick pulls `PosZ` back from the root, and the
raw unit of the creation is lost at once (2 097 153 becomes 2 097 152 before the first fall step). This is exactly the defect of D-F3-8 and D-E0K-4 (the logical Z), which E20.e0k2 fixes; E20.e0k1 does not claim the raw
unit. The slice's tests (k2) and (k4) do not pin it (they compare the landing tick and the rest height). The decision the stop asks for, before E20.e0k2: keep the prediction of the plan for e0k2's own tests (the series
must then equal the oracle **minus one** raw unit, which only the logical Z gives), and record that after e0k1 alone the series is the oracle **minus two**.

## Phase A results

`ApplyMapDependentState` extracted from `ApplySpawnInitialization` (same lines, same order), optional `TileMapData? tileMapData = null` on `CreateEntityFromSpriteIndex`, not passed by `SpawnEntityBySpriteIndex`
(commit `b4f3938`). Suite 4 894 of 4 894 in Release and in Debug (4 886 + 8 new of `AlundraMapStateTests`), no existing test moved, `cmp` of `Alundra.dll` identical, the five traces unchanged. The tests of (a) were red on
the skeleton (the call commented out: 4 red for the missing state; (b) and the P3 pin green, as by construction), M3 killed by the P3 pin.

## The world tests (k1) to (k4), written before the wiring (not committed; they are committed with phase B)

`Alundra.Tests/AlundraMapStateWorldTests.cs`, run on the phase A code (the interface does not pass the map): **4 red, each for the stated reason**.

| Test | Red before B because | Value seen |
|---|---|---|
| K1 | the arrow crosses the wall that only the mask blocks | max X 29 949 952 (457 px), no `ForceAdjusted`; expected 22 872 064 (349 px) and the flag |
| K2 | the ball never lands | first landing tick 0 (expected the oracle's 11), `PosZ` stays 2 097 152; the "no map" witness is the same |
| K3 | the ball does not fall on map 14 either | `PosZ` stays at 32 px on both maps (expected 0 on map 14 and a drop of less than a pixel on map 159) |
| K4 | the entity born by the interface has the defaults | (0, 0, 0, false, 0.001) against (128, 4096, 0x40, true, 0) for the record |

Rehearsal (not kept): with the one wiring line `..., CollisionField, _tileMapData);` in `SpawnEntityBySpriteIndex` the four tests are green; the line was reverted (`git checkout`).

## Results of the probes after the phase B wiring (commit `81b68b5`; T2, second half)

The same throwaway probes (never committed, removed) rerun on the phase B code. A0 is now the entity as the interface creates it, WITH the map state; A1 is the manual rehearsal on top of it (a second call of the idempotent helper). Compared line by line with the
phase A rehearsal (A1 of the first half): **P1, P2/P4, P3, P5 and P6 are identical** (A0 of phase B = A1 of phase A, and A1 of phase B = A1 of phase A). No new contradiction.

| Probe | Before B (A0 of phase A) | After B | Against the prediction |
|---|---|---|---|
| P1 - ball (11), Z + 32 px, flat | stays at 32 px | 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5 px at ticks 1 to 10, 0 with `IsOnGround` 1 at tick 11; raw: the oracle minus TWO units while falling (2 064 384 against 2 064 386 at tick 1), minus one at rest | prediction "oracle minus one" contradicted (already recorded in the first half): the root truncates the position to the pixel at birth; e0k2 (logical Z) |
| P2 - arrow (3), wall column 15 | crosses (X = 517 at tick 70) | stops at X = 349.0 (22 872 064) for the six distinct whole-pixel starts, status 3 | confirmed |
| P3 - arrow, Z + 16 px above a 16 px field | `IsOnGround` 1 from tick 1 (`PosZ` 1 048 577 then 1 048 576) | unchanged | confirmed; the red test of e0k2 |
| P4 - wall flag over the ten starts of C1 | no flag (crosses) | flag at tick 43 for the ten starts, the arrow at 349.0 at tick 42 (flag = arrival + 1); oracle 43 for 100, 102, 103, 105 and 100+12 345, 44 for 101, 104, 100+1, 100+40 000, 101+65 535: one-tick gap on 5 of the 10 starts as given (2 of the 6 whole-pixel starts, 101 and 104) | confirmed ("+1" always); feeds Q-K2 |
| P5 - bomb (global 2, impulse 1 024) | rise cancelled: 4 px at tick 1, then 0 and `IsOnGround` 1 at tick 2 | 4, 7.5, 10.5, 13, 15, 16.5, 17.5, 18, 18 px, then the descent, landing at tick 17; `IsVerticalOwnedExternally` true | confirmed |
| P6 - gap root / logical Z of a ball finishing its fall | 0.0000 at each of 30 ticks | 0.0000 at each of 30 ticks | not decidable before e0k2 (the logical Z is pulled from the root) |

## Removal proof, mutants and counts (T3 and T4)

- Suite after the wiring: 4 898 of 4 898 in Release and in Debug (4 894 + 4 world tests); no existing test moved; `cmp` of `Alundra.dll` identical after the Debug build; the five traces unchanged.
- Removal proof: with the wiring line removed (neutralised DLL copy), the full suite in Release fails EXACTLY `AlundraMapStateWorldTests` K1, K2, K3, K4 (4 failures, 4 894 passed) and nothing else. File restored with `git checkout --`.
- DLL mutants (real patches by script, `AlundraMapState*` tests, each restored): MA helper not called by the interface (8 red), MB `_tileMapData` null (K1 to K4), MC mask on the wrong flags, MD vertical ownership forgotten, ME `MinMoveDistance` forgotten, MF gravity and viscosity words swapped (10 red), MH call lost from the record path,
  MI walk-mask line lost from the helper, M3 (the D-E0A-8 guard of `TickScriptedNpc` reduced to the marker alone: exactly one red, the P3 pin of e0a3b). MG (helper called twice) survives and is equivalent: the helper only assigns values that depend on the map and the flags (no read-modify-write).

## E20.e0k2 - T0: the predicted list recounted at the starting HEAD `5c5f70a`, and the oracle of the magnet

Written before any code of E20.e0k2 (task T0 of subsection 1.18.2 of `docs/plan-e20-heros.md`).

### Counts at the starting HEAD `5c5f70a` (after E20.e0k1, ADR-0068)

`dotnet test Alundra.Tests/Alundra.Tests.csproj -c <config> --no-build --blame-hang-timeout 300s`, no filter: **4 898 passed, 0 failed, 0 skipped, in Release and in Debug**, as the plan states. `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`
after the Debug build: identical. The five traces unchanged (`git diff --ignore-cr-at-eol --exit-code`; the four `hero-trace-389-*` files are rewritten with line-ending noise by the suite and restored).

### The recounted predicted list of tests that can move (replaces the list of E20.e0k1 for the acceptance and the stops of E20.e0k2)

Same method as for E20.e0k1 (`rg` over `Alundra.Tests/` for `SpawnEntityBySpriteIndex`, `CreateEntityFromSpriteIndex`, `BySpriteIndex`, `SpawnWarp`, `WiredWeaponTypesForTests`, `WeaponSpawnBirthHookForTests`, `SpawnWeaponProjectile`,
`WeaponSpawnCall`, then read; the cases are those of `dotnet test --list-tests`, a theory counting one case per data row). The files found: the seven of the list of E20.e0k1 and the two that E20.e0k1 added (`AlundraMapStateTests`, `AlundraMapStateWorldTests`);
`AlundraHeroAttackTests`, `AlundraHeroChargeTests` and `AlundraShotDirectionTests` mention the shot seam but never wire a type (they do not reach the interface), and `AlundraSpawnIndexOracle.cs` is a data helper, not a test class.

| File (`Alundra.Tests/`) | Class | Methods | Cases |
|---|---|---|---|
| `AlundraBirthTickProbePaTests.cs` | `AlundraBirthTickProbePaTests` | 5 | 5 |
| `AlundraBirthTickTests.cs` | `AlundraBirthTickTests` | 11 (one theory of 8 rows) | 18 |
| `AlundraEntitySlotV5RowsTests.cs` | `AlundraEntitySlotV5RowsTests` | 3 | 3 |
| `AlundraEntitySlotWorldTests.cs` | `AlundraEntitySlotWorldTests` | 14 (one theory of 2 rows) | 15 |
| `AlundraSpawnBySpriteIndexInterfaceTests.cs` | `AlundraSpawnBySpriteIndexInterfaceTests` | 11 | 11 |
| `AlundraSpawnBySpriteIndexTests.cs` | `AlundraSpawnBySpriteIndexCoreTests` | 12 | 12 |
| `AlundraWeaponSpawnSeamTests.cs` | `AlundraWeaponSpawnSeamTests` | 6 | 6 |
| `AlundraMapStateTests.cs` | `AlundraMapStateTests` (E20.e0k1) | 4 (two theories, 3 and 2 rows) | 8 |
| `AlundraMapStateWorldTests.cs` | `AlundraMapStateWorldTests` (E20.e0k1, K1 to K4) | 4 | 4 |
| **Total** | 9 classes | **70** | **82** |

The list of E20.e0k1 had 7 classes, 62 methods and 70 cases; the difference is the two classes of E20.e0k1 (8 methods, 12 cases). As for E20.e0k1, the tests that run entities **from a record** are the whole rest of the suite: they are guarded by
"the suite does not move" and by the mutants, not by this list. The names are recomputed with `dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --list-tests`.

### The oracle of the magnet (`k0_magnet.py`, keys `magnet-3px` and `magnet-4px`)

Run from a scratch copy of the oracle, twice (`python -B -I run_all.py` after deleting nothing but the prefixed outputs, as the driver does): the two manifests are **identical** (93 files: the 88 of E20.e0k1, unchanged byte for byte, and the 5 new ones:
`magnet-3px.txt/.json`, `magnet-4px.txt/.json`, `magnet-summary.txt`); the log of the new module is `out/log-k0_magnet.txt`. The driver `run_all.py` has the new module and the prefix `magnet` (`checksums.txt` updated). The existing scenarios
`walker-ramp-up` (E3) and `walker-ramp-down` (E2) of `k0_walker.py` are the witnesses; the mutants M12 (magnet window 3 px to 2 px) and M14 (magnet also with `ForceZ != 0`) of `mutants_e0k.py` are the teeth.

The scenario: the Beannoid of map 14 (sprite 79, ClassA, Gravity, the walker of E2 to E8), animation 7 (walk) with the speed of the animation set overridden to 400 by the harness (4.6875 px per tick, full step 307 200 raw), on the ascending ramp of E3
(cells 3 to 6: height byte 1 to 4, slope 3), at rest on the flat ground (T = 0) at tick 0, starting at x = 61 (`magnet-3px`) and x = 62 (`magnet-4px`). Raw 16.16 integers, `pz` in the binary's frame (the DLL's frame is this minus one raw unit, ADR-0026).

| Key | Tick 1 | Tick 2 |
|---|---|---|
| `magnet-3px` (`out/magnet-3px.json`, `rows[0].slots[<slot>]`) | displacement 307 200 (the full step, accepted whole), `pz` 196 609 = `terr_138` 196 608 + 1 (3 px higher), `ground_144` 1, `adj_13C` 0 | displacement 307 200, `pz` 393 217, `terr_138` 393 216 |
| `magnet-4px` (`out/magnet-4px.json`) | displacement **307 196** (4 raw short of the full step), `pz` 262 145 = `terr_138` 262 144 + 1 (4 px higher), `ground_144` 1, `adj_13C` 0 | displacement 307 200, `pz` 458 753, `terr_138` 458 752 |

What the machine does at tick 1 of `magnet-4px` (a write trace of `+0x114`, `+0x11C`, `+0x138` by address of the writing instruction, throwaway): the first attempt (the full step, terrain 4 px under the new box) is **refused** (`0x80037938`
gives back X and `0x80037948` gives back Z: no magnet, the window is 3 px, then the blocking function), then the refinement by halves accepts sub-steps one after the other, and **each accepted sub-step poses the entity on its terrain** (`0x80037898`, Z = T + 1): the terrain of
the successive sub-steps is 2 px, 3 px, then 4 px, and the magnet compares the terrain of the attempt to the Z **left by the previous accepted attempt**, never to the Z of the start of the tick, so the staircase 2, 3, 4 px is climbed by steps of at most 3 px. The step ends 4 raw short and the entity is
on the terrain at the end of the same tick; `ForceAdjusted` is not raised (a sub-step was accepted). With a rise of 3 px (`magnet-3px`) the first attempt passes (the magnet poses the entity, the full step is accepted). The mutant M12 (window 2 px) makes `magnet-3px` take the 4-raw-short step of the other
(checked, throwaway): the keys have teeth.

**Discrepancy with the plan, to be read by whoever reads the results of E20.e0k2:** the plan's wording of (z9) says the entity facing a terrain 4 px higher is "not posed". The oracle says the full step is refused and the **step is shortened by 4 raw units**, the entity then being on the terrain at the end of the
tick anyway (by the sub-steps). The observable difference between 3 px and 4 px is the **displacement of tick 1** (full against 4 raw short), not the height at the end of the tick. The test (z9) as written in `Alundra.Tests/AlundraLogicalZWorldTests.cs` pins the keys as they are (see the results of phase A in the plan).

### Addendum to the oracle of the magnet: the descending side (keys `magnet-down-3px` and `magnet-down-4px`), written at the red run of the world tests of E20.e0k2

Why. The red run of the world tests (task T2) showed that the ascending keys above cannot make a world test red before phase B: with the map state of E20.e0k1 the engine already steps up a terrain at most `StepHeight` (3 px, the value of the prefab)
above the foot and ends the tick on it (a probe of the DLL, throwaway: Beannoid born by the interface, speed 400, `x0` 61 gives displacement 307 200 and `PosZ` = 3 px = the oracle minus one raw unit, exactly the oracle; `x0` 62 gives displacement 262 144 and 3 px, where the oracle gives 307 196 and 4 px,
which no engine mover reproduces: the engine shortens to a whole pixel). So the magnet of 3 px is not observable on the ascending side. It is on the descending side, where the engine's ground snap (4 px, `GroundSnapDistance`) differs from the binary's window (3 px).

The scenario (`k0_magnet.py`, run with the rest by `run_all.py`): the same Beannoid (anim 7, speed overridden by the harness) walking east from x = 40 down the descending ramp of `k0_walker.py` E2 (cells 0..2 at height byte 4, cells 3..6 height byte 4..1 with slope 2, flat 0 after),
40 ticks, the two interpreters identical (`out/magnet-down-*.txt` and `.json`, `meta.init.pz` = 4 194 305 = 64 px + 1 raw).

| Key | Speed | What the machine does |
|---|---|---|
| `magnet-down-3px` | 320 (3.75 px per tick, the ground drops by 2 to 3 px per tick: inside the window) | `ground_144` = 1 at every one of the 40 ticks (no tick in the air): the magnet poses the entity on the lower terrain at every tick |
| `magnet-down-4px` | 480 (5.625 px per tick, the ground drops by 3 to 4 px per tick: outside the window) | `ground_144` = 0 at ticks 10 to 24 (15 ticks in the air: the ground drops faster than the fall); `pz` at tick 10 equal to tick 9 (3 866 625), then falling by 32 768, 65 536, 98 304, ... raw per tick (the gravity step accumulating) |

The manifest of `run_all.py` is now 97 files (the 88 of E20.e0k1, the 5 of the first part of this section, 4 new ones; `magnet-summary.txt` has the two new lines), identical on two complete runs.

## E20.e0k2 - phase B (T3) and T4: wirings, the one moved pin, the removal proof, the mutants

- Commit `82a979d`: the four wirings of the logical Z (W1 two calls around the XY step, W2 the support block yields, W3 the root pull leaves `PosZ` and `IsOnGround`, W4 the step height rule, the sentinel after a `Teleport` and at a thaw). The 18 cases of (z1) to (z10) are green at 1, 2 and 3 ticks per frame. Suite: 4 925 of 4 925 in Release and in Debug (4 907 + 18).
- Predicted list (70 methods, 82 cases, T0) against the measured moves: exactly one existing test moved, `AlundraMapStateWorldTests.K2` (in the list). Its "no map" witness was the whole pixel `32 << 16` (the root's truncation); it is now the oracle's `init.pz` (`arrows-ball-fire`) minus one raw unit (derivation, D-E0K-10).
- Root / logical Z gap, measured after phase B: (z6) under 0.5 raw unit at 1, 2 and 3 ticks per frame and no displacement of the root by an engine update without a tick; the P6 probe resumed (ball until its landing, 30 ticks, an engine update without tick after every frame): gap 0, engine displacement 0, `PosZ` 2 064 385 at tick 1 and 294 913 at tick 10 (the oracle minus one), 0 with `IsOnGround` 1 at tick 11.
- Removal proof (the production files put back to the phase A commit `181954f`, neutralised DLL copy, full suite in Release): exactly 20 red, (z1) to (z10) (18 cases), (z11) and `AlundraMapStateWorldTests.K2`; nothing else. Restored; `git status` back to the working state.
- 22 real mutants (scripts in the session scratchpad, each restored), all killed; four survived the first pass and were closed by a stronger B6 (the order of the magnet and of the `IsOnGround` derivation, the shared entity's gravity flag left down by an earlier case) and by the world test (z11) (terrain height field, force of the landing tick, logical ground flag across tickless engine updates, thaw of an entity one raw unit above the floor).
- ADR-0069.
