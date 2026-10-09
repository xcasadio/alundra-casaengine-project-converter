# Survey 2: the hero attack

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# Hero ATTACK: findings (read-only; nothing written)

**Method.** I decoded `ALUN_CD.EXE` with capstone. I also ran the real instructions of `PlayerTryAttack`, `UpdatePlayerWeaponEffect` and `UpdateWeaponStepProgression` in `docs/plan-e19-h2-annexe/tools/emu.py` (`python -B`, stubs only for effect creation, sound and spawn). Data facts come from `alundra-project/Data/{sprite-records,balance}.json` and `Entities/Alundra/*.anim2d`, which are converter output of the game data.

Tags: **F** = FACT, **I** = INFERENCE, **U** = UNKNOWN. Decompilation paths are under `alundra-datas-analyser/AlundraTools/AlundraEngine/`, abbreviated `PM` = `Gameplay/PlayerManager.cs`, `EM` = `Gameplay/EntityManager.cs`. The player entity base is `0x80127D30`, and entity stride is `0x294`.

## 1. Button, edge or hold, and where an attack can start

- **F** The attack button is Square, pad bit `0x80` (`Gameplay/PadState.cs:12`). The DLL already maps "Attack" to Square (`Alundra/Scripts/AlundraPlayerController.cs:93`).
- **F** A swing starts on the press edge: `ButtonsJustPressed` (`0x80126F2A`, instruction `94620012`, `andi 0x80` at `0x8002EC94`).
- **F** Charge uses `ButtonsHold` (`0x80126F28`) and the release edge `ButtonsReleased` (`0x80126F2C`).
- **F** The release branch is tested first. In emulation, press and release in the same frame with timer 60 takes the release path.
- **F** `PlayerTryAttack` (`0x8002EAF4`) has only two call sites in the whole executable (`jal` scan):
  - `0x80031EA8` is the shared tail of Idle/Moving and the jump states.
  - `0x80031F74` is PrepareSprint.
- **F** MovePlayer dispatches on the state through the table at `0x80022D3C` (`TargetAnimationId < 0x50`, `sltiu` at `0x80031D8C`). Only these states reach an attack:
  - 00 Idle and 01 Moving, entering at `0x80031E38`.
  - 02, 2B, 2C, 2D (the four jump states), entering at `0x80031E84`.
  - 29 PrepareSprint, entering at `0x80031F54`.
- **F** Not attack-capable: Sprint (03), StopSprint (2A), carrying, climbing, swimming, sand, mine cart.
- **F** Per-tick order, with `0x…` giving the instruction address in the binary:
  1. Idle/Moving: `TargetDirection = dir` (`0x80031E44`), then `TryUseItem` (`0x80031E40`), `PlayerTryAction` (`0x80031E50`), `CheckEntityInteraction` (`0x80031E60`), then `PlayerTryAttack`.
  2. Jump states: the same but without `CheckEntityInteraction`.
  3. A nonzero return from `PlayerTryAttack` goes to END, so the Cross jump test is never reached. Square and Cross on the same tick give an attack, not a jump.
- **F** Consequences of that order:
  - A used Circle item (`TryUseItem` returns 0) suppresses the attack.
  - `CheckEntityInteraction` returns 2 on a Square press at a button-interact entity. The hero goes Idle and there is no attack (`0x80031E74`–`0x80031E80`).
  - `CheckEntityInteraction` returns 1 for an auto-touch entity. The state ends with no attack that tick.
  - `PlayerTryAction` returns 1 when the hero carries something, which blocks the attack. It also starts a pickup when Square meets a liftable `XCollisionEntity`. A non-liftable entity returns 0 and the attack proceeds.
- **F** `PlayerTryAttack` returns 0 at once if `CombinedVramFlagsOR & 0x2000` (`0x8002EB1C`–`0x8002EB28`). There is no check of MP, inventory or ammo anywhere in `0x8002EAF4`–`0x8002ED60`.
- **I** `CombinedVramFlagsOR` is the OR of the cells' `Walkability | GroundProperty<<8` (`GameEngine.cs:2499`), so `0x2000` is ground-property bit `0x20`. **U** Which cells carry it.
- **F** The same bit blocks throwing (`PM:595`).

## 2. Weapon selection

- **F** The game-state field is `g_playerStats.WeaponId`. It holds a slot 1..6 or -1, and the pointer to the stats is at `0x80176318`, with `WeaponId` at +0xA. `GetItemIdFromCurrentWeapon` is `0x8004E030`.
  - The slot-to-lookup jump table at `0x800265D4` is sequential: slots 1..6 go to `GetItemIdFromSlotId(1..6)`.
  - The DLL already ports this: `AlundraPlayerManager.GetItemIdFromCurrentWeapon`, line 936.
