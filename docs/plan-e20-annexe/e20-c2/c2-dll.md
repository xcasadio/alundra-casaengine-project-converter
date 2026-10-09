> Report returned by one agent of the read-only discovery workflow for E20.c2 and E14.a of 2026-10-09 (slice E20.c2), kept verbatim as evidence. The plan holds the reconciled facts.

# E20.c2, DLL-side map (label c2-dll)

Read only. I built and ran nothing in the repository. My only artifacts are the helper scripts `mipsdis.py`, `rd.py`, `hazard_cells.py` and `hazard_map.py` in `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-c2-e14a/c2-dll/`.

Labels: FACT (with file:line or binary address), INFERENCE, UNKNOWN (with what would decide it). DLL paths are relative to `D:/development/repo/alundra-casaengine-project-converter/`. Binary addresses come from `ALUN_CD.EXE` (France) disassembled with capstone. Dumps of table bytes were done with `rd.py`.

## 0. Things that change the plan

1. **The hero's HP has two sources in the DLL, and c2 must pick the source of truth before it adds the end-of-tick mirror.**
   - FACT: the binary's live HP is the hero entity (`+0x14` Hp, `+0x18` HpMax). `MovePlayer` ends by copying it to the stats: `SetPlayerHpMax(+0x18)`, `SetPlayerHp(+0x14)` at 0x80032920-0x80032930. The HUD reads the stats.
   - FACT: the DLL has one source, `PlayerStats` (HUD `AlundraHudDirector.cs:544-545`, save `AlundraSaveGame.cs:216`). E19 ADR-0048 made the life vessel stats-only as a "provisional seam" (`AlundraPlayerManager.cs:1000-1009`).
   - FACT: the hero entity's `Hp` and `HpMax` are set to 10 by `InitializeBalance` from balance record 0 (`AlundraEntityScriptProxy.HitPass.cs:70-71`, called at `AlundraWorldProxy.cs:1978`).
   - FACT: the binary's `ResetEntityState` pulls HP back from the stats right after creation (0x80031974: `jal 0x8004dc50` then `jal 0x8004dd18`, stored to `+0x18` and `+0x14`). The DLL documents this as "Deliberately NOT ported" (`AlundraWorldProxy.cs:1826-1827`).
   - Consequence: adding the end-of-tick mirror without (a) pulling HP from the stats at adoption and (b) updating the entity in `IncreaseHpMaxAndFill` would reset a loaded game or a fresh life vessel to 10/10 on the first tick.
   - FACT, order: `ApplyPendingLoad` (`AlundraWorldProxy.cs:790`) runs before `AdoptPlayerPawn` (`:832`), so a pull at adoption sees loaded stats.
   - Decision needed: binary shape (entity is the source, stats mirror it) or keep stats as the only source with the entity mirroring it (a documented deviation).
   - Also note the unported HP opcodes `0xBC` and `0xC3`. In the decompilation they write `PlayerEntity.HpMax` and `PlayerEntity.Hp` directly (`EntityEventHandlers.cs:3609-3613`, `:3680`), and the DLL does not port them (`EventOpcodeSizeTable.cs:236,243` only).

2. **The driver 0x8003634C "for real" is dead code until E20.h.**
   - FACT (D5 plus my read): with `[0x80127164] == 0` it returns 1 and writes nothing. In the DLL `TryUseItem` is a stub returning 1, so nothing can set `[0x80127164]`.
   - Suggested envelope change: port the two call sites (the hurt-path one at 0x80031C6C and the normal-tick one at 0x80031CC4) as named no-ops with a test. Move the sequences to E20.h.

3. **`MovePlayer` has no case for the hurt chain, so the hero would be stuck in 0x1C.**
   - FACT: the DLL has cases only for Idle/Moving, the jump states, climbing, the attack groups and LoadingMap (`AlundraPlayerManager.cs:283-456`). The class doc says unported states keep their value (`:62-69`).
   - FACT, from `sprite-records.json` (hero `192c2eeb-bdda-5bb4-92d6-39a4ae298307`): animation 0x31 has End=Chain, ChainTo=28 (0x1C), 4 frames. Animation 0x1C has End=Hold, 6 frames. After the animation bridge carries 0x31 into 0x1C, no `MovePlayer` code ends it.
   - Binary handlers, from the jump table at 0x80022D3C:

     | State | Handler | Behaviour |
     |---|---|---|
     | 0x31 | 0x80032680 | `TryUseItem()`, then END. Nothing else. |
     | 0x1C and 0x3E | 0x80032558 | Shared. See the next paragraph. |
     | 0x3A and 0x3B | 0x80031DB4 | Flip direction, then 0x80031DD0. Details below. |
     | 0x39 and 0x3C | 0x80031DD0 | Same tail as 0x3A and 0x3B. |

   - Handler 0x80032558: `TryUseItem()`. If `+0xAC != 1` go to END. Otherwise `+0x8C = (+0x8C + 0x10) & 0x1F`, and `+0x88 = IsOnGround ? 0 : 1`.
   - Handler 0x80031DB4 for 0x3A and 0x3B: flip `+0x8C`, then 0x80031DD0 writes `+0x88 = IsOnGround ? 0 : 0x2D`, then `TryUseItem`, then END.
   - c2 must port 0x1C. Because 0x3E shares the code, the port should be shared with E20.d (the plan sends 0x3E to E20.d).

