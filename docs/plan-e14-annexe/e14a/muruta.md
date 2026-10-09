> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E14.a), kept verbatim as evidence. The plan holds the reconciled facts.

# Muruta (claws) Lv.1 native AI (E14.a): decode of C10 / D9 / E1 and helpers

Read-only discovery, label `e14a-muruta`. Evidence is in my scratch folder `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/e14a-muruta/`:
- `mdis.py`, a capstone disassembler with an RNG annotator;
- `c10.txt`, `loot.txt`, `spawn.txt`, `anim.txt`, `force.txt`, `sel.txt`, `search.txt` (raw listings);
- `map_*.txt` (program dumps).

Nothing in the repo was written, built or run. I ran `research/census/waits-0x24/m.py` as a script, redirected to scratch, as the plan does.

Labels: **FACT** (binary address or file:line), **INFERENCE**, **UNKNOWN**. Binary = `ALUN_CD.EXE` (France). "Decomp" = `alundra-datas-analyser/AlundraTools/AlundraEngine/...`.

---

## 0. Things that change the plan

1. **FACT: the hall Muruta do not start in native C10.**
   - Records 24-27 of map 115 carry a bytecode C program. `EventCodesC_TickIndex` = 153, so `& 0x7F` = 25 and `C[25]` is at @1464.
   - The program is `0x2B` (clear ClassA), `0x25` (wait for ground), `0x37 [20]`, `0x2A` (set ClassA), `0x40 [2,0]` (clear the C index), `0xFF`.
   - Only after that does native C10 run, because the dispatch calls the native handler only when `ProgramIndexes[type] & 0x7F == 0` (`0x800388F8-0x80038904`).
   - The DLL already runs this prelude (`AlundraEventProgramRunner.cs:807`).
   - Map 362 records 6-9 work the same way: `C[6..9]`, wait on T7, then `0x40 [2,0]`.
   - Map 15 records 0-2 never reach native code. `C[6..8]` are full scenes ending in `0x2E [128]` (@712, @777, @890), which is the 1-argument `DestroyEntity` (`0x8003A774`). It has no loot, no effect and no sound.
   - **Map 15 needs nothing from E14.a.**

2. **FACT: death is C10's job, not D9's or E1's.**
   - D9 only does damage and sets `Bytes[3]=1` (the "dying" flag).
   - C10 state 5 then turns that into animation 6 plus `Flags |= 0x40` (DeactivateOnAnimationEnd).
   - E1 only destroys.
   - D9 + E1 alone can never open the hall. C10's state 5 is required.
   - A minimal hall slice is C10 + D9, with E1 already partly there. Loot and the break effect are missing (item 5).

3. **FACT: two sword hits kill a Muruta.**
   - Balance record 347 (`0x100 | 91`, chain L1, D=0) has MaxHp 3 and responses `[16,16,16,64,16,...]`.
   - `DamageResponses[k]` is the response to attack nibble `k+1`, because the binary reads `rec[(attr&0xF)+2]` and `rec[2]` is the HP.
   - The base-sword hit is attr `0x81`, power 1, plus 1 from the equipment term, so damage = (2 x 16) >> 4 = 2. 3 HP therefore takes two hits.
   - The target lock-out `+0x1D0 = 0x19` makes the hits at least 25 ticks apart.
   - **Nibble 4 = Bomb kills outright (response `0x40`).** This is the 362 lid and bomb case.

4. **FACT: the DLL has no per-entity inputs C10 needs.**
   - **TileX/TileY:** `TileX` is declared (`AlundraEntityScriptProxy.cs:316`) and never written. The binary's TileX is `table[PosX>>16]` = pixel / 24 (init at `0x8003B194-0x8003B1D4`). TileY is `PosY >> 20`.
   - **FloorHeight:** it is maintained for the hero only (`UpdateFloorHeight`).
   - **MapHeights:** the 4 footprint-corner heights `MapHeights[4]` (`+0x158..0x164`) are declared (`:381`) and never written.
   - NPC `IsOnGround` and `ForceAdjusted` do exist (`AlundraScriptedMotion`).

5. **FACT: the break effect id is not exported.**
   - `DestroyEntity(e,-1)` (`0x8003A59C`) creates effect `SpriteHeader+0x1E` ("BreakEffect"). For the Muruta this is **8**.
   - `alundra-project/Data/sprite-records.json` has no BreakEffect key. It is only in `data-extracted/data/map_N.json` (`SpriteRecords[91].Header.BreakEffect`).
   - Effect 8 exists in `Data/effects/effects-global.json` (11 frames, ends in Destroy).
   - The loot and break-effect parts of `DestroyEntity` are not in the DLL (`AlundraWorldProxy.cs:3022` only flags the destroy).
   - E1 is shared by 61 exported sprite records. This is E14 infrastructure, tied to E20.e0.