- **F** MovePlayer recomputes the weapon record index every tick: `g_currentWeaponFlags (0x8012710C) = byte[0x80028C14 + itemId]` (`0x80031B70`–`0x80031B8C`).
- **F** With no weapon, `itemId = -1`. The binary then reads `0x80028C13`, which is `0x00`, so record 0 is used. The decompilation indexes with -1 (`PM:24-28`), which is an array error in C#.
- **F** Slot to items, from the table at `0x800B9FE8` (properties) and `EntityNames.csv`:

| Slot | Items |
|---|---|
| 1 sword | 1 Basic sword, 2 Steel Edge, 3 Demon Buster, 4 Holy Sword Elnard, 8 "Item 008" |
| 2 bow | 5 Hunter's Bow, 6 William's Bow |
| 3 flail | 9 Chain Throw, 10 Crusher, 11 and 12 unused |
| 4 | 13 unused, 14 Drifting Ice Rod |
| 5 | 15 unused, 16 Fireball Rod |
| 6 | 7 Magic staff |

- **I** New Game equips slot 1 with the basic sword (`AlundraPlayerManager.cs:914` calls `SetPlayerWeaponId(..., 1)`).

## 3. Weapon table (binary `0x80028C78`, stride 13, identical to `StaticVariables.cs:1265`)

Layout per record: `[chargeable flag][ground: anim, type, delay, z][air: anim, type, delay, z][charge: anim, type, delay, z]`.
- Item to record map is at `0x80028C14`.
- `type` is the global sprite-table index spawned, `delay` is in ticks, and `z` is shifted `<<16` and added to the hero's PosZ.
- The ground record starts at `0x80028C79 + 13w`, the air record is +4, and the charge record is +8.

| w | Item(s) | Ground | Air | Charge | Can charge |
|---|---|---|---|---|---|
| 0 | 1 Basic sword, unmapped items, no weapon | 10 | 14 | none | no |
| 1 | 2 Steel Edge | 3F | 41 | 18, spawns 0x1B (Wave sword 1), delay 20, z 0 | yes |
| 2 | 3 Demon Buster | 44 | 46 | 18, spawns 0x1C, delay 20 | yes |
| 3 | 4 Holy Sword Elnard | 49 | 4B | 18, spawns 0x1D, delay 20 | yes |
| 4 | 16 Fireball Rod | 11, spawns 0x0B (Fireball), delay 10, z 0x20 | 15, same | 11, spawns 0x0C | yes |
| 5 | 14 Ice Rod | 11, spawns 0x0E, delay 10, z 0x20 | 15, same | 11, spawns 0x0F | yes |
| 6 | 9 Chain Throw | 12 | 16 | none | no |
| 7 | 10 Crusher | 40 | 42 | 19 | yes |
| 8 | 5 Hunter's Bow | 13, spawns 3 (Arrows 1), delay 8, z 0x10 | 17, spawns 3, delay 0, z 0x10 | none | no |
| 9 | 6 William's Bow | 13, spawns 4, delay 8, z 0x10 | 17, spawns 4, delay 8 | 13, spawns 7 (Charged arrows), delay 8, z 8 | yes |
| 10 | 7 Magic staff | 0 | 0 | none | no |
| 11 | 8 Item 008 | 10 | 14 | none | no |

- **F** An animation id of 0 (the staff, w=10) plays sound 3 and returns 0 (`0x8002ED30`).
- **F** Hero attack-animation ids, from the decompiled enum `Gameplay/PlayerAnimation.cs`: 10 and 14 sword basic, 11/15 wand, 12/16 flail, 13/17 bow, 18 and 19 charged sword and charged flail, 3F–4B steel, demon and holy sword, 40/42 steel flail.
- **F** A second per-animation table at `0x80028D14` (2 bytes per animation id) has two flags:
  - b0 means "the charge counts in this state". It is set for 00, 01, 02, 03, 04, 0E, 29, 2A, 2B, 2C, 2D, 35 and 3E.
  - b1 means "the delayed spawn may run". It is set only for 11, 13, 15, 17 and 18.

## 4. Charge (`UpdatePlayerWeaponEffect` 0x8002F49C, called at 0x80031D68 every tick)

- **F** `g_playerWarpTimer` is at `0x80126FFC`. Each tick:
  - If Square is held or was just released, and the weapon's chargeable flag is set, and b0 of the current animation is set, the timer goes up by 1 (cap 60).
  - Otherwise the timer is set to 0.
