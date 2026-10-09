> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

**e0-spawn: read-only discovery for slice E20.e0**

Labels: FACT[B] = decoded in ALUN_CD.EXE (France). FACT[E] = executed on the real binary code in the MIPS machine. FACT[D] = DLL source. FACT[X] = exported data. INF = inference. UNK = unknown, with what would decide it.

Nothing was written in the repo, built or run in place. The oracle tools were copied to `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/e0-spawn/` (`tools/`, `out/`). The probes I wrote are `exp_spawn.py`, `exp_spawn2.py`, `loot_trials.py`, `callers.py`, `funcs.py`; they run the real `0x80039F58`, `0x80039B28`, `0x80039AD0`, `0x80039D04`, `0x80032A40`, `0x80032B90`, `0x8007C174` and the entity passes. "Binary = gospel" holds throughout; decompilation losses are noted.

## Things that change the plan

1. **Weapons need only one of the 102 call sites.**
   - FACT[B]: the sites split as follows.

     | Group | Sites |
     |---|---|
     | Hero weapon shot (`0x8002F818`) | 1 |
     | Loot (`0x80032BEC`, `0x80032CD8`) | 2 |
     | Item use (`0x80034284`…`0x800362A0`, driven by `0x8003499C` and `0x8003634C`) | 11 |
     | Enemy and boss handlers | 88 |

   - The 88 enemy and boss sites are: 80 spawn map-local sprites (map-table sprites, `a1=1`), and 7 spawn global sprites (0xEC ×4, 0x19, 0xC, and the chest-item spawn in F255).
   - So the author's order "armes, objets, collisions" needs from E20.e0 only the spawn API (E20.e0a below). The loot, RNG and pickup parts do not block E20.f or E20.h.
2. **No data maps a global sprite index to a prefab.**
   - FACT[B/X]: `SpawnWarpEntity` takes a table index (global table or map table). The converter holds `bankKey → prefab id` only in memory (`SpriteWriter.cs:494`, `prefabAssetIdsByBankKey`), and nothing in `alundra-project/` records it.
   - The prefab id is `Ids.For("entity:alundra_N/<folder>")` (`SpriteWriter.cs:394`), and the folder name comes from `EntityNames.csv`, which is not shipped. The DLL cannot derive it.
   - So E20.e0 needs a converter change: a new `Data/global-sprites.json` (index → prefab id), a full export with manifest proof, and an ADR.
   - FACT[X]: 151 global indices are valid and 105 are invalid (`SpriteTable` entry −1, record null). Entry 256 is a sentinel.
   - All 22 hero projectile and magic indices are exported as prefabs. 88 of the 98 item sprites exist; items 42 and 90–97 have no sprite.
3. **Slot choice changes behaviour (O-E20-13).**
   - FACT[E]: the binary reuses the lowest free slot (1..63). An entity spawned during the second loop of the events pass runs its A program in the same pass only if its slot is above the slot count. If the slot is a recycled one at or below the count, A waits for the next tick (section 8).
   - The DLL appends. The order of every per-slot pass differs: events, counters, animation, physics, and the hit pass "first attacker wins". The order of RNG draws within a pass differs too.
   - Recommendation: port a slot index (hero 0, others 1..63) with first-free reuse and a 63-entity cap. The cap is a decision for the author (Q1).
4. **O-E20-11 is answered: a fresh entity takes part in the tick it is born in.**
   - FACT[E]: a spawn made inside MovePlayer or by a map event runs, in that very tick, the loop-1 status change to Normal, its A program, the counters, the lists, the animation and the physics. An arrow spawned by the hero at X = 120 moves 3 px (ForceX 196608) in its spawn tick and 6 px per tick after.
   - FACT[D]: the DLL gives a fresh entity its first tick next frame. `World.AddEntity` queues (`AlundraWorldProxy.cs:3119`), and `CollectTickEntities` keeps only `TookPartInFrame` entities (`:2622-2638`).
   - So E20.f arrow positions lag the oracle by one tick, 3 px at spawn. Either document it or make the engine integrate at once. The engine's `AddEntity` timing is UNK; a read of the engine's `World` would decide it (Q2).
5. **A record-less spawn must write `EntityRefId = -1`.**
   - FACT[B]: `0x80039D04` stores −1 at `+0x48` when the record pointer is null (`0x80039D70-0x80039D88`). The DLL's default `EntityRefId` is 0, so a hero arrow would answer `0x2C [0]` and `0x2E [0]`, which match by `EntityRefId`.
6. **The RNG advance is unconditional, once per call of the frame function `0x8002BAEC`.**
   - FACT[B]: the advance is at `0x8002BD0C-0x8002BD34`, after UpdateWorld, the menu trigger and `0x8004B1D4`. It runs even when `[0x800DC4B8] & 0x48` gates the entities, and never during the warp transition loop.
   - No code in the exe loads `0xB017xxxx`, so the state is the data-image value `0xB017C93D` at boot. The DLL's `Reset()` comment about "InitializeEngine" is a decompilation artefact.
   - FACT[D]: the DLL has no advance. `AlundraWorldProxy.cs:2766-2772` leaves the pass empty.