6. **FACT: decompilation divergences (the binary wins).**
   - **Z field:** `CalculateEntityRelativePosition` (`0x8007FE20`) and `HandleAnimationDirection` (`0x80080144`) read `+0x134` = FloorHeight. The decomp uses `TerrainHeight` (+0x138): `ScriptHelper.cs:96-97` comments out the FloorHeight line, and `EntityGameplayManager.cs:137` uses `TerrainHeight`.
   - **C10 `default:` bug:** in the decomp's C10 switch, every state without a handler (1, 2, 3, 4, 6, 7, 8, 11 and above 12) falls out of the switch. It then runs `AIValues[1]=0x1e; Bytes[1]=0`. In the binary these states jump to `0x80068904` and only snap the direction. Porting the decomp would reset the cooldown every tick during hops and hurt.
   - **Mis-named table:** `0x80028B34` ("g_directionFlipTable") is a counter-clockwise quarter-turn table. Entry 0 is 0x18, not 0 (see 3.4).
   - Everything else in C10, D9, `TryAttackPlayerFront/2` and `GetDirectionToTarget` matches.

7. **FACT: the Muruta appear in more waits than the brief says.**
   - **Map 115:** a second kill-wait. `B[3]` @611-637 activates records 6-9 with `0x2D` and waits with `0x2C [6..9]`. It is guarded by G261, and afterwards `0x2D [21]` appears.
   - **Map 115:** three ambush Muruta (records 12-14, `C[13]/C[17]/C[18]`, area-triggered, no wait).
   - **Map 362:** the chest rec10 (item 79) is spawned by `B[7]` @312-341 only after `0x2C [6..9]` all see zero. The dream end `B[2]` does not wait for kills.

8. **INFERENCE (recommended): run C10/D9/E1 under the repo's MIPS emulator as an oracle.**
   - C10's inputs are the 0x294-byte entity, the hero slot at `0x80127D30`, the RNG word and the helpers listed in section 5. That makes an oracle feasible, like the E20 ones (`docs/plan-e19-h2-annexe/tools/emu.py`).
   - I did not run it, per the rules.
   - The tick numbers in 4.4 are by hand and need the oracle to be exact.

---

## 1. When the native handlers run (FACT)

- **Entity table:** slot n is at `0x80127D30 + n*0x294`. The hero is slot 0. The count word is `0x80134360`. The empty template is `0x80134368`.
- **Phase 1 (pick), `0x800386D0` from `0x8003871C`:** for a Normal entity, in this order:
  - DestroyOnSlidingSlope and DestroyOnVramFlags (destroy at once);
  - DeactivateOnImpact (`0x10`), DeactivateOnHit (`0x20`), DeactivateOnAnimationEnd (`0x40`, tests `+0xAC != 0`). Any of these sets `Status=3` and trigger **4 (E)**;
  - else `+0x224` TouchingEntity != 0 gives **3 (D)**;
  - else **2 (C)**. F (5) is only chosen when `[0x80127108]` is this entity and it has an F program.
  - So a hit replaces that tick's C with D, and E beats D.
- **Phase 2 (run), `0x800388E4-0x80038944`:**
  - If `ProgramIndexes[trig] (+0x4C+4*trig) & 0x7F != 0`, run the bytecode.
  - Else take the native handler `table[trig][SpriteProgramIndexes[trig] (+0x70+4*trig)]`.
  - Handler tables: pointer array `0x80098F4C`. C base `0x800C4F34`, D `0x800C5334`, E `0x800C5734`.
  - The loop repeats while any handler ran.
- **The Muruta:**
  - C10 = `0x800C4F5C` -> `0x80068154`.
  - D9 = `0x800C5358` -> `0x8007DE04`.
  - E1 = `0x800C5738` -> `0x8007ED10`. E0 and E1 share it.
- **The Muruta's sprite (sprite 91, GUID `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08` in `sprite-records.json`):**
  - header bytes `[3,1,3,0,10,9,1,0, F6,F9,00, 14,0E,15, 08, FB]` (raw `map_115.json` `SpriteRecords[91].Header`);
  - ProgramLoad 0, Tick 10, Touch 9, Deactivate 1, Interact 0;
  - offset (-10,-7,0), size (20,14,21), BreakEffect 8, Contents 251;
  - **Flags = `lhu +0x10 | lbu +0x12 << 16` = `0x30103`** = ClassA | HitsClassB | Gravity | shadow size 3. Collidable (`0x80`) is NOT set.
- **Hit pass (E20 domain, FACT):** a target must be Normal or Deactivated (statuses 2-3), and the hero's sword reaches it (ClassA and the hero's HitsClassA). The Muruta's own claws reach the hero (HitsClassB), by body-sized frame boxes (`Attacks[0]` = attr 1, power 1). Animations 2, 5 and 6 have no box.

