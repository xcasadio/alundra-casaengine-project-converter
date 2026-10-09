> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E14.a), kept verbatim as evidence. The plan holds the reconciled facts.

# E14.a discovery check (label `e14a-check`)

This is an adversarial check of the three read-only reports `e14a-muruta`, `e14a-beannoide` and `e14a-dll`. I re-derived every decision-carrying fact myself from:
- the binary (`ALUN_CD.EXE` France, capstone);
- the map and export data;
- the DLL source.

Nothing in the repository was written, built or run. My scratch folder is `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/e14a-check/`. It holds `xd.py` (disassembler and dumper), `full.py` → `all.txt` (full disassembly), `scan_handlers.py`, `scan2.py` and `scan_break.py` (map scans).

Labels: **FACT** (binary address or file:line), **INFERENCE**, **UNKNOWN**. "Decomp" means `alundra-datas-analyser/AlundraTools/AlundraEngine/` (read only).

---

## Things that change the plan

1. **The relative-position helper needs a Z value per enemy that the DLL does not keep. This is a hard prerequisite.**
   - FACT: `0x8007FE20` reads `+0x134` (FloorHeight) for both entities (`0x8007FE38`, `0x8007FE3C`).
   - FACT: for an entity that is not Collidable, `GetCollisionOnZ` `0x80037F28` gives `FloorHeight = TerrainHeight(+0x138) + 1`. The Muruta and the Beannoïde both have `+0x6C & 0x80 = 0`, so they take this path (test at `0x80037F2C-0x80037F38`).
   - FACT: the DLL writes `FloorHeight` only for the hero (`AlundraEntityScriptProxy.cs:1999`). For enemies it stays at 0.
   - INFERENCE: the bounds are 0x100000 (Muruta, `0x800681EC`) and 0x200000 (Beannoïde, `0x80065F60`). With FloorHeight 0 on enemies, |dz| is the hero's own floor height, which exceeds those bounds on any raised ground. The Muruta would then never alert and the Beannoïde would never lunge. The hall Muruta stand at Height 16 and the grate Beannoïde at Height 20.
   - The `e14a-dll` report says these helpers read TerrainHeight. That repeats the decomp's error (see C1 below).

2. **`HandleAnimationDirection` needs the four per-corner heights, which the DLL never stores.**
   - FACT: the helper reads `+0x158..+0x164` (`0x8008014C-0x80080164`). The binary writes them in `ComputeEntityGroundHeight` `0x800370C4` (store at `0x80037334`), called at entity init (`0x80039EE0`) and from the physics routines.
   - FACT: the DLL's `ComputeTerrainHeight` already samples the four corners (`AlundraEntityScriptProxy.cs:1747-1750`) but keeps only the highest.
   - INFERENCE: with MapHeights all 0, any Muruta above 64 px gets "turn" on every tick of state 12. It would never walk.

3. **Dispatch must be gated by sprite type (or by the full header handler tuple). The case for this is stronger than `e14a-dll` stated.**
   - FACT (my scan of all `map_N.json` headers):
     - Header (A,C,D)=(0,10,18) uses C10 with D18. It is on map 329: records 4, 11, 12 (scripted C) and 19, 20 (native C from the start, scripted A 148). It is also loaded on maps 153 and 368 with no static records.
     - Header (0,1,23) uses C1 with D23. It is loaded on maps 99, 123 and 449 with no static records.
     - Header (1,26,1) (Slime gélatineux, 118 records on 18 maps) shares A1 and D1.
   - INFERENCE: registering by (slot, code) alone would bring these sprites half to life:
     - the C10/D18 sprite would chase the hero but never die (D18 is a no-op);
     - slimes would lose HP and set `Bytes[3]` with no C26 to finish them.
   - FACT: the only header with the tuple (1,1,1,1,0) is the Beannoïde (`1ef8f16c…`). The only one with (0,10,9,1,0) is the Muruta (`e3a39ec4…`). Gating on sprite type and gating on the header tuple give the same result today.