7. **The loot roll at creation also applies to every map-record spawn, so it shifts the RNG stream at every map load.**
   - FACT[B]: `0x80039F38` (end of InitializeEntity) calls `0x80032A40` for every spawn path.
   - FACT[X]: 4,697 of 9,631 enabled records carry a nonzero loot code (104 distinct codes; the top four are `0x82` ×2069, `0xFC` ×853, `0xFB` ×669, `0xFF` ×537).
   - FACT[D]: the DLL never writes `ContentsItemId` (`AlundraEntityScriptProxy.cs:80`), and the mapper ignores the record's `Contents` property (`EntityRecordMapper.cs:34-38`).
8. **One handler, C255, serves three populations.**
   - These are dropped loot (`Bytes[0]=1`), chest items (`Bytes[0]=2`), and map-placed items (`Bytes[0]=0`). The placed items are 153 records in 41 maps (item 83, the life vessel, ×16; item 74 ×17; item 85 ×10).
   - FACT[D]: they are inert today. `RunSpriteEvent` logs once and does nothing for code 255.
   - The pickup function `0x80033F00` is not the DLL's `HandleMapTriggerCommand` (the port of `0x80034108`). It differs in its return value and in "stock full means use now". The same work closes O-E19-39 for ids 0x50-0x56 (`AlundraEventProgramRunner.cs:3077`).
9. **Corrections to earlier reports.**
   - Lifetime: `byte[0x800C5F7C+8·item+4] != 0` gives 600 ticks, `== 0` gives −1 (never). The delay slot at `0x80032C38` makes this the opposite of the earlier reading.
   - `0x80032A40` has one caller, `0x80039F38`, which is the end of InitializeEntity. It is not "the load path, not decoded".
   - Fire and ice are C60/E9 (global sprites 0xB, 0xC, 0xE, 0xF), not C54. C54 (`0x80078E34`) is an enemy handler.
   - In the shipped data no map-local sprite uses E17, C60, C61, C94, E9 or E10 (section 11).
   - Stats `+4` is Mp and `+6` is MpMax. This is FACT, via `0x8004DE4C` (SetMpMax) and `0x8004DF10` (AddMp).
   - The decompilation stores `g_numberOfEntities = Index + 1`; the binary stores `Index` (the highest slot used) and its loops are inclusive.
10. **The break effect is not needed for loot.**
    - It is a separate field, header `+0x1E`. ADR-0055 D-E14-2 already schedules its export with E14.a, so share one full export.
11. **Blink.** The item's last 120 ticks use `+0x1CC` and the hide rule `(+0x1CC & 3) == 3` (D-E20-21, owned by E20.c2). Either E20.c2 comes first or the item slice implements the rule for all entities.
12. **Hearts write the hero entity's Hp at `+0x14`.** MovePlayer copies it to the stats at the end of the tick. Before E20.c2 the DLL's source of truth is `PlayerStats`, so use `SetPlayerHp` (`AlundraPlayerManager.cs:1015`) or order the pickup after E20.c2.

## 1. SpawnWarpEntity `0x80039F58`

FACT[B]: arguments are `a0` = parent, `a1` = isMap (0 = global table, ≠0 = map table), `a2` = sprite index, `a3` = X. On the caller stack at `sp+0x10`, `+0x14`, `+0x18` are Y, Z (16.16 binary convention) and direction (0, 8, 0x10 or 0x18, stored as TargetDirection). It returns the entity pointer, or 0.

| Step | Address | What |
|---|---|---|
| 1 | `0x80039F94` | `0x80039B28(isMap, idx, &palette, &sheet)`. A null result returns 0 before any slot is used. |
| 2 | `0x80039FA8` | `0x80039AD0` FindFreeEntitySlot. A null result returns 0. |
| 3 | `0x80039FBC-0x80039FC4` | speaker id = idx, plus `0x100` if `a1 != 0` |
| 4 | `0x80039FCC-0x8003A014` | calls `0x80039D04(slot, parent, spriteHeader, record=0, …)` with ref −1, X, Y, Z, anim 0, direction, palette, sheet |

`0x80039B28` (FACT[B]):
- `a1 = 0` uses the table block `0x80126EC0`, palette 0xB, sheet 0x60. `a1 ≠ 0` uses `0x80126E70`, palette 0, sheet 0x20.
- The block holds the pointer array at `+0xC` and the count at `+0x10`.
- A valid pointer is `0x80000000..0x801FFFFF`. Anything else, or an index ≥ count or negative, prints via `0x800815EC` (string `0x800239CC`) and returns 0.

## 2. Slot search and lifecycle

- **Table (FACT[B]).** 64 slots of 0x294 bytes from `0x80127D30`. Slot 0 is the hero.
- **Search (FACT[B/E]).** `0x80039AD0` returns the first slot in 1..63 with `+0x10 == 0`. A full table prints (string `0x800239B0`) and returns 0.
- **Real-code probes (FACT[E]):**
  - Spawns land in slots 1, 2, 3 in order.
  - After a slot is set to status 4, a spawn does not reuse it. It lands in slot 4.
  - After the slot pass `0x80038634`, slot 2 is status 0 with its index word restored, and the next spawn takes slot 2.
  - With the table full: the hero plus 63 entities, the 64th call returns 0, and each failure prints once.
  - An invalid index (−1 entry) and index 300 both return 0 and print once each.
