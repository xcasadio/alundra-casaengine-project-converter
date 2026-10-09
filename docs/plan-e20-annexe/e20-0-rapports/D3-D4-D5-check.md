# Check of D3, D4 and D5

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| **D3** attacker gate: box +0x1D4, attack +0x1C8, attr byte ≠ 0; power never tested | CONFIRMED | 0x80039364 `lw 0x1d4`/`beqz`, 0x80039374 `lw 0x1c8`, 0x80039384 `lbu 0(v0)`/`beqz` | – |
| Class mask `(F>>2)&1 \| (F<<2)&8 \| 0x800 if F&0x1000`, target `Flags&mask` | CONFIRMED | 0x8003939C–0x800393C0, 0x80039450–0x8003945C | The target gate also skips targets with +0x1D0≠0, +0x1CC≠0 or +0xB4&0x40 (0x8003941C–0x80039448) |
| Hero flags 0x3118C → ClassB only | CONFIRMED | map_alundra SpriteRecords[0]: MoreFlags 0x8C, CanPickup 0x11, byte 0x12 = 3. Composition at 0x80039DA4–0x80039DD0. No direct global store to 0x80127D9C in the EXE | – |
| CurrentAttack rule (count 0 → none; `anim+1<count` → rec+0x11+2·anim; else rec+0xF) | CONFIRMED | 0x80038B74–0x80038BAC | – |
| Collision index 0xFFFF → no box | CONFIRMED | 0x80038C2C `ori 0xffff` / `beq` → 0x80038CB8 `sw zero,0x1d4` | – |
| Balance choice: remap 0x1F..0x7F→0x1E, first record with level ≥ [0x80143DF0]; threshold stored only at 0x80044524, sole caller 0x8002C3A0 passes `lbu 0xb` | CONFIRMED | 0x80044550–0x800445BC. An EXE-wide scan finds one `sw …,0x3df0` and one `jal 0x80044520` | The remap has no effect: record 0x1E has count 0, and no common-bank HitsB sprite has an index in 0x1F..0x7F (HitsB sprites: 2, 150–153, 238, 239, 251, 252) |
| +0xB = exported `Info.D` | CONFIRMED (consistency) | GameMapInfo.cs reads sequentially: u32 MapId (map 14 = 14), Gravity +4, … D +0xB. The binary's loads through [0x800E4334] are `lhu 4`, `lb 8`, `lbu 9/a/b/c` | Not "BalanceLevel" (+9). If +9 were used, map 14 would select level 2 |
| Per-map sprite index +0x100; spawn animation 0; auto-spawn when `dir&0x40` | CONFIRMED | 0x8003A304 `addiu a2,0x100` (an add, equal to OR here); 0x8003A320 `sw zero,0x24(sp)`; callee frame 0x30, so 0x80039D28 `lw t0,0x54(sp)` reads it; 0x80039D70 `sw →0x68` | – |
| Hero balance idx 0; record 0 responses all 0, so the hit pass never treats the hero as immune | CONFIRMED | 0x8003199C `sw zero,0x10(sp)` → 0x80039D70 → 0x80039E38 (the only `sw …,0x1c4` in the EXE). Raw record 0: `ff a9 0a 00×11 4d…`. The immune test at 0x800395D4 reads +0x1C4 | ComputeNewHp uses 0x80127008 for the hero (0x8003A3A0), so equipment immunity (0x80) applies there, not in the pass |
| Per-map table (records, attrs, powers, HP, levels, box counts) | CONFIRMED (spot-checked) | Raw BALANCE.BIN: 335 `01 01`, 341 `02 04`, 347 L1/L8/L255 HP 3/15/15, 238 count 2 `(06,01)(01,01)`, 252/310/311 chains, 459 HP 42, 2 `(04,05)`. Map 14 entities: 335×7A, 455×3A, 392×2A, 238×1A, 252×3s. Beannoid boxes 4/4, 8/8, 11/11, 1/13, Up-only on a7. Denominators exclude the terminator frame (IsTransitionFrame, offset −1) | – |
| Corpus totals 483/9741/2642/2600/211/1572/1028/2346/2402/2512 | CONFIRMED | My own recomputation from raw BALANCE.BIN, Info.D and `CollisionData != null` matches exactly | – |
| "The pass does four things on contact" | REFUTED (incomplete) | 0x800395F4–0x80039664: if not immune, it also spawns effect 4 on the target for attr&0xF 6 or 10, and effect 5 for 7 or 9 (`jal 0x8003BE74`, a3 = target) | The bramble's a1 is Fire (attr 6), so its touch also spawns effect 4 on the hero. There is a debug-only print at 0x80039524 |
| 0x1D0 = 0x19 blocks hits for 25 ticks | CONFIRMED (if the pass runs once per tick) | The only store is 0x80039670. 0x800389CC–0x80038A18 decrements +0x1D0 and +0x1CC per entity and zeroes +0x220/+0x224 | – |
| "Only 0x80031A68 reads the hero's TouchingEntity" | REFUTED | `lui 0x8012; lw 0x7f54` at 0x800667F8, 0x80066E58, 0x8006F8B8 and 0x8007D2B0. These are native handlers that compare it to self and react (0x8006F8B8 sets +0x88=1, Flags\|=0x40; 0x8007D2B0 calls 0x8003A59C) | "No HP loss before c2" stands. Some enemies react to touching the hero once E14 ports them |
| **D4** sole caller 0x80032914; runs every MovePlayer tick | CONFIRMED | jal scan. The only `jr ra` after 0x80031B50 is at 0x80032954. The four `jr v0` load from tables at 0x80022D1C, 0x80022D3C, 0x80022E7C and 0x80022FBC. The hurt path jumps to 0x80032914 | – |
| Boots → [0x80127000] stored at 0x8003082C/848/864/88C | REFUTED (incomplete) | Also `sw zero,0x7000(v0)`, v0 = 0x8012xxxx, at 0x80031730 inside the init routine 0x80031700 | Add the init store |
| Categories 1/2/3 and the record fill rules | CONFIRMED | Table dump at 0x800C5F7C+8n+6. Code at 0x80030868 and 0x800308AC–0x800309A0. WeaponId k maps to slot k (0x8004E268…0x8004E308 pass a0 = 1..6) | – |
| GetItemIdFromSlotId: highest priority; returns immediately if flag bit 0 is clear; 0x80 items | CONFIRMED with a nuance | 0x8004E1F4–0x8004E248 | Only the first owned match is tested for the early return. Later items compare priority only (`slt best<new`). No bound against [0x8017638C] |
| 0x8004E0F8 rewrites stats+0xC and returns the old ItemId | CONFIRMED | 0x8004E10C, 0x8004E140 → 0x8004E51C `sh a1,0xc` | – |
| Equipment raw bytes (powers 1/2/4/8, item 8 `(06,0x32)`, armor 10/0c/08/04, Nava 0x80 at index 6, 0x37/0x38 byte 2 = 200) | CONFIRMED | Raw BALANCE.BIN entries n+0x1E, all L255 with chain length 1 | – |
| Response-combination algorithm; the new-game kit gives 0x10 | CONFIRMED | 0x80030B40–0x80030C18. A and B default to 0x10, initialised once before the loop | When b has no flags, the start value is b itself |
| Regen gates and `MaxHp < counter` | CONFIRMED | 0x80030C24–0x80030D3C (`slt` at 0x80030CD4). C# PlayerManager.cs:1311 uses `<=` | – |
| C# scans 100 items vs binary 0x80 | CONFIRMED | PlayerManager.cs:4382, 0x8004E248 | – |
| c1 damage formula | CONFIRMED but incomplete | 0x8004464C–0x80044818 | ComputeNewHp also branches on the target response flags: **0x40 → returns 0 (instant kill)**, 0x80/0xC0 → HP unchanged, 0 → normal. Damage = max(1, (P·r)>>4), HP floored at 0. The combined table can yield 0x40, so E20.c2 must port all three branches |
| **D5** driver semantics, jump table, counter `[0x8012715C]`, clear on v1≠0 | CONFIRMED | 0x8003634C–0x800364C0. Table at 0x800235F4 decoded (indices 0, 1, 4, 12–19) | – |
| Hurt path: zeroings in caller, driver ticks before `sw 0x88`; second site 0x80031CC4 | CONFIRMED | 0x80031C58–0x80031C84, 0x80031CA0–0x80031CC4 | Also missed: the hurt path sets hero+0x8C = return of 0x80031A68 and **+0x1CC = 0x78**, then skips to 0x80032914. +0x1CC≠0 excludes the hero as a target (0x8003942C) for 120 ticks, the real post-hurt invulnerability. The 25-tick +0x1D0 is not it |
| 0x800350C0 direct callers 0x80031BFC, 0x80032758, 0x80032770, 0x800327B4 | CONFIRMED | jal scan (plus the handlers 0x800351E8, 0x80035244, 0x800352A8, 0x80035304) | – |
| Handler logic for 0x1F, 0x20, 0x23 and the helper 0x800351B8; 13 helper call sites | CONFIRMED | 0x800351B8–0x8003531C. jal scan finds 13 sites from 0x80035370 to 0x800362D8 | – |

**Other things the reports missed**
- The hit pass also writes a debug record through `jal 0x80082918`, gated by [0x800DC058]<0 and &0x800. It has no gameplay effect.
- The per-tick loop at 0x800389CC zeroes TouchingEntity (+0x224) and HitCounter (+0x220) on every entity each pass. That supports the "written at tick N, read at N+1, then cleared" claim, as long as that loop runs before the hit pass. Its order in the tick was not checked here.
- The new-game ItemId stays UNKNOWN, as D4 says. If it is −1, 0x8004E428 calls the debug print 0x8004DB90 and returns 0, which is harmless.
