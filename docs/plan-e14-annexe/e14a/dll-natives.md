> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E14.a), kept verbatim as evidence. The plan holds the reconciled facts.

# E14.a, DLL side: how native sprite handlers run today, and what E14.a must add

Label e14a-dll, read-only. Nothing was built, run or written in the repository. My scratch files are in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/e14a-dll/`.

Conventions: `DLL:` means a path under `Alundra/Scripts/`. `EXE` means `ALUN_CD.EXE` (France), checked by capstone or a byte dump. `DECOMP` means the analyser's `Gameplay/Scripts/*.cs`, which is read-only. FACT, INFERENCE and UNKNOWN are marked.

---

## 0. Things that change the plan

1. **E14.a also needs the native Load handler A1, not only C, D and E.**
   - FACT: the Beannoïde header codes (A,C,D,E,F) are (1,1,1,1,0). The Muruta are (0,10,9,1,0). Data: `alundra-project/Data/sprite-records.json`.
   - FACT: the native A table is at EXE `0x800C4B34`. A[0]=`0x8006174C` writes `AIValues[0] = PosZ>>16` (`lh 0x11E; sh 0x280`). A[1]=`0x80061758` writes `+0x88 = 2` (TargetAnimationId).
   - FACT: today slot A native is a counted no-op, because `RunPickedEvent` sends any slot with `ProgramIndexes[slot]&0x7f==0` to `RunSpriteEvent` (`DLL:AlundraEntityScriptProxy.cs:1624-1630`) and `RunSpriteEvent` only knows the save book and E0/E1.
   - INFERENCE: without A1 a Beannoïde spawns in animation 0 and acts awake. In the binary it starts in animation 2, and C1 case 2 wakes it (animation 4) when the hero is within 4 tiles (DECOMP `FunctionTypeC.cs:190-195`).
   - The oracle notes confirm that the c1 oracle never ran A (`e20-c1/oracle/notes.md` §2: "Program A (load) never runs").

2. **The hall Muruta are not native at spawn.**
   - FACT: records 24-27 of map 115 have programs (A..F) = (0,0,153,0,0,0). C=153 is the scripted C program 25 (table `EventCodesCTable[25]=1464`). It reads `2B` (clear ClassA), `25` (wait for landing), `37 [20]`, `2A` (set ClassA), `40 [2,0]`, `FF`.
   - So `0x40 [2,0]` (ported, `DLL:AlundraEventProgramRunner.cs:807-820`) hands the entity over to native C10 after the fall. D and E are native from the start.
   - FACT: all four C programs checked on 115 (@1296, @1360, @1400, @1424 and @1464) end the same way, with `40 [2,0]`.
   - Consequence: today the Muruta fall, then stand inert and untouchable, because D9 is a no-op, and the `0x2C` wait at `@414-431` never ends.

3. **Dispatch must stay gated by sprite type, as the save book is.**
   - FACT, corpus census of enabled `Entities` records whose slot is native:
     - A1 is used by sprite types 335 (Beannoïde, 120), 329 (Slime gélatineux, 118), 401 (Homme-insecte, 84), 410 (Ver, 76) and 395 (Tête de roche, 4).
     - D1 is used by 335 (130) and 329 (118).
     - C1 is used only by 335 (105).
     - C10 and D9 are used only by 347 (28 and 198 records).
   - FACT: the Slime gélatineux header is (A,C,D,E,F)=(1,26,1,1,0).
   - INFERENCE: a registry keyed only on (slot, code) would give 118 slimes A1 and D1 (anim 2 at load, then HP loss and `Bytes[3]=1`) without their C26, which handles death. They would never die. See AD-1.

4. **The existing c1a and c1b tests assume the Beannoïde natives are inert.**
   - FACT: `Alundra.Tests/AlundraHitArena.cs:65` wraps the runner in `RecordingRunner`, whose `RunSpriteEvent` forwards to the real runner (`:267-271`). The class doc says the Beannoïde C and D are "counted no-ops, as the oracle's stubs" (`:23`). The same assumption is in `AlundraHitPassTests.cs:229, 345, 368`.
   - FACT: the c1 oracle hooks all 201 non-null handler pointers with a journalling no-op (`e20-c1/oracle/notes.md` §2).
   - Consequence: once C1 and D1 are live, the arena's seven real Beannoïde (map 14, sprite 79) wander and react, and the tick-by-tick oracle comparison breaks. Either make the arena's `RecordingRunner` not forward `RunSpriteEvent`, or add a natives-off switch.
   - FACT: the in-progress c1b-2 lever arcs on 115, 116, 117 and 44 (untracked `AlundraLeverArcSupport.cs` and `AlundraLeverSmokeTests.cs`) run on maps that contain Muruta or Beannoïde. They get the same baseline change.

5. **The decompilation's tables are a trap for the executor.**
   - FACT: C1 case 1 and C10 use "g_directionFlipTable". In EXE they read 32 bytes at `0x80028B34` (`lbu` at `0x800660AC-0x800660C4` in C1, `0x800683A4-0x800683AC` in C10).
   - FACT: the bytes are `idx0=0x18, idx1..8=0x00, idx9..16=0x08, idx17..24=0x10, idx25..31=0x18`.
   - The cardinal table `0x80028B54` is `00 10 08 18`, which equals `AnimationTables.CardinalDirectionTable` (`DLL:AnimationTables.cs:20`). C10 reads it at `0x80068388-0x80068390`.
   - FACT: DECOMP writes `ScriptHelper.DirectionTable[entity.TargetDirection]` (DECOMP `FunctionTypeC.cs:177`). The DLL has a private `ScriptHelper.DirectionTable`, a 16x16 octant table whose doc claims address `0x80028b34` (`DLL:ScriptHelper.cs:23-29`). Its first 32 entries are not the bytes above. Using it would be wrong, and its doc address is wrong.

6. **`HandleAnimationDirection` (used by C10 case 0xC) reads `MapHeights[0..3]`, which the DLL never writes.**
   - FACT: `DLL:AlundraEntityScriptProxy.cs:381` declares `MapHeights` (binary +0x158). The only other uses are in `Clone` (`:2521`) and a clone test. No production code writes it.
   - So the helper needs the four per-corner terrain heights, whereas the DLL only keeps the 4-corner max (`ComputeTerrainHeight`).

7. **The break effect and the loot are not in the DLL's data.**
   - FACT: `data-extracted/data/map_*.json` `SpriteInfo.SpriteRecords[i].Header` has `BreakEffect` and `Contents`. Values: Muruta (map 115 idx 91) BreakEffect 8, Contents 251; Beannoïde (map 14 idx 79) BreakEffect 8, Contents 252.
   - FACT: `alundra-project/Data/sprite-records.json` has `Contents` but no `BreakEffect`.
   - The E1 destroy (`DestroyEntity(e,-1)`, EXE `0x8003A59C`) is ported without effect or loot (`DLL:AlundraWorldProxy.cs:3022-3026`). This is accepted debt (O-E19-23, D-E20-17). E14.a can either keep it or touch the converter and export.

8. **ComputeNewHp is nearly ready, but the DLL has no production caller. The world seam lacks the hero animation latch.**
   - FACT: `AlundraDamage.ComputeNewHp(target, sheet, equipment, animationLatch)` is `internal static` (`DLL:AlundraDamage.cs:76-81`). Its doc says "No production code calls them yet" (`:7`).
   - FACT: the runner holds an `IEntityWorldContext`, which has `EquipmentRecords` (`DLL:IEntityWorldContext.cs:114`) but no hero animation latch. The latch lives on `AlundraWorldProxy.HeroAnimationLatch` (`DLL:AlundraWorldProxy.cs:2781`, internal) and is reachable neither through the context nor through `IAlundraScriptHost`.

9. **There is no per-slice E14 plan yet.** FACT: `docs/` has no `plan-e14*`. The only E14 text is `plan-conversion-totale.md:741-753` and the E14.a row at `plan-e20-heros.md:464`.

---

## 1. How the DLL runs native handlers today

### 1.1 Dispatch chain, tick level

1. FACT: `AlundraWorldProxy.RunEntityBlock` (`DLL:AlundraWorldProxy.cs:2655-2729`) runs per tick: hero `MovePlayer`, then `TickPrograms()` for every entity, then the pending-trigger re-scan, counters, lists, `BuildHitList`, animation clock, physics, effects slot, hit pass.
2. FACT: `TickPrograms()` (`DLL:AlundraEntityScriptProxy.cs:1421-1430`) skips the hero, then calls `PickEventTrigger()` and `RunPickedEvent(host.Runner)`.
3. FACT: `PickEventTrigger` (`:1519-1608`) chooses the slot:
   - Loaded gives A, and the status becomes Normal.
   - Normal: the E branch comes first, for `DeactivateOnImpact`, `DeactivateOnHit` (with `HitCounter != 0`) or `DeactivateOnAnimationEnd` (with `ForceResetAnimationFlag != 0`). The status becomes Deactivated (`:1561-1571`).
   - Otherwise D if `TouchingEntity != null`, else C, else F if it is the active collision entity (`:1572-1596`).
   - Deactivated gives E.
4. FACT: `RunPickedEvent` (`:1617-1637`) takes `ProgramIndexes[EventTrigger] & 0x7f`. If 0 it calls `runner.RunSpriteEvent(this)`, else `runner.RunScript(this, slot)`. It then sets `EventTrigger = -1`.
   - EXE equivalent (verified): the events pass at `0x800388F8-0x80038930`:
     - `[entity+0x4C+4*slot] & 0x7F == 0`: native, `table[slot][ [entity+0x70+4*slot] ]`.
     - Otherwise: the bytecode runner `0x8004205C`.
     - The tables are reached through the pointer array at `0x80098F4C` = {A `0x800C4B34`, B 0, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`}.

### 1.2 `RunSpriteEvent` today

FACT, `DLL:AlundraEventProgramRunner.cs:293-330`:
- `SpriteEventRunCount++` on every call. The slot is read from `entity.EventTrigger` and the code from `entity.SpriteProgramIndexes[slot]`.
- Branch 1 (`:297-310`): `AlundraSaveBook.TryMatch(entity, out slot)`. It holds for sprite type 237, native slot, and (F, code 1) or (C, code 72). It runs `AlundraSaveBook.Instance.RunInteract` or `RunTick(entity, _worldContext, _gameState)` and counts `SaveBookEventRunCount`.
- Branch 2 (`:315-321`): `EventTrigger == E && (ProgramIndexes[E]&0x7f)==0 && (uint)SpriteProgramIndexes[E] <= 1` calls `entity.ScriptHost?.DestroyEntity(entity, -1)` (E19.d2b, D-E19-29).
- Everything else logs one debug line for the whole session (`_loggedSpriteEventOnce`, `:150, 323-329`) and does nothing.
- `RunSpriteEvent` has no sprite-type or code table. A "counted no-op" is just the counter plus that single log.

### 1.3 Natives already ported

| Native | Where | Notes |
|---|---|---|
| E[0], E[1] = `DestroyEntity(e,-1)` (EXE `0x8007ED10`) | `DLL:AlundraEventProgramRunner.cs:312-321` | Generic: the table maps both to the same function, so it is not sprite-type gated. No loot or effect (O-E19-23). The 2-argument host overload only sets FlagToDestroy (`DLL:AlundraWorldProxy.cs:3022-3026`). 1,538 enabled records have E code 1 (census). |
| Save book F[1] (`0x8007FC64`) and C[72] (`0x8007B998`) | `DLL:AlundraSaveBook.cs:64-434` | Sprite type 237 only. |
| Bomb E12 (`0x8007F27C`) | none | grep finds no bomb handler in `Alundra/Scripts`. |
| Anything else | none | `SpriteProgramIndexes[...]` is read only by `RunSpriteEvent`, the pick (F test, `:1581`) and `AlundraPlayerManager.cs:948`. |

### 1.4 The save-book pattern (the only precedent)

- FACT: one process-global class `AlundraSaveBook.Instance` (private constructor, `:67-71`) plus a static `TryMatch` (`:136-152`).
- FACT: handlers are methods taking `(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state)` (`:173, 196`).
- FACT: per-entity state sits in the proxy's own binary fields:
  - the state int is `Bytes[0..3]` read little-endian (+0x274, `:156-165`);
  - the wait counter is `DelayOrAngleOrEntityId` (+0x278);
  - session-global flow state (`_ownedOpenSerial`, `_choiceOpened`) is class fields, which only works for one book at a time.
- FACT: it reaches the world through `IEntityWorldContext`: `PlayerEntity`, `WorldName`. It uses singletons (`AlundraDialogueDirector.Instance`) and a test seam (`Screen`, `ResetForTests`).
- FACT: it corrects proven defects of the original and documents each (H2, SE1-SE8, `:48-62`).
- Planning precedent: `docs/plan-e16-etat-partie.md` §H4/J6/L1 (`:1499, 1592, 1600-1609`) chose sprite-type-gated dispatch because code F1 is shared by entities whose C is not ported. T1 (`:1767-1779`) built dispatch tests (`AlundraSaveBookDispatchTests`): the right type reaches the handler, other types stay counted no-ops.
- The E16.e port was tested by transitions, mutations (8 killed) and an end-to-end test. It had no MIPS oracle.

---

## 2. Seams E14.a plugs into

### 2.1 Entity fields the handlers use (EXE offset to DLL field)

| EXE | DLL field | Source |
|---|---|---|
| `+0x14 / +0x18` HP / MaxHp | `Hp`, `HpMax` | `AlundraEntityScriptProxy.cs:70-71`. Set to `record.MaxHp` by `InitializeBalance` (`AlundraEntityScriptProxy.HitPass.cs:56-76`), called at the three creation sites (`AlundraWorldProxy.cs:894, 1978, 3116`). |
| `+0x1C4` balance sheet | `BalanceRecord` | `HitPass.cs:22` |
| `+0x1C8` current attack | `CurrentAttack` (`.Attribute`, `.Power`) | `HitPass.cs:25`. Reloaded at each animation switch the clock sees. |
| `+0x1CC / +0x1D0` | `DamagedTickCounter` / `FrameCollisionTickCounter` | `AlundraEntityScriptProxy.cs:411-412`. `+0x1D0 = 0x19` is the 25-tick cooldown after a hit. |
| `+0x1D4` frame box present | `HasFrameBox` | `HitPass.cs:31`. Refreshed by `StoreHitBox` after every tick's physics (`AlundraEntityScriptProxy.cs:1458`). |
| `+0x220 / +0x224` | `HitCounter` / `TouchingEntity` (an engine `Entity`) | `:426-427` |
| `+0x88 / +0x8C` | `TargetAnimationId` / `TargetDirection` | `:120-121` |
| `+0x9C` direction row | `AnimationDirection` | `:124`. Written only by `SyncAnimation` (`AlundraFrameSyncPasses.cs:143`). |
| `+0xAC` hold flag | `ForceResetAnimationFlag` | `:129`. Raised by the animation-end bridge (Hold end, `AlundraEntitySpawnFactory.cs:319-322`). |
| `+0x13C / +0x140 / +0x144` | `ForceAdjusted` / `CollidedWithEntityZ` / `IsOnGround` | `:359, 378-379` |
| `+0x274` bytes, `+0x278`, `+0x27C`, `+0x280` | `Bytes[4]`, `DelayOrAngleOrEntityId`, `ItemState`, `AIValues[10]` | `:1129-1132`. All copied by `Clone`. |
| `+0x138` TerrainHeight, `+0x158` MapHeights | `TerrainHeight`, `MapHeights` | `TerrainHeight` is written each tick for NPCs (`:791`) and the hero (`:1997`). `MapHeights` is never written. |
| `+0x114/118/11C` pos, `+0x120/124/128` tile | `PosX/Y/Z`, `TileX/Y/Z` | Kept by `RunOneKinematicTick` (`AlundraScriptedMotion.cs:552-554`). |
| `+0xC` parent | `ParentEntity` (an `Entity`, so `.GameplayProxy.Bytes[3]` for C10's map-0x99 branch) | `:68` |

INFERENCE on the "+0x1D8" hint in the brief: by the proxy field order, `+0x1D8` is `ModdedPosX`, not an HP field. The HP fields are +0x14/+0x18.

### 2.2 Damage seam (c1b-1)

- FACT, `DLL:AlundraDamage.cs:76-81`: `ComputeNewHp(target, sheet, equipment, latch)` reads `target.TouchingEntity?.GameplayProxy as AlundraEntityScriptProxy`, then its `CurrentAttack`. It sets `target.Hp` through `ResolveBalanceTarget` and returns `Hp == 0`.
- To call it from a D handler the runner needs:
  - the sheet: `entity.BalanceRecord` (available);
  - the equipment: `_worldContext.EquipmentRecords` (available, `IEntityWorldContext.cs:114`; the world returns its own at `AlundraWorldProxy.cs:304`);
  - the latch: missing from the context (point 0.8).
- EXE (verified): D1 `0x8007D9A4` and D9 `0x8007DE04` are each 25 words. They call `ComputeNewHp` (`0x8003A374`). If the result is nonzero they set `sb 1, 0x277` (`Bytes[3]`). Then `+0x88 = 5` (D1) or `2` (D9), and `+0x8C = GetDirectionToTarget(entity.PosX - hero.PosX, entity.PosY - hero.PosY)`, using the hero at `0x80127D30` (`+0x114/+0x118`). This equals DECOMP `HitCommon(…, 5)` and `HitCommon(…, 2)` (`FunctionTypeD.cs:414-423`).
- FACT: `ScriptHelper.GetDirectionToTarget(int dx, int dy)` already exists and equals the binary routine `0x8002E814` (`DLL:ScriptHelper.cs:71-132`).
- EXE: the Muruta responses and HP come from `Data/balance.json` through `AlundraBalanceTables.Select(347, threshold)`. D3 report: L1, HP 3, power 1; map 115 has `Info.D = 0`, so level 1 is chosen.

### 2.3 Destroy, recycling and `0x2C`

- FACT, destroy: `IAlundraScriptHost.DestroyEntity(entity, effectId)` (`DLL:IAlundraScriptHost.cs:35`, implemented at `AlundraWorldProxy.cs:3022-3026`) sets `Status = FlagToDestroy` and logs. It does not clear `EventTrigger`. The 1-argument world overload (`:3038-3043`) also clears `EventTrigger` and backs `0x2E`.
- FACT, recycling: `RecycleDestroyedEntities` (`:2990-3012`) runs right after each tick's map-events pass. It does nothing while `GameplayBlockedMask` is set. For each FlagToDestroy entity (never the hero) it sets `Status = Destroyed` and `EntityRefId = -1`, removes it from `_spawnedEntities` and calls `_world.RemoveEntity`.
- FACT, `0x2C` (`DLL:AlundraEventProgramRunner.cs:1900-1909`):
  - `Result = (EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], …).Count == 0) ? 1 : 0`; size 2.
  - For a raw id (< 0x80) the match is `candidate.EntityRefId == v[1]` over `SpawnedEntities`, gated only on the reference entity (the logic entity, the hero in map events) being Loaded, Normal or Deactivated (`EntitySearchService.cs:104-118`).
  - It does not test the candidate's status. A FlagToDestroy corpse stays found until recycling, then disappears because its id becomes -1.
- Timeline of a kill, by the code (INFERENCE from the passes above):
  - hit pass at tick N writes `TouchingEntity`;
  - at tick N+1 the pick chooses D and D9 runs `ComputeNewHp`;
  - the C10 death sequence (case 5, then `Flags |= DeactivateOnAnimationEnd`) ends on the Hold end;
  - the pick goes to E, E1 flags the entity;
  - recycling removes it after a map-events pass;
  - the hall's wait program `@413 Break / @414-431` sees `Result 1`.

### 2.4 Movement, animation and collision

- FACT: a chasing or hopping enemy needs no new mover. It writes `TargetDirection` and `TargetAnimationId`. `RunOneKinematicTick` (`DLL:AlundraScriptedMotion.cs:457-555`) derives `TargetForce = OffsetXY[dir] * AnimSet.Speed`. Acceleration is the low nibble of the set's byte 0xD.
  - Force steps use `IncrementForce` (`:746`).
  - The step goes through `MoveControllerAndPullPosition` (controller `Move`, entity blocking) and `SlideAlongWall` (`:574-685`), which sets `ForceAdjusted`.
  - Handlers also zero `ForceX/Y`, `TargetForceX/Y` and `ForceStepX/Y` directly (C10 cases 0xC and 0; DECOMP `FunctionTypeC.cs:9254-9262`).
- FACT: the animation switch is applied to the sprite only at the end of the frame (`SyncAnimation`, D-E19-13). A switch the script asks for in tick N feeds the box cursor and the attack at tick N (`NoteSwitchPendingAtTick`, `HitPass.cs:120-135`). But the NPC mover reads `CurrentAnimationId` (`AlundraScriptedMotion.cs:165-168`), which stays at its old value until the frame ends.
  - INFERENCE: in a frame with 2 or more ticks, an NPC keeps the previous animation's speed for the ticks after a switch. In EXE the clock pass `0x80038E18` precedes physics `0x80038364` in the same tick.
  - This is an oracle-visible difference for movers (D-E19-13 / ADR-0019 territory), see AD-4.
- FACT: `AnimationDirection`, `AnimFlags` and `CurrentAnimationId` are also end-of-frame values. `TryAttackPlayerFront` reads `AnimationDirection`, so the same caveat applies (INFERENCE).
- FACT: obstacle checks that exist: `AlundraCellsCollisionField.TrySampleGround` and `WalkabilityMaskFor` (`AlundraScriptedMotion.cs:693-717`), `AlundraTerrainProbe.SampleGroundCorner`, `ComputeTerrainHeight`, `EntitySupport`, `AlundraEntityCollision`. None returns the four corner heights separately.
- Helper functions C1 and C10 call in EXE (jal targets, verified): C1 calls `0x8002E814` (x2), `0x8007FE20` (relative position), `0x8007FEF8` (StartFlying). C10 calls `0x8002E814` (x3), `0x8003A59C` (DestroyEntity), `0x800490FC` (sound, x3), `0x8007FE20`, `0x80080144`, `0x8008048C` (TryAttackPlayer2), `0x800805C8` (TryAttackPlayerFront, x3). A1, D1, D9 and E1 call only what is above.
  - FACT: none of `0x8007FE20`, `0x8007FEF8`, `0x80080144`, `0x8008048C`, `0x800805C8` exists in the DLL (grep for `CalculateEntityRelativePosition`, `StartFlying`, `TryAttackPlayer` in `Alundra/Scripts` finds nothing).
  - DECOMP (reading aid) places them in `EntityGameplayManager.cs:96-554` and `ScriptHelper.CalculateEntityRelativePosition`.
  - The pure ones (relative position, TryAttackPlayer, TryAttackPlayer2, TryAttackPlayerFront) read only `TileX/TileY/TerrainHeight`, `AnimationDirection` and thresholds. `0x80080144` and its wrapper `HandleAnimationDirection` need `MapHeights`.
- FACT: C10 reads the hero's `FrameCollision`, `CurrentAttack` and `CurrentAttack.AttackAttribute` (DECOMP `:9292-9295`). DLL equivalents: `PlayerEntity.HasFrameBox` and `CurrentAttack?.Attribute != 0`.
  - Timing equivalence: native C runs in the programs phase, before the clock and physics of the tick. In both EXE and DLL it sees the previous tick's end state. INFERENCE.

### 2.5 RNG

- FACT: the DLL has the same generator. `AlundraRandom.RandomSeed` (static `ulong`, initial `0xB017C93D`) with `Next()`: `RandomSeed = RandomSeed * 0x7d2b89dd + 0xe06a02e7`, returns the low 32 bits (`DLL:AlundraRandom.cs:21-35`).
- FACT, EXE: state `0x80098708` (`lw -0x78f8($a0=0x800A0000)`). The per-frame advance is at `0x8002BD0C-0x8002BD34`. The same inline pattern (`lui 0x7d2b`) appears at 300+ sites, 8 inside C10 (`0x80068238 … 0x80068754`) and 3 inside C1 (`0x80065F4C, 0x80065F74, 0x80066088`).
- FACT: the DLL does not advance it per frame (E20.e0 row; ADR-0053; `AlundraWorldProxy.cs:2766-2772` leaves the pass empty). Existing users: opcodes `0x0C`, random-branch `0x?? ` at `AlundraEventProgramRunner.cs:1478`, random-direction modes (`:3382, 3385`), `AlundraNativeEffects`.
- FACT: it is a process-global static. Tests seed it and run in `AlundraRandomStaticStateCollection` (`AlundraRandomOpcodeTests.cs:15`, `AlundraNativeEffectTests.cs:20`). The existing oracles do not model the per-frame advance (c1 notes §3 and §7.7).
- INFERENCE: handlers draw `(uint)Next() * n >> 32`. There is no `Below(n)` helper in the DLL.

### 2.6 Sound, effects and world access from a handler

- FACT: sound: `IEntityWorldContext.SoundPlayer?.PlaySfx(id)` (`IEntityWorldContext.cs:108`). C10 plays sound `0xCD` at 3 sites.
- FACT: effects: `IEntityWorldContext.EffectStage` plus `AlundraEffectStage.Emit(Action<AlundraEffectReservoir>)` (`AlundraEffectStage.cs:190`) and `AlundraNativeEffects` (`CreateHitSparks`, `CreateLifeVesselEffects`). C1 and C10 create no effect (DECOMP).
- FACT: hero: `context.PlayerEntity` (can be null in fakes, so handlers must degrade).
- FACT: host-level 2-argument destroy and tables come from `entity.ScriptHost` (`IAlundraScriptHost`).
- FACT: the DLL exposes no current-map-id accessor to handlers other than `IEntityWorldContext.WorldName` (suffix `-{id}`, `:208`). C10's `g_currentMap == 0x99` branch (EXE `0x800DC5A0`, `0x80068170`) reads map 153.
  - FACT: the census shows no Muruta record on map 153, so that branch is dead for the exported corpus. The `ParentEntity` bytes it reads are `ParentEntity.GameplayProxy.Bytes[3]`.

---

## 3. What the six handlers need (EXE verified, `capstone`)

| Handler | EXE | Size | Calls | Needs from DLL | In DLL? |
|---|---|---|---|---|---|
| A0 | `0x8006174C` | 3 words | none | `AIValues[0] = (short)(PosZ >> 16)` | no (6,722 records use A0) |
| A1 | `0x80061758` | 3 words | none | `TargetAnimationId = 2` | no |
| D1 (Beannoïde) | `0x8007D9A4` | 25 words | `0x8003A374`, `0x8002E814` | ComputeNewHp, hero position, `Bytes[3]`, anim 5 | seam exists, no caller, no latch accessor |
| D9 (Muruta) | `0x8007DE04` | 25 words | same | same, anim 2 | same |
| E1 | `0x8007ED10` | 8 words | `0x8003A59C` (a1 = -1) | | done (branch 2) |
| C1 (Beannoïde) | `0x80065ED4`-`0x8006624F` | 223 words | relpos, StartFlying, direction x2, 3 draws | helpers in 2.4 and the 32-byte flip table | no |
| C10 (Muruta) | `0x80068154`-`0x8006892F` | 503 words | see 2.4, 8 draws | helpers in 2.4, map-153 branch, sound `0xCD` | no |

- FACT, C10: jump table at `0x800271DC` (13 entries, index `+0x88`): animation 0 to `0x800681DC`; 5 to `0x8006889C`; 9 and 10 to `0x800688EC`; 12 to `0x80068800`; all others (1-4, 6-8, 11) to the exit `0x80068904`.
- FACT, C10 exit `0x80068904-0x80068914`: `TargetDirection = (TargetDirection + 2) & 0x1C`, executed on every path except the map-153 destroy. DECOMP shows the same as the shared label.
- UNKNOWN: the exact logic of C10 and C1 per animation case. DECOMP is a reading aid with known losses (it routes many branches through `goto`). The per-case logic must be re-read from EXE before porting. Decisive source: disassembly of `0x800681DC-0x80068903` (C10) and `0x80065ED4-0x8006624F` (C1) with the `0x800271DC` table, and oracle runs.

---

## 4. Corpus census (my script `census.py`, `census2.py`, `census3.py` over `alundra-project/Maps/*/*/tilemap/*.tileMap`)

- FACT: 9,631 enabled entity records. 121 distinct native (slot, code) pairs with a nonzero code (or slot A) are used.
- FACT: sprite type 335 (Beannoïde): 130 records on 18 maps. Of the 130, 95 are fully native (programs all 0). The rest start with a scripted A (133 or 131) or C (130, 131, 146-151, 164) and hand over later.
- FACT: sprite type 347 (Muruta): 203 records on 25 maps. Almost all have a scripted C (129-193) that hands over with `0x40 [2,0]`. A few (map 1, 4 records) also have D scripted (129-132).
- FACT, chain maps:

| Map | Records | Role |
|---|---|---|
| 115 | 11 Muruta (6-9 C135, 12-14 C141/145/146, 24-27 C153) | hall: 24-27 spawned by `0x2D @403-407`, awaited by `0x2C @414-431` |
| 116 | 11 Beannoïde; records 0 and 1 fully native | grate: `A[1]@128` and `C[1]@312-325` use `0x2C [0]` and `0x2C [1]` |
| 117 | 5 Beannoïde | |
| 44 | 14 Beannoïde (17-21 scripted C146-150, 22-28 scripted C151) | |
| 362 | 4 Muruta (6-9 scripted C134-137) | |
| 14 | 7 Beannoïde, all native | author's route; behaviour changes visibly with E14.a |
| 15 | 3 Muruta (0-2, C134-136) | scene Muruta that destroy themselves |

- FACT: E14.a makes the seven map-14 Beannoïde live (A1/C1/D1) on the author's route. The c1a recette text ("sparks, no damage yet") no longer describes it: they would now react to hits, and `Hp` falls.

---

## 5. Existing docs, ADRs and conventions

- E14: `docs/plan-conversion-totale.md:741-753` (E14 section, D-E20-1/3/4/17/18 hand-offs). No separate plan.
- E20: `docs/plan-e20-heros.md:79-83` (D-E20-3, D-E20-4), `:117-121` (D-E20-17/18), `:464` (E14.a row), `:474-478` (order: "E14.a is planned as soon as E20.c1b-1 is done").
- ADRs: 0014 (`docs/decisions/0014-…`, "native-AI hook only covers the SaveBook"), 0052 and 0053 (E20/E14 split and tick order). The next free ADR is 0054.
- E19 open points: `plan-e19-opcodes.md:12325` (O-E19-4, E0/E1 done), `:12327` (O-E19-23, break effect and loot to E14), `:2489` (`0x40` leaves about 200 entities in 40 maps inert until E14, "faithful, native behaviour missing"), `:2589-2593`.
- Conventions for an oracle-backed native port, from E20:
  1. Discovery annex `docs/plan-e20-annexe/<slice>/` with facts labelled FACT/INFERENCE/UNKNOWN, a `check.md` cross-check, and an `oracle/` folder (`tools/*.py`, `out/*.json|txt`, `manifest.txt`, `mutations.txt`).
  2. The MIPS emulator (`emu.py`, `mips.py`, two interpreters compared) runs the real EXE code; run command `python -B …/run_c1.py`.
  3. C# tests find `docs/.../oracle/out/` by walking up from `AppContext.BaseDirectory` and compare tick by tick (`AlundraDamageTests.cs:22-47`). Expected values are the oracle's, never computed by the code under test.
  4. Mutation checks by script (`kmut.py`, `mutations.py`), and arcs on the real prefab (`ArcRun`, `AlundraArcSupport.cs`, `AlundraLeverArcSupport.cs`).
  5. One executor under a contract, then a fresh verifier. Tests: 3,480 in Release and Debug at c1b-1.
- INFERENCE (feasibility of a Muruta/Beannoïde oracle): the c1 oracle already runs the real events pass, spawner and balance on real Beannoïde, and hooks the five handler tables with no-ops (`e20-c1/oracle/notes.md` §2). Un-hooking `0x80061758`, `0x80065ED4`, `0x8007D9A4`, `0x8007ED10` (Beannoïde) and `0x80068154`, `0x8007DE04` (Muruta) and stubbing their callees (sound `0x800490FC` is already stubbed; `0x8003A59C` and `0x8007FEF8` family run as real code or journalled) should give a real-handler oracle. The notes say `mips.py` stubs `jal` targets and the copied `interp2.py` handles stubbed `jalr` (§6), so real handlers called by `jalr` are within the machine's reach. UNKNOWN until tried: the effect and loot stubs inside `0x8003A59C`.

---

## 6. Gaps E14.a must fill

1. A registry plus dispatch in `RunSpriteEvent` for A1 (and A0), C1, C10, D1, D9, with E0/E1 and the save book kept as they are or folded in (AD-2). Decide what a missing (slot, code) does (counted no-op as today, plus a per-key debug log).
2. `HeroAnimationLatch` on the context (a default interface member, like `EquipmentRecords`); `AlundraWorldProxy` returns `_hitPass.HeroAnimationLatch`.
3. The shared helpers: relative position (6 ints from `TileX/TileY/TerrainHeight`), TryAttackPlayer, TryAttackPlayer2, TryAttackPlayerFront, StartFlying, HandleAnimationDirection, the 32-byte flip table `0x80028B34`, a `Below(n)` draw helper. All read EXE first (3 addresses `0x8007FE20`, `0x8007FEF8`, `0x80080144`, `0x8008048C`, `0x800805C8`).
4. `MapHeights[0..3]` written (the four corner terrain heights, +0x158, EXE routine `ComputeEntityGroundHeight` `0x800370C4`), or computed on demand inside `HandleAnimationDirection` (AD-5).
5. The test baselines of the c1a/c1b arena and the c1b-2 lever arcs (AD-7).
6. The oracle extension for real handlers (5 addresses, section 5).
7. Arcs: the 115 hall (spawn via `0x2D`, fall, kill four Muruta, `0x2C` ends, G569 set at `@434`, portals restored `@493-503`) and the 116 grate (`A[1]@128` needs the Beannoïde records 0 and 1 gone).
8. Loot and break effect: either explicitly deferred to E20.e0 (as D-E19-29 did) or `BreakEffect` exported (converter plus full export, ADR).
9. Docs: a plan `docs/plan-e14a-….md` plus annex, the E14.a row of `plan-e20-heros.md`, ADR-0054 (dispatch policy), update of the stale comment at `AlundraEventProgramRunner.cs:15-21`, the `IntroTraceHarnessTests.cs:1366` message, and the doc address of `ScriptHelper.DirectionTable`.

---

## 7. Proposed minimal architecture (for the main session to decide)

- One static `AlundraNativeSpriteHandlers` (internal) holding a small table `(slot, code) → handler` and an allow-list of sprite types per entry. Handler signature copies the save book: `(AlundraEntityScriptProxy entity, IEntityWorldContext context, AlundraGameState state)`. `RunSpriteEvent` order: save book, E0/E1 special case, then the registry, then the counted no-op.
- One class per handler family (for example `AlundraBeannoideAi` for A1/C1/D1, `AlundraMurutaAi` for C10/D9) plus one `AlundraNativeAiHelpers` for the shared helpers. All state in the proxy's own fields (`Bytes`, `AIValues`, `DelayOrAngleOrEntityId`), no static state (unlike the book). The DECOMP names stay in comments with EXE addresses.
- Hit reaction (`HitCommon`) as one shared function parameterized by the animation id.
- A runner counter `NativeEventRunCount` next to `SaveBookEventRunCount`; `SpriteEventRunCount` keeps counting every call.
- Tests: oracle `out/*.json` comparison tick by tick (real handlers), a gating test per sprite type, mutation scripts, the 115/116 arcs.

Decisions for the author or main session:

- **AD-1 dispatch gating.** Sprite-type allow-list now (recommended, matches the book and avoids half-live slimes), or pure (slot, code). Section 0.3 gives the cost of the second.
- **AD-2** keep or fold E0/E1 and the book into the registry. Folding touches pinned tests (`AlundraSaveBookDispatchTests`, `SpriteEventRunCount` assertions in `AlundraEventProgramRunnerProgramIndexTests.cs:92-96, 166-170`, `AlundraEventProgramRunnerTests.cs:545`, `AlundraWarpDepartureTests.cs`).
- **AD-3** register A0 for all entities (6,722 records, behaviourally invisible until something reads `AIValues[0]`).
- **AD-4** whether native movers read the animation of the box cursor instead of `CurrentAnimationId` (binary-exact walking in multi-tick frames). It touches `TickScriptedNpc` for every NPC.
- **AD-5** `MapHeights` per tick for every entity, or on demand inside the helper.
- **AD-6** break effect: export `BreakEffect` now, or defer with the loot to E20.e0.
- **AD-7** how tests keep natives inert where an oracle needs them.

---

## 8. UNKNOWNs and how to decide

- Per-case logic of C1 and C10: disassemble the EXE ranges in section 3, then run the extended oracle.
- Whether the map-14 and map-15 recettes still describe the intended behaviour once natives are live: the author's recette run.
- Whether a defect of the original must be corrected in C1 or C10 (rule: author's "fix proven defects"): only visible after the EXE re-read.
- Exact break effect id and its drawing (`0x8003BE74`, header byte `0x1E`, value 8 for both sprites): the E20.e0 / effect-stage work.
- Whether `PlayerEntity` can be null while native handlers run in production: no, but fakes need the degraded path.

Files: `D:/development/repo/alundra-casaengine-project-converter/Alundra/Scripts/AlundraEventProgramRunner.cs`, `AlundraEntityScriptProxy.cs`, `AlundraEntityScriptProxy.HitPass.cs`, `AlundraSaveBook.cs`, `AlundraDamage.cs`, `AlundraWorldProxy.cs`, `AlundraScriptedMotion.cs`, `AlundraFrameSyncPasses.cs`, `IEntityWorldContext.cs`, `IAlundraScriptHost.cs`, `AlundraRandom.cs`, `ScriptHelper.cs`, `EntitySearchService.cs`. Tests: `Alundra.Tests/AlundraHitArena.cs`, `AlundraHitPassTests.cs`, `AlundraDamageTests.cs`. Docs: `docs/plan-e20-heros.md`, `docs/plan-conversion-totale.md`, `docs/plan-e20-annexe/e20-c1/oracle/notes.md`, `docs/plan-e20-annexe/decouverte-2026-10-09/5-chain-needs.md`.
