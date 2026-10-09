> Report returned by one agent of the E20.c1b-2 read-only discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

**E20.c1b-2 arc scaffolding: report (label: arc-scaffolding)**

Read-only. I built and ran nothing. My scratch scripts are under `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20c1b2/arc-scaffolding/`. Paths are relative to `D:/development/repo/alundra-casaengine-project-converter/`. Tests are in `Alundra.Tests/`. The binary is `ALUN_CD.EXE` (France), read with capstone.

## 0. Findings that change the slice

1. **An arc must start from `Arrival` on 115, 116 and 117, or the lever records do not exist.**
   - FACT: records spawn at map load only if the hero's tile at that moment lies inside the record's `XMin..XMax` by `YMin..YMax` zone (`AlundraWorldProxy.cs:868-875`, `AlundraEntitySpawnFactory.cs:111-155`).
   - FACT: without `Arrival`, the hero is on the New Game tile (33,59) at that moment (`AlundraWorldProxy.cs:1913-1914`, `AlundraGameState.cs:45,48`). `ArcRun` writes `HeroTileX/Y` only after `InitializeWithWorld` (`AlundraArcSupport.cs:209` then `:249-261`), which is too late.
   - FACT: the zone gate is load-time only, in the binary too. `ALUN_CD.EXE` has 10 `jal 0x8003A1B8` (SpawnEntity). Only `0x8003B308` has `a2 = 0`, which is the map-load loop. The other nine pass `a2 = 1`. The decompilation agrees (`GameEngine.cs:636` against all other callers).
   - FACT: the zones are:

     | Map | Record | Zone (tile X by tile Y) |
     |---|---|---|
     | 115 | 2 | 0..9 by 5..16 |
     | 116 | 39, 40 | 0..7 by 0..27 |
     | 117 | 0 (bascule) | 17..34 by 4..16 |
     | 117 | 22 | 0..16 by 33..59 |
     | 44 | 7-12, and 0-6 (stairs) | 0..51 by 0..59 (whole map) |

   - Source: `alundra-project/Maps/<zone>/<world>/tilemap/<world>.tileMap`, layer `Entities`, `custom_properties`.
2. **The bascule does not exist after the entries the facts file lists for it.**
   - FACT: the arrival from 116 portal 0 is 117 tile (13,34), and from 115 portal 7 it is (5,21). Both are outside X 17..34 by Y 4..16.
   - FACT: only 117's own portals 2, 4 and 6 arrive inside the zone, at (21,6), (25,6) and (29,6).
   - Likewise the 116 levers exist only for X ≤ 7 arrivals, such as (3,20) from 115 portal 12. From 117 portal 1, which arrives at (26,35), they do not exist.
3. **The "tiles" in `faits-leviers-opcodes-dll.md` §1 are exporter `DisplayX/Y` (`XPos/2`, `YPos/2`), not runtime tiles.**
   - FACT: the runtime position is `(XPos*12+12, YPos*8+8, Height*8)` px (`EntityRecordMapper.cs:177-199`). Use the pixel values in §1.3.
   - FACT: they differ by one tile whenever `XPos` or `YPos` is odd.
4. **Temporary flags set through `ArcSpec.Flags` are wiped before the first frame.**
   - FACT: `ArcSpec.Flags` is applied at `AlundraArcSupport.cs:156-159`, before `InitializeWithWorld`. Map entry then runs `InstallForMapEntry`, which calls `Array.Clear(TemporaryFlags)` (`AlundraGameState.cs:351`, called from `AlundraWorldProxy.cs:783`).
5. **117 starts with a ~21-tick scripted lock when the hero is in the bascule zone.** Details in §3.4.
6. **No engine change is needed.** Details in §7.

## 1. ArcSpec arguments per map

### 1.1 Zone and world names

FACT: the source is `alundra-project/Maps/world-index.json`. Each entry is `"Maps\\<Zone>\\<World>\\<World>.world"`. The zone is path segment 2 and the world is segment 3. The world name always ends in `-<mapId>`, which `BackdropLoader.TryParseMapIndex` (`BackdropLoader.cs:110`) parses.

