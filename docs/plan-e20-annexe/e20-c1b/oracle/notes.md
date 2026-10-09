# E20.c1b - the consequences of a hit, all real code (2026-10-09)

Purpose: reference values for the E20.c1b pins (`ComputeNewHp` / `ResolveBalanceTarget` as pure functions, the three equipment records and the hero sheet that damage needs, opcodes `0x86`, `0x87`, `0x95`, `0x71`),
produced by running the **real code of `ALUN_CD.EXE` (France)** on the c1 world (copied) with a chosen inventory. Ground truth: PS-X EXE, text at `0x80020000`, file offset `0x800 + addr - 0x80020000`.
Labels: **FACT** (decoded instruction words or a measurement, with where), **INFERENCE** (with its base), **UNKNOWN**. Everything lives in this folder; `e20-c1/oracle`, `d2-oracle-coups`, `e20-b/oracle`, the decompilation, `Alundra/`,
`Alundra.Tests/`, the engine, `alundra-project/` and `data-extracted/` are untouched. `tools/` holds copies of the c1 oracle tools (byte-identical: `bank.py callgraph.py compare.py dump_dis.py emu.py hazards.py interp2.py lib.py machine.py mips.py
mutations.py oracle.py padcheck.py pass38634.py run_all.py run_update_animation.py startstate.py world.py c1world.py`; D1 runs from them) and the new files `kworld.py kmodel.py k1.py k2.py k3.py kmut.py kcheck.py run_c1b.py disrange.py`.

## 1. How to run

```
python -B docs/plan-e20-annexe/e20-c1b/oracle/tools/run_c1b.py      # about 95 s; exit code 0 = D1 PASS and every check below passed
```
Order: (1) D1 re-validation with this folder's copy (`tools/run_all.py` -> `out/d1/`; S-A and S-B equal to the annex, 216 values each, 0 differences); (2) K1 -> `out/K1.txt`, `K1.json`, `K1-dependency.txt`; (3) K2 -> `out/K2.txt`, `K2.json`;
(4) K3 -> `out/K3.txt`; (5) K1-K3 again through the second interpreter -> `out/diff2.txt`; (6) mutation tests -> `out/mutations.txt`; (7) `out/coverage.txt`, `out/load-delay-check.txt`, `out/dis.txt` (disassembly of every function under test),
`out/summary.txt` (verdicts and the key values, computed), `out/manifest.txt` (md5 of every other file). Needs Python 3 with `capstone`, the disc file
`D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE` (+ `DATA/BALANCE.BIN`), `data-extracted/data/map_14.json`, `BALANCE.BIN.json`. Always `python -B`.
Two complete runs give identical manifests (checked: no clock, no host path, no hash-ordered iteration in the output).

## 2. Real code, stubs, harness writes

| Piece | Run as |
|---|---|
| New-game inventory: `InitPlayerStats` `0x8004DAC0`, `AddItem` `0x8004E530`, `SetPlayerWeaponId` `0x8004E484` (-> `0x8004E030`), `SetCurrentItemId` `0x8004E4D8` (the "item setter": `sh a1, 0xc(stats)` at `0x8004E51C`, range test against `[0x8017638C]`) | **real** |
| `UpdateItemEffectState` `0x800307E8` (and `0x8004E0F8`, `0x8004E18C`, `0x8004E428`, `0x800445C0` below it) | **real** |
| `ComputeNewHp` `0x8003A374`, `ResolveBalanceTarget` `0x8004464C`, `0x80044550`, `0x80044520` | **real** |
| Opcode handlers `0x8003FA10` (0x71), `0x8003FFD4` (0x86), `0x80040048` (0x87), `0x800407C0` (0x95), the search `0x8003C954` (+ `0x8003C914`, `0x80039BD0`), the program runner `0x8004205C` | **real** |
| Tick passes `0x80038634 0x800386D0 0x80038998 0x800384F4 0x80038E18 0x80038364 0x80039300` (hit pass), `MovePlayer`, spawner `0x8003A1B8` | **real** (c1 world, unchanged) |
| Effects `0x8003BDD8 0x8003BE74`, sound, spawn `0x80039F58`, effects pass `0x80038E84` | stubs of `machine.py` (journalled; the sparks are drawn but K never reads them) |
| The targets' own programs | inert (c1 `install_inert`); for K3 only the stub on `0x8004205C` is removed after the ticks so the real runner can be called |

