# D3: contact damage

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

# D3: contact damage, result

Everything below is read-only. I changed no tracked file. One scratch file I created by mistake in the session scratchpad was deleted straight away. The untracked `docs/plan-e20-annexe/d1-oracle-moveplayer/` in `git status` is not mine.

Facts are FACT, with binary address and instruction words or file. Inferences and unknowns are labelled. Nothing was refuted by a second agent. Instead I recomputed the totals through two independent paths. One read the raw French `DATA/BALANCE.BIN`, the other `alundra-project/Data/balance.json`. One tested `CollisionData != null`, the other `CollisionOffset != -1`. Both gave the same totals.

## Binary facts used

- **FACT: attacker gate, `0x80039364-0x800393C0`.** The pass skips an attacker unless three things hold.
  - It has a box (`8e2201d4 lw +0x1D4`, `beqz 0x8003936c`).
  - It has an attack (`8e2201c8 lw +0x1C8`, `beqz 0x8003937c`).
  - The attack's attribute byte is non-zero (`90440000 lbu 0(v0)`, `beqz 0x8003938c`).
  - Power is never tested. A power-0 attack with a non-zero attribute still hurts, because damage has a minimum of 1.
- **FACT: class mask.** The mask is built from the attacker's Flags `+0x6C` (`0x8003939C-0x800393BC`) as `(F>>2)&1 | (F<<2)&8 | (F&0x1000 ? 0x800 : 0)`. A zero mask skips the attacker (`beqz fp`, `0x800393c0`). The target must satisfy `Flags & mask != 0` (`0x80039450-0x8003945C`).
- **FACT: hero flags.** The hero's Flags are `0x3118C` (`map_alundra.json` SpriteRecords[0]), so it has ClassB (`0x8`) and neither ClassA nor ClassC. Only attackers with HitsClassB (header `MoreFlags` bit `0x2`) reach it. HitsA and HitsC attackers never do.
- **FACT: Flags composition, `0x80039DA4-0x80039DD0`.** Flags = `lhu 0x10(hdr) | lbu 0x12(hdr)<<16`.
- **FACT: CurrentAttack, `0x80038B74-0x80038BAC`.**
  - With `count = 0` there is no attack.
  - If `anim+1 < count` it is `Attacks[anim+1]` (`rec+0x11+2*anim`).
  - Otherwise it is `Attacks[0]` (`rec+0xF`).
- **FACT: frame box, `0x80038C1C-0x80038CB8`.** Collision index `0xFFFF` gives no box (`sw zero,0x1d4`). In the JSON this is `CollisionData: null`.
- **FACT: balance record choice.**
  - `0x80044550` first remaps sprite indices `0x1F..0x7F` to `0x1E`. It then returns the first record in the chain with `level >= [0x80143DF0]`.
  - The threshold is stored only by `0x80044524`, whose sole caller passes map header `+0xB` (`0x8002C39C`, `lbu a0,0xb(v0)`). That is the exported `Info.D`.
  - The record index is the sprite index OR `0x100` for per-map sprites (`0x8003A304`). Common-bank sprites keep the raw index.
- **FACT: spawn.** A map record spawns at animation 0 (`0x8003A320 sw zero,0x24(sp)` → `0x80039D28` → `0x80039DC0 sw t0,0x88`). It auto-spawns only if `SpriteDirection & 0x40` (`0x8003A268`). Otherwise a script spawns it.
- **FACT: data cross-check.** `balance.json` equals the raw `BALANCE.BIN` on all 512 chains, with 0 mismatches.
- **FACT: hero as target.** The hero spawns with balance index 0 (`0x8003199C sw zero,0x10(sp)`). Record 0 has all 11 responses at 0, so the hit pass never treats the hero as immune. The damage multiplier comes from `0x80127008`, which is D4 (UNKNOWN here).
- **INFERENCE: animation roles.** Animation 0 is the spawn (idle) animation, which is a FACT above. Animation 1 as "walk" is inferred from its non-zero `Speed` in the data (Beannoid 64, Orc 128, Muruta 256).
- **Not verified:** which animations actually play is decided by the native C/D/E handlers (E14). The lists below are the data-level upper bound.
- **UNKNOWN:** runtime flag changes by opcodes `0x28-0x2B`, `0x62` and `0x63` were not scanned.

