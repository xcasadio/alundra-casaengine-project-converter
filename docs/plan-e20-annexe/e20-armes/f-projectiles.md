> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# f-projectiles: entities and native handlers of the hero's projectile weapons

Read-only discovery of 2026-10-09. FACT means decoded instruction words (capstone, `ALUN_CD.EXE` France) or a measurement on my oracle. INFERENCE and UNKNOWN say what would decide them. `C60`, `E17` and so on are indices in the native handler tables.

## Things that change the plan

1. **The sharing premise of D-E20-18 and D9 is refuted, so E20.f can own every hero-weapon projectile handler.**
   - FACT, census of all `map_*.json` and `map_alundra.json`: E17, C60, E9, C61, E10 and C94 appear only on common-bank sprites (the 257 `SpriteRecords` of `map_alundra.json`, called "global" below).
   - These are global 3-6 (E17), 7 (C94), 10-15 (C60+E9) and 27-29 (C61+E10). No map-local sprite uses them.
   - Fire is not C54. C54 (`0x80078E34`) is map 326 idx 190, a boss (A16 C54 D27 E11). Fire and ice both run C60 and E9; a type 11/12 vs 14/15 test selects the effect id.
   - The hero weapon routine (`0x8002F818`) is the only spawner of types 3, 4, 7, 11, 14, 15 and 27-29.
   - The only other spawner of any of them is enemy handler E20 (`0x8007F6C8`, map sprite 137 on maps 86-94, 441, 442). It spawns global 12 and zeroes the six program words `+0x70..+0x84`, so C60/E9 never run on that copy. That belongs to E14.
   - From the shared set the hero needs only E1, the generic destroy. Its charged-arrow case needs the header `BreakEffect` byte, which is 8 (D-E14-2).
2. **A spawned entity runs its whole spawn tick.**
   - FACT, oracle: the arrow is already status 2 at the end of its spawn tick S, has run A0 and the counters pass, and has moved (3 px).
   - Cause: world init `0x8003B24C` (the only caller is `0x8002E02C`) writes slot index `+0` for all 64 slots. `InitializeEntity` then raises `[0x80134360]` immediately (`0x80039D24`), so the events-pass loop that follows `MovePlayer` covers the new slot.
   - My first runs had zeroed slot indices and showed S+1; that was a harness artefact, since fixed.
   - The DLL's `RunEntityBlock` builds `CollectTickEntities()` before `MovePlayer`, and `AddEntity` is queued (E20.a rule). Every hero shot would therefore be at least one tick late.
   - This answers O-E20-11: yes, the binary runs a new entity in the same tick. E20.e0 must put the entity in `_tickEntities` at once.
3. **Normal arrows (types 3, 4) fly at hero Z + 16 px and miss bodies with `SizeZ` ≤ 16.**
   - FACT, oracle: the box Z window is `[heroZ+16, +27)`. A target at the hero's level is hit only when `SizeZ` ≥ 17 (tested for 8, 15, 16, 17, 24).
   - The Beannoïde (`SizeZ` 8, the first E14.a enemy) is missed by Hunter's and William's normal arrows.
   - The charged arrow (Z+8, box offset −8), fire/ice balls and the wave sword all hit it.
   - INFERENCE, proxy census: 88 of 217 map-local ClassA sprites with MaxHp > 0 have `SizeZ` ≤ 16.
   - This is faithful to the binary but will look like a bug in a recette. It is a question for the author.
4. **E17's valid-target filter reads `+0x1CC == 0`, not `DamageResponses[5]`.**
   - The decompilation (`FunctionTypeE.cs:AI_UpdateArrows`) says `BalanceRecord?.DamageResponses[5] == 0`; D9 repeated it.
   - FACT, binary `0x8007F508` is `lw 0x164(a0)` with `a0 = slot+0x68`, so it reads `+0x1CC`.
   - The oracle confirms it: the arrow is destroyed on a Beannoïde whose responses are all 16.
   - The scan excludes the target type `[+0x68] == 0x1AD`, which is map-local sprite 173. FACT: that is the "Interrupteur ballon" on maps 16, 60, 343, 344, whose D program uses `0x87` arrow (map 16 D544). The arrow is not destroyed by it and keeps flying.
5. **Fire and ice balls take no Z impulse at spawn.**
   - FACT: anim 0 has impulse 512, but `UpdateAnimation` clears `+0xF8` at every call. The spawn-time call is cleared by the same tick's animation pass (`0x80038E18`) before physics reads it.
   - So the ball is a pure gravity drop from hero Z + 32 px at 6 px/tick (4 px/tick vertically). On level ground it lands at S+10, 66 px away (ice 63 px).
   - The DLL's R2 rule ("no impulse for an appearance animation", `AlundraFrameSyncPasses.cs:209-226`) already agrees.
   - The arrow's anim-1 switch does take its impulse (1024, giving ForceZ 262144), because it happens inside the animation pass.
