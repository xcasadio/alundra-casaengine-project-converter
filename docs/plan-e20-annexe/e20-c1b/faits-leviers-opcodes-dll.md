# Levers, opcodes and DLL facts for E20.c1b

> Report returned by one agent of the E20.c1b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c1b facts (read-only discovery, 2026-10-09)

Labels: FACT (decoded or read, with file:line or address), INFERENCE, UNKNOWN. Paths are under `Alundra/Scripts/` unless stated. Binary addresses were re-decoded with capstone on `ALUN_CD.EXE`. I wrote no file.

## 0. Things that change the plan

1. **FACT: the scripted lever reactions need only the hit pass plus bytecode D.** No lever D calls `ComputeNewHp`, and the levers' MaxHp 1 is never used. They need no damage code.
   - Of the opcodes reached from the lever entry points, `0x86` (115 B[2] and 44 B[5]/C[35]), `0xB9` (117 B[1]) and `0x87` (362 D[1]) are not implemented by the DLL. Only 362's `0x87` is on a lever or lid path. The `0x86` sites are scene invulnerabilities.
2. **FACT: `AlundraBalanceTables.Select` (`AlundraBalanceTables.cs:219-224`) remaps indices `0x1F..0x7F` to `0x1E`.**
   - Equipment is entry `n+0x1E`, which spans exactly `0x1F..0x7F` for `n` in 1..0x61. Calling `Select` would collapse every item onto entry 0x1E.
   - The binary's `0x800445C0` has no remap: it guards `n < 0x62`, reads the offset table at `0x80143E34 + 2n` and walks the level chain. c1b needs a separate `SelectEquipment(n, threshold)`.
3. **FACT: the item category table is already exported.** It is `Data/item-drop-properties.json`, column index 2 (`Field3`), exposed as `AlundraItemTables.DropField3` (`AlundraItemTables.cs:138`).
   - I checked all 98 rows against EXE `byte[0x800C5F7C + 8n + 6]`: 0 differences. Category = `DropField3[n] & 0x7F`; bit 0x80 is the new-game unlock.
4. **FACT: the 0x87 corpus labels in the discovery reports are off.**
   - `typeIdx` goes through the table `[1,2,3,4,5,5,6,7]` at `0x80098FA4`, so typeIdx 1 gives nibble 2 (Hammer), not Sword. The sword is typeIdx 0.
   - Sword sites: 0x87 in maps 77, 136, 304, 352, 429, 430; 0x95 in maps 41 (2 sites), 42 (3 sites) and 226.
5. **FACT: c1a already made every bytecode D that contains `0x87` reachable by a sword hit.**
   - A hit now sets `TouchingEntity`, so those D programs run. `0x87` is skipped by size (`UnknownOpcode`, `AlundraEventProgramRunner.cs:2969`) and `Result` keeps its stale value.
   - 181 D-sites in 40 maps are affected, so c1b changes behaviour well beyond the levers. Implementing `0x95` also stops a stale `Result` in 43 sites.

## 1. Lever programs

All offsets are code indexes (`Codes[]`). Records use half-tile XPos/YPos; tiles below are DisplayX/Y. Hit gating: a lever is ClassA because sprite flags are `MoreFlags` 1 (`Data/sprite-records.json`). The hero has `MoreFlags` 140 (HitsClassA and ClassB), so the pass mask is bit 0. The levers' `D` is bytecode, since index `& 0x7F != 0`.

**Balance responses (`Data/balance.json`).** The nibble-k response is `DamageResponses[k-1]`.

| Entry | Sprite | Nibbles 1-7 | Nibbles 8-11 |
|---|---|---|---|
| 305 | levers (49, plus 0x100) | 16 | 128 (immune) |
| 308 | bascule (52, plus 0x100) | 16 | 128 (immune) |
| 243 | lid (362) | 128 for all except nibble 4 = 16 | 128 |

- The sword (attribute 129, nibble 1) reads 16, so the lever is touched.
- All three chains are a single level (255), so the threshold `D` is irrelevant.
- The lid is touched only by nibble 4 (bomb). The sword never sets its `TouchingEntity`.

