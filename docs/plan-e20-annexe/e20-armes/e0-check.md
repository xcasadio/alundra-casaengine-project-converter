> Report returned by one agent of the read-only discovery workflow for E20.e0, E20.f and the dependencies of E20.h and E20.g (night of 2026-10-09), kept verbatim as evidence. The plan holds the reconciled facts.

# e0-check: adversarial check of the E20.e0 discovery

Labels: FACT[B] means decoded by me in ALUN_CD.EXE (France) with capstone. FACT[E] means I ran it on my copy of the e0-spawn MIPS machine. FACT[D] means DLL or engine source. FACT[X] means exported or extracted data. INF means inference. UNK means unknown, with what would decide it.

Nothing was built or written in the repository, and no git command changed anything. Everything I made is in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/e0-check/` (artifacts at the end).

## Things that change the plan

1. **The DLL can remove the one-tick lag itself; no engine change is needed (answers e0-spawn's Q2 at the code level).**
   - FACT[D] (engine):
     - `World.AddEntity` only queues the entity (`CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:168-173`).
     - `InternalAddEntities` runs at the head of the next `World.Update` (`:528`, `:774-802`).
     - The Alundra tick runs in `GameplayProxy.Update` at the end of `World.Update` (`:589-592`).
   - FACT[D] (DLL):
     - `AlundraEntityScriptProxy.InitializeWithWorld` is empty (`:1148-1150`).
     - `TookPartInFrame` is set only in the proxy's engine `Update` (`:1245`), and that flag is the only thing that keeps a fresh proxy out of `CollectTickEntities` (`AlundraWorldProxy.cs:2622-2638`).
   - INF: the DLL can include proxies created this frame in the remaining passes of the tick, following the binary rules (item 3 of the check). Rendering would start one frame later and the logic would match the binary.
   - This matters for E20.f. FACT[E] (my probe): a hero arrow is in the update and collision lists in its spawn tick, so it moves and can hit in that same tick.

2. **D-E20-18's premise is refuted. The hero's projectile handlers belong to E20.f, and E20.f needs two e0 pieces first.**
   - FACT[X]:
     - No map-local sprite in the shipped data uses E17, C60, E9, C61, E10 or C94.
     - C54 is used only by map bank 190 (an enemy).
     - C60/E9 are used only by global sprites 0xA-0xF. Their non-hero users are the torch's E20 handler (`0x8007F6C8`, which spawns global 0xC at `0x8007F730`, FACT[B]) and three records on map 309.
   - FACT[B]: the handlers that E20.f needs reach two things e0 must supply.
     - **DestroyEntity(e, fx).** It is called by E1 `0x8007ED10` (fx −1), E17 `0x8007F610` (fx −1) and E9 `0x8007EFA0`/`0x8007F10C` (fx 6) and `0x8007EFB8` (fx −1).
     - **The RNG.** E9 draws at `0x8007F02C` and `0x8007F120`.
   - So the break effect (the charged arrow, sprite 7, has BreakEffect 8) and the per-tick RNG advance must land before E20.f, not in a late e0b.
   - FACT[D]: the effect creator `0x8003BE74` already exists in the DLL (`AlundraEffectReservoir.cs:203`).

3. **The drop must not ship before the item handler C255.** e0-spawn's e0b, which has the drop without C255, would create inert, permanent items.
   - FACT[B]: `0x8003A59C` always calls the drop `0x80032B90`. The pickup's lifetime, bounce and destruction all live in C255 `0x8007C174`.
   - FACT[D]: the DLL already destroys entities through E0/E1 (`AlundraEventProgramRunner.cs:315-322`).
   - Recommendation: ship the drop, the veto, `0x80032C7C` and C255 together.

4. **Template `+0x228 = 0` is now proven statically (it was UNK in the report).**
   - FACT[B]: the entry point `0x8008B538` zero-fills `0x800CA0E8..0x801F7F20`, which contains the template at `0x80134368`.
   - FACT[B]: the only direct store into `0x80134368..0x801345FC` is `0x8003B21C`, which writes `+0x48 = -1`.
   - FACT[B]: map load `0x8003B24C` copies the template into all 64 slots.
   - Indirect pointer stores are not excluded.

5. **One converter export can serve both pages of sprites, and the loot table needs no export.**
   - FACT[X]: map-local banks are deduplicated by `Sector5Id` alone, across all maps (`SpriteBankReader.cs:174-217`, `:321-340`). So "map table index → prefab" is a single 256-entry table, not one table per map (e0-spawn §5 says per map).
   - FACT[X]: `Data/sprite-records.json` has 395 entries keyed by prefab id. Each entry carries `Contents` (header `+0x1F`) but neither the bank key nor `BreakEffect`.
   - Recommendation: one export adds `BankKey` and `BreakEffect` per entry. That covers E20.f, the 81 map-local sites of E14 and D-E14-2.
   - The loot table (`0x80028E0C`, 128 × 16 bytes) can be copied as C# data, following the `AlundraWeaponTables` precedent (`AlundraWeaponTables.cs:4-16`).

6. **The RNG placement is exact in the arcs and approximate in-game.**
   - FACT[D]: ArcRun runs one tick per `Update(0.02f)` (`AlundraArcSupport.cs:69`, `:376-392`). There, "advance N times after `_effectStage.RunFrame`" (`AlundraWorldProxy.cs:2449`) is exact.
   - INF: in frames of two or more ticks, entity draws in the later ticks (AI, loot rolls, `0x0C`, `0x8C`) are also shifted, not only the effect draws. The deviation note must say so.

7. **Smaller population facts.**
   - The binary spawns at load by record byte 5 bit `0x40` plus the zone test, not by `IsEnabled`. FACT[X]: 3,729 of 7,240 such records have a nonzero effective loot code (60 distinct codes; `0x82` ×2014, `0xFC` ×575, `0xFF` ×480, `0xFB` ×216).
   - FACT[X]: at most about 39 entities spawn at load (map 11), so the 63-slot cap is reached only through dynamic spawns.

## (a) Check table

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| S1 | SpawnWarpEntity: sprite lookup before the slot; id = idx (+0x100 if map); ref −1; anim 0; Y/Z/dir from caller `sp+0x10..0x18` | CONFIRMED | `0x80039F94-0x8003A018` |
| S2 | `0x80039B28`: global `0x80126EC0` (palette 0xB, sheet 0x60), map `0x80126E70` (palette 0, sheet 0x20); bounds check; valid range `0x80000000..0x801FFFFF`; error string | CONFIRMED | `0x80039B28-0x80039BCC`; string `0x800239CC` is "Illegal charcter Race!!" |
| S3 | FindFreeEntitySlot: first slot in 1..63 with `+0x10 == 0`; message when full | CONFIRMED | `0x80039AD0-0x80039B20`, starts at `0x80127FC4`; string `0x800239B0` is "Charcter over execute!!" |
| S4 | FindFreeEntitySlot has two callers | CONFIRMED | `0x80039FA8`, `0x8003A29C` |
| S5 | A status-4 slot is not reused before the slot pass | CONFIRMED | the search tests only `+0x10 == 0` |
| S6 | Slot pass: copies 0x294 bytes from `0x80134368`, writes the index, count = highest slot in use | CONFIRMED | `0x80038634-0x800386CC`. It covers slots 0..63; "in use" means status not 0 and not 4 after the reset |
| S7 | Template = BSS zeros plus `+0x48 = -1` (INF in the report) | CONFIRMED (static) | Things #4 |
| S8 | InitializeEntity field table (§3) | CONFIRMED | `0x80039D04-0x80039F54`: count raise, `+0x0C` always written, root `+0x08`, `+0x48`, serial `++[0x80134600]`, flags `hu[0x10] \| b[0x12]<<16`, codes `+0x13..0x17` (B=0), `+0x88..0x94`, `+0x1B0/+0x1B4`, `+0x1C4/+0x14/+0x18`, body box `0x80039C40`, `Z - bodyZ + 1`, `0x80038AB4`, ModdedPos, terrain raise, `0x80038064`, `0x80032A40` |
| S9 | `+0x4C..` = `record[0xA..0xF]` | CONFIRMED (location refined) | written by `0x8004201C`, which also sets `+0x230 = e`; called from `0x80039E48` |
| S10 | Arrow flags `0x12074` | CONFIRMED | header bytes 116, 32, 1 |
| S11 | Creation paths: hero, SpawnWarp, record spawn, re-init `0x8003A048` | PARTLY REFUTED | 5 callers of `0x80039D04`. Missed: `0x8003AC78` inside `0x8003ABCC` (re-init from the record's home tile; caller `0x8006B82C`, an E14 handler). It re-rolls the loot, sets status 1 again and takes a new serial |
| S12 | 102 sites: 1 / 2 / 11 / 88 | CONFIRMED | jal scan. Headline "80 map-local" is really 81: `0x80062280` passes a1 = 1 with index = own id − 0x100. 80 + 1 + 7 global = 88 |
| S13 | Hero shot | CONFIRMED | `0x8002F7D4-0x8002F81C`: (hero, 0, `ptr[1]`, X, Y, Z + `ptr[3]<<16`, `{0,0x10,8,0x18}[+0x98]` at `0x80023644`). Return value unused. Only caller `0x80031D70`, inside MovePlayer |
| S14 | Recursive destroy `0x8003A648`; callers itself and E11 | CONFIRMED | children over slots 0..count with status 1-3 and `+0x0C == e`; E11 = `0x8007F25C` (contains `0x8007F264`) |
| S15 | Global table: 257 entries, 151 valid, 105 invalid, entry 256 is a sentinel | CONFIRMED | 105 entries are −1; entry 256 is 0; `Sector5Id == index` for all 151 |
| S16 | Header codes of the hero sprites (§5 table) | CONFIRMED | every row. Also unplaced: 5, 6 (E17) and 0xA, 0xD (C60/E9) |
| S17 | Hero sprites have Contents 0; nonzero global Contents only at 0xE6, 0xE8, 0xEA, 0xEE, 0xEF (255) and 0xFE (130) | CONFIRMED | FACT[X] |
| S18 | Items: 88 with a sprite (42 and 90-97 missing); 87 with C255 | CONFIRMED | also: sprite 0x1E (item 0) is the chest (A255/F255); item 89's record has all codes 0 |
| S19 | Placed items: 153 in 41 maps; 74 ×17, 83 ×16, 85 ×10 | CONFIRMED | FACT[X] (criterion `IsEnabled`) |
| S20 | 4,697 of 9,631 records carry a loot code; 104 distinct; top four | CONFIRMED arithmetic, wrong population | the binary criterion gives Things #7. The raw `+0x12` byte alone is nonzero on only 497 records; the rest comes from header `+0x1F` |
| S21 | No map-local user of E17/C60/C61/C94/E9/E10; C54 is an enemy handler | CONFIRMED | C54 = `0x80078E34`, map bank 190 only |
| S22 | E1 on 54 map banks; E3 on 15 map banks plus 0x96-0x99 | CONFIRMED | FACT[X] |
| S23 | Placed global sprites: 0xC on map 309 ×3, sprite 2 on 362 ×1, 0x10 on 388 ×4; E20 spawns 0xC | CONFIRMED | FACT[X]; `0x8007F730` |
| T1 | Events pass loop 1 | CONFIRMED | `0x800386F4-0x800388B0`, jump table `0x800237B4`: status 1 gives trigger 0 and status 2; status 2 picks a trigger in 2..5; status 3 gives 4; 0, 4, ≥5 or `+0x20 ≠ 0` give −1. Slots 1..count inclusive |
| T2 | Loop 2 repeats while any program ran and re-reads the count | CONFIRMED | `0x800388C8-0x80038968`. Record byte `(+0x4C+4t) & 0x7F ≠ 0` runs the interpreter `0x8004205C`; otherwise the native `[[0x80098F4C+4t]+4·code]` |
| T3 | A spawn inside MovePlayer runs A in the same tick | CONFIRMED | FACT[E] re-run X1: slot 1, status 2 after events, `+0x280 = 40` |
| T4 | Loop-2 spawn: higher slot runs A in the same pass; recycled lower slot waits a tick | CONFIRMED | FACT[E] re-run X2: template trigger 0 vs −1. Exact rule: a free slot that loop 1 did not visit this tick still holds trigger 0 |
| T5 | Arrow moves 3 px in its spawn tick, then 6 px per tick | CONFIRMED (no artifact backed it) | FACT[E] `chk_motion.py`: dir 0x18 gives X 150→153→159→165; dir 0x10 gives Y 100→98→94→90 |
| T6 | Animation and physics use lists built after the events pass | CONFIRMED, refined | `0x800384F4`: update list = status 2/3 and `+0x20 == 0`; collision list = flag 0x80, `+0xB4 & 0x80 == 0`, `+0x28 == 0`, **any status**; draw list = status 2/3 and `(+0x1CC & 3) ≠ 3` |
| T7 | Pass order and the 0x48 gate | CONFIRMED | `0x8003B388-0x8003B408`: slots, events, counters, lists, `0x80038E18`, `0x80038364`, `0x80038E84`, `0x80039300`. Gate `[0x800DC4B8] & 0x48` leaves only the lists |
| T8 | Decompilation stores count = Index + 1; the binary stores Index with inclusive loops | CONFIRMED | `EntityManager.cs:50`, `:969` |
| R1 | Generator, state `0x80098708`, draw `(s'·N) >> 32` | CONFIRMED | `0x8002BD0C-0x8002BD34`, `0x80032984-0x800329CC` |
| R2 | Unconditional advance after `0x8002E058` and `0x8004B1D4` | CONFIRMED | `0x8002BC4C`, `0x8002BD04-0x8002BD34` |
| R3 | Callers `0x8002C3E4` (a0 = 1) and `0x8002C404` (a0 = 0); the transition loop calls neither | CONFIRMED | loop `0x8002C490-0x8002C4C0`. Note: the post-load call also runs a full `0x8002E058` |
| R4 | No reseed; initial state `0xB017C93D` | PLAUSIBLE | no `lui 0xB017` anywhere; the image word is `0xB017C93D`; 332 stores to the state all sit in draw code (heuristic; 2 of 13 outliers checked by hand) |
| R5 | 255 `lui 0x7d2b` sites | CONFIRMED | jal/lui scan |
| R6 | DLL has no advance; `AlundraRandom` state is a ulong whose low 32 bits match; users listed | CONFIRMED | `AlundraRandom.cs`, `AlundraEventProgramRunner.cs:625,1478,3382,3385`, `AlundraNativeEffects.cs:52-53,83-85` |
| R7 | Placement after `RunFrame` with N ticks | CONFIRMED for 1-tick frames | Things #6 |
| L1 | Roll `0x80032A40` | CONFIRMED | flag threshold `0x800`; bank `0x801EBA40` if `0x8000`, else `0x801EB344`; a set flag gives code 0 and no draw; no record gives header `+0x1F` |
| L2 | ChooseRandomlyAnItem `0x80032968` | CONFIRMED | ≥ 0x100 gives 0; `0x80` bit draws 16, table `0x80028E0C`; < 0x62 is returned as is |
| L3 | Rows 118/121/125/126 referenced; rows 0x7F, 0x7D, 0x7E, 2 | CONFIRMED | 13 nonzero rows; direct items {36, 37, 41, 69-72, 79-86} |
| L4 | "Sprite 0xFC gives 2 draws, 0xFB gives 1" | CONFIRMED as a sample, mislabelled | these are **codes**. Both rows give 1 or 2 draws depending on the first draw (0xFC: 11/16 give 0 after one draw) |
| L5 | Drop `0x80032B90` fields; veto; lifetime polarity | CONFIRMED | `0x80032BA4-0x80032C60`. Veto `0x80032A00` for items 0x26/0x51/0x52 when `0x8004DDDC` (lh `+6`) == 0. Delay slot `0x80032C38`: byte ≠ 0 gives 600 |
| L6 | `0x80032C7C(code, X, Y, Z)` | CONFIRMED | draws first, then the veto; `+0x280 = 0`; only call is `0x8003A988` with code 0xFF |
| L7 | Callers of the drop | CONFIRMED | `0x8003A5B0`, `0x8003A6D0`, `0x800670F8` |
| L8 | DestroyEntity steps; `0x8003A774` does not drop | CONFIRMED | `0x8003A59C-0x8003A640`: the last step clears `[+0x28].+0x2C`. `0x8003A774` sets status 4 and trigger −1 and clears the links |
| L9 | Item property bytes (lifetime flag, sound) | CONFIRMED in the binary | `0x800C5F7C` byte 4 ≠ 0 for 36-39, 41, 69-72, 81, 82, 84-86; sounds match the export |
| I1 | Handler addresses A0/A254/A255/C255/F255 | CONFIRMED | A0 `0x8006174C` writes halfword `+0x280` = `+0x11E` (PosZ high). A255 also: chest id 0x1E with `+0x3C == 0` sets `+0x88 = 1`, so native chests read the creation roll |
| I2 | C255 rules | CONFIRMED (static) | bounce only on ground (`+0x144`); `+0x284·12 >> 4`, zeroed if ≤ `Gravity<<8`; lifetime 0/−1 skipped; destroy at 0; `+0x1CC = 0x78` at 0x78; states 0..3; 3-axis overlap with +1. The per-tick timeline is UNVERIFIABLE here (I did not re-run X4) |
| I3 | Pickup `0x80033F00` | CONFIRMED | table `0x800230D4` for item < 0x57, default `0x8004E530`; returns `props.byte4 == 0`. Item 84: `+0x14 = min(+0x14 + 2, +0x18)` (`0x80032E2C`) |
| I4 | `0x80034108` is a different function | CONFIRMED | own table `0x80023234`; none of the "use now" calls `0x80032E50/0x80033274/0x80033A2C/0x800330FC` |
| I5 | "No sound means the jingle path" | REFINED | jingle only if `[0x80029BCC]` or `[0x8009A858]` ≠ 0; otherwise ETC string 0x46 is appended and state += 2 |
| X1 | `+4` = Mp, `+6` = MpMax | CONFIRMED, naming wrong | `0x8004DE4C` is AddMpMax (calls SetMpMax `0x8004DDF4`, cap < 5); `0x8004DEBC` SetMp; `0x8004DF10` AddMp; `0x8004DF3C` DecreaseMp |
| D1 | DLL file:line facts in §3, §7, §12 | CONFIRMED | `AlundraWorldProxy.cs:2449`, `2622-2638`, `2770`, `3022-3026`, `2990-3012`, `3082-3135` (AddEntity `3119`, Add `3120`), map load `887-898`; factory `529`, `565`, `636`, `693`; runner `293`, `904`, `1199`, `1206`, `3051`, `3077`, `3097`; `AlundraPlayerManager.cs:567-575`, `1015`, `1371`; `AlundraArcSupport.cs:327` |
| D2 | `ContentsItemId` never written; the mapper ignores `Contents` | CONFIRMED | only the declaration `:80` and a snapshot `:2413`; `_10` becomes `ContentsGameFlag` with the same threshold (`EntityRecordMapper.cs:170-175`) |
| D3 | No slot index and no root in the DLL | CONFIRMED | `Index` is only copied into a snapshot (`AlundraEntityScriptProxy.cs:2398`) |
| D4 | The prefab id cannot be derived without new data | CONFIRMED | `Ids.For("entity:{BankKey}/{folder}")` (`SpriteWriter.cs:394`); `EntityNames.csv` is only in `alundra-datas-analyser`, not in `alundra-project/` |
| O1 | The oracle extension is feasible | CONFIRMED | FACT[E]: my copies ran X1, X2 and the motion probe in about one minute each |

## (b) Corrections to the reports

1. The template trigger: UNK becomes a static FACT (Things #4). The SP-scenario oracles may keep "template = zeros, `+0x48 = -1`" as a proven setup.
2. There is a fifth InitializeEntity caller (`0x8003AC78` in `0x8003ABCC`). The spawn API should expose "re-initialize in place" for E14.
3. Map-local index → prefab is one global table, not one per map (Things #5).
4. The site count is 81 map-local plus 7 global non-hero, not 80 plus 7.
5. The load-time loot population uses byte 5 bit `0x40` plus the zone: 3,729 of 7,240 records (Things #7). The 4,697 figure counts records the binary does not spawn at load.
6. "Sprite 0xFC/0xFB" means loot **code** 0xFC/0xFB; the draw count depends on the data (L4).
7. `0x8004DE4C` is AddMpMax, not SetMpMax.
8. C255 without a sound: the jingle depends on `[0x80029BCC]` and `[0x8009A858]`; otherwise message 0x46.
9. A loop-2 spawn (status 1) is not in the update or draw lists in its spawn tick, but it is in the collision list when its flag `0x80` is set (`0x800384F4`).
10. Test-means and the plan: "fire C54" does not exist. Fire and ice are C60/E9, and C54 is the enemy handler of map bank 190.
11. Native chests (sprite 0x1E, A255/F255) depend on the creation roll (`+0x3C`). The roll is not only for loot.
12. e0-spawn's slicing (drop in e0b, C255 in e0c) is unsafe (Things #3). Its claim that weapons need only the spawn API is incomplete: E20.f also needs DestroyEntity's break effect and the RNG advance (Things #2).

## (c) Recommended design for E20.e0

Order under D-E20-24: **e0a → e0b → E20.f → (E20.h) → e0c** (e0c can wait for E20.e or E14.a). One caveat for the main session: plan §1.1 says E20.f needs E20.e. If that stays, E20.e's grass loot pulls e0c ahead of E20.f.

### E20.e0a: spawn by sprite index (converter plus DLL)

**Tasks**

1. Converter. Add `BankKey` (`alundra_N` or `N`) and `BreakEffect` (header `+0x1E`) to each entry of `Data/sprite-records.json`, plus report counters. Run the full in-place export, prove it with the manifest and a double export (bit-identical), and write a format ADR. This also satisfies D-E14-2 early (needs the author's agreement, Q3).
2. DLL catalog. Build `(page, index) → prefab` for page 0 (global, 151 entries) and page 1 (map banks, 244 entries).
3. Host API `SpawnEntityBySpriteIndex(parent, isMap, index, x, y, z, dir)`, a port of `0x80039F58` and the record-less half of `0x80039D04`.
   - It returns null for an invalid or out-of-range index, and for a full table, without using a slot.
   - It sets the S8 fields: id plus 0x100, flags, codes, body box, `Z - bodyZ + 1`, terrain raise, TargetDirection = dir, anim 0.
   - It sets `EntityRefId = -1`, `ParentEntity` and the root (`+0x08`), all `ProgramIndexes = 0`, and the serial.
   - This needs the factory refactor away from `TileMapObjectData` (`:565`, `:636`, `:693`).
4. Slot model (Q1).
   - Hero in slot 0, others in 1..63, first free slot reused. A `FlagToDestroy` slot is reusable only after `RecycleDestroyedEntities`. Full table returns null with one log line.
   - Per-slot passes iterate by slot index (`CollectTickEntities`, hit pass). Re-measure the pins.
5. Same-tick participation (Q2, DLL side).
   - Fresh proxies take part in the rest of the current tick.
   - A spawn by MovePlayer or a map event runs A, the counters, the lists and the physics in that tick.
   - A loop-2 spawn runs A in the same pass only if loop 1 did not visit its slot; it stays Loaded and runs A again next tick.
6. Native registry by sprite type (D-E14-1), seeded with A0 (`+0x280 ← PosZ >> 16`) and A254 (no-op).
7. Wire `SpawnWeaponProjectile` (`AlundraPlayerManager.cs:573`) to the API.

**Oracle** (`docs/plan-e20-annexe/e20-e0/oracle/`, copied from c1 plus `exp_spawn*.py`)

- SP1: fields for global indices 1, 2, 3, 4, 7, 0xB, 0xC, 0xE, 0xF, 0x10, 0x11, 0x13-0x18, 0x1B-0x1D, plus 3 items.
- SP2: slot rules.
- SP7: participation, including the motion trace above.

**Acceptance**

- Fields equal SP1.
- Slot sequences and the null return equal SP2.
- Participation equals SP7: the arrow is at X + 3 px at the end of its spawn tick, then +6 px per tick.
- Arc: the bow shot creates a `alundra_3` entity with parent = hero, `EntityRefId -1`, and position and direction as in AT9 (tick 10, x 212 px, y 96 px, z 1048577, dir 24, from test-means). No `0x2C [0]` match.
- Full suite green, with only explained pin moves.

**Stops**

- The manifest changes beyond the two new fields.
- Item 5 needs an engine change.
- A pin moves without a slot-order explanation.

### E20.e0b: RNG stream and destroy effects (before E20.f)

**Tasks**

1. Per-tick advance after `RunFrame`, N = the tick count passed to it (0 during a warp departure, not gated by `GameplayGateNow`), with the deviation for frames of two or more ticks written out.
2. Creation roll `0x80032A40` and `0x80032968` on every spawn path (map load, `0x2D`, `0x8A`, `0x8B`, SpawnWarp). The loot table is copied as data from `0x80028E0C`. The roll writes `ContentsItemId` and the flag; nothing drops yet.
3. `DestroyEntity(e, fx)`: status, trigger, `+0x1B8` link, the break effect through the existing `0x8003BE74` creator (fx −1 reads `BreakEffect`), the `[+0x28].+0x2C` clear, and the child destroy `0x8003A648`. **No drop.**
4. One re-pin pass for the arcs that depend on RNG draws. Re-enable the c1 spark-speed comparisons.

**Oracle**: SP3 (all 128 rows × flag × bank: item, `+0x40`, draws, state), SP8 (advance), destroy fields and effect journal.

**Acceptance**: oracle equality; N advances for N ticks; 0 during a warp; the c1 sparks match.

**Stops**: pins move without an RNG explanation.

### E20.e0c: loot items (after E20.f, with E20.e or E14.a)

**Tasks**

- The drop `0x80032B90` with its veto, and `0x80032C7C`, shipped together with A255/C255 for the three populations of `+0x274`.
- Pickup: a port of `0x80033F00`, which also covers O-E19-39 for 0x50-0x56 where the cases overlap.
- Hero Hp through `SetPlayerHp` until E20.c2.
- The blink rule `(+0x1CC & 3) == 3` in the draw predicate, for all entities (D-E20-21).

**Oracle**: SP4-SP6.

**Acceptance**: the per-tick C255 trace equals the oracle (rise, bounces, destroy at 601, slot freed at 602) and the stats after pickup match.

**Stops**: the messages and jingle of placed items (Q5).

## (d) Questions for the author

Each has a recommendation, usable under "fait toutes les recommandations" if the main session judges it covered.

- **Q1 (slot cap).** Port the 63-slot cap? Recommended: yes, with a log line. It never triggers at map load (at most about 39 entities), and D-E20-11 already follows the binary's slot order.
- **Q2 (fresh entities).** Should newly spawned entities act in the tick they are born (logic as in the original, drawing one frame later), instead of today's one-tick lag? Recommended: yes, done in the DLL with no engine change.
- **Q3 (export timing).** Move D-E14-2 (the BreakEffect export) from E14.a into E20.e0a, in the same full export as `BankKey`, with one format ADR? Recommended: yes. E20.f's charged arrow needs it before E14.a.
- **Q4 (D-E20-18).** Its premise is refuted: no enemy uses E17 or C60/E9, and "fire C54" is an enemy-only handler. Move E17 and C60/E9 into E20.f? Recommended: yes. C54 stays in E14.
- **Q5 (placed items).** Port the name message and the jingle of map-placed items in e0c, or defer them? Recommended: defer. They need ETC strings 0x45/0x46/0x4C-0x4F and the `0x800450F0` flag semantics, which are still UNK.

## UNKNOWN that remains

- Whether anything reseeds the RNG through an indirect store. A RAM watch of `0x80098708` in an emulator run would decide it.
- The C255 per-tick timeline: I did not re-run it. The e0-spawn output `out/x4_*.txt` exists, and the SP5 oracle in e0c decides it.
- Whether proxy state other than `TookPartInFrame` (collision components) is needed before engine integration for Q2. Task 5 of e0a decides it with a probe arc.

## Artifacts (e0-check scratch folder)

- Scripts I wrote: `mdis.py` (disassembler), `scan.py`, `stores.py`, `rngstores.py`, `globals.py`, `census.py`, `census2.py`, `capcheck.py`, `handlers.py`, `sites.py`; `c255.dis` is a disassembly dump.
- `tools/` holds copies of e0-spawn's tools plus my `tools/chk_motion.py`.
- Outputs: `run_x1x2.txt`, `run_motion.txt`.
