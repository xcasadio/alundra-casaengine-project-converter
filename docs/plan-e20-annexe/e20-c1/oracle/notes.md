# E20.c1 - the hit pass end to end, hero and real targets (2026-10-09)

Purpose: reference traces for the E20.c1 pins (hit pass, damage, the hit written at tick N and read at tick N+1), produced by running the **real code of `ALUN_CD.EXE` (France)** on
the D1 MovePlayer oracle (copied) plus real Beannoid targets. Ground truth: PS-X EXE, text at `0x80020000`, file offset `0x800 + addr - 0x80020000`. Labels: **FACT** (decoded
instruction words or a measurement, with where), **INFERENCE** (with its base), **UNKNOWN**. Everything lives in this folder; the originals (`e20-b/oracle`, `d2-oracle-coups`, `d1-oracle-moveplayer`),
the decompilation, `Alundra/`, `Alundra.Tests/`, the engine, `alundra-project/` and `data-extracted/` are untouched. `tools/` is a copy of the needed files of `e20-b/oracle/tools` (D1 tools, byte-identical except `interp2.py`: a stubbed `jalr` target is handled, see section 6). New files: `c1world.py`, `scen.py`, `sweep.py`,
`checks.py`, `datareport.py`, `run_c1.py`.

## 1. How to run

```
python -B docs/plan-e20-annexe/e20-c1/oracle/tools/run_c1.py      # about 8 minutes; exit code 0 = D1 PASS and every check below passed
```
Order: (1) D1 re-validation with this folder's copy (`tools/run_all.py` -> `out/d1/`, S-A and S-B equal to the annex, 216 values each, 0 differences); (2) the reach sweep
(`out/reach-sweep.txt`, 364 runs); (3) the scenarios (`out/<name>.txt` + `.json`); (4) the two-interpreter comparison (`out/diff2.txt`); (5) the mutation tests (`out/mutations.txt`);
(6) the load-delay check (`out/load-delay-check.txt`); (7) `out/data-boxes.txt`, `out/summary.txt` (key facts of every scenario, computed), `out/manifest.txt` (md5 of every file). Needs Python 3 with
`capstone`, the disc file `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` (+ `DATA/BALANCE.BIN`), `data-extracted/data/map_alundra.json`, `map_14.json`,
`BALANCE.BIN.json`. Always `python -B`. Two complete runs give byte-identical `out/` (the manifests are equal; no clock, no host path, no hash-ordered iteration in the output).

`out/<name>.txt` holds, per scenario: the hero table, one table per target (every column listed in the header of the file), the per-tick journal (RNG word, latched animation `0x80146DF8`, effects, sounds,
which program letter the events pass chose for each target), the ComputeNewHp experiments, and the key facts (hit ticks, sparks, clears, model-vs-real). `.json` has every value of every tick.

## 2. Real code and stubs

| Piece | Run as |
|---|---|
| Tick passes `0x80038634`, `0x800386D0` (events: `MovePlayer` `0x80031B50` first, then the program choice and dispatch for slots 1..), `0x80038998` (counters), `0x800384F4` (lists), `0x80038E18` (`UpdateAnimation` `0x80038AB4` on every active entity, over its own bank), `0x80038364` (physics), `0x80039300` (hits) | **real** (the hit pass is the real one: D1/E20.b stubbed it) |
| `ComputeNewHp` `0x8003A374`, `ResolveBalanceTarget` `0x8004464C`, `UpdateItemEffectState` `0x800307E8`, the spark routine `0x80039188` and the random state `0x80098708`, the hero's damage entry `0x80031A68` (inside `MovePlayer`) | **real** |
| Spawner `0x8003A1B8` -> `0x80039BD0`, `0x80039B28`, `0x80039AD0`, `InitializeEntity` `0x80039D04`, `0x8004201C`, `0x80044550`, `0x80032A40` -> `0x80032968` (spawn loot draw) | **real** (for the targets; the hero is placed as in D1) |
| Effects `0x8003BDD8` (spark: `(0, 9, 0, cx, cy, cz)`), `0x8003BE74` (effects 4/5 on a target), sound `0x800490FC`, spawn `0x80039F58`, effects pass `0x80038E84` | **stubs**, journalled (a zeroed dummy effect slot is returned, so the spark RNG draws happen) |
| **The targets' own programs** | **inert**, see below |