Harness writes (all stated in the output line they affect): the inert stub; synthetic attacks (2-byte pairs at `0x801EA000`); copies of the Beannoid record with one response changed (`0x801EA100`); a synthetic 3-attack equipment record (`0x801EA200`);
latch / equipment pointer / HP / `TouchingEntity` pokes inside **sandboxes** (`c1world.Sandbox`: the whole RAM, the initialised-byte shadow, journals restored byte for byte after each experiment); in K3 a few `+0x48`, `+0x1CC`, `+0x1D4`, `+0x1C8`, `+0x10`, `+0x20`, hero HitBox pokes
on copies. The setup (spawner, `InitializeEntity`, balance load) always runs on `emu.py`; every K-level call of a function of the binary and every tick runs on the interpreter under test (`kworld.call` / `kworld.tick`).

## 3. Setup choices

| Item | Choice |
|---|---|
| World | c1 flat field, map 14 constants (asserted), level D = 0 through the real `0x80044520`; BALANCE.BIN of the disc at `0x80143DF8` |
| Inventories | built by the real new-game code (section 4). The kit loop (`0x80031868-0x80031890`) read from the EXE gives `[1, 17, 25]`, asserted equal to the kit used |
| Hero | slot 0, (212, 96) px facing east, HP 10; the c1 `place_hero` (real `InitializeEntity`) |
| K1 | no targets; the pad hold word `0x80126F28` is zeroed first (the regeneration gate of `0x800307E8` reads it, `lhu` at `0x80030C54`; in a tick the pad routine writes it, outside a tick it is an unwritten byte) |
| K2/K3 world | Beannoids (map 14 sprite 79, balance index 335) from the real spawner, map records 0..4: **T** (252, 96) 40 px east of the hero, **T2** (264, 104), **A** (348, 96), **V** (360, 96), **U** (252, 168); a sixth enabled map record (5) is never spawned. Slots: hero 0, T 1, T2 2, A 3, V 4, U 5. `+0x48` = map record index (FACT, `0x80039D7C`) |
| K2 HERO | Square at tick 1; sampled at the first tick where the real hit pass leaves `T.TouchingEntity` = hero (tick 5 for every inventory) |
| K2 NH | A gets the HitsA bit (`+0x6C |= 4`) at once; tick 1, hero idle: the real hit pass makes A touch V. Latch = 0 (hero idle animation) |
| K3 | kit inventory; A's `CurrentAttack` := synthetic (attr `0x04`, power 2) from the start and A armed (`+0x6C |= 4`) just before tick 5; 5 ticks, Square at tick 1: at the end of tick 5 the hero touches T and T2 (sword, attr `0x81`), A touches V (attr `0x04`), hero HitCounter 2, A HitCounter 1 (asserted). Flags: hero `0x3118C`, Beannoids `0x103`, A `0x107` |
| Item names | Steel Edge = item 2, Holy Sword Elnard = item 4: INFERENCE (E20.b oracle notes "Steel Edge (2) slot 1 rank 1 ... Holy Sword (4) slot 1 rank 3", powers +2 and +8 from the disc records) |

## 4. K1 - equipment records and the hero sheet (FACT, `out/K1.txt`)

Inventories (owned items added after `InitPlayerStats`; weapon argument of `SetPlayerWeaponId`; `SetCurrentItemId` argument):

