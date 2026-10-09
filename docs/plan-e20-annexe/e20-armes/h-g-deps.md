> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# h-g-deps: slices E20.h (item use) and E20.g (carrying, throwing, solid crates)

Nothing was built, no tests were run, no `git` command was run, and the repository was not written to.

All tooling is in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/h-g-deps/`:
- `mdis.py`: capstone disassembler for `ALUN_CD.EXE`.
- `tbl.py`: table dumper.
- `scan.py`, `census.py`, `census2.py`: scanners over the drivers and the map placements.
- `evm.py` is a copy of `research/census/waits-0x24/m.py`.

Labels are FACT (binary address or `file:line`), INFERENCE, UNKNOWN. The binary wins over the decompilation. Item ids equal the `In` number of `EntityNames.csv`: id 1 is I01 Basic sword, and the new-game kit 1/17/25 matches the plan.

## 0. Things that change the plan

1. **Map 362 does not need the hero's bomb or any lifting.**
   - FACT: the bomb is record 5 (sprite 2). Olen's script spawns it with `0x8B [128,5,...]` at pc 569. The bomb's own `C[5]` program flies it with `0x1A [2]` after T4 (@635) and explodes it with `0x1A [1]` after T5 (@641).
   - FACT: `B[1]` gives the hero control at load. The lid is record 0, sprite 243, `MoreFlags 0xA1` (ClassA + Collidable). Its `D[1]` is `0x87 [128,3]` (@728).
   - FACT: the bomb sprite has `MoreFlags 0x4E` (HitsClassA + HitsClassB + ClassB + DeactivateOnAnimationEnd). Its balance entry has `Attacks[0]` = attr 4, power 5. Its animation 1 (explosion, sfx 46) has 28 collision boxes in `data-extracted/data/map_alundra.json`. The lid has a balance sheet (map sprite 243 gives index 499, HP 15, all responses 16).
   - INFERENCE: after E20.c1a and E20.c1b-1, the lid probably already reacts to Olen's bomb. D-E20-10 ("the 362 asks for a bomb placed on a lid") and the E20.h row ("the lid of 362") look mistaken.
   - Decision method: a test-only arc on 362 (like E20.c1b-2). This can be done at any time and needs no E20.h.
2. **E20.h's bomb and beans depend on E20.g.**
   - FACT: both start functions (`0x8003453C`, `0x80034224`) spawn the object and make it the carried entity (`hero+0x2C`). The player then holds, throws or drops it through the carry states of `MovePlayer`.
   - The order "weapons, item use, collisions" therefore cannot complete item use before carrying. Section 5 proposes splitting E20.h.
3. **A native-handler registry per sprite type is a prerequisite for E20.f, E20.g and E20.h, and it is currently inside E14.a.**
   - FACT: `RunSpriteEvent` runs only the save book and E0/E1 (`AlundraEventProgramRunner.cs:293-330`).
   - D-E14-1 plans the registry inside E14.a. E20.f (C61/E10 sword wave, C94 charged arrows) needs it first. So do E20.g (C23, C4, C70, E2, E20) and E20.h (C73/E21, C62/D30/E12, the spell handlers).
   - D-E14-4 and D-E20-24 put E14.a after all of them. The registry must be extracted as its own slice before E20.f, or E20.f owns it.
4. **Breaking a crate needs a converter change and a full export.**
   - FACT: the extracted `data-extracted/data/map_alundra.json` headers carry `BreakEffect` (beans have 8), and `DestroyEntity` `0x8003A59C` reads header `+0x1E` (`lbu 0x1e` at `0x8003A5E8`).
   - FACT: `Data/sprite-records.json` does not export it.
   - D-E14-2 puts the export in E14.a. E20.g needs it, so either it goes first or E20.g owns that converter change.
5. **Binary versus decompilation, and the DLL doc is wrong.** The binary's rule is that kind-2 and kind-3 liftables need item 0x3B (I59 Power Glove).
   - FACT (`0x8002EDF8-0x8002EE04`): `GetNumberOfItem(0x3B)`; `beqz v0 -> return 0`. Kinds 2 and 3 are liftable only if the player owns item 0x3B.
   - The decompilation (`PlayerManager.cs:1105`) reads it inverted. `EntityFlags.cs:282` documents "only while the player does not own item 0x3B". Do not copy either.
   - Corpus: kind 1 appears 2,244 times and kind 3 appears 7 times ("Pierre très lourde", 4 maps). Kind 2 never appears.
6. **Making crates and jars solid again changes arcs on the story chain.**
   - Liftable placements on chain maps: 390 (12 crates), 392 (12), 179 (8), 164 (17), 163 (8), 165 (8), 166 (6), 167 (23), 168 (15), 115 (13), 116 (9), 117 (2).
   - Tests tied to the deviation: `AlundraShipCorridorArcTests` (A5/A5r put the hero in the 392 corridor with "crates between the hero and the portal do not block", line 16), `AlundraMovementObstacleProbeTests.cs:130`, `AlundraEntityContactArcTests.cs:480`, `AlundraDialogueOpcodesProductionTests.cs:382`, and the crate tests in `AlundraHeroObjectTopsTests`, `AlundraHeroApexAndCeilingTests` and `AlundraAbsoluteZWritesTests`.
7. **The locked-control branch of `MovePlayer` calls the item cancel and `UpdatePlayerCarriedEntity(1)`.**
   - FACT (`c2-binary.md` §1): when `[0x800DC4B8] & 0x34`, the binary runs `0x8002FB14(1)`, `0x8002F884(1)`, then `0x800350C0` (item cancel).
   - The DLL returns early there (`AlundraPlayerManager.cs:220-223`).
   - Consequence: a dialogue opened during a held item sequence cancels it (bomb destroyed with effect 12, bean dropped). A carried object that has attacks is pulled into the hero and destroyed after 5 ticks (section 3.3). E20.g and E20.h must port those two calls.
8. **The HP model is a prerequisite of the heals.**
   - FACT: the heal functions write `hero+0x14` on the entity; the HUD reads `PlayerStats` through a mirror made at the end of `MovePlayer` (`0x80032914`).
   - The DLL has `Hp = HpMax = 10` on the entity (`AlundraEntityScriptProxy.HitPass.cs:70-71`) and no mirror. The life vessel writes `PlayerStats` directly (`AlundraPlayerManager.cs:1004`, "provisional seam").
   - See section 2.4 for the options.
9. **The sand cape and the spell-casting hero states belong to no planned slice.**
   - FACT: `MovePlayer` states `0x20-0x24` (sand) and `0x32-0x34` (spell) have handlers at `0x800324B8`, `0x8003251C`, `0x800325A4` and `0x80032680`.
   - E20.h as written lists neither. They would fit E20.h.

## 1. Binary skeleton of item use (E20.h)

- **`TryUseItem` `0x8002ED64`.** FACT.
  - No Circle edge (`0x80126F2A & 0x20`): returns 1 (`0x8002EDA8`).
  - Edge and `[0x800DC4B8] == 0`: `jal 0x8003499C` (the call site is `0x8002ED90`). It returns that function's result.
  - Edge and controls locked: `PlaySfx(3)`, returns 1.
  - Result 0 ends the tick in Idle, Moving, the jump states, the hold states, `0x05`, `0x0B`, `0x29` and `0x2A`. It is ignored in the attack groups and in `0x04`.
- **Start `0x8003499C`.** FACT.
  - `s0 = 0x8004E0F8()` (SetItemIdFromCurrentItemId, the equipped accessory).
  - Fails with `PlaySfx(3)` and returns 1 if `s0 >= 0x62`, or if `[0x80127164] != 0 && != s0`.
  - Otherwise switches on `s0-0x1F` (20 words at `0x8002339C`).
  - If the handler returns 0 it sets `[0x80127164] = item`, `[0x8012715C] = 0`, `[0x80127160] = 0` (`0x80034A90-0x80034A9C`).
  - Instant items return a constant 1 (`0x80034A38...`).
- **Driver `0x8003634C`.** FACT.
  - Returns 1 if `[0x80127164] == 0`.
  - Otherwise dispatches through `0x800235F4`, increments `[0x8012715C]` (saturating at `0x7FFFFFFF`), and zeroes `[0x80127164]` if the handler returned non-zero.
  - Call sites: `0x80031C6C` (hurt path) and `0x80031CC4` (every normal tick, result ignored).
- **Cancel `0x800350C0`.** FACT.
  - Dispatches per item through `0x800233EC`. Entries: `0x1F` → `0x80035100` (calls `0x80034ACC`), `0x23` → `0x80035118` (calls `0x80034B54`), `0x2C` → `0x80035130`, `0x2D/0x2E` → `0x80035148`, `0x2F` → `0x80035160`, `0x30` → `0x80035178`, `0x32` → `0x80035190`. Everything else goes to `0x800351A0`, which only zeroes `[0x80127164]`.
  - Callers: the locked branch (`0x80031BFC`), the default case `0x80032758`, the warp-tile branch `0x80032770`, the death branch `0x800327B4`, and the slope-4 helper `0x800351B8`.

## 2. Slice E20.h in detail

### 2.1 Items, entry points, what they do

All are FACT unless marked. "Fail" means `PlaySfx(3)`, return 1.

| Id | Name | Start | Driver and cancel | Behaviour |
|---|---|---|---|---|
| `0x1F` | I31 Jack's beans | `0x80034224` | driver `0x80035204`, cancel `0x80034ACC` | Needs carried == null, slope != 4, nothing in use. `SpawnWarpEntity(hero, 0, 1, x, y, z, 0)` spawns global sprite 1. Sets the bean as carried (`hero+0x2C`, `bean+0x28 = hero`). Hero anim 5 (ground) or 0x0B (air). Writes `hero+0x30..0x38` = bean position − hero position. Returns 0. The driver stays "in progress" while the bean is carried, its sprite is 1 and the slope is not 4. In water the cancel releases the hero's hold, spawns effect 8 and destroys the bean. |
| `0x20` | I32 Sand Cape | `0x80034320` | driver `0x80035260` | Start from slope 3 with target anim < 2 → hero anim `0x20`, returns 0. A second use while in `0x22` with zero force, on the ground and no entity under him → anim `0x21`, returns 0. The driver is "in progress" while the hero anim is in `0x20-0x24`. |
| `0x23` | I35 Explosive bomb | `0x8003453C` | driver `0x800352C4`, cancel `0x80034B54` | Needs carried == null, slope != 4, nothing in use, and fewer than 3 sprite-2 entities among the first 0x40 slots. Spawns global sprite 2 at the hero position, sets the fuse `Bytes[0..3] = 0xF0`, carries it (anim 5/0x0B), returns 0. The cancel releases the hold, spawns effect 12 (0x0C) and destroys the bomb. |
| `0x24` | I36 Herb | `0x80034680` | none (instant) | Only if Hp < HpMax: `0x80032E50(hero)` adds max(5, HpMax/4) to `hero+0x14`, capped at HpMax, plus global effect 14 (`0x8003BDD8(0,14,0,x,y,z+0x100000)`, field `+0x5C = 0x20000`). Sound 0x31. `0x8004E5C4` consumes one item. Otherwise fail. |
| `0x25` | I37 L Potion | `0x800346F0` | instant | If Hp < HpMax: `0x80033274` sets Hp = HpMax and creates 8 ring effects (tables `0x80023654/0x80023694`). Sound 0x32. Consumes one. |
| `0x26` | I38 Magic Elixir | `0x80034760` | instant | If Mp < MpMax (`0x8004DEA4` / `0x8004DDDC`): `0x800336F0` sets Mp to MpMax (`0x8004DEBC`) with ring effects. Sound 0x30. Consumes one. |
| `0x27` | I39 Cure Ale | `0x800347D4` | instant | If Hp < HpMax or Mp < MpMax: `0x80033A2C` sets both to max. Sound 0x30. Consumes one. Also the E18 revival item (`c2-binary.md` §2.5). |
| `0x29` | I41 S Potion | `0x80034870` | instant | If Hp < HpMax: `0x800330FC` adds max(10, HpMax/2), capped, plus 4 sparkles with 3 RNG draws each. Sound 0x31. Consumes one. |
| `0x2B-0x32` | I43-I50 Tomes (8) | `0x800348E0` | 8 drivers, section 2.2 | Needs carried == null, hero target anim < 2, nothing in use. If `GetItemIdFromCurrentWeapon() != 7` (the magic staff): Mp must be > 0, else fail; then `DecreaseMp(1)` (`0x8004DF3C`). `0x8005A9E0(byte[0x80023365 + item])` starts a CD voice (UNKNOWN: which sample, which DLL sound). Hero anim `0x32`, returns 0. |
| `0x21,0x22,0x28,0x2A`, others | I33, I34, I40 Aqua Cape, I42, ≥ `0x62` | n/a | n/a | Fail (default `0x80034AAC`). |

In-use sequences hold `[0x80127164]` and block any other use. This is the whole role of the beans, bomb and sand-cape drivers (they are 23 to 25 instructions each).

### 2.2 The eight tome drivers (the large part)

FACT sizes: earth upper `0x80035320` (0x1B0 bytes), earth lower `0x800354D0` (0x2C0), water `0x80035790` (0x2F4, shared by `0x2D` and `0x2E`), fire upper `0x80035A84` (0x1E0), fire lower `0x80035C64` (0x24C), wind upper `0x80035EB0` (0x368), wind lower `0x80036218` (0x134). About 4,100 bytes in total.

SpawnWarpEntity sprite arguments, read from the `a2` immediates (global bank, hero as spawner):

| Tome | Sprites spawned | Header codes (map_alundra.json) |
|---|---|---|
| Earth upper `0x2B` | 16 "Earth magic Lv.1" (`0x800353EC`) | MoreFlags `0x14`, E3 |
| Earth lower `0x2C` | 17 "Earth magic Lv.2" (`0x8003567C`), then `0x8003A774` | MoreFlags `0x14`, E13 |
| Water `0x2D/0x2E` | 19 "Water magic Lv.2" (`0x80035874`), heals via `0x80032F84` (+max(5, HpMax/2), 4 sparks) and `0x80033274` (full), sfx 48 | MoreFlags `0x34`, C75, brk 8 |
| Fire upper `0x2F` | 20 "Fire magic Lv.1" (`0x80035B0C`), 23 "Fire Lv.1 projectile" (`0x80035BF4`) | 20: MoreFlags `0x44`, E1; 23: `0x74`, C63, E3 |
| Fire lower `0x30` | 21 "Fire magic Lv.2" (`0x80035CEC`) | MoreFlags `0x44`, E28 |
| Wind upper `0x31` | 22 "Wind magic Lv.1" (two spawns, `0x80036054` and `0x80036134`), calls `0x8003AF70`, `0x8003A774` | 22: MoreFlags `0x44`, E1 |
| Wind lower `0x32` | 24 "Wind magic Lv.2 (lower)" (`0x800362A0`) | MoreFlags `0x44`, C65, E1; the decompilation shows `IsFadeActive` and `g_windMagicEntityPtr`, so likely a warp or teleport (INFERENCE) |

- UNKNOWN: the bodies of these handlers (C63, C65, C75, E3, E13, E28) were not read. Reading them is the first task of the tome sub-slices.
- The hero's casting states `0x32/0x33` (`0x800325A4`) and `0x34` (`0x80032680`) hold while `[0x80127164] - 0x2B < 8`.

### 2.3 What the DLL has today (E20.h)

- `AlundraPlayerManager.cs:468`: `TryUseItem` returns 1. Circle is bound to "UseItem" (`AlundraPlayerController.cs:109`), but nothing reads it. So even the fail sound 3 never plays.
- `AlundraPlayerManager.cs:646`: `UpdateItemUseDriver`, an empty named stub. Called at `:230`, which is the second site `0x80031CC4`. There is no hurt-path site yet (E20.c2).
- `AlundraPlayerManager.cs:1303` (`SetItemIdFromCurrentItemId`), `:1267` (`GetNumberOfItem`), `:1349` (`UseItem`, the consumer), `:1015` (`SetPlayerHp`), `:1056` (`SetPlayerMp`, MpMax capped at 4). There is no `DecreaseMp`.
- `AlundraNativeEffects.CreateLifeVesselEffects` (`AlundraNativeEffects.cs:28`) already builds global effect 14 rings and sparks, so potions can follow its pattern.
- `AlundraItemTables` carries column 3 (max count) and the category. The HUD reads `PlayerStats` (`AlundraHudDirector.cs:544-586`).
- The hero states `0x05`, `0x0B`, `0x20-0x24`, `0x32-0x34` have no case in `MovePlayer`. The hero has no spell sprites.

### 2.4 What E20.h needs from other slices

- **E20.e0** (spawn by global sprite index): beans (1), bomb (2), spells (16, 17, 19-24). FACT: `SpawnWarpEntity(a0 spawner, a1 bank flag (0 global, nonzero adds 0x100), a2 sprite, a3 x, [sp+0x10] y, [sp+0x14] z, [sp+0x18] direction)`. It also draws loot at spawn (`0x80039D04`).
- **Registry per sprite type** (point 3 in section 0) for C73/E21, C62/D30/E12 and the spell handlers.
- **E20.g** for the bean and the bomb (carry, throw, drop).
- **HP model of E20.c2.** The herb, the S Potion and water magic add to `hero+0x14`.
  - Option A (recommended): pull forward only the entity-HP part of E20.c2. That means the hero HP overwritten from `PlayerStats` at spawn, the mirror at the end of `MovePlayer`, the early returns at `AlundraPlayerManager.cs:208-213` and `:220-223` reaching the end site, and the life vessel rewritten. `c2-binary.md` §0.1 and §0.2 describe it.
  - Option B: a `HeroHealth` seam that writes `PlayerStats` today, redirected by E20.c2. Cheap, but a second rework.
- **Engine:** none expected for the instant items. The carried-entity follow is the open risk (section 3.4).
- **E20.f:** only the weapon-id-7 test in the tome start, and the shared `SpawnWarpEntity`.
- **E20.i:** the slope-4 cancel (beans and the bomb are dropped, or the sand-cape refused, in deep water). Without E20.i, slope 4 is never seen in the DLL.

### 2.5 Proposed sub-slicing of E20.h

- **h0, item-use frame (no dependency).** Circle edge, the control-flags test (sound 3 when locked), the start switch with its fail cases, `[0x80127164/5C/60]`, the driver wrapper and the cancel table as named dispatchers, and the two extra call sites in the locked branch and in the hurt path. Observable change: the fail sound 3 plays on unusable items. Proof: a MIPS-emulated oracle of `0x8002ED64`, `0x8003499C`, `0x8003634C`, `0x800350C0`, from `docs/plan-e19-h2-annexe/tools/emu.py`.
- **h1, instant items (needs the HP model).** Herb, L Potion, S Potion, Magic Elixir, Cure Ale (the heal part only; the revival is E18), consume, sounds 0x30-0x32, effects. Oracle: run `0x80032E50`, `0x800330FC`, `0x80033274`, `0x800336F0`, `0x80033A2C` against synthetic heroes and compare HP, MP, effect list and RNG draws. Chests already give herb (36) and S Potion (41) on the chain, so this is directly usable.
- **h2, sand cape (needs slope-3 physics).** Optional, with its own states. UNKNOWN whether the story needs it. Candidate for deferral.
- **h3, beans and bomb (needs E20.e0, E20.g, the registry).** Starts, drivers, cancels, C73/E21 and C62/D30/E12. The bean planting needs the "Pot magique" (sprite 153) and E21's check on `SpriteTableIndex == 0x199` (map sprite 153; 39 placements on maps 12, 14, 15, 17, 60, 77, 139, 144, 145, 342, 356, 380, 407, 431, 439, 447). Bomb recipe: the optional rock of map 10 `C[75]` (`0x95` @6418 with typeIdx 3) and the secrets of maps 14/15.
- **h4a-h4d, tomes by element** (earth, water, fire, wind). Each starts by reading its sprite handlers. Water reuses h1's heals. Wind lower probably needs the warp or fade directors. Largest and last.
- **362 lid:** a test-only arc, see point 1 of section 0, independent of all of the above.

## 3. Slice E20.g in detail

### 3.1 The binary mechanism (all FACT)

- **`PlayerTryAction` `0x8002EEAC`** (called in Idle, Moving, the jump states, `0x29`, `0x03`, `0x2A`).
  - Carrying (`hero+0x2C != 0`): `hero+0x88 = (hero+0x144 != 0) ? 0x0C : 0x30`, returns 1. This ends the tick whenever the hero carries.
  - Else if `hero+0x130` (the entity that blocked this tick's step, `XCollisionEntity`) is non-null: with a Square edge (`0x80126F2A & 0x80`) it returns `TryLift(that entity)`. Without the edge it falls into the loop below.
  - Loop over slots 0..`[0x80134360]`: status 2 or 3, `+0x20 == 0`, `+0x12C == hero` (standing on the hero): `TryLift(entity)`; the first non-zero result wins. Otherwise returns 0.
- **`TryLift` `0x8002EDBC`.** Kind = `(flags & 0x600) >> 9`.
  - Kind 0 or ≥ 4: return 0.
  - Kind 1: lift.
  - Kinds 2 and 3: lift only if `GetNumberOfItem(0x3B) != 0`.
  - Lift: `hero+0x2C = it`, `it+0x28 = hero`. If `it+0x12C == hero`, hero anim `0x0C` (ground) or `0x30` (air). Otherwise `0x05` (ground) or `0x0B` (air). Writes `hero+0x30..0x38` = `it.Pos − hero.Pos`. Returns 1.
- **Hero states.**
  - `0x05` (`0x800322F0`): `TryUseItem`; carried null → `0x2D`; on the ground it waits for the animation chain; in the air → `0x30`.
  - `0x0B` (`0x800322B4`): `TryUseItem`; carried null → Idle.
  - Carry group `0x06,0x07,0x0C,0x2E,0x2F,0x30` (`0x8003232C`): `TryUseItem`; carried null → Idle on the ground, `0x2D` in the air. A Square edge throws, unless `hero+0x180 & 0x2000` (no-action ground property).
  - Throw: `carried+0x8C = byte table 0x80023644[hero+0x98]` (0x00, 0x10, 0x08, 0x18); carried position = hero X, Y and Z + `0x200000` (32 px).
    - Ground: hero anim 9; `carried+0x24` = 1 (still) or 2 (moving).
    - Air: hero anim `0x0A`; `carried+0x24` = 2 (still) or 3 (moving).
  - A heavy object (kind 3) keeps the hero in `0x0C` or `0x30` (no walking, no jump).
  - Otherwise: Cross edge (unless `hero+0x180 & 0x4000`) → `0x06` or `0x2E`; moving → `0x07` or `0x2F`; still → `0x0C` or `0x30`.
  - States 9 and `0x0A` sit in the attack groups `0x80031E18` and `0x80031DF8`: no pad read, `TryUseItem` after.
  - States `0x38` and `0x3D` fall into the default case `0x80032758`, which calls the item cancel.
- **`UpdatePlayerCarriedEntity` `0x8002F884`**, three modes.
  - Mode 0 (every tick at `0x80031D78`, and `0x80031D68-0x80031D78` before the state switch): eases `hero+0x30..0x38` toward (0, 0, `0x200000`) at `0x10000` per tick per axis. X and Y move only while |Z error| < max(|dx|, |dy|). Resets `[0x80098F28]` to 0.
  - Mode 1 (locked branch): if the carried entity's sheet byte `0xE` is 0, it becomes mode 0. Otherwise, after 5 ticks of `[0x80098F28]`, it eases the offset by `0x20000` and destroys the carried entity (`0x8003A774`) once all three offsets are below `0x10000`.
  - Mode 2 (hurt, slope 4, death, warp tile): `carried+0x24 = -1`, direction = hero's, position = hero's + 32 px. It does not clear `hero+0x2C`.
- **Physics `MoveEntity` `0x80037E34`.** An entity with `+0x28` (platform) non-null is not simulated. It is placed at `platform.Pos + platform.RelativeWarpOffset` (hero `+0x30..+0x38`), with `CollidedWithEntityZ = 1`, `ForceAdjusted = 1`, `XCollisionEntity = null`, and the floor and terrain heights copied from the carrier (decompilation `PhysicsEngine.cs:60-110`).
- **The thrown object's own handler.** C23 (`0x8006B848`, crates and jars) is verified in the binary.
  - Carrier set and `+0x24 == 0` → anim 0.
  - Else anim `word[0x80027400 + 4*(+0x24)]`, i.e. state 1 → 3, 2 → 1, 3 → 4, −1 → 3. Then `+0x24 = 0`, `carrier+0x2C = 0`, `+0x28 = 0`, flags `|= 0x30` (DeactivateOnImpact, DeactivateOnHit) and `&= ~0x80` (no longer collidable).
  - The meaning "3 = put down, 1 = ground throw, 4 = moving air throw" is INFERENCE.
  - E2 (`0x8007ED30`, decompilation only): on impact, set anim 2 and DeactivateOnAnimationEnd; when anim 2 ends, or a hole is hit, `DestroyEntity(entity, -1)`. That gives the break effect (header `+0x1E`) and the loot (`0x80032B90`).
  - C4 (`0x80066984`), C70/E20 (torches) and C73 are the variants for items on the ground, torches and beans, with other animation tables (decompilation).
  - The thrown crate has `MoreFlags 0xE4` (HitsClassA), so it hurts class-A targets through the hit pass already ported.

### 3.2 Corpus (FACT, census over `alundra-project/Maps/*/tilemap`)

2,251 liftable placements: crates 995 on 127 maps, jars 914 on 189 maps, barrels 76, torches 57, logs/small iron balls 56 (kind 1), jars no. 2 (42), stones 27, turtle rocks 21, ice blocks 15, planted herbs 8, the four "Objet ... (transportable)" 32, "Pierre très lourde" 7 (kind 3), the 362 bomb 1.
Native codes (kind, C, D, E): (1, 23, 0, 2) 1,995; (1, 4, 0, 2) 159; (1, 70, 0, 20) 57; (1, 4, 0, 0) 32; (3, 23, 0, 2) 7; (1, 62, 30, 12) 1. E2 therefore covers 2,161 of them, matching O-E19-23.

### 3.3 What the DLL has today (E20.g)

- Stubs: `AlundraPlayerManager.cs:471` (`PlayerTryAction`, returns 0), `:579` (`UpdatePlayerCarriedEntity`, empty). The call sites and their order are already right (`:277`, `:301`, `:320`).
- The fields exist and are unused: `Flags2`, `PlatformEntity`, `CarriedEntity`, `RelativeWarpOffsetX/Y/Z` (`AlundraEntityScriptProxy.cs:74-79`), copied in `Clone` (`:2407-2412`).
- `XCollisionEntity` is already the binary's `+0x130` (`:2223-2228`, comment names "the grab"). `CheckEntityInteraction` consumes it (`AlundraPlayerManager.cs:893-940`).
- `EntitySupport.IsEligibleSubject` already excludes `PlatformEntity != null` (`EntitySupport.cs:67-75`), as the binary does.
- Per-tick motion: non-hero entities run `AlundraScriptedMotion.TickScriptedNpc` then `EvaluateEntitySupport` in `TickMotion` (`AlundraEntityScriptProxy.cs:1465-1485`). No platform-follow branch exists. That branch is the new piece E20.g adds (the binary's `MoveEntity` rule above).
- Natives: only E0/E1 run (`AlundraEventProgramRunner.cs:300-322`). C23, C4, C70, E2 and E20 are counted no-ops.
- The E-slot pick for `DeactivateOnImpact/OnHit/OnAnimationEnd` is already ported (`AlundraEntityScriptProxy.cs:1561-1563`).
- `DestroyEntity` is a port without `ActiveEffect`/`CarriedEntity` cleanup or loot (`AlundraWorldProxy.cs:3020-3033`).
- Hero states `0x05-0x0C`, `0x2E-0x30` have no case. `0x09` and `0x0A` are in the attack groups but nothing starts them.

### 3.4 The two deviations and the barricade

- **D-E19-28 and D-E19-46 (ADR-0021, ADR-0022; O-E19-23).** FACT: the one code site is `AlundraMovementObstacleProbe.cs:49`, passing `EntityFlags.PickupKindMask` as `skippedCandidateFlags` to `AlundraEntityCollision.FindEntityCollisionCandidate` (declared at `AlundraEntityCollision.cs:32`). Removing the mask makes liftables obstacles. The three tests that use the mask are listed in point 6 of section 0.
- **Barricade of map 392 (O-E20-5).**
  - FACT (cells and records): crates are on ground height 4 (64 px). Walls flank rows 18-20 at x=27..28. At x=26 (record `XPos/2`): row 17 has rec4 + rec8 (2 high), row 18 has rec5 only (1 high, 16 px), rows 19 and 20 have rec6 + rec10 and rec7 + rec11 (2 high, 32 px).
  - The earlier statement that all four rows are 2 high is wrong for row 18.
  - FACT: the jump apex is 27.5 px (plan UH-1) and the hero lands on object tops (D-E19-42). A 16-px crate is therefore climbable; a 32-px stack is not.
  - E19's R4 note (`plan-e19-opcodes.md:9548-9551`) says solid crates leave only a 3 px Y window on crate 5, which is why solidity was deferred.
  - INFERENCE: lifting rec5 (kind 1, free) is the straightforward way through; climbing crate 5 is possible only inside that narrow window.
  - UNKNOWN: which one the original expects. Decision method: after the lift and throw slices, an arc on the real preset with the real controller, plus a geometry search on the footprint (21 × 15 px) against the 3-px window.

### 3.5 What E20.g needs from other slices

- **Registry per sprite type** (section 0, point 3) for C23, C4, C70, C73, E2, E20, E21.
- **E20.e0:** the loot and `DestroyEntity` (`0x8003A59C`, `0x80032B90`, break effect via `0x8003BE74`).
- **Converter:** `BreakEffect` export plus a full export (D-E14-2).
- **E20.c2:** `UpdatePlayerCarriedEntity(2)` at the hurt-path call site, which E20.c2 inserts. It is a named stub today (D-E20-19).
- **E20.d:** the run states `0x29`, `0x03` and `0x2A` also call `PlayerTryAction`.
- **Engine:** UNKNOWN whether the carried entity's character controller can be bypassed by writing the logical position (the existing `PushLogicalPositionToRoot` suggests yes). A spike is cheaper than a decision.

### 3.6 Proposed sub-slicing of E20.g

- **g0, prerequisites** (shared with E14.a): the registry, the `BreakEffect` export, the loot and destroy path.
- **g1, lift and carry.** `PlayerTryAction`, `TryLift` (Power Glove for kinds 2/3, as the binary), states `0x05`, `0x0B`, `0x0C`, `0x06/0x07/0x2E/0x2F/0x30`, the carry-lost fixups, `UpdatePlayerCarriedEntity(0)` and the platform-follow branch, the carried-state C handlers. Solidity stays off, so the chain arcs do not move.
- **g2, throw and break.** The Square edge in the carry states, the direction table, states `0x09/0x0A`, the `+0x24` modes, C23's switch to a projectile, E2 break, `UpdatePlayerCarriedEntity(1)` and `(2)`, the thrown-object damage through the hit pass (an arc against a Beannoïde of map 14).
- **g3, solid again.** Remove `AlundraMovementObstacleProbe.cs:49`'s mask, rewrite the listed tests and A5/A5r, re-measure all chain arcs on the 12 maps of section 0 point 6, and check `CheckEntityInteraction` priority.
- **g4, barricade 392.** Decision (lift or climb) and the recipe.

## 4. Oracle method for both slices

The MIPS emulator already exists (`docs/plan-e19-h2-annexe/tools/emu.py`, `mips.py`; `docs/plan-e20-annexe/d1-oracle-moveplayer/` runs the whole `MovePlayer`). E20.g can be compared tick by tick on `MovePlayer` for the lift and the throw (hero state, `+0x2C`, `+0x30..0x38`, the carried entity's `+0x24` and position). E20.h h0 and h1 are pure and small enough to compare the same way. The thrown object's flight needs the ported physics; compare it against a recorded oracle run of the C23 animations (speeds from `sprite-records` / `map_alundra.json`).

## 5. Order recommendation

The author's order was weapons (E20.f), item use (E20.h), collisions (E20.g). With the dependencies found:
1. E20.e0 (spawn), then the registry slice and the `BreakEffect` export. Both are prerequisites of E20.f too.
2. E20.f (weapons).
3. E20.h: h0 and h1 (instant items). They need only the HP model (option A) and are visible: herb and S Potion are already in chests of the chain.
4. E20.g: g1, g2, g3, g4.
5. E20.h: h3 (beans, bomb), then h4 (tomes), with h2 (sand cape) wherever it fits.
6. The 362 arc can run any time.

This keeps "item use" before "collisions" for everything that can be done, and defers only what truly needs carrying.

## 6. What "collisions" could mean besides E20.g

All are listed in plans or ADRs; the main session picks.
1. **E20.g's reading (current):** liftables become obstacles again (D-E19-28/46), plus carrying and throwing.
2. **Native-destruction obstacles, about 600** (iron-ball walls, burning thorns, ice columns, rocks and lids, pillars). They stay solid until E14 (D-E19-34, ADR-0022). Weapons and items (bomb, fire, ice, hammer) are what destroys them, so their "collision" only becomes meaningful after E20.f and E20.h.
3. **Terrain rules for the hero not ported (O-E19-22, "to be placed"):** deep water blocks the hero without Merman boots (10,644 cells on 54 maps, 399 on map 416), and hazard cells hurt (7,322 cells). The hazard hurt is E20.c2's; the water block is E20.i or c2.
4. **Exact contact resolution (O-E19-42, O-E19-28):** the binary splits a step jointly in 16.16 (`ComputeXYPosition` `0x80037730`); the DLL slides along contacts. D-E19-58 kept the sliding. Porting the exact rule needs an engine plan and an ADR, and the author's choice.
5. **Vertical contact:** ceilings (`0x80036D94`), the top magnetization doors (O-E19-44: door 2 approximated by E19.m13), the 4-px downward snap versus the binary's 3 px (O-E19-40, engine).
6. **The hit and attack collisions:** done for the hero (E20.c1a, c1b-1), still to come for the thrown objects (E20.g), arrows and spells (E20.f, E20.h), and enemy attacks (E14).
7. **Engine refactor leftovers** (memory `collisions-architecture-cible`): attack and defence semantics (largely done by E20.c1a) and the editor UI for collision timelines (not a game feature).
8. **Entity-versus-entity riding:** `RidingEntity` is recomputed once per frame, not per tick (D-E20-13). E20.g's carried and stacked objects (392) would benefit from a per-tick recompute.

## 7. UNKNOWN list with the decision that would settle each

- Whether the 362 lid already reacts to Olen's bomb: a test-only arc on 362.
- Whether the original passes the 392 barricade by lifting or by climbing crate 5: section 3.4.
- Which CD voice `0x8005A9E0` plays for each tome, and the bodies of the spell handlers: read the binary at the drivers' sprites.
- Whether the sand cape (and its states) is needed for the chain: grep the chain's items and maps for slope-3 cells.
- Whether a carried controller-bearing entity can be moved by writing its logical position: a spike.
- Whether item start is refused while the hero is hurt (c2-binary U4): read the per-item start functions (beans, bomb, tomes check carried, slope and animation; no hurt test was seen in the beans start).
- What a stack (392 rows 19/20) does when its lower crate is lifted: the physics of riders (`RidingEntity`), emulated or tested on the E20.g arc.