4. **The RNG does not block E14.a, but it decides how acceptance must be built.**
   - FACT: C10 draws at 8 sites (`0x80068238`, `0x800682C0`, `0x80068344`, `0x800684C8`, `0x8006856C`, `0x80068614`, `0x800686B0`, `0x80068754`).
   - FACT: C1 draws at 3 sites. StartWalk `0x8007FEF8` draws 2 or 3 times.
   - FACT: the per-tick advance `0x8002BD0C-0x8002BD34` is absent from the DLL (`AlundraWorldProxy.cs:2766-2772`).
   - FACT: the spawn loot roll `0x80032A40` is absent from the DLL.
   - FACT: the DLL's hit sparks draw from the RNG at the end of the frame (`AlundraWorldProxy.cs:2818` → `AlundraEffectStage.RunFrame`, `:196-207`). The binary draws them inside the hit pass.
   - INFERENCE: matching the binary's RNG stream across several ticks is impossible before E20.e0 (and in frames with several ticks). Acceptance must therefore:
     - compare handlers one call at a time, with the RNG word as part of the input and output state;
     - use in-world arcs that check only outcomes the RNG cannot change.

   If E14.a is built that way, E20.e0 does not have to come first.
5. **Loot can be deferred to E20.e0 without breaking the game logic. The break effect has no owner and needs data that is not exported.**
   - Loot:
     - FACT: `DestroyEntity` `0x8003A59C` sets Status 4 whatever the loot does.
     - FACT: a dropped item is spawned with a null record (`0x80039F58` passes `a3=0`). `0x80039D80-0x80039D84` then gives it `+0x48 = -1`, so it can never match `0x2C [N]`.
   - Break effect:
     - FACT: the break effect id is the sprite header byte `+0x1E`. Among the distinct E0/E1 headers it takes the values {0 ×115, 8 ×50, 10 ×14, 11 ×2, 12, 13}, so it cannot be hard-coded.
     - FACT: `sprite-records.json` has no BreakEffect field.
     - Exporting it is a format decision: converter change, full export, ADR. The E20.e0 row (`plan-e20-heros.md:467`) lists loot but not the break effect. O-E19-23 gave both to E14.
6. **E14.a breaks the oracle baseline of E20.c1a and E20.c1b, and their recette text.**
   - FACT: `AlundraHitArena.RecordingRunner.RunSpriteEvent` forwards to the real runner (`Alundra.Tests/AlundraHitArena.cs:267-271`).
   - FACT: the arena's targets are the real Beannoïde of map 14.
   - FACT: the c1 oracle hooks all handler tables with no-ops and never runs A (`docs/plan-e20-annexe/e20-c1/oracle/notes.md` §2).
   - INFERENCE: once A1 is live, the targets start in anim 2, which has no boxes. Contact hits and sparks then vanish, so the c1a/c1b oracle comparisons fail until there is a switch that turns the natives off in tests, or until the oracle is rebuilt.

---

## (a) Check table