The production reader is `AlundraWorldIndexTable.Resolve(int)` (`AlundraWorldIndexTable.cs:84-94`). Test lookups: `AlundraHeightTargetSitesTests.cs:85-86` (scans the `Maps` folders for `*-<N>`) and `AlundraStoryChainOpcodeAudit.cs:344-352` (`LoadMap`, which reads the index).

| Map | Zone | World | `world-index.json` line |
|---|---|---|---|
| 115 | `Tarn's Manor` | `Tarn's Manor-115` | 117 |
| 116 | `Tarn's Manor` | `Tarn's Manor-116` | 118 |
| 117 | `Tarn's Manor` | `Tarn's Manor (unused broken)-117` | 119 |
| 44 | `Wendels Nightmare` | `Wendels Nightmare-44` | 46 |

FACT: for all four maps `tilemap/<world>.tileMap` and `.tileset`, `dialogues/<world>.dialogue` and `events/<world>.events.json` exist. `ArcRun` reads the dialogue at `AlundraArcSupport.cs:218-219`.

FACT (my check): every record of the four maps has a `PrefabAssetId` that is an `entity` asset in `AssetInfos.json` and has a row in `Data/sprite-records.json`. So the prefab path does not fall back to bare entities for data reasons.

The map folder for the 117 tile map is named `Tarn's Manor (unused broken)-117`, which is the same string as the world name.

### 1.2 Template and mandatory flags

The template is `AlundraFireSwitchRegressionTests.cs:38-40`. Both `RealController: true` and `Prefabs: true` are mandatory.
- FACT: `Prefabs` without `RealController` throws (`AlundraArcSupport.cs:140-143`).
- INFERENCE: without `Prefabs` the hero is a bare box with no collision keyframes, so the sword gets no frame box. The prefab hero is built at `:177-179`.

The `Arrival` record has this shape: `new ArcArrival((tx*24+12)<<16, (ty*16+8)<<16, zLevel<<20, AlundraGameState.ResetAnimationId, direction)`. FACT: the animation must be `0x36`, because `AlundraArcSupport.cs:243` asserts it. `HeroTileX/Y/Z` are unused when `Arrival` is set (`:249`). Direction codes are 0 south, 8 west, 16 north, 24 east (`AlundraHitArena.cs:53`, `AlundraHeroAttackTests.cs`).

### 1.3 Recommended specs

Arrival tiles are real `DestTile`s of the tileMap `Portals` layer. The stand points are INFERENCE: I checked that the cells are free (walkability `0x00`), at the same height as the lever, with no ground property `0x20`. The settle assertions in §3.3 decide them.

| Map and record | Lever pixel position (px, from XPos/YPos/Height) | Arrival tile | Flags | Stand point and facing |
|---|---|---|---|---|
| 115 rec 2 | (60,120,24), tile (2,7), cell height 1 | (4,7), the destination of 116 portal 3 | none | hero (82,120,16), facing west (8); the cells west of the lever are walls (height 5, walkability `0x41`) |
| 116 rec 39 | (864,424,128), tile (36,26) | (3,20), the destination of 115 portal 12 | none | hero (842,424,128), facing east (24) |
| 116 rec 40 | (1104,424,128), tile (46,26) | (3,20) | none | hero (1082,424,128), facing east (24) |
| 117 rec 0 | (528,192,16), tile (22,12) | (21,6), the destination of 117 portal 2 | none | hero (506,192,16), facing east (24) |
| 44 rec 7..12 | (144,440), (432,472), (384,936), (672,376), (1080,904), (936,408), all Z=128; tiles (6,27), (18,29), (16,58), (28,23), (45,56), (39,25) | any tile, for example (5,27), with Z level 8 (`8<<20`) | `{ 1633 }` | lever.X − 22, same Y, Z=128, facing east; for rec 12 use dx = 12 (the platform is 2 tiles wide) |

On the 116 and 117 rows, place the hero with `PlaceHero` after the arc starts. The hero lands in the zone at load, then moves to the stand point.