4. **The DLL's early returns skip the END block, which the binary does not.**
   - FACT: the binary routes the BlockedByEntity branch (0x80031BAC) and the locked branch (0x80031BD4) to END at 0x80032914 (`UpdateItemEffectState`, then the two HP copies).
   - FACT: the DLL does `return` at `AlundraPlayerManager.cs:212` and `:222`, so it skips `UpdateItemEffectState` there (`:463`).
   - FACT: in the locked branch the binary also calls `0x800350C0` (cancel item use) at 0x80031BFC, and when `g_playerControlFlags == 0x20` it calls the 0x80031A68 entry. If the result is not -1 it only sets `+0x1CC = 0x78` (0x80031C04-0x80031C2C). There is no animation change.
   - INFERENCE: this 0x20 path cannot run today. Nothing in the DLL sets bit 0x20, and the binary's setters are native C handlers, which belong to E14 (decompilation `FunctionTypeC.cs:15795`, `:17273`). In the DLL the warp departure is a gate (`GameplayGateNow`, `AlundraWorldProxy.cs:625`), not that bit.
   - The path can be ported with a test that sets the bit directly.

5. **No engine change is needed for the blink.**
   - FACT: the engine already has `Entity.IsVisible` (`CasaEngineMonogame/CasaEngine/Framework/Scene/Entities/Entity.cs:107`). `Entity.Draw` returns early when it is false (`:511-514`). `World.Draw` calls `entityBase.Draw(0f)` (`World.cs:823`).
   - FACT: the DLL already sets `entity.IsVisible = false` once, for destroyed entities (`AlundraFrameSyncPasses.cs:104`). Nobody else writes it. `Destroy` and `ReActivate` at `Entity.cs:395,404` are engine-side.
   - FACT, binary: the `(+0x1CC & 3) == 3` test at 0x800385F0 is in the lists pass 0x800384F4. It builds the third list at 0x80134250 (count at 0x8013435C), which the decompilation names `g_visibleEntities` and which the draw consumes (`EntityManager.cs:1000-1004`, `GraphicManager.cs:449`).
   - Consequence 1: the rule applies to every active entity, not only the hero. The 76 corpus sites of opcode `0x86` (which now SETs the counter) will flicker once the blink is generic. That is faithful but visible.
   - Consequence 2: the same test at 0x80038E84 (the active-effects pass, `EntityManager.cs:675`) removes the entity's effect, which is its shadow. The DLL has no shadow renderer (`AlundraEventProgramRunner.cs:2545`).
   - The natural place for the DLL blink is `AlundraWorldProxy.PublishEntityFrame` (`:2841`).
   - Publication is once per rendered frame (D-E19-13). A frame of 2 ticks can skip the hidden tick of the 4-tick pattern. This is a deviation to document.

6. **Decompilation trap in the hazard table: the binary wins.**
   - FACT: the decompilation has `g_warpStepThresholdTable = [0x0000, 0x0100, 0x0200, 0x0300]` (`StaticVariables.cs:696`).
   - FACT: the EXE at 0x80028B8C holds bytes `00 00 01 00 02 00 03 00`. The halfword loaded at 0x80031B08 is little-endian, so the values are 0, 1, 2, 3.
   - Reading the decompilation would subtract 256 to 768 HP, which is an instant kill.
   - FACT: the table is not exported (no hit in `alundra-project/Data` or the converter). It is 4 constants to inline, with a comment citing the address.

7. **The existing oracle already covers most of the non-hazard hurt flow.**
   - FACT: `docs/plan-e20-annexe/e20-c1/oracle/out/H3.*` and `H1-both.*` contain the real 0x80031A68 flow, in the part c1a excluded. The real flow in H3 is: contact at tick 17, hurt at tick 18, 0x1C at tick 22, Idle at tick 33. See section 4.
   - FACT: `e20-c1b/oracle/out/K1.json` has, for 12 inventories, the combined 255-byte hero sheet and the boots level. The scenarios are: all, bracelet, charm, holy, kit, leather, noarmour, noweapon, ring, rings2, steel, tonic. It does not cover regeneration (c1b report line 67).
   - Gaps that still need an oracle extension are in section 4.

