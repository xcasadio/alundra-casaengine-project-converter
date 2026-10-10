# E14.a1 - the six native handlers of the Muruta and the Beannoide, E1 and DestroyEntity, all real code (2026-10-10)

Purpose: reference values for the ports of E14.a1b (Beannoide A1, C1, D1, type 335; the break effect of `DestroyEntity`) and
E14.a1c (Muruta A0, C10, D9, type 347), plan `docs/plan-e14-ia-native.md` sections 1.3b and 1.3c. The handlers run as **real code of
`ALUN_CD.EXE` (France)** in the emulated machine of the E20 oracles, on single-call vectors and on 21 scenarios.
Ground truth: PS-X EXE, text at `0x80020000`, file offset `0x800 + addr - 0x80020000`. Labels: **FACT** (decoded instruction words
or a measurement, with where), **INFERENCE**, **UNKNOWN**.

Produced by the read-only discovery of 2026-10-10 in the session scratchpad, re-run from a third directory with an identical manifest,
then copied here unchanged (E14.a1b T0). `tools/` is byte-identical to `docs/plan-e20-annexe/e20-c2/oracle/tools/`; `hworld.py` is
byte-identical to the c2 one and is imported only for `htick`, the gate-faithful dispatcher.

## 1. How to run

`python -B docs/plan-e14-annexe/e14a/oracle-a1/run_e14.py` from the repository root (about 500 to 560 s; exit code 0 means every
verdict of `out/summary.txt` is PASS). Options: `--reuse-d1`, `--only name,name`. Needs Python 3 with capstone, the disc EXE
`D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` and its `DATA/BALANCE.BIN`, and the map
JSONs of `data-extracted/data/` (read only). Nothing outside this folder is written. Two runs give the same `out/manifest.txt`
(74 files, md5; no clock, no host path in the written files).

## 2. Files

| File | Content |
|---|---|
| `e14world.py` | machine with per-call recording; selective hooks (7 live handlers); real cells and map constants; wipe template; spawn with a drop stub; `0x2C` stand-in; RNG helpers; `FIELDS` (the 53 decoded entity fields) |
| `e14run.py` | runner, events, the `Swordsman` policy |
| `e14scen.py` | the 21 scenarios |
| `e14out.py` | readers and writers |
| `e14model.py`, `e14check.py` | independent model and its check |
| `e14vec.py` | single-call vectors (`e1_vectors`: 9 contents x 2 `ae` x 2 `plat` x 4 = 144 per species) |
| `e14cov.py` | instruction coverage |
| `run_e14.py` | everything, in order |
| `aux-tools/dis1.py`, `aux-tools/findjal.py` | disassembler and call-site finder (folder renamed from `aux`, a reserved device name on Windows that git cannot index) |

`out/` (74 files, about 24 MB of text, read by the C# tests): `d1/` (the 19 D1-gate files, equal to E20.c2's, plus
`run_all.stdout.txt`); `<scenario>.txt` and `<scenario>.json` for the 21 scenarios; `vectors-beannoide.json`, `vectors-muruta.json`,
`vectors.txt`; `timeline.txt`, `arena.txt`, `h3-equivalence.txt`, `diff2.txt`, `model-scenarios.txt`, `inputs-previous-row.txt`,
`coverage.txt`, `read-sets.txt`, `mutations.txt`, `summary.txt`, `manifest.txt`.

## 3. Real, stubbed, harness

- **Real** (emu.py, cross-checked on mips.py): the eight entity passes and `htick` with the `[0x800DC4B8] & 0x48` gate; the spawner
  (`force = 1`, as opcode `0x2D` calls it) and `InitializeEntity`, including the loot roll; ComputeNewHp, the hit pass, MovePlayer,
  UpdateItemEffectState; A1, C1, D1, A0, C10, D9 and E1 with relpos, TryAttackPlayer2/Front, HandleAnimationDirection, StartWalk and
  GetDirectionToTarget; DestroyEntity and the loot function; the real search `0x8003C954` and the `0x2C` handler `0x8003DC84`.
