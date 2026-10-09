> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# Report f-weapons: slice E20.f (hero weapons) — read-only discovery, 2026-10-09

Method: `ALUN_CD.EXE` (France) decoded with capstone, plus a copy of the E20.b attack oracle in my scratch folder. Nothing was written to the repository, and nothing was built or tested.

- **Binary addresses:** file offset = `0x800 + addr - 0x80020000`.
- **Labels:** FACT carries a file:line or binary address, INFERENCE carries its base, UNKNOWN carries what would decide it.
- **Oracle runs:** FACT from my own runs on the MIPS oracle, not from the existing annex.
- **Scratch folder:** `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/f-weapons/`
  - `mdis.py` disassembles.
  - `probe.py` runs the oracle probes.
  - `out/*.txt` holds the per-tick traces.
  - `tools/` is the copied oracle.
  - `globals.py` and `census2.py` are the data censuses.

## 0. Things that change the plan

1. **D-E20-18's premise is wrong on all three examples.** The handlers it sends to E14 are hero-only.
   - **FACT, handlers:** E17 (arrows), C60 (fire and ice balls), C61, C94, E9 and E10 appear only on the hero's projectile sprites.
     - Census of all 395 exported prefab records plus the 2507 map-local records in `data-extracted/data/map_*.json`.
     - The sprites are global types 3-6, 7-9, 0x0A-0x0F and 0x1B-0x1D (`globals.py`).
     - Enemy arrows use E3, which has 21 users.
   - **FACT, C54:** it is the boss "Ronan divin corrompu" (map 326, sprite 190, binary `0x80078E34`), not the hero's fire.
   - **FACT, sharing:** the only non-hero spawner of a hero projectile is the prop "Torche" (handler E20, `0x8007F6C8`, `jal 0x80039F58` at `0x8007F730`). It spawns type 0x0C, the charged fireball.
   - **Consequence:** E20.f can own C60, C61, C94, E1, E9, E10 and E17. Nothing from the weapons goes to E14.
2. **The sword wave does not fly.**
   - **FACT, handler:** C61 (`0x8007A958`) is only `CheckAndTriggerTileEffect(self)`, which cuts grass.
   - **FACT, data:** the wave (types 0x1B-0x1D) has Speed 0 and a one-shot 26-tick Hold animation. It has 13 frame boxes per direction and the attack row (type 1, attribute 0x81, power 10/20/38). It spawns at the hero's own X and Y.
   - **FACT, lifetime:** E10 (`0x8007F23C`) is `DestroyEntity(self, -1)`, identical to E1. It runs at the animation's Hold end.
   - **Consequence:** the wave is an attack volume. Its damage comes from the generic hit pass that E20.c1 already ported.
3. **The wave exists only on a charged release with the three swords.**
   - **FACT, records:** Steel Edge, Demon Buster and Holy Sword are the only weapons whose charge record spawns it.
   - **FACT, timing:** it spawns 21 ticks after the release tick, with delay 0x14 and z 0.
   - **FACT, not chargeable:** the Basic Sword and item 8 never spawn one.
4. **Bows and wands cost no ammo and no MP.**
   - **FACT:** `PlayerTryAttack`, `UpdateWeaponStepProgression` and `SpawnWarpEntity` contain no inventory or stat access.
   - **FACT, `jal` targets:** only `0x800490FC` (sound), `0x8003BDD8` (effect) and `0x80039F58` (spawn).
   - **FACT, MP:** MP exists (stats `+4`/`+6`, `GetMp` at `0x8004DEA4`, `SetMp` at `0x8004DF3C`). Only magic spends it, in `0x800348E0`; the Magic Staff (item 7) casts for free (`GetItemIdFromCurrentWeapon == 7` at `0x80034924`). That is E20.h.
5. **E20.f cannot be tested end to end before E20.e0, and two pieces belong to E20.e.** The author ordered f before e0 and e.
   - **FACT, spawn:** the projectile spawn is the `SpawnWeaponProjectile` stub (`AlundraPlayerManager.cs:573`), which needs E20.e0's spawn by global sprite index. Nothing named `SpawnWarpEntity` exists in `Alundra/Scripts`.
   - **FACT, grass:** C61 and C94 call `CheckAndTriggerTileEffect`, an E20.e function.
   - **FACT, flail:** the flail bounce sits inside `CreatePlayerAnimationEffects` mode 0 (`0x80031C3C`), also E20.e.
   - **Proposed way out:** see section 12.
6. **The charge visuals need the effect stage from the hero.**
   - **FACT:** `IAlundraScriptHost` exposes no effect stage. The reservoir has `CreateAttached`/`Free` (`AlundraEffectReservoir.cs:205,237`).
   - **FACT, generator:** the hold spark draws two LCG steps, and the DLL does not advance the generator per frame (E20.e0).
