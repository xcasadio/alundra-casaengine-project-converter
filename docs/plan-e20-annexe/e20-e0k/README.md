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