## 2. Muruta animation sheet (FACT, `map_115.json` raw header + frame streams; same in maps 15/362)

Frame delays are `byte & 0x7F` ticks per frame. A frame with delay d is shown d ticks. The end command is read the tick after the last frame (`0x80038BD8-0x80038D88`).

| Anim | Speed | Z impulse | AnimFlags (byte 0xD) | Sfx | Ticks | End | Role (INFERENCE) |
|---|---|---|---|---|---|---|---|
| 0 | 0 | 0 | 0 | 0 | 32 | Loop | idle (boxed) |
| 1 | 256 | 0 | 0 | 0 | 32 | Loop | walk (not used by C10) |
| 2 | 256 | 0 | 0 | 65 | 4 | Chain -> 5 | hurt, knockback along TargetDirection; no box |
| 3 | 256 | 1792 | 7 | 0 | 39 | Chain -> 10 | leap at the hero; 8 boxed frames |
| 4 | 0 | 1792 | 7 | 0 | 23 | Chain -> 10 | big hop |
| 5 | 0 | 0 | 0 | 0 | 8 | Hold | hurt hold; no box |
| 6 | 0 | 0 | 0 | 64 | 8 | Hold | death; no box |
| 7 | 0 | 768 | 3 | 0 | 25 | Chain -> 9 | claw hop in place; 8 boxed frames |
| 8 | 0 | 768 | 3 | 0 | 11 | Chain -> 9 | small hop |
| 9 | 0 | 0 | 0 | 0 | 8 | Loop | landing wait |
| 10 | 256 | 0 | 0 | 0 | 8 | Loop | landing wait, moving |
| 11 | 208 | 0 | 0 | 0 | 32 | Loop | not used by C10 |
| 12 (0xC) | 192 | 0 | 0 | 0 | 32 | Loop | walk used by C10 |

- **Hold:** sets `+0xAC = 1` (`0x80038D50`), and `+0xA8 = 0x7FFFFFFF`.
- **Chain:** writes `+0x88` and re-enters the change code. That change resets `+0xAC = 0` (`0x80038B6C`).
- **Loop:** only increments `+0xB0`.
- **Speed/force path (FACT, `0x80036614-0x800366F4`):**
  - `TargetForce = Speed (AnimSet+8 halfword) x direction table` (`0x80023654` / `0x80023694`).
  - `ForceStep = |TargetForce - Force| >> (AnimFlags & 0xF)`, so flags 7 and 3 accelerate slowly.
  - All of this is the generic path the DLL already has.
- **Sprite-record JSON caveat (UNKNOWN):** the exported `Acceleration` column is not that nibble. What it maps to would be decided by reading `AnimationSet` bytes 8-0xD in `SpriteRecord.cs`.

## 3. C10 (`0x80068154`-`0x8006892C`), exact

### 3.1 Fields (FACT)

| Offset | Name | Use |
|---|---|---|
| `+0x0C` | ParentEntity | only on map 0x99 (153) |
| `+0x6C` | Flags | `\|= 0x40` at death |
| `+0x88` | TargetAnimationId | the switch value |
| `+0x8C` | TargetDirection | 0 down, 8 left, 0x10 up, 0x18 right; 32 steps |
| `+0x98` | AnimationDirection | 0 down, 1 up, 2 left, 3 right |
| `+0xAC` | animation-end flag | set by Hold |
| `+0xBC` ... `+0xD8` | TargetForceX/Y, ForceX/Y, ForceStepX/Y | zeroed together |
| `+0x13C` | ForceAdjusted | blocked by a wall |
| `+0x144` | IsOnGround | |
| `+0x274` | Bytes[0] | 0 calm, 1 alerted |
| `+0x275` | Bytes[1] | calm patrol step counter |
| `+0x277` | Bytes[3] | dying flag, set by D9 |
| `+0x282` | AIValues[1] | u16 down-counter and cooldown |

### 3.2 Per tick, in order

1. **Map 0x99 only** (`[0x800DC5A0] == 0x99`): if `ParentEntity.Bytes[3] != 0`, run `DestroyEntity(self,-1)` and return without the tail (`0x80068170-0x80068198`).
2. `rel = CalculateEntityRelativePosition(self, hero)` (section 5.1).
3. Switch on `TargetAnimationId` (table `0x800271DC`, 13 entries). Only states 0, 5, 9, 10, 12 have code.
4. **Tail, always** (`0x80068904`): `TargetDirection = (TargetDirection + 2) & 0x1C`, so it is always a multiple of 4.
5. **Shared exits:**
   - `0x80068900`: store the pending value in `AIValues[1]`, then the tail.
   - `0x800688E0`: `AIValues[1] = 0x1E; Bytes[1] = 0`, then the tail.
   - `0x800688D4`: `Bytes[0] = 1; anim = 0`, then `0x800688E0`.