- **Slot pass `0x80038634` (FACT[B]).**
  - It runs first in every entity pass, after the map events.
  - For each status-4 slot it copies the 0x294-byte template from `0x80134368` and rewrites `+0` = slot index.
  - It then sets `[0x80134360]` = highest non-free slot index. This is the index, not index + 1.
- **Template (INF, high).** It is BSS zeros except `+0x48 = -1`, written at `0x8003B21C`. A scan of stores found no other writer.
  - Consequences: free status 0, EventTrigger `+0x228` = 0, `EntityRefId` = −1.
  - UNK: only a RAM dump of `0x80134590` would prove `+0x228`.
- **Callers (FACT[B]).** FindFreeEntitySlot has two: SpawnWarpEntity and the record spawner `0x8003A1B8`.

## 3. InitializeEntity `0x80039D04`

Arguments (FACT[B]): `a0` = slot, `a1` = parent, `a2` = sprite header, `a3` = record pointer. Stack: id@0x40, ref@0x44, X, Y, Z, anim, dir, palette, sheet.

| Field | Value |
|---|---|
| `+0x00`, `[0x80134360]` | if `[0x80134360] < index`, it becomes `index` |
| `+0x04` | `++[0x80134600]` (global serial; the hero is 1 in the probes) |
| `+0x08` | `parent.+0x08` if nonzero, else `parent` (only if parent ≠ 0) |
| `+0x0C` | parent (always written, even null) |
| `+0x10` | 1 (Loaded) |
| `+0x44` | record pointer |
| `+0x48` | `-1` if record is null, else the stack ref |
| `+0x64` | sprite header |
| `+0x68` | id (`idx`, or `idx + 0x100` for the map table) |
| `+0x6C` | flags = `MoreFlags \| CanPickup<<8 \| FlagsPortraitShadowType<<16` (arrow: `0x12074`) |
| `+0x70..0x84` | A, B (0), C, D, E, F native codes from header `+0x13..0x17` |
| `+0x88/+0x8C` | TargetAnimation (0 for SpawnWarp), TargetDirection |
| `+0x90/+0x94` | the complements of `+0x88/+0x8C` |
| `+0x1B0/+0x1B4` | palette and sheet size |
| `+0x1C4` | balance sheet from `0x80044550(id)` |
| `+0x14` and `+0x18` | sheet byte `+2` (MaxHp) |
| `+0x230` | the entity itself (`0x8004201C`) |
| `+0x4C..` | with a record, `record[0xA..0xF]`, signed bytes; with none, template zeros |
| Body box | `0x80039C40` from header `+0x18..0x1D` |
| `+0x114/+0x118` | X, Y |
| `+0x11C` | `Z - bodyZ(+0x1EC) + 1` |
| First animation change | `0x80038AB4` |
| ModdedPos `+0x1D8..` | position plus body offsets |
| `+0x138` | `0x800370C4` ground height |
| PosZ raise | if `terrain + 1 >= PosZ`, then `PosZ = terrain + 1` |
| `0x80038064` | hit box and tile fields |
| `0x80032A40` | loot roll |

FACT[E]: an arrow spawned by `SpawnWarpEntity` with Z = 0 lands at PosZ = 1.

DLL status (FACT[D]):
- Mirrored by `ApplySpawnInitialization`: flags, program codes, body box, PosZ minus ModZ, ModdedPos, initial `~anim` (`AlundraEntitySpawnFactory.cs:571-706`).
- Missing: the slot and serial, `+0x08`, and `+0x48 = -1`.
- `ApplySpawnInitialization` needs a `TileMapObjectData` for three things: the PrefabAssetId, `ResolveSpriteType(record)` and `SpriteDirection` (`:564`, `:636`, `:693`). A record-less spawn needs a refactor.
- `ApplyRecord` sets `EventTrigger = ProgramUnknown` (`:529`), the documented next-frame start.

## 4. Parent link

- FACT[B/E]: for `parent = hero`, the arrow has `+0x0C` = slot 0 and `+0x08` = slot 0. With `a0 = 0` both are untouched, which reads as 0 from the template.
- FACT[B]: `0x8003A648` destroys, recursively, every active entity whose `+0x0C` is the destroyed one, before itself (callers: `0x8003A648` itself and handler E11).
- FACT[D]: the DLL has `ParentEntity` (`AlundraEntityScriptProxy.cs:68`) but no root field for `+0x08`. The record spawn passes `logicEntity.LogicContextEntity` (`AlundraWorldProxy.cs:3111`).

## 5. Sprite index space

- FACT[X]: `map_alundra.json` `SpriteInfo.SpriteRecords` is the global table, 257 entries.
  - `Header.Sector5Id` equals the array index for all 151 non-null records.
  - Bank key `alundra_<N>`, name = row N of `EntityNames.csv` (rows 0-255 are global, 256-511 are map-local).
  - Folder = sanitized name, plus `-alundra_N` if shared, or `bank_alundra_N` if unnamed. Prefab = `Entities/<folder>/<folder>.entity`.
