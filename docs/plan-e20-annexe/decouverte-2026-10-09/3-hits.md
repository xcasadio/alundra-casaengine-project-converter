# Survey 3: hit resolution and the hero / E14 / E18 boundary

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# Hit resolution and the hero / E14 / E18 boundary

**Scope:** I read the decompilation (`alundra-datas-analyser/AlundraTools/AlundraEngine`, abbreviated `AE/`) and checked every decision-carrying fact in `ALUN_CD.EXE` with capstone. Nothing was written. Labels are FACT (file:line, or address plus the words I decoded), INFERENCE, UNKNOWN.

## 1. Verdict

- **FACT:** one shared pass, `UpdateBalanceRecords` at 0x80039300, resolves every hit in the game: hero to enemy, enemy to hero, projectiles. It only writes fields. The sole caller is 0x8003B3F0, in `UpdateEntities`. `ComputeNewHp` has 37 callers in the binary: the hero's damage entry (0x80031A90) and 36 enemy D-handlers (0x8007D9B0 to 0x8007ECA8).
- **FACT:** the hero's weapon boxes, `CurrentAttack` and the hit pass belong to no enemy. Per-enemy reactions are the D, C and E handlers.
- **Proposal:** the hit core, hero attack, sprint, hero damage reception and the attack predicates belong in the new hero task. Per-sprite D, C and E handlers belong to E14. The `Hp == 0` branch belongs to E18.

## 2. The hit pipeline

**Frame order** (0x8003B388). It is gated on `[0x800DC4B8] & 0x48 == 0` and runs 0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364, 0x80038E84, 0x80039300.
- 0x800386D0 calls `MovePlayer` (0x80031B50) first, at 0x800386EC.
- A hit written at the end of tick N is consumed by the events pass of tick N+1. The counters pass then clears it.

**Attacker data**
- **CurrentAttack:** reloaded on every animation change (0x80038B74 to 0x80038BAC). `CurrentAttack` (+0x1C8) is `BalanceRecord(+0x1C4).Attacks[anim+1]`. It falls back to `Attacks[0]` when `anim+1 >= count`, and is null when the count is 0.
- **FrameCollision:** the per-frame box (+0x1D4), set at 0x80038C44 to 0x80038CB4. Signed offsets, and W/D/H stored as `(n<<16)-1`. Offset 0xFFFF gives null (0x80038CB8).
- **HitBox:** Pos plus offset, recomputed only if a box exists (0x80038064 to 0x800380B4).
- **AnimFlags:** byte 0xD of the animation set, reloaded on each change (0x80038B68).

**Selection rules** (0x80039364 to 0x80039450)
- The attacker needs a box, an attack, and a non-zero attribute.
- Class fan-out is `(F>>2)&1 | (F<<2)&8 | (F&0x1000 ? 0x800 : 0)`. So HitsA reaches ClassA, HitsB reaches ClassB, HitsC reaches ClassC.
- A target is skipped if `FrameCollisionTickCounter` (+0x1D0) is non-zero, if `DamagedTickCounter` (+0x1CC) is non-zero, if `AnimFlags & 0x40`, or if `(Flags & fan-out) == 0`.
- Overlap per axis (0x80039464 to 0x8003951C): `d = attackerBox - target.ModdedPos`. If `d >= 0` it needs `d < targetSize+1`. Otherwise it needs `-d < attackerBoxSize+1`.
- The target body is `ModdedPos` with sprite-header Width, Height and Depth. The first attacker in active-list order wins, and the hero is slot 0.

**On a match** (0x800395D4 to 0x80039680)
- The response byte is `r = target.BalanceRecord[(attr&0xF)+2]`.
- If `(r&0xC0) == 0x80` the target is immune and gets no `TouchingEntity`.
- Otherwise attack types 6 and 10 attach effect 4, types 7 and 9 attach effect 5, and `target.TouchingEntity = attacker` (0x80039668).
- Always, immune or not: the target's `FrameCollisionTickCounter = 0x19`, the attacker's `HitCounter++`, and sparks (effect 9) at the intersection centre.
- No other store to +0x224 exists in game code. The only game-code store to +0x1D0 is 0x80039670.