### 3.3 State 0 (idle), entry `0x800681DC`

**Calm (`Bytes[0] == 0`):**
1. If `TryAttackPlayerFront(self, rel, 2, 6, 0x100000)`: `Bytes[0] = 1`, `AIValues[1] = 0`, then the tail. (`0x80068200-0x80068218`)
2. If `AIValues[1] != 0`: `AIValues[1]--`, then the tail.
3. If `Bytes[1] == 0`: draw RNG(4). If it is not 1, the tail. (`0x8006823C-0x8006827C`)
4. `b = Bytes[1]; Bytes[1] = b + 1`. Then switch `b` (table `0x80027214`):

| b | Action |
|---|---|
| 0, 1, 2 | draw RNG(4); if 0, play sound `0xCD`. `anim = 8`. `TargetDirection = GetDirectionToTarget(hero - self)`. `AIValues[1]` unchanged. |
| 3 | draw RNG(4), `dir = cardinal[r]` with `[0x00,0x10,0x08,0x18]` (table `0x80028B54`). Then `AIValues[1] = 0x14`, zero the 6 force fields, `TargetDirection = dir`. |
| 4 | `AIValues[1] = 0x14`, zero the 6 force fields, `TargetDirection = quarterTurn[TargetDirection]` (table `0x80028B34`, section 3.4). |
| 5 | `anim = 0xC`, `AIValues[1] = 0x78`. |
| >= 6 | `Bytes[1] = 0`, `AIValues[1] = 0`. |

**Alerted (`Bytes[0] != 0`), `0x800683F4`:**
1. If neither `TryAttackPlayerFront(self,rel,2,6,0x100000)` nor `TryAttackPlayer2(self,rel,(AnimationDirection^1),2,3,0x100000)` holds: `Bytes[1]=0; Bytes[0]=0; AIValues[1]=0x1E`, then the tail.
2. If `AIValues[1] != 0`: `AIValues[1]--`, then the tail.
3. `TargetDirection = GetDirectionToTarget(hero - self)`.
4. **Lunge.** If the hero has a frame box (`+0x1D4`), a current attack (`+0x1C8`), attack attr != 0, and `rel0 < 5 && rel1 < 5`:
   - draw RNG(4); if 0, play `0xCD`;
   - `anim = 3`; `TargetDirection` toward the hero; `AIValues[1] = 0x1E`; `Bytes[1] = 0`.
5. Else by distance (`rel0 = |dTileX|`, `rel1 = |dTileY|`), with `away = (TargetDirection - 0x10) & 0x1F`:

**Close (`rel0 < 3 && rel1 < 3`): draw RNG(8), table `0x8002722C`**

| r | Action |
|---|---|
| 0 | `anim 0xC`; `AIValues[1] = 0x14`; direction toward the hero. |
| 1 | `anim 0xC`; `AIValues[1] = 0x3C`; `TargetDirection = away`. |
| 2, 3 | `anim 0xC`; `AIValues[1] = 100`; `TargetDirection = away`. |
| 4, 5 | draw RNG(4); if 0, play `0xCD`. `anim = 7`. |
| 6, 7 | `anim = 4`; `TargetDirection = away`. |

**Mid (`rel0 < 5 && rel1 < 5`): draw RNG(5), table `0x8002724C`**

| r | Action |
|---|---|
| 0, 1 | `anim = 8`. |
| 2, 3, 4 | `anim 0xC`; `AIValues[1] = 0x1E`. |

**Far (`rel0 < 7 && rel1 < 7`): draw RNG(8), table `0x80027264`**

| r | Action |
|---|---|
| 0, 1, 2 | `anim 0xC`; `AIValues[1] = 0x3C`. |
| 3 | `anim 0xC`; `AIValues[1] = 0x1E`. |
| 4, 5 | `anim = 4`. |
| 6, 7 | `anim = 8`. |

**Beyond that:** nothing. Then the tail.

### 3.4 Other states

- **State 5, `0x8006889C`:**
  - If `+0xAC == 0`, the tail.
  - Else if `Bytes[3] == 0` (recover): `Bytes[0]=1`, `anim=0`, `AIValues[1]=0x1E`, `Bytes[1]=0`.
  - Else (dying): `anim=6`; `Flags |= 0x40`.
- **States 9, 10, `0x800688EC`:** if `IsOnGround == 0`, the tail. Else `anim=0` and `AIValues[1]=0x14`.
- **State 12 (0xC), `0x80068800`:**
  - If `Bytes[0]==0` and `TryAttackPlayerFront(...)`, go to `0x800688D4` (alert).
  - `AIValues[1]--` (u16, stored).
  - If it reached 0 or `ForceAdjusted != 0`: `anim=0`, zero the 6 force fields, then `0x800688E0`.
  - Else if `HandleAnimationDirection(self, 0xC, 0x400000) != 0`: `anim=0`, then `0x800688E0`.
  - Otherwise keep walking.