7. **The registry for native handlers must be shared with E14.a (D-E14-1, ADR-0055).** E20.f precedes E14.a in the author's order, so whoever lands first creates the "(slot, code) gated by sprite type" seam. The global types are keyed by their raw index `type` (SpawnWarpEntity stores `type`, or `type + 0x100` for map-local sprites).
8. **No test save holds any weapon, and `AlundraTestSaves.Preset` has no weapon-slot field** (`AlundraTestSaves.cs:36-46`).
   - Swords need only `ItemCounts`, because slot 1 resolves to the highest rank owned.
   - Bows, flails and rods need `PlayerStats.WeaponId` = their slot, which the preset cannot set today.
9. **Opcodes 0xC0 and 0xC1 are not on the story chain.** Strict census: 2 sites each, both in pub minigames (map 472 "Pub (Ring)" and map 473 "Pub (Shooting)"). Section 10 gives details.
10. **Decompilation loss in E17.** The binary skips a target when its `+0x1CC` (DamagedTickCounter) is non-zero. The decompilation reads that as `DamageResponses[5] == 0`. The binary wins.
11. **Facing lag risk at spawn.** The spawn direction reads `+0x98` (AnimationDirection) at the tick. The DLL writes that value only at end of frame (`SyncAnimation`, D-E19-13), so in multi-tick frames it can lag. INFERENCE.
12. **Item 8 ("Objet 008", slot 1 rank 4) is a dominant debug weapon if ever owned.** Its equipment fiche adds +50 to every hero attack that has the equipment bit. UNKNOWN whether any map gives it.

## 1. Weapon inventory (all FACT unless marked)

**Item map and slots.**
- Items map to weapon records through `0x80028C14`; `AlundraWeaponTables.cs:74-89`.
- Slot and rank come from `Data/items-properties.json` and the oracle, as `[slot, flags, rank]`.
- Records are 13 bytes at `0x80028C78`: `[chargeable][ground a,type,delay,z][air ...][charge ...]`.
- Swing ticks D and sounds come from `map_alundra.json` AnimSets, checked equal to the binary sum in `e20-b/faits-binaire-dll.md` §3.
- "Hero attack" is the hero sheet row at index `anim + 1` (`balance.json` entry 0): `(type, equip bit, attribute, power)`.
- "Fiche" is the equipment fiche, entry `item + 0x1E`. It is added to the power when the attribute has bit 0x80.

| Item | Name | Slot/rank | Record w | Ground anim (D, sound) | Air anim (D) | Charge record | Hero attack (ground / air / charge) | Fiche power |
|---|---|---|---|---|---|---|---|---|
| 1 | Basic sword | 1 / 0 | 0 | 0x10 (21, 29) | 0x14 (10) | none (not chargeable) | (1,1,0x81) P1 / P1 | 1 |
| 2 | Steel Edge | 1 / 1 | 1 | 0x3F (21, 29) | 0x41 (10) | 0x18, type 0x1B, delay 0x14, z 0 | P2 / P2 / 0x18: P8 | 2 |
| 3 | Demon Buster | 1 / 2 | 2 | 0x44 | 0x46 | 0x18, type 0x1C, delay 0x14 | P4 / P4 / P8 | 4 |
| 4 | Holy Sword Elnard | 1 / 3 | 3 | 0x49 | 0x4B | 0x18, type 0x1D, delay 0x14 | P8 / P8 / P8 | 8 |
| 8 | "Objet 008" | 1 / 4 | 11 | 0x10 | 0x14 | none | as item 1 | one attack (6, no equip bit, attribute 6, P50) |
| 5 | Hunter's Bow | 2 / 0 | 8 | 0x13 (23, 32): type 3, delay 8, z 0x10 | 0x17 (15; 14 on right): type 3, delay 0, z 0x10 | none | type 0 (no hero attack) | count 0 |
| 6 | William's Bow | 2 / 1 | 9 | 0x13: type 4, delay 8, z 0x10 | 0x17: type 4, delay 8, z 0x10 | 0x13, type 7, delay 8, z 8 | type 0 | count 0 |
| 9 | Chain Throw (iron flail) | 3 / 0 | 6 | 0x12 (31, 35) | 0x16 (28; 27 on right) | none | (2,1,0x82) P3 / P3 | count 0 |
| 10 | Crusher (steel flail) | 3 / 1 | 7 | 0x40 (34, 35) | 0x42 (31) | 0x19, no spawn (58 ticks, sound 36) | P6 / P6 / 0x19: P12 | count 0 |
| 16 | Fireball Rod | 5 / 1 | 4 | 0x11 (31, 37): type 0x0B, delay 10, z 0x20 | 0x15 (25; 24 on right): same | 0x11, type 0x0C, delay 10, z 0x20 | type 0 | count 0 |
| 14 | Drifting Ice Rod | 4 / 1 | 5 | 0x11: type 0x0E, delay 10, z 0x20 | 0x15: same | 0x11, type 0x0F, delay 10, z 0x20 | type 0 | count 0 |
| 7 | Magic Staff | 6 / 1 | 10 | animation 0 | animation 0 | none | none | count 0 |