6. **What the DLL lacks for these projectiles.**
   - A spawn-by-global-index primitive (E20.e0) and a global index → prefab id table. The export has no such table: `sprite-records.json` is keyed by prefab id, and the index only exists in file names `bankalundra_<N>_anim*`. I checked the 19 relevant indices by name.
   - A generic Z model for non-hero entities (gravity bit 0x100, impulse pulse, landing, terrain-height blocking for a constant-Z flyer). The Z model in `AlundraScriptedMotion.cs:226-380` is hero-only.
   - `DestroyEntity` with effect and attach, and `BreakEffect` in the export.
   - A shared `CheckAndTriggerTileEffect(entity)` (E20.e), also used by C61 and C94.
7. **RNG.** Spawn draws nothing (`Contents` 0). Only E9 draws: 10 draws at the impact tick, then 2 draws every 8th tick of the explosion. Everything else draws 0.
8. **Handler order and the whole chain are verified on a real-code oracle**, with two interpreters identical (section 0).

## 0. Method and evidence

- **Static.** Master table `0x80098F4C` = {A `0x800C4B34`, B 0, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`}. I decoded the handlers, `SpawnWarpEntity`, `InitializeEntity`, `DestroyEntity`, `CheckAndTriggerTileEffect` and `UpdateWeaponStepProgression`. I scanned all 102 `jal 0x80039F58` call sites for table and type.
- **Dynamic.** I copied `docs/plan-e20-annexe/e20-c1/oracle/tools` into scratch and added `pworld.py`:
  - real `0x80039F58` (c1 stubbed it) and real `MovePlayer` with the real weapon equip (AddItem/SetPlayerWeaponId);
  - the global sprite records of `map_alundra.json` laid out behind block `0x80126EC0` (+0xC pointers, +0x10 count);
  - real projectile handlers (C60 C61 C94 E1 E9 E10 E17), all other handlers inert stubs;
  - effects, sound and the effects pass stubbed and journalled;
  - flat 52x60 field, map-10 constants, hero at (100,100) facing east, Beannoïde targets from map 14 (inert).
- **Cross-check.** `diff2.py` runs hunter_wall, fire_rod, ice_rod_target, steel_charged and william_charged on `emu.py` and on `mips.py` (via `interp2.py`): rows, journal and RNG identical over 70 to 150 ticks each.
- **Limits.**
  - The template block `0x80134368` is zero in the oracle (165 words read at `0x80038668` after a destroy). The real template is BSS zero except `+0x48 = -1` (`0x8003B21C`).
  - Targets are inert, so the real D1 reaction is not exercised.
  - The field is synthetic (flat, plus a wall column, a grass cell, a deep-water cell and raised tiles).
  - Body size of one target was patched (`SizeZ` 32, or 8 to 24 in the sweep), documented in each script.
- **Scratch folder** (`<S>` below): `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/f-projectiles`.
  - `<S>/exe.py`: disassembler and reader.
  - `<S>/oracle/`: `pworld.py`, `p1.py`..`p13.py`, `diff2.py`.
  - `<S>/out/traces/*.txt`: 18 traces, listed in section 11.
  - Run: `cd <S>/oracle && python -B p7.py steel` and similar.

## 1. What the hero spawns

- FACT, `UpdateWeaponStepProgression` (`0x8002F768`, called at `0x80031D70`). The only hero spawn site is `0x8002F818`: `SpawnWarpEntity(owner = hero 0x80127D30, table 0, type = rec[1], X = hero +0x114, Y = hero +0x118, Z = hero +0x11C + (rec[3] << 16), dir = [0x80023644 + 4*(hero +0x98)])`.
  - The position is the hero's own, with no offset along the direction.
  - The direction table is {0, 0x10, 8, 0x18} for AnimationDirection 0 (down), 1 (up), 2 (left), 3 (right).
- FACT, weapon table `0x80028C78` (13 bytes: charge flag, then ground, air and charge as anim, type, delay, z). The nonzero spawn types are exactly {3, 4, 7, 11, 12, 14, 15, 27, 28, 29}:

| Rec (item) | Ground | Air | Charge |
|---|---|---|---|
| 8 (5 Hunter's bow) | 0x13, type 3, d8, z 0x10 | 0x17, type 3, d0, z 0x10 | none |
| 9 (6 William's bow) | 0x13, type 4, d8, z 0x10 | 0x17, type 4, d8, z 0x10 | 0x13, type 7, d8, z 8 |
| 4 (16 Fire rod) | 0x11, type 11, d10, z 0x20 | 0x15, type 11, d10, z 0x20 | 0x11, type 12, d10, z 0x20 |
| 5 (14 Ice rod) | 0x11, type 14, d10, z 0x20 | 0x15, type 14, d10, z 0x20 | 0x11, type 15, d10, z 0x20 |
| 1, 2, 3 (Steel, Demon, Holy) | no spawn | no spawn | 0x18, type 27 / 28 / 29, d20, z 0 |
| 6, 7 (flail, Crusher), 0, 10, 11 | no spawn | no spawn | no spawn |

- **Flail and Crusher: no entity** (type 0 in the table), and no other hero spawn site exists. The Chain Throw / Crusher swing is the hero's own animation and boxes; bounce logic is in `CreatePlayerAnimationEffects`.
- **Aura and charge sparks are effect records**, not entity slots. The aura is `fxattach(id 3, hero)`, its pointer is `[0x80126FF8]`, and the pool is the 128 effect records at `0x80138608`.
- **No lightning or thunder weapon exists**. Items (seeds, bomb, magic) are `TryUseItem` callers `0x80034224..0x80036218` with immediate global types 1, 2, 0x10, 0x11, 0x13-0x18 (E20.h).
- **Spawn tick S**, FACT oracle. The pointer is set on the press tick P (or release tick R), and the (delay+1)-th call after it spawns.

| Weapon | S |
|---|---|
| Hunter ground | P+9 |
| Hunter air | P+1 |
| William ground and air | P+9 |
| William charged | R+9 |
| Fire / ice | P+11 |
| Fire / ice charged | R+11 |
| Charged sword | R+21 |

  - The anim must keep the b1 flag of `0x80028D14` (0x11, 0x13, 0x15, 0x17, 0x18), else the pointer is cleared with no spawn.
  - A full pool drops the shot silently and nothing is refunded.

## 2. `SpawnWarpEntity` (`0x80039F58`) and `InitializeEntity` (`0x80039D04`)

- **Signature, FACT:** `(owner a0, table a1, type a2, X a3, Y [sp+0x10], Z [sp+0x14], dir [sp+0x18])`.
  - Table 0 uses the global block `0x80126EC0` and outputs 0xB and 0x60 into `+0x1B0/+0x1B4`.
  - Any other table uses the map block `0x80126E70`, outputs 0 and 0x20, and type + 0x100 in `+0x68`.
  - `0x80039B28` rejects a pointer outside `0x80000000..0x801FFFFF`. `0x80039AD0` takes the first slot 1..0x3F with status 0, so 63 slots.
- **Initial state, FACT:**
  - `+0xC` = owner, `+0x44 = 0`, `+0x48 = -1`.
  - `+0x68` = type index, `+0x10 = 1`, `+0x4 = ++[0x80134600]`.
  - `+0x88 = 0` (TargetAnimationId), `+0x8C = dir`, `+0x90 = ~0` and `+0x94 = ~dir` (so the first animation pass sees a change).
  - `+0x6C` flags = header `u16@0x10 | u8@0x12 << 16`. Codes A..F = header bytes 0x13..0x17 into `+0x70, +0x74, +0x78, +0x7C, +0x80, +0x84`.
  - Balance sheet `+0x1C4 = 0x80044550(type)`. HP `+0x14/+0x18` comes from the sheet byte 2.
  - `0x8004201C` copies map program indices; with no record (`+0x44 = 0`) it does nothing.
  - `0x80039C40` sets the body box from header bytes 0x18..0x1D: offsets `<<16` into `+0x1E4/+0x1E8/+0x1EC`, sizes `(n << 16) - 1` into `+0x1F0/+0x1F4/+0x1F8`.
  - Position: X and Y as given. `+0x11C = Z - bodyOffsetZ + 1` (so hero resting raw Z 1 gives a charged arrow raw 524290). `0x800370C4` fills `+0x138`, and Z is raised to `ground + 1` if lower.
  - `0x80038AB4` runs once, so frame 0's sound, `CurrentAttack` and box exist at spawn. `0x80038064` sets the HitBox. `0x80032A40` rolls loot: header `Contents` 0 gives 0 draws.
- **Spawn-tick order, FACT oracle:** init → events pass of tick S (A0 `0x8006174C`: `AIValues[0] = PosZ >> 16`; status → 2) → counters (`+0x1C` = 1) → animation → physics. The first step is full: 3 px for the arrow (byte D 0x41), 6 px for byte D 0x40.

## 3. Global sprites, data (FACT, `map_alundra.json` + `balance.json`)

Flags are `MoreFlags | CanPickup << 8 | Shadow << 16`. Bits: 2 = HitsClassA, 3 = ClassB, 4 = DeactivateOnImpact, 5 = OnHit, 6 = OnAnimationEnd, 7 = Collidable, 8 = Gravity, 13 = NoObstacleSlide, 20 = DestroyOnSlidingSlope. Anim byte D: low nibble = acceleration shift; 0x40 = invulnerable (skipped as hit target), 0x80 = not collidable. The impulse is `(Flags << 8) | Unknown` read as s16.

| Type (prefab) | Flags | Codes | Body off / size | Anim 0: speed, byte D, sfx, ticks, end, box | Anim 1: speed, impulse, sfx, ticks, end | Attack (attr, power) |
|---|---|---|---|---|---|---|
| 3, 4 Flèches Niv.1/2 | 012074 | E17 | (-10,-7,0) 21x15x8 | 512, 0x41, 0, 12, loop, (-12,-8,0) 24x16x11 | 160, 1024, 34, 16, hold, none | one attack, 131 (nibble 3), power 1 / 2, serves every anim |
| 5, 6 | 012074 | E17 | same | same | same | none |
| 7 Flèches chargées | 032054 | C94, E1 | same, BreakEffect 8 | 512, 0x40, 33, 16, loop, (-12,-8,-8) 24x16x15 | 0, 0, 0, 16, hold, (-12,-12,0) 23x23x15 (unused) | 131, power 5 |
| 8, 9 | 032054 | E1 | same | same shape | | none |
| 11 Boule de feu | 132154 | C60, E9 | 21x15x8 | 512, 0x40, 0, 12, loop, (-15,-10,-6) 30x20x15, impulse 512 | 0, 0, 39, 36, hold, (-15,-10,0) 30x20x31 | `Attacks[anim+1]`: 134 (nibble 6), power 4, [4] = (6, 1) |
| 12 chargée | 132154 | C60, E9 | 21x15x15 | same; anim 1 sfx 41, box 36x24x31; anim 2 loop; anim 3 hold 36, box 21x15x15 | | 134, power 12; [4] = (6, 1) |
| 14 Boule de glace | 132154 | C60, E9 | 21x15x8 | 512, 0x41, 0, 12, loop; impulse 512 | 0, 0, 38, 32, hold | 135 (nibble 7), power 4, one attack |
| 15 chargée | 132154 | C60, E9 | 21x15x16 | same | sfx 40, 56 ticks, box 36x24x31 | 135, power 12 |
| 10, 13 | 132154 | C60, E9 | | | | none |
| 27, 28, 29 Épée d'onde | 00205C | C61, E10 | 21x14x8 | speed 0, byte D 0xC1, sfx 30, 26 ticks, hold; 13 frames of 2 ticks | 0, 0, 0, 16, hold | 129 (nibble 1), power 10 / 20 / 38 |

- **Wave sword box.** It is a 36x24x31 box sweeping per frame: first frame at (−18,−12), then +12 px/frame in X (east: 42 final, west −78) or +8 px/frame in Y (down 28, up −52), frozen from frame 6 to 12. The entity itself never moves.
- **Attack attributes.** Bit 0x80 = adds the hero's equipment power; the low nibble is the 0x87/0x95 index source (table `0x80098FA4` = {1,2,3,4,5,5,6,7}).
- **Effect ids.** Exported effects 2, 4, 5, 6, 8, 9, 16, 17 (0x11), 18 (0x12), 25 (0x19) exist in `effects-global.json`.
- **Prefab names.** Global index → prefab: 2 Bombe, 3/4 Flèches Niv.1/2, 7 Flèches chargées, 11/12 Boule de feu (chargée), 14/15 Boule de glace (chargée), 27-29 Épée d'onde Niv.1-3. 5, 6, 8, 9, 10, 13 are `bank_alundra_N`.
- **Handler entry addresses.**

| Handler | Table entry | Handler address |
|---|---|---|
| A0 | `0x800C4B34` | `0x8006174C` |
| C60 | `0x800C5024` | `0x8007A8A0` |
| C61 | `0x800C5028` | `0x8007A958` |
| C62 (bomb) | `0x800C502C` | `0x8007A978` |
| C94 | `0x800C50AC` | `0x8007C0D8` |
| E1 | `0x800C5738` | `0x8007ED10` |
| E9 | `0x800C5758` | `0x8007EF50` |
| E10 | `0x800C575C` | `0x8007F23C` |
| E12 (bomb) | `0x800C5764` | `0x8007F27C` |
| E17 | `0x800C5778` | `0x8007F420` |
| D30 (bomb) | `0x800C53AC` | `0x8007E79C` |

## 4. Handlers (FACT unless stated)

Pick rules already in the DLL (`PickEventTrigger`): `DestroyOnSlidingSlope && Slope_18c == 4` → `DestroyEntity(e, 6)`; else `DeactivateOnImpact && (ForceAdjusted || IsOnGround)`, or `DeactivateOnHit && HitCounter`, or `DeactivateOnAnimationEnd && ForceResetAnimationFlag` → status 3 and E; status 3 keeps picking E every tick.

### C61 (`0x8007A958`), wave sword tick
`CheckAndTriggerTileEffect(e)` only.

### C94 (`0x8007C0D8`), charged arrow tick
- `CheckAndTriggerTileEffect(e)`.
- Then if `(+0x1C & 3) == 0`: `p = CreateEffect(0, 0x19, e.+0x98, X, Y, Z + 0x80000)`. If `p`: `p.+0x54 = (-e.+0xC4) >> 1; p.+0x58 = (-e.+0xC8) >> 1`.

### C60 (`0x8007A8A0`), ball trail tick
- If `(+0x1C & 3) == 0`: `fx = (+0x68 - 0xB) < 2u ? 0x11 : 0x12; zoff = fire ? 0x80000 : 0`.
- `p = CreateEffect(0, fx, 0, X, Y, Z + zoff)`. If `p`: `p.+0x54 = -e.+0xC4; p.+0x58 = -e.+0xC8; p.+0x5C = -e.+0xB8`.

### E10 and E1
E10 (`0x8007F23C`) and E1 (`0x8007ED10`) are both `DestroyEntity(e, -1)`.

### DestroyEntity (`0x8003A59C`)
1. `0x80032B90(e)`: loot drop if `+0x3C != 0` (not for projectiles).
2. `+0x10 = 4`, `+0x228 = -1`.
3. If `+0x1B8 != 0` (child): clear the child's `+0x68` and `+0x1B8`.
4. If `fx == -1`: `fx = header[+0x1E]` (BreakEffect). If `fx != 0`: `0x8003BE74(0, fx, 0, e, 1, 0, 0, 0)`.
5. If `+0x28 != 0`: that object's `+0x2C = 0`.

### E9 (`0x8007EF50`), ball explosion
- `fx` and `zoff` as in C60.
- **Branch A**, `e.+0x88 == 1` (explosion running):
  - If `+0x18C ∈ {1, 4}`: `DestroyEntity(e, 6)`, and it continues.
  - If `+0xAC == 1`: `DestroyEntity(e, -1)`, return.
  - If `(+0x1C & 7) == 0`: `p = CreateEffect(0, fx, 0, X, Y, Z + zoff)`. If `p`: `a = Next(); b = Next(); p.+0x5C = 0x20000; p.+0x54 = hi(a*0x30001) - 0x18000; p.+0x58 = hi(b*0x20001) - 0x10000`.
- **Branch B**, first call after impact:
  - If `+0x18C ∈ {1, 4}`: `DestroyEntity(e, 6)`, return.
  - 5 times: `p = CreateEffect(0, fx, 0, X, Y, Z)`. If `p`: `a = Next(); b = Next(); p.+0x54 = hi(a*0x60001) - 0x30000; p.+0x58 = hi(b*0x40001) - 0x20000`.
  - Then `+0x88 = 1; +0x6C = (+0x6C | 0x40) & ~0x30`. The anim-1 explosion has its own attack and box and ends with a hold.
- `Next()` is the shared generator (`0x80098708`, `s*0x7D2B89DD + 0xE06A02E7`). `hi(x*k)` is the high word of the unsigned 64-bit product.
- Slope 4 is the cell ground-property bits 0xE00 (`prop 8` gives slope 4), valid only when the corners' ground height equals `PosZ + ModZ`, i.e. landed. The meaning of slope 1 is UNKNOWN; what decides it is the ground-property census of `UpdateGroundSlope`.

### E17 (`0x8007F420`), arrow deactivate
```
if +0x88 == 1:                              # 0x8007F430, bounce phase
    if +0xAC == 1: DestroyEntity(e,-1)      # 0x8007F450
    return
if +0x220 (HitCounter) != 0:                # 0x8007F468
    m = ((F<<2)&8) | (F&4 ? 1 : 0) | (F&0x1000 ? 0x800 : 0)   # F = +0x6C
    if m != 0:
        found = false
        for n in 0..[0x80134360]:           # inclusive, hero slot 0 included
            t = slot[n]
            skip if t == e or (t.+0xB4 & 0x40) or t.+0x1CC != 0 or (t.+0x6C & m) == 0
            if overlap(e box +0x1FC.. +0x204, sizes +0x214..+0x21C,
                       t body +0x1D8.. +0x1E0, sizes +0x1F0..+0x1F8) and t.+0x68 != 0x1AD:
                found = true; break
        if not found: e.+0x10 = 2; return   # 0x8007F5FC, the arrow flies on
    DestroyEntity(e, -1)                    # 0x8007F60C, then falls through
+0x88 = 1                                   # 0x8007F618
+0x8C = (+0x8C + 0x10) & 0x1F               # reverse direction
+0x6C = (+0x6C | 0x140) & ~0x30             # Gravity + AnimEnd, no Impact/Hit
```
- The overlap is the hit pass's inclusive form: `d = a - t`; if `d ≥ 0` then `d < tSize + 1`, else `-d < aSize + 1`, with sizes stored `(n << 16) - 1`. `AlundraHitPass.AxisOverlaps` applies.
- Fall-through: after `DestroyEntity` the `+0x88 = 1` and flag writes are harmless (status 4).
- The arrow's own anim flag makes it invulnerable. It is not Collidable, so it never blocks on bodies, only on walls, edge and terrain heights.

## 5. Verified timelines (oracle, level ground, east, hero (100,100))

Traces are in `<S>/out/traces/`. `S` is the spawn tick; positions are after the tick.

- **Arrow (3, 4), Hunter ground P=1, S=10:**
  - x = 103 at S, then +6 per tick. Z is constant at 16 px (raw 1048578); no gravity (`ground_144` stays 0).
  - North and south: 2 px then 4 px per tick, because the vertical offset table is 2/3 of the horizontal.
  - Wall column at x = 360: `ForceAdjusted = 1` at I with x = 349 (body edge touches).
  - At I+1 (status 3): anim 1, direction 24 → 8, ForceX −122880, ForceZ +262144, gravity −32768/tick, sound 34, flags `012144`, box gone.
  - ForceResetAnimationFlag at I+17, status 4 at I+18, slot free after.
  - On the map border (field 1248 px): impact at x = 1237, same sequence (I = t200, E t201, reset t217, status 4 t218).
  - Tile height byte 1 (16 px) at col 15 does not block; byte 2 (32 px) blocks.
  - Hit with a target of `SizeZ` 32 at x = 300: arrow hit written at t40 (`HitCounter` 1, target `+0x1D0 = 25`, 3 sparks), E17 at t41 → status 4 (nothing else, BreakEffect 0).
- **Charged arrow (7), William charged, R=96, S=105:**
  - Z = 8 px (raw 524290), 6 px/tick from S (px 106 at S), sound 33 at S.
  - Trail effect `0x19` at ticks where the pre-tick counter is a multiple of 4 (first at S+4), position = previous tick position, Z+16.
  - Hit: `HitCounter` 1, no E (no `DeactivateOnHit`): it pierces and flies on. Hits the Beannoïde (`SizeZ` 8) too.
  - Wall impact: E1 next tick → `fxattach(id 8, e)`, status 4 (no bounce).
- **Fire / ice ball (11 / 14), P=1, S=12:**
  - Z = 32 px, then 31.5, 30.5, 29, 27, ... (gravity −32768/tick, no impulse).
  - Fire 6 px/tick from S; ice 3 px then 6 px (byte D 0x41).
  - Trail effect `0x11` (fire, Z+8) or `0x12` (ice) at counter multiples of 4.
  - Lands at S+10 (x = 166 fire, 163 ice), `ground_144` = 1.
  - Impact tick: E9 branch B: 5 effect particles, 10 RNG draws, sound 39 (fire) / 38 (ice), anim 1, flags `132144`. Explosion lasts 36 ticks (fire) / 32 (ice): reset at E+36 / +32, status 4 one tick later.
  - During the explosion: one particle at Z+8 (fire) every 8th counter tick, 2 draws each.
  - Both pierce: a target at x = 156 is hit at t17 (flight, attr 6/7 → `fxattach` id 4 / 5 on the target, 3 sparks) and again at t42 (explosion box), 25 ticks apart.
  - Landing on deep-water cells (property 8 → slope 4): `DestroyEntity(e, 6)` the next tick (`fxattach id 6`), no explosion.
  - Charged versions (12, 15): same arc, power 12, explosion box 36x24x31, charged ice lasts 56 ticks.
- **Wave sword (27-29), Steel charged R=96, S=117:**
  - Stays at the hero's position (raw Z 2). Sound 30 at S. The box sweeps to x offset 42 by frame 5 and stays there. Hit target (x = 156) hit once at t122.
  - It cut a grass cell (flag 0x02 at (7,6)) at t127: effect id 2 (map header byte 9), sound 0x1F, 1 RNG draw (`0x80032C7C(0xFF)`), cell word cleared.
  - Hold end: reset flag at S+25, status 4 at S+26.
  - It pierces; it does not hit through "DestroyOnImpact" because it never moves (ForceAdjusted stays 0, no gravity bit).

## 6. Hit pass and switches

- FACT: arrows and fire/ice have HitsClassA only (target mask bit 0 = ClassA: enemies, levers). The hero is ClassB, so he is never hit. Wave swords also carry the ClassB bit but are skipped as targets (byte D 0x40).
- Each projectile sets `+0x1D0 = 0x19` on its target. Multiple projectiles share that 25-tick window.
- `0x87` / `0x95` type index → attack nibble {sword/wave 1, hammer 2, arrow 3, bomb 4, charge 5, fire 6, ice 7}. Census over `Codes` (matches the earlier totals 182 / 43 / 76):
  - `0x87` idx 2 (arrow): 6 sites, maps 16 (D544), 77, 136 (4 sites).
  - `0x87` idx 4 and 5 (charge): 3 sites, maps 77 and 136.
  - `0x87` idx 6 (fire): 56 sites, maps 13 (B132), 77, 92 (16), 110 (4), 137, 303 (6), 337 (8), 343 (6), 347, 443 (2), 444, 446, 448 (8).
  - `0x87` idx 7 (ice): 41 sites, maps 77, 137, 396 (3), 443 (13), 444 (7), 446, 447 (2), 448 (13).
  - `0x87` idx 0 (sword, which the wave also satisfies): 20 sites, maps 77, 136, 304, 352, 429, 430.
  - `0x95` idx 0: 6 sites, maps 41, 42, 226.
  - `0x95` idx 2: 1 site, map 226.
- None of these is on the day 1-4 chain, which has only the basic sword.

## 7. Who else uses what

- Header users (FACT, `<S>/census_maps2.py`): see item 1 above. E1 is on 178 distinct sprite headers across the corpus; E10 only on 27-29; E9 only on 10-15.
- Global spawn call sites with `a1 = 0` (table 0), from `<S>/out/typeargs.txt`:
  - hero weapon `0x8002F818`;
  - item drops `0x80032BEC`, `0x80032CD8`, `0x8007FD30` (type = item + 0x1E);
  - items `0x80034224..0x80036218`;
  - enemy `0x8007F6C8` (type 12), `0x80078E3C` (type 0x19), `0x80074D00` and `0x8007773C` (type 0xEC).
  - The two sites that compute their type, `0x80062280` and `0x80077250`, use table 1 (map-local, type − 0x100).
- So global types 5, 6, 8, 9, 10, 13 have no spawn site (INFERENCE: unused; UNKNOWN only if a computed index exists elsewhere, which my scan did not find).
- The bomb (global 2: C62 `0x8007A978`, D30 `0x8007E79C`, E12 `0x8007F27C`) is E20.h, not decoded here.

## 8. Hosting in the DLL

- **Registry (D-E14-1, gated by global sprite type).** Handler signature as the save book, `(AlundraEntityScriptProxy e, IEntityWorldContext ctx, AlundraGameState)`:

| Type | Entries |
|---|---|
| all of 3-15, 27-29 | (A, 0): `AIValues[0] = (short)(PosZ >> 16)`; harmless but counted |
| 3, 4, 5, 6 | (E, 17): arrow deactivate |
| 7 | (C, 94): tile cut plus trail; E1 already handled by the existing destroy branch (`AlundraEventProgramRunner.cs:315-321`) |
| 10-15 | (C, 60) trail; (E, 9) explosion |
| 27, 28, 29 | (C, 61): tile cut; (E, 10): destroy, NOT covered by the existing `<= 1` branch |

- **What exists and what needs adding.** All items are relative to `Alundra/Scripts`.
  - Hit pass, `CurrentAttack`, frame boxes (`AlundraFrameBoxTimeline`), balance by index (`AlundraBalanceTables.Select`, index = global type), `PickEventTrigger` including `DestroyOnSlidingSlope`, `Status`, and the hero seam `SpawnWeaponProjectile(player, recordPointer)` (`AlundraPlayerManager.cs:573`) all exist.
  - Missing:
    1. `SpawnWarpEntity`-equivalent with same-tick integration (`_tickEntities` must include entities spawned by `MovePlayer` in that tick). `CreateEntityFromPrefab` (`AlundraEntitySpawnFactory.cs:427`) is a base.
    2. A global-index → prefab id table.
    3. A Z/gravity/landing model for non-hero entities. XY already exists in `RunOneKinematicTick` (speed × OffsetXY, `IncrementForce`, controller move, `SlideAlongWall`), but gravity is the engine controller for NPCs.
    4. Constant-Z flight with wall, map-edge and terrain-height blocking (`ForceAdjusted` set when the body edge touches); the flag bit 13 (`NoObstacleSlide`) is set on all these headers.
    5. `DestroyEntity(e, fx)` with effect, BreakEffect exported, and effects 0x11, 0x12, 0x19 with velocities via `AlundraEffectStage`.
    6. A pure E17 scan helper over `_spawnedEntities` (reuse `AlundraHitPass.AxisOverlaps`).
    7. A shared tile-cut for entities.
    8. A `MulHigh32` helper for the `hi(a*k)` terms.
- **Order within a tick.** Handlers C and E run in the programs pass (before counters, animation, physics). A hit or impact written at tick N is read at N+1 (same as the c1 oracle). The animation pass sets the box and attack for tick N before the hit pass of tick N.
- **Tests.** Take expected values from `<S>/out/traces/*.txt` (copy `oracle/` and traces into `docs/plan-e20-annexe/e20-f/` at implementation time; I wrote nothing in the repo). Add a mutation per constant (E17 `+0x1CC`, `0x1AD`, slope set, 0x40 mask) as c1/c1b did.

## 9. Decompilation vs binary (decompilation read-only, not corrected)

- E17 scan: decompilation `DamageResponses[5] == 0`, binary `+0x1CC == 0` (offsets relative to `slot+0x68`, a transliteration loss). The decompilation's field names `TileAttributes`, `Slope_18c`, `Slope_190` in that function are the `+0x1F0/+0x1F4/+0x1F8` body sizes of the target, not the entity's own `+0x188/+0x18C/+0x190`.
- E9 `+0x18C`: here it is genuinely `Slope_18c` of the entity (decompilation correct).
- D9's "fire is C54" is wrong (item 1).
- D9's statement that the arrow "scans targets with `DamageResponses[5]` and `HitCounter`" is the same decompilation reading; the binary reads the damaged-tick counter.
- No other difference found for C60, C61, C94, E9, E10, E1.

## 10. UNKNOWN, and what would decide

1. Whether to keep the `SizeZ ≤ 16` arrow miss (item 3): author question; a recette with a Beannoïde would show it.
2. Meaning of slope value 1 in E9 and `DestroyOnSlidingSlope` (only 4 is documented as deep water): decide with a census of `UpdateGroundSlope` ground-property values.
3. The real in-game state of unused types 5, 6, 8, 9, 10, 13: decide by running the game, or accept INFERENCE "unused".
4. How `0x8003BE74` attached-effect ids 6 and 8 render (E20.e effect infrastructure).
5. Pool-full behaviour (63 slots) is only reachable with many simultaneous entities; not exercised.
6. Equipment-power term for projectiles (attr bit 0x80) is the c1b damage code; the bow/wand sheets' own contribution to `ComputeNewHp` was not measured here.

## 11. Files

- Scratch root: `<S>` above.
- Disassembly and census: `<S>/out/spawnwarp.txt`, `9d04.txt`, `e17.txt`, `e19_e20.txt`, `cattile.txt`, `destroy_entity.txt`, `global257.txt`, `callers_spawn.txt`, `typeargs.txt`, `balance_proj.txt`, `census_maps_summary.txt`; scripts `census_global.py`, `census_maps2.py`, `census_hitops.py`, `table_types.py`, `callers.py`, `typeargs.py`, `exe.py`.
- Oracle: `<S>/oracle/pworld.py`, `p1.py`..`p13.py`, `diff2.py`, plus the copied c1 tools.
- Traces: `<S>/out/traces/` (`hunter_east_to_map_edge`, `hunter_wall`, `hunter_vs_tall_target`, `fire_rod`, `ice_rod`, `fire_rod_charged`, `ice_rod_charged`, `william_charged_arrow` (+`_wall`, `_vs_target`), `steel_wave` (+`_vs_target`), `wave_cuts_grass`, `fire_ice_vs_target`, `fire_on_deep_water`, `arrow_vs_tile_heights`, `directions`, `rng_draws`). `traces_zero_index_pool/` is the superseded run with the harness artefact, kept to show S+1.
- Repo files read: `docs/plan-e20-heros.md`, `docs/plan-e14-annexe/e14a/dll-natives.md`, `docs/plan-e20-annexe/e20-b/`, `e20-c1/oracle/`, `e20-0-rapports/D8-D9-stick-et-proprietaires.md`, `Alundra/Scripts/{AlundraEventProgramRunner,AlundraEntityScriptProxy,AlundraWorldProxy,AlundraScriptedMotion,AlundraPlayerManager,AlundraHitOpcodes,AlundraBalanceTables}.cs`.