| Inventory | Owned | Record [0] `0x80146E00` | Record [1] `0x80146E08` | Record [2] `0x80146E10` | Sheet responses [3..13] |
|---|---|---|---|---|---|
| kit | 1, 17, 25; slot 1 | blob+1951 = `0x80144597`, word 47 (item 17) | blob+1701, word 31 (item 1), attacks `[(1,1)]` | null | all `0x10` |
| steel | + 2 | same | blob+1718, word 32 (item 2), `[(1,2)]` | null | `0x10` |
| holy | + 4 | same | blob+1752, word 34 (item 4), `[(1,8)]` | null | `0x10` |
| ring | + 0x33, ItemId 0x33 | same | item 1 | blob+2461, word 81 (item 51), `[(1,3)]` | `0x10` |
| leather | + 0x12 | blob+1966, word 48 (item 18) | item 1 | null | all `0x0C` |
| noweapon | 17, 25; slot 1 | item 17 | **null** (word 0) | null | `0x10` |
| extras | all (4 + 0x12 + 0x33), noarmour, bracelet 0x35, charm 0x36, tonic 0x38, rings2 | see `out/summary.txt` | | | noarmour `0x00`; bracelet `0x0F`; charm `0F 0F 0F 0F 0F 80 0F...` |

The pointer is `0x80143DF8 + blob offset`; the second word of each record is **item id + 0x1E** (0 = none). The 255-byte sheet is a copy of the record [0] bytes (the armour record's own 15 bytes, **then the following records of the blob**: it copies 255 bytes whatever `RecordSize`)
or, when record [0] is null, of the hero's `+0x1C4` record (header `FF A9 0A`, 77 attacks); bytes 3..13 are then rewritten. Boots level `[0x80127000]` is 0 in every inventory (items 0x1A-0x1C not owned).

**What each output depends on (measured, `out/K1.txt` "dependency", `out/K1-dependency.txt`)**:
- Record pointers and item words depend on **no record byte** (flipping any of 3 x 3 patterns of bytes 0..31 of any record changes none): only on the inventory: record [0] = the **highest-numbered owned item of category 2** (`byte[0x800C5F7C + 8n + 6] & 0x7F`; here items 0x11-0x14), record [1] = the item `0x8004E030` returns for `WeaponId` (slot, owned count > 0, highest rank) if it is 1..0x61 and category 1,
  record [2] = the ItemId `0x8004E0F8` returns (the OLD one) if owned, 1..0x61 and category 3. Measured on `kit`: AddItem of items 0x12-0x14 changes record [0] and the sheet; 2-4 and 8 change record [1]; 0x33-0x38 change record [2] only when also set with `SetCurrentItemId`; 0x1A-0x1C change only the boots level;
  every other item changes nothing; `SetPlayerWeaponId(s)` for s != 1 empties record [1]. The 99 + 99 + 7 variants equal the independent model.
- Sheet bytes 0..2 and 14..254 depend only on the same byte of record [0] (or of the hero record). Sheet bytes 3..13 (the 11 responses) depend on byte i of record [0], [1] and [2]: rule (FACT, `0x80030B64-0x80030C0C`, confirmed on 4730 triples (9x9x9 grid + 4000 seeded random) by the independent `kmodel.combine_response`):
  a byte whose top two bits are exactly `0x80` in any of the three forces `0x80`; base flagged (top bits != 0) and weapon flagged and accessory flagged -> `0x40`; base flagged otherwise starts at `0x10`, unflagged starts at its value; the weapon / accessory byte is added minus `0x10` only when it has no flag bits (a missing record counts as `0x10`); clamp 0..`0x1F`.
  The 12 weapon records, the rings 0x33 / 0x34 and the items 0x37 / 0x38 are `0x40`-flagged so they do not alter the armour response; the bracelet 0x35 and the charm 0x36 carry `0x0F` (added minus 0x10: -1), the charm has `0x80` at response index 6. The 22 records of categories 1-3 have `AttackCount` 0 or 1 (table in `out/K1.txt`).
- `0x8004E0F8` returns the old ItemId and rewrites it with the best item of its slot: with rings 0x33 and 0x34 owned and 0x33 set, the first call uses 0x33 (word 81) and the next calls 0x34 (word 82). FACT (rings2).

## 5. K2 - ComputeNewHp (FACT, `out/K2.txt`, `out/K2.json`)

`ComputeNewHp(e)`: attack = `[e+0x224].+0x1C8`; sheet = `0x80127008` if `e` is slot 0 else `e+0x1C4`; `e.Hp := ResolveBalanceTarget(attack, sheet, Hp)` (store, no clamp); returns `new Hp == 0`. `ResolveBalanceTarget`: Hp unchanged when sheet / attack null or attribute byte 0;
`r = sheet[(attr & 0xF) + 2]`; top bits `0x40` -> 0; other non-zero top bits (`0x80`, `0xC0`) -> unchanged; else `p = power + (attr & 0x80 ? sum over the 3 equipment records of Attacks[g+1 < count ? g+1 : 0].Power, g = 0x80146DF8 (signed), count 0 skipped, null skipped : 0)`,
`dmg = (p * r) >> 4`, 0 -> 1, `Hp = dmg < Hp ? Hp - dmg : 0`. Attacker identity and class are never tested: the equipment term uses the global records and the global latch whoever attacks (FACT, section 5 NH lines).

Key values (target HP 100 unless stated; the Beannoid response for nibble 1 is `0x10`; every line equals the independent model):

| Inventory | Hero swing (real) | Natural hit HP 100 / HP 9 | Responses 0 / 0x40 / 0x80 / 0xC0 | Attack without 0x80: power 1 / power 16; nibble 4 | NH: A's attr 0x01 / synthetic 0x81 p1 |
|---|---|---|---|---|---|
| kit | anim `0x10`, attr `0x81` p1, latch 16 | 98 (dmg 2) / 7 | 99 / 0 / 100 / 100 | 99 / 84; kill (0) | 99 / 98 |
| steel | anim `0x3F`, p2, latch 63 | 96 (dmg 4) / 5 | same | same | 99 / 97 |
| holy | anim `0x49`, p8, latch 73 | 84 (dmg 16) / 0 | same | same | 99 / 91 |
| ring | `0x10`, p1 | 95 (dmg 5) / 4 | same | same | 99 / 95 |
| leather | `0x10`, p1 | 98 / 7 (armour does not touch the hero's damage) | same | same | 99 / 98 |
| noweapon | `0x10`, p1 | 99 (dmg 1) / 8 | same | same | 99 / 99 |

- The task's pair "CurrentAttack for animation `0x10` (attr `0x81`, power 1, `Attacks[0x11]` of the hero record, `0x80144229`) and latch 16" is the real state of kit, ring, leather and noweapon. **Steel Edge and Holy Sword swing with other animations** (`0x3F`, `0x49`), whose hero attacks are `Attacks[0x40]` (power 2) and `Attacks[0x4A]` (power 8): the weapon power is therefore counted twice
  (attack power + record power: 2+2, 8+8). The A10 state forces the `0x10` pair on those inventories: steel 97 (dmg 3), holy 91 (dmg 9), all-three 88; reported next to the real state in `out/K2.txt`.
- Response 0: damage `(p*0)>>4 = 0` -> minimum 1 (HP 100 -> 99, HP 9 -> 8). `0x40`: HP 0, returns 1. `0x80` and `0xC0`: unchanged, returns 0.
- Non-hero attacker (A, attr `0x01` power 1, no equipment bit): damage 1. With A's attack replaced by a synthetic attr `0x81` power 1: kit 98, steel 97 (record +2), holy 91 (+8), ring 95, all 88, noweapon 99: **the hero's equipment is added for any attacker** that has the `0x80` bit, and the latch is read as it stands (idle hero: 0; poked 16: same result because every real record has `AttackCount <= 1`).
  A synthetic 3-attack record `[(1,2),(1,3),(1,5)]` in slot [2] (kit, A attr `0x81` p1, HP 100): latch 16 -> damage 4 (Attacks[0]), 0 -> 5 (Attacks[1]), 1 -> 7 (Attacks[2]), 2 -> 4 (3 is not < 3: Attacks[0]), -1 -> 4 (Attacks[0]). With the equipment records nulled the term is 0.
- Hero as target (HP 200, sheet `0x80127008`): kit A's attr `0x01` p16 -> 184 (response `0x10`), `0x81` p16 -> 183; leather (`0x0C`) 188 / 188; noarmour (hero record, responses 0) 199 / 199 (minimum damage 1 whatever the power); bracelet (`0x0F`) 185 / 185.

## 6. K3 - opcodes (FACT, `out/K3.txt`, 96 checks)

Program state layout (decoded from the runner `0x8004205C`, loop `0x80042280-0x80042344`): owner entity (`s4`); self pointer `[owner+0x230]` (= handler a0); state struct `owner+0x234`; program-counter cell `owner+0x238` (pointer to the opcode byte); **Result word `owner+0x260`**;
call `a0 = self`, `a1 = owner`, `a2 = &pc cell`, `a3 = state`; handler address = word at `0x80098FAC + 4*opcode`; the returned v0 is the instruction size, added to the pc cell by the loop; opcode `0xFF` ends. The runner also writes `owner+0x22C` (the letter) and some globals (`0x8013D9DC`, `0x8013DA28`...). Handlers: `0x71 -> 0x8003FA10`, `0x86 -> 0x8003FFD4`, `0x87 -> 0x80040048`, `0x95 -> 0x800407C0`.
Every case runs twice (handler called directly; the real runner on `[opcode bytes, 0xFF]`): same Result, same fields written, same size.

| Opcode | Size | Result / effect (measured) |
|---|---|---|
| `0x71` | 1 | Result := `self.HitCounter` (`+0x220`): hero 2, T 0, T2 0, A 1, V 0, U 0 at the end of tick 5 |
| `0x86 [search, lo, hi]` | 4 | `+0x1CC := lo | hi<<8` (`sw`, **set**, 0..0xFFFF) on every match; Result untouched. `[0, 0x2C, 1]` -> T 0x12C; id 0 shared by T and U -> both 0x1234; id 5 (enabled record, no entity) -> nothing; preset 0x0100 then value 2 -> 2 (not 0x102); value 0 clears; search `0x80` -> the a0 entity (owner A, self T -> T), `0x81` -> hero, `0x82` -> every active entity, hero included |
| `0x87 [search, typeIdx]` | 3 | Result 1 iff a match has `TouchingEntity != 0` whose `+0x1D4` and `+0x1C8` are non-null, attribute byte non-zero and `(attr & 0xF) == byte[0x80098FA4 + typeIdx]`. T (hero sword, nibble 1): typeIdx 0 -> 1, typeIdx 1..7 (nibbles 2,3,4,5,5,6,7) -> 0; T2 -> 1; V (A, nibble 4): typeIdx 3 -> 1, typeIdx 0 -> 0; U untouched -> 0 for 0 and 3; search `0x80` (self T) typeIdx 0 -> 1; shared ids work (any match qualifies); frame box / attack / attribute zeroed or TouchingEntity 0 -> 0; reads at program time, no HitCounter |
| `0x95 [x,y,z,wx,hy,_,maskLo,maskHi,typeIdx]` | 10 | Result 1 iff some slot 0..`[0x80134360]` (inclusive) has status 2 or 3, `+0x20 == 0`, `+0x1D4`, `+0x1C8`, attribute != 0, `Flags & mask != 0`, `(attr & 0xF) == byte[0x80098FA4 + typeIdx]` and its HitBox overlaps the rectangle: origin `(24x, 16y, 16z)` px; X and Z size `24 wx` px (shared), Y size `16 hy` px; `d = hit - origin`; `d >= 0 -> d < size`, `d < 0 -> -d < attackerSize + 1`; zero mask -> 0 at once; byte 6 unused. Hero box at tick 5: X 233, Y 88, Z 1 raw (0.00002 px), sizes 29 x 15 x 15 stored `(n<<16)-1`. IN rect (9,5,0) 2x2 + hero-only mask `0x0008` -> 1; typeIdx 3 -> 0; mask `0x0040` -> 0; mask 0 -> 0; rectangles right / left / below / above / z-above -> 0; boundaries d = 0, size-1, -asize -> 1, d = size, -asize-1 -> 0 on X, Y and Z (Z uses the X size) |
| `typeIdx` 8..11 | | the bytes after the 8-byte table (`0x80098FAC..`): **`58 D1 03 80`**. Index 10 behaves as nibble 3 (arrow): `0x87` / `0x95` return 1 for an attack with nibble 3 at typeIdx 10 (and 2); indices 8, 9, 11 (0x58, 0xD1, 0x80, above 0xF) never match; with a nibble-4 attacker all four give 0 |

## 7. Checks of the oracle itself

- **D1** (`out/d1/comparison.txt`): S-A and S-B equal to the annex, 216 values each, 0 differences.
- **Independent models** (`tools/kmodel.py`, written from the D4 / D2 / E20.c1 reports and the arithmetic of the disassembly, sharing only EXE tables and BALANCE.BIN with the oracle): the inventory -> records -> sheet pipeline (12 inventories, 99 + 99 + 7 variants, 4730 response triples), the damage formula (every line of K2: HP after the call), the search and the three predicates (every case of K3). **No difference.**
- **Two interpreters** (`out/diff2.txt`): `emu.py` and `mips.py` (via `interp2.py`) give the identical output text for K1 (366 lines), K2 (395) and K3 (110); 2605 calls of binary functions through each. K1 includes the full dependency analysis and the sweep on both.
- **Teeth** (`out/mutations.txt`): 17 one-word patches with the expected change written beforehand in `tools/kmut.py` (equipment term, min-1 rule, kill test, plain-response test, shift, attack-index bound, hero-sheet selector, attack source, record [0] category test, 0x80 branch, record slot, `0x86` store, `0x87` nibble compare, `0x95` mask test / typeIdx byte / Y size, `0x71` offset), all CAUGHT (predicate holds and the independent model disagrees); 3 controls (a debug-print read, the spark count twice) change nothing.
- **Coverage** (`out/coverage.txt`): every instruction of `0x71`, `0x86`, `0x87`, `0x95`, `0x8004E030`, `0x8004E0F8` executed. Not executed: the `[0x800DC058] < 0` printf branches of `ComputeNewHp` (`0x8003A3E0-0x8003A524`) and `ResolveBalanceTarget` (`0x80044660-0x80044678`, `0x80044790-0x800447C8`, `0x800447DC-0x80044808`);
  in `UpdateItemEffectState` the regeneration / HP part (`0x80030C88-0x80030D28`, E20.c2) and the copy path for an unaligned hero record (`0x80030A78-0x80030AC8`, unreachable: the hero record sits at the aligned blob offset `0x400`); in the runner the program-start path and the letter A / D / E cases; in the search the codes 3..11.
- **Load delay** (`out/load-delay-check.txt`): no executed load is followed by a use of its register (both interpreters ignore the MIPS-I load delay slot).
- **Unwritten bytes**: no read of an uninitialised byte in any scenario (the machine reports them; K1 found the pad word, section 3).

## 8. Limits and open issues

1. The targets are inert Beannoids: nothing in K2 comes from a real D handler (`0x8007D9A4`); `ComputeNewHp` is called at the end of the hit tick on copies, equivalent to tick N+1 before the counters pass (the hit, the latch and the equipment are unchanged between them; c1 notes section 5).
2. Level D = 0 only (map 14). Every equipment record has a single record at level 255, so D does not change them; the Beannoid record 335 is a single record. Chains (13 sprites) are not exercised.
3. The hero's damage entry (`0x80031A68`, the hero as target inside `MovePlayer`) is not run; K2 HT calls `ComputeNewHp(hero)` directly with `TouchingEntity` written by the harness.
4. The regeneration / HP part of `UpdateItemEffectState` (E20.c2) and the boots / item-use driver `0x8003634C` are not exercised.
5. K3 covers the handlers on a synthetic state; the real lever programs (115 NW lever D `ProgramIndexes[3] = 129`, 116 levers, 117 bascule, 44 levers) are not run (they need the map event tables and the spawner of those maps; their reactions are DLL work of E20.c1b, not part of this oracle).
6. One field (flat), one weapon animation per inventory, east only; the item names are INFERENCE (section 3).
7. `0x95`'s boundary lines write the hero HitBox (synthetic geometry on the real handler); the rest of K3 reads boxes produced by the real animation and physics passes.
