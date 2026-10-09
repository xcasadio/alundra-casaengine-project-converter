> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E14.a), kept verbatim as evidence. The plan holds the reconciled facts.

# E14.a discovery: the Beannoïde (label e14a-beannoide)

Read-only throughout. I built nothing and changed nothing in the repository.

My scratch folder is `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/e14a-beannoide/` (`SCR` below).

Labels: FACT carries a binary address or a file reference. INFERENCE and UNKNOWN say what would decide them.

Notation:
- `e` is the Beannoïde entity and `h` the hero (slot 0, 0x80127D30).
- `E+off` is an entity field offset.
- `ALUN` is ALUN_CD.EXE (France).
- Tick means one logic tick.
- `rng(n)` is defined in section 3.

## Things that change the plan

1. **FACT: the Beannoïde native AI is small and fully decoded, and a model of C1 matches the real binary.**
   - Four native handlers: A1 `0x80061758`, C1 `0x80065ED4`, D1 `0x8007D9A4`, E1 `0x8007ED10`.
   - I ran the real ALUN instructions in a MIPS-I interpreter (`SCR/mipsx.py`, a scratch copy of the repo's `mips.py`) against a Python model written from the disassembly. C1 gave 24 000 random states with 0 mismatches, seed included. D1 gave 500/500. E1's calls were verified with stubs. `GetDirectionToTarget` gave 3 000/3 000. The loot chooser gave 20 000/20 000.
   - Reproduce with `SCR/oracle_c1.py`, `oracle_de.py` and `oracle_loot.py`.
   - Sharing: C1 is also the tick handler of sprite 478 (Petit slime, maps 99, 123, 449). D1 is also the touch handler of sprite 329 (Slime gélatineux, 18 maps). E1 is shared by dozens of sprites. D9 (Muruta) is the same code as D1 with animation 2 instead of 5.

2. **FACT: the wave Beannoïdes of map 44 (records 22-29) do not run native C1 at first.**
   - Their record C index is 151, and `151 & 0x7F = 23`. The map program C[23] @2212 runs instead (dispatch at `0x80038900`: a non-zero masked record index runs the bytecode, otherwise the sprite handler runs).
   - C[23] does: clear ClassA (`0x2B`), `0x25` wait for the ground, `0x37 [20]`, set ClassA (`0x2A`), then `0x40 [2,0]`. That last opcode sets ProgramIndexes[C] = 0, which hands over to native C1.
   - Records 17-21 (C146..150 → C[18..22]) are scripted dream actors with ClassA cleared. They are not fighters. C[36] @2292-2300 destroys them with `0x2E`.

3. **FACT: the map 44 boss spawns Beannoïdes through C1's state 19.**
   - The giant slime AI (C102 `0x80065B0C`, calls at `0x80065C80` and `0x80065E54`) calls `0x8003A1B8(parent, recordId, 1)`.
   - The large slime splits into 4× record 36 (small slime). Each small slime splits into 4× record 37 (Beannoïde) when its animation 3 ends.
   - Each child gets anim 0x13 (19), AIValues[1] = 0x1E, direction 4, 12, 20 or 28, and `SpriteProgramIndexes[0] = 0`. That last write disables A1, so the child keeps anim 19 instead of going dormant.
   - So C1's case 19 exists for this, and `0x2C 35/36/37` in B[5] @895-907 waits for all of them. Up to 16 Beannoïdes (INFERENCE from the loop counts; see UNKNOWN).

4. **FACT: four differences between the binary and the C# decompilation. The binary wins.**
   - **a. Blocked walk expiry.** In C1 case 1 with `ForceAdjusted != 0`, the binary never writes `E+0x88`, so the Beannoïde stays in anim 1 (walk). The decompilation sets anim 0 (`FunctionTypeC.cs:186`).
   - **b. Direction quantizer.** The table at `0x80028B34` is 32 bytes. The decompilation's `g_directionFlipTable` (`StaticVariables.cs:546`) differs from the binary at indices 14, 15, 16, 19-23 and 25-27. C1's own line (`FunctionTypeC.cs:177`) indexes the wrong table, the atan table. The binary values are in section 4.
   - **c. Relative Z.** The binary's `CalculateEntityRelativePosition` (`0x8007FE20`) reads `E+0x134` (FloorHeight). The decompilation's active line uses `TerrainHeight` (`ScriptHelper.cs`, the FloorHeight line is commented out).
   - **d. Atan table address.** The atan table is at `0x80022488`, not `0x80028B34` as the decompilation and `Alundra/Scripts/ScriptHelper.cs:25` claim. The 256 values do match (checked 256/256).

5. **FACT: there is no jump, no projectile, and the Beannoïde is not Collidable.**
   - Flags are `0x103` (ClassA | HitsClassB | Gravity).
   - It hurts the hero only by contact: the body box exists on most frames of anims 0, 1, 3 and 4, plus a 20-tick extended box in anim 3 (section 3).
   - Anims 8-14 are airborne states for spawn drops. Map 87's C[36] sets anim 8.

6. **FACT: the Beannoïde starts dormant.**
   - A1 sets anim 2, which has no frame boxes.
   - It wakes (anim 4, 95 ticks) only when the hero is within ±3 tiles in X and Y.
   - So the two grate Beannoïdes of map 116 do nothing until the hero comes near.

7. **FACT: killing one takes 5 basic-sword hits.**
   - HP is 9, and the response to a sword (nibble 1) is 0x10. The new-game kit does 2 damage per hit (K2 oracle, `docs/plan-e20-annexe/e20-c1b/oracle/out/K2.txt`).
   - The steel edge does 4 per hit, so 3 hits.
   - A bomb (nibble 4, response 0x40) kills in one hit.

8. **FACT: DLL prerequisites missing for E14.a.**
   - `RunSpriteEvent` is a counted no-op except E0/E1 (`AlundraEventProgramRunner.cs:312-321`).
   - The DLL E1 only flags destruction. It does no loot, no break effect 8, and no ActiveEffect/PlatformEntity cleanup (`AlundraWorldProxy.cs:3015-3025`).
   - `ContentsItemId` is never rolled at spawn (`EntityRecordMapper.cs:35-40,86-88`). The original draws 1 or 2 RNG values per Beannoïde created, so the RNG stream drifts from the original.
   - `FloorHeight` is maintained for the hero only (`AlundraEntityScriptProxy.cs:1999`, in `RunHeroProbes`). The binary updates `E+0x134` and `E+0x144` for every entity each tick (helper `0x80038064`, `0x800380E8-0x8003810C`).
   - There is no port of `CalculateEntityRelativePosition`, of the walk starter `0x8007FEF8` (the decompilation calls it `StartFlying`), or of the quantizer table.
   - Already present: `AlundraDamage.ComputeNewHp`, the hit pass, the Hold/Chain animation clock, the E dispatch, and the kinematic tick with Speed/Acceleration.

## 1. Identification

### Handler tables (FACT: table dumps of ALUN)
- Master table `0x80098F4C`, one pointer per program type: [0]=A `0x800C4B34`, [1]=B (null), [2]=C `0x800C4F34`, [3]=D `0x800C5334`, [4]=E `0x800C5734`, [5]=F `0x800C5B34`.
- Entries are 4 bytes each, indexed by handler number:
  - A[1] at `0x800C4B38` → `0x80061758`.
  - C[1] at `0x800C4F38` → `0x80065ED4`.
  - D[1] at `0x800C5338` → `0x8007D9A4`.
  - E[0] and E[1] at `0x800C5734` and `0x800C5738` → both `0x8007ED10`.
  - F[1] → `0x8007FC64` (unused by the Beannoïde: its Interact index is 0).
- Dispatch (`0x800388B4-0x80038948`): the record program index at `E+0x4C+4·type`, masked `& 0x7F`, is tested first. If non-zero the map bytecode runs (`0x8004205C`). Otherwise the sprite index at `E+0x70+4·type` selects the native handler.

### Sprite
- Per-map sprite index 79, with SpriteDirection bit 0x80 set, so the global index is 0x14F = 335, "◆Beannoïde". Balance record 335.
- The header and all 20 animation sets are identical on all 18 maps that hold it (md5 of header and animation data compared): 7, 9, 12, 14, 24, 40, 44, 63, 64, 66, 67, 68, 87, 116, 117, 144, 472, 473.
- **Map 69 has no Beannoïde record** (scan of all 483 `map_N.json`, `SCR/scan_335.txt`). The prompt's mention of map 69 gets no hit.
- Sprite header, read from the map JSON and cross-checked in `E+0x70..0x84` at spawn (`0x80039DE0-0x80039E10`):
  - MoreFlags 3, CanPickup 1, ShadowType 0, so Flags `E+0x6C` = 0x103 = ClassA | HitsClassB | Gravity.
  - Handler indexes: ProgramLoad 1, Tick 1, Touch 1, Deactivate 1, Interact 0.
  - Offset (-10,-7,0), size 20×14×8, BreakEffect 8, Contents 252 (0xFC).
- Balance record 335 (`alundra-project/Data/balance.json`):
  - Level 255 (single record, so `Info.D` is irrelevant), MaxHp 9.
  - Responses [16,16,16,**64**,16,16,16,16,16,16,16]. The response read is `rec[(attr&0xF)+2]`, so sword, hammer and arrow give 0x10 (×1) and a bomb (nibble 4) gives 0x40 (instant kill).
  - One attack: type 1, attr 0x01 (no equipment bit 0x80), Power 1. With count 1 it serves every animation (`Attacks[0]`).

### Entity field offsets used (FACT: asm plus `Gameplay/Entity.cs` order)

| Offset | Field |
|---|---|
| +0x10 | Status |
| +0x14 | Hp |
| +0x3C | ContentsItemId |
| +0x40 | ContentsGameFlag |
| +0x44 | record pointer |
| +0x48 | EntityRefId (= record index) |
| +0x64 | sprite header pointer |
| +0x68 | sprite global index |
| +0x6C | Flags |
| +0x88 | TargetAnimationId |
| +0x8C | TargetDirection |
| +0x9C | AnimationSet pointer |
| +0xAC | ForceResetAnimationFlag (set by an animation HOLD terminator) |
| +0xB4 | AnimFlags |
| +0xBC / +0xC0 | TargetForceX / Y |
| +0xC4 / +0xC8 | ForceX / Y |
| +0xD4 / +0xD8 | ForceStepX / Y |
| +0xF0 / +0xF4 / +0xF8 | Acceleration / Speed / anim impulse |
| +0x114, +0x118, +0x11C | PosX, PosY, PosZ (16.16) |
| +0x120, +0x124 | TileX, TileY |
| +0x134 | FloorHeight |
| +0x13C | ForceAdjusted |
| +0x144 | IsOnGround |
| +0x1C8 | CurrentAttack |
| +0x224 | TouchingEntity |
| +0x228 | EventTrigger |
| +0x275 / +0x277 | Bytes[1] / Bytes[3] |
| +0x282 | AIValues[1] (16 bits) |

- TileX is a LUT of pixelX/24 (table `0x80127168`, built at `0x8003B194-0x8003B1D0`: 52 rows × 24 columns). TileY = PosY>>20, i.e. pixelY/16.
- The hero is at `0x80127D30`.

## 2. Event selection for a Beannoïde (FACT: `0x800386D0-0x80038890`)

- **Status:** 1 Loaded → runs A, becomes 2. Status 2 Normal runs the checks below. Status 3 → E. Status 4 Destroyed (flagged for removal) runs no program.
- **In status 2, in this order:**
  - the two Destroy-flag checks (not set on the Beannoïde);
  - the E triggers: `Flags&0x10` and (ForceAdjusted or IsOnGround); `Flags&0x20` and HitCounter; `Flags&0x40` and ForceResetAnimationFlag. These set Status = 3 and select E;
  - `TouchingEntity != 0` selects D;
  - otherwise C.
- Consequences:
  - C1 does not run on a tick where the Beannoïde was hit; D1 runs instead.
  - E fires only through `Flags&0x40`, which C1 sets when the Beannoïde dies.
- The DLL's `PickEventTrigger` already matches this order.

## 3. C1, the state machine (`0x80065ED4-0x8006624C`, jump table `0x80027014`, 20 entries)

### Animations (FACT: map_14 sprite 79, `SCR/anims_14_79.txt`; frame delay = `byte & 0x7F` ticks)

Terminators: raw 1 = loop; raw 0 with byte1 ≥ 0x80 = HOLD (sets `E+0xAC` = 1); raw 0 with byte1 < 0x80 = chain to anim byte1. `byte0xD` is the export's "Acceleration" field.

- `byte0xD & 0xF` is the acceleration shift, `& 0x20` selects the sound bank, `& 0x40` makes the target immune to hits.
- Speed is multiplied by the direction tables: X amplitude 768 and Y amplitude 512, so Speed 64 gives 0.75 px/tick in X.

| Anim | Role | Ticks / end | Speed | byte0xD | Sfx | Boxes |
|---|---|---|---|---|---|---|
| 0 | idle | 4×15 = 60, loop | 0 | 0 | | body |
| 1 | walk | 8×10 = 80, loop | 64 | 0x01 | | body |
| 2 | dormant | 4×10, loop | 0 | 0 | | none |
| 3 | notice + lunge | 7,7,7,7,7,15,5,20,6,6,6 = 93, HOLD | 0 | 0 | 211 | body; lunge frame (index 7, 20 ticks) extended |
| 4 | wake | 12×5 + 35 = 95, HOLD | 0 | 0 | 211 | last frame only |
| 5 | hurt A | 4×1 = 4, chain→7 | 256 | 0x40 | 65 | none |
| 6 | death | 4×2 = 8, HOLD | 0 | 0x40 | 64 | none |
| 7 | hurt B | 4×2 = 8, HOLD | 160 | 0x40 | | none, except one Up-direction frame (index 1): (-12,-12,0) 23×23×15 |
| 8 | falling | 4×10, loop | 208 | 0x40 | | none |
| 9-14 | air | 1 frame ×10, loop | 208, 256, 320, 384, 448, 512 | 0x40 | | none |
| 15, 17 | map 473 / 472 only | | | | | |
| 16, 18 | | 13×1, HOLD | | | 211 (anim 18) | |
| 19 | boss-child walk | 4×5 = 20, loop | 96 | 0 | | none |

- Body box: (-10,-7,0), 19×13×13.
- Anim 3 extended boxes (offset, W×D×H):
  - Down: (-10,-7,0) 19×29×13.
  - Up: frame 5 (15 ticks) (-11,-23,0) 21×29×13; frame 7 (-13,-9,0) 25×17×22.
  - Left: (-39,-7,0) 51×13×13.
  - Right: (-12,-7,0) 52×13×13.
- Anim 3 frame starts (Down): 0, 7, 14, 21, 28, 35, 50, 55-74 (extended), 75, 81, 87; ends at tick 93.

### Pseudo-code (FACT: equal to the binary on 24 000 random states, `SCR/oracle_c1.py`)

```
rel0 = |e.TileX - h.TileX|; rel1 = |e.TileY - h.TileY|; rel2 = |e.FloorHeight(+0x134) - h.FloorHeight|   // 0x8007FE20
// anim >= 20 (unsigned): nothing
case 0 (idle):
  if (AI1 != 0) { AI1--; if (AI1 != 0) return; }                  // 16-bit lhu/sh
  if (rel0 < 3 && rel1 < 3 && rel2 <= 0x200000 && B1 == 0)        // B1 = Bytes[1]
      { Dir = GetDir(h.PosX - e.PosX, h.PosY - e.PosY); Anim = 3; B1 = 1; return; }
  Anim = 1; AI1 = 0x3C + rng(0x3D);                                // 60..120 ticks of walk
  if (rel0 < 4 && rel1 < 4) Dir = GetDir(...);                     // else Dir unchanged
  B1 = 0;
case 1 (walk):
  AI1--; if (AI1 != 0) return;
  if (ForceAdjusted == 0) { r = rng(0x2000); Anim = 0; AI1 = r & 0x7F; Dir = r >> 8; }   // idle 0..127, random dir 0..31
  else { r = rng(0x1F); Dir = QUANT[Dir]; ForceStepY=ForceStepX=ForceY=ForceX=TargetForceY=TargetForceX=0;
         AI1 = 0x1E + r; /* Anim NOT written: stays 1 */ }
case 2 (dormant):    if (rel0 < 4 && rel1 < 4) Anim = 4;           // no Z test
case 3 (lunge):      if (E+0xAC != 0) { StartWalk(e, 1, 0x28, 0x14); B1 = 0; }
case 4 (wake):       if (E+0xAC != 0) { Anim = 0; AI1 = 0x1E; }
case 7 (hurt end):   if (E+0xAC != 0) { if (B3 != 0) { Anim = 6; Flags |= 0x40; } else { Anim = 0; AI1 = 0x28; } }   // B3 = Bytes[3]
case 8..14 (air):    if (IsOnGround != 0) Anim = 2;
case 19 (boss child):AI1--; if (AI1 == 0) { Anim = 0; AI1 = 0x1E; }
cases 5, 6, 15..18: nothing
```

### RNG
- `seed = seed*0x7D2B89DD + 0xE06A02E7` (32-bit, stored at `0x80098708`).
- `rng(n) = (seed * n) >> 32` on the new seed. This matches `AlundraRandom.Next()` plus the high part of the product.

### Draws per case
- case 0, walk path: 1 draw (n = 0x3D).
- case 1, free: 1 draw (0x2000).
- case 1, blocked: 1 draw (0x1F).
- case 3: StartWalk, 2 or 3 draws.
- Other states: none.

### Walk starter `0x8007FEF8` (named `StartFlying` in the decompilation)

```
r = rng(0x65);                                  // 0..100
Dir = (r < chance) ? GetDir(h.PosX - e.PosX, h.PosY - e.PosY) : rng(0x20);
Anim = anim;
if (base != 0) AI1 = base + rng(0x10);
```
- The Beannoïde calls it with (1, 0x28, 0x14): chase probability 20/101, walk 40-55 ticks. FACT: verified by the model.

### Behaviour cycle (INFERENCE from the above)
- Idle for AI1 ticks, then:
  - If the hero is within ±2 tiles (X and Y) and within 0x200000 (32 px) in floor height: face the hero and play anim 3 for 93 ticks. Then walk 40-55 ticks with a 20% chance toward the hero, then idle again (AI1 stays 40..55 as set by StartWalk; back in idle after the walk ends via case 1).
  - Otherwise walk 60-120 ticks. The direction is toward the hero only if the hero is within ±3 tiles; otherwise the previous random direction is kept.
- Re-aiming happens only when the timers expire. There is no mid-walk chase.
- An attack interrupted by a hit leaves B1 = 1. The next case 0 skips the attack once and starts a walk, which clears B1.

### Movement values
- Walk: Speed 64, shift 1 (`byte0xD & 0xF`). Target force = offset × Speed, so X ±768·64 = ±0.75 px/tick and Y ±512·64 = ±0.5 px/tick.
- Direction convention (tables `0x80023654` and `0x80023694`): 0 = +Y (down), 8 = -X, 16 = -Y, 24 = +X.
- The DLL's `RunOneKinematicTick` already implements this physics.

## 4. Routines called

- **`0x8007FE20` relpos (FACT: disassembly).** It writes |ΔTileX|, |ΔTileY|, |ΔFloorHeight| (entity minus hero, from `+0x120`, `+0x124`, `+0x134`) to `[0..2]`, and the signed deltas to `[3..5]`. There are 46 callers. The second entry `0x8007FE8C` uses `+0x11C` instead of FloorHeight for the entity, and C1 does not use it.
- **`0x8002E814` GetDirectionToTarget (FACT).** It uses the atan table `0x80022488` (16×16 shorts) and the thresholds at `0x80022688` (2^k-1). The DLL's `ScriptHelper.GetDirectionToTarget` is equal; I checked its table 256/256 and my Python port against the binary 3 000/3 000.
- **`0x8007FEF8` StartWalk (FACT).** See section 3; 23 callers. The Muruta's C10 does not use it.
- **Quantizer `0x80028B34` (FACT: binary bytes).** `QUANT[0]=24; [1..8]=0; [9..16]=8; [17..24]=16; [25..31]=24`. For a cardinal direction it is a quarter turn (-8 mod 32); otherwise it snaps to the cardinal strictly below. 15 binary sites read it, among them C10 at `0x800683A4`.
- **Engine passes (shared, already ported):**
  - `UpdateAnimation` `0x80038AB4`: the Chain/Hold/Loop terminators, the Sfx, `CurrentAttack = Attacks[anim+1]` or `Attacks[0]`, and the FrameCollision box.
  - `UpdateEntityPhysics` `0x80036614`: Speed and shift.
  - The ForceAdjusted writer.
  - The hit pass `0x80039300`.

## 5. D1 and the hurt and death sequence

### D1 (`0x8007D9A4`, FACT, oracle 500/500)
```
if (ComputeNewHp(e)) Bytes[3] = 1;                      // 0x8003A374
Anim = 5;
Dir = GetDir(e.PosX - hero.PosX, e.PosY - hero.PosY);   // away from the hero (always slot 0)
```
- Call order in the binary is ComputeNewHp, anim, direction. No RNG.
- `ComputeNewHp` writes `E+0x14` = new HP and returns (HP == 0). `AlundraDamage.cs` already ports it (`ResolveBalanceTarget` `0x8004464C` included). Nothing calls it in production yet.
- The binary has no knockback code. The knockback is the hurt animation's Speed along `Dir`.

### Timeline (INFERENCE, derived from `UpdateAnimation` and the pass order; the passes run events → counters → lists → animation → physics → hits)
- Let t be the tick D1 runs.
  - Anim 5 plays t..t+3 (Speed 256, shift 0, so the force snaps to the target; 3.0 px/tick in X for a cardinal X direction).
  - Anim 7 starts at t+4 and its HOLD flag is set at t+12. Physics reads anim 7 on t+4..t+12 (Speed 160, 1.875 px/tick).
  - C1 case 7 runs in the event pass of t+13.
  - Survivor: anim 0 with AI1 = 0x28 from t+13.
  - Fatal: anim 6 from t+13, HOLD flag at t+21, E1 in the event pass of t+22 (Status 4).
- Free-space knockback is about 12 + 16.9 = 28.9 px in X and 19.3 px in Y (for a cardinal direction). It is shorter against walls.
- Invulnerability: anims 5, 6 and 7 have `byte0xD = 0x40`, so the hit pass skips the target. Also every hit sets the victim's `+0x1D0 = 0x19` (25 ticks).

### Damage numbers (FACT: K2.txt)
- Basic sword: 9 → 7 → 5 → 3 → 1 → 0, so 5 hits.
- Steel edge (item 2): 4 per hit, so 3 hits.
- A bomb kills in 1 hit.

## 6. E1 and the loot (`0x8007ED10`, FACT, oracle)

- **E1 = `DestroyEntity(e, -1)`**, i.e. `0x8003A59C(e, -1)`. This is the same handler as E0.
- **Sequence:**
  - It calls `0x80032B90(e)`, the loot function.
  - It sets `Status = 4` and `E+0x228 = -1`.
  - If `E+0x1B8` (ActiveEffect) is non-null: `effect+0x68 = 0` and `E+0x1B8 = 0`.
  - fx = -1 is replaced by the sprite header byte `+0x1E` (8 for the Beannoïde).
  - It then calls `0x8003BE74(0, 8, 0, e, 1, 0, 0, 0)` (effect 8 at the entity).
  - If `E+0x28` (PlatformEntity) is non-null: `platform+0x2C = 0`.
  - The helpers in the effect and spawn paths contain no RNG reference (checked by grep on `-0x78f8`).

### Loot
- **Roll at creation, not at death.**
  - `InitializeContents` `0x80032A40` is called at the end of entity init (`0x80039F38`).
  - For the Beannoïde, record Contents is 0 and no record flag is set, so it uses the sprite Contents 252.
  - `ChooseRandomlyAnItem` `0x80032968` draws `idx = rng(16)` in row 0x7C of the table at `0x80028E0C` (16 bytes per row). Row 0x7C = `[0×11, 0xF6, 0xF6, 0xFD, 0xFE, 0xFE]`. Entries with bit 0x80 select another row and draw again:
    - 0xF6 → row 0x76: item 69 ×5, 70 ×7, 71 ×4.
    - 0xFD → row 0x7D: 69 ×9, 70 ×5, 71 ×2.
    - 0xFE → row 0x7E: 84 ×12, 85 ×4.
  - Exact probabilities (FACT: exact fractions, confirmed on 20 000 seeds by the real function): none 11/16 (68.75%), item 69 19/256, item 70 19/256, item 71 5/128, item 84 3/32, item 85 1/32. Items 69-71 are Gildas coins and 84/85 hearts per `3-hits.md`.
  - RNG cost per Beannoïde created: 1 draw if the first pick is 0, otherwise 2.
- **Spawn at death** (`0x80032B90`):
  - Only if `ContentsItemId != 0` and the exclusion check `0x80032A00` allows. That check concerns items 0x26, 0x51 and 0x52, which the Beannoïde cannot drop.
  - The item entity is spawned with `0x80039F58(0, 0, item+0x1E, x; stack y, z, 0)`, i.e. sprite item+0x1E (99-101, 114, 115), and sound 0x54 plays.
  - Initial fields: ForceZ (+0xB8) = 0xA0000, Bytes[0] = 1, Flags &= ~0x80, lifetime `+0x278` = 600 if byte 4 of the item row at `0x800C5F7C + item·8` is non-zero (true for 69-71, 84, 85), else -1. AIValues[0..1] = the dead entity's ContentsGameFlag, and `+0x284 = 0xA0000`.
  - These item sprites have header Contents 0, so no extra RNG.
  - Item pickup is the C255 handler. Per D-E20-17, this loot belongs to E20.e0, shared with E14.

## 7. A1 and spawn

- **A1 (FACT: `0x80061758`):** `TargetAnimationId = 2`, nothing else. It runs once at the first event pass (Loaded → Normal).
- **Spawn conditions:**
  - A record auto-spawns only if `SpriteDirection & 0x40`. Records with 0x80 only are spawned by `0x2D`, `0x8A`, `0x8B` or `0x8003A1B8`.
  - Creation happens at anim 0, then A1 sets anim 2.
  - HP = `E+0x14` = MaxHp 9. AIValues and Bytes start at 0 (INFERENCE; I did not locate the slot clear).

## 8. Records, programs, and what `0x2C` needs

### Opcode `0x2C [N]` (FACT: `0x8003DC84`, search `0x8003C954`)
- For N < 0x80 it matches entities by `EntityRefId` (`+0x48`, the record index) with `Status` in 1..3. `Result = (count == 0)`.
- A Beannoïde stops counting only when `DestroyEntity` sets `Status = 4`, which is E1's job, 22 ticks after the lethal D1 tick (section 5).
- Records not yet spawned count as already destroyed, which is why the scripts run `0x2D` before `0x2C`.
- The prelude `0x8003C914` calls `0x80039BD0`, which tests the record's enabled byte.

### Map 116 (D=0)
- Gate: A[1] @128-149 and C[1] @312-325, run by the grate (rec 6, A=129/C=129), test `0x2C [0]` and `[1]`. Both are Beannoïde records, auto-spawn 0xC0, at XPos/YPos (32,36) and (40,36), Height 20, i.e. tiles (16,18) and (20,18).
  - A[1] at load: if both are dead, it moves the grate (`0x65`) and sets its program index (`0x40 [2,0]`).
  - C[1] polls and then sets anim 3.
- Other Beannoïdes with no program: rec 17 (92,70,10), recs 24-27, recs 41-44.
- All are native, with A/C/D/E = 0.

### Map 14 (D=0)
- Records 0..6, auto-spawn, no programs (native only). XPos/YPos/Height: (10,52,10), (44,68,6), (54,56,10), (56,92,2), (12,62,10), (14,104,8), (26,60,10).
- B[5] `0x2C [7],[8],[9]` @252-262 targets the wasps, not the Beannoïdes.

### Map 117
- Records 5, 7, 9, 15, 16. No programs, no `0x2C`.

### Map 44 (D=2)
- **Wave, B[3] @328:**
  - `0x2D` activates records 22..29 at @434-448. Their positions are XPos 80-94, YPos 25-37, Height 36.
  - Each runs C[23] (section "Things that change the plan", 2) and then native C1.
  - `0x2C` 22..29 @465-502 waits for all eight. `0x5D` sets Status = 3 on the grates 30-32 afterwards.
- **Boss, B[5] @744:** `0x2C` 35/36/37 @895-907. Rec 37 is only created by the slime split (section "Things that change the plan", 3).
- **Dream actors:** recs 17-21 with C-programs C[18..22].

### Other maps with Beannoïde kill-gates (found by scanning all 18)
- Map 24: B[3] activates recs 7, 8, 9 and waits; C[12] waits for recs 10, 17, 18, 19.
- Map 40: B[5] activates recs 10-13 and waits.
- Map 87: C[34] waits for recs 35-38, C[35] waits for 39-42. Their C[36] sets anim 8, waits for the ground, sets anim 2 and hands over (`0x40 [2,0]`).
- Map 68 recs 1-4: scripted walkers (C[2], C[3]), ClassA cleared.
- Maps 472 and 473: their A-programs change the sprite programs to other creatures.

## 9. Comparison with the Muruta (C10 `0x80068154`, D9 `0x8007DE04`)

- **Shared (FACT):**
  - E1 `0x8007ED10`.
  - The D skeleton `ComputeNewHp` → Bytes[3] → anim → away-direction. D1 and D9 are byte-identical except the animation constant (5 vs 2), so one parametrised hit routine covers about 15 sprites. The decompilation's `HitCommon` has call sites with anims 2-8.
  - `0x8007FE20`, `0x8002E814`, `0x8003A59C`, and the quantizer table at `0x80028B34` (read at `0x800683A4`).
- **Muruta only:**
  - `0x80080144`, `0x8008048C` (`TryAttackPlayer2`), `0x800805C8` (`TryAttackPlayerFront`).
  - It does not call StartWalk `0x8007FEF8`.
- **Knockback, same mechanism:** the Muruta's hurt animation 2 has Speed 256, 4 ticks, byte0xD 0x41, chain to anim 5. The Beannoïde's hurt animation 5 has Speed 256, 4 ticks, byte0xD 0x40, chain to anim 7. Both are only animation Speed along the away direction.
- **Header comparison:**
  - Same Flags 0x103.
  - The Muruta (global 347, per-map index 91) has Contents 251 (row 0x7B), ShadowType 3 and BreakEffect 8.

## 10. Decompilation vs binary, summary

| # | Item | Decompilation | Binary |
|---|---|---|---|
| 1 | C1 case 1 blocked | sets Anim 0 | leaves Anim 1 |
| 2 | Quantizer | `g_directionFlipTable` array wrong; C1 indexes the atan table | 32-byte table at `0x80028B34` |
| 3 | relPos Z | TerrainHeight (`+0x138`) | FloorHeight (`+0x134`) |
| 4 | Atan table address | `0x80028B34` | `0x80022488` (values equal) |
| 5 | StartFlying | "flying" | walk starter; its random-direction draw is `rng(0x20)`, not `Random.Next() % 32` of the same stream |

## 11. UNKNOWN

1. **Who sets Beannoïde anims 9-14?** I found no Beannoïde-record program that sets them. Anim 8 is set by map 87 C[36]. A scan of all programs for `0x1A`/`0x59` on Beannoïde records with operand 9-14, or a write trace on `+0x88`, would decide.
2. **Exact tick numbers of the death timeline.** The 22-tick figure and the knockback distances are derived. Running `UpdateAnimation` and the physics in the oracle with a full entity would confirm them.
3. **Boss count and EntityRefId of the split children.** The loop counts give up to 16 Beannoïdes. That children carry the record id (so `0x2C 37` counts them) is inferred from `0x80039D64-0x80039D84`.
4. **Effect 8 content.** Not read. `Alundra/Scripts/AlundraEffectBank.cs` and the `alundra-project/Data/effects` folder would decide.
5. **Initial value of AIValues/Bytes at spawn.** The binary slot-clear was not located.

## 12. Evidence files (all in `SCR`)

- `bn.py`: disassembly and table dump helper.
- `c1.dis.txt`, `destroy.dis.txt`, `loot.dis.txt`, `updateanim.dis.txt`: disassembly.
- `all.dis.txt`: full text disassembly of ALUN.
- `anims.py` and `anims_14_79.txt`: animation table.
- `scan_maps.py`, `scan_335.json`, `scan_335.txt`: the 483-map scan.
- `scan_programs.py`, `scan_2c.py`: handler-index and `0x2C` scans.
- `m.py`, `m14.txt`, `m44.txt`, `m68.txt`, `m87.txt`, `m115.txt`, `m116.txt`, `m117.txt`, `m472.txt`, `m473.txt`: event program dumps.
- `mipsx.py`, `oracle_c1.py`, `oracle_de.py`, `oracle_loot.py`: the MIPS oracle (reusable as the port's acceptance oracle).

Repo files read: `Alundra/Scripts/AlundraEventProgramRunner.cs`, `AlundraWorldProxy.cs`, `AlundraEntityScriptProxy.cs`, `AlundraScriptedMotion.cs`, `AlundraFrameSyncPasses.cs`, `ScriptHelper.cs`, `AlundraRandom.cs`, `AlundraDamage.cs`; `docs/plan-e20-annexe/decouverte-2026-10-09/3-hits.md`, `5-chain-needs.md`; `e20-0-rapports/D3-degats-au-contact.md`; `e20-c1b/oracle/out/K2.txt`; and the decompilation `FunctionTypeC.cs`, `FunctionTypeD.cs`, `EntityGameplayManager.cs`, `StaticVariables.cs`.