**Notes on the table.**
- **Unused items.**
  - Items 11 and 12 (slot 3, ranks 2 and 3), 13 (slot 4, rank 0) and 15 (slot 5, rank 0) exist in `items-properties.json`.
  - They map to record 0 (the basic sword's animations) and have no fiche power.
  - UNKNOWN whether any map gives them.
- **Staff.** Animation 0 plays sound 3 and returns 0 (`0x8002ED30`). The press does nothing else.
- **Damage terms.**
  - Rods and bows have an equipment fiche with count 0, so the equipment term is 0 (K2 in `e20-c1b`).
  - Swing damage is therefore `(hero P + fiche P) * r >> 4`. Examples: Basic 1+1, Steel 2+2, Holy 8+8.
  - Charged sword: 0x18 gives P8 + fiche.
  - Wave: 10/20/38 + fiche, attribute 0x81.
- **Sound.** The swing sound is the animation's Sfx, played at the animation switch (E19.t).
- **Cost.** Section 0 point 4 covers it.

## 2. PlayerTryAttack (`0x8002EAF4`) and the weapon passes

**FACT, `PlayerTryAttack`:**
- Returns 0 if `+0x180 & 0x2000`.
- Tests the release edge first (`0x80126F2C & 0x80`, `0x8002EB38`).
  - Timer < 60 returns 0.
  - Not on the ground returns 0 (`+0x144`).
  - Otherwise it runs the charged attack.
- Only without a release does it test the press (`0x80126F2A & 0x80`, `0x8002EC8C`).
- Charged attack (`0x8002EB6C-0x8002EC84`):
  1. 16 sparks.
  2. Sound `0x2B` (43).
  3. Pointer = `0x80028C79 + 13w + 8`.
  4. Counter 0.
  5. `+0x88` = the charge animation.
  6. Returns 1.
- Press attack: pointer = `0x80028C79 + 13w`, plus 4 in the air. Animation 0 gives sound 3 and returns 0.
- Other callers: only `0x29` PrepareSprint (E20.d).

**FACT, `UpdatePlayerWeaponEffect` (`0x8002F49C`, `MovePlayer` `0x80031D68`):**
- The timer is at `0x80126FFC`.
- It increments, capped at 60, when `(Hold | Released) & 0x80`, record byte 0 is non-zero, and flag b0 (`0x80028D14 + 2*(+0x88)`) is non-zero.
- Otherwise it resets to 0.
- b0 is set for animations 0, 1, 2, 3, 4, 0x0E, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x35 and 0x3E. Charging therefore continues in the air and while climbing, but never during an attack animation.
- Timer < 11 (`0x8002F730`): the aura handle's `+0x68` is zeroed and the handle (`0x80126FF8`) cleared.
- Timer >= 11:
  - Create effect 3 attached to the hero if the handle is null or the handle's `+0x7D == 0`. Call `0x8003BE74(0, 3, 0, hero, 1, 0, 0, z 0x100000)`.
  - Then, only when `FrameCounter (+0x1C) & 3 == 0`: timer < 60 plays sound `0x2A`.
  - Timer == 60 creates the hold spark (effect 0x10) and plays `0x1AD` if `+0x1C & 7 == 0`, else `0x1AC`.
- Hold spark (`0x8002F5EC-0x8002F71C`):
  - Position: hero X, Y, Z + 0x100000, jittered.
  - Jitter X: `(u1 * 0x60001 >> 32) - 0x30000`, times 20.
  - Jitter Y: `(u2 * 0x40001 >> 32) - 0x20000`, times 20.
  - Force: hero ForceX/Y minus the unscaled jitter.
  - It uses two LCG steps (INFERENCE from the code).
  - Sound plays before the draws, and only if the effect was created.

**FACT, release burst:** 16 effects 0x10, for `i = 0..15`.
- Z + 0x100000.
- X + `cos[2i] * 3 << 10`, Y + `sin[2i] * 3 << 10` (tables `0x80023654` / `0x80023694`; the cos amplitude is 768 and the sin amplitude 512).
- Forces: `cos[(2i+0x18)&0x1F] * 448` and the same for sin. No RNG.

**FACT, `UpdateWeaponStepProgression` (`0x8002F768`):** it runs before the state switch and uses `+0x88` for flag b1.
- Flag b1 (`0x80028D14 + 2a + 1`) is set only for animations 0x11, 0x13, 0x15, 0x17 and 0x18.
- Pointer null: nothing.
- `ptr[1]` (type) is 0, or b1 is clear: clear the pointer. This is how a swing cut short drops its shot.
- Counter < `ptr[2]`: increment.
- Otherwise: `SpawnWarpEntity(hero, table 0, ptr[1], PosX, PosY, PosZ + (ptr[3] << 16), dir)` and clear the pointer.
- `dir` is `{0, 0x10, 8, 0x18}[+0x98]` (table `0x80023644`).
- The result is ignored; there is no refund.
- Spawn tick = press tick + delay + 1.

**FACT, zeroing sites.**
- Timer: `0x80031A18` (init), `0x80031BB4` (`+0x20 != 0`), `0x80031BEC` (blocked `0x34`), `0x80031C5C` (hurt), `0x80032614`.
- Pointer: `0x80031A40`, `0x80031BC0`, `0x80031BF8`, `0x80031C68`, `0x80032620`, `0x80032774`, `0x800327B0`.
- The blocked paths `0x80031BB4` and `0x80031BEC` do not call `UpdatePlayerWeaponEffect`, so the aura stays until the next normal tick.

**FACT, order inside `MovePlayer`** (`0x80031B50-0x80031D78`):
1. Weapon lookup.
2. `0x8002F120` (CheckAndExecuteWarp).
3. `+0x20` exit.
4. `CreatePlayerAnimationEffects(0)` (`0x80031C3C`).
5. Hurt entry (`0x80031A68`).
6. HP == 0 branch.
7. `0x8003634C`.
8. Pad.
9. Ladder grab.
10. `UpdatePlayerWeaponEffect`.
11. `UpdateWeaponStepProgression`.
12. `UpdatePlayerCarriedEntity(0)`.
13. State switch.

## 3. Charge: measured timeline (oracle, Steel Edge, Square held from tick 1)

Probe `C2-steel`, FACT:
- t1: press, animation 0x3F, sound 29, timer 1 (the tap bump).
- t2: timer 0 (swing, b0 clear).
- t22: Idle.
- t23: timer 1.
- t33: timer 11, aura call (`effect 3`, stack `1,0,0,0x100000`) and sound 42. Sound 42 then repeats every 4 ticks.
- t82: timer 60.
- t85: spark call (`effect 0x10`) and sound 428 (`0x1AC`).
- t89: spark and sound 429 (`0x1AD`).
- t91: release tick. `Hold = 0`, animation 0x18 at t91, pointer `0x80028C8E`, 16 spark calls, sounds 43 and 0.
- t92: timer 0, counter 1.
- t112: `spawn(table 0, type 27, x = PosX, y = PosY, z = 1, dir 0x18)`.
- t137: Idle (0x18 lasts 46 ticks).

**Rules derived from the runs (FACT):**
- First counting tick = S + D + 1 (press tick S, swing length D).
- The aura appears 10 ticks later.
- The timer reaches 60 at S + D + 60.
- Earliest successful release = S + D + 60, because the release tick increments before `PlayerTryAttack` sees it.
- Concrete cases:
  - Fireball Rod: release at t92 or later (D = 31); a release at t91 left the timer at 59 and failed.
  - Crusher: first count at 36, earliest release 95.
  - William's Bow: first count at 25, earliest release 84.
- A release in the air with timer 60 does nothing, and the timer is 0 the next tick (X2, D-E20-9).
- Getting hurt zeroes the timer and the pointer.

**Charged runs (my probes):**

| Weapon | Release tick | Animation | Spawn |
|---|---|---|---|
| Fireball Rod | t101 | 0x11 | t112: type 12, z 0x200001 |
| Ice Rod | t101 | 0x11 | t112: type 15 |
| William's Bow | t91 | 0x13 | t100: type 7, z 0x80001 |
| Crusher | t101 | 0x19, pointer `0x80028CDC`, cleared next tick, 58 ticks | none |

**Normal-shot spawn ticks** (AT9, `e20-b/oracle/out/weapons.txt`, press at t1 on the ground or t5 in the air):

| Shot | Spawn tick | Swing end |
|---|---|---|
| Hunter ground | 10 | t24 |
| Hunter air | 6 | t19 (0x17 to 0x2D) |
| William air | 14 | t19 |
| Fire / Ice ground | 12 | t32 |
| Fire / Ice air | 16 | cut at the landing, t22 |

## 4. Flail (Chain Throw 0x12/0x16, Crusher 0x40/0x42)

**FACT, swing:** animations, durations and the brake are in section 1; the swing brakes the walk exactly like the sword (E20.b).

**FACT, bounce** (`CreatePlayerAnimationEffects` mode 0 only; `0x803D8` gate, flail branch `0x80030434-0x800306xx`):
- It reads `+0x88`, the state chosen the previous tick. It acts only for 0x12, 0x16 (index 0, iron) and 0x40, 0x42 (index 1, steel).
- Two triggers:
  - `HitCounter (+0x220) != 0`.
  - Or, with a frame box present, a wall under a box corner.
- Wall test: the four corners (HitBox X and X + size, HitBox Y and Y + size, Z = HitBox Z). The tile column comes from `0x80127168`, the row is `y >> 20`, and the cell is at `0x800DD86C + 0x604 + ...`.
  - The wall triggers if `HitBoxZ < height << 20`, or if cell walkability has bit `0x40`.
- Result: `+0x88 = byte[0x800228A4 + 2*index + (air ? 1 : 0)]`, with bytes `08 1F 25 26`.
  - Iron ground 0x08, iron air 0x1F.
  - Steel ground 0x25, steel air 0x26.
- Order in a tick:
  - The hit pass writes `HitCounter` at the end of tick N.
  - `MovePlayer` at N+1 reads it.
  - The counters pass then clears it.

**FACT, bounce states** (hero `AnimSets`):
- Speed 0 and Acc 0 for all four.
- Sound 28.
- 0x08 and 0x25: 16 ticks, then chain to 0 (Idle).
- 0x1F and 0x26: 11 ticks, then chain to 0x2D.
- All four are in the E20.b attack groups (`IsGroundAttackGroup` / `IsAirAttackGroup`), so they already end correctly.

**Oracle probes** (FACT; `HitCounter` poked at tick 6 or 8):
- `F-iron-ground`: 0x08 at t6, sound 28, Idle at t22.
- `F-steel-ground`: 0x25 at t6.
- `F-iron-air`: 0x1F at t8, 0x2D at t19, landing at t22 as before. The jump arc is unchanged.
- `F-steel-air`: 0x26 at t8.
- `F-iron-walk`: the bounce adds no force. Force is 79872 at the press tick, then 0, and 0 through the bounce.
- Wall probes, with cells at column >= 5 east of the hero:
  - Height byte 1, height byte 2 and walkability `0x40` all bounced at t10 (iron).
  - The steel flail with a height-2 wall bounced the same way, to 0x25.
  - A flat field gave no bounce.
  - The swing's frame box exists only from t9 (hero X 100, box X 107..129, 22 px wide); the bounce comes at t10.

## 5. Wands (Fireball Rod, Ice Rod)

- **FACT, animation and spawn:** swing 0x11 (31 ticks, sound 37) or air 0x15. The hero sheet row for these animations has attack type 0, so the swing itself hurts nothing.
- **FACT, projectile:** the shot spawns at press + 11 ticks as type 0x0B (fire) or 0x0E (ice). The charged release spawns type 0x0C or 0x0F at release + 11 ticks.
- **FACT, entity data:**

| | Fire 0x0B / 0x0C | Ice 0x0E / 0x0F |
|---|---|---|
| Handlers A/C/D/E/F | 0/60/0/9/0 | 0/60/0/9/0 |
| `MoreFlags` | 0x54 | 0x54 |
| `CanPickup` | 0x21 | 0x21 |
| Attack | type 6, attribute 0x86, 5 attack rows, P4 / P12 | type 7, attribute 0x87, P4 / P12 |
| Anim 0 | 12 ticks, loop, Speed 512, `IsZForceApplied` 512 | same |
| Anim 1 (burst) | Hold, 36 ticks, 12 box frames; sound 39 / 41 | Hold, 32 ticks / 56 ticks, 8 / 14 box frames; sound 38 / 40 |

- **FACT, flags:** `MoreFlags` 0x54 means HitsClassA | DeactivateOnImpact | DeactivateOnAnimationEnd.
- **FACT, hit pass effects:** on a hit the pass attaches effect 4 (attribute 6 or 10) or effect 5 (attribute 7 or 9) to the target (`0x800395F4-0x80039660`, `0x8003BE74(0, id, 0, target, 1, 0, 0, 0)`). Not ported (E20.c1a left this to f).
- **FACT, C60** (`0x8007A8A0`):
  - Every tick with `+0x1C & 3 == 0`.
  - Effect 0x11 for types 0x0B and 0x0C, else 0x12.
  - Placed at (X, Y, Z + 0x80000 for fire, + 0 for ice), animation 0.
  - Forces are the negation of the entity's ForceX, ForceY and ForceZ.
- **FACT, E9** (`0x8007EF50`):
  - Not in the burst (`+0x88 != 1`), on slope property 1 or 4: `DestroyEntity(self, 6)` and stop.
  - Otherwise at impact: five puff effects 0x11/0x12 with RNG forces, then `+0x88 = 1`, `Flags |= 0x40`, `Flags &= ~0x30`.
  - In the burst:
    - `DestroyEntity(self, 6)` on slope 1 or 4.
    - `DestroyEntity(self, -1)` when `+0xAC == 1`.
    - A trail puff every 8th tick (`+0x1C & 7`).
  - RNG draw count per puff is INFERENCE (multiply pairs).

## 6. Bows

- **FACT, spawn:** the arrow spawns at the hero's centre, not in front: X = PosX, Y = PosY, Z = PosZ + 16 px (z byte 0x10) for the arrows, + 8 px for the charged arrow. It flies by its animation Speed.
- **FACT, sprites:**

| Type | Handlers A/C/D/E/F | Attack | Anim 0 | Anim 1 |
|---|---|---|---|---|
| 3 Flèches Niv.1 | 0/0/0/17/0 | type 3, attribute 0x83, P1 | loop, 12 ticks, Speed 512 | Hold, 16 ticks, Speed 160, `IsZForceApplied` 1024, sound 34 |
| 4 Flèches Niv.2 | same | P2 | same | same |
| 7 Flèches chargées | 0/94/0/1/0 | P5 | loop, 16 ticks, Speed 512, sound 33 | Hold, 16 ticks, Speed 0 |

- **FACT, flags:**
  - Arrows 3 and 4: `MoreFlags` 0x74 and `CanPickup` 0x20, which is HitsClassA | DeactivateOnImpact | DeactivateOnHit | DeactivateOnAnimationEnd, with NoObstacleSlide.
  - The charged arrow has 0x54, so it has no DeactivateOnHit and can pierce.
- **FACT, E17** (`0x8007F420`, binary):
  - If `+0x88 == 1` (turning back): destroy when `+0xAC == 1`.
  - Otherwise, if `HitCounter != 0`:
    - Build a class mask from Flags.
    - Scan the entity slots (0 to `[0x80134360]`), skipping the arrow itself.
    - Skip targets that have `AnimFlags & 0x40` or `+0x1CC != 0`.
    - Match on the mask and an inclusive box test of the arrow's HitBox against the target's ModdedPos and sizes.
    - Reject target type `0x1AD`.
    - A valid blocker destroys the arrow.
    - Mask 0 destroys the arrow.
    - If nothing valid is found, Status goes back to 2 and the arrow flies on.
  - A wall impact with `HitCounter == 0` turns the arrow: `+0x88 = 1`, `TargetDirection = (dir + 0x10) & 0x1F`, `Flags |= 0x140`, `Flags &= ~0x30`.
- **FACT, E1** (`0x8007ED10`) is `DestroyEntity(self, -1)`, already ported (`AlundraEventProgramRunner.cs:312-321`). E1 is the charged arrow's E, so it just disappears on impact.
- **FACT, C94** (`0x8007C0D8`): `CheckAndTriggerTileEffect(self)`; with `+0x1C & 3 == 0`, effect 0x19 at (X, Y, Z + 0x80000), animation argument `+0x98`, forces `-ForceX/2`, `-ForceY/2`.
- **FACT, tile cutter:** `CheckAndTriggerTileEffect` (`0x8003A7B0`) takes the entity in `a0` and needs `+0x1D4` (a frame box). It works for any entity with a box.
- **FACT, damage** for the projectiles: attack rows are in the sheets at index = type (balance entries 3, 4, 7, 0x0B, 0x0C, 0x0E, 0x0F, 0x1B-0x1D). Their equipment bit (attribute 0x80) applies, but the bow and rod fiches have count 0, so no equipment power is added.

## 7. Sword wave

- **FACT, which sword and when:** see section 0 point 3.
- **FACT, sheets:**

| Type | Sheet | Attack |
|---|---|---|
| 0x1B (Steel) | entry 27 | (1, equip, 0x81, 10) |
| 0x1C (Demon) | entry 28 | (1, equip, 0x81, 20) |
| 0x1D (Holy) | entry 29 | (1, equip, 0x81, 38) |

- **FACT, flags:** `MoreFlags` 0x5C (HitsClassA | ClassB | DeactivateOnImpact | DeactivateOnAnimationEnd), `CanPickup` 0x20.
- **FACT, anim 0:** Hold, 26 ticks, 13 box frames, Speed 0, `Acceleration` 0xC1 (so the animation has the invulnerable bit 0x40), sound 30. Anim 1 (4 frames, 16 ticks) has no known user.

## 8. Remaining weapon states

- **0x08 / 0x09 / 0x0A / 0x25:** 0x09 and 0x0A are the throw animations of the carried object (E20.g). They sit in the attack groups only because of the jump table.
- **0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D:** exported but never selected by the weapon table. They fall in the default group.

## 9. Spawn, entity and sprite facts E20.e0 and E20.f share

- **FACT, spawn call:** `SpawnWarpEntity(owner, table, type, x, y, z, dir)` at `0x80039F58` runs:
  1. `0x80039B28` (record lookup).
  2. `0x80039AD0` (free slot).
  3. `0x80039D04` (init).
- **FACT, table:** table 0 is the global sprite table at `0x80126EC0` (type used as is). A non-zero table uses `0x80126E70` with `type + 0x100`.
- **FACT, global data:** the global sprite records are `map_alundra.json` `SpriteInfo.SpriteRecords` (257 entries). In the export they are the `bankalundra_<type>` prefabs, e.g. `Entities/Flèches Niv.1` is type 3.
- **FACT, entity init:**
  - Parent (`+0xC`) = the owner.
  - `+0x88` = 0, `+0x8C` = `dir`, `+0x90/+0x94` = bitwise NOT of those, which forces the animation restart.
  - Z = z minus box offset Z plus 1.
  - The balance sheet is chosen by type, so a global type's index is the type.
  - `Flags` = `MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16`.
- **FACT, ADR-0055:** per-type registration (D-E14-1).
- **FACT, all hero projectile types:** types 2 (bomb, E12, E20.h), 3, 4, 7, 0x0B, 0x0C, 0x0E, 0x0F, 0x1B-0x1D. Item-use spawns (types 1, 2, 0x10-0x18) belong to E20.h.

## 10. Opcodes 0xC0 and 0xC1

- **FACT, 0xC0 "Set equipped weapon"** (`0x80041CDC`, size 2):
  - `SetPlayerWeaponId(operand + 1)` (`0x8004E484`).
  - `[0x800DC4B8] |= 0x80`; returns 2.
- **FACT, 0xC1** (`0x80041D18`, size 1): `[0x800DC4B8] &= ~0x80`; returns 1. Dispatch table entry at `0x800992AC` for 0xC0.
- **FACT, what the bit does:**
  - It is a whole-value `!= 0` gate on `[0x800DC4B8]`.
  - `TryUseItem` (`0x8002ED80`) plays sound 3 and returns 1, so the Circle does nothing.
  - The inventory-open gate (`0x8002BB68`) and the warp gate (`0x8002F1C4`) also read the whole value.
  - It does not block `PlayerTryAttack` or `MovePlayer`'s `0x34` branch.
  - The DLL already has the constant (`AlundraGameState.cs:86`) but not the opcodes.
- **FACT, corpus:** strict census (8140 programs walked, 363 unparsable skipped, so possible misses; INFERENCE):
  - 0xC0 at map 472 B[5] `@327` (operand 0, slot 1) and map 473 B[1] `@198` (operand 1, slot 2).
  - 0xC1 at map 472 B[5] `@378` and map 473 B[1] `@289`.
  - Each follows "Try spend money [30]" (`0x99`).
- **UNKNOWN:** what the shooting gallery does if the player owns no bow. `GetItemIdFromSlotId(2)` would give -1, so record 0 swings the sword. The pub's reachability from the main story is also not checked.

## 11. What the DLL already has (E20.b) and the gaps

**Ported (FACT, `Alundra/Scripts/AlundraPlayerManager.cs`):**
- Weapon record lookup at the head of `MovePlayer`, `:176-179`.
- `PlayerTryAttack`, `:492-522` (release stub at `:499-503`). It is called from the shared tail `ApplyGroundOrAirTail` at `:661`.
- Attack groups, `:437-456`, with `IsGroundAttackGroup` `:525` and `IsAirAttackGroup` `:529` (ids exactly the binary table, including 0x08, 0x09, 0x0A, 0x1F, 0x25, 0x26).
- `UpdateWeaponStepProgression`, `:547-569`, including the delay and the b1 check.
- `BlockedByEntity` branch clears the pointer, `:210-212`.
- Prologue calls, `:275-277`.
- `AlundraWeaponTables.cs` (all three tables; `AnimationCountsCharge` `:121`; `AnimationSpawnsAfterDelay` `:128`).
- Hero fields `CurrentWeaponRecord`, `WeaponRecordPointer`, `WeaponStepCounter`, `AlundraEntityScriptProxy.cs:391-399`.
- `HitCounter` is written by the hit pass; `HitBox*` and `Collision*` exist (`AlundraEntityScriptProxy.cs:417-426`; `AlundraEntityScriptProxy.HitPass.cs`).
- `FrameCounter` (`+0x1C`) is at `AlundraEntityScriptProxy.cs:72`.

**Stubs and gaps:**
- `UpdatePlayerWeaponEffect` is an empty stub, `:533-535`.
- `SpawnWeaponProjectile` only increments a test counter, `:573-576` (`WeaponSpawnCallCountForTests` at `:539`).
- There is no charge timer, no aura handle, no charged release, no flail bounce, no effects 3/0x10/4/5, and no native handlers (C60, C61, C94, E9, E10, E17).
- `UpdatePlayerWeaponEffect` takes only `player`; the charge needs the pad (Hold and Released), so its signature must grow.
- `IAlundraScriptHost` has no effect-stage accessor.

**Where the weapon is equipped in the DLL (E13):**
- `AlundraPlayerManager.SetPlayerWeaponId` `:1157` (accepts -1 or slots 1-6), `GetItemIdFromCurrentWeapon` `:1173`, `GetItemIdFromSlotId` `:1214` (highest rank wins), `InitializeNewGameInventory` `:1131`.
- The inventory screen is `AlundraInventoryDirector.RunEquipWeapon`, `:818-896`.
  - Grid column 0 gives slot 1 (swords).
  - Column 1 gives slot 3 (flails).
  - Column 2 gives slot 2 (bows).
  - Column 3 gives slot 4 (Ice Rod).
  - Column 4 gives slot 5 (Fireball Rod).
  - Column 5 gives item 7 with `WeaponId` 6 (Magic Staff).
- There is no in-game weapon hotkey in the binary or the DLL. Callers of `SetPlayerWeaponId` are only: new game (`0x80031894`), opcode 0xC0 (`0x80041CF0`), and the inventory (`0x800579E8`, `0x80057B04`).

## 12. How a test gets a weapon, and a proposed cut

**How a test gets a weapon (FACT):**
- A rig test: `AlundraHeroAttackTests.cs:54-69`. It sets `host.GameState.NumberOfItems[id*2+1] = 1`, then `SetPlayerWeaponId(state, tables, tables.ItemsProperties[id*5])`.
- AT9: `:349-358`; the Steel Edge pick-up `:361`; the real-prefab test `:415`; the world-tables test `:473`.
- Oracle traces: `e20-b/oracle/out/at9/AT9{g,a}-itemNN.trace.json`.
- An arc (`ArcRun`) has no items field in `ArcSpec` (`AlundraArcSupport.cs:52`). A test mutates `ArcRun.State` (the `AlundraGameState` singleton) before the first frame; the world supplies `ItemTables` (E20.b).
- Test saves: `AlundraTestSaves.Preset.ItemCounts` (`AlundraTestSaves.cs:46`, applied `:131-134`; item n's counter is index `n*2+1`). The builder starts from the new-game inventory (WeaponId 1).
  - Sword items work with `ItemCounts` alone.
  - Bows, flails, rods and the staff need `WeaponId` = slot, which the preset cannot set (INFERENCE: one new field fixes it).
  - A weapons recipe also needs a map with grass, a wall to bounce a flail on, and targets; map 14 has Beannoïde, an arrow-immune check and open ground. I did not test any of this.

**Proposed cut** (INFERENCE; the order of work, not new decisions):
- **F-a, charge and release.**
  - Timer, release path, zeroing sites, signature change, the tap bump.
  - No dependency except the pad.
  - Oracle traces `C*-` (mine) and X1/X2 give the exact ticks.
- **F-b, flail bounce.** A standalone port of the mode-0 flail block at its place, before the hurt entry. It needs `HitCounter` (done), HitBox (done) and a cell reader (`AlundraCellStore`). No E20.e0.
- **F-c, spawn.** Needs E20.e0 (spawn by global sprite index, the prefab loader for `bankalundra_N`, the balance sheet by type, loot untouched).
- **F-d, the handlers.** The registry seam (shared with E14.a), then C60, C94 (trail effects), C61 and C94 tile cutting (a named stub until E20.e), E9, E17, E10 (reuse E1).
- **F-e, effects.** Aura, sparks, hit effects 4 and 5, and a host effect-stage accessor.
- **F-f, opcodes 0xC0 and 0xC1.** Small; the gate on `TryUseItem` is E20.h's, but the bit can be set now.
- **F-g, preset and recipe.** Extend `Preset` with a weapon slot; one preset with all weapons.

## 13. UNKNOWN and what would decide it

- Where each weapon is given in the story. Opcode `0x82` calls `HandleMapTriggerCommand` (`0x80034108`) with an id; decoding it and the chest handler (global sprite 0x1E, A=255, F=255) would show the item. My census of `0x82` operands is not reliable for item identity.
- Whether the Torche prop's charged fireball (E20) belongs to E20.f or a later prop slice. Deciding needs the author.
- RNG draw counts per puff in E9, and the hold spark's visual effect on screen. A trace with the effects live would decide.
- Whether the bounce states need any force change when the hero walks into a wall. In the oracle the bounce adds none; real-game confirmation is a recipe check.
- Whether item 8 or the unused items 11-13 and 15 are ever given.