- **Quarter-turn table `0x80028B34`, 32 bytes:** `[0]=0x18`, `[1..8]=0`, `[9..16]=8`, `[17..24]=0x10`, `[25..31]=0x18`.
  - Counter-clockwise quarter turn: down -> right -> up -> left -> down, diagonals going to the earlier cardinal.
  - Sample entries: 0->0x18, 4->0, 8->0, 0xC->8, 0x10->8, 0x14->0x10, 0x18->0x10, 0x1C->0x18.

### 3.5 RNG use (FACT)

- **State:** the word at `0x80098708`. Step = `s = s*0x7D2B89DD + 0xE06A02E7 (mod 2^32)`, then the result is `(s*N) >> 32`.
- **Exact draw counts per tick:**
  - calm, `AIValues[1]==0`, `Bytes[1]==0`: one RNG(4) every tick;
  - calm step 0-2: one more RNG(4); step 3: one RNG(4);
  - alerted lunge: one RNG(4);
  - close: RNG(8), plus RNG(4) on r = 4 or 5;
  - mid: RNG(5);
  - far: RNG(8);
  - no other draws.
- The DLL's `AlundraRandom` has the same constants (`AlundraRandom.cs`).
- The per-frame advance `0x8002BD14` is planned in E20.e0 (plan line 467). UNKNOWN whether it exists in the DLL today.

### 3.6 Behaviour summary (INFERENCE from the above)

- **Calm:** wanders. Three hops facing the hero, a random cardinal turn, a quarter turn, then a 120-tick walk. A new sequence starts with probability 1/4 per tick.
- **Alert:** the hero stands in the 4-way front cone, or the back cone. Front cone = lateral |d| <= 2 tiles, forward <= 6 tiles, |dFloor| <= 0x100000. Back cone: lateral <= 2, depth <= 3.
- **Pursuit:** there is no pathfinding. The chase is "face the hero, play the walk animation for N ticks". Blocked by a wall (`ForceAdjusted`) or a ledge (`HandleAnimationDirection`) it stops.
- **Position writes:** none. C10 never writes PosX/PosY/PosZ.

## 4. D9 (`0x8007DE04`), E1 (`0x8007ED10`), death

### 4.1 D9 (FACT)

```
r = ComputeNewHp(self)            // 0x8003A374; returns (newHp == 0)
if r != 0 : Bytes[3] = 1
TargetAnimationId = 2
TargetDirection = GetDirectionToTarget(self.Pos - hero.Pos)   // away from the hero
```

- `ComputeNewHp` writes `+0x14` Hp.
- The decomp's `HitCommon(gameEngine, entity, 2)` is the same, using `g_entitySlots[0]` as the hero.
- D18 (`0x8007E228`) is D9 plus `ParentEntity.+0x278 -= 1` when the Muruta dies. It belongs to a different sprite (GUID `aae35994-...`, Tick 10, Touch 18) and is out of this scope.

### 4.2 E1 (FACT)

`E1 = DestroyEntity(self, -1)` (`0x8003A59C`):
1. `SpawnEntityContents(self)` (`0x80032B90`, loot, section 5.7).
2. `Status (+0x10) = 4`; `EventTrigger (+0x228) = -1`.
3. If `ActiveEffect (+0x1B8) != 0`: clear its `+0x68` and the field.
4. Effect id = `SpriteRecord(+0x64)->byte 0x1E` = **8**. If nonzero, `CreateAttachedEffect(0, 8, 0, self, 1, 0, 0, 0)` (`0x8003A614`).
5. If `PlatformEntity (+0x28) != 0`: `platform.+0x2C = 0`.

### 4.3 Hurt and death sequence (FACT in structure, INFERENCE in ticks)

- **Non-lethal hit:** D9 gives anim 2 (4 ticks), then anim 5 (8 ticks, Hold). C10 state 5 then sets `Bytes[0]=1`, anim 0, `AIValues[1]=0x1E`, `Bytes[1]=0`. A 30-tick cooldown follows.
- **Lethal hit:** the same up to state 5. Then anim 6 and `Flags |= 0x40`. After the anim 6 Hold the E trigger fires in phase 1 (`Status=3`) and E1 runs in the same pass.
- **Tick bookkeeping (INFERENCE; needs the oracle).** Let H be the tick whose hit pass sets TouchingEntity.
  - D9 runs at H+1, and the animation pass applies anim 2 that tick.
  - Anim 2 lasts through H+4, anim 5 from H+5 to H+12, and the Hold flag is set during the animation pass of H+13.
  - C10 sees it at H+14. Anim 6 runs H+14 to H+21, and the Hold flag is set at H+22.
  - E1 runs at H+23.
  - `0x2C` still counts the slot at H+24 (map events run before the slot wipe).
  - The wipe `0x80038634` happens at the start of the entity pass of H+24. The first tick where `0x2C` sees zero is **H+25**, about 0.5 s at 50 Hz.
  - This assumes the animation pass (`0x80038E18`, listed in plan 0.2.1) is after the events pass. UNKNOWN until confirmed by the oracle.