- FACT[X]: `SpriteBankReader` keys map-local banks by `Sector5Id` alone, 244 banks, so 151 + 244 = 395 prefabs. `sprite-records.json` is keyed by prefab id, with no bank key.
- FACT[X]: a map record selects the table by `SpriteDirection & 0x80` (`EntityPrefabLinkWriter.cs`), and SpawnWarpEntity by `a1`. The map-load and 0x2D paths already carry `PrefabAssetId` on the record.
- Map-local spawns (`a1 = 1`) are 80 sites in enemy handlers (E14). They need a per-map `index → prefab` table and are out of E20.e0.

**Hero-related global sprites** (header A/C/D/E/F codes):

| idx | name | A/C/D/E/F | spawned at |
|---|---|---|---|
| 3 | Flèches Niv.1 | 0/0/0/17/0 | weapon shot, fiche 8 |
| 4 | Flèches Niv.2 | 0/0/0/17/0 | fiche 9 |
| 7 | Flèches chargées | 0/94/0/1/0 | fiche 9, charged |
| 0xB, 0xC | Boule de feu, chargée | 0/60/0/9/0 | fiche 4 |
| 0xE, 0xF | Boule de glace, chargée | 0/60/0/9/0 | fiche 5 |
| 0x1B-0x1D | Épée d'onde 1-3 | 0/61/0/10/0 | fiches 1-3, charged |
| 1 | Haricots de Jack | 0/73/0/21/0 | `0x80034284` (item use) |
| 2 | Bombe | 0/62/30/12/0 | `0x800345DC` (item use) |
| 0x10, 0x11 | Magie de terre 1, 2 | E3, E13 | `0x800353EC`, `0x8003567C` |
| 0x13 | Magie d'eau 2 | 254/75/0/0/0 | `0x80035874` |
| 0x14, 0x15 | Magie de feu 1, 2 | E1, E28 | `0x80035B0C`, `0x80035CEC` |
| 0x17 | Projectile de feu 1 | 254/63/0/3/0 | `0x80035BF4` |
| 0x16 | Magie de vent 1 | E1 | `0x80036054`, `0x80036134` |
| 0x18 | Magie de vent 2 (bas) | 254/65/0/1/0 | `0x800362A0` |
| 0x1E + item | items (88 with a record, 87 with C255) | 255/255/0/0/0 | loot, chests, placed |
| 0xEC | Nuage de sable | none | enemies C48, C51 |

FACT[X]: all header `Contents` values of the hero-spawned sprites are 0, so a hero spawn draws no RNG at creation (`0x80032A40` returns 0 for code 0, no draw). Nonzero global Contents exist only for 0xE6, 0xE8, 0xEA, 0xEE, 0xEF (255) and 0xFE (130).

**Weapon record table** (FACT[B], `0x80028C78`, 13 bytes per fiche: charge flag, then ground, air and charged blocks of anim/type/delay/Z). Object → fiche map at `0x80028C14`: object 2→1, 3→2, 4→3, 5→8, 6→9, 14→5, 16→4.

| Fiche | Ground type | Air type | Charged type |
|---|---|---|---|
| 1-3 | 0 | 0 | 27, 28, 29 (delay 20, Z 0) |
| 4 | 11 (delay 10, Z 32) | 11 | 12 |
| 5 | 14 | 14 | 15 |
| 8 | 3 (delay 8, Z 16) | 3 (delay 0, Z 16) | none |
| 9 | 4 (delay 8, Z 16) | 4 (delay 8, Z 16) | 7 (delay 8, Z 8) |

## 6. The 102 callers

FACT[B] (`callers.py`, function starts from jal targets and handler tables; output in `callers2.txt`):

| Group | Sites | Sprites |
|---|---|---|
| UpdateWeaponStepProgression `0x8002F768` | 1 (`0x8002F818`) | `ptr[1]`: 3, 4, 7, 0xB, 0xC, 0xE, 0xF, 0x1B-0x1D |
| Loot | 2 | item + 0x1E (`0x80032BEC` from `0x80032B90`; `0x80032CD8` from `0x80032C7C`) |
| Item use (E20.h) | 11 | 1, 2, 0x10, 0x11, 0x13, 0x14, 0x17, 0x15, 0x16 ×2, 0x18 |
| Enemy A handlers (map-local) | 7 | |
| Enemy C handlers (map-local) | 72 | |
| Enemy E handler (map-local) | 1 | |
| Helper of a C handler (map-local) | 1 | |
| Enemy C handlers, global | 5 | 0xEC ×4 (C48 ×3, C51), 0x19 (C54) |
| E20 (map bank 137, "Torche"), global | 1 | 0xC (the charged fireball) |
| F255 chest item, global | 1 | item + 0x1E |

Total 14 + 7 + 72 + 5 + 1 + 1 + 1 + 1 = 102. The "global" rows are the non-hero global sites.

The item-use sites are reached from `TryUseItem` (`0x8002ED64`): on a Circle edge it calls `0x8003499C` (beans `0x80034224`, bomb `0x8003453C`), and the driver `0x8003634C` calls the magic spawners. They belong to E20.h.