- **F** Attack animations have b0 = 0. Charging therefore starts only after the swing ends and Square is still held.
- **F** At timer >= 11 an attached effect 3 is created with z `0x100000`. Below 11 it is destroyed.
- **F** Every tick where `FrameCounter & 3 == 0`:
  - timer < 60 plays sound `0x2A`.
  - timer == 60 creates a spark (effect `0x10`) and plays sound `0x1AD` if `FrameCounter & 7 == 0`, else `0x1AC`.
- **F** Release path in `PlayerTryAttack` (`0x8002EB4C`–`0x8002EC84`) requires timer >= 60 and `IsOnGround`. It then:
  - creates 16 sparks in a 72x48 px ellipse with tangential velocities,
  - plays sound `0x2B`,
  - starts the charge record's animation.
- **F** Emulation: holding 59 ticks plus the release tick succeeds, and 58 plus release fails. The release tick itself increments the timer.
- **F** Timer, effect and pending-spawn pointer are zeroed:
  - at the hero init (`0x80031A18`–`0x80031A40`),
  - on BlockedByEntity, blocked input, warp and swimming paths (`0x80031BB4`, `0x80031BEC`, `0x80031C5C`, `0x80032614`, `0x80032768`, `0x800327A4`).
- **F** Logic is 50 Hz in this repo, so 11 ticks is 0.22 s and 60 ticks is 1.2 s.

## 5. Delayed spawn (`UpdateWeaponStepProgression` 0x8002F768, called at 0x80031D70)

- **F** `0x80127110` is a pointer to the weapon record, written only at `0x8002ED18` and cleared by `sw $zero`. `0x80127114` is a step counter.
- **F** Each tick, if the pointer is nonzero, `ptr[1] != 0` and b1 of the current animation is set:
  - while counter < `ptr[2]` the counter increments;
  - otherwise it calls `SpawnWarpEntity` (`0x80039F58`) with owner = hero, global table, type = `ptr[1]`, X, Y, PosZ + (`ptr[3]` <<16), direction = `{0, 0x10, 0x08, 0x18}[AnimationDirection]` (table at `0x80023644`), then clears the pointer.
  - If any condition fails, the pointer is cleared.
- **F** Emulated: the spawn happens on the (delay+1)-th call after the press tick. Delay 0 spawns on call 1, 8 on call 9, 10 on call 11, 20 on call 21.
- **F** The return value of `SpawnWarpEntity` is ignored. A full pool drops the shot silently and nothing is refunded.
- **F** If the hero's animation leaves the b1 set before the spawn, nothing spawns.
- **F** Melee swings (sword, flail) spawn nothing. Only wand, bow and the charged sword do.
- **I** Fireball, Ice ball, Arrows, Charged arrows and Wave sword are separate global-sprite entities. **F** The arrow entity's program is the native handler `AI_UpdateArrows` (`Gameplay/Scripts/FunctionTypeE.cs:439`, registered `SpriteEventHandlers.cs:220`). **U** The other projectile programs were not read.

## 6. Hitbox representation and delivery

- **F** A melee hitbox is not an entity. It is fields on the hero entity:
  - `FrameCollision` (+0x1D4) points at the current frame's 6-byte record.
  - `UpdateAnimation` (`0x80038C38`–`0x80038CB4`) converts it: signed bytes Offset X/Y/Z `<<16` into +0x208/+0x20C/+0x210, and unsigned Width/Depth/Height `<<16`, minus 1, into +0x214/+0x218/+0x21C.
  - `UpdateTileAttributes` (`0x80038064`–`0x800380B4`) sets `HitBox` (+0x1FC/+0x200/+0x204) = Pos + Offset.
  - `CurrentAttack` (+0x1C8) is set on every animation switch (`0x80038BAC`). It is Attacks[anim+1] of the hero's balance record, or Attacks[0] when anim+1 >= AttackCount, or null if AttackCount = 0.