- A second hit while dying (after the 25-tick lock-out) restarts anim 2, 5, 6. The recovery loops harmlessly.

### 4.4 Damage numbers (FACT from `balance.json` and the binary)

- Sword hit: 2 damage, 2 hits to kill.
- Bomb (nibble 4): response `0x40`, outright kill.
- All other nibbles: x1.
- Enemies never blink. There is no `+0x1CC` for them, only the 25-tick `+0x1D0` lock.

## 5. Helpers (decoded; section 7 lists who shares them)

### 5.1 `CalculateEntityRelativePosition(self, out)`, `0x8007FE20` (FACT)

- `dx = self.+0x120 - hero.+0x120`; `dy = self.+0x124 - hero.+0x124`; `dz = self.+0x134 - hero.+0x134`.
- `out[3..5] = dx, dy, dz` (signed). `out[0..2] = |dx|, |dy|, |dz|`.
- **The three fields (`0x800380DC-0x800380F4`):**
  - `+0x120` = `colTable[(PosX>>16) as s16]`. The table at `0x80127168` is filled at init as `colTable[c*24 + j] = c` for c in 0..51, so it is pixel / 24.
  - `+0x124` = `PosY >> 20`, so 16-pixel rows.
  - `+0x128` = `PosZ >> 20`.
  - `+0x134` = `GetCollisionOnZ` result (FloorHeight).
  - `+0x144` = `!(floor < PosZ)`, written in the same function.
- DLL status: INFERENCE. Per-entity on-demand computation (`ComputeFloorHeight(ComputeTerrainHeight())` for both entities) would replace the missing fields.

### 5.2 `GetDirectionToTarget(dx, dy)`, `0x8002E814` (FACT)

- Quadrant, then a 16x16 table at `0x80022488` after shifting by the thresholds in `0x80022688` (15, 31, 63, ...). It returns a 0..31 angle.
- Already in the DLL as a "bit-for-bit port" (`ScriptHelper.cs:71`).
- The Muruta call it with `(hero.Pos - self.Pos)` (16.16 `+0x114/+0x118`), and D9 with the opposite sign.

### 5.3 `TryAttackPlayerFront(self, rel, near, far, zmax)`, `0x800805C8` (FACT)

- Returns 0 if `zmax < |dz|`.
- Cases on `AnimationDirection`:

| Dir | Lateral limit | Forward condition |
|---|---|---|
| 0 (down) | `\|dx\| <= near` | `dy <= 0` and `\|dy\| <= far` |
| 1 (up) | `\|dx\| <= near` | `dy >= 0` and `\|dy\| <= far` |
| 2 (left) | `\|dy\| <= near` | `dx >= 0` and `\|dx\| <= far` |
| 3 (right) | `\|dy\| <= near` | `dx <= 0` and `\|dx\| <= far` |

- Any other direction returns 0.
- `TryAttackPlayer2` (`0x8008048C`) is the same with `(a2 = direction, a3 = near, [sp+0x10] = far, [sp+0x14] = zmax)`.
- The decomp's `TryAttackPlayerFront` calls its `TryAttackPlayer2` with the object's own direction. This matches.

### 5.4 `HandleAnimationDirection(self, animId, dist)`, `0x80080144` (FACT)

- `thr = dist + 1`. `Dk = FloorHeight(+0x134) - MapHeights[k]` for k = 0..3 (`+0x158..+0x164`). `bk = (thr < Dk)`.
- `d = TargetDirection`.
- Returns 1 ("TURN") iff
  `(b0 && b1 && 9<=d<=23) || (b1 && b3 && 17<=d<=31) || (b3 && b2 && !(8<=d<=24)) || (b2 && b0 && d<16)`.
  The code orders these as early returns, but they are logically an OR.
- TURN: `TargetAnimationId = animId & 0xFF`; zero the 6 force fields; `TargetDirection = quarterTurn[d]`; return 1.
- Otherwise it returns 0 with no effect.
- INFERENCE: the order [0] NW, [1] NE, [2] SW, [3] SE of the corners follows from the direction ranges.
- C10 calls it with `(0xC, 0x400000)`, so a drop of more than 4 height steps (0x400000 = 64 px) triggers the turn.

### 5.5 Sound, damage

- `PlaySoundEffect` (`0x800490FC`) is called with `0xCD` (growl).
- `ComputeNewHp` and `ResolveBalanceTarget` are ported as `AlundraDamage.ComputeNewHp(target, sheet, equipment, latch)`.

### 5.6 Direction and sprite tables