Other creation paths (FACT[B]): `0x8003A1B8` SpawnEntity(record), 10 callers (map load `0x8003B24C`, 0x2D `0x8003DCC4`, 0x8A, 0x8B, and handlers C95, C98, C102); `0x8003A048` re-init in place, 1 caller; hero creation `0x80031974`. `DestroyEntity(e, fx)` `0x8003A59C` has 38 callers, `0x8003A774` has 32.

## 7. RNG

- **Generator (FACT[B]).** State `0x80098708`; `s' = s·0x7D2B89DD + 0xE06A02E7 (mod 2^32)`; a draw of N is `(s'·N) >> 32`.
- **Draw sites.** 255 `lui 0x7d2b` sites in 93 functions.
- **Frame advance.** The 14-instruction advance at `0x8002BD0C-0x8002BD34` is the only unconditional one.
  - `0x8002BAEC(a0)` is called at `0x8002C3E4` (`a0 = 1`, once after load) and `0x8002C404` (`a0 = 0`, main loop; the loop does render, then `0x8002BAEC`, then vsync).
  - The warp transition loop (`0x8002C48C…`) calls neither.
  - Statically (jal-only), `0x8004B1D4` reaches no RNG function (INF).
- **DLL users (FACT[D]).**
  - `AlundraRandom.cs:21-35`: the state is an unmasked `ulong`; the low 32 bits are the binary's.
  - `AlundraEventProgramRunner.cs:625` (0x0C), `:1478` (0x8C), `:3382` and `:3385` (`ResolveDirectionFromParam` modes 4 and 5).
  - `AlundraNativeEffects.cs:52-53` (life vessel sparks, 2 draws each) and `:83-85` (hit sparks, 3 draws each).
  - `AlundraLibcRandom` is a separate stream.
- **Placement recommendation.** In `AlundraWorldProxy.Update`, after `_effectStage.RunFrame(...)` (`:2449`), advance N times, where N is the same tick count passed to `RunFrame` (0 during a warp departure). Do not gate on `GameplayGateNow`.
  - This keeps the order effect-draws then advance for 1-tick frames. For frames of 2 or more ticks the existing "all ticks, then effects" structure already differs, so that is a documented deviation.
  - Stage-level tests that call `stage.RunFrame` directly are unaffected (`AlundraNativeEffectTests.cs:347,352,488,506`). `ArcRun` resets the seed (`AlundraArcSupport.cs:327`).
  - After the advance exists, the c1 comparisons of spark speeds, excluded until now (plan §1.5), can be re-enabled.

## 8. Tick participation and slots

Probes (FACT[E]) use the real passes `0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364`.

| Spawn made | Result |
|---|---|
| Inside MovePlayer (before loop 1) | status Normal after the events pass; A ran (`AIValues[0] = 40 = PosZ>>16`); trigger −1; the arrow moves in that tick |
| By a loop-2 handler, new slot above the count | template trigger 0 is still there, so A runs in the spawning pass, status stays 1, and loop 1 of the next tick runs A again (a quirk) |
| By a loop-2 handler, recycled slot ≤ count | loop 1 had written −1, so A runs only next tick |

Loop structure (FACT[B], `0x800386D0`): MovePlayer first; loop 1 sets the trigger per slot (status 1 → A and status 2; free or destroyed → −1); loop 2 runs handlers and repeats while one ran. Both loops re-read `[0x80134360]` each iteration. The animation and physics passes iterate the list built after the events pass (statuses 2 and 3).

## 9. Loot

**Roll `0x80032A40`** (FACT[B], exercised on the real function):
- No record (`+0x44 = 0`): `+0x40 = 0`, code = sprite header `+0x1F`.
- With a record: flag = `hu[rec+0x10]`. If `(flag & 0x7FFF) >= 0x800`, the flag becomes 0. `+0x40 = flag`.
  - Flag 0: code = `rec[0x12]` if nonzero, else sprite `+0x1F`.
  - Flag ≠ 0 and not set: the same code. Flag set: code 0, no draw.
- Bank is selected by `0x8000`: set means the temporary bank `0x801EBA40`, clear the persistent bank `0x801EB344`. A flag set in the wrong bank counts as not set (probed).
- `+0x3C = ChooseRandomlyAnItem(code)`.

**`ChooseRandomlyAnItem` `0x80032968`** (FACT[B]):
- code ≥ 0x100 returns 0.
- If code & 0x80, one draw gives `idx = s' >> 28`, code = `byte[0x80028E0C + (code&0x7F)·16 + idx]`, and it repeats.
- Otherwise the result is code if < 0x62, else 0.
- Table: 128 rows × 16 bytes; only rows 118, 121, 125, 126 are referenced by other rows. The table is not exported (FACT[X]).
- Draw counts, probed: sprite `0xFC` gives 2 draws and item 69; sprite `0xFB` gives 1 draw and 0; a record with a direct item code gives no draw.

**Rows used by the cut grass** (`0x80032C7C(0xFF)`):

| Row | Contents | Probability |
|---|---|---|
| `0x7F` | 14 × 0, 253, 254 | 1/16 money, 1/16 hearts |
| `0x7D` | 69 ×9, 70 ×5, 71 ×2 | money split (of 16) |
| `0x7E` | 84 ×12, 85 ×4 | heart split (of 16) |