## Per map

Notation: `Nf` is frames with a box out of frames in the animation. It is the same in all 4 directions unless bracketed as [Down, Up, Left, Right]. Attack types are Sword 1, Hammer 2, Fire 6, Bomb 4. "Auto/script" is the count spawned by zone versus by script. Idle/walk means animations 0 and 1.

| Map | Sprite | Placed (auto/script) | Balance record used | Hurting animations (attr, power, frames) | Idle / walk |
|---|---|---|---|---|---|
| **14** (D=0) | Burning thorns 222 (g238) | 1 (1/0) | idx 238, L255, HP 1, 2 attacks | a0 Sword pow 1, 1/1f; a1 Fire pow 1, 9/15f | yes / yes |
| 14 | Falling rock (g252) | 3 (0/3) | idx 252, L0, HP 0 | a0 Hammer pow 1, 1/1f | yes / – |
| 14 | Beannoid (g335) | 7 (7/0) | idx 335, L255, HP 9 | Sword pow 1: a0 4/4, a1 8/8, a3 11/11, a4 1/13, a7 [0,1,0,0]/4 | yes / yes |
| 14 | Fish Lv.1 (g392) | 2 (2/0) | idx 392, L255, HP 2 | Sword pow 1: a3, a4, a5 4/4 | **no / no** |
| 14 | Wasp Lv.1 (g455) | 3 (3/0) | idx 455, L255, HP 2 | Sword pow 1: a0 to a3 12/12 | yes / yes |
| **15** (D=0) | Burning thorns 222 | 2 (2/0) | as map 14 | as map 14 | yes / yes |
| 15 | Orc (mace) Lv.1 (g341) | 3 (3/0) | idx 341, L255, HP 9 | Hammer **pow 4**: a0 4/4, a1 6/6, a3 6/6, a7 3/3 | yes / yes |
| 15 | Muruta (claws) Lv.1 (g347) | 3 (0/3) | idx 347, **L1**, HP 3 | Sword pow 1: a0, a1, a4, a8 to a12 4/4; a3, a7 8/8 | yes / yes |
| **115** (D=0) | Falling rock | 2 (2/0) | L0 | a0 Hammer pow 1 | yes / – |
| 115 | Muruta (claws) | 11 (3/8) | L1, HP 3 | as map 15 | yes / yes |
| **116** (D=0) | Falling rock | 3 (3/0) | L0 | a0 Hammer pow 1 | yes / – |
| 116 | Iron ball large (g311) | 11 (11/0) | idx 311, L0, HP 0 | Hammer pow 2: a0 to a9, all boxed (a5 12/12) | yes / yes |
| 116 | Beannoid | 11 (11/0) | L255, HP 9 | as map 14 | yes / yes |
| **117** (D=0) | Iron ball small (g310) | 4 (4/0) | idx 310, L0, HP 0 | Hammer pow 1: a0 to a8 | yes / yes |
| 117 | Iron ball large | 5 (5/0) | L0 | Hammer pow 2: a0 to a9 | yes / yes |
| 117 | Beannoid | 5 (5/0) | L255 | as map 14 | yes / yes |
| **44** (D=2) | Beannoid | 14 (5/9) | L255, HP 9 | as map 14 | yes / yes |
| 44 | Giant slime large (g459) | 1 (0/1) | idx 459, L255, HP 42 | Sword pow 2: a0 12/12, a1 12/12, a2 15/15, a3 4/4 | yes / yes |
| 44 | Giant slime small (g460) | 1 (0/1) | idx 460, L255, HP 27 | Sword pow 1: a0 12/12, a1 12/12, a3 7/14 | yes / yes |
| **362** (D=0) | Bomb (g2, ClassB, Hits A and B) | 1 (0/1) | idx 2, L255 | a1 Bomb pow 5, 7/16f (reaches the hero) | no / yes |
| 362 | Muruta (claws) | 4 (0/4) | L1, HP 3 | as map 15 | yes / yes |