8. **D-E20-5 needs a stated choice for HP 0.**
   - The hero can reach `Hp == 0`. The binary's death branch (0x80031CA0, to 0x800327A4) stays unported.
   - Regeneration is gated on `Hp != 0`, so a hero at 0 never regenerates.
   - FACT: `plan-e16-etat-partie.md:1042` (SC8) accepts a save with `Hp = 0` and defers the problem to E18.
   - Decision to state: leave HP at 0, or floor it at 1.

## 1. DLL inventory

### 1.1 The stubs and what exists

- **`UpdateItemEffectState`**: `AlundraPlayerManager.cs:588-594`. It only calls `FillEquipmentRecords` (steps 2 to 5, `:618-642`) when the host has `EquipmentRecords`, `ItemTables` and `BalanceTables`.
  - Missing steps: 1 (boots), 6 (255-byte copy and combination of the 11 responses), 7 (regeneration), and the end HP copies.
  - Its signature has no `pad`. Regeneration needs the pad hold word at `0x80126F28` (`pad.ButtonsHold`), `state.PlayerControlFlags & 0x7C`, `BlockedByEntity` and the hero's HP.
- **`FillEquipmentRecords`** clears via `AlundraEquipmentRecords.Clear()` (`AlundraEquipmentRecords.cs:34-41`), which also clears the item words. The binary (0x80044520) clears only the pointers; this is the P4 already reported in plan §1.6.
- **Stub of 0x8003634C**: `UpdateItemUseDriver`, `AlundraPlayerManager.cs:646-648`. It is called at `:230` (the 0x80031CC4 site, placed after `Flags |= Gravity`). There is no call at the hurt site 0x80031C6C because there is no hurt path.
- **Other stubs the hurt path calls**: `UpdatePlayerCarriedEntity(player, mode)` at `:579` (the hurt path uses mode 2, 0x80031C64), and `TryUseItem` at `:468` (the 1 stub that keeps every state alive).
- **Entry 0x80031A68**: no trace in `Alundra/`. The names `FindWarpFacingDirection`, `DamageTaken` and `HurtState` appear in no DLL or test file. It exists only in the plan, the annexes and the decompilation (`PlayerManager.cs:3156-3201`, named `FindWarpFacingDirection`).
- **Stale class doc**: `AlundraPlayerManager.cs:70-71` still says END has "no item/HP system".

### 1.2 The binary's `MovePlayer` hurt skeleton (FACT, 0x80031B50)

1. 0x80031B98: `s2 = Slope_18c` is read first and kept. It is the water test used later.
2. 0x80031B9C: `CheckAndExecuteWarp`.
3. `+0x20 != 0`: clear `0x80126FFC` (charge timer) and `0x80127110` (weapon pointer), go to END.
4. `controlFlags & 0x34`: `CreatePlayerAnimationEffects(1)`, clear the same two cells, `UpdatePlayerCarriedEntity(1)`, `0x800350C0`. Then if `controlFlags == 0x20`, call the entry; if the result is not -1, set `+0x1CC = 0x78`. Go to END.
5. Normal branch: `Flags |= 0x100`, `CreatePlayerAnimationEffects(0)`, then `jal 0x80031A68` at 0x80031C44.
6. Result not -1 (0x80031C58-0x80031C98):
   - clear 0x80126FFC and 0x80127110;
   - `UpdatePlayerCarriedEntity(2)`;
   - `jal 0x8003634C` (one driver tick, still on the pre-hurt animation);
   - `+0x88 = (s2 == 4) ? 0x3A : 0x31`;
   - `+0x8C = result`;
   - `+0x1CC = 0x78`;
   - go to END.
7. Result is -1: `Hp == 0` goes to the death branch (0x80031CA0). `+0x188 & 0x80` goes to 0x80032768. Otherwise `jal 0x8003634C` at 0x80031CC4, then pad and direction, then the state switch.
8. END (0x80032914): `UpdateItemEffectState`, then `SetPlayerHpMax(+0x18)` and `SetPlayerHp(+0x14)`.

### 1.3 The entry 0x80031A68 (FACT, disassembled)

The hero is at 0x80127D30. Offsets used: `+0x224` TouchingEntity, `+0x114` PosX, `+0x118` PosY, `+0x1CC` damaged counter, `+0x180` CombinedVramFlagsOR, `+0x14` Hp, `+0x8C` TargetDirection.