**Inert target (exactly).** The real events pass chooses a program letter per entity (its first loop writes it to entity `+0x228`) and its second loop calls the handler
`*(*(0x80098F4C + 4 * letter) + 4 * [entity + 0x70 + 4 * letter])`, or the map bytecode runner `0x8004205C` when `[entity + 0x4C + 4 * letter] & 0x7F != 0`. Here **every non-null handler pointer
of the five tables (201 addresses) and `0x8004205C` are hooked with one journalling no-op** (returns without touching the entity). So the letter chosen is the binary's (C every tick, D after a touch),
but nothing runs: the Beannoid neither moves nor changes animation nor reacts to a hit. Its handlers are A[1] `0x80061758`, C[1] `0x80065ED4`, D[1] `0x8007D9A4`, E[1] `0x8007ED10`, F none
(sprite header bytes `0x13..0x17` = 1,1,1,1,0; the map record's event indices are 0). Program A (load) never runs because the harness sets status 2 after the spawn, as D1 does for the hero.
The one place where something is called from a hook is the D stub: it runs the ComputeNewHp experiments (below) on copies of the machine, then restores it byte for byte.

## 3. Setup choices

| Item | Choice |
|---|---|
| Field | D1's flat field (52 x 60 cells, height byte 0), map constants of map 10 = those of map 14 (asserted from `map_14.json`: Gravity 128, ZViscosity 4096, SlideEffectId 0, +9 = 2, C = 7, D = 0) |
| Balance | disc `DATA/BALANCE.BIN` (equal to `balance.json`, D2) at `0x80143DF8`; level D of map 14 = **0** through the real `0x80044520`. Beannoid = balance index 335 (`79 \| 0x100`, `0x8003A304`): level 255, MaxHp 9, responses `[16,16,16,64,16,...]`, one attack `0x01` power 1, `FileOffset` 7565 |
| Hero | D1 `place_hero` (real `InitializeEntity`), new-game kit and `SetPlayerWeaponId(1)` by real code (Basic sword), direction 24 (east), HP 10; start `(252 - dx, 96 + dy)` px (`dx` = target x - hero x, `dy` = hero y - target y) |
| Beannoid | `map_14.json` `SpriteRecords[79]` laid out by `bank.py` at `0x801C0000` (20 animation sets, 1988 / 42 / 1236 bytes of animations / collision / images, 0 clashes); map entity record = entity 0 of map 14 with `XPos 20, YPos 11, Height 0` (px `(252, 96)`, `X = 12 * XPos + 12`, `Y = 8 * YPos + 8`, `Z = Height << 19`) in the per-map block `0x80126E70` (`+8` records at `0x801CF000`, `+0xC` sprite pointer table at `0x801CE000`, `+0x10` = 257, `+0x14` = count); spawned by the real `0x8003A1B8(0, i, 0)`; status set to 2 |
| Random state | seed `0x12345678` before the spawn; the spawn's loot draw (`0x80032A40`) advances it: the value before tick 1 is `0x24D2C78A` (H1 row 0). The binary's per-frame advance (`0x8002BD14`) is not modelled, as in D1 |
| Inputs | Square `0x80` at tick 1 (H1*, H2, H4b, H5), at ticks 1 and 23 (H4a), Right `0x2000` from tick 1 (H3). No warm-up |
| H4b | the harness writes `TargetAnimationId` (`+0x88`) = 6 on the Beannoid before tick 1 (stands for what its C/E handler would write); `+0xB4` then comes from the real `UpdateAnimation` |
| Rows | state AFTER the passes of the tick; tick 0 = start. Positions in px (exact decimals); `pos_z_raw` = binary convention (rest = terrain + 1 = 1), `pos_z_dll` = raw - 1; sizes are the stored `(n << 16) - 1` |

## 4. Results (all computed; see `out/summary.txt` for the full lists)

**Hero swing boxes (basic sword, east, `out/H1.txt`, FACT):** ticks 1-4 offset (0,0,0) 29x15x15; 5-6 (21,-8,0) 29x15x15; 7-11 (22,-16,0) 23x15x15; 12-21 (-5,-18,0) 23x15x15; tick 22 back to Idle (no box).
`CurrentAttack` = `0x80144229` (attr `0x81`, power 1) during the swing. The Beannoid (anim 0): body `ModdedPos = pos + (-10,-7,0)`, size 20x14x8 (stored 1310719/917503/524287), frame box
(-10,-7,0) 19x13x13 on every frame of anims 0 and 1 (`out/data-boxes.txt`), `CurrentAttack` = `0x80145B94` (attr `0x01`, power 1; the record has one attack, so `Attacks[0]` serves every animation).

| Scenario | Key ticks and values |
|---|---|
| **H1** dx 40, Square at 1, 40 ticks | **first contact tick 5** (frame box 21,-8). Target: `TouchingEntity` = hero (slot 0, `0x80127D30`), `+0x1D0 = 0x19`, hero `HitCounter` 1, **3 sparks** (effect 9, centre (251.99998, 95.99998, 4) px, 9 RNG draws: `0x24D2C78A` -> `0x5AAD3C51`). Tick 6: hero `HitCounter` 0, target `TouchingEntity` 0 (counters pass), `+0x1D0` 24, the real events pass chooses **D** for the target (stub `0x8007D9A4`) - C on every other tick. Boxes overlap on ticks 5-11 but only tick 5 is a hit (blocked by `+0x1D0`: 25 at 5, 24 at 6, ... 1 at 29; it would be 0 at 30). Latched animation `0x80146DF8` = 16. Hero and target HP unchanged. |
| H1 ComputeNewHp at tick 6 | attack `0x80144229` attr `0x81` power 1, latched anim 16, response byte 16. Equipment **empty**: damage 1 (9 -> 8). Equipment **as `0x800307E8` fills it** (also the live memory): `[0]` jacket record blob+0x79F (AttackCount 0), `[1]` sword record blob+0x6A5 (Attacks `[(1,1)]`), `[2]` null: damage **2** (9 -> 7), returns 0. `((1+1) * 16) >> 4 = 2` |
| H1-near dx 30 | contact on **tick 1** (frame-0 box 29x15x15 at offset 0 already overlaps; the animation pass precedes the hit pass in the same tick); sparks (246.49998, 99.49998, 4); the Beannoid does not touch the hero |
| H1-both dx 20 | tick 1: **both** hit: target (`TouchingEntity` hero) and hero (`TouchingEntity` slot 1); hero `HitCounter` 1 and target `HitCounter` 1; 6 sparks. Tick 2: the hero's damage entry runs (anim `0x31`, HP 10 -> 9), his swing ends |
| H1-edge dx 59 | contact at tick 5 (ticks 5-6 overlap), sparks (242.49998, 95.99998, 4) |
| **H2** dx 60 (reach + 1) | **no contact** in 40 ticks, no overlap tick at all |
| **Reach** (`out/reach-sweep.txt`) | dy = 0: contact for **dx in [-9, 59]**, first contact tick 1 for dx -9..38 and tick 5 for 39..59. Y window: dx 20 -> dy in [-21, 24]; dx 40 -> [-13, 22]; dx 55 -> [-13, 14]. The Beannoid's own body box touches the hero for dx in [-18, 20]. INFERENCE (checked against 364 runs): `dx_max = 21 + 29 + 10 - 1 = 59` from the frame-1 box, `dx_min = -9` from the frame-0 box against the body |
| **H3** dx 60, Right held, 45 ticks | hero Moving (anim 1), ForceX 79872 at tick 1 then 159744. **Contact tick 17** (hero x 232.21875): hit written **on the hero**: `+0x1D0 = 0x19`, `TouchingEntity` = slot 1 (Beannoid), Beannoid `HitCounter` 1, 3 sparks (242.60936, 95.49998, 6.5). Tick 18 (counters clear both): the hero's damage entry inside `MovePlayer`: `TargetAnimationId` `0x31` (AnimFlags `0x40`, sound 23), `TargetDirection` 24 -> 8, HP 10 -> 9, `+0x1CC` 119 after the tick (INFERENCE: 0x78 set by `MovePlayer`, decremented by the counters pass of the same tick), ForceX -196608; tick 22 anim `0x1C`, tick 33 Idle, tick 34 walking again; hero `+0x1D0` 25 -> 0 at tick 42. The Beannoid keeps HP 9, anim 0, position 252. Hero sheet `0x80127008` responses all 16: `(1 * 16) >> 4 = 1` |
| **H4a** dx 40, Square at 1 and 23 | hits at **ticks 5 and 30** (= 5 + 25). Overlap ticks 5-11 and 27-33; not hits: 6-11, 27, 28, 29 (window of the first hit) and 31-33 (window of the second). `+0x1D0` (row values): 25 at tick 5, 24 at 6, 1 at tick 29; at tick 30 the counters pass brings it to 0, the pass accepts the hit and the row shows 25 again |
| **H4b** dx 40, Beannoid in anim 6 | the animation set byte `0xD` of set 6 is `0x40`: `+0xB4 = 0x40` from tick 1 (anim 6 holds, no box). Boxes overlap on ticks 5-11, **no hit** in 40 ticks |
| **H5** targets (+40, 0) and (+52, +8) | **both hit at tick 5**: each `+0x1D0 = 0x19`, `TouchingEntity` = hero, hero **`HitCounter` 2**, 6 sparks (centres (251.99998, 95.99998, 4) and (257.99998, 99.99998, 4)); tick 6: both cleared, D chosen for both, two experiments (same numbers as H1) |
| **H6** immune target, dx 40 | target = REAL sprite 126 of map 69 (`SpriteRecords[126]`, entity 0 of that sprite, 15 placed in map 69), balance record **382** (MaxHp 1, `DamageResponses[0]` = 128: nibble 1 -> 0x80). Searched: of the placed sprites in the decoded maps, the records with `(r & 0xC0) == 0x80` and MaxHp > 0 are 228-243, 295/296, 316, 320, 374-376, 382, 427, 429, 495; 382 chosen (class bit 0 set, 24x16x16 body, no program). **Tick 1** (24 px wide body: frame-0 box reaches it): target `+0x1D0 = 0x19`, **TouchingEntity none**, hero `HitCounter` 1, 3 sparks (effect 9 at (240.49998, 99.99998, 7.5)), no D program, HP unchanged, no ComputeNewHp experiment. The hit pass jumps `0x800395EC beq` to `0x8003966C` (the `0x19` store and the attacker's counter) and skips the `0x224` store at `0x80039668` |
| **H7** two attackers, dx 30 | hero (slot 0), target Beannoid (slot 1), second attacker (slot 2) = an inert Beannoid 12 px east of the target whose `+0x6C` got bit 2 (HitsA) from the harness (`scen.or_flags`, before tick 1; its attack `0x80145B94` attr `0x01` power 1 and its frame box come from its real animation, present every tick). Both reach the target on **tick 1**: only the hero (first in list order) sets target `+0x1D0 = 0x19` and `TouchingEntity` = slot 0; slot 2 `HitCounter` stays 0 on that tick. The hero also hits the second attacker at tick 5 (it is a Beannoid in reach, `TouchingEntity` slot 0). Slot 2 hits the target only at **tick 26** (`+0x1D0` window of tick 1 over), `TouchingEntity` slot 2, its `HitCounter` 1 |
| **H8** west, dx -40 | hero direction 8, target 40 px west: **contact tick 5**, as H1. Swing boxes (offset x, width 29/23): ticks 1-4 -30, 5-6 -51, 7-11 -45 (23 wide), 12-21 -21 (23 wide); y/z offsets as east. `TouchingEntity` = hero, `+0x1D0 = 0x19`, hero `HitCounter` 1, 3 sparks at (251.99998, 95.99998, 4) |

H6-H8 additions to the tools: `c1world.KINDS` / `kind_data` (a real sprite record of another map as the target; the world stays map 14's), `scen.or_flags`, `scen.hit_model_multi` (the tick model for scenarios where several attackers contest a victim: per tick counters decrement, attackers in list order against every other entity, the first valid pair writes `+0x1D0 = 0x19`; TouchingEntity unless the balance response byte of the attack nibble has `(r & 0xC0) == 0x80`, read from `BALANCE.BIN.json`) and two mutations in `checks.py` (immunity branch removed on H6, `+0x1D0` guard removed on H7). Existing outputs of H1-H5 and D1 are byte-identical to the previous run; only the files that list all scenarios gained lines.

## 5. Facts decoded for this task (new, all in the emulated binary)

- **FACT, events pass dispatch** `0x800386D0`: first loop `0x8003871C-0x80038898` over slots 1..`[0x80134360]`: `+0x20 != 0` skips (`0x8003871C-0x80038724`), status table `0x800237B4`; the choice for a normal entity is D if `+0x224 != 0`
  (`0x80038840 8e02fffc`, `0x80038848 14400011`, `0x8003884C 24050003`), else C if `[0x80127108] != entity` (`0x80038854-0x80038860`, `a1 = 2`), written to `+0x228` (`0x80038894`). Second loop `0x800388E4-0x80038948`:
  `8c82004c` (`+0x4C[letter]`), `3042007f`, `0c010817 jal 0x8004205c`, or `8c850070` (`+0x70[letter]`), table pointer from `0x80098F4C` (`3c02800a 24558f4c`), `0040f809 jalr v0`, then `+0x228 = -1`; the pass repeats while a handler ran.
- **FACT, spawner `0x8003A1B8(parent, record index, force)`**: record `0x80039BD0` (block `0x80126E70`: `+0x14` count, `+8` base, 20 bytes per record, byte 4 = enabled); sprite record `0x80039B28(byte5 & 0x80, byte 6)` (per-map block `+0xC`
  table, `+0x10` count; out values 0 and 0x20 for per-map, 0xB and 0x60 for the common bank); slot `0x80039AD0` (first free from 1); camera-window test only when `force == 0`; position `0x8003A2B0-0x8003A2E0`; direction `0x80023644[byte5 & 3]`; balance index byte 6 (+0x100 if byte5 & 0x80, `0x8003A304`); animation 0 (`0x8003A320`).
- **FACT, AnimFlags**: `+0xB4` = byte `0xD` of the animation set (`0x80038B68 9082000d`, `0x80038B70 ae0200b4`; `+0xF8` = s16 at `0xA`, sound = byte `0xC`). The decoded JSON names byte `0xD` **`Acceleration`**. Beannoid sets with bit `0x40`:
  **5-14, 16, 17, 18** (hurt, roll, fall...), without: 0-4, 15, 19 (`out/data-boxes.txt`). So a Beannoid is untouchable while in any of those animations.
- **FACT, same-tick order**: for a swing pressed at tick T the animation pass (`0x80038E18`) loads the frame-0 box before the hit pass of tick T, so a target in the frame-0 reach is hit **on the press tick** (H1-near: tick 1).
- **FACT, the D program at N+1**: the hit written at tick N is still visible (`TouchingEntity`, `HitCounter`) when the events pass of tick N+1 chooses D; the counters pass of tick N+1 clears them afterwards (H1 ticks 5, 6).
- **FACT, ComputeNewHp reads**: the attacker's `+0x1C8` at the call, the latch `0x80146DF8` from the tick-N hit pass, the equipment records as left by `MovePlayer` of tick N+1 (`UpdateItemEffectState` runs at its end): the live memory equals the explicitly filled one.

## 6. Checks of the oracle itself

- **D1** (`out/d1/comparison.txt`): S-A and S-B equal to `docs/plan-e19-d2c-valeurs.md` B.1 field by field in this copy: 216 values each, 0 differences.
- **Independent model** (`scen.hit_model`, written from plan 0.2.5, fed only with the geometry/counter columns of the rows): equal to the real hit ticks for every ordered pair of entities of every scenario and for the 364 sweep runs (both directions: swing on the Beannoid, Beannoid on the hero).
- **Two interpreters** (`out/diff2.txt`): all scenarios on `emu.py` and on `mips.py` (via `interp2.py`): rows, the hero and target slots (0x294 bytes each) after every tick, journal, experiments, RNG word identical. `mips.py` stubs only `jal` targets; the sprite handlers are called by `jalr`, so `Cpu2.step` in the copied `interp2.py` handles a stubbed `jalr` target (delay slot, stub, continue at pc + 8).
- **Teeth** (`out/mutations.txt`): 10 one-word patches or data patches of the emulated binary with the expected change written beforehand in `tools/checks.py` (class-mask bits, `+0x1D0` guard, AnimFlags 0x40 test, the `0x19` constant, the spark count, the equipment term, counters pass skipped, hit pass skipped,
  hero box 1 px wider moves the reach by exactly 1), all caught; 2 controls (other RNG seed, effects pass skipped) change nothing but the RNG word / the journal.
- **Load delay** (`out/load-delay-check.txt`): over every instruction executed by any run (both interpreters ignore the MIPS-I load delay slot), no load is followed by a use of its register.
- **Unwritten bytes**: no read of an uninitialised byte in any scenario (the machine reports them; each scenario file ends with the line).
- The explicit ComputeNewHp calls run with a stack pointer below the live frames (`c1world.side_call`) and the machine (RAM, shadow, journal, counters) is restored after each variant.

## 7. Limits and open issues

1. The targets are inert: no reaction to a hit (no hurt animation, no knockback, no HP change, no death), no movement; programs A, C, D, E of the Beannoid are UNKNOWN here (E14). A real Beannoid moves and changes animation, which changes its boxes and its AnimFlags.
2. ComputeNewHp is called from the D stub, i.e. at the entry of the D program, on copies. What the real D handler `0x8007D9A4` does around it (hurt animation, direction, `Bytes[3]`) is not run.
3. The hero damage entry is exercised only as `MovePlayer` runs it in H3/H1-both (E20.c2's behaviour, recorded, not pinned). Hazard tiles are not exercised.
4. Effects are stubs: the effect objects, their velocities, and what effects 4/5 do are not modelled (effects 4/5 never occur here: attribute `0x01`). The effects pass `0x80038E84` is a stub.
5. One weapon (Basic sword, no charge), one direction (east, the hero does not move during the swing), one target kind. The synthetic field is flat; the Beannoid is spawned at Z 0.
6. The frame boxes come from the decoded data laid out by `bank.py` (D6 compared the export to these records; neither compares them to the disc bytes).
7. The per-frame random advance (`0x8002BD14`) is not modelled; only the draws made by the passes (sparks, spawn loot) are real.