- **Stubs** (journalled): sound; effects `0x8003BDD8` and `0x8003BE74`; the effects pass (so `+0x1B8` stays 0 in the scenarios);
  every other handler of the five tables; the bytecode runner `0x8004205C`. `0x80039F58` returns a zeroed dummy entity (`0x801E4000`),
  so the code after it in `0x80032B90` runs and sound `0x54` is journalled; the dummy is not in the entity table.
- **Harness writes**: program A runs at status 1; the hero is a ghost (`+0x1CC` poked to `0x78` before every tick) except in
  ARENA-H3-live; `0x2C` is read before each tick with the hero as owner (map events, then the entity block, as `UpdateWorld`
  `0x8002E100`/`0x8002E108`); the per-frame RNG advance (`0x8002BD14`-`0x8002BD34`) is OFF except in the `-adv` scenarios; the wipe
  template has `+0x48 = -1` (`0x8003B214`-`0x8003B21C`); junk is written to the ten words `0xB8`-`0xDC` to show which are touched.

## 4. Scenarios

Positions in px; a tile is 24 x 16 px; "flat" is the map-14 constants with all cells 0; default seed `0x12345678`.

| Scenario | World, records | Hero and inputs | Ticks |
|---|---|---|---|
| BN-dormant | flat, BN at (252,96) | hero (452,96), no pad | 150 |
| BN-wake-walk | same | hero (180,96) | 380 |
| BN-lunge | same | hero (204,96) | 460 |
| BN-walk-free | same | hero (452,96); before tick 2: anim 0, AI1 0 | 700 |
| BN-walk-blocked | map 116 cells, BN at (396,296) | hero (732,648); before tick 2: anim 1, dir 8, AI1 100 | 260 |
| BN-hurt | flat | hero (212,96), Square at 1 | 120 |
| BN-death | flat | Square at 1, 31, 61, 91, 121, hero put 40 px west before each; probe `0x2C [0]` | 200 |
| MU-calm | flat, Muruta at (252,96) | hero (652,96) | 600 |
| MU-alert-close / mid / far | same | hero (252,112), (252,160), (252,192) | 300 each |
| MU-lunge | same | hero (252,144); Square at 10, 40, ... | 300 |
| MU-hurt | same | hero (212,96), Square at 1 | 120 |
| MU-death | same | Square at 1 and 31; probe `[0]` | 100 |
| MU-hall-calm | map 115 cells, records 24-27, landed and native | hero (252,888); probes `[24..27]` | 300 |
| MU-hall-kill | same | `Swordsman` policy, 4 kills | 700 |
| BN-grate-kill | map 116, BN records 0 and 1 | hero (444,328); policy from tick 120; probes `[0]`, `[1]` | 900 |
| MU-death-adv, BN-death-adv | as MU-death and BN-death | frame RNG advance ON | 100, 200 |
| ARENA-H1-live, ARENA-H3-live | flat, BN | E20.c1 H1 and H3 inputs; H3 non-ghost | 40, 60 |

The `Swordsman` policy is deterministic: when the target is hittable (status 2, on ground, no collision tick counter, animation
without bit `0x40`), the hero is put 40, 34, 28 (grate: 22, 16) px west of it on a footprint of walkable cells at the target's
height, and presses Square, 25 ticks apart. Note for the DLL arc G116: the oracle's arrival (444,328) works here because the oracle
forces the spawn; the DLL applies the player-tile spawn zone at map load, so the DLL arc arrives at (84,328) (plan section 1.3b).

## 5. Facts the ports rely on

- **Death timeline** (FACT, `out/timeline.txt`, 11 kills): with the lethal hit written at tick H, D1/D9 at H+1, animation 6 at H+14,
  E1 at H+23 (status 3 to 4), slot wiped at H+24, `0x2C [N]` read before the entity block counts the entity at H+24 and sees 0 from
  H+25; identical with and without the per-frame RNG advance.
