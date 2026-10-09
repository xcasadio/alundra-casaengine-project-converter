# Survey 4: state of the port (DLL, tests, tools)

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# Port state for the hero's sprint and attack (read-only report, branch `chantier/e19-suite3`, HEAD `8e023d8`, tree clean)

Binary addresses below are quoted from repo docs. I did not decode any instruction, and I ran no tests. Paths are under `D:/development/repo/alundra-casaengine-project-converter`.

## 1. Hero state machine: `Alundra/Scripts/AlundraPlayerManager.cs` (1175 lines)

**Entry and call order**
- FACT: `MovePlayer` (169-416) runs once per rendered frame, called at `AlundraEntityScriptProxy.cs:1408` inside the `IsPlayer` branch (1384-1457). The kinematic tick, animation clock and slope/VRAM/floor probes follow per logic tick (1415-1455).
- FACT: the pad snapshot `LastPadState` is rebuilt every rendered frame (`AlundraEntityScriptProxy.cs:1207-1214`). `AlundraTickPad`, advanced per logic tick, is fed from it at `AlundraWorldProxy.cs` ~2236. Only the inventory, dialogue, choice and save screens read it.

**`TargetAnimationId` cases ported**
- LoadingMap 0x36 (265-271).
- Idle 0 / Moving 1 (277-290): `TargetDirection = dir`, then `CheckEntityInteraction` (643-730), then `ApplyGroundOrAirTail`.
- The jump queue S1 (294-302): ids 2, 0x2B, 0x2C, 0x2D. They take `dir` without the interaction check, then the same tail. `JumpStartTickStamp` (`AlundraEntityScriptProxy.cs:284`) stops a frame without a tick from losing a take-off.
- Climbing 0x0E / ClimbStill 0x35 (306-410).
- Everything else keeps its value untouched (412-413).

**Common tail** `ApplyGroundOrAirTail` (426-448):
- In the air: 0x2C if a direction is held, else 0x2D.
- On the ground: a Cross edge starts the jump (unless `CombinedVramFlagsOR & 0x4000`); otherwise Moving or Idle.

**Stubs (all are plain absences, not named methods)**
- Class doc: 29-31 (TryUseItem, PlayerTryAction, CheckEntityInteraction, PlayerTryAttack not ported), 61-62 (weapon effect, weapon step progression, carried entity), 63-70 (every other case: sprint, attack, pickup/carry/throw, swimming, spell cast, damage taken, victory pose).
- Code: 258-259 (weapon effect calls), 272-276 (Idle/Moving case), 291-293, 303-305 (Climbing case), 412-415 (END: `UpdateItemEffectState`, HP mirroring).
- Tail doc: 423 ("TryUseItem, PlayerTryAction, PlayerTryAttack and the run of the Triangle stay no-ops").
- Pinned by test: `AlundraHeroJumpStatesTests.cs:216-219` asserts Triangle alone gives Idle.
- Not ported: HP==0 death branch (82-170 in the decompilation), warp-tile branch, slope cases other than 6, `g_currentWeaponFlags` (23-27, 61-79).

**Known gaps already written down (FACT, `docs/plan-e19-d2c-valeurs.md`)**
- M7 (l.351): other Cross readers include group masks 0xD0 (swim 0x800326D0 to state 0x28; transport 0x800324E4 to 0x24) and "Hold&0xE0 while running (0x800320FC to state 4)".
- M8 (l.352): the binary calls PlayerTryAttack before the IsOnGround test in the jump-state handler. Until it is ported, Square is dead in the air.
- F19 (l.362): "sprint + Cross to 0x2A" is only the absence of Triangle held.
- UH-4(d) (l.211): Cross + Square on the same tick should attack, not jump.
- `AlundraHeroJumpStatesTests.cs:113` pins only the NPC-interaction case.

**Speed comes from the animation sets, so a ported state gets its speed for free**
- FACT: `RunOneKinematicTick` looks up `AnimSetsByAnim[TargetAnimationId]` for the hero (`AlundraScriptedMotion.cs:86`, 457-482). Speed and acceleration are the set's, and the hero already has ice/water modifiers (484-505).
- FACT: opcode 0x5B scripts already set the hero's sprint animation on map 392 (ADR-0028). No hero code writes sprint by pad.