- **With a toucher** (`+0x224 != 0`): `ComputeNewHp(hero)` (0x8003A374). The result is `GetDirectionToTarget(heroX - touchX, heroY - touchY)` (0x8002E814), taken as raw 16.16 deltas. The DLL already has `ScriptHelper.GetDirectionToTarget(int, int)` at `ScriptHelper.cs:71`.
- **Without a toucher**: if `+0x1CC == 0` and `[0x80127000] < 3`, then `e = (VramOR & 0x180) >> 7`. If `e != 0`: `Hp = Hp > T[e] ? Hp - T[e] : 0` and the result is `(TargetDirection + 0x10) & 0x1F`. `T` is `{0, 1, 2, 3}`.
- **No armour applies on the hazard branch.** `ComputeNewHp` is not used.
- **Where the DLL touches the pieces this needs:**
  - `ComputeNewHp` and `ResolveBalanceTarget` are pure, in `AlundraDamage.cs:21-81`.
  - The DLL's `ComputeNewHp` takes the sheet as an `AlundraBalanceRecord`. The hero case needs the combined sheet, not the proxy's `BalanceRecord`.
  - The toucher's attack is read as `(TouchingEntity.GameplayProxy as AlundraEntityScriptProxy).CurrentAttack` (`:78`). `TouchingEntity` is an engine `Entity` (`AlundraEntityScriptProxy.cs:427`).
- **The frozen animation (0x80146DF8)** needed by `ComputeNewHp` exists as `AlundraWorldProxy.HeroAnimationLatch` (`:2781`) but only as an `internal` member of the world proxy. `IAlundraScriptHost` and `IEntityWorldContext` have no seam for it (they expose `EquipmentRecords`, `BalanceTables`, `BalanceThreshold`).
- **The boots level `[0x80127000]`** is not stored in the DLL. It is derived on demand by the private `AlundraScriptedMotion.BootsLevel` (`:723-742`): 3, 2 or 1 from items 0x1C, 0x1B, 0x1A, else 0. This equals step 1 (0x80030814-0x80030888). It would need to be shared (`internal`) or moved, so the hazard test can use it.
- **A third binary reader of the boots level** is at 0x800374FC (`boots < 2`). It is in the hero's per-corner obstacle test and gates blocking on cells with `(flags & 0xE00) == 0x800` (deep water). Not in c2 scope (E20.i territory).

### 1.4 HP storage and the HUD

- `AlundraPlayerStats.Hp` and `HpMax` are `short`, default 10/10 (`AlundraPlayerStats.cs:37,40`). `SetPlayerHp` and `SetPlayerHpMax` at `AlundraPlayerManager.cs:1015` and `:983` apply the binary's clamps (0..HpMax, 0..50). No god-mode overrides.
- The hero entity's `Hp` and `HpMax` are `int` fields on the proxy (`AlundraEntityScriptProxy.cs:70-71`). Nothing reads or writes them in production except `InitializeBalance`.
- The HUD director reads `PlayerStats.Hp` and `HpMax` every tick in `UpdateHpAndMpCatchUp` (`AlundraHudDirector.cs:537-588`). The displayed value follows with a roll-down of one point per 8 active ticks. `Tick()` runs once per logic tick after the entity block (`AlundraWorldProxy.cs:2534-2545`).
- The hearts are composed by `AlundraHudComposer.ComposeLife` (`:197`), fed by `AlundraHudPresenter.cs:133`, and drawn only while `IsDrawn` (the jauge is open).
- So once `PlayerStats.Hp` changes, the HUD needs no new code. The jauge must be open for the author to see it (UNKNOWN whether the recipe state has it open).
- The heartbeat sound at low HP (`Hp * 5 <= HpMax`, sound 400, `PlayerManager.cs:3864-3877`) lives in `CreatePlayerAnimationEffects`, i.e. E20.e, not c2.

### 1.5 `DamagedTickCounter` (+0x1CC) and `FrameCollisionTickCounter` (+0x1D0)

- Declared at `AlundraEntityScriptProxy.cs:411` and `:412`. Cloned at `:2479-2480`.
- Counters pass: both decremented when non-zero, `AlundraWorldProxy.cs:2741-2764`. It runs after the programs of the tick and after `MovePlayer`. So a value set by `MovePlayer` at tick N is seen as 0x77 by the lists and the hit pass of tick N (the oracle row after tick 18 shows 119).
- Hit pass: a target with either counter non-zero is skipped (`AlundraHitPass.cs:116-122`). With `+0x1CC = 0x78` the hero cannot be re-touched for 120 ticks. `+0x1D0 = 0x19` is only the 25-tick spacing.
- Opcode 0x86 SETs it (`AlundraEventProgramRunner.cs:758-764`). Test `AlundraHitOpcodesTests.cs:145-186`.
- Nothing writes it for the hero. Nothing reads it for any blink.

### 1.6 Blink and visibility

See section 0 item 5. Engine API: `Entity.IsVisible`, public get/set, no engine change. The publish point is `PublishEntityFrame`, which already iterates `_spawnedEntities` (the hero is added there at `:2063`).

Rule to implement: `Owner.IsVisible = !(Status.IsActive() && (DamagedTickCounter & 3) == 3)`. Set it only when the counter is non-zero or when restoring after a previous hide. Do not touch destroyed entities (`SyncAnimation` sets them hidden and returns before, `AlundraFrameSyncPasses.cs:102-106`). No test asserts `Entity.IsVisible`.

### 1.7 Hurt animations in the hero's tables