- `0x80028B54` = `[0x00, 0x10, 0x08, 0x18]` = the DLL's `AnimationTables.CardinalDirectionTable`.
- `0x80028B34` is not in the DLL.

### 5.7 Loot (FACT, `0x80032A40`, `0x80032968`, `0x80032B90`)

- **Rolled at creation** (`InitializeEntity` `0x80039E38` -> `0x80032A40`): `ContentsItemId (+0x3C) = ChooseRandomlyAnItem(code)`.
  - Code = the record's Contents byte if the game flag is not set, else the sprite header's `+0x1F` (251 = 0xFB for the Muruta).
  - A record with a nonzero `ContentsGameFlag` already set gives no loot.
- **`ChooseRandomlyAnItem`:**
  - code >= 0x100 returns 0;
  - code & 0x80: draw RNG(16), code = `byte[0x80028E0C + (code&0x7F)*16 + r]`, and repeat;
  - otherwise the item is `code` if below 0x62, else 0.
- **Row 0xFB** is `[0 x9, 0xF6, 0xF6, 0xFD, 0xFE x4]`:
  - nothing 9/16;
  - item 69 19/256;
  - item 70 19/256;
  - item 71 5/128;
  - item 84 3/16;
  - item 85 1/16.
- INFERENCE: items 69/70/71 are the 1/2/10-gilder coins (`items-properties.json` rows `[25,0,1|2|10,...]`) and 84/85 the hearts.
- So a Muruta spawn consumes 1-2 RNG(16) draws at creation.
- **Dropped at `DestroyEntity`** (`0x80032B90`):
  - unless `CanDropMpItems(item)` vetoes (items 0x26, 0x51, 0x52 when `GetPlayerMpMax() != 0`);
  - `SpawnWarpEntity(0, item+0x1E, self.PosX, PosY, PosZ)`;
  - the dropped item gets `ForceZ (+0xB8) = 0xA0000`, `Bytes[0]=1`, `Flags &= ~0x80`, `+0x278 = 600` (or -1), `+0x280 = ContentsGameFlag`, `+0x284 = 0xA0000`;
  - sound `0x54`.
- This is E20.e0's domain (plan D-E20-17).

## 6. Records and programs

### 6.1 Maps in scope (FACT, `alundra-project/Maps/*/tilemap` Entities layer and `m.py` dumps)

`EventCodes*_Index` is the raw byte; `& 0x7F` is the program index; 0 means native.

| Map | Records | A | C raw (idx) | D | E | F | Spawn | Notes |
|---|---|---|---|---|---|---|---|---|
| 15 | 0, 1, 2 | 0 | 134, 135, 136 (6, 7, 8) | 0 (native D9) | 0 (native E1) | 0 | `0x2D [0,1,2]` in `B[1]` @93-97 | pure scripts, never native; destroyed by `0x2E [128]` @712, @777, @890; `B[1]` waits `0x2C [2]` @109 |
| 115 | 24-27 (hall) | 0 | 153 (25) | 0 | 0 | 0 | `0x2D [24..27]` @401-407 in `B[2]` | `C[25]` @1464 then `0x40 [2,0]` -> native; `B[2]` waits `0x2C [24..27]` @414-431, then G569 @434 |
| 115 | 6-9 (second wave) | 0 | 135 (7) | 0 | 0 | 0 | `0x2D [6..9]` @611-617 in `B[3]` | `C[7]` -> native; `B[3]` waits `0x2C [6..9]` @620-637, then `0x2D [21]`; G261 skips it |
| 115 | 12, 13, 14 (ambush) | 0 | 141, 145, 146 (13, 17, 18) | 0 | 0 | 0 | auto (`SpriteDirection` 195) | wait for the hero in an area, leap (anim 3), then `0x40` -> native; no wait |
| 362 | 6-9 | 0 | 134-137 (6-9) | 0 | 0 | 0 | `0x8A [6..9]` @182-212 in `B[4]` | `C[6..9]`: wait radius, anim 0, sound 205, wait T7, `0x40 [2,0]` -> native; `B[5]` @240 and `B[7]` @312 `0x2C [6..9]`; `B[7]` spawns chest rec10 (item 79) only when all four are gone; `B[2]` ends the dream without waiting |

Where `C raw` is 7-bit-masked to a nonzero value, a bytecode program exists for that slot.

### 6.2 Corpus (FACT, scan of the exported tilemaps)

- 203 records named "Muruta (griffes) Niv.1", all sprite 91, in 25 maps.
- 175 have a C program (low 7 bits non-zero); 28 are pure native from the start.
  - Maps 110 (15-18), 114 (17-21), 151 (15-19), 152 (17-20), 271 (0-3), 293 (2-5, with E scripts), 383 (2, 3).