**115 NW lever, rec 2** (tile 2,7; C=D=129):
- D[1] @1476: `0x2B` (clears ClassA, so one-shot), `T2` on.
- C[1] @1200: wait `T2`; `T2` off; anim 2; wait anim end; anim 1; `T1` on.
- B[1] @128, map event 0: `0x85` at load, then wait `T1`; sound 222; `0x85` frames 3, 5 and 7 with 20-tick waits onto (4,6).
- Result: the door frames toward portal 12 (tiles (4..5,6), to 116 tile (3,20)) animate.

**116 levers, rec 39 and 40** (tiles (35,26) and (45,26); C 151 and 152 give C[23] @692 and C[24] @736; D 133 and 134 give D[5] @1076 and D[6] @1084):
- D: `0x2B`; `0x32` toggle `T1` (rec 39) or `T2` (rec 40); `T101` (rec 39) or `T102` (rec 40) on.
- C[23]/C[24]: set ClassA each tick until `T101`/`T102` is on. Then clear ClassA, play anim 3 then 0 (flag on) or anim 2 then 1 (flag off), clear `T101`/`T102`, loop. The lever is re-hittable.
- B[1] @152: wait `T1` and `T2` both on; control lost; sound 221; five `0x85` steps at 8 ticks each extend the bridge toward dest (40,20..24). It retracts when either flag drops.
- Plates rec 7 and 8 (C[5] @400, C[6] @436) also set `T1`/`T2` for 60 ticks, so the plates share these flags.
- The gate A[1] @128 opens the grate (rec 6) only when `0x2C [0]` and `0x2C [1]` hold: Beannoides rec 0 and 1 are gone.

**117 bascule, rec 0** (tile 21,11; sprite 52; flags 0x81; A=C=D=129):
- A[1] @132: set ClassA, clear `T2..T5`, `T1` on.
- D[1] @1184: clear ClassA. Cycle `T1→T4→T2→T3→T1`, then `T5` on.
- C[1] @444: while `T5` is off, set ClassA each tick. When `T5` is on, take the branch of the new state: `0x3A` direction, anim 1, wait, anim 0, `T5` off, sound 224, `0x85` platform rewrites with 5-tick waits, ClassA on.
- Also rec 22, a lever: D[23] @1240 sets `T230`; C[23] @1104 plays the anim and sets `T231`.
- The map folder is named "Tarn's Manor (unused broken)-117", yet it is on the chain: 115 portal 7, 116 portal 0.

**44 levers, rec 7-12** (tiles (5,27), (17,29), (15,58), (27,23), (44,56), (38,25); all D 136 give D[8] @2320):
- D[8]: if `G1633` is off, end. Otherwise `T80` on, control lost, `0x63` clears flag bit 1 (ClassA) on rec 7-12.
- C[8] @1556, on rec 7: wait `T80`; `0x59` anim 2 on all six; `T81` on; wait `T10..T70`; `0x62` re-sets ClassA; phase two (anims 3 and 0) follows. `0x49` Restart ends it.
- Visible result: seven stair trap-doors (rec 0-6, sprite 72, C[1..7] @1160..) walk 48 and 32 px with sound 25.
- `G1633` is set at B[2] @321, after the intro scene.

**362 lid, rec 0** (tile 8,6; sprite 243; flags 0xA1): D[1] @728 is `0x87 [128,3]`; if true, `T6` on and `0x19` deactivate.
- Search 128 means the logic entity itself (`EntitySearchService.cs`, function id 0).
- `T6` releases B[4] @168. Camera sway, four Muruta (rec 6-9) spawn, then dialogue 133, then `T7`.

**Opcode coverage today (`Dispatch`, `:562-2026`): every opcode in these programs is implemented except these.**
- `0x86`: 115 @242 `[22, 600 ticks]` and @248 `[22, 0]`; 44 @940 `[16, 120]`, @2268 `[33, 30]`.
- `0xB9` (`Start CD streaming`): 117 @171, in the Livre scene, skipped by size.
- `0x87`: 362 @728.
- Nothing else is skipped in 116.
- `0x85`, `0x54`/`0x55`, `0x32`, `0x2A`/`0x2B`, `0x62`/`0x63`, `0x59`, `0x3A`, `0x1C` and `0x49` are all present. `0x85` is wired through `CellMutator` (`:1393`).