Row 2 (code `0x82`, 2069 records) is `84,84,84,85,85` then 11 zeros.

**Drop `0x80032B90`** (FACT[B/E]): `item = [e+0x3C]`; 0 returns 0. Veto `0x80032A00(item)` returns 1 for items 0x26, 0x51, 0x52 when `GetPlayerMpMax() == 0` (stats short at `+6`). Probed: item 81 with MpMax 0 gives return 0, no spawn, no sound.
- `SpawnWarpEntity(0, 0, item+0x1E, X, Y, Z, 0)`, using the dying entity's `+0x114/+0x118/+0x11C`. If it fails (null sprite), it returns 0 and the loot is lost.
- On the spawned item: `+0xB8 = 0xA0000`, `+0x274 = 1`, `+0x6C &= ~0x80` (Collidable), `+0x278 = (props[item].byte4 != 0) ? 600 : -1`, `+0x27C = 0`, `+0x280 = dying.+0x40`, `+0x284 = 0xA0000`, then sound 0x54. Returns 1.
- `0x80032C7C(code, X, Y, Z)` is the same but `+0x280 = 0`; its only caller is the tile cut `0x8003A988`.
- Callers of `0x80032B90`: `DestroyEntity(e,fx)`, `0x8003A648` and `0x800670F8` (a C handler). The 1-argument `0x8003A774` does not drop.

**`DestroyEntity(e, fx)` `0x8003A59C`** (FACT[B]):
1. SpawnEntityContents.
2. `+0x10 = 4`, `+0x228 = -1`.
3. Clear the active effect link (`+0x1B8`).
4. If `fx = -1`, use header `+0x1E`; a nonzero value creates the attached effect `0x8003BE74`.
5. Clear `platform.+0x2C`.

DLL (FACT[D]): `AlundraWorldProxy.cs:3022-3026` only sets `FlagToDestroy`. Recycling is at `:2990-3012`.

## 10. Item entity C255 `0x8007C174`

FACT[B/E] with Gravity 128 (map 10 constants); sprite id is `idx - 0x1E = item`. A[255] `0x80061BD4` is the Load program. A[254] `0x80061BCC` is a bare `jr ra`. A[0] `0x8006174C` writes `AIValues[0] = PosZ>>16`.

**Dropped loot (`Bytes[0] = 1`) timeline**, probed with tick 0 = the drop:

| Tick | Event |
|---|---|
| 1 | A255 returns at once; ForceZ 622592 |
| 1-20 | rise, ForceZ −32768 per tick (`Gravity<<8`) |
| 21 | ForceZ ≤ 0, so ItemState 0 → 1 (pickable from here) |
| ≈ 40 | first landing, bounce `0x78000` |
| 150 | rests (`onGround`, bounce 0) |
| 481 | lifetime reaches `0x78`, `+0x1CC = 0x78` (blink, hidden when `&3 == 3`) |
| 601 | lifetime 0, `0x8003A774` (no loot), status 4 |
| 602 | slot freed by the slot pass |

- Bounce: while on the ground with `+0x284 != 0`, each tick `+0x284 = +0x284·12 >> 4`; if it is ≤ `Gravity<<8`, it becomes 0. `ForceZ = +0x284`. Sequence: `0xA0000, 0x78000, 0x5A000, 0x43800, 0x32A00, 0x25F80, 0x1C7A0, 0x155B8, 0x1004A, 0xC037, 0x9029`, then 0.
- Lifetime: skipped for 0 or −1; otherwise decremented, destroyed at 0, blink set at `0x78`.
- State 1: wait while `0x80045004()` is nonzero (message flag `0x80152F08 & 4`). Test the 3-axis overlap of ModdedPos against the hero (hero size for d ≥ 0, item size for d < 0, each +1). Then call `0x80033F00(hero, item)`.
  - Text is empty when the return is 0 and any `Bytes[]` is nonzero. All loot items have byte4 = 1, so the return is 0 and there is no message.
  - Then `SetFlag(+0x280)`, then sound `props[item].byte5` (52, 53 or 54). If a sound exists the item is destroyed after the message (`ItemState += 2`); if not, the jingle path runs (CD streaming 0xB, block entities).
- Next tick: `ItemState` 3 → `0x80045004() == 0`, unblock, `0x8003A774`. So pickup at tick T, destroy at T+1, slot free at T+2.

**Item properties** (FACT[X] `item-drop-properties.json`; the converter writes bytes +4..+7 as `[lifetime flag, sound, field3, field4]`). The table is at `0x800C5F7C`, 8 bytes per item.
- Lifetime flag ≠ 0 (600 ticks) for items 36, 37, 38, 39, 41, 69-72, 81, 82, 84-86. Every other item lives forever.
- Sounds: 52 for items 33, 36-39, 41, 61, 79, 84-87; 53 for 69-72; 54 for 81, 82.
- DLL (FACT[D], `AlundraItemTables.cs:60-75`): keeps only field 3, so fields 1 and 2 are not read today.

**What `0x80033F00` gives** (FACT[E] for the starred ones, FACT[B] for the rest). It returns `props[item].byte4 == 0`.