Short names: M = `e14a-muruta`, B = `e14a-beannoide`, D = `e14a-dll`.

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | Master table `0x80098F4C` = {A `0x800C4B34`, B 0, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`} (M, B, D) | CONFIRMED | Word dump |
| 2 | C10=`0x80068154` (`0x800C4F5C`), D9=`0x8007DE04` (`0x800C5358`), E0=E1=`0x8007ED10`, A0=`0x8006174C`, A1=`0x80061758`, C1=`0x80065ED4`, D1=`0x8007D9A4` (M, B, D) | CONFIRMED | Word dump |
| 3 | Dispatch: a nonzero `+0x4C+4t & 0x7F` runs the bytecode `0x8004205C`, otherwise `table[t][+0x70+4t]` (M, B, D) | CONFIRMED | `0x800388F8-0x80038944` |
| 4 | Pick order: 0x100000/0x200000 destroys, then E (0x10 ForceAdjusted or IsOnGround / 0x20 HitCounter / 0x40 `+0xAC`), then D (`+0x224`), then C, with F only for `[0x80127108]` (M, B) | CONFIRMED | `0x80038764-0x80038888`. Status 1 gives A then 2. Status 3 gives E every tick (`0x800237B4` table → `0x80038838`). An entity with `+0x20 != 0` is skipped (`0x8003871C`). |
| 5 | The "repeat while a handler ran" loop re-picks (M §1) | REFUTED (wording) | `0x80038968` loops back to phase 2 (`0x800388C8`), not to the pick. Only triggers set during the pass run again. |
| 6 | `0x2C`: `Result = (count == 0)`, size 2 (M, B, D) | CONFIRMED | `0x8003DC84-0x8003DCAC` |
| 7 | The search tests the candidate's Status 1..3 (B §8) | **REFUTED** | `0x8003C9AC` reads `0x10($s3)` where `$s3` is the owner. The candidate (`$v1`) is tested on `+0x48` only. M and D are right. |
| 8 | "A Beannoïde stops counting when DestroyEntity sets Status 4" (B §8) | **REFUTED** | The candidate stops matching only after the wipe `0x80038634` copies in the template. The template's `+0x48` is set to -1 at `0x8003B21C`. The wipe opens the entity block (`0x8003B3A0`), so the first zero `Result` comes in the map events of the tick after the wipe tick. |
| 9 | Wipe template `0x80134368` "BSS, INFERENCE zeros" (M §10) | PARTLY REFUTED | `+0x48 = -1` (`0x8003B214-0x8003B21C`). The other fields are UNKNOWN (BSS). |
| 10 | Hall: records 24-27, sprite 91, C=153 → C[25]@1464 = `2B 25 37[20] 2A 40[2,0] FF` (M, D) | CONFIRMED | `map_115.json` codes 1464-1472. Records: Contents 0, flag `_10` 0, SpriteDirection 128. |
| 11 | `0x2D` @403-407 (brief, D) | REFUTED (off by one op) | The four `0x2D` are at @401, 403, 405, 407 (bytes `45 24 … 45 27`). The wait loop @413-433 is `00`, `2C n`, `04 back`. `05 39 02` at @434 sets G569. |
| 12 | 116 grate: A[1]@128 tests `2C[0]`, `2C[1]`; C[1]@312 polls, then `1A 03` (B, D) | CONFIRMED | Codes 128-149 and 312-325. Records 0 and 1: sprite 79, SpriteDirection 0xC0, every program index 0. |
| 13 | C10 states (0, 5, 9/10, 12, tail), every constant, RNG tables `0x80027214/722C/724C/7264`, cardinal table `0x80028B54`, TryAttack arguments (M §3) | CONFIRMED | Full read of `0x80068154-0x8006892C` |
| 14 | Decomp C10 `default:` writes `AIValues[1]=0x1E; Bytes[1]=0` every tick in states without a case (M §0.6) | CONFIRMED | Decomp `FunctionTypeC.cs:9479-9487`. In the binary, jump-table entries 1-4, 6-8 and 11 go straight to `0x80068904`. |
| 15 | Table `0x80028B34` = `18, 00×8, 08×8, 10×8, 18×7` (M, B, D) | CONFIRMED | Byte dump. The decomp table differs at indices 14-16, 19-23 and 25-27 (`StaticVariables.cs:543-547`), as B says. |
| 16 | C1 (all cases), and case 1 blocked does not write `+0x88` (B §3, §0.4a) | CONFIRMED | `0x800660F8` (`j 0x80066238`) stores only `AIValues[1]`. The decomp sets anim 0 (`FunctionTypeC.cs:186`). |
| 17 | StartWalk `0x8007FEF8`: rng(0x65) < chance then aim at the hero, else rng(0x20); then anim; then `base + rng(16)` (B) | CONFIRMED | `0x8007FEF8-0x80080000` |
| 18 | D1 / D9: ComputeNewHp, then `Bytes[3]=1` on death, then anim 5 / 2, then direction away from the hero (M, B, D) | CONFIRMED | `0x8007D9A4`, `0x8007DE04` |
| 19 | E1 = `DestroyEntity(e,-1)`: loot, Status 4, `+0x228=-1`, ActiveEffect, effect `hdr+0x1E`, platform (M, B) | CONFIRMED | `0x8003A59C-0x8003A644` |
| 20 | A0: `AIValues[0] = PosZ>>16`. A1: anim 2 (D) | CONFIRMED | `0x8006174C`, `0x80061758`. Only native handlers read `+0x280` (`0x80061C20`, `0x8006A708`, `0x8006D2E8`, `0x8007C360`, …), never an opcode. The DLL reads `AIValues` nowhere. |
| 21 | relpos: abs at [0..2], signed at [3..5], Z from `+0x134`. A second entry `0x8007FE8C` uses the entity's `+0x11C` (B) | CONFIRMED | `0x8007FE20-0x8007FEF4` |
| 22 | TryAttackPlayer2 / Front argument layout and per-direction table (M §5.3) | CONFIRMED | `0x8008048C-0x800806FC` |
| 23 | HandleAnimationDirection = OR of 4 corner pairs, thr = dist+1, "turn" writes anim, zeroes the forces, applies the quarter-turn (M §5.4) | CONFIRMED | `0x80080144-0x8008025C`. Corner order [0]=x1y1, [1]=x2y1, [2]=x1y2, [3]=x2y2 is an INFERENCE from the direction ranges. It matches the DLL's sampling order. |
| 24 | The atan table is at `0x80022488` and the thresholds at `0x80022688`. The DLL doc says `0x80028B34` (B, D) | CONFIRMED | `0x8002E850`, `0x8002E894`. `ScriptHelper.cs:23-29` gives the wrong address. |
| 25 | "TileX/TileY declared, never written" (M §0.4) | **REFUTED** | Written every tick in `AlundraScriptedMotion.cs:552-553` (`(PosX>>16)/24`, `(PosY>>16)/16`), and at spawn (`EntityRecordMapper.cs:182,190`; `AlundraWorldProxy.cs:1925-1926`). It equals the binary's LUT for 0..1247 px (`0x8003B194-0x8003B1D4`). |
| 26 | FloorHeight kept for the hero only (M, B) | CONFIRMED | `AlundraEntityScriptProxy.cs:1999`. No other code reads FloorHeight except the hero's (`AlundraPlayerManager.cs:381`). |
| 27 | MapHeights never written (M, D) | CONFIRMED | grep: only the declaration at `:381` and in Clone. |
| 28 | "Pure helpers read TileX/TileY/TerrainHeight" (D §2.4) | **REFUTED** | See item 21: the binary reads FloorHeight `+0x134` = TerrainHeight+1 for non-Collidable entities (`0x80037F28-0x80037F3C`). |
| 29 | Animation table "AnimFlags (byte 0xD)" = 7 for anims 3/4 and 3 for anims 7/8; anims 2/5/6 = 0 (M §2) | **REFUTED** | Animation-set layout: `lh +0xA` → `+0xF8`, `lbu +0xD` → `+0xB4` (`0x80038B5C-0x80038B70`). The export's "Flags" column is byte 0xB, the high byte of the impulse (1792 = 0x700, 768 = 0x300). The real byte 0xD is the export's "Acceleration" column: anim 2 = 0x41, 3 = 1, 4 = 0, 5 = 0x44, 6 = 0x40, 7/8 = 0, 12 = 1. |
| 30 | "Exported Acceleration column is not that nibble (UNKNOWN)" (M §2) | REFUTED | The DLL reads it as byte 0xD (`AlundraFrameSyncPasses.cs:146-151`, `AlundraScriptedMotion.cs:463`). The Beannoïde `Flags` values 5/6 match impulses 0x500/0x600. |
| 31 | Enemies have only the 25-tick lock (M §4.4). Anims 5, 6, 7 are immune via bit 0x40 (B) | M REFUTED, B CONFIRMED | Hit pass `0x8003943C-0x80039444` tests `+0xB4 & 0x40`. Muruta anims 2/5/6 also carry 0x40. The DLL already applies it (`AlundraHitPass.cs:118`). |
| 32 | "A second hit while dying restarts anim 2, 5, 6" (M §4.3) | REFUTED | Anim 6 is immune and E1 fires at H+23, before the lock-out ends at H+25. |
| 33 | Death timeline: E1 at H+23, `0x2C` zero at H+25 (M); E1 at t+22 = H+23 (B) | CONFIRMED (INFERENCE) | Tick order: wipe, events, counters, lists, clock `0x80038E18`, physics, effects, hits (`0x8003B3A0-0x8003B3F0`). It needs the oracle to be exact. |
| 34 | Muruta L1 HP 3, responses `[16,16,16,64,…]`, sword 2 damage, so 2 hits on map 115 (M) | CONFIRMED for 115 (D=0) | `balance.json` record 347; K2.txt (sword attr 0x81 → 2 damage) |
| 35 | "Two sword hits kill a Muruta" (generic, M §0.3) | REFUTED for 362 | Map 362 has D=2. `WalkChain` (`AlundraBalanceTables.cs:236`) moves on to L8: HP 15, power 6, so 8 sword hits. |
| 36 | Beannoïde HP 9, 5 sword hits (B) | CONFIRMED | Record 335 (L255); K2.txt shows 9→7 |
| 37 | Loot rows `0x7B`, `0x7C`, `0x76`, `0x7D`, `0x7E` and their probabilities (M, B) | CONFIRMED | Byte dump at `0x80028E0C + row*16` |
| 38 | Loot code chosen as "record Contents if the flag is not set, else the sprite's +0x1F" (M §5.7) | REFUTED (garbled) | `0x80032A40`: a set flag gives code 0 (no loot, no draw). Otherwise the record's Contents byte `+0x12` if nonzero, else the sprite's `+0x1F`. B's wording is right. |
| 39 | MP veto "when GetPlayerMpMax() != 0" (M) | REFUTED (inverted) | `0x80032A00` vetoes when `0x8004DDDC` returns 0. It does not matter for these sprites. |
| 40 | The drop entity has `+0x48 = -1` and never counts in `0x2C` (new) | FACT | `0x80032BEC` → `0x80039F58` with `a3=0` → `0x80039D80` |
| 41 | Muruta Flags `0x30103` (M); "same Flags 0x103" for the Muruta (B §9) | M CONFIRMED, B REFUTED (minor) | `0x80039DA4-0x80039DD0`: `lhu +0x10 \| lbu +0x12<<16`. The Muruta header's `+0x12` is 3. |
| 42 | "C10 and D9 are used only by 347; C1 only by 335" (D §0.3) | PARTLY REFUTED | True for static records only. C10 is also used by header (0,10,18): map 329 (5 records), and loaded on 153 and 368. C1 is also used by header (0,1,23), loaded on 99, 123 and 449. |
| 43 | "The C10 map-0x99 branch is dead for the corpus" (D §2.6) | REFUTED (scope) | It is dead for sprite 347. Map 153 loads the C10/D18 sprite, spawned at runtime with a parent. |
| 44 | Map 15 Muruta never reach native code; `0x2E [128]` @712/777/890; `0x8003A774` has no loot and no effect (M) | CONFIRMED | Bytes `2E 80` at those offsets; `0x8003A774-0x8003A7AC` |
| 45 | "Map 15 needs nothing from E14.a" (M) | UNVERIFIABLE | Their D slot is native D9 (record D=0). Whether the hero can hit them during the scene is UNKNOWN; reading opcode `0x63` and the scene's control flags would decide it. |
| 46 | `RunSpriteEvent` = save book + E0/E1 destroy + a counted no-op (D) | CONFIRMED | `AlundraEventProgramRunner.cs:293-330` |
| 47 | Pick and run in the DLL (D) | CONFIRMED, with a nuance | `AlundraEntityScriptProxy.cs:1519-1637`. The DLL picks and runs per entity, the binary picks all entities first. This does not matter for E14.a: these handlers touch only their own entity. |
| 48 | Recycling right after the map events; `0x2C` does not test the candidate's status (D) | CONFIRMED | `AlundraWorldProxy.cs:2323-2331`, `:2990-3012`; `EntitySearchService.cs:104-117`. Equivalent to the binary (item 8). |
| 49 | No hero latch on `IEntityWorldContext` or `IAlundraScriptHost` (D) | CONFIRMED (interfaces) | `AlundraWorldProxy.cs:2781` (internal). INFERENCE: `entity.ScriptHost as AlundraWorldProxy` reaches it, so this is a seam choice, not a blocker. |
| 50 | RNG constants are the same; no per-tick advance in the DLL (D) | CONFIRMED | `AlundraRandom.cs:21-35`; `AlundraWorldProxy.cs:2766-2772`. Note: `RandomSeed` is a `ulong` and keeps high bits, so a `Below(n)` helper must multiply `(uint)Next()`. |
| 51 | NPC movement and `AnimationDirection` are values from the end of the frame (D AD-4) | CONFIRMED | `AlundraEntityScriptProxy.cs:1444-1446`; `AlundraFrameSyncPasses.cs:142-143`. The per-tick direction is `BoxDirection` (`HitPass.cs:103-133`). In the binary `+0x98` is updated in the clock pass (`0x80038AD8-0x80038B40`). |
| 52 | Hold-flag reset between ticks of one frame (pick of E) | CONFIRMED safe | `ClearHoldFlagsOfPendingSwitches` runs at each tick > 0 before the map events (`AlundraWorldProxy.cs:2319-2324`), so the C10 state-5 → anim 6 path cannot fire E early. |
| 53 | Lever files `AlundraLeverArcSupport.cs` / `AlundraLeverSmokeTests.cs` are untracked (D) | REFUTED (stale) | Both are listed by `git ls-files`. |
| 54 | An oracle with real handlers is feasible by un-hooking them in the c1 machine (M, D) | CONFIRMED (INFERENCE) | notes.md §2 and §6: the passes, the spawner and `0x80032A40` are real; effects and sound are stubbed and journalled; a stubbed `jalr` target is handled. |
| 55 | B's MIPS runs: C1 24 000/0, D1 500/500, loot 20 000 | UNVERIFIABLE (not re-run) | My independent disassembly read agrees with every case of B's pseudo-code. |
| 56 | Up to 16 boss-split Beannoïde, and who sets anims 9-14 (B) | UNVERIFIABLE | Outside E14.a's acceptance (map 44 boss) |

---

## (b) Corrections

- **C1. The Z in the relative position is FloorHeight `+0x134`.**
  - For the Muruta and the Beannoïde (not Collidable), the binary gives `+0x134 = +0x138 + 1` (`0x80037F3C`). For the hero it is the floor including any support.
  - The port must keep a FloorHeight per enemy, written where `0x80038064` writes it: at init (`0x80039F30`) and at the end of the physics pass (`0x800384BC` loop).
  - For these sprites the DLL's `ComputeFloorHeight(TerrainHeight)` gives the same value. `EntitySupport` rejects entities that are not eligible: an INFERENCE, to be confirmed with `IsEligibleSubject`.
  - Nothing else reads an enemy's FloorHeight (item 26), so adding it is invisible elsewhere.
- **C2. MapHeights come from `0x800370C4`, which is the same four-corner routine the DLL already samples.** Storing each corner in `ComputeTerrainHeight` is the natural seam. The DLL's +1 convention (`AlundraEntityScriptProxy.cs:1773-1780`) must stay the same between FloorHeight and MapHeights so that the differences match the binary's.
- **C3. Muruta animation physics:**
  - byte 0xD gives these shifts: anim 2 = 1, 3 = 1, 4 = 0, 5 = 4, 6 = 0, 7 = 0, 8 = 0, 10 = 1, 12 = 1;
  - anims 2, 5 and 6 are immune (0x40);
  - the exported "Flags" column is the high byte of the Z impulse.
- **C4. `0x2C` timeline.** The candidate is never tested on status. The last tick that still counts is the tick after E1 (the slot is wiped at the start of that tick's entity block). The first zero comes one tick later. With the hit at H: E1 at H+23, still counted at H+24, zero at H+25 (INFERENCE, needs the oracle).
- **C5. Map 362 Muruta are L8:** HP 15, contact power 6.
- **C6. The C10/D18 sprite (maps 329, 153, 368) and the Petit slime (C1/D23) share code with E14.a.** They must stay unregistered until their D handlers exist.
- **C7. The decomp is a trap in four places for an executor:**
  - the C10 default branch;
  - C1 case 1 blocked;
  - the `g_directionFlipTable` contents, and C1 indexing the atan table;
  - the relpos Z field.

  `ScriptHelper.cs:23-29` gives the wrong address for its table.
- **C8. The hall Muruta do not start native** (C[25] hands over through `0x40 [2,0]`). Of the 115 Muruta, only states 0, 5, 9/10 and 12 of C10 matter after the hand-over, and their TargetAnimationId is still the spawn value (INFERENCE: 0).

---

## (c) Recommended plan for E14.a

**Order (INFERENCE, within D-E20-4):**
1. After E20.c1b-2 closes, and before E20.c2 and E20.e0.
2. E14.a needs neither E20.e0 (RNG advance, spawn, loot) nor E20.c2 (hurt hero), provided acceptance follows the rules below.
3. E20.c2 and E20.e0 then re-measure the E14.a arcs, as E20.a did for its pins.

**Slice E14.a0: shared infrastructure, no behaviour change.**
- Registry in `RunSpriteEvent`, after the save book and E0/E1:
  - entries keyed by (slot, code), and allowed only for listed sprite types (or the full header tuple);
  - an unknown key stays a counted no-op, with one debug log per key;
  - a switch that turns the natives off, for tests;
  - the registry is empty in this slice.
- Enemy FloorHeight and MapHeights[0..3]:
  - written at spawn and at the end of each physics tick, following `0x80038064` and `0x800370C4`;
  - only for entities that are not the hero.
- Helpers (all new):

  | Helper | Binary address |
  |---|---|
  | relpos | `0x8007FE20` |
  | TryAttackPlayer2 | `0x8008048C` |
  | TryAttackPlayerFront | `0x800805C8` |
  | HandleAnimationDirection | `0x80080144` |
  | StartWalk | `0x8007FEF8` |
  | quarter-turn table | `0x80028B34` |
  | `Below(n) = ((ulong)(uint)Next() * n) >> 32` | — |
  | `HitReaction(e, anim)` (the skeleton of D1 and D9) | — |
  | a hero-latch seam | — |
- Acceptance:
  - each helper under the MIPS emulator on random states (like B's `oracle_c1.py`), with expected values from the oracle and never computed by the code under test;
  - mutation script;
  - the whole suite unchanged (Release and Debug).
- Stop:
  - if the enemy FloorHeight or MapHeights cannot be made equal to the binary's on the oracle maps (+1 convention): pause and ask the author.

**Slice E14.a1: Beannoïde and Muruta.**
- Register A1, C1 and D1 for type 335, and C10 and D9 for type 347. E1 stays as it is.
- Do not port C10's map-0x99 branch (it is unreachable for 347). Document it.
- Oracle:
  - extend `e20-c1/oracle` by un-hooking `0x80061758`, `0x80065ED4`, `0x8007D9A4`, `0x80068154`, `0x8007DE04` and `0x8007ED10`;
  - let A run (status 1);
  - keep effects, sound and `0x80039F58` as journalled stubs;
  - add `0x8003BE74(…8…)` to the journal so the deferred effect is visible.
- Scenarios:
  - Beannoïde: dormant, wake, lunge, walk (free and blocked), hurt, death;
  - Muruta: calm sequence, alert, lunge, close/mid/far, hurt, death;
  - the hall death timeline (E1 at H+23, `0x2C` zero at H+25).
- The C# side compares tick by tick, with the RNG word copied in from the oracle after spawn, in frames of one tick. The hero must not be touched, or the tick where he is must be excluded with a reference to E20.c2.
- Baselines:
  - the c1a/c1b arena uses the natives-off switch, so the existing oracle stays valid;
  - the c1b-2 lever arcs are re-run;
  - every changed pin is justified.
- Arcs (outcomes only, independent of the RNG):
  - 115: four `0x2D`, Muruta killed by scripted hits, `0x2C` passes, G569 set at @434, portals @493-503;
  - 116: Beannoïde records 0 and 1 killed, grate record 6 gets anim 3.
- Recette: the hall of 115 and the grate of 116. On map 14 the Beannoïde are now alive.
- Stops:
  - a handler in the oracle reaches an unstubbed call;
  - a defect of the original is suspected (the author decides);
  - the c1a/c1b oracle cannot stay valid behind the switch.

**Deferred, written in the plan:**
- loot (roll at spawn and drop at death) goes to E20.e0. It is visible: dead enemies drop nothing;
- break effect `hdr+0x1E` (author decision, see Q2);
- A0 (no reader yet).

---

## (d) Questions only the author can answer

1. **Dispatch gating (ADR-0054).** Native handlers by sprite-type list, or by the full header tuple? Or by (slot, code) alone, which brings the C10/D18 sprite, the slimes and the Petit slime half to life?
2. **Break effect.** Export `BreakEffect` now (converter, full export, ADR) inside E14.a, or defer it? If deferred, who owns it: E20.e0 (whose row does not list it) or E14 (O-E19-23)?
3. **Loot deferred to E20.e0.** Do you accept that killed enemies drop nothing, and roll nothing at spawn, until E20.e0?
4. **Order against E20.c2.** E14.a first (D-E20-4, the hall arcs are re-measured by c2), or E20.c2 first (the hall arcs are final, with the hero hurt by claws)?
5. **Tests and recette.** Add a switch that turns the natives off to keep the c1a/c1b oracle arena, or rebuild that oracle with live handlers? Do you accept the new behaviour of the map-14 Beannoïde on your route?
6. **D-E19-13 for the new movers.** Enemies read the animation and AnimationDirection at the end of the frame, which differs from the binary only in frames with several ticks. Keep it as a documented gap, or have the native handlers read the per-tick values (`BoxDirection`, box animation)?
7. **Scope.** Are maps 362 (L8 Muruta, chest after `0x2C [6..9]`) and the map 44 wave part of E14.a's recette, or do they just come along?