FACT, `alundra-project/Data/sprite-records.json`, hero AnimSets (Speed, byte 0xD "Acceleration", Sfx):

| Anim | Role | Speed | Byte 0xD | Sfx | End |
|---|---|---|---|---|---|
| 0x31 (49) | DamageTaken | 256 | 64 (0x40: untouchable) | 23 | Chain to 28, 4 frames |
| 0x1C (28) | KnockBack | 0 | 68 (0x44) | 0 | Hold, 6 frames |
| 0x3A (58) | DamageTakenSwimming | 256 | 64 | 23 | Chain to 59, 4 frames |
| 0x3B (59) | KnockBackSwimming | 0 | 68 | 0 | Hold |
| 0x39 (57), 0x3C (60) | death family | | | | not c2 |

- The `.anim2d` files exist in the four directions for 49, 28, 57, 58, 59 and 60 (`alundra-project/Entities/Alundra/bankalundra_0_anim<N>_<dir>.anim2d`).
- The generic machinery already handles them: Speed and acceleration (`AlundraScriptedMotion.RunOneKinematicTick`, `:457-482`; `TickPlayer` is keyed on `TargetAnimationId`, `:86`), the Z impulse and the animation sound at the tick of the switch (`AlundraFrameSyncPasses.GiveImpulse`, `:302-310`), `HitAnimByte` loaded at the tick (so 0x40 protects during 0x31), the Chain and Hold bridge (`AlundraEntitySpawnFactory.OnAnimationFinished`, `:304-330`). The hurt sound 23 needs no new code.
- Acceleration 0 on 0x31 gives a step of `|target - force| >> 0`, i.e. an instant force. The oracle shows ForceX going straight to -196608. `IncrementForce` at `AlundraScriptedMotion.cs:746` handles it.
- The DLL does produce `Slope_18c == 4` on deep-water cells (`AlundraEntityScriptProxy.cs:1840`: `bestFlagMask >> 9`). So the 0x3A path is reachable in the 54 maps with deep water, while the DLL lets the hero walk on water (E20.i).

### 1.8 The hero's combined defence sheet (0x80127008)

- No representation exists. The proxy's `BalanceRecord` (`AlundraEntityScriptProxy.HitPass.cs:22`) is the binary's `+0x1C4`, which is fiche 0 and has 11 responses all 0 (balance.json entry 0: `MaxHp 10, DamageResponses [0 x11], AttackCount 77`). The hit pass correctly uses it for the "never immune" test.
- Needed for c2: a per-world mutable sheet. `AlundraEquipmentRecords` says it is "read by the step 6 of UpdateItemEffectState (E20.c2)" (`AlundraEquipmentRecords.cs:7-8`) and is one instance per world, so it is the natural home.
- `AlundraBalanceRecord` is immutable but keeps a reference to its `byte[]` of responses (`AlundraBalanceTables.cs:35-43`). A sheet object built once can have that array rewritten each tick with no allocation.
- Only responses 1 to 11 matter. FACT: over the whole `balance.json`, the attack attribute nibbles that occur are 1 to 11 (counted: 1:258, 2:61, 3:5, 4:1, 5:1, 6:68, 7:3, 8:2, 9:2, 10:6, 11:2), so the 255-byte copy beyond the responses is not read.
- Step 7 also needs 3 regeneration counters (`0x80126FE8 + 4i`). They have no home yet; `AlundraEquipmentRecords` is a candidate.

### 1.9 Equipment defence data (exported)

FACT, `alundra-project/Data/balance.json` (entry = item + 0x1E, all level 255, no chain). The DLL already loads these through `AlundraBalanceTables.SelectEquipment` (`:249-257`).

| Item (hex) | Entry | Responses (hex) | Other fields |
|---|---|---|---|
| 0x11 cloth | 47 | 0x10 x11 | no attack |
| 0x12 leather | 48 | 0x0C x11 | |
| 0x13 | 49 | 0x08 x11 | |
| 0x14 | 50 | 0x04 x11 | |
| 0x33 | 81 | 0x40 x11 | attack 1/3 |
| 0x34 | 82 | 0x40 x11 | attack 1/3 |
| 0x35 | 83 | 0x0F x11 | |
| 0x36 charm | 84 | 0x0F x11, but index 5 (nibble 6) = 0x80 | |
| 0x37 | 85 | 0x40 x11 | record byte 2 = 200 (regeneration period 201) |
| 0x38 | 86 | 0x40 x11 | record byte 2 = 200 |