| Item | Effect |
|---|---|
| 69, 70, 71, 72* | AddMoney 1, 5, 10, 30 |
| 84* | Hp += 2 (clamped to MaxHp), no effect object |
| 85* | Hp += max(MaxHp>>2, 1), one effect 0xE |
| 86* | Hp += max(MaxHp>>1, 5), four effects 0xE with random velocities |
| 81* | AddMp(1) + effects |
| 82* | Mp = MpMax + effects |
| 36, 37, 38, 39, 41 | AddOneItem; if the stock is full (`0x80033EC8` false), use it now |
| 79 | `0x8004E6EC(1)` (FalconTemp) |
| 80 | magic seed `0x8003382C`: MpMax + 1, Mp = MpMax, sound 0x36 |
| 83 | life vessel `0x800333AC` |
| Others | `AddOneItemIfUnlocked(0x8004E530)` |

Pickup effects (`0x8003BDD8`, effect id 0xE, `+0x5C = 0x20000`, velocities from draws) are only partly decoded. UNK: exact draw counts per kind; the decode of `0x80032EEC`, `0x80032F84`, `0x800335AC`, `0x800336F0`, `0x8003382C` decides.

**Chest item (`Bytes[0]=2`) and placed item (`Bytes[0]=0`) paths.**
- A255, placed: `Flags &= ~0x80`. If the flag at `+0x40` is nonzero and unset, the item destroys itself (1-arg). Otherwise `+0x27C = 0`, `+0x280 = -1`, `+0x284 = 0`, `+0x278 = flag`.
- The pickup then calls `SetFlag(-1)`, which sets bit 31 of word 0x3FF of the temporary bank. In the DLL, `IndexOf(0xFFFFFFFF)` and `BankFor` land in range, so passing it through works and is harmless.
- Placed items show a name message (`0x800450F0`) or the jingle. UNK: ETC strings 0x45, 0x46, 0x4C-0x4F and the `0x800450F0` flag semantics; reading them decides.
- The chest-item spawn is handler F255 (`0x8007FC88`, spawn at `0x8007FD30`). The DLL ports chests through opcodes (E19.l2), not through item entities.

## 11. Shared handlers: what the weapons need

FACT[X]: in the shipped data no map-local sprite uses E17, C60, E9, C61, E10 or C94. They are used only by global slots: hero sprites plus unnamed, unplaced slots 5 and 6 (E17), 0xA and 0xD (C60/E9).

Shared with enemies:

| Code | Shared with |
|---|---|
| E3 | 15 map banks and the global enemy projectiles 0x96-0x99; used by earth magic Lv1 and the fire projectile |
| E1 | 54 map banks (generic destroy, already ported as E0/E1 in `RunSpriteEvent`) |
| Sprite 0xC (charged fireball) | map bank 137 "Torche" (E20 spawns it) and 3 records on map 309 |
| Sprite 2 (bomb) | 1 record on map 362 |
| Sprite 0x10 (earth magic Lv1) | 4 records on map 388 |

Handlers E20.f needs (A0/A254 are no-ops or one write):
- E17 (arrows).
- C94 and E1 (charged arrow).
- C60 and E9 (fire and ice).
- C61 and E10 (wave swords).

What the weapons need from E20.e0: the spawn API, `EntityRefId = -1`, the parent link, a per-sprite-type native handler registry, the generic destroy, and, if arrows disappear on hit, `DestroyEntity(e, -1)` with or without the break effect.

In the DLL, `AnimationDirection` is an end-of-frame value (`AlundraFrameSyncPasses.cs:143`). The binary reads `+0x98` of the hero as the spawn direction index, so a mismatch is possible in multi-tick frames.

## 12. DLL today

| What | Where |
|---|---|
| 0x2D | `AlundraEventProgramRunner.cs:904`, helper `:2322-2335` |
| 0x8A | `:1199`, `:2337-2362` |
| 0x8B | `:1206`, `:2365-2385` |
| `SpawnEntityByRecordId` | `AlundraWorldProxy.cs:3082-3135` (`InitializeBalance` `:3116`, `AddEntity` `:3119`, `_spawnedEntities.Add` `:3120`, appearance sound `:3121`); interface `IEntityWorldContext.cs:54-59` |
| Map-load spawn | `AlundraWorldProxy.cs:887-897` |
| Effects | `AlundraNativeEffects` (reservoir operations, not entities) |
| Hero shot stub | `AlundraPlayerManager.cs:547-577` (`SpawnWeaponProjectile` only counts) |
| Natives | `RunSpriteEvent` `AlundraEventProgramRunner.cs:293`; save book, E0/E1 destroy; all else a counted no-op |
| Items | `HandleMapTriggerCommand` `:3051`, `UseLifeVessel` `:3097`, `AddMoney` `AlundraPlayerManager.cs:1371` |
| No cap | `_spawnedEntities` is unbounded |

## 13. Oracle proposal (extend the c1/c2 machine)

New folder: `docs/plan-e20-annexe/e20-e0/oracle/`. Tools are copied from c1 plus my `exp_spawn*.py`.

