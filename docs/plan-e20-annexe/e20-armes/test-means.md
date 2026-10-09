> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

**Report: how the weapons and items will be tested and played (label test-means)**

Nothing in the repository was written or built. My scratch files are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/test-means/` (called `$S` below). I used a scratch copy of the e20-c1b oracle tools, which only read the executable and the exported data.

## 0. Things that change the plan

1. **No entity loses HP in production before E14.a.**
   - FACT: `ComputeNewHp` has no production caller (plan §1.6, "Hors périmètre" and "Écarts acceptés").
   - So E20.f and E20.h can only be proven on three kinds of observable: the contact (`TouchingEntity`, hero `HitCounter`, `+0x1D0 = 0x19`, 3 sparks), the reaction of scripted `0x87` programs (flag, class A cleared, animation), and pure `AlundraDamage` values.
   - The playable recette targets are therefore the scripted objects of §5. Beannoïdes only give sparks.
2. **A test save cannot carry a weapon, an item slot, MP, HP or money today.**
   - FACT: `Preset` has 10 fields (`AlundraTestSaves.cs:36-46`). `TryBuild` always starts from `InitializeNewGameStats` and `InitializeNewGameInventory` (`:112-114`) and then applies only flags, map-table entries and item counters (`:116-134`).
   - FACT: the save format and its validation already carry Hp/HpMax/Mp/MpMax/Money/WeaponId/ItemId (`AlundraSaveGame.cs:106-134`, checks `:403-451`).
   - Creating the weapon saves means extending `Preset`. `AlundraTestSavesTests.cs:70-72` pins the list to exactly two names and must change. `docs/test-saves.md` must be updated too.
3. **The inventory always equips the highest-rank owned item of a slot.**
   - FACT: `GetItemIdFromSlotId` (`AlundraPlayerManager.cs:1214-1263`).
   - Steel Edge, Demon Buster and Holy Sword (slot 1), Chain Throw (slot 3) and Hunter's Bow (slot 2) can each only be equipped if the better items of their slot are absent. The kit's item 1 (basic sword) must also be set to count 0 for the three swords.
   - `ItemCounts` can write 0, so a loadout is expressible, but several saves are needed (§7).
4. **`ArcSpec` has no inventory field.**
   - FACT: `AlundraArcSupport.cs:52-54`.
   - An arc with `Arrival` starts with an empty inventory and WeaponId 0, so the hero swings record 0 (basic sword). `InitializeNewGameInventory` only runs when there is no arrival (`AlundraWorldProxy.cs:1907-1911`).
   - Without `Arrival`, the arc gets the new-game kit. `HitArena` is of that kind: map 14, spec at `AlundraHitArena.cs:60`.
   - Weapons are given after construction (precedent, §2a). A small helper is enough.
5. **The ported story contains almost nothing to hit with the other weapons.**
   - Of the 37 story maps (30 chain maps + 14, 15, 44, 115, 116, 117, 362), only these hold a weapon-specific object: 14, 15, 362 and 10 (a bomb hole).
   - 44, 115, 116 and 117 are sword-only (levers).
   - To play the other weapons, the arenas must come from outside the chain (§6). Maps 6, 9, 13, 16, 19, 77, 136, 137, 145 and 303 are one portal hop away.
6. **The plan text "fire C54" has no support in the exported data.**
   - D-E20-18 (ADR-0053), the master plan and `D8-D9-stick-et-proprietaires.md:143` ("address not read") say fire is C54.
   - FACT: in `data-extracted/data/map_alundra.json`, the common-bank headers of sprites 0x0A to 0x0F all have `ProgramTick 60` and `ProgramDeactivate 9`. That covers the fire bolts 0x0B/0x0C and the ice bolts 0x0E/0x0F. No common sprite has tick 54.
   - Fire and ice bolts share C60/E9, so E20.f needs both rods' handlers, not only the hero-only ones.
7. **The existing MIPS oracle already runs every weapon with the real hit pass.** I only added rows to the inventory table (§4). The projectile side (real `0x80039F58`, handlers C60/C61/C94/E17/E9) is the part that does not exist.
8. **The binary refuses a spell at MP 0, unlike the decompilation.** The Magic Staff (item 7) bypasses the cost (§1).
9. **The weapon chests cannot be used yet.** The chest of map 77 holds the Hunter's Bow, Chain Throw and the bomb, but chests are native handlers assigned to E14 (`AlundraNativeEffects.cs:7`, INFERENCE). So test saves are the only way to get weapons in the DLL for now.

## 1. Inventory model

- **PlayerStats.** `Hp`, `HpMax` (new game 10/10), `Mp`, `MpMax` (0/0), `Money`, `WeaponId`, `ItemId`, `Falcon`, `FalconTemp` (`AlundraPlayerStats.cs:33-79`).
  - FACT: `SetPlayerWeaponId` accepts -1 or slots 1..6 and rejects 0 (`AlundraPlayerManager.cs:1157-1169`).
  - FACT: `MpMax` is capped at 4 and `Mp` at `MpMax` (`AlundraSaveGame.cs:57, 415-423`). `HpMax` is capped at 50 (`:53`).
  - FACT: `SetCurrentItemId` sets `ItemId`, the Circle item (`AlundraPlayerManager.cs:1285`).
- **Counters.** `AlundraGameState.NumberOfItems[2*item + 1]` is the count (`:1232, 1277, 1333`). The array has 256 entries.
  - FACT: a save requires `count ≤ items-properties[item][3]` for odd indices and 0 for even ones (`AlundraSaveGame.cs:344-356`).
  - Weapons have max 1. Herbs (36) have max 9. Bomb (35) has max 1. Keys have max 99.
- **`Data/items-properties.json`** (`AlundraItemTables.cs:132-134`) has rows `[slot, replace flag, rank, max, icon]`:

| Weapon slot | Items (rank) |
|---|---|
| 1 sword | 1 (0), 2 (1), 3 (2), 4 (3), 8 (4) |
| 2 bows | 5 (0), 6 (1) |
| 3 flails | 9 (0), 10 (1) (items 11, 12 are unnamed placeholders) |
| 4 ice rod | 14 (1) (item 13 is a placeholder) |
| 5 fire rod | 16 (1) (item 15 is a placeholder) |
| 6 staff | 7 (1) |

- **French names** (`Etc.yarn` nodes 0x200 + item): 1 Poignard, 2 Épée, 3 Épée infernale, 4 Épée sacrée, 5 Arc de chasse, 6 Arc en saule, 7 Bâton magique, 8 Épée légendaire, 9 Fléau de fer, 10 Fléau d'acier, 14 Bâton de glace, 16 Bâton de feu. The oracle notes use the US names (Steel Edge, Demon Buster, Holy Sword, and so on).
- **Weapon categories.** FACT: `item-drop-properties.json` Field3 & 0x7F = 1 for items 1-10, 14 and 16 (weapon). Armours are 17-20 (category 2). Rings and the like are category 3.
- **Weapon item to record** (`AlundraWeaponTables.cs:74-89`): 2→1, 3→2, 4→3, 5→8, 6→9, 7→10, 8→11, 9→6, 10→7, 14→5, 16→4. Every other item, and no weapon, gives record 0.
- **Circle items.**
  - FACT: `UseItem` is at `0x8003499C`. It calls `0x8004E0F8`, refuses ids ≥ 0x62, and jumps through the table at `0x8002339C` for `item - 0x1F < 0x14`.
  - The cases come from the decompilation (`PlayerManager.cs:1920-2004`). Every other id plays sound 3:

| Item | Name | Effect |
|---|---|---|
| 31 | Haricot | spawns sprite 1 |
| 32 | Cape de sable | sand cape |
| 35 | Bombe | spawns sprite 2 |
| 36 | Herbes | consumed, needs HP < max |
| 37 | Elixir de force | restores HP |
| 38 | Elixir de magie | restores MP |
| 39 | Essence magique | revive cutscene, E18 |
| 41 | Fortifiant | restores HP and MP |
| 43-50 | scrolls and books | `TryLaunchMagic` |

- **MP rules** (binary `TryLaunchMagic` at `0x800348E0-0x80034984`, disassembled).
  - FACT: it refuses with sound 3 if the carried entity `+0x2C ≠ 0`, if `+0x88 ≥ 2`, or if `[0x80127164] ≠ 0`.
  - FACT: if the weapon item is 7 it skips the MP check and the cost. Otherwise `GetPlayerMp() == 0` also refuses (`beqz` to `0x80034978`). The decompilation only plays the sound and falls through; the binary wins.
  - FACT: `DecreaseMp` (`0x8004DF3C`) has exactly one caller, `0x80034948`. I found this by scanning the `jal`s.
  - Cost is 1 MP per cast. Proven in the oracle: Mp 1→0 and 2→1 for a scroll and a book.
  - FACT: no weapon (bow or rod) tests MP, ammo or inventory. The plan says `PlayerTryAttack` has no such test, and no `GetNumberOfItem` or `UseItem` caller lies in `0x8002F49C-0x8002F884`. E20.f needs neither MP nor ammo. E20.h needs `MpMax ≥ 1`.
  - INFERENCE: bombs are never consumed, since the bomb path has no `UseItem` caller and the max count is 1.
- **Inventory screen.** `AlundraInventoryDirector.RunEquipWeapon` (`:818-896`) maps grid columns 0..4 to weapon ids 1, 3, 2, 4, 5. Column 5 is the fixed item 7 with weapon id 6. It ends in `SetPlayerWeaponId(state, tables, id)` (`:893`).
  - An empty slot plays sound 3 and changes nothing.
  - `RunEquipItem` (`:921-980`) ends in `SetCurrentItemId` (`:975`).
  - Grid rows: herbs 36, fortifiant 41, elixirs 37 and 38, essence 39; slot id 11 gives the bomb 35; slot ids 16-19 give scroll or book (43/44, 45/46, 47/48, 49/50); 32, 40, 54, 59, 31; then slot ids 20-22 (rings).
- **Buttons** (`alundra-casaengine-project-converter/Writers/PlayerSetupWriter.cs:29-32`): Cross is A, Square is X, Circle is B (UseItem), Triangle is Y.

## 2. Giving a weapon, an item or MP

**a) In a DLL arc (`ArcRun`).**
- FACT: after construction the state is `ArcRun.State = AlundraGameState.Instance` (`AlundraArcSupport.cs:126`). The tables are `((IAlundraScriptHost)arc.Proxy).ItemTables`, non-null in prefab arcs (asserted at `AlundraHeroAttackTests.cs:478-479`).
- Weapon, precedent `AlundraHeroAttackTests.cs:484-488` (and the `Equip` helper `:54-69` for the rig):
  - `state.NumberOfItems[2*item+1] = 1;`
  - `AlundraPlayerManager.SetPlayerWeaponId(state, tables, slot);`
- MP: `SetPlayerMpMax(state, n)` (`AlundraPlayerManager.cs:1037`) then `SetPlayerMp(state, n)` (`:1056`).
- HP, money and the Circle item: `SetPlayerHp` (`:1015`), `SetMoney` (`:1080`), `SetCurrentItemId` (`:1285`).
- `ArcRun.Dispose` resets everything (`:295-331`).
- Proposed test-support addition: `ArcRun.Give(int[] items)`, `Equip(int slot)`, `SetMp(mpMax, mp)` and `SetItem(item)`, plus a map parameter on `HitArena` (hard-wired to map 14, `:60, :74`).

**b) In the real game (F9).** `tools/AlundraTestSaves` (`docs/test-saves.md`).
- It must be run outside the Claude app, in Debug.
- F9 loads the newest slot by write date, and refuses in the cases listed in `docs/test-saves.md`.
- Only `day3-after-dream` and `day4-meeting` exist (`AlundraTestSaves.cs:54-68`). Neither has a weapon, so a weapon save must be created.
- What to add to `Preset`, applied in `TryBuild` with the real setters (`SetPlayerWeaponId`, `SetCurrentItemId`, `SetPlayerMpMax`, `SetPlayerMp`, `SetPlayerHpMax`, `SetPlayerHp`, `SetMoney`): `Weapon`, `Item`, `Hp`, `HpMax`, `Mp`, `MpMax`, `Money`.
- `ItemCounts` takes the raw index `2*item+1`; add `CheckPreset` bounds for the new fields.
- The slot name is `test-<name>` (a-z0-9_-, 32 characters at most).
- Raw indices: item 2→5, 3→7, 4→9, 5→11, 6→13, 7→15, 8→17, 9→19, 10→21, 14→29, 16→33, 1→3 (the kit's sword), 31→63, 32→65, 35→71, 36→73, 37→75, 38→77, 39→79, 41→83, 43..50→87,89,91,93,95,97,99,101.

**c) In the game once owned.** The author equips through the inventory (Start). The rows and columns of §1 apply, and sound 2 means equipped.

## 3. What the tick reads

- `MovePlayer` head, every tick: `CurrentWeaponRecord = RecordIndexOfItem(GetItemIdFromCurrentWeapon(...))` (`AlundraPlayerManager.cs:176-179`). Binary `0x80031B70`.
  - With no item tables, or no owned item in the slot, the result is NoItem and record 0.
- `UpdateItemEffectState`, end of each normal tick (`AlundraPlayerManager.cs:461-463`, `:588-594`, `:618-642`), calls `FillEquipmentRecords`:
  - record [0] is the highest owned armour (category 2);
  - record [1] is the equipped weapon (category 1);
  - record [2] is `SetItemIdFromCurrentItemId` (`:1303-1318`), which also rewrites `ItemId` with the best owned item of that slot.
- Binary callers of `0x8004E030` (`GetItemIdFromCurrentWeapon`): `0x800308F4` (UpdateItemEffectState), `0x80031A24` (hurt entry), `0x80031B70` (MovePlayer), `0x80034924` (TryLaunchMagic), and the inventory.
- `0x8004E0F8` is called at `0x8003094C` (tick) and `0x800349A4` (`UseItem`).
- Stubs still in place: `SpawnWeaponProjectile` (a counter `WeaponSpawnCallCountForTests`, `:567-570`), `UpdateItemUseDriver` (`:646-648`) and `TryUseItem`/`PlayerTryAction` (E20.h and E20.g).

## 4. What the MIPS oracles can do

- **`run_e20b.py` (AT9).** FACT: hero side for 11 weapon items, ground and air (`weapons.txt`).
  - It gives animation, sound, record pointer, counter, the spawn call journal, and per-tick hero frame boxes (`box_off_*`, `box_size_*`, `cur_attack_1c8`).
  - The hit pass, effects and spawn are stubs, so no hit is delivered and the projectile does not exist.
  - Hero boxes of the flail (`AT9g-item09`): from tick 9, offset (7, -6, 3) px, size 22x11x11. At tick 11 the offset is x = 30, at tick 15 x = 47. Crusher is the same at ticks 9, 12, 17. The sword boxes are 29x15x15.
  - The DLL pins AT9 in `AlundraHeroAttackTests.cs:346-361`.
- **`run_c1b.py` (K2 'HERO').** FACT: the real hit pass, one real Beannoïde, inventories built with the real `AddItem` and `SetPlayerWeaponId`.
  - The inventory table is data (`kworld.py:35-50`: name, owned items, weapon argument, item argument).
  - `c1world.KINDS` already holds one other-map target, `map69-sprite126`, the hammer-only post.
- **Scratch proof.** I added inventory rows and ran Square at tick 1 on a Beannoïde 40 px east (`$S/weapons_vs_target.py`) and on the post and out-of-reach cases (`$S/weapons_vs_post.py`).
  - Results are computed by the emulated binary, with the copied tools unchanged except for my driver. They are not pins.

| Weapon | Hero animation | Attack (attr, power) | First touch (Beannoïde dx 40) | Spawn call |
|---|---|---|---|---|
| basic sword | 0x10, idle at 22 | 0x81, 1 | tick 5 | none |
| Steel Edge | 0x3F | 0x81, 2 | tick 5 | none |
| Demon Buster | 0x44 | 0x81, 4 | tick 5 | none |
| Holy Sword | 0x49 | 0x81, 8 | tick 5 | none |
| item 8 | 0x10 | 0x81, 1 | tick 5 | none |
| Chain Throw | 0x12 | 0x82, 3 | tick 11 | none |
| Crusher | 0x40 | 0x82, 6 | tick 12 | none |
| Hunter's Bow | 0x13, idle at 24 | no attack | none | tick 10, type 3, x 212 px, y 96 px, z 1048577, dir 24 |
| William's Bow | 0x13 | no attack | none | tick 10, type 4, same position |
| Ice Rod | 0x11, idle at 32 | no attack | none | tick 12, type 14, z 2097153 |
| Fire Rod | 0x11 | no attack | none | tick 12, type 11, z 2097153 |
| Magic staff | stays 0 | none | none | none |

- **Rebound of the flail, new fact.**
  - Chain Throw: the hero goes to animation 0x08 on the tick after the touch (touch 11 gives 0x08 from tick 12 to 27, idle at 28). Crusher goes to 0x25 (touch 12, 0x25 from 13 to 28, idle at 29). The rebound lasts 16 ticks.
  - Without a hit (target at 100 px) there is no rebound. Chain Throw ends at idle 32, Crusher at idle 35.
  - Against the hammer post of map 69 the touch is at tick 9 for both flails. The rebound is 0x08 or 0x25 from tick 10 to 25, then idle at 26.
  - The sword on that post gives no `TouchingEntity` (immune), but `+0x1D0 = 0x19` is still set at tick 1 or 5, as in H6.
  - No horizontal recoil (hero x stays 212).
- **Item use, scratch proof** (`$S/items_use.py`, `$S/mp_probe.py`): Circle at tick 1, real `MovePlayer`, `TryUseItem` and `UseItem`.

| Case | Result |
|---|---|
| herbs at 5/10 HP | sound 49, count 3→2 |
| herbs at full HP | sound 3, not consumed |
| fire scroll, MpMax 1 Mp 1 | animation 0x32 from tick 1, `[0x80127164] = 47`, 0x33 at tick 33, idle at 34, Mp 1→0 |
| fire scroll, Mp 0 | sound 3, nothing |
| fire scroll, staff equipped, Mp 0 | casts, Mp stays 0 |
| fire book (48), Mp 2/4 | Mp 2→1 |
| no item equipped | sound 3 |
| bean (31) | nothing happens |
| bomb (35) | nothing happens |

  The bean and bomb do nothing because `0x80039F58` is stubbed to return 0, so they need the real spawn.
  I did not verify the herb heal amount: my poke of the entity HP did not match what the tick copies back.
- **What is missing for E20.f and E20.h.** For melee, the oracle needs only inventory rows. For projectiles it needs the real spawn `0x80039F58` and the common-bank sprite records from `map_alundra.json` (the bank layout code of `bank.py` exists), then the handlers.
  - Sprite headers (`ProgramLoad, Tick, Touch, Deactivate, Interact`) from `map_alundra.json`:
    - bomb sprite 2: (0, 62, 30, 12, 0);
    - arrows 3 and 4: (0, 0, 0, 17, 0);
    - charged arrow 7: (0, 94, 0, 1, 0);
    - rod bolts 0x0A-0x0F: (0, 60, 0, 9, 0);
    - sword waves 0x1B-0x1D: (0, 61, 0, 10, 0).
  - Effort UNKNOWN. The decider is a trial in a copy, like my rows above.
- **Hit-pass facts for the projectiles** (FACT from the common-bank headers and the balance records):

| Sprite | Hit mask | Attack | Power |
|---|---|---|---|
| arrows 3, 4, 7 | 0x001 (class A only) | 0x83 (equipment adds) | 1, 2, 5 |
| fire bolts 0x0B, 0x0C | 0x001 | 0x86 | 4, 12 |
| ice bolts 0x0E, 0x0F | 0x001 | 0x87 | 4, 12 |
| waves 0x1B, 0x1C, 0x1D | 0x001 | 0x81 | 10, 20, 38 |
| bomb 2 | 0x009 (A and B, so it also hurts the hero) | 0x04 | 5 |

  - The hero melee mask is 0x801 (header F = 0x03118C).
  - The charged sword wave comes from the weapon records 1, 2 and 3: charge `18 1B/1C/1D 14 00` (animation 0x18, delay 20).
  - William's charge record is `13 07 08 08` (type 7).

## 5. Targets (corpus counts match the plan: 182 `0x87`, 43 `0x95`, 76 `0x86`)

Method: all 483 `data-extracted/data/map_N.json`, decoded with the size table, balance chain at the threshold `D` of each map. The sprite bank rule matters: `SpriteDirection & 0x80` selects the per-map table with balance index +0x100, otherwise the common bank. A sprite number alone is ambiguous (my first pass confused them).

**Generic enemies.** Almost every hero-hittable enemy answers r = 16 (damage = power) to nibbles 1-3 and 5-7, and r = 0x40 (kill) to the bomb (nibble 4). Examples: Beannoïde 79 (9 HP), 73, 85, 121, 106, 91, 136. Weapons only differ by power on them.

Special enemies (outside the story):
- fire kills: sprite 193 (maps 21, 22, 24, 41), 169 (27, 29, 30, 31, 32, 33), 198 (339-345);
- ice kills: sprite 217 (59, 444, 445, 447, 448), which resists fire (r = 0).

**Objects that answer only one weapon type** (hero-hittable, class A):

| Type | Objects (bank, sprite: maps) |
|---|---|
| hammer (2) only | common 232: maps 5, 6, 7, 8, 9, 15, 19, 140, 479. Common 230: map 52. Map sprites 126, 120, 118, 119: Coastal Caves 69-75, Lizardman 80-83, 409, 410, 142, 343, 345, 346, 442. Native D31/E15, so inert before E14 |
| arrow (3) only | map sprite 173: 16, 60, 343, 344. Common 228: 352 |
| bomb (4) only | common 241, 242, 243, 253: maps 6, 8, 14, 61, 63, 64, 67, 80, 85, 90, 104, 151, 362, 382, 429 |
| fire (6) only | common 238, 239 (157 records): maps 2, 3, 4, 7, 13, 14, 15, 86-94 and others |
| hammer and ice | map sprite 134 (73 records): 50, 125, 126, 128, 304, 396, 443, 444, 447, 448 |

**Scripted `0x87` targets** (all class A and answering r = 16 unless noted):

| Type | Sites by map |
|---|---|
| sword | 77 (1), 136 (3), 304 (2), 429 and 430 (6 each, sprite 142) |
| hammer | 77, 136 (4), 442 (8, sprite 126) |
| arrow | 16 (1), 77, 136 (4) |
| tackle | 77, 136 |
| fire | 92 (16), 110 (4), 303 (6), 337 (8), 343 (6), 443 (2), 444 (9), 446 (7), 137 (1), 347 (sprite 162, r = 5, 68 HP) |
| ice | 396 (3), 443 (13), 444 (15), 446 (7), 447 (2), 448 (5), 137 (1) |
| bomb | 6, 14, 61, 63, 64, 67, 80, 90, 104, 151, 362, 382, 429 |

- Not reachable by the hero's mask: common sprite 25 (crates and jars, no class, maps 10, 145, 154, 331, 352, 448). Also sprite 171 of map 147 and sprite 63 of maps 443 and 444 have no class A at load (INFERENCE: a script sets it; the decider is to read their A programs).
- `0x95`: 28 bomb sites (the overworld maps 1-20 minus 11 and 18, plus 82, 84, 226, 331, 335, 407, 409, 411, 439, 479, each a 3x3 hole). Sword sites at 41 and 42 (5), and 226 (all indices).

**Distances.** Hops from the story maps over `Info.Portals` (static, ignoring flags): 6, 13, 16, 77, 136, 137, 145, 303 are 1 hop. 69 is 3 hops. 443 is 2. Map 14 has portals to 77 at tiles (43..44,43) and (41,43).

## 6. Proposed arenas

Positions are pixels from `(XPos*12+12, YPos*8+8, Height*8)` (`EntityRecordMapper.cs:177-204`). Stand points are INFERENCE from the cells (free, same level), to be confirmed by settle assertions as in the c1b-2 arcs. Spawn gating: a record only exists if the hero's tile at load lies in its zone (`AlundraWorldProxy.cs:868-875`), so an arc needs `Arrival`.

| # | Weapon(s) | Map and record | Target | Stand point | Observable |
|---|---|---|---|---|---|
| A1 | basic, Steel, Demon, Holy, Chain, Crusher, sword wave | 14 (existing `HitArena`): Beannoïdes records 0-6, sprite 79, 9 HP | flat free cells rows 2-7, hero tile (8,6) | per the oracle table | contact tick, 3 sparks, D at tick + 1, `+0x1D0`, rebound animation. Also in 14: fire-only bush rec 17 at (180,232,z144), bomb rock rec 24 (common 243) at (480,184,z80) |
| A2 | Chain Throw, Crusher | 69 `Coastal Cave-69`, rec 0, sprite 126 hammer post (132,232,z0) | immune to the sword, touched by flails | (110,232) facing east, or (154,232) facing west, 22 px | oracle: touch tick 9, rebound from tick 10 |
| A3 | Hunter's and William's Bow | 16 `Overworld 3,3-16`, rec 2, sprite 173 (876,504,z48), cell h3 | arrow-only, scripted `D[1]` | (854,504) or (830,504) facing east | T180 on, G229 on, ClassA cleared. The sword gives no touch (control) |
| A4 | Fire Rod | 303 `Olen, Cephas house`, records 4-9, sprite 51, z32, zone x35-51 y5-24 | `D @700/@724`: `0x87 [128,6]`, then flag and `0x2B` | the proven geometry of `AlundraFireSwitchRegressionTests.cs:34-37`: arrival tile (38,11), hero (950,184,z32) for rec 4, (950,280,z32) for rec 6, facing east | flag set and ClassA cleared with fire; the sword leaves them (existing regression) |
| A5 | Ice Rod and Fire Rod | 443 `Torla (inner)`, rec 1 and rec 2, sprite 51, (120,704,z64) and (288,704,z64), threshold D = 9 | `0x87` fire OR ice | all four sides free at 22 and 46 px, e.g. (74,704) facing east | `D[1] @1264` |
| A6 | Ice Rod | 396 `Sluice, Berue, Cave`, records 6-8, sprite 134 at (180,216), (204,216), (228,216), z144 | `0x87 [128,7]` then T5.. on | the three are adjacent, west and east are walls; only the N or S 22 px stands are free, e.g. (228,201) | T5 and its siblings |
| A7 | all weapon types except bomb | 77 `Underground Watercourse`, rec 22, sprite 50 at (1152,472,z80) | `D[22] @2048` tests types 0, 1, 2, 4, 5, 6, 7 and all lead to one reaction: sound 218, animation 4 then 1, G915 on, ClassA cleared. Not the bomb | the target sits at z 80 but the cells around it are height 3 (48 px) and ground property 0x10 | any weapon sets G915. Whether the sword reaches it is UNKNOWN (z mismatch) |
| A8 | sword, hammer, arrow, tackle | 136 `Church`, records 2, 3, 9, 13 (sprite 49) | each toggles a flag: T100 and T101 (hammer, sword or arrow); rec 9 sets G545 on hammer or arrow; rec 13 also accepts tackle | rec 2: (866,464) facing east is OK; rec 13 at (348,120): (370,120) or (348,134) | flag toggles |
| A9 | fire, ice | 137 `Church (basement)`, rec 12 (300,104) | `D[1] @2396`: a hit of fire or ice goes to End; anything else sets G548 | (300,118) facing north | inverted test, a good negative control |
| A10 | bomb (E20.h) | 14 rec 24 (480,184,z80), 362 rec 0 (216,104,z32), 6 recs 15 and 31 | bomb rock, scripted `0x87 [128,3]` (14: C130, D129) | 14: (458,184) or (502,184) | the 362 lid is the plan's acceptance |
| A11 | magic (E20.h) | any free cell of 14 | scrolls and books | none | Mp decrement, animation 0x32 and 0x33, `[0x80127164]` = item id |

Notes:
- The sword wave, charged arrows and every projectile arena (A3 to A6, A10) need E20.e0's spawn plus E20.f's handlers before they can pass.
- The melee arenas (A1, A2, A7 with the flails) can already pass the contact part today.
- Map 77's record 22 is the single object that reacts to six attack types. It is a handy universal detector, but its Z needs care.
- The fire-only commons (238/239) and hammer posts have native D handlers (E14), so only the contact is checkable before E14.
- Unlisted but useful: dev-style item-gift programs (`0x82`) in maps 17, 31, 77, 80, 158, 439 and 443. I could not tell how they are triggered, so I did not use them.

## 7. Proposed test saves (to create)

Common to all: `Hp 4/10` is possible to test herbs. `MpMax 4`, `Mp 4`. Items: bomb 35 (count 1), herbs 36 (9), elixirs 37 and 38, fortifiant 41. Scrolls 43, 45, 47, 49 (the books 44, 46, 48, 50 win in their slot when owned).

| Preset (slot `test-...`) | Map, tile, z | Owned weapons | Selected |
|---|---|---|---|
| `arsenal-a-m14` | 14, (8,6), 0 | 1 (kit), 5, 9, 14, 16, 7 | WeaponId 1 |
| `arsenal-b-m14` | 14, (8,6), 0 | 1, 4, 6, 10, 14, 16, 7 plus books 44, 46, 48, 50 | WeaponId 1 |
| `steel-m14` | 14, (8,6), 0 | item 1 count 0, item 2 | WeaponId 1 |
| `demon-m14` | 14, (8,6), 0 | item 1 count 0, item 3 | WeaponId 1 |
| `fire-m303` | 303, (38,11), 1 | as `arsenal-a` | WeaponId 5 |
| `arrow-m16` | 16, (34,31), 3 | as `arsenal-a` | WeaponId 2 |

Weapon slots reachable in the inventory from `arsenal-a`: 1 basic sword, 2 Hunter's Bow, 3 Chain Throw, 4 Ice Rod, 5 Fire Rod, 6 Magic Staff. `arsenal-b` gives 1 Holy Sword, 2 William's Bow, 3 Crusher.

Open choices for the lead:
- `Preset` could take a loadout list shared by several map entries instead of a record per combination.
- Tiles must pass `CheckInitialMap` and the camera limits (`AlundraSaveGame.cs:314-327`). The z values above are the cell height byte of the arrival cell, read from `Map.MapTiles`: 14 (8,6) → 0, 303 (38,11) → 1, 16 (34,31) → 3.
- The save keeps no direction (0) and no map-local state (`docs/test-saves.md`).

## 8. Unknowns and what decides them

| Unknown | Decider |
|---|---|
| Do maps 16, 69, 77, 136, 137, 304, 396, 443 build in `ArcRun`? Known to build: 14, 115, 116, 117, 44, 303 and the chain maps. | Smoke test per map, as L0 of c1b-2: construct, 6 frames, `AssertNoUnexpectedError`. |
| Stand points of A2 to A9 hold (no drift, `IsOnGround == 1`)? | Settle assertions. |
| Does the sword reach the 77 record 22 at z 80? | Reach sweep in an arc; a jump or air swing may be needed. |
| Are sprites 171 (map 147) and 63 (443, 444) given class A by a script? | Read their A programs and the `0x2A` sites. |
| Fire and ice bolts both on C60/E9, and C54 does not exist? | Common-bank header bytes `+0x13..0x17` of sprites 0x0B and 0x0E in the executable. |
| `SpawnWarpEntity` type = common sprite index; prefabs exist (`Entities/Flèches Niv.1` → `bankalundra_3`, `Boule de feu` → 11, `Boule de glace` → 14, `Bombe` → 2, `Épée d'onde Niv.1-3`, `Flèches chargées` → 7). | E20.e0. |
| Do bombs ever get consumed? | Disassemble `0x8003453C-0x80034680` and its continuation. |
| Does the native chest (sprite 30, `F255`) open in the DLL? | An arc on map 77 with a press at (528,912,z16). |
| Herb heal amount and HP copy-back in the oracle. | Set stats HP instead of the entity HP. |