## 2. Combat-related fields: what exists, and who writes it

**On `AlundraEntityScriptProxy`**
| Field | Written by | Read by |
|---|---|---|
| `Hp`, `HpMax` (l.70-71) | nothing; the hero's stay 0 (`AlundraWorldProxy.cs:1790` "deliberately NOT ported") | nothing |
| `HitCounter` (416) | nothing | opcodes 0x47/0x48 (`AlundraEventProgramRunner.cs:743-753`), pick phase (`AlundraEntityScriptProxy.cs:1517`) |
| `TouchingEntity` (417) | nothing | pick phase (1531) |
| `DamagedTickCounter`, `FrameCollisionTickCounter` | nothing | nothing |
| `HitBoxX/Y/Z`, `HitBoxOriginX/Y/Z`, `Collision*` (385-415) | nothing (only copied by `Clone`, 2423-2451) | nothing |
| `CurrentAttack`, `BalanceRecord`, `FrameCollision` (399-403) | commented out, no type exists | none |
| `XCollisionEntity` (327) | `MoveControllerAndPullPosition` (2178-2183), from the controller's blocking report | `CheckEntityInteraction` |
| `CarriedEntity`, `BlockedByEntity` (73-76) | nothing | nothing in the hero path (`BlockedByEntity` is a never-set early return at 198) |

- **Flags:** `EntityFlags.cs` defines `ClassA/B/C`, `HitsClassA/B/C` and `DeactivateOnHit`. The only consumers are the terrain mask (`AlundraCellsCollisionField.cs:256-268`), opcodes 0x28-0x2B, and the pick phase.
- **Per-animation invulnerability:** `AnimFlags` is loaded from the set's byte 0xD on every switch (`AlundraFrameSyncPasses.cs:146-150`). The decompilation's `EntityAnimFlags.Invulnerable = 0x40` is therefore reachable, but nothing reads it.
- **Missing pass:** the DLL has no frame-collision pass. In the decompilation the name `ProcessFrameCollisions` appears only in doc comments (`EntityFlags.cs:36`, `EntityAnimFlags.cs:22`). UNKNOWN: whether the binary has a function behind that name.

**On the game state**
- `AlundraGameState.PlayerStats` (`AlundraPlayerStats.cs`): `Hp`, `HpMax`, `Mp`, `MpMax`, `Money`, `WeaponId`, `ItemId`, `Falcon`, `FalconTemp`. New Game gives HP 10/10, MP 0/0.
- Ported setters: `SetPlayerHp/HpMax/Mp/MpMax/SetMoney/AddMoney` (`AlundraPlayerManager.cs:746-857`), `UseItem` (1112), `IncreaseHpMaxAndFill` (767). There is no damage function; `IsGodMode` is not ported.
- HUD: `AlundraHudDirector.cs:274-275, 544-546` reads `PlayerStats.Hp`, so damage would show on the HUD without HUD work.
- Invulnerability and knockback: no field, no code (grep for invuln / knock / DamageTaken finds nothing).