- Weapons (items 1 to 10, 14, 16, entries 31 to 40, 44 and 46) have responses 0x40 x11, so they add nothing to defence.
- Boots 0x19 to 0x1C are category 0 (`item-drop-properties.json` rows 25 to 28: field3 = 128, 0, 0, 0). They have no balance sheet; only the item counts matter (items 0x1A, 0x1B, 0x1C).
- Categories: weapon 1 (items 1 to 10, 14, 16), armour 2 (0x11 to 0x14), accessory 3 (0x33 to 0x38).
- Combination rule, FACT from the check report: b = base byte (record 0 or the armour); A and B = weapon/accessory bytes, defaulting to 0x10. If any has `& 0xC0 == 0x80`, result is 0x80. Otherwise result starts at b (or 0x10 if b has flags), adds `A - 0x10` when A has no flags and `B - 0x10` when B has none, clamped to 0..0x1F. The K1 oracle gives 0x10 for the new-game kit and 0x0C for leather.
- Regeneration (decompilation `PlayerManager.cs:1297-1333`, binary rule `MaxHp < counter` at 0x80030CD4): runs only when `controlFlags & 0x7C == 0`, `BlockedByEntity == null`, pad hold == 0, `Hp != 0` and `Hp < HpMax`. For each of the 3 records with non-null record and non-zero byte 2, `counter++`; when `byte2 < counter`, reset and `Hp++`. Otherwise the counter is zeroed. The period is `byte2 + 1`, i.e. 201 ticks for items 0x37 and 0x38. If any gate fails, all three counters go to 0.

### 1.10 Dangerous-cell data

- The 0x180 bits of `CombinedVramFlagsOR` come from the cell as `walkability | (groundProperty << 8)` (`AlundraTerrainProbe.cs:84-100`; `AlundraCellsCollisionField.cs:293-295`, `:167`). `UpdateVramFlags` runs per tick after the physics, only for corners whose ground height equals `PosZ + ModZ`.
- So level 1 = walkability bit 0x80 (1 PV), level 2 = ground-property bit 0x01 (2 PV), both = 3 PV. The hero is not blocked by bit 0x80: the walkability mask is `0x40`, plus `0x01` for ClassB, plus `0x1000` for ClassA (`AlundraCellsCollisionField.cs:259-274`), so he can stand on them.
- FACT, my scan of the 483 `data-extracted/data/map_<N>.json` files: 36 maps have hazard cells (6738 cells of level 1, 559 of level 2, 25 of level 3). The list includes maps 0, 27, 36, 52 to 60, 84, 86, 87, 89, 90, 92, 94, 101, 104, 107, 120, 122, 160, 325, 411, 422, 425, 426, 438, 443 to 448. Map 0 and map 27 are also in the list.
- FACT: none of the maps used by the story chain or the arcs I checked has any: 14, 15, 115, 116, 117, 44, 362, 389 to 392, 179, 127, 10, 135, 162, 165, 176, 178, 185, 303, 346, 478, 363. Map 36 has 180 level-1 cells at x 4..26, y 43..56; the hero tiles of the `AlundraHeightTargetSitesTests` sites on map 36 are (5,10) and (30,19), outside it.
- The oracle did not exercise hazards (c1 oracle report, limit 3).
- INFERENCE: the hazard rule fires on maps 52 to 60 and 443 to 448 (hundreds of cells each), which are dungeons; the author's current path does not reach them.

## 2. Tests and traces that would move once the hero loses HP

FACT (file:line):

- `Alundra.Tests/AlundraHitPassTests.cs`
  - `H3_...` (`:272-292`): asserts at tick 18 `AnimTarget == 1`, `Latch == 1`, `Hp == 10`, `DamagedTicks == 0`. The oracle has 0x31, latch 0x31, 9, 119. The `ignore` clause (`:280-281`) and the `lastTick: 18` limit go away.
  - `H1both_...` (`:295-313`): asserts tick 2 `AnimTarget == 0x10`, `Latch == 0x10`, `Hp == 10`. The oracle has hurt (0x31, HP 9) at tick 2.
  - `WhileTheContactLasts_...` (`:316-335`): pins hit ticks `{1, 26, 51, 76}`, `DamagedTicks == 0` everywhere and `Hp == 10`. After c2 the first hurt (tick 2) gives `+0x1CC = 0x78`; the hero is not re-touched for 120 ticks, so only tick 1 remains in an 80-tick run, and HP becomes 9.
  - `TheReachBehindTheHero_Beyond9Pixels...` (`:452-458`): pins DLL first contact at tick 12 for dx -10 while the oracle has none (`Assert.Null(oracle)`). After c2 it should be null. The oracle report says the Beannoid body touches the hero for dx in [-18, 20], so dx -10 to -18 are all candidates; only -10 is pinned.
