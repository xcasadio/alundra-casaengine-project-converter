# E20.b - the attack oracle (2026-10-09)

Purpose: reference traces for the future pins of E20.b (sword swing without damage), produced by running the **real code of `ALUN_CD.EXE` (France)** on the D1 MovePlayer
oracle. Ground truth: PS-X EXE, text at `0x80020000`, file offset `0x800 + addr - 0x80020000`. Labels: **FACT** (decoded instruction words or a measurement, with where),
**INFERENCE** (with its base), **UNKNOWN**. Everything lives in this folder; `d1-oracle-moveplayer/`, the decompilation, `Alundra/`, `Alundra.Tests/`, the engine and
`alundra-project/` are untouched. The tools are a copy of `d1-oracle-moveplayer/tools/` (only change: `compare.py` looks one folder higher for the annex, `run_all.py` writes to
`out/d1/`) plus new files: `attack.py`, `run_attacks.py`, `mutations_attack.py`, `hazards_attack.py`, `run_e20b.py`.

## 1. How to run

```
python -B docs/plan-e20-annexe/e20-b/oracle/tools/run_e20b.py      # about 2.5 minutes, exit code 0 = everything below is fine
```
It runs, in order: (1) the D1 re-validation with this copy (`tools/run_all.py` -> `out/d1/`, S-A and S-B equal to the annex field by field); (2) `run_attacks.py` -> `out/`
(scenarios, summary, the two-interpreter comparison, the load-delay check); (3) `mutations_attack.py` -> `out/mutations.txt`. Needs Python 3 with `capstone`, the disc file
`D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` (+ `DATA/BALANCE.BIN`) and `data-extracted/data/map_alundra.json`.
Always `python -B`. Two complete runs give byte-identical `out/` (md5 of every file compared).

**D1 re-validation: PASS** (`out/d1/comparison.txt`: S-A 216 values, S-B 216 values, 0 differences; `out/d1/mutations.txt` and the other D1 checks as in D1; `out/d1/S-A.trace.json`
and `S-B.trace.json` are byte-equal to the D1 originals).

## 2. What is computed and what is chosen