**Setup.**
- A full global table from `map_alundra.json` (151 records) installed with `bank.install`, with a pointer array in free RAM (`0x801EE000`, records from `0x801F0000`). The map table at `0x80126E70` is the same layout.
- Engine init reproduced: slot index words, template `+0x48 = -1`.
- Hooks: `0x800815EC/FC` (journal), message helpers `0x800450F0, 0x8004507C, 0x80049B7C, 0x8005A9E0`, MovePlayer `0x80031B50`.
- Nested spawns use `side_call` with a private stack.

**Scenarios.**

| ID | What | Compared |
|---|---|---|
| SP1 | Spawn of all 22 hero indices and 88 items: the field table of section 3 | every field |
| SP2 | Slot rules: first-free, status 4 not reusable, slot pass, table full, invalid and out-of-range index | slot, return value, error-print journal |
| SP3 | Loot roll for all 128 rows and all code/flag/bank combinations | item, `+0x40`, draw count, RNG word |
| SP4 | Drop: veto matrix, fields, sound, null-sprite failure | return, fields, sounds |
| SP5 | C255 lifecycle per item | per-tick trace to destroy |
| SP6 | Pickup stats for items 69-72, 84-86, 81, 82, 36-41 | money, Hp, Mp |
| SP7 | Tick participation: MovePlayer-spawn, loop-2 higher slot, loop-2 lower slot | per pass status, trigger, A effect, X |
| SP8 | RNG advance: run `0x8002BD0C` to its epilogue with a prepared stack and sentinel return, check one LCG step | state |

**Mutations** (`mutations.py`): append instead of first-free; reuse status 4; template trigger −1; count word +1 (decompilation loss); wrong flag bank; lifetime 600/−1 swapped; bounce 3/4 → 0.7; overlap on X/Y only; veto removed; advance missing or doubled; blink counter 0x77.

Run time is about 1 s per scenario. Outputs go to `out/` with a `manifest.txt`.

## 14. Tests (Alundra.Tests)

- `AlundraSpawnBySpriteIndexTests`: prefab resolution for all 151, failure for the 105, fields equal SP1, `EntityRefId = -1`, parent and root, `SpriteType`, direction and Z convention (Z − 1 convention of D-E19-59).
- `AlundraSlotModelTests`: SP2 against the oracle: reuse timing relative to `RecycleDestroyedEntities`, the cap of 63, pass order.
- `AlundraSpawnTickParticipationTests`: SP7, with the one-frame deviation pinned or removed.
- `AlundraLootRollTests`: SP3 values and draw counts; record-spawn roll at map load (stream shift pinned on a chain map).
- `AlundraRandomPerTickTests`: N advances for N ticks, 0 in a warp departure, none for stage-only calls.
- `AlundraDropAndPickupTests`: SP4-SP6 traces; placed-item path; blink counter.
- An arc on a real prefab: the hero fires an arrow (E20.f consumer) and the shot appears with the right sprite, position and direction.
- Existing impact: a pinned test that asserts "no ContentsItemId written" (none found) and arc tests reading draws after map load.

## 15. Proposed slicing and decisions

| Slice | Content | Needs |
|---|---|---|
| **E20.e0a** | converter export `Data/global-sprites.json` (+ report counter, full export, ADR); host API `SpawnEntityBySpriteIndex`; factory refactor; slot model; `EntityRefId = -1`; parent and root; loot roll at creation; RNG per-tick advance; wire `SpawnWeaponProjectile`; A0, A254 no-ops | nothing; unblocks E20.f and E20.h |
| E20.e0b | `DestroyEntity` drop and veto; tile-cut loot function | e0a, the RNG advance |
| E20.e0c | A255/C255 with the native registry (sprite-type gated, shared with E14.a); pickup via a port of `0x80033F00`; placed items | e0a, e0b; E20.c2 for hero Hp or `SetPlayerHp` |

Questions for the author:
- **Q1.** Port the 64-slot model, including the 63-entity cap, or keep an unbounded list with a slot index only?
- **Q2.** Accept the one-frame lag of freshly spawned entities, or ask for an engine change so that `AddEntity` integrates at once?
- **Q3.** One converter export for the global-sprite table and the BreakEffect byte, shared with E14.a?
- **Q4.** For placed items, the name message and the jingle: port them in e0c or defer?

## 16. UNKNOWN list

- Template `+0x228 = 0` (INF high): a RAM dump of `0x80134590` would prove it.
- Whether the engine can integrate an entity at once: reading its `World.AddEntity`/`Update` decides.
- ETC strings and `0x800450F0` semantics for placed items and chests.
- Pickup effect draw counts for hearts and crystals.
- Static check only for RNG between UpdateWorld and the advance (jal-only, `0x8004B1D4` depth 6).

## Artifacts (all under the scratch folder named at the top)

`tools/exp_spawn.py`, `tools/exp_spawn2.py`, `tools/loot_trials.py`, `tools/callers.py`, `tools/funcs.py`, `tools/refs.py`, `tools/d.py` (disassembler); `callers2.txt`, `loot_rows.json`, `item_drop_props.json`, `init_entity.dis`, `events_pass.dis`, `giveitem.dis`; `out/exp_spawn.txt`, `out/exp_spawn2_x1_x3.txt`, `out/loot_trials.txt`, `out/x4_pickups.txt`, `out/x4_lifecycle_gilder.txt`.