- **F** Delivery is the generic `UpdateBalanceRecords` (`0x80039300`, once per tick, last of the entity update at `0x8003B3F0`). For each active entity with FrameCollision, CurrentAttack and a nonzero attribute:
  - For every other entity: skip if `FrameCollisionTickCounter` (+0x1D0) is nonzero, or `DamagedTickCounter` (+0x1CC) is nonzero, or `AnimFlags & 0x40` (invulnerable), or the class mask (`Flags` +0x6C) does not match.
  - Then an inclusive integer box test of the attacker's HitBox against the target's ModdedPos and Width/Height/Depth.
  - On a hit: `target.TouchingEntity (+0x224) = attacker` (only if the target's response is not `0x80`), `target.FrameCollisionTickCounter = 0x19`, `attacker.HitCounter (+0x220)++`, and 3 poofs (effect 9).
- **F** Order inside `UpdateEntities`: events (including MovePlayer), counters (`HitCounter` and `TouchingEntity` are zeroed there), lists, animation, physics, active effects, `UpdateBalanceRecords`. The hero therefore sees `HitCounter` in the next tick's MovePlayer.
- **F** Damage itself is done on the target side: `ComputeNewHp` (`0x8003A374`, reads `TouchingEntity.CurrentAttack`) then `ResolveBalanceTarget` (`0x8004464C`). I only name them.
- **F** `g_balanceAnimIndex` (`0x80146DF8`) = the hero's `TargetAnimationId` each tick.
- **F** Exported `.anim2d` `collision_keyframes`, down direction, are Box fixtures with min corner + size/2 and an empty profile. Examples: sword 3F has 7 boxes (23x19x15 px), bow 13 has 0, wand 11 has 4, flail charge 19 has 51.
- **F** Hero base attack table (balance.json, sprite 0, AttackCount 77), with bit 7 set so equipment power is added:

| Animation | Type | Power |
|---|---|---|
| 04 (sprint dash) | 5 | 2 |
| 10 and 14 | 1 | 1 |
| 3F and 41 | 1 | 2 |
| 44 and 46 | 1 | 4 |
| 49 and 4B | 1 | 8 |
| 18 | 1 | 8 |
| 12 and 16 | 2 | 3 |
| 40 and 42 | 2 | 6 |
| 19 | 2 | 12 |
| 1A, 1B, 37 (mine cart) | 1 | 100 |

  - Sword items add a single entry: Basic 1, Steel Edge 2, Demon Buster 4, Holy Sword 8.
  - Projectile records: Arrows 1, 2 and Charged arrows have power 1, 2 and 5; Fireball and Charged fireball 4 and 12; Ice ball and Charged ice ball 4 and 12; Wave sword 1, 2, 3 have 10, 20 and 38.
- **Decomp only (not re-read in the binary):** the damage formula `(power + sum of item powers) * response >> 4` (`EM:1204-1309`) and which items fill `g_itemBalanceRecords` (`PM:1138-1213`).

## 7. Tile cutting and flail bounce (CreatePlayerAnimationEffects, entry 0x8002FB14)

- **Entry address.** `0x8002FB14` is the function entry. `0x8002FB1C` is its second instruction (`addiu sp,sp,-0x68`).
- **F** For animations 04, 10, 12, 14, 16, 18, 19, 3F, 40, 41, 42, 44, 46, 49 and 4B only (jump table `0x800228AC`), it calls `CheckAndTriggerTileEffect` (`0x8003A7B0`) every tick, even under blocked input.
  - For the four corners of the HitBox, with the tile's flag bit 2 set and `HitBoxZ <= (tile height <<20) + 0x80000 <= HitBoxZ + CollisionHeight`:
  - it clears flag bit 2 and the wall tile id,
  - creates the map-info byte 9 effect at (tile centre, height <<20),
  - calls `RandomlySpawnItem(0xFF, ...)` (`0x80032C7C`),
  - plays sound `0x1F`.
- **U** `RandomlySpawnItem` behaviour (drop table) was not read.
- **F** In mode 0 only, for animations 12, 16 (flail 0) and 40, 42 (flail 1):
  - If `HitCounter != 0`, the hero switches to the hit animation from the table at `0x800228A4` = `{08, 1F, 25, 26}`: ground 08 and air 1F for the iron flail, 25 and 26 for the steel one.
  - Otherwise the same switch happens if any HitBox corner touches a tile higher than HitBoxZ or with walkability bit `0x40`.

## 8. Recovery, cancel, movement, sound, cost

- **F** Attack-state handlers (groups `0x80031E18` and `0x80031DF8`) never read the pad and never call `PlayerTryAttack`. They only:
  - turn a ground attack into Jump (`0x2D`) if `IsOnGround == 0`,
  - turn an air attack into Idle if `IsOnGround != 0`,
  - run `TryUseItem`.
  - There is no buffering and no combo. Recovery is the whole animation.
- **F** Attack-state handlers never write `TargetDirection`. The facing is whatever the Idle/Moving or jump state wrote just before.
- **F** Animation end: all ground attacks chain to 0 (Idle) and all air attacks chain to `0x2D` (`sprite-records.json` `End/ChainTo`).
- **F** Exported total duration (down, 50 Hz):

| Animation | Seconds |
|---|---|
| Sword ground (10, 3F, 44, 49) | 0.42 |
| Sword air (14, 41, 46, 4B) | 0.20 |
| Wand ground / air (11 / 15) | 0.62 / 0.50 |
| Bow ground / air (13 / 17) | 0.46 / 0.30 |
| Flail (12 / 40) | 0.62 / 0.68 |
| Charged sword (18) | 0.92 |
| Charged flail (19) | 1.16 |

- **F** Movement: every attack animation has Speed 0. Acceleration is 1 on the ground and 15 in the air (animation data). **Decomp only:** the shift formula in `PhysicsEngine.cs:1581-1597`. **I** Ground swings brake the hero and air swings keep their momentum.
- **F** No attack animation sets high `AnimFlags` bits, so there is no invulnerability.
- **F** Swing sound = the animation's `Sfx` (sword 29, flail 35, wand 37, bow 32, charged flail 36, sprint dash 83), played on animation switch (`0x80038BB0`–`0x80038BC8`). Sound 3 is the "cannot attack" beep.
- **I** Circle during an attack runs `TryUseItem`. A bean or bomb can interrupt a swing (`PM:2008-2041`, decomp only); `TryLaunchMagic` cannot, since it requires an animation below 2.

## 9. Decompilation vs binary differences

1. `g_playerEffectTransitionCooldown` is modelled as `byte[4]` (`PM:1028-1031`). In the binary it is a pointer to the table record (`0x8002ED18`, `0x8002F774`).
2. Charge-release sparks (`PM:1004-1015`):
   - Z offset is `+0x10` in the decompilation, `+0x100000` in the binary (`0x8002EB94`).
   - The binary also offsets the spark position by `offset[2i]*3*0x400`.
   - The binary's velocity index is `(2i+0x18)&0x1F`, not `2i`.
3. Hold-sparks (`PM:1561-1578`) omit the position jitter `+a*20` (`0x8002F6B4`–`0x8002F6F0`).
4. `CheckAndTriggerTileEffect` (`GameEngine.cs:2506-2523`):
   - Effect and item Z is height `<<20` in the binary (`0x8003A908`, `0x8003A978`), not `+0x80000`.
   - The effect id is map-info byte 9 (`0x8003A968`).
   - The Y tile index uses `>>20` for both edges (`0x8003A844`, `0x8003A860`).
5. Hit poofs: 3 in the binary (`0x800391A8`, loop `0x800392C0`), 2 in the decompilation (`EM:648`).
6. No-weapon MovePlayer indexing (see section 2).

## Open questions for the author

1. **Who owns the generic hit pass (`UpdateBalanceRecords`) and the balance load?** The attack task has no visible result against targets without it, and the flail bounce needs `HitCounter`. The hero's attack side and the target-reaction side both depend on it.
2. **How should the DLL read frame boxes?** Either the engine's `collision_keyframes` fixtures with engine overlap queries, or a new exact export of the 6-byte per-frame records so the DLL can rerun the original's inclusive integer box test. This changes the design and the exactness.
3. **Does the attack task include cutting grass (`CheckAndTriggerTileEffect`, E7 mutation and `RandomlySpawnItem`), or is the item drop stubbed?**
4. **Who builds "spawn a global sprite by index with an owner" (`SpawnWarpEntity`)?** It is also needed by beans, bombs and magic, and the DLL's spawn factory works from map records only. The projectile flight programs are E14 handlers.

## Suggested slice boundaries

- **A0 (shared prerequisite):** spawn by global sprite index with owner and direction, plus `CreateEffectEntity` with forces. The DLL has `AlundraEffectReservoir.CreateAttached`, nothing for the rest.
- **A1 (hero only, testable now):** weapon record tables and item map (no weapon gives w=0), press path in the seven states with the original precedence. The insertion points are `AlundraPlayerManager.cs:272-286` (Idle/Moving) and `ApplyGroundOrAirTail` (jump states). Also the `0x2000` gate, the error sound, and the attack-state cancel rules.
- **A2:** charge timer, aura and sounds, release path with 16 sparks. Needs effects, and must run per logic tick.
- **A3:** pending-spawn pointer and counter, then the spawns. Needs A0.
- **A4:** frame boxes, `CurrentAttack` from `balance.json`, hit delivery. Shared boundary with E14.
- **A5:** tile cutting. Needs E7 and the item drop.
- **A6:** flail bounce. Needs A4 and tile data.
- **Boundary with sprint:** SprintDash (anim 04) is reached from Sprint when `Hold & 0xE0` is set. It uses the same CurrentAttack and tile-effect machinery as the attack states.