The traces are computed by the emulated binary. `attack.py` holds only the SETUP (field, weapon, warm-up) and the INPUTS (the pad word of every tick). It contains no expected value
of any output: the animation a press chooses comes from the binary's weapon table, the sound from the animation set, the tick a swing ends from the animation bytes. The two
places where a setup value is picked by looking at the machine are stated: the warm-up of AT2/AT8 (as D1's S-B: Right held until two consecutive Moving ticks at ForceX 159744)
and the start column of AT8 (section 4).

Per tick the trace has: tick, hold word, `+0x88` (target animation), `+0x90` (current animation), `+0x8C` (target direction), `+0x94`, `+0x98`, ForceX/Y/Z (`+0xC4/+0xC8/+0xB8`),
PosX/PosY/PosZ (`+0x114/+0x118/+0x11C`), IsOnGround (`+0x144`), `+0xB0`, the charge timer `0x80126FFC`, the record pointer `0x80127110`, the step counter `0x80127114`, the weapon
record index `0x8012710C`, and the journal of sound / effect / spawn calls of that tick. The `.trace.json` files add `+0x180`, `+0x1D4` (frame box pointer), `+0x1C8` (current attack
pointer), `+0x220` (HitCounter), the terrain `+0x138`, the converted frame box (`+0x208..+0x21C`) and the HitBox (`+0x1FC..+0x204`). The row of tick N is the state AFTER the passes of
tick N; "tick 0" is the state before tick 1 (for AT2/AT8 the state at the end of the warm-up; the warm-up ticks are numbered -3..-1).

**Convention.** Only PosZ has the `+1`: the binary rests at terrain + 1, so the rest value on the flat field of height 0 is `PosZ = 1` (`pos_z` column, raw) and the DLL value
is `pos_z - 1` (`pos_z_dll` column, plan D-E19-59). PosX and PosY are the raw 16.16 values (px << 16), no offset. Terrain of a cell with height byte `h` is `h << 20`.

Tick structure = D1 (`world.tick`): pad words written (`AlundraTickPad` port, cross-checked every tick against the binary's own pad routine `0x8002E250`), then the eight passes
of `0x8003B388` in the binary's order, once per tick. Pass 7 (`0x80038E84` effects) and pass 8 (`0x80039300` hits) are stubs journalled once per tick, as in D1: **no hit is ever
delivered** (HitCounter stays 0, no target exists). Stubs with a journal: `0x800490FC` sound, `0x8003BDD8` and `0x8003BE74` effect creation (they return a zeroed dummy slot),
`0x80039F58` spawn. Pad words: Up `0x1000`, Right `0x2000`, Down `0x4000`, Left `0x8000`, Triangle `0x10`, Circle `0x20`, Cross `0x40`, Square `0x80`.

## 3. How WeaponId and the items are set (decoded)

- **New Game** (D1, `world.init_stats`): `0x8004DAC0` InitPlayerStats, then `0x8004E530` AddItem for every item whose property byte 6 has bit `0x80` (items 1 Basic sword, 17, 25),
  then `0x8004E484` SetPlayerWeaponId(1) (`0x80031894`). All of it real code.
- **AddItem `0x8004E530(item)`**: range test against `[0x8017638C]`; entry = `[0x8017631C] + 4 * item`; if the count (`lh +2`) equals the item's maximum (`lh 6` of the property
  record `0x800B9FE8 + 10 * item`) it is left, else the count is incremented (`sh` at `0x8004E5A0`). FACT.
- **Item property record** (`0x800B9FE8`, 10 bytes): `+0` slot, `+2` flags, `+4` rank, `+6` maximum count. Read by the oracle from the binary: Basic sword (1) slot 1 rank 0,
  Steel Edge (2) slot 1 rank 1, Demon Buster (3) slot 1 rank 2, Holy Sword (4) slot 1 rank 3, item 8 slot 1 rank 4, Hunter's Bow (5) slot 2 rank 0, William's Bow (6) slot 2 rank 1,
  Magic staff (7) slot 6 rank 1, Chain Throw (9) slot 3 rank 0, Crusher (10) slot 3 rank 1, Drifting Ice Rod (14) slot 4 rank 1, Fireball Rod (16) slot 5 rank 1 (`out/at9/*.trace.json`,
  field `meta.item_props` = slot, flags, rank, max, other).
- **SetPlayerWeaponId `0x8004E484(a0)`**: if `a0 == -1` or `(a0 - 1) < 6` (unsigned), `sh a1, 0xa(stats)` (`0x8004E4B4`, `[0x80176318] + 0xA` = WeaponId), else an error call
  `0x8004DB90`; then `jal 0x8004E030`. FACT.
- **GetItemIdFromCurrentWeapon `0x8004E030`**: `lh 0xa(stats)`; -1 gives -1 (`0x8004E0E4`, `addiu v0,-1`); slots 1..6 go through the jump table `0x800265D4` to
  `0x8004E268 .. 0x8004E308`, each `jal 0x8004E18C(slot)`.
- **GetItemIdFromSlotId `0x8004E18C(slot)`**: loops over the 128 item records; item i matches when `lh (0x800B9FE8 + 10i) == slot` (`0x8004E1D4-0x8004E1DC`) and the inventory
  count `lh 2([0x8017631C] + 4i) > 0` (`0x8004E1E4-0x8004E1EC`). The first match is taken; if its flags (`lhu +2`) bit 0 is 0 it is returned at once (`0x8004E1FC-0x8004E210`), else the
  scan goes on and a later match replaces it when its rank (`+4`) is greater (`slt` at `0x8004E230`). INFERENCE from reading the loop plus all runs agreeing: **the owned item of the
  slot with the highest rank is the weapon**; all weapons have flags 1.
- **Consequence for the oracle (setup in `attack.setup`)**: the kit (items 1, 17, 25, slot 1) is built first; to test a weapon the oracle calls the real `AddItem(item)` and then the real
  `SetPlayerWeaponId(slot)`, the slot being the first half-word of the item's property record, read from the binary. Because the Basic sword stays owned, a higher rank in slot 1
  wins (Steel Edge, Demon Buster, Holy Sword, item 8); in the other slots the tested item is the only owned item of its slot. After setup the oracle asks the real
  `0x8004E030` which item is equipped and records it (`meta.weapon_item_now`) next to the record index MovePlayer later computes (`byte[0x80028C14 + item]`, `0x80031B70-0x80031B8C`,
  written to `0x8012710C` every tick).
- **No weapon (AT10)**: `SetPlayerWeaponId(-1)` gives item -1; MovePlayer then reads `byte[0x80028C13]` = 0, so record 0 (the Basic sword record). FACT (measured: AT10 swings 0x10).
  A WeaponId whose slot has no owned item (mutation `WeaponId 2 but the bow not owned`) resolves to -1 the same way.
- The weapon table: records of 13 bytes at `0x80028C78 + 13 * w` = chargeable flag, ground (anim, type, delay, z), air (...), charge (...). The press path (`0x8002EC94-0x8002ECFC`)
  takes `0x80028C79 + 13w` on the ground, `+4` in the air; success writes the record pointer to `0x80127110`, `0` to `0x80127114` and the animation to `+0x88`; record animation 0
  plays sound 3 (`0x8002ED30`) and returns 0.

## 4. Setup choices per scenario

| Scenario | Inputs (pad word per tick) | Field / weapon / special |
|---|---|---|
| AT1 | tick 1 Square; 60 ticks | flat field, Basic sword (kit), hero at (100,100) px, direction 24 |
| AT2 | warm-up as S-B (Right held 3 ticks to the steady walk), tick 1 Right+Square, Right held after; 60 ticks | as AT1 |
| AT3 / AT3b / AT3c | tick 1 Cross; Square at tick 5 / 14 / 19; 60 ticks | as AT1 (AT3b/c are the same jump with the swing still running at the landing; not asked, added to see the landing in the air-attack state) |
| AT4 | tick 1 Square+Cross | as AT1 |
| AT5 | Square on ticks 1..30, released at 31; 60 ticks | as AT1 |
| AT6 | Square at 1, 4, 10, 16 (during the swing) and at 23 (= AT1 chain tick + 1); 80 ticks. `at6-sweep.txt`: one extra press at p = 18..25 | the chain tick is read from AT1's own trace |
| AT7 | tick 1 Square; 30 ticks | every cell has ground property `0x20` (cell word & `0x2000`). AT7c control (flag on a far cell), AT7d (Square+Cross), `at7-cellmap.txt` (one flagged cell at a time) |
| AT8 | warm-up east, tick 1 Right+Square, Right held; 60 ticks | platform: columns 0..4 (x < 120 px) height byte 1 (terrain 16 px), columns 5.. height byte 0. **Start column chosen by an observed condition**: the smallest start x in 108..139 for which the hero is on the platform after the warm-up, the swing starts on tick 1 and IsOnGround is 0 after tick 1 (search table in `summary.txt`: x = 123 px). AT8n = control one pixel to the left. |
| AT8b | tick 1 Square; 40 ticks | the harness lowers the height byte of EVERY cell from 1 to 0 just before tick 7 (the floor drops under an Idle hero in mid swing); not a walk, a field change |
| AT9 (`out/at9/`) | ground: tick 1 Square; air: tick 1 Cross, tick 5 Square; 70 ticks each | one run per weapon and per kind (22 runs, + item 8), weapon set as in section 3 |
| AT10 | tick 1 Square | no weapon |
| AT11 | tick 1 Left+Square, Up held on all other ticks | as AT1 (the facing at the press and during the swing) |
| X1, X2 | X1: Square held 1..90; X2: Square held 1..89 with Cross at tick 80 | INFORMATIONAL (charge, not asked), Steel Edge. The aura, sparks and sounds are the stubs of D1: only their calls are journalled. |

The random state word `0x80098708` is set to `0x12345678` (D1 correction N1); no scenario draws from it except the landing dust and the charge sparks (stubs). Field geometry facts used:
a cell's height byte `h` gives terrain `h << 20` (measured with h = 1, 2, 4, 8); `+0x180` is the OR of the cell words under the four corners of the hero's footprint (`0x80038170-0x80038190`).

## 5. Checks of the oracle itself (`out/`)

- **Two interpreters** (`diff2-attack.txt`): all 40 named scenarios run on `emu.py` and on `mips.py` (via D1's `interp2.py`); rows, the hero's 0x294-byte slot after every tick, the
  pad/charge/weapon/stats memory at the end and the stub journal are identical.
- **Teeth** (`mutations.txt`): 15 mutations with the expectation written beforehand (skip a pass, `+0x20 = 1`, a byte of the weapon table, a byte of the item map, the `bnez` of the `0x2000`
  gate turned into a nop, field without the bit, Steel Edge equipped...) and 6 controls that must not change (physics pass skipped on an Idle swing, another record patched, other
  start position, other random state, held Square, bow slot without bow): all as expected.
- **Load delay** (`load-delay-check-attack.txt`): both interpreters ignore the MIPS-I load delay slot; over every instruction executed by the attack runs (3477 distinct, 704 loads) no load
  is followed by a use of its register.
- **Unwritten bytes** (`*.machine.txt`): no read of an uninitialised byte in any scenario; MovePlayer reach points as in D1 (normal path, no early exit); the attack code points
  (`PlayerTryAttack`, press path, success tail, `UpdateWeaponStepProgression`) are listed per scenario.

## 6. Limits and open issues

1. Synthetic world: flat field, no other entity, no map events, no dialogue/menu flags (`[0x800DC4B8] = 0`), no transition. The hit pass is a stub: damage, HitCounter, TouchingEntity, the
   flail bounce (needs HitCounter) and the cutting of grass (`CheckAndTriggerTileEffect`, no tile has flag bit 2 here) are **not** exercised.
2. Effects are stubs: the charge aura, sparks, the landing dust and their sounds show only as journal lines; nothing says what the real effect objects do.
3. The pad word of a tick is what the scenario gives; when the DLL samples the pad relative to the tick is E20.a's concern (D1 limit 7). A press and a release of Square on the same
   tick cannot occur with this pad model (pressed = new & ~old, released = old & ~new), so "a release tick never reaches the press path" (D-E20-9) is reached only through a real
   release tick (X1: release with a full charge on the ground attacks; X2: release in the air with a full charge is lost, timer zeroed on the next tick).
4. The footprint rule used by AT8 (the hero stays on the platform until his whole footprint has left it, 10 px either side of PosX, from x = 129.09 + 1.22) is an INFERENCE from the search
   table, not a decoded rule; the oracle's own FloorHeight (`0x80037F28`, real code) produces it.
5. Sound ids are the argument of `0x800490FC`; `sound(0)` lines are calls with id 0 (animation switch to an animation whose Sfx is 0, walk cycle): not sounds, but they are journalled
   because the binary calls the function.