- D scripts: 5 records (maps 1 and 2). E scripts: 4 records (map 293).
- Auto-spawn (`SpriteDirection & 0x40`): 35 records.
- Other Muruta types (boomerang, sprite 100; arc, sprite 94; Zazan, sprite 250) use other handlers and are out of scope.

## 7. Shared helpers (they become E14 infrastructure)

Callers counted from `jal` sites in the exe and mapped to handler slots (`callers.py` in scratch).

| Helper | Sites | Handlers |
|---|---|---|
| `CalculateEntityRelativePosition` `0x8007FE20` | 46 | 45 C handlers |
| `TryAttackPlayerFront` `0x800805C8` | 30 | 18: C7, 9, 10, 12-16, 18, 27-30, 32/34, 35, 39, 40, 50 |
| `TryAttackPlayer2` `0x8008048C` | 6 | C10, C46, C49 |
| `HandleAnimationDirection` `0x80080144` | 16 | 14: C91, 101, 2, 3, 7, 8, 10, 11, 13, 14, 25, 28, 32/34, 35 |
| `GetDirectionToTarget` `0x8002E814` | 158 | 82, including every D handler up to D46 |
| `ComputeNewHp` `0x8003A374` | 37 | per plan |
| `DestroyEntity(e,-1)` | E0/E1 | 61 exported sprite records use E1 |

Also shared: the quarter-turn table `0x80028B34`, the cardinal table `0x80028B54`, the RNG draw sequence, the dispatch rules of section 1, and the loot rows `0x80028E0C`.

## 8. What the hall of 115 needs for `0x2C`

- **`0x2C [N]`** (`0x8003DC84`): `Result = (GetMatchingEntityBySearchType(logicEntity, N) == 0)`, size 2.
- **Search for N < 0x80** (`0x8003C954-0x8003C9F8`): scans slots 1..count. A slot matches if its `EntityRefId (+0x48) == N`. **The test on a candidate has no status filter.** The only status test is on the owner: `owner.Status - 1 < 3`, i.e. Loaded, Normal or Deactivated. The hall's owner is the hero after `0x42` @372.
- **What "destroyed" means:**
  - `DestroyEntity` sets `Status = 4` but leaves `+0x48`. The slot stops matching only when `UpdateDestroyedEntities` (`0x80038634`) overwrites it with the template (start of the next entity pass, after the map-event pass).
  - So the first `0x2C` that sees zero is the map-event pass of the tick after the wipe tick.
  - The DLL already does this: `RecycleDestroyedEntities` (E19.r R3), `EntitySearchService.cs:104-117`.
- **Per record:** each of the four must reach `Status 4`, and the only path is C10 state 5 -> `Flags|=0x40` -> anim 6 Hold -> phase-1 E pick -> E1.
- **Per Muruta:** 2 sword hits (or one bomb), at least 25 ticks apart.

## 9. DLL state vs what E14.a must add (FACT unless stated)

Exists:
- spawn and activation: `0x2D`, `SpawnEntityByRecordId`;
- bytecode preamble ops `0x2A`, `0x2B`, `0x25`, `0x37`, `0x40`;
- dispatch hook `runner.RunSpriteEvent(this)` (`AlundraEntityScriptProxy.cs:1629`);
- the pick logic including `DeactivateOnAnimationEnd` (`:1563`);
- E0/E1 destroy-only (`AlundraEventProgramRunner.cs:312-319`);
- the hit pass and `AlundraDamage`;
- Hold sets `ForceResetAnimationFlag` (`AlundraEntitySpawnFactory.cs:321`);
- NPC `IsOnGround` and `ForceAdjusted`;
- the `Bytes` and `AIValues` fields (`:1129-1132`);
- the RNG and `GetDirectionToTarget`.

Missing:
- the C10 and D9 bodies (today counted no-ops);
- per-entity TileX/TileY/FloorHeight and corner heights (section 0 item 4);
- the quarter-turn table;
- `DestroyEntity` effect and loot;
- the BreakEffect export;
- the loot roll at creation.

## 10. UNKNOWN (what would decide each)

- Exact tick offsets of death (section 4.3): decided by a MIPS-emulator run of C10/D9/E1 with the animation pass, or an in-game trace.
- Initial `Bytes`/`AIValues` of a fresh entity: the template at `0x80134368` is BSS and not readable from the file. INFERENCE: zeros, consistent with the DLL defaults.
- Corner order of `MapHeights` (section 5.4): decided by reading `ComputeEntityGroundHeight` (`0x800370C4`) loop order.
- Whether the DLL advances the RNG once per frame (`0x8002BD14`): E20.e0 plans it. Decided by a grep of `AlundraWorldProxy` after E20.e0.
- Whether the hall Muruta see the hero in their front cone at spawn: needs the spawn positions in tile units against the hero's arrival tile (10,55).
- The meaning of the exported `Acceleration` column in `sprite-records.json`: decided by `SpriteRecord.cs` field offsets 8-0xD.