**Counters and dispatch**
- The counters pass (0x80038998, 0x800389D0 to 0x80038A10) zeroes `TouchingEntity`, `RidingEntity` and `HitCounter`, increments `FrameCounter`, and decrements both counters.
- 0x800386D0 picks program D when `TouchingEntity != 0`. It does so after the E-triggers (`ForceAdjusted`/`IsOnGround`, `HitCounter`, anim-end) and the two destroy flags.
- If the entity record's D index (masked `&0x7F`) is 0, the sprite handler runs. Otherwise the map bytecode runs.

**Damage** (`ComputeNewHp` 0x8003A374, `ResolveBalanceTarget` 0x8004464C)
- If `r&0xC0 == 0`: `dmg = ((Power + (attr&0x80 ? sum of the 3 equipped records' Attacks[heroAnim+1].Power : 0)) * r) >> 4`. The minimum is 1.
- HP becomes `dmg < HP ? HP - dmg : 0`.
- `r&0xC0 == 0x40` is an instant kill. `0x80` leaves HP unchanged. The function returns `HP == 0`.
- The hero uses `g_balanceRecord` (0x80127008). Others use their own record.
- `g_balanceAnimIndex` is always the hero's `TargetAnimationId` (0x80136DF8 from 0x80127DB8), even when the attacker is not the hero.
- Typical `r` values (INFERENCE from `balance.json`): armor 17 to 20 give 16, 12, 8, 4 (100% down to 25%). Weapon records carry 0x40 and modify nothing.

**Balance level**
- `InitializeEntity` takes the record by the map-info byte +0xB threshold (0x8002C39C, 0x80044550). `HpMax = Hp = record.MaxHp` (0x80039E30 to 0x80039E4C).
- FACT, export hazard: the export's `BalanceLevel` is byte +9. It is 2 on all 483 maps and is the tile-break effect id. The real threshold is exported as `D`, with values 0 to 11.

## 3. Per target class (census of 9741 entity records, 483 maps)

- **Hero attack channels:** the hero header is 0x3118C: `HitsA | ClassB | HitsC | Collidable | Gravity`. His attacks therefore reach ClassA and ClassC. Enemies are `A|HitsB`, so they reach ClassB: the hero and the chicken.
- **Enemies and obstacles** (2055 records, 64 names, ClassA with a native D):
  - 32 of the 45 D-handlers are used. The largest are D6 (323), D31 (292, iron-ball walls), D1 (248), D9 (198), D2 (178), D11 (157), D34 (157, burning brambles).
  - A D-handler (verified at 0x8007D9A4 and 0x8007DCD8) calls `ComputeNewHp`, sets `Bytes[3]=1` (+0x277) if dead, sets the hurt animation, and sets `TargetDirection` to the direction from hero to self.
  - Knockback is not code. It is the hurt animation's Speed along `TargetDirection`, set by `UpdateEntityPhysics` (AE `PhysicsEngine.cs:1579`).
  - `DamagedTickCounter = 0x5A` is written by the C-handlers (AE `FunctionTypeC.cs:2105` and others).
  - The death sequence (`Bytes[3]`) and the loot are per-enemy C code (UNKNOWN beyond the sampled handlers).
- **Scripted ClassA reactions** (417 records, 24 names: levers 108, fire-lit switches 78, lamp switches 31, breakable armor 27). The D bytecode runs for any non-immune hit.
  - Which weapon is accepted is encoded in the target's `DamageResponses` in BALANCE.BIN, not in the script.
  - 143 D programs start with 0x87. By weapon: fire 42, bomb 29, hammer 27, ice 27, sword 17, arrow 1.
- **Cuttable tiles:** 9089 tiles on 165 maps have walkability bit 0x02. The only reader I found is `CheckAndTriggerTileEffect`. Reading bit 1 as "cuttable" is INFERENCE.
- **Crates and jars:** 995 and 914 records carry no class bit, so no weapon ever selects them.
  - D-E19-28 and D-E19-46 concern lift and throw, not the sword.
  - FACT: when the hero is hurt, `UpdatePlayerCarriedEntity(2)` (call at 0x80031C64) sets `Flags2 = -1` and drops the object. The breakable branch then arms `DeactivateOnImpact` (AE `FunctionTypeC.cs:588 to 606`).