- `AlundraHitArena.cs`: sample columns (`:11-13`) have `Hp`, `DamagedTicks`, `Touching`, `PosX`, `PosY`, but not `TargetDirection`, `ForceX/Y/Z`, `IsOnGround`, `PlayerStats.Hp` or `Owner.IsVisible`. The comparison (`AlundraHitPassTests.cs:55-77`) checks `anim_target, anim_flags_B4, frame_box, atk_*, touching_slot, hit_counter, c1cc, c1d0, hp`, positions and latch. The oracle JSON has `dir_tgt8C`, `force_x/y/z`, `pos_z`, `on_ground` columns already.
- `AlundraTickStructureTests.cs:276-286`: sets `Hero.DamagedTickCounter = 3` and checks the counter pass. Low risk (no visibility assertion); re-run.
- `AlundraDamageTests.cs:185-200` (K2 hero tick with `JumpHeroRig`): a tick of `MovePlayer` will now also run the end HP copy. A bare rig hero has `Hp 0` (the field default), so the mirror would set stats to 0 in that rig unless the copy is guarded. Same exposure for the 30 direct `AlundraPlayerManager.MovePlayer(` call sites in 7 test files (`AlundraPlayerManagerTests` 16, `AlundraLadderClimbTests` 6, `AlundraPortalDetectionTests` 3, `AlundraLoadingMapAirArrivalTests` 2, `AlundraSaveBookTests` 1, `AlundraHeroJumpStatesTests` 1, `AlundraDialogueOpcodesProductionTests` 1), 6 of which pass `host: null`.
- Canary tests if the pull at adoption is forgotten: tests that set the stats then run a real hero: `AlundraSaveGameApplyTests.cs:69-70`, `AlundraSaveGameLoadResetTests.cs:71,221`, `AlundraItemAndMoneyOpcodesTests.cs:277-304`, `AlundraNativeEffectTests.cs:319-383` (these use a fake host, no `MovePlayer`), `AlundraSaveBookTests.cs:390,437,455` (own rig). I have not verified which of these tick a real hero; run them.
- Story-chain arcs: plan §1.5 says the only hit of the whole suite on the hero is the small iron ball of map 127 (`docs/plan-e20-heros.md:1098-1100`). I could not identify which test did it. Candidates are the `s127_*` sites of `AlundraHeightTargetSitesTests.cs:34-41`. UNKNOWN; to decide, watch `Hero.TouchingEntity` and, after c2, `Hero.Hp` in that suite.
- Reference traces: `docs/hero-trace-389-*.txt` and the intro traces. Maps 389 to 392 have no hazard cells and (per c1a) no hit, so I expect no change, provided the end-of-tick mirror leaves `PlayerStats` at 10/10 and the intro is not touched. INFERENCE.

## 3. Harnesses that can host the acceptance

- **`HitArena`** (`Alundra.Tests/AlundraHitArena.cs`): real world of map 14 (`Overworld 3,1-14`), real prefabs, real controller, via `ArcRun`. Supports scripted pad (`Frame(pad, ticks)`), per-tick samples, sparks, handler letters, `BeforeTick` and `AfterTick`. It reproduces the H3 walk-into-a-Beannoid and the H1-both swing-plus-body cases, so the non-hazard acceptance of the envelope row ("oracle of 0x80031A68 and arcs") can reuse it with the exclusions removed. Needs the extra columns listed in section 2.
- **`ArcRun`** (`AlundraArcSupport.cs`): any exported map with flags, real hero, `PlaceHero(x, y, z)` (`:416`), `OnFrame`, `EntityByRecord`. It uses the session singleton `AlundraGameState.Instance`, so `PlayerStats.Hp` is reachable directly. For hazard cells it can load a map with them (for example one of 52 to 60, 443 to 448, or map 36 at y 43..56). For the HUD, `AttachHudPresenterForTests` (`AlundraWorldProxy.cs:1410`) exists. The HUD director rolls down 1 point per 8 active ticks (`AlundraHudDirector.cs:528-536`), so a HUD assertion needs more than 8 ticks per point.
- **Real-game probe** (`sonde-vrai-jeu-premiere-image.md`, harness outside the repo): needed to see the blink and the stats pull on a Continue. The first image of the real game differs from `ArcRun`'s.

## 4. Oracle coverage and gaps

FACT, existing (real MIPS, `ALUN_CD.EXE`):

- **H3** (`e20-c1/oracle/out/H3.txt`): the hero walks east into a Beannoid. Contact at tick 17 (`+0x1D0 = 25`, `TouchingEntity = slot 1`). Tick 18: `TargetAnimationId 0x31`, `AnimFlags 0x40`, `TargetDirection 24 -> 8`, `ForceX -196608` (ticks 18 to 21), `hp 10 -> 9`, `+0x1CC 119` after the tick (0x78 set, then decremented by the counters pass), sound 23. Tick 22: `0x1C` (`AnimFlags 0x44`), ForceX -184320 decaying by 12288 per tick. Tick 33 Idle, tick 34 Moving. Hero `+0x1D0` reaches 0 at tick 42.
- **H1-both** (dx 20): hurt at tick 2, HP 10 -> 9, his swing cut.
- **K1** (c1b): hero sheet and records for the 12 inventories listed in section 0 item 7. Boots level is 0 in all of them.