- **E1 journal, in order** (FACT): loot `spawn_39F58(0, 0, ContentsItemId + 0x1E, X, Y, Z)` then sound `0x54`; then the break effect
  `0x8003BE74(0, id, 0, entity ; 1, 0, 0, 0)` with id = sprite header `+0x1E` (8 for both sprites), skipped when 0. The loot is vetoed
  for items `0x26`, `0x51`, `0x52` when MpMax is 0. The spawn loot roll draws 1 or 2 RNG words per entity (the DLL has no such roll,
  E20.e0). `+0x1B8` is written by `UpdateActiveEffects` (`0x80038F5C`), stubbed here.
- **RNG draws per call**: A0, A1, D1, D9 and E1 draw 0; C1 draws 0, 1, 2 or 3; C10 draws 0, 1 or 2.
- **D calls of the scenarios**: the attacker's attack is `(attribute 0x81, power 1)` at latch 16 (`curattack_1C8 = 0x80144229`).
- **C10** reads the BSS word `[0x800DC5A0]` at `0x80068164` (map 0x99 test, dead branch for sprite 347, not ported).
- **Rows are valid call inputs**: over 6765 calls on ticks without a harness write, every decoded entity field at call time equals
  the row of tick t-1, except `+0x228` (the pick) and `status` (A 1 to 2, E to 3); the hero fields the handlers read also equal t-1
  (D-E14-6).
- **D-E14-5 is necessary**: with the natives live in the E20.c1 arena, H3 gets no contact in 60 ticks; behind the switch the inert setup
  equals E20.c1's H3.json on 46 rows x 88 columns.

## 6. Checks of the oracle (all PASS, `out/summary.txt`)

- Second interpreter: identical rows and final entity memory for the 21 scenarios; identical entity image, RNG word and journal on all
  6965 vectors.
- Independent model: 0 differences on 6815 scenario calls and 6965 vectors.
- Mutations (one-word patches against 5539 vectors plus one trace): D9 anim 2 to 3; C1 reach `slti` 3 to 4; C1 floor limit; C10 state 5
  flag `0x40` to `0x20`; HandleAnimationDirection `slt` to `sltu`; StartWalk threshold; DestroyEntity status 4 to 5; QUANT byte; C10
  tail mask `0x1C` to `0x1F`; control on the dead map-0x99 branch changes nothing.
- Coverage: A0, A1, C1, D1, D9, E1, DestroyEntity, relpos, StartWalk, HandleAnimationDirection, TryAttackPlayerFront at 100%; C10
  98.0% (only the dead branch missing); ComputeNewHp 36% (sword path; the rest is E20.c1b's oracle).
- D1 gate unchanged (S-A and S-B, 216 values each, 0 differences).

## 7. How the C# tests consume it, and the E1 projection

Format, field table, E1 projection (compared C1-C7, excluded X1-X6 with their reasons and counts) and the reader
`Alundra.Tests/AlundraEnemyHandlerOracle.cs`: plan section 1.3b. In short, scenario `<name>.json` holds the per-tick columns and
`calls[]` (`tick, handler, slot, letter, rng_in, rng_out, latch, v0, before, after, hero, changed, changed_words,
hero_changed_words, journal`); `vectors-*.json` holds `inputs, entity_before (27 fields), hero_before, hero_attack, attacker_attack,
latch, rng_in, rng_out, draws, changed, journal`. Read every number as a 64-bit integer (pointers and RNG words exceed `Int32`);
never compare `v0`; compare only `calls[].journal`, never the per-tick `journal`.

## 8. Limits

- The effects pass and `UpdateActiveEffects` are stubbed: `+0x1B8` is 0 in every scenario; vectors cross `ae` in {0, 1} by hand.
- `+0x28` (platform) is 0 in every scenario and in every vector except the 72 E1 `plat = 1` vectors per species: no enemy resting on an
  entity is exercised.
- The D vectors always have the hero as toucher; the "no attack" and "no sheet" branches of `ResolveBalanceTarget` rest on E20.c1b.
- C1 and D1 without a hero are not in the oracle.
- The loot dummy entity is not in the entity table; loot items and sound 84 are not ported before E20.e0.