- **Chests (518) and NPCs:** chests have no class and open through program F. NPC sprites are ClassB or classless, so no hero channel reaches them.
- **ClassC** exists on no sprite header (2658 scanned). It is script-only: 12 `0x62` sites on maps 40, 149, 306, 382, 399, 426, 441.
- **Tile cutting** (`CheckAndTriggerTileEffect` 0x8003A7B0):
  - Four corners of the current box are tested every tick.
  - The tile needs flag 0x02, and `(Height<<20)+0x80000` must lie inside `[HitBoxZ, HitBoxZ+CollisionHeight]`.
  - It clears the bit and sets WallTilesOffset to 0xFFFF. It then creates effect `mapinfo[+9]` and calls `RandomlySpawnItem(0xFF)` (0x8003A988), with sound 0x1F.
  - Row 0x7F of the item table is `[0 x14, 0xFD, 0xFE]`, which redirects to row 0x7D (Gildas 69/70/71) or row 0x7E (hearts 84/85). That gives 1/16 money and 1/16 hearts per cut.
  - It is called from hero animations 0x04, 0x10, 0x12, 0x14, 0x16, 0x18, 0x19, 0x3F, 0x40, 0x41, 0x42, 0x44, 0x46, 0x49, 0x4B (jump table 0x800228AC, call 0x8002FDC0). So sprint cuts grass; the bow and wand animations do not.
  - Other callers are the sword beam (0x8007A960, C61), the charged arrows (0x8007C0E4, C94), and the bomb (0x8007F2D4, E12).
- **Destruction and loot:** `DestroyEntity(e, fx)` (0x8003A59C, 38 callers) spawns the loot (0x80032B90) and the break effect. The single-argument form (0x8003A774, 32 callers) does not.

## 4. The hero receiving damage

- **Damage entry** (0x80031A68), called at 0x80031C44 in the normal path and at 0x80031C14 when `controlFlags == 0x20`.
  - With a `TouchingEntity`, it runs `ComputeNewHp(hero)`. The direction is `GetDirection(hero.Pos - toucher.Pos)`.
  - Without one, and only if `DamagedTickCounter == 0 && g_gravityFlag < 3`, it checks hazard tiles. `n = (VramOR & 0x180) >> 7` costs `[0,1,2,3][n]` HP, and the direction is the facing turned 180 degrees. The table is at 0x80028B8C. 7322 tiles are involved (O-E19-22).
- **Hurt state** (0x80031C74 to 0x80031C9C): `TargetAnimationId` is 0x31, or 0x3A if `Slope_18c == 4`. `DamagedTickCounter = 0x78`, `TargetDirection = dir`.
  - Hero anim 0x31 has AnimFlags 0x40 (invulnerable), Speed 256 and Chain to 0x1C (`balance`/sprite data). 0x3A chains to 0x3B.
  - 28 hero animations carry the 0x40 bit.
- **Flicker:** the visible list drops an entity when `(DamagedTickCounter & 3) == 3` (0x800385F0 to 0x800385F4). The shadow drops it too.
- **Flail recoil:** at the end of `CreatePlayerAnimationEffects` (0x8003043C to 0x80030650), for animations 0x12, 0x16, 0x40, 0x42.
  - If `HitCounter != 0`, or a box corner lies in a wall (tile height above `HitBoxZ`, or walkability & 0x40), the hero switches to `[0x08, 0x1F, 0x25, 0x26]` (table 0x800228A4, ground or air).
  - This is why hero attack needs the hit core.
- **Hp mirror:** `MovePlayer` ends by calling `SetPlayerHpMax` and `SetPlayerHp` (0x80032920 to 0x80032930).
- **Effective defence:** `UpdateItemEffectState` (0x800307E8) recomputes `g_balanceRecord` and the regen ticks every tick.
- **HP == 0** is checked right after the damage entry (0x80031CA0).
  - The branch at 0x800327A4 to 0x80032910 sets death animations. It then calls `GetNumberOfItem(0x27)` at 0x80032808; 0x27 is "Breuvage de soin". With none, it sets `g_desiredMap = 0x1DD` (0x80032854).
  - Start+Select held 0x78 ticks consumes item 0x27 and sets Hp to 0 (0x8002BBE4 to 0x8002BC34).