## 9. Files

All scratch files are under `$S`:
- `extract_maps.py`, `maps_compact.json`, `names.json`, `etc.json`, `sizes.json`, `opnames.json`;
- `common.py`, `census1.py` to `census9.py`, `census_items.py`;
- `progdis.py`, `dis_at.py`, `cells.py`, `stand.py`, `graph.py`, `callers.py`, `disasm.py`, `globals.py`;
- `oracle/` (copy of the c1b tools), `weapons_vs_target.py`, `weapons_vs_post.py`, `items_use.py`, `mp_probe.py`.

I also read these repository files (none modified):
- `Alundra/Scripts/AlundraTestSaves.cs`, `AlundraPlayerStats.cs`, `AlundraPlayerManager.cs`, `AlundraWeaponTables.cs`, `AlundraItemTables.cs`, `AlundraInventoryDirector.cs`, `AlundraSaveGame.cs`, `AlundraBalanceTables.cs`, `AlundraHitOpcodes.cs`;
- `Alundra.Tests/AlundraArcSupport.cs`, `AlundraHitArena.cs`, `AlundraHeroAttackTests.cs`, `AlundraFireSwitchRegressionTests.cs`;
- `docs/test-saves.md`, `docs/plan-e20-heros.md`, `docs/plan-e20-annexe/e20-b/oracle/`, `docs/plan-e20-annexe/e20-c1/oracle/`, `docs/plan-e20-annexe/e20-c1b/oracle/`, `docs/plan-e20-annexe/e20-c1b2/arc-scaffolding.md`.