Notes on the table:
- **Balance chains.** Falling rock and the iron balls have chains L0 to L11 plus L255, and L0 is chosen at D=0. Muruta has L1, L8 and L255, so D≤1 picks L1 (HP 3, pow 1). Every other sprite above has a single L255 record, so D does not matter for them. Every record has `count = 1` except the thorns (2), so `Attacks[0]` serves all animations.
- **Frame boxes.** They are body-sized. Beannoid a0 is (-10,-7,0, 19×13×13) against a header body of 20×14×8. This is mere body contact.
- **Direction quirk.** Beannoid a7 has a box only in the Up direction. The DLL must read boxes per direction as stored.
- **Harmless on these maps** (no HitsB, or no attack with a box):
  - Map 14: 9 records. Map 15: 17. Map 115: 21, including 13 crates that have Hits[A] only.
  - Map 116: 24. Map 117: 18. Map 44: 22. Map 362: 6.
  - The mine cart on 362 has attack `0x81` pow 20 with boxes, but its flags `0x32080` carry no Hits bit.

## Corpus totals

483 maps, 9741 entity records, plus the common bank `map_alundra.json`.

- **Reach.** 2642 records have a HitsB sprite. 2600 of them, on 211 maps, have at least one animation that hurts by contact. 1572 of the 2600 auto-spawn and 1028 are script-spawned.
- **No hurting animation.** 42 HitsB records have none: King Mimming ×8, Generator ×17, Driftwood ×16 and After Melzas ×1.
- **Distinct sprites.** 71 of the 282 placed sprites hurt. 55 are ClassA, so the hero can hit them. 16 are not (rock, iron balls, flames, laser, spiky floor, bomb and similar).
- **Animations.** There are 446 distinct (sprite, animation) pairs. A pair that hurts is also a pair with a box and a non-zero attribute.
- **Idle and walk.**
  - Animation 0 hurts on 2346 records (90.2%). Animation 1 hurts on 2402. Either one hurts on 2512 (96.6%).
  - By sprite: 64 have a0, 65 have a1, 69 have either. The only exceptions are Fish Lv.1 (g392: a3 to a5 only) and Shadow Man Lv.1 (g440: a2 to a4 only).
- **Attack mix.** Over 16 178 (record × animation) pairs, 10 457 are Sword, 3618 Hammer, 2098 Fire, 4 Arrow and 1 Bomb. Power runs from 1 to 30.
- **Attribute check.** No hurting attack has bit `0x80`, so the equipment term never applies. None has `attr&0xF >= 12`, so the response index never leaves the table.

## What E20.c1 changes on maps 14 and 15

- **Map 14.** 16 of 25 records become contact hazards: Beannoid ×7, Wasp ×3, Fish ×2, thorns ×1 and Falling rock ×3. All are auto-spawned except the rocks, which a map program spawns (C131).
  - Beannoid, Wasp and thorns hurt in the spawn animation and while walking.
  - Fish do not hurt in animation 0 or 1.
  - The rock hurts in its only animation.
- **Map 15.** 8 of 25 records become hazards: Orc (mace) ×3, thorns ×2 and Muruta ×3. The Orcs (Hammer pow 4) are the hardest hitters on the two maps. The plan already says the three orcs are optional and the scene Murutas destroy themselves. The scene Murutas are script-spawned and hurt in animation 0 and 1 if touched.
- **What a touch does with c1 only.** Per the binary check, the pass does four things on contact.
  - It sets the hero's `+0x1D0 = 0x19`, which blocks further hits on the hero for 25 ticks.
  - It sets the hero's `TouchingEntity`.
  - It increments the enemy's `HitCounter`.
  - It spawns 3 effect-9 sparks at the overlap centre.
- **No HP loss yet.** Only the damage entry `0x80031A68` reads the hero's `TouchingEntity`, and that is E20.c2. So until c2 the author sees sparks on touching a Beannoid, Wasp, Orc, bramble or rock, and no heart loss.
- **Swords.** The hero's sword channel (HitsA) reaches only the ClassA ones: Beannoid, Wasp, Fish, Orc, Muruta and thorns. It does not reach the rocks. That channel also needs E20.b's attack boxes.
- **UNKNOWN.** What these enemies do today without their native handlers (E14), and so which animation they play when touched, is not established here.