## 5. The predicates

- **0x87** (0x80040048) is byte-identical to the decompilation. For each matching entity it tests: `TouchingEntity` non-null, its `FrameCollision`/`CurrentAttack` non-null, attr non-zero, and `(attr&0xF) == table[v2]`.
  - The table at 0x80098FA4 is `[1,2,3,4,5,5,6,7]`: Sword, Hammer, Arrow, Bomb, Tackle, Tackle, Fire, Ice.
  - It sets `Result` (+0x2C) and returns 3.
  - Corpus: 182 sites.
- **0x95** (0x800407C0) is equivalent to the decompilation. It scans slots 0..N inclusive for entities with status 2 or 3, unblocked, with box, attack, `Flags & mask`, and nibble match. It then runs a box overlap, X and Z over `v4*0x180000`, Y over `v5<<20`.
  - Corpus: 43 sites. The type index is 3 (bomb) 28 times, sword 6, and each of the others once. Four sites on map 226 use indices 8 to 11.
  - **Q-A2 answer (FACT):** indices 8 to 11 read the bytes `[0x58,0xD1,0x03,0x80]` that follow the table (the start of the opcode pointer table at 0x80098FAC). Only index 10 (=3, arrow) can match. The rule that the binary wins gives "copy the binary".
- **0x86** (0x8003FFD4) is a SET of `DamagedTickCounter` to `v2|v3<<8`. It is not an add. Corpus: 76 sites.
- The DLL already reads `HitCounter` in opcodes 0x47 and 0x48 (`AlundraEventProgramRunner.cs:747,753`). Its comment "stays 0 until E14" becomes wrong once the hit pass lands.

## 6. Decompilation vs binary

| # | Decompilation | Binary |
|---|---|---|
| 1 | `CreateRandomPoofs` (AE `EntityManager.cs:648`) creates 2 sparks | 3 iterations: `addiu s3,zero,3` at 0x800391A8, `addiu s3,s3,-1; bgtz` at 0x800392C0 to 0x800392C4. Each draws 3 RNG values. |
| 2 | `CheckAndTriggerTileEffect` (AE `GameEngine.cs:2467`) does `(HitBoxY+Depth)>>20` then `/MapTileHeight` | A single `sra 0x14` (0x8003A860). The double division is a decomp error. |
| 3 | `ChooseRandomlyAnItem` (AE `GameEngine.cs:861`) draws RNG twice (`% 17`, then `*16>>32`) | One draw (0x80032980 to 0x800329B0). |
| 4 | Start+Select block (AE `GameEngine.cs:1545`) comments out `DamagedTickCounter` | `sw s0,0x1cc(s2)` at 0x8002BC2C stores 0x78. |
| 5 | Labels: 0x86 "Add" (`EventCodeDebugger.cs:262`, `EventOpcodeSizeTable.cs:182`). The hero damage entry is named `FindWarpFacingDirection`. | The code sets the counter. The entry at 0x80031A68 is the damage entry. |
| 6 | `UpdateEntityLists` loops `i < g_numberOfEntities` | `<=`. No effect, since slot N is destroyed. |

Everything else checked is equivalent: 0x80039300 (including the ±1 widening), 0x8004464C, 0x8003A374, 0x800386D0, 0x80038998, 0x800384F4, 0x8003A59C, the flail tail, the hazard branch, the death branch.

## 7. State of the port

