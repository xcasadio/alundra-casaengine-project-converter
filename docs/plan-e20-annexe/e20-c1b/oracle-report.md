# Damage, equipment and opcode oracle (summary; full notes in oracle/notes.md)

> Report returned by one agent of the E20.c1b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

The oracle is built and passes. `run_c1b.py` exits 0: D1 re-validates (S-A and S-B, 216 values each, 0 differences) and every K check passes. Two full runs gave identical manifests.

**Stray file to remove.** I wrote one scratch output to `D:/tmp_k1.txt` by mistake and, under your no-delete rule, left it. Please delete it.

**Run:** `python -B D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-c1b/oracle/tools/run_c1b.py` (about 95 s). Everything is under `.../docs/plan-e20-annexe/e20-c1b/oracle/`: `notes.md`, `out/` (`summary.txt` has the key values, plus `K1.txt`, `K2.txt`, `K3.txt`, `diff2.txt`, `mutations.txt`, `coverage.txt`, `manifest.txt`) and `tools/`.

**Setup choices**
- Inventories are built with the real new-game code (`InitPlayerStats`, `AddItem`, `SetPlayerWeaponId`, `SetCurrentItemId 0x8004E4D8`, the item setter).
- Steel Edge is item 2 and Holy Sword Elnard is item 4. That mapping is an inference from the E20.b notes.
- World is the c1 one with Beannoid targets T, T2, A, V, U from the real spawner. The hero swings at tick 1 and hits at tick 5 in every inventory.
- Damage is checked on every line against an independent model, and everything is re-run on `mips.py`. Output is identical (2605 binary calls each).
- 17 one-word patches of the binary are all caught; 3 controls change nothing.

**K1: records and hero sheet**
- Each record is a blob pointer plus a word holding item id + 0x1E (0 = none).

| Inventory | Record [0] | Record [1] | Record [2] | Sheet responses |
|---|---|---|---|---|
| kit | blob+1951, word 47 (item 17) | blob+1701, word 31 (item 1), power 1 | null | `10` ×11 |
| steel | same | blob+1718, word 32, power 2 | null | `10` |
| holy | same | blob+1752, word 34, power 8 | null | `10` |
| ring `0x33` | same | item 1 | blob+2461, word 81, power 3 | `10` |
| leather `0x12` | blob+1966, word 48 | item 1 | null | `0C` ×11 |
| no weapon | item 17 | null | null | `10` |

- The pointer is `0x80143DF8` + blob offset. Boots level is 0 everywhere.
- Records depend only on the inventory, not on any record byte. Record [0] is the highest-numbered owned category-2 item. Record [1] is the item `0x8004E030` returns for the weapon slot. Record [2] is the accessory item, taken before `0x8004E0F8` rewrites the equipped item id.
- The sheet is a 255-byte copy of record [0] (the following blob records come along). If record [0] is null it copies the hero's own `+0x1C4` record instead. Bytes 3..13 combine records [0], [1] and [2], and all other bytes depend only on the same byte of record [0]. The combination rule was confirmed on 4730 byte triples.
- With rings `0x33` and `0x34` both owned, the first call uses `0x33`, then the equipped id becomes `0x34`.

**K2: `ComputeNewHp` on a Beannoid (response 0x10)**
- Beannoid HP is 9 or 100. Damage is `((power + equipment) × response) >> 4`, minimum 1. Response `0x40` kills, and `0x80` and `0xC0` leave HP unchanged.

| Inventory | Hero swing | Natural hit, HP 100 / HP 9 | Responses 0 / 0x40 / 0x80 / 0xC0 (HP 100) | Attack without bit 0x80 |
|---|---|---|---|---|
| kit | anim `0x10`, power 1, latch 16 | 98 / 7 | 99 / 0 / 100 / 100 | 99 (power 1), 84 (power 16) |
| steel | anim `0x3F`, power 2, latch 63 | 96 / 5 | same | same |
| holy | anim `0x49`, power 8, latch 73 | 84 / 0 | same | same |
| ring | anim `0x10`, power 1 | 95 / 4 | same | same |
| leather | anim `0x10`, power 1 | 98 / 7 | same | same |
| no weapon | anim `0x10`, power 1 | 99 / 8 | same | same |

- The latch-16 and `Attacks[0x11]` pair you specified only holds for kit, ring, leather and no weapon. Steel Edge and Holy Sword swing with other animations, so their own attack power and the record power both count: 2+2 and 8+8. I report a forced `0x10` state next to the real one for those two; steel gives 97 and holy gives 91 at HP 100.
- A non-hero attacker with attribute `0x81` and power 1 still gets the hero's equipment added: kit 98, steel 97, holy 91, ring 95, no weapon 99. The binary reads the global records at `0x80146E00` and the global latch for any attacker, and never tests who attacks.
- All 22 real equipment records have AttackCount 0 or 1, so the latch value never matters in practice. A synthetic 3-attack record shows the index rule: latch 16, 2 and -1 pick Attacks[0] (+2), latch 0 picks Attacks[1] (+3), latch 1 picks Attacks[2] (+5).
- With the hero as target, the response is read from the sheet at `0x80127008`. Kit with a power-16 attack takes 16, or 17 with attribute `0x81`; leather (response `0x0C`) takes 12 either way.

**K3: opcodes**
- Layout from the runner at `0x8004205C`:
  - Self pointer: owner `+0x230`
  - State struct: owner `+0x234`
  - Program-counter cell: owner `+0x238`
  - Result word: owner `+0x260`
  - Handler call: a0 = self, a1 = owner, a2 = pc cell, a3 = state
  - Handler address: word at `0x80098FAC + 4×opcode`
- Every case ran twice, once with the handler called directly and once through the real runner. Both gave the same result, fields written and size.
- `0x71` (size 1): Result is the self entity's HitCounter. Hero 2, A 1, all others 0.
- `0x86` (size 4): sets (not adds) `+0x1CC` = lo | hi<<8 on every match and leaves Result untouched. `[0, 0x2C, 1]` gives `0x12C`. Two entities sharing id 0 both get the value. An id with no entity changes nothing. A preset of `0x100` followed by value 2 gives 2.
- `0x87` (size 3): T touched by the hero's sword (nibble 1) gives 1 for typeIdx 0 and 0 for typeIdx 1 to 7. V touched by the nibble-4 attacker gives 1 for typeIdx 3 and 0 for typeIdx 0. An untouched U gives 0 for both.
- `0x95` (size 10): the hero's box inside the rectangle gives 1. Outside on X, Y or Z gives 0. A zero mask gives 0. Edge cases on all three axes are right. Z shares the X size.
- Indices 8 to 11 read the bytes after the 8-byte table: `58 D1 03 80`. Only index 10 can match, behaving as nibble 3 (arrow).

**Not covered:** the targets' own D handlers, the regeneration part of `UpdateItemEffectState` (E20.c2), and the real lever programs of maps 115, 116, 117 and 44.