## 2. Reaching each lever with the hero free

**Arc mechanics (FACT).** `ArcSpec(name, zone, world, int[] flags, tileX, tileY, tileZ, frames, RealController: true, Prefabs: true)` plus `ArcRun.PlaceHero` or `Arrival` place the hero anywhere. `Alundra.Tests/AlundraHitArena.cs:60` is the template, hard-wired to map 14. There are no existing arcs for 115, 116, 117, 44 or 362.

**Test saves (FACT).** `AlundraTestSaves.Presets` has only `day3-after-dream` (179) and `day4-meeting` (185). A preset takes a map, tile, flags below 2048, map-table entries and item counts, and refuses temporary flags (`AlundraTestSaves.cs:36-70`).

| Lever | DLL arc | In game (author) |
|---|---|---|
| 115 NW | Direct placement, no flag needed. | Reachable via portal 2 (5,42) once the hall is open. INFERENCE: a preset on 115 with `G569` skips the Muruta. |
| 116 | Direct placement. | INFERENCE: behind the grate (22,21), which needs both Beannoides dead. Their native D is a counted no-op until E14.a. |
| 117 bascule | Direct placement. | Entered from 116 portal 0 or 115 portal 7, so also after E14.a. |
| 44 | Direct placement; arc flag `1633`. | UNKNOWN whether the levers are reachable before the Beannoide wave in B[3] @328. |
| 362 lid | Test attacker with nibble 4. | Bomb (E20.h, E12); a sword cannot touch it. |

**115 hall gating (FACT).**
- B[4] @684 sets the three load states: `G659` all open; `G569` leaves doors (10,44), (11,44), (5,42) walkable and (16,42) closed; `G262` all closed; none falls to the open state at 699.
- B[2] @172 exits if `G569` is on; with `G262` on it jumps to the Muruta wait @381.
- G1650 is map 15's first-arrival scene flag (B[1] @72/@114), not a lever-room gate.
- 116's gate needs `E14.a`; presets cannot carry local map state.

## 3. Opcodes 0x71, 0x86, 0x87, 0x95

**Binary (FACT, re-decoded).**
- `0x71` (`0x8003FA10`, size 1): `Result = HitCounter (+0x220)`.
- `0x86` (`0x8003FFD4`, size 4): `[search, lo, hi]`; for each match `+0x1CC = lo | hi<<8`. This is a SET. The DLL table names it "Add" (`EventOpcodeSizeTable.cs:182`).
- `0x87` (`0x80040048`, size 3): `[search, typeIdx]`. `Result = 1` iff some match has `+0x224` non-null, and that attacker has `+0x1D4` and `+0x1C8` non-null, `attack[0] != 0` and `(attack[0] & 0xF) == byte[0x80098FA4 + typeIdx]`.
- `0x95` (`0x800407C0`, size 10): `[x, y, z, wx, hy, _, maskLo, maskHi, typeIdx]`.
  - Origin X = x·24 px, Y = y·16 px, Z = z·16 px. Size X = Z = wx·24 px, Y = hy·16 px. Byte 6 is unused. Zero mask gives `Result 0`.
  - Scan slots 0..max: status 2 or 3, `+0x20 == 0`, box, attack with non-zero attribute, `Flags & mask != 0`, nibble equal, `HitBox` overlap with the same widened test. First hit gives `Result 1`, else 0.

**Indices 8-11 (FACT).** Bytes at `0x80098FAC..B3` are `88, 209, 3, 128` (the start of a pointer table).
- Index 8 gives nibble 88, 9 gives 209, 11 gives 128: never equal to a nibble 0..15.
- Index 10 gives nibble 3, which equals Arrow, like index 2.
- Only map 226 uses 8-11 (11 sites, Poulet rec 14, C[14] @2113-2243). The DLL needs a 12-byte table `[1,2,3,4,5,5,6,7,88,209,3,128]`.
- `0x87` never exceeds typeIdx 7.