Not covered (UNKNOWN to be measured, or to be written as hand-derived expectations):

1. Hazard branch (`+0x1CC == 0`, boots 0, 1, 2, 3, levels 1 to 3, direction `(TargetDirection + 0x10) & 0x1F`, HP floor at 0, no armour).
2. The `controlFlags == 0x20` path (only `+0x1CC = 0x78`, no animation).
3. `Slope_18c == 4` (0x3A, then 0x3B, then the 0x80031DB4 tail).
4. Hurt in the air (IsOnGround 0: the 0x1C end writes 1, the 0x3A/0x3B tail writes 0x2D).
5. Hurt while attacking or jumping (the entry runs before the state switch).
6. Armour 0x12, 0x13, 0x14 on the real hurt path (the sheet is covered, damage through `ComputeNewHp(hero)` is not), and the charm 0x36 (immune to nibble 6 yet still knocked back, since the hit pass uses fiche 0 and never filters the hero).
7. Toucher at several bearings (the `GetDirectionToTarget` table), including a toucher at the same position.
8. Regeneration: period 201 with items 0x37 and 0x38, the 0x7C gate, pad-hold gate, the counters zeroed when a gate fails.
9. The stats mirror: no oracle column reads `g_playerStats.Hp`.
10. `Hp == 0` behaviour after hits.
11. The 0x1C end when `TryUseItem` returns 0 (0x8003270C): unreachable with the stub.

## 5. Concrete gaps (what c2 has to create or change)

- **G1** `MovePlayer`: insert the entry between `Flags |= Gravity` (`AlundraPlayerManager.cs:227`) and `UpdateItemUseDriver` (`:230`), with the hurt path described in section 1.2, plus the two zeroed cells (`WeaponRecordPointer`, charge timer) and `UpdatePlayerCarriedEntity(2)`.
- **G2** `MovePlayer`: send the two early returns (`:212`, `:222`) through the END block, add the locked-branch `0x20` path, and add the missing branch order (`Hp == 0` and `+0x188 & 0x80` stay unported).
- **G3** `MovePlayer`: add cases for 0x31, 0x1C (shared with 0x3E, coordinate with E20.d), 0x3A and 0x3B (and 0x39/0x3C only if wanted).
- **G4** A hero-sheet object plus 3 regeneration counters, and `UpdateItemEffectState` extended: steps 1, 6, 7 and the two HP copies, with a `pad` parameter.
- **G5** Hero HP source of truth: pull from stats at `AdoptPlayerPawn`; guard the end mirror for proxies that never pulled (bare test proxies); update `IncreaseHpMaxAndFill`; decide on `0xBC` and `0xC3`.
- **G6** Seam for the frozen animation latch on the host interfaces (additive default members), so `MovePlayer` can call `ComputeNewHp(hero, sheet, equipment, latch)`.
- **G7** Share `BootsLevel` (`AlundraScriptedMotion.cs:723`).
- **G8** Blink in `PublishEntityFrame` (generic or hero only: decide).
- **G9** Tests listed in section 2 and the harness columns listed in section 3.
- **G10** Update the stale class doc (`AlundraPlayerManager.cs:70-71`) and plan §0.2.5 (the blink test lives in the lists pass; the hazard table values are 0..3).

## 6. UNKNOWN list

- Which test the c1a verifier used for the map 127 iron-ball hit. Decided by running the suite with a watch on `Hero.TouchingEntity`/`Hp`.
- Whether the HUD jauge is open at the author's recipe point on map 14. Decided by the probe (section 3).
- Whether the hazard bit 0x80 of `Walkability` is exactly the in-game "hurting ground" (it is by the binary read; the confirmation is an oracle scenario on a hazard cell plus a visual check on map 36 y 43..56 or a dungeon).
- The role of the third boots reader at 0x800374FC. Decided in E20.i.
- Whether anything besides native handlers sets control bit 0x20 (the decompilation search found only `FunctionTypeC.cs`).
- The behaviour at `Hp == 0` the author wants (section 0 item 8).

Files read for this report (all absolute under `D:/development/repo/alundra-casaengine-project-converter/`): `docs/plan-e20-heros.md`, `docs/plan-e20-annexe/e20-0-rapports/D3-degats-au-contact.md`, `D4-D5-equipement-0x8003634C.md`, `D3-D4-D5-check.md`, `docs/plan-e20-annexe/e20-c1/oracle/notes.md` and `out/H3.txt`, `docs/plan-e20-annexe/e20-c1b/oracle-report.md` and `oracle/out/K1.json`, `Alundra/Scripts/*.cs` (named above), `Alundra.Tests/AlundraHitPassTests.cs`, `AlundraHitArena.cs`, `AlundraDamageTests.cs`, `alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerManager.cs` (read-only), `CasaEngineMonogame/CasaEngine/Framework/Scene/Entities/Entity.cs`.