- **FACT:** the proxy declares `HitBox*`, `Collision*`, `HitCounter`, `TouchingEntity` and `DamagedTickCounter`. `BalanceRecord`, `CurrentAttack` and `FrameCollision` are commented out (`AlundraEntityScriptProxy.cs:399,400,403`). No counters pass exists: grep finds no `TouchingEntity = null`, `HitCounter = 0` or `FrameCounter++` in `Alundra/Scripts`.
- **FACT:** `PickEventTrigger` already ports the E and D selection (`AlundraEntityScriptProxy.cs:1498 to 1531`). Hero `Hp` is never set, and the `Hp == 0` branch is skipped on purpose (`AlundraPlayerManager.cs` header). Once `Hp` is live that guard must stay.
- **FACT:** `balance.json` is exported but nothing loads it (`ItemsWriter.cs:39`). The per-frame boxes are exported as engine `CollisionKeyframes`, with no attack semantics (`SpriteWriter.cs:51 to 57`).
- **Hazards:**
  - The DLL's `PosZ` is the binary's minus 1 (O-E19-45, D-E19-59). The Z edge tests of 0x80039300 and 0x8003A7B0 must add it back.
  - The DLL runs entities before the world tick, the opposite of the binary (`AlundraWorldProxy.cs` around 2300 to 2370). The hit pass must run after movement, and the counters pass after the events pass.
  - Sword swing delays are 4,2,2,3,4,5,1 = 21 ticks, under the 25-tick lock. That gives one hit per swing (INFERENCE). Sprint dash is 8 x 4 = 32 ticks.

## 8. Proposed boundary

**Hero task (hits work end to end without E14)**
1. Fields, counters pass (0x80038998), `Hp` from the record, `Info.D` threshold, boxes read from the active frame.
2. `UpdateBalanceRecords` with the 3-spark loop and statuses 4 and 5; `ComputeNewHp` and `ResolveBalanceTarget`.
3. Hero attack: `PlayerTryAttack`, the weapon rows, flail recoil, `CheckAndTriggerTileEffect` with its loot rows and effect.
4. Sprint, including the tackle attack (anim 0x04, attr 0x85) and the tile cut.
5. Hero damage: entry, `UpdateItemEffectState`, hazard tiles, 0x78 invulnerability plus flicker, Hp mirror, carried-object drop, and a stub for `Hp == 0`.
6. 0x86, 0x87, 0x95, and the scripted reactions they serve (switches, bomb lids, ice columns).
7. Candidate, not decided: hero-owned native handlers. These are the sprites 0x02 to 0x1D:
   - C: 60, 61, 62, 63, 65, 73, 75, 94
   - D: 30
   - E: 1, 3, 9, 10, 12, 13, 17, 21, 28

**E14**
- All non-hero C, D and E handlers: 2055 enemy-like records, 32 D-handlers in use.
- Enemy attack animations: `CurrentAttack` then follows automatically from slice 1.
- Death sequences, loot callers, bosses.

**E18**
- The death branch at 0x800327A4 to 0x80032910, the Dead animations, item 0x27 revive, map 0x1DD, opcode 0xBB, and Start+Select with the corrected 0x78.

## Open questions for the author

1. Is the hit pass a faithful integer loop in the DLL (my recommendation, because of the ±1 widening, first-attacker-wins, and pre-move `ModdedPos` vs post-move `HitBox`), or driven by engine collision events?
2. Who owns drops and pickups (0x80032968, 0x80032B90, 0x80032C7C, the item entities' C255 handler at 0x8007C174)? Grass cutting needs them first, and E14 and chests reuse them.
3. Before E18, what does `Hp == 0` do: a hero that cannot die, a frozen death pose, or the full branch?
4. Are the hero projectiles and the bomb (item 7 above) in the hero task? They are the only path to the fire, ice and bomb targets.
5. Before E14, what happens to an enemy whose `Bytes[3]` becomes 1: it stays as it is, or a dev-only death?

## Suggested slice boundaries

| Slice | Content | Proof |
|---|---|---|
| H0 | Data and fields: balance loaded, `Info.D`, boxes, `Hp` init, counters pass | Pure tests |
| H1 | Hit core and damage (incl. the hit-written `HitCounter` consumers 0x47/0x48, `PickEventTrigger`) | MIPS emulation of 0x80039300 / 0x8004464C against random entity sets with the existing `emu.py` |
| H2 | Tile cut and loot rows | Same oracle; row 0x7D/0x7E draw |
| H3 | Hero attack and flail recoil | Arcs |
| H4 | Sprint | Arcs |
| H5 | Hero damage, hazard tiles, Hp mirror, `Hp == 0` stub | Emulated 0x80031A68 |
| H6 | 0x86 / 0x87 / 0x95 and the scripted reactions | Corpus sites |
| H7 | Hero projectiles (pending Q4) | Per-handler emulation |

Re-measure the arcs and traces when H1 lands, because `HitCounter` and `TouchingEntity` start moving.