**DLL changes.**
- Add the cases in `Dispatch`:
  - `0x71` near `0x47`/`0x48` (`:746-753`);
  - `0x86`/`0x87`/`0x95` near `0x2C` (`:1853`) or before `default` (`:2026`);
  - unchanged return sizes 1/4/3/10.
- `0x86`: for each `EntitySearchService` match, `DamagedTickCounter = v1 | v2<<8`. The counters pass decrements it; the hit pass skips non-zero targets.
- `0x87`: for each match, `m.TouchingEntity?.GameplayProxy is AlundraEntityScriptProxy a`. The test is `a.HasFrameBox && a.CurrentAttack is {Attribute: != 0} c && (c.Attribute & 0xF) == nibble`.
  - FACT: `HasFrameBox` is refreshed by `EvaluateFrameBox` in the hit pass of the hit tick. Programs of the next tick run before the clock, so it is still valid.
- `0x95`: scan `_worldContext.SpawnedEntities` (the hero is first), not `_hitList` (stale statuses). Per candidate:
  - require `Status.IsActive()` and `BlockedByEntity == null`;
  - require `Flags & mask != 0` and an attack with non-zero attribute whose nibble matches;
  - call `EvaluateFrameBox()` and use `Pos + CollisionOffset`;
  - use size `CollisionWidth`/`CollisionDepth`/`CollisionHeight`.
- Z comparison for `0x95` (ADR-0026): the rectangle origin is absolute binary Z, and DLL Z is binary minus 1. Use `HitBoxZ = PosZ + 1 + CollisionOffsetZ` against the rectangle only.

**Corpus (my linear census over `EventCodes`).**

| Opcode | Sites | Maps | Letters |
|---|---|---|---|
| 0x86 | 76 | 24 | C 39, B 37 |
| 0x87 | 182 | 40 | D 181, B 1 (map 13 B[2] @132, search 27) |
| 0x95 | 43 | 30 | C 34, B 9 |
| 0x71 | 0 | 0 | none |

- 0x87 typeIdx counts: 3 → 32, 1 → 24, 6 → 56, 2 → 6, 0 → 20, 4 → 2, 5 → 1, 7 → 41.
- 0x95 outside map 226: typeIdx 3 in 27 sites (masks 0x0808 or 0x0809), typeIdx 0 in 5 sites (maps 41 and 42).
- Story-chain 0x95: map 10 @6418, map 14 @494, map 15 @902 (all bomb, mask 0x0808).

## 4. Equipment records

- **Stub (FACT):** `AlundraPlayerManager.cs:583-587` (`UpdateItemEffectState(player)`), called at `:463`. `MovePlayer` has `state` and `host` in scope (`:172`).
- **Binary routine (`0x800307E8`, decoded):**
  1. Boots level to `0x80127000` from counts of items 0x1C, 0x1B, 0x1A.
  2. Clear the six words at `0x80146E00..14` (pointer and id+0x1E for each of three records).
  3. Record 0 is the armor: scan `n = 0x61..0`, the first owned item with category 2.
  4. Record 1 is the weapon: `GetItemIdFromCurrentWeapon`; id in 1..0x61 and category 1.
  5. Record 2 is the ring: `SetItemIdFromCurrentItemId`; id in 1..0x61 and category 3.
  6. The 255-byte copy to `0x80127008` and the response combination.
  7. Regeneration and the HP copies.
- **Map load (FACT):** `InitializeItems` (`0x80044520`, also stores the threshold) zeroes the three pointers at every map load. c1b must clear them at world initialisation.
- **Readers (FACT, lui/addiu scan of text up to `0x80098000`):** `0x80146DF8` is written at `0x80039348` and read only at `0x800446DC` (ResolveBalanceTarget). The records are read only by UpdateItemEffectState and ResolveBalanceTarget. The id words are read only in debug branches.
- **Item selection (DLL, existing):**
  - `GetItemIdFromSlotId` (`AlundraPlayerManager.cs:1159`): first owned match returns when flag bit 0 is clear, else a strictly higher priority replaces it.
  - `SetItemIdFromCurrentItemId` (`:1248`) already rewrites `PlayerStats.ItemId` (the `0x8004E51C` store, inside `SetCurrentItemId`) and returns the original id. It already runs from the HUD every frame.