**Equipped weapon**
- FACT: stored as `PlayerStats.WeaponId` (short, 1-based slot 1-6, -1 none; `AlundraPlayerStats.cs:51-60`). New Game sets slot 1 (the sword's) via `InitializeNewGameInventory` (894-915, called at `AlundraWorldProxy.cs:1873`).
- `GetItemIdFromCurrentWeapon` resolves it to an item through `GetWeaponIdBySlotId` and `GetItemIdFromSlotId` (936-1026). `ResolveHudEquipmentIcons` (1157) uses it.
- The inventory sets it (E13.d). Counters live in `NumberOfItems[id*2+1]`.
- `PlayerControlBits.ForcedWeapon = 0x80` exists (`AlundraGameState.cs:85-86`), but nothing reads it. Opcodes 0xC0 (set equipped weapon) and 0xC1 (clear the bit) are not ported. `docs/plan-e19-opcodes.md` ~10115 lists both among the 22 opcodes "without an owner".
- Also unported: 0x71 (get hit counter), 0x86 (add DamagedTickCounter), 0x87 (if TouchingEntity anim value), 0x95 (E14) and 0xC3 (restore HP/MP).

**Data exported but unused**
- `alundra-project/Data/balance.json`: `EntriesBySpriteIndex`, 512 entries, 147 with `MaxHp > 0`. Each has `DamageResponses[11]` and `Attacks[]` with `AttackType`, `UsesEquipmentPower`, `AttackAttribute`, `Power`. No DLL reader (grep). The `ItemsWriter.cs:39` doc says nothing loads these raw `.json` companions.
- Item properties have 5 columns: section, flags, priority, max, icon. There is no weapon-power column. UNKNOWN: where equipment power comes from.
- Per-frame collision volumes are exported in the `.anim2d` as collider-fixture keyframes (`SpriteWriter.cs:808-890`, from `SpriteBankReader.cs:22`). UNKNOWN: whether these are sword hit boxes or hurt boxes.

## 3. Pad and button mapping

**FACT, converter `PlayerSetupWriter.cs:133-148` = exported `Data/Alundra.buttonsMapping`** (verified by dumping the JSON):

| Action | PSX bit | Keyboard | Gamepad |
|---|---|---|---|
| Jump | Cross 0x40 | Space | A |
| Attack | Square 0x80 | X | X |
| UseItem | Circle 0x20 | C | B |
| Sprint | Triangle 0x10 | LeftShift | Y |
| Menu | Start 0x800 | Escape | Start |
| L1 / L2 / R1 / R2 / Select | 0x04 / 0x01 / 0x08 / 0x02 / 0x100 | U / Y / I / O / P | LeftShoulder / LeftTrigger / RightShoulder / RightTrigger / Back |

- The D-pad and left stick feed the same four PSX bits.
- `AlundraPlayerController.ActionBits` (76-106) mirrors this table, and `ComputePadState` ORs it into `ButtonsHold` / `ButtonsJustPressed` per rendered frame.
- Consumers today:
  - Cross: the jump (`AlundraPlayerManager.cs:435`).
  - Square: interaction (718), dialogue (`AlundraDialogueDirector.cs:125`), save screen (`AlundraSaveScreenDirector.cs:558`).
  - Triangle: closes both inventories (`AlundraInventoryDirector.cs:693`, `AlundraSubInventoryDirector.cs:534`).
  - Circle: no consumer at all.
- Decompilation line cites for the mapping (`PlayerManager.cs:413 / 549 / 964 / 1904 / 430`) come from the converter's comments. I did not re-check them in the binary.

## 4. Hero prefab and animations (`alundra-project/Entities/Alundra/`)

- FACT: 94 animation ids (0-93) times 4 directions = 376 `bankalundra_0_anim{N}_{dir}.anim2d` files. AnimSets are in `Data/sprite-records.json` under key `192c2eeb-bdda-5bb4-92d6-39a4ae298307`.
- Names below come from the decompilation's `PlayerAnimation.cs`, so they are INFERENCE for meaning. Values are exported DATA. "End" is the direction-0 entry only.

| Id | Name | Speed | Accel | Sfx | End |
|---|---|---|---|---|---|
| 0x03 | Sprint | 336 | 2 | 13 | Loop |
| 0x04 | SprintDash | 0 | 6 | 83 | Chain to 0 |
| 0x29 | PrepareSprint | 0 | 0 | 0 | Loop |
| 0x2A | StopSprint | 0 | 5 | 14 | Chain to 0 |
| 0x3E | SprintAgainstWall | 256 | 64 | 28 (IZF 1024) | Hold |
| 0x10 / 0x11 / 0x12 / 0x13 | attack: dagger, wand, flail iron, bow | 0 | 1 | 29 / 37 / 35 / 32 | Chain to 0 |
| 0x14-0x17 | jump attacks | 0 | 15 | 29 / 37 / 35 / 32 | 0x14 Chain to 0x2D (45) |
| 0x18 / 0x19 | charge attack: sword, flail steel | 0 | 1 | 0 / 36 | 0x18 Chain to 0 |
| 0x3F | AttackSword | 0 | 1 | 29 | Chain to 0 |
| 0x40 | AttackFlailSteel | 0 | 1 | 35 | Chain to 0 |
| 0x41 | JumpAttackSword | not extracted | not extracted | not extracted | Chain to 0x2D (45) |
| 0x1C | DamageKnockBack | 0 | 1 | 0 | Hold |
| 0x31 | DamageTaken | 256 | 64 | 23 | Chain to 0x1C (28) |
| 0x4E | Dead | not extracted | not extracted | not extracted | Hold |

- The 0x18/0x19/0x1C/0x31 pair ends are for direction 0 only. For 0x14 the Sfx value of the original dump (29) was used for 0x14 only; ids 0x15-0x17 follow the same pattern (37 / 35 / 32).
- The end bridge is generic and already works for the hero. `AlundraEntitySpawnFactory.OnAnimationFinished` (304-336) turns Chain into a new `TargetAnimationId` and Hold into `ForceResetAnimationFlag`, and the E19.c2 logical clock times it.
- Sound on every switch is already generic (ADR-0028, `AnimationSoundOf`, `AlundraEntityScriptProxy.cs:296`). It is requested through `AlundraFrameSyncPasses.GiveImpulse` (280), so attack and sprint sounds come with the state change.
- The hero has deformed-quad animations including 0x04, 0x18, 0x19, 0x29, 0x2A (`docs/plan-e19-g2b-annexe/data-notes.md:~80`). They are exported as a Corners track (ADR-0044).

## 5. Effects, sound, contacts

**Native effect interface (E19.g)**
- The static class `AlundraNativeEffects` (61 lines) has one function, `CreateLifeVesselEffects(reservoir, x, y, z)`, which draws from `AlundraRandom`.
- Reservoir API: `AlundraEffectReservoir.Create / CreateAttached / CreateDetached / Free / Tick` (`AlundraEffectReservoir.cs:201-237, 387`). It has 128 slots, and creation fails silently at 129.
- Stage API: `AlundraEffectStage.Emit / Queue(tick, rank, op) / BeginSource(tick, rank)` (170-190). Operations apply at their tick; the quads are pushed to the engine every rendered frame (ADR-0049).
- An entity's script reaches the stage through `(ScriptHost as IEntityWorldContext)?.EffectStage` (`AlundraEntityScriptProxy.cs:1313`). `MovePlayer` receives only `IAlundraScriptHost`.
- INFERENCE: effects emitted from `MovePlayer` (per rendered frame) need a decision on their source tick and rank.
- The census of native effect sites (`docs/plan-e19-g1g3-annexe/binary-data-notes.md` ~160) lists `PlayerTryAttack` 0x8002EAF4 sparks, `UpdatePlayerWeaponEffect` 0x8002F49C and `CreatePlayerAnimationEffects` 0x8002FB1C ×6. None is ported. The same note states `AnimateWarpEffect` is item use (E14), not map warps.

**Sound:** `IAlundraSoundPlayer.PlaySfx` is reachable through `IAlundraScriptHost.SoundPlayer` (`IAlundraScriptHost.cs:92`); `AlundraWorldProxy.SoundPlayer` is at line 494.

**Entity contacts (E19.d2b; ADR-0021/0022 parent, engine ADR-0047)**
- Engine `IMovementObstacleProbe` is installed on `World.MovementObstacleProbe`. The DLL implementation `AlundraMovementObstacleProbe` calls `AlundraEntityCollision.FindEntityCollisionCandidate` (the binary's 0x80036FE0 per its doc).
- The report gives the controller's `LastContact.H1Obstacle/H2Obstacle`, written into `XCollisionEntity` (`AlundraEntityScriptProxy.cs:2178-2183`).
- It is a movement-blocking contact only. Nothing tests a hit box against a hurt box.
- Liftables (`Flags & 0x600`) are deliberately not obstacles (D-E19-28/46, until E14).
- The engine has no combat or health classes (checked by name).

## 6. Test rigs and ground-truth tools

- **`JumpHeroRig`** (`Alundra.Tests/AlundraJumpTestSupport.cs:229-`): real `World`, synthetic cells, the real exported controller settings. `Build(freePad: true)` plus `Step(hold, pressed, elapsed)` drives an arbitrary pad. `SetAnimSet` adds real set entries.
  - Its default AnimSets cover ids 0, 1, 2, 43, 44, 45 only.
  - It has no animated sprite, so animation-end chains do not fire. Attack and sprint ends need either manual chaining or `ArcRun`.
- **`ArcRun`** (`AlundraArcSupport.cs:84-`): real exported map. With `Prefabs: true` the hero is the real prefab `Alundra.entity` (`AddPrefabHeroPawn`, 538), animated. API: `Press(button)` (one frame, held and pressed), `HoldDirections(bits)` (any pad bits, only the first frame is "just pressed").
  - Known limit: the montage resolves the controller's ground on frame 1, unlike the real game (E19.v2, `docs/plan-e19-opcodes.md` §1.2z).
- **Golden traces:** four hero traces `docs/hero-trace-389-*.txt` from `HeroTraceHarnessTests.cs`. They script directions only (no Triangle, Square or Cross in the file). E19.v acceptance asks "the six reference traces identical" (the four plus the two intro traces).
- **Test saves:** `tools/AlundraTestSaves`, `Alundra/Scripts/AlundraTestSaves.cs`, `docs/test-saves.md`. F9 loads a preset in the Debug DLL. Run it outside the Claude app. Presets: `day3-after-dream` (map 179), `day4-meeting` (map 185). The `Preset` record has `ItemCounts`. No preset puts the hero on a map with enemies.
- **Real-game probe:** E19.v2 used an out-of-repo harness deriving `CasaEngineGame`, running `alundra-project/AlundraGame.json` and logging the hero per frame. Its source was in an earlier session's scratchpad, not in the repo; UNKNOWN whether it still exists.
- **MIPS emulators** (`docs/plan-e19-h2-annexe/tools/`):
  - `mips.py` is a minimal MIPS-I interpreter over the EXE image. It supports the integer subset, honours delay slots, stubs callees, and raises on unknown opcodes.
  - `emu.py` is similar, with address hooks. It raises on `lwl/lwr/swl/swr`, GTE, and any regimm other than `bltz/bgez`.
  - `zsim.py` runs the real physics pass 0x80038364, the list builder 0x800384F4, and the Z handlers 0x16, 0x17, 0x1B, 0x20-0x23, 0x25, 0x26, 0x63. It uses the hero slot layout (ENT0 0x80127D30, stride 0x294), real map cells and the exported sprite records.
  - FACT: none of the tools references 0x80031B50 (`MovePlayer`) or `PlayerTry*` (grep).
  - `docs/plan-e19-o9-annexe/run_update_animation.py` runs `UpdateAnimation` 0x80038AB4 over synthetic bytes.
  - `docs/plan-e19-backlog-1006-annexe/mipsdis.py` is a disassembler.
  - `notes.md:54-65` recommends against emulating the whole scene loop and says the binary's frames are not what the DLL pins.
- INFERENCE: ground-truth traces of a sprint's forces and positions look feasible with `zsim.py` plus the AnimSet values (the XY pass is already run). Running `MovePlayer`'s sprint or attack states, or hit resolution, would need new stubs (pad globals, effect and sound creators, entity pairs). Cost is UNKNOWN.

## 7. Numbering and plan layout

- Next free task number: **E20**. `docs/plan-conversion-totale.md` ends at E19 (section at 916; status table rows 976-981 hold E14, E18, E19). No `E20` token exists in the docs.
- Next free parent ADR: **ADR-0052** (index `docs/decisions/README.md:67` ends at ADR-0051). Engine ADR index at engine HEAD `b2205d9f` ends at 0073, so **0074** is next there. UNKNOWN: numbers taken on other branches or worktrees.
- Decision prefix: E19 used `D-E19-1..107` and `O-E19-1..80`. A new task would start at `D-E20-1` / `O-E20-1`.
- Layout of `docs/plan-e19-opcodes.md`:
  - Header with status, then a review table (plan-verifier before approval, fresh verifier after execution).
  - §0 frame: §0.1 author decisions, §0.2 established facts.
  - §1 slices: §1.1 envelope table (slice, content, test arcs, in-game recipe), then one `### 1.2x` section per slice, with tasks as `✅ V-1 / V-2 / V-3`, acceptance, stops, rollback, recipe.
  - §1.3 test arcs, §2 measurements, §3 open points, §4 out of scope, §5 stops, budgets and rollbacks.
  - Annexes in `docs/plan-e19-<slice>-annexe/`.
- There is no `ai-agent/` folder; plans live in `docs/plan-*.md`.

## 8. Decompilation versus binary, as already recorded in the repo

- `SetPlayerWeaponId`: the decompilation's `ushort` parameter inverted both the -1 and 0 cases (`AlundraPlayerManager.cs:922-926`).
- `AddOneItemIfUnlocked`: the decompilation returned the item id where the binary returns the count (1083-1086).
- Triangle closes both inventories through mask 0x813, a bit the decompilation lost (`plan-conversion-totale.md` ~737-739).
- Sound lookup: the decompilation reads byte 0xB, the binary byte 0xD, wrong for 91 of 2405 sets (ADR-0028).
- Opcode 0x78 is 3 bytes in the binary, 4 in the decompilation; 0xC2 reads one parameter byte (`EventOpcodeSizeTable.cs:25`; `plan-conversion-totale.md` E18).
- Not checked by me: whether the decompiled sprint and attack states carry the same losses. That is the discovery slice.

## Open questions for the author
1. Does the attack task include hurting enemies? It needs `balance.json`, hit boxes and a target set, which overlaps E14's native AI. Or does it stop at the swing: states, animations, sounds, effects, weapon flags, with hit resolution left to E14?
2. Which weapons come first: the sword only (slot 1, New Game), or the six slots (sword, flail, wand, bow...)? Are the jump attack, charge attack and magic or bow in the first slice?
3. Who owns the hero being hit: `DamageTaken` / `KnockBack`, invulnerability, the HP==0 branch (E18 owns only "Retry")?
4. Do Circle (`TryUseItem` and the item-use family) and `PlayerTryAction` (pickup, throw, carry) belong to this task or stay in E14? The E19 plan handed "swimming, transport, run, air attack, grab and hits" to E14.
5. Pad timing: keep the hero on per-rendered-frame edges with the jump's tick-stamp trick, or move the hero's Square/Triangle edges to the per-tick `TickPad`? The second would also touch the pinned jump behaviour.
6. In-game recipe: which map and test save? None of the existing presets has an enemy.

## Suggested slice boundaries
- **E20.0, read-only discovery:** decode in the binary the `MovePlayer` cases for sprint (3, 4, 0x29, 0x2A, 0x3E) and attack (0x10-0x19, 0x3F-0x4D), `PlayerTryAttack`, `UpdatePlayerWeaponEffect`, `CreatePlayerAnimationEffects`, and the weapon flags / `ForcedWeapon`. List decompilation-versus-binary differences. Decide whether to extend `zsim.py` to the hero slot for sprint traces. Write the ADR.
- **E20.a, ground sprint** (DLL only): Triangle hold, states 3 / 0x29 / 4 / 0x2A / 0x3E, ground and jump interplay (F19). Speed comes from the AnimSets. Tests in `JumpHeroRig` and `ArcRun` (prefab hero). The six traces must stay identical. A dust-effect sub-slice is separate.
- **E20.b, attack without damage:** Square edge to attack states by equipped weapon, ground and air (M8, UH-4 d), priority against `CheckEntityInteraction`, `ForcedWeapon` and opcodes 0xC0 / 0xC1, weapon flags, spark and weapon effects through the effect stage.
- **E20.c, hit detection and damage:** hit and hurt volumes, writers for `HitCounter` / `TouchingEntity`, a `balance.json` reader, opcodes 0x71 / 0x86 / 0x87. Boundary with E14 depends on questions 1 and 3.
- **E20.d, later:** charge attack, bow, magic (`MP`), item use, depending on questions 2 and 4.
- **Recipes:** an author recipe per slice. Add a test-save preset only if the recipe needs it.