For 117, hero tile (21,12) is inside MapEvent_1 (17..34 by 4..16). For 116, the lever cells are at height 8 with ground property `0x02`, not `0x20`.

Lever and bascule bodies are 24x16x16 with offset (−12,−8,0). Lever sprite 49 has `MoreFlags` 1, which has no 0x80 (Collidable) bit, so it does not block the hero. The bascule (sprite 52) has `MoreFlags` 129, which does. FACT: `Data/sprite-records.json`.

## 2. Initial flags

- FACT: global flags go in `ArcSpec.Flags` (`int[]`, the plain flag number). The loop at `AlundraArcSupport.cs:156-159` is `State.AddFlag((uint)f, 1u << (f & 0x1f))`. `ResetAll` runs just before it (`:145`).
- FACT: the flag operand of the programs is a 16-bit word. Temporary flag `Tn` is `0x8000 + n`, and the mask is `1u << (n & 0x1f)` (`AlundraGameState.cs:34,315`; `AlundraEventProgramRunner.cs:581-587`). I decoded 115 D[1] at 1476 as `0x2B; 0x05 [2,128]`, which is T2 on.
- FACT: temporary flags can be preset only after the `ArcRun` constructor returns. Precedent: `AlundraHeightTargetSitesTests.cs:93-101` (`AlundraGameState.Instance.AddFlag(32769, 1u << 1)`).
- FACT: the constructor runs no frame, so a flag preset right after it is seen from frame 0.
- Recommendation: do not preset T flags for the lever tests. The point of c1b-2 is that the sword hit makes the programs set them.
  - 117's own A[1] at 132 sets `T1` on and `T2..T5` off at the bascule's load.
  - The 116 plates also set `T1`/`T2`.
  - Preset only to isolate a downstream program on purpose, for example 116 B[1] with `T1` and `T2` both set after the constructor.