- **Data:** entries 31 (item 1) to 86 are in `balance.json`.
  - Items 1-4: attack attribute 1, power 1/2/4/8; item 8: attribute 6, power 50; items 0x33 and 0x34: power 3.
  - Armors have no attack. Responses are 16/12/8/4, with 0x40 flags on weapons.
  - All are level 255, a single link.
- **Damage terms (FACT):** `ResolveBalanceTarget` adds, for each non-null record, `Attacks[g+1 < count ? g+1 : 0].Power`, with `g` the latch, when `attr & 0x80`. A new game adds +1 from item 1.
- **c1b ports:** steps 2-5 for all three records, plus the map-load clear and `SelectEquipment`. Filling record 0 costs nothing and c2 needs it as the defence base.
- **c2 keeps:** the `0x80127008` copy and combination, boots level, regeneration, HP mirror, and the driver `0x8003634C` (D-E20-14).
- **UNKNOWN (P4):** the binary scans slots 0..0x7F in `GetItemIdFromSlotId` (`0x8004E248`), reading properties and counts unbounded (`lh 2(base+4n)`). The DLL scans 100 rows with `ItemsCount` 99. Only matters if a row above 99 held a matching slot and a count.

## 5. Pure functions: inputs and homes

**ResolveBalanceTarget(attack, sheet, hp)** `0x8004464C`:
- null sheet, null attack or attribute 0 returns `hp`.
- `r = sheet[2 + (attr & 0xF)]`.
  - `r & 0xC0 == 0x40` returns 0.
  - `0x80` or `0xC0` returns `hp`.
  - Otherwise `dmg = ((attack[1] + equipment powers) * r) >> 4`, at least 1. Result is `dmg < hp ? hp - dmg : 0`.

**ComputeNewHp(e)** `0x8003A374`:
- `t = e.TouchingEntity` with no null check; `sheet = (e == hero slot 0x80127D30) ? 0x80127008 : e.+0x1C4`.
- It calls `ResolveBalanceTarget(t.+0x1C8, sheet, e.Hp)`, stores to `e.Hp`, returns `newHp == 0`.

**DLL inputs:**
- Target record: `BalanceRecord` (`AlundraEntityScriptProxy.HitPass.cs`), by `ByteAt(nibble + 2)`.
- Attacker: `e.TouchingEntity?.GameplayProxy` as `AlundraEntityScriptProxy`, then `.CurrentAttack` (`Attribute`, `Power`).
- Latch: `HeroAnimationLatch` on `AlundraHitPass`, `AlundraWorldProxy.cs:2766-2769`, internal today.
- Equipment: the three records.
- Hero sheet: `0x80127008` is c2's. The cleanest way is a synthetic `AlundraBalanceRecord`.

**Proposed homes.**
- New `internal static class AlundraDamage` with the two pure functions. Equipment and latch are passed in, so they are testable with no world.
- A small `AlundraEquipmentRecords` (three `AlundraBalanceRecord?`) owned by `AlundraWorldProxy` beside `_hitPass`, exposed by a default-null seam, like `BalanceTables` (`IAlundraScriptHost.cs:113`) and on the interface the runner sees (`IEntityWorldContext`).
- E14.a native D handlers live in `RunSpriteEvent` (`AlundraEventProgramRunner.cs:~290`) and use the seam through `_worldContext`. E20.c2's hero entry in `MovePlayer` uses `host`.
- A hit at tick N is read by D at tick N+1, after `MovePlayer`, so the records are fresh and the latch is the one from the pass.

**Oracle (D2).**
- `docs/plan-e20-annexe/d2-oracle-coups/out/cases.txt` and `cases.json` carry the damage cases: 3 (0xC0), 4 (minimum), x6 (equipment sum), x7 (hero sheet), x8 (0x40 kill) and x9 (latch).
- `random.txt` holds only statistics. `tools/model.py` is the independent model for vectors.

## 6. Stale text to fix

- `AlundraEventProgramRunner.cs:747` ("HitCounter stays 0 until E14 writes it").
- `AlundraWorldProxy.cs:2727` ("0x86 and 0x87 are not implemented").
- The "Add" label of `0x86`, which the binary shows as SET.