- Flags needed per map:
  - 44: `G1633` for D[8] (`0x31 [97,6]`; 244: `0x30 G1633` is B[2]'s own gate).
  - 115 NW, 116 and 117: none. G569/G262/G659 only gate the 115 south-west hall, which is a different map-event rectangle.
- AlundraTestSaves presets are irrelevant here: they refuse temporary flags and flags ≥ 2048 (`AlundraTestSaves.cs:36-70`).

## 3. Swinging the sword in an arc

### 3.1 Pressing

- FACT: one frame of `OneFrame()` (0.02 s) is exactly one tick. `AlundraScriptedMotion.FixedTickSeconds = 1f/50f` (`AlundraScriptedMotion.cs:52`) and the accumulator in `AlundraLogicClock.cs:55-77` leaves 0 remainder.
- FACT: the tick order is pad, dialogue box, map events, entity block (MovePlayer, programs, hit pass, counters), inventory (`AlundraWorldProxy.cs:2283-2348`).
- Two equivalent ways to press:
  - `arc.Press(AlundraPadState.Square)` (`AlundraArcSupport.cs:400-405`), as in `AlundraHeroAttackTests.cs:428`.
  - `arc.HoldDirections(Square); arc.OneFrame(); arc.HoldDirections(0)`, which is what `HitArena.Frame/RunSwing` do (`AlundraHitArena.cs:159-176`).
- FACT: the edges are the tick pad's own (`AlundraWorldProxy.cs:2287`), so the previous frame must have Square released.
- Preconditions: `PlayerControlFlags & InputBlockedMask` (`0x34`) must be 0; `Hero.TargetAnimationId == 0`; `Hero.TargetDirection` set to the facing; no ground property `0x20` under the footprint (`AlundraPlayerManager.cs:473,494`).
- FACT: with `Arrival` and an empty inventory the hero swings the basic sword. `RecordIndexOfItem(NoItem)` is 0 (`AlundraPlayerManager.cs:178-179`; `AlundraWeaponTables.cs:111`), and `AlundraFireSwitchRegressionTests.cs:63-72` relies on it. `InitializeNewGameInventory` is skipped when there is an arrival (`AlundraWorldProxy.cs:1907`).

### 3.2 Ticks from the press to the contact

All of this is the basic sword, ground swing animation `0x10`, attribute `0x81`, power 1, facing east against a Beannoid.

| Fact | Source |
|---|---|
| Press at tick S: `TargetAnimationId = 0x10` in the same tick, the swing sound 29 requested in that tick, `0x10` held for ticks S..S+20, Idle at S+21 | `AlundraHeroAttackTests.cs:415-443` |
| H1: Square at tick 1, hero (212,96), Beannoid (252,96) (dx = 40): contact tick 5 (`+0x1D0 = 0x19`), hitbox offset (21,−8) at tick 5, hero `HitCounter` 1, 3 sparks at the overlap centre, target `TouchingEntity` cleared at tick 6, D chosen at tick 6 | `docs/plan-e20-annexe/e20-c1/oracle/out/H1.txt`; `AlundraHitPassTests.cs:129-146` (`samples[3..5]`) |
| H1-near, dx = 30: contact at tick 1 (the box of the first frame, offset 0, 29 px wide) | `AlundraHitPassTests.cs:167-176` |
| Reach sweep east at dy = 0: contact tick 1 for dx −9..38, tick 5 for dx 39..59, none at ≥ 60 | `oracle/out/reach-sweep.txt` (the Beannoid body is 21 px) |
| H8: facing west, 40 px away: tick 5 | `AlundraHitPassTests.cs:265` |
| Real arc on map 14: tick 5, D at tick 6 (`samples[hitTick].Handlers` contains (1,3)) | `AlundraHitPassTests.cs:342-371` |

- INFERENCE: with the hero 22 px from a lever body of 24 px (offset −12), the box of the first frame overlaps it, so the contact is at the press tick. West-facing at dx = 22 is likewise INFERENCE. The test decides it by sampling the end of the press frame.
- FACT: D of the target is chosen at the tick after the contact, so lever D runs at trace frame `F+1` when the press frame is `F` (the value of `arc.Frame` before the press).

### 3.3 Settling (needed because the arrival does not place the hero where you want)

Do what `AlundraFireSwitchRegressionTests.cs:55-61` and `AlundraHitArena.cs:86-97` do:
- `PlaceHero(x,y,z)`, then set `TargetDirection`, then run about 4 to 6 frames.
- Assert `Hero.IsOnGround == 1`, `PosX/PosY/PosZ` unchanged, and `PlayerControlFlags == 0`.
- `ArcRun` already does `Hero.TargetAnimationId = 0` (`AlundraArcSupport.cs:244`). The same line in the FireSwitch test (`:41`) is redundant.
- In E20.b, `WaitUntilTheHeroIsFree` (`AlundraHeroAttackTests.cs:403-413`) loops until target and current animation are 0 and `PlayerControlFlags == 0`.

### 3.4 What runs at load, and when the hero is free

FACT: map-event programs run every tick while the hero's tile is inside the event rectangle, and re-arm when it leaves (`AlundraWorldProxy.cs:2887-2935`). `ControlLocked` (0x04) does not stop them. Only `MenuOpen` and `Unused40` do (`AlundraGameState.cs:89-90`).

- **115:** hero at (3,7) is inside MapEvent_0 (0..10 by 4..17) only. B[1] at 128 runs: `0x85` at load, then `0x36 T1`. Keep the hero inside this rectangle during the test; leaving it re-arms B[1].
- **116:**
  - B[1] at 152 and B[2] at 300 cover the whole map.
  - B[1]'s first instructions are `0x62 [16,0,97]` and `0x85 [30,19,3,5,40,20]` (the retracted bridge, written at load). Take cell baselines after the first frames, not from the export.
  - B[2] plays sound 149 twice at the first frame.
- **117, hero at (21,12):**
  - It is inside MapEvent_1 (17..34 by 4..16), so B[2] at 284 runs at load: `0x10`, sound 224, twelve `0x85` with four `0x37 5`, then `0x11`. About 21 ticks of lock.
  - MapEvent_4 (15..35 by 3..18) also runs B[5] at 420, which plays sound 149 twice.
  - The swing must wait for control to come back.
  - Sound 224 is also played by C[1] when the bascule is struck. Window sounds by frame.
  - B[1] at 152 (the `0xB9` "Livre" scene) is in MapEvent_0 (0..13 by 3..23) and is not reached from x = 21.
- **44:** all seven map events cover the whole map, but B[1..5] test `0x3B` hero areas that are far from the levers.
  - B[1] area x 4..7, y 57..59.
  - B[2] area x 5..6, y 30; also gated by G1633.
  - B[3] area x 43..44, y 20; gated by G1634.
  - B[4] x 14..36, y 12..17.
  - B[5] x 10, y 19..20.
  - B[6] at 996 is `Break; Restart`. B[7] at 1056 plays sound 25 when one of the trap doors has current animation 2.

## 4. Assertion accessors

| What | Accessor (file:line) | Notes |
|---|---|---|
| Temporary flag N | `ArcChecks.IsTemporarySet(n)` (`AlundraArcChecks.cs:73`); raw: `ArcRun.State.GetFlag(0x8000u+n)` | Sample at end of frame: `ArcSamples(arc, records, new uint[]{...}, watches...)` then `.FirstSet[n]` / `.Cleared[n]` (`AlundraArcSamples.cs:32-33,65-78`). |
| Global flag N | `ArcChecks.IsSet(n)` (`AlundraArcChecks.cs:70`) | Example: `AlundraTarnMurggArcTests.cs:34,50`. |
| Exact frame of a flag write | `ArcChecks.FramesOf(arc, slot, pc, opcode)` (`AlundraArcChecks.cs:32-33`), `arc.Has/FirstIndexOf` (`AlundraArcSupport.cs:339-341`) | `0x05` set, `0x06` clear, `0x32` toggle. Catches a flag set and cleared within one tick, which an end-of-frame sample misses. |
| Entity animation | `AlundraEntityScriptProxy.TargetAnimationId` (+0x88, program-driven, `AlundraEntityScriptProxy.cs:120`), `CurrentAnimationId` (synced at end of frame, `:122`) | Examples: `AlundraVisionAndCoastArcTests.cs:448,454`. |
| Entity flags (ClassA) | `(e.Flags & EntityFlags.ClassA) != 0` (`:118`; `EntityFlags.cs:42`, value 0x01) | `AlundraFireSwitchRegressionTests.cs:51,77`. |
| Entity record lookup | `arc.EntityByRecord(n)` (`AlundraArcSupport.cs:336`); `ProgramIndexes[3] & 0x7F` is the D index (`AlundraEntityScriptProxy.cs:93`) | Returns null if the zone gate dropped the record. It returns the first entity of that record id. |
| 0x85 cell rewrite | `var store = (AlundraCellStore)arc.Proxy.CellMutator!;` then `GetFloorTileId`, `GetHeight`, `GetWalkability`, `GetGroundProperty`, `GetWallTileStack(x,y)` (`AlundraCellStore.cs:148-176`) | Precedent: `AlundraInoaDayOneArcTests.cs:161-163`. No "frame index" accessor exists. |
| Which frame a `0x85` applied | Compare the destination cells to the source rectangle's cells (sources are untouched) | Or `AlundraCellStore.CellsMutated` (event of touched cell indices, `:76`) with `arc.Frame`. |
| Sound request id | Inject a recording `IAlundraSoundPlayer` after the constructor: `typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", NonPublic\|Instance).SetValue(arc.Proxy, player)` | Precedents: `AlundraHeroAttackTests.cs:395-401`, `AlundraAnimationSoundTests.cs:61-66`. |
| Control lost / regained | `ArcRun.State.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked` (0x04) | `0x10` sets it, `0x11` clears it (`AlundraEventProgramRunner.cs:628-634`). Precedent: `AlundraTarnMurggArcTests.cs:51`. |
| Hit and spark count | `arc.Hero.HitCounter`; `lever.TouchingEntity`; `lever.FrameCollisionTickCounter` (25 after a hit); `arc.Proxy.HitSparksForTests = (x,y,z) => ...` (`AlundraWorldProxy.cs:2784`), one call per hit; 3 sparks per hit (`AlundraNativeEffects.HitSparkCount`); reservoir `arc.Proxy.EffectStage!.Reservoir.Slots.Where(s => s.Active && s.TargetEffect == 9)` | `AlundraHitArena.cs:64`; `AlundraHitPassTests.cs:160,366`. |
| Which program ran | `arc.Trace` / `OnInstruction` (`ArcInstruction.Frame/Slot/ProgramStart/Pc/Opcode`) | Slot constants `ScriptHelper.cs:16-21` (A=0, B=1, C=2, D=3, E=4, F=5). `AlundraFireSwitchRegressionTests.cs:69` filters by `ProgramStart`. |

Notes on the sound row:
- FACT: the headless proxy's `SoundPlayer` is null (`AlundraWorldProxy.cs:520`). Every `PlaySfx` is a no-op until injected.
- FACT: `RecordingSoundPlayer` (`AlundraJumpTestSupport.cs:192`, internal) records ids only. The frame-stamped one, `StampedSoundPlayer`, is private (`AlundraAnimationSoundTests.cs:36`).
- Sounds you will see:
  - Opcode `0xBD` sends `(v2<<8)|v1` (`AlundraEventProgramRunner.cs:1242`), so 221, 222 and 224 come through as written.
  - FACT: the hero swing asks sound 29 (anim set 16 `Sfx` 29, `Data/sprite-records.json`).
  - FACT: a lever asks 218 when it enters anim 2 or 3 (the sprite 49 anim sets). The bascule (sprite 52) asks 218 for anim 1 and 2. This comes from the animation-change sound rule (`AlundraAnimationSoundTests.cs:22-26`).
  - Assert with `Contains` plus the frame, not `Equal` on the whole list.
- Seams that save and replace each other: `arc.OnInstruction` and `arc.OnFrame` are single delegates. `ArcSamples` overwrites them. Chain the previous delegate if you add your own (the pattern is in `AlundraEntityContactArcTests.cs:60-80`).
- Frame numbering: `ArcInstruction.Frame` is the frame during which the instruction ran. The `OnFrame` callback sees `arc.Frame` already incremented, so a flag sampled in `OnFrame` is keyed one higher (`AlundraEntityContactArcTests.cs:84-86`).

Missing accessors: none that need production changes. Optional test-only additions, all under `Alundra.Tests`:
- A shared frame-stamped sound recorder: promote `StampedSoundPlayer` and `Inject` into `AlundraArcSupport.cs`.
- A cell-tuple helper that reads all five getters plus the wall stack for a rectangle.
- A world-index lookup helper (map id to zone and world), or constants.

## 5. Cost and known loadability

- **Evidence on cost.** FACT, from `docs/plan-e19-opcodes.md`: arcs of 5100 frames (T-A10v) take 0.5 to 0.8 s in Release (`:12155`). New arcs of E19.d2c1 run each under a second (`:12169`). The full suite took 30 s at 2228 tests (`:12155`, `:12165`). The E20 plan records 3480 passing tests at c1b-1 (`docs/plan-e20-heros.md:1215`).
  - Construction plus first frame on map 478 took 993 ms (`docs/plan-e19-opcodes.md:1529`).
  - These timings predate E20.a and the per-tick hit pass. UNKNOWN: today's cost. The decider is timing one 115 arc.
  - FACT: the lever programs need far fewer than 1000 ticks (the longest waits are 20 ticks x 3, 8 ticks x 5 and 5 ticks x 4), so a few thousand frames is not a problem.
- **Known to load:** FACT: only 115. `AlundraHeightTargetSitesTests.cs:73-92` runs `s115_rec19` through `ArcSpec` with `RealController: true, Prefabs: true` and `Arrival` at (10,55), with a 6000-frame limit and the dialogue button held.
- **Not tested on these maps:**
  - 116, 117 and 44 have no `ArcRun` test.
  - Map 44 appears only as `LoadRealCompanion("Wendels Nightmare-44")` in `BackdropStagePsxSemiTransparencyTests.cs:37`.
  - 14, 15, 44, 115, 116, 117 and 362 are listed as exempt combat maps in `AlundraStoryChainOpcodeAudit.cs:44` (`AlundraStoryChainSkippedOpcodesTests.cs:78-80`). That audit parses programs only and never builds a world.
- **Loading 116, 117 and 44** is UNKNOWN. The data is complete (§1.1), and the one risk I can name is 44's backdrop, which has `cellsheet0` plus `layer1`. That is not new to arcs: maps 478 and 363 also have a cellsheet and are arc-tested. The decider is a smoke test: construct, run 6 frames, `AssertNoUnexpectedError(arc)` (`AlundraArcChecks.cs:61-67`, which fails on any "falling back to a bare entity").
- **The noise:** the prefab mode logs thousands of "can't resolve sprite" errors (6463 on map 478). The assert tolerates those.

## 6. Conventions and guards

- **Files and format:** `Alundra.Tests/Alundra<Subject>Tests.cs`, `#nullable enable`, file-scoped `namespace Alundra.Tests;`, `public sealed class`, CRLF line endings. FACT: `file` shows CRLF on `AlundraHitArena.cs`, `AlundraFireSwitchRegressionTests.cs` and `AlundraArcSupport.cs`.
- **Collection and traits:** `[Collection(AlundraMusicPlayerSingletonCollection.Name)]` on the class (61 files do this). No `[Trait]` or `[Category]` exists anywhere in the project. FACT: `xunit.runner.json` has `parallelizeTestCollections: false`.
- **Test names and headers:** names look like `Method_Condition_Expected`. Class headers cite the plan section and decision ids, and say "every expected value is written by hand before the code".
- **Disposal:** `using var arc = new ArcRun(spec);`. `ArcRun.Dispose` (`AlundraArcSupport.cs:295-316`) restores `ProjectPath`, resets singletons and re-checks guards. `HitArena : IDisposable` just disposes its arc (`AlundraHitArena.cs:116`).
- **Guards to know about:**
  - `ArcsThatSlide` (`AlundraArcSupport.cs:288`): every `RealController` arc must end with exactly the registered slide count (0 if absent). Dispose throws otherwise. A stationary hero slides 0 times. If an arc walks the hero near corners, register its name there (a test-support edit).
  - `ArcsWithoutEntityContact` (`:278`) lists the old pinned arcs. A new name is not in it.
- **`HitArena` is hard-wired to map 14** (spec at `AlundraHitArena.cs:60`, Beannoid assert at `:74`). For levers use `ArcRun` directly, or give the arena a spec parameter. The Beannoid-specific `Snapshot` and `Targets` are not needed.
- **Arc naming:** use a unique `Name` like `"E20c1b2-115"`. It appears in failure messages.
- **`FrameLimit`:** required, and only enforced by `RunUntil` (`:429-440`). Suggested limits (INFERENCE, to pin after the first run): 1500 for 115, 116 and 117; 3000 for 44.
- **Reference traces that must stay byte-identical** (plan `docs/plan-e20-heros.md:366`, acceptance `:1071,1195`):
  - `docs/hero-trace-389-spawn-fixedstep.txt`
  - `docs/hero-trace-389-spawn-freestep.txt`
  - `docs/hero-trace-389-highground-fixedstep.txt`
  - `docs/hero-trace-389-highground-freestep.txt`
  - `docs/intro-trace-389.txt`
  - `docs/intro-programs-389.txt`
  - FACT: `HeroTraceHarnessTests.cs:694-696` and `IntroTraceHarnessTests.cs:94-98` rewrite them on every test run.
  - FACT: `git status --short` on those six paths prints nothing at the time of this report. The session-start snapshot listed the four hero traces as modified, which is the LF rewrite showing up. They are not part of this slice and must not be staged.
  - Verification after a test run: `git diff --ignore-cr-at-eol` on those paths, and restore with `git checkout` if only line endings differ (plan convention in `docs/plan-e19-opcodes.md`).

## 7. Anything that would touch the engine (`CasaEngineMonogame`)

Nothing in c1b-2 requires it. The slice is test-only plus the arc scaffolding:
- The cell store, the flags, the entity ids, the control bits and the sound seam are all DLL-side.
- `0x85` already runs inside `ArcRun` on maps 389 and 165 (`AlundraInoaDayOneArcTests.cs:161`).
- The sound capture uses reflection on the DLL's own proxy.
- Remaining opcodes: after c1b-1, the only skip on the lever paths is `0xB9` (117 B[1] at 171). I checked that the bascule hero position does not reach it.

Stop conditions, each UNKNOWN until a smoke run:
1. A prefab on 116, 117 or 44 that needs a headless loader beyond `ArcPrefabAssets` (`Entity`, `Animation2dData`, `UIScreenAsset`; `AlundraArcSupport.cs:635-638`). Extending test loaders is test code. A loader that needs a `GraphicsDevice` would be a stop.
2. An assertion on the drawn door or bridge tiles. In arcs the tile map uses stub tiles, so only the cell store is observable. Visual gids would need engine internals.
3. Any need to change `CasaEngine.Launcher/Program.cs`. Never touch it.

## 8. Corrections and open points

- **Corrections to `faits-leviers-opcodes-dll.md`:**
  - §1 tiles are `DisplayX/Y`. The runtime positions are in §1.3. Examples: 116 rec 39 is tile (36,26), not (35,26); 117 rec 0 is (22,12), not (21,11); 44 rec 12 is (39,25), not (38,25).
  - §2 "Direct placement" for 115, 116 and 117 needs the zone-matched arrival (§0.1). The facts file's entry routes for the bascule (116 portal 0, 115 portal 7) do not spawn it.
- **Doc:** the E20 plan §1.1 row for c1b-2 and section 1.7 do not yet exist. The executor writes them.
- **UNKNOWN, with deciders:**
  - Does 116, 117 or 44 build in `ArcRun`? Smoke test per §5.
  - Is the contact at the press tick for each lever at 22 px, west or east? Sample `Hero.HitCounter`, `lever.TouchingEntity` and `lever.FrameCollisionTickCounter` at the end of the press frame.
  - Do the stand points hold (no drift, `IsOnGround == 1`)? The settle assertions.
  - Current arc run time. Time one arc.
  - Is the 44 first-arrival flow (B[1] scene area x 4..7, y 57..59) reachable before the Beannoide wave? Not an arc question; the facts file already lists it as UNKNOWN.

Skeleton for one lever arc:

```csharp
var spec = new ArcSpec("E20c1b2-115", "Tarn's Manor", "Tarn's Manor-115", Array.Empty<int>(), 0, 0, 0, 1500,
    RealController: true, Prefabs: true,
    Arrival: new ArcArrival((4 * 24 + 12) << 16, (7 * 16 + 8) << 16, 0, AlundraGameState.ResetAnimationId, 8));
using var arc = new ArcRun(spec);
var sounds = InjectSounds(arc);                 // reflection on <SoundPlayer>k__BackingField
var lever = arc.EntityByRecord(2); Assert.NotNull(lever);
arc.PlaceHero(82, 120, 16); arc.Hero.TargetDirection = 8;
for (var i = 0; i < 6; i++) arc.OneFrame();     // settle; assert IsOnGround == 1 and PlayerControlFlags == 0
var pressFrame = arc.Frame;
arc.Press(AlundraPadState.Square);              // end of this frame: HitCounter == 1, lever.TouchingEntity != null
arc.OneFrame();                                 // lever D (slot 3) runs: 0x2B then T2 on
```

Scratch scripts (mine, not in the repository): `dump_records.py`, `tilemap_records.py`, `codes.py`, `disasm.py`, `cells.py`, `ground.py`, `prefab_check.py`, `spawn_callers.py` in the scratch folder named at the top.
