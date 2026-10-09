# Binary, DLL and data facts for E20.b

> Report returned by one agent of the E20.b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan section 1.4 holds the reconciled facts.

# Facts for E20.b (sword swing without damage)

Nothing was written. Binary facts come from capstone on `ALUN_CD.EXE`. Timelines come from inline runs of the D1 oracle (`docs/plan-e20-annexe/d1-oracle-moveplayer/tools`, `oracle.setup` and `world.tick`, with the real `PlayerTryAttack`, `UpdateAnimation` and physics passes). In those runs, tick S is the press tick and D is the animation length in ticks.

## 1. Binary

**State dispatch** (FACT: `sltiu 0x50` at `0x80031D8C`, jump table `0x80022D3C`, 80 words).

| Handler | Ids that reach it |
|---|---|
| Ground group `0x80031E18` | 04, 08, 09, 10, 11, 12, 13, 18, 19, 25, 3F, 40, 44, 49 |
| Air group `0x80031DF8` | 0A, 14, 15, 16, 17, 1F, 26, 41, 42, 46, 4B |
| Idle/Moving `0x80031E38` | 00, 01 (the staff, anim 0, never leaves it) |
| Jump states `0x80031E84` | 02, 2B, 2C, 2D |
| Default `0x80032758` | 0D, 1A, 1B, 1E, 27, 37, 38, 3D, 43, 45, 47, 48, 4A, 4C, 4D, and ids ≥ 0x50 |

- Every attack id of the weapon table is in a group: ground ids 10, 11, 12, 13, 3F, 40, 44, 49 and charge ids 18, 19 go to the ground group. Air ids 14 to 17, 41, 42, 46 and 4B go to the air group.
- Flail-bounce ids 08 and 25 go to the ground group. 1F and 26 go to the air group.
- Ids 43, 45, 47, 48 and 4A have exported anim sets but are never selected by the weapon table. They fall in the default group, whose body is just `jal 0x800350C0` then END.

**What each group does** (FACT, decoded from `0x80031E18` and `0x80031DF8` to `0x80032680`):
- Ground group:
  - `lw 0x144` (IsOnGround) is non-zero: nothing changes.
  - IsOnGround is zero: `+0x88 = 0x2D`.
- Air group:
  - IsOnGround is zero: nothing changes.
  - IsOnGround is non-zero: `+0x88 = 0`.
- Both then run `TryUseItem` (`0x80032680`, return value ignored) and jump to END `0x80032914`.
- Neither group reads the pad, writes `+0x8C` (TargetDirection), or calls `PlayerTryAttack`.
- END runs `0x800307E8`, then `SetPlayerHpMax(+0x18)` and `SetPlayerHp(+0x14)`.

Other facts about the dispatch:
- FACT: the Slope==6 ladder grab (`0x80031D20`: `+0x8C` = 0x10, `+0x13C` ≠ 0, `+0x2C` = 0 gives `+0x88 = 0xE`) runs before the dispatch for every state, so it can also override an attack state. The DLL already does the same at `AlundraPlayerManager.cs:257-265`.
- FACT (oracle): `TryUseItem` returns 1 when Circle has no rising edge (`0x8002EDA8`).

**`PlayerTryAttack`** (FACT, `0x8002EAF4`):
- It returns 0 at once if `+0x180 & 0x2000` (`0x8002EB1C-28`). No sound, no state change. I confirmed it in the oracle with every cell at ground property 0x20: `vramor = 0x2000`, Square gives no attack and no journalled sound.
- The release edge `0x80126F2C & 0x80` is tested first (`0x8002EB38`).
  - If set, it needs timer ≥ 60 and IsOnGround; otherwise it returns 0.
  - If clear, it goes to the press test `0x80126F2A & 0x80` (`0x8002EC8C`).
- Press path:
  - Record base is `0x80028C79 + 13w`, plus 4 when `+0x144 == 0` (air).
  - If the anim byte is 0 it plays sound 3 and returns 0 (`0x8002ED30`).
  - Otherwise `0x80127110 = &rec`, `0x80127114 = 0`, `+0x88 = anim`, returns 1 (`0x8002ED18-2C`).
- INFERENCE: `AlundraTickPad` edges (`AlundraTickPad.cs:56-58`) make pressed and released disjoint on one bit. So "release first" only changes anything for the charge path (E20.f). In E20.b a release tick has no press edge anyway.

**Weapon resolution** (FACT):
- `MovePlayer` head, `0x80031B70-8C`: `jal 0x8004E030`, then `0x8012710C = byte[0x80028C14 + itemId]`.
- Only `PlayerTryAttack` (`0x8002EC5C`, `0x8002ECB0`) and `UpdatePlayerWeaponEffect` (`0x8002F4E0`) read `0x8012710C`. Resolving it inside `PlayerTryAttack` is therefore equivalent.
- `0x80028C13` is 0, so item −1 gives record 0.

**Item to record map `0x80028C14`** (100 bytes, all other items map to 0):

| Item | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 14 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Record | 0 | 1 | 2 | 3 | 8 | 9 | 10 | 11 | 6 | 7 | 5 | 4 |

**The 12 records at `0x80028C78`** (13 bytes, ends at `0x80028D14`): `[chargeable][ground anim, type, delay, z][air ...][charge ...]`. The pointer stored in `0x80127110` points at an anim byte, so `ptr[1]` is type, `ptr[2]` is delay, `ptr[3]` is z.

| w | Chg | Ground | Air | Charge |
|---|---|---|---|---|
| 0 | 0 | 10,0,0,0 | 14,0,0,0 | 0 |
| 1 | 1 | 3F,0,0,0 | 41,0,0,0 | 18,1B,14,0 |
| 2 | 1 | 44,0,0,0 | 46,0,0,0 | 18,1C,14,0 |
| 3 | 1 | 49,0,0,0 | 4B,0,0,0 | 18,1D,14,0 |
| 4 | 1 | 11,0B,0A,20 | 15,0B,0A,20 | 11,0C,0A,20 |
| 5 | 1 | 11,0E,0A,20 | 15,0E,0A,20 | 11,0F,0A,20 |
| 6 | 0 | 12,0,0,0 | 16,0,0,0 | 0 |
| 7 | 1 | 40,0,0,0 | 42,0,0,0 | 19,0,0,0 |
| 8 | 0 | 13,3,8,10 | 17,3,0,10 | 0 |
| 9 | 1 | 13,4,8,10 | 17,4,8,10 | 13,7,8,8 |
| 10 | 0 | 0,0,0,0 | 0,0,0,0 | 0 |
| 11 | 0 | 10,0,0,0 | 14,0,0,0 | 0 |

**Flag table `0x80028D14`** (2 bytes per anim, 0x50 entries):
- Byte 0 (charge counts) is 1 for 00, 01, 02, 03, 04, 0E, 29, 2A, 2B, 2C, 2D, 35, 3E.
- Byte 1 (delayed spawn) is 1 for 11, 13, 15, 17, 18.
- Everything else is 0 for both bytes.

**Oracle timelines**, all with the default new-game kit (item 1, basic sword):
- **Standing ground swing, anim 0x10, S=1:**
  - The anim switches on tick S itself. `UpdateAnimation` runs after `MovePlayer` in the same tick (`0x80038B18` to `0x80038BF0`). Frame 0 gets its countdown, `CurrentAttack` (`+0x1C8`) and the box (`+0x1D4`) are set, and sound 29 is journalled at tick 1.
  - D is 21 ticks. The chain happens inside `UpdateAnimation` at tick S+21 (`0x80038D54`: `+0x88 = chain target`, `+0xB0++`, jump to the switch block). Idle shows at tick 22 and its sound 0 is journalled there.
  - `MovePlayer` at tick 22 still sees the attack state, so a press at tick 22 is lost. A press at tick 23 swings again (sound 29 at 23). Holding Square re-swings nothing.
  - The pending-spawn pointer `0x80127110` is 0x80028C79 after tick 1 and 0 from tick 2 (the stub weapon-step pass clears it).
- **Walking ground swing:** `ForceX` is 79872 at the switch tick and 0 the next tick (Acc 1, step = |Δ|>>1 computed at the switch). This holds with or without the stick kept. After the swing: Idle at tick 22, Moving at tick 23 if the stick is still held.
- **Air swing, anim 0x14, pressed at tick 6 of a walking jump:**
  - It lasts ticks 6 to 15 (D=10) and chains to 0x2D at tick 16; 0x2C follows at tick 17 with the direction held.
  - `ForceX` goes 150528, 150524, 150520, ... (Acc 15, step 4). At tick 16 `ForceX` is 75244; at tick 17 it is 112886.
  - The jump arc is unchanged.
- **Steel Edge** (item 2 owned and equipped): `ptr = 0x80028C86`, anim 3F, D=21, sound 29.
- **Staff:** sound 3 at every press, no state change.
- **Chain Throw:** anim 0x12, D=31, sound 35.
- **Hunter's Bow:** anim 0x13, D=23, sound 32, spawn call at tick 10 (not E20.b).

## 2. DLL after E20.a (`Alundra/Scripts/AlundraPlayerManager.cs`)

**Where the new code plugs in**
- `PlayerTryAttack` is a stub at **:444** (doc :443). It is called at **:484**, the head of `ApplyGroundOrAirTail` (:481-508). That is the tail shared by Idle/Moving (:304) and the jump states (:316), so priority is already item, then action, then interaction, then attack, then Cross.
- Related stubs: `TryUseItem` :438 (returns 1), `PlayerTryAction` :441 (returns 0), `UpdatePlayerWeaponEffect` :447, `UpdateWeaponStepProgression` :452, `UpdatePlayerCarriedEntity` :457, `UpdateItemEffectState` :463, `UpdateItemUseDriver` :469. Prologue calls are at :269-271 and :224.
- The state chain in `MovePlayer` is if/else-if: LoadingMap :277, Idle/Moving :289, jump states :311, climbing :322, then the comment "every other TargetAnimationId case - NOT PORTED" at **:428**. The attack groups go as new branches before :428.
- The ground and air predicate is `IsHeroInTheAir` (:513): `Controller != null ? IsOnGround == 0 : HeroAirborne`.
- Sprint-prepare 0x29 (second `PlayerTryAttack` call site, `0x80031F74`) is not ported. It belongs to the sprint work.

**Weapon**
- `AlundraPlayerStats.WeaponId` is a short, slot −1 or 1 to 6 (`AlundraPlayerStats.cs:60`).
- `SetPlayerWeaponId` is at :980, `GetItemIdFromCurrentWeapon` at :996 (it is `GetWeaponIdBySlotId(WeaponId − 1)`, :1004), `GetItemIdFromSlotId` at :1037, and `NoItem` (0xFFFFFFFF) at :945.
- It needs `AlundraItemTables`. `MovePlayer` has no tables parameter. `AlundraWorldProxy` exposes them through `IEntityWorldContext.ItemTables` (`AlundraWorldProxy.cs:276-278`), but that is not `IAlundraScriptHost`, and `ContactHost` does not implement it.
- A default `WeaponId` of 0, or −1, gives `NoItem` and so record 0. A bare state therefore swings the basic sword, which matches the binary.
- For slot 1, items 1, 2, 3, 4 and 8 have priorities 0, 1, 2, 3, 4 with replace flag 1, so the highest owned item wins. Item 8 would beat the others and uses record 11, which has the same animations as record 0.

**`CombinedVramFlagsOR`**
- It is a public field of the hero proxy (`AlundraEntityScriptProxy.cs:381`), already read at `AlundraPlayerManager.cs:498` for the 0x4000 jump gate.
- It is written per tick by `UpdateVramFlags` (:1862-1891) via `RunHeroProbes` (:1464-1469), after the physics.
- It is 0 without `EntityFlags.Gravity` or without an `AlundraCellsCollisionField`.
- Ground property 0x20 gives 0x2000, because the flag is `walkability | gp << 8`.

**Sound 3 from MovePlayer**
- Use `player.ScriptHost?.SoundPlayer?.PlaySfx(3)` (`IAlundraScriptHost.cs:100`, same pattern as `AlundraFrameSyncPasses.cs:286`). `MovePlayer`'s `host` parameter is the same object, but `ApplyGroundOrAirTail` and the stubs do not receive it.
- The swing sound itself comes for free. `StepAnimationClock` calls `TakeZImpulse`, which calls `GiveImpulse` (`AlundraFrameSyncPasses.cs:280-288`). That plays `AnimationSoundOf(target)` (`AlundraEntityScriptProxy.cs:288`) at the switch tick, before the sprite-null check.

**Animation end and clock**
- `StepAnimationClock` (`AlundraFrameSyncPasses.cs:215-260`) runs after `MovePlayer` in the same tick. A pending switch reserves the tick, so the new animation sits at logical tick 0 on tick S.
- `SyncAnimation` (:85-186) restarts the sprite at frame end and compensates for catch-up frames.
- The sprite clock ends a Once animation at `_logicalTick == round(DurationSeconds × 50)` (`AnimatedSpriteComponent.cs:361-371`, :399-401). `OnAnimationFinished` (`AlundraEntitySpawnFactory.cs:304-336`) sets `TargetAnimationId = ChainTo`, `AnimCompleteCounter++` and `PendingChainRestartFlag`.
- Export data: ground attacks have `End Chain, ChainTo 0`; air attacks have `ChainTo 45` (0x2D).
- INFERENCE (the stub means nothing is measured yet): the DLL swing occupies ticks S to S+D−1, the chain is raised at S+D by the clock, and `MovePlayer` sees Idle (or 0x2D) at S+D+1. That is the same tick as the binary (oracle: S=1, chain 22, Idle seen from 23).
- Supporting pins: the Chain-length tests in `AlundraAnimationClockDriveTests.cs` (TD9, TD19, TD20), and the binary reading above (frame 0 loaded on the switch tick with its countdown not decremented, `0x80038B48` and `0x80038BF0`).
- Remaining risk: the exported `DurationSeconds` rounds to the binary D. I checked this offline for all relevant ids (see section 3).

## 3. Data (hero prefab `alundra-project/Entities/Alundra`, record `192c2eeb-bdda-5bb4-92d6-39a4ae298307`)

- Each anim has 4 direction files (`bankalundra_0_anim<decimal id>_<dir>.anim2d`, ids 0 to 93), and every attack id and bounce id exists in all four.
- For every id and direction in the table below, `round(last keyframe / 0.02)` equals the binary frame-delay sum in `data-extracted/data/map_alundra.json` (0 mismatches). Binary D is the sum of `Delay & 0x7F` over frames with bit 0x80.
- For all of them `Speed=0`, `IsZForceApplied=0`, `Flags=0`, and the `Acceleration` byte has no 0x20, 0x40 or 0x80 bit.

| id | Acc | Sfx | Chain | D (ticks) |
|---|---|---|---|---|
| 10 | 1 | 29 | 0 | 21 |
| 11 | 1 | 37 | 0 | 31 |
| 12 | 1 | 35 | 0 | 31 |
| 13 | 1 | 32 | 0 | 23 |
| 14 | 15 | 29 | 2D | 10 |
| 15 | 15 | 37 | 2D | 25 (right 24) |
| 16 | 15 | 35 | 2D | 28 (right 27) |
| 17 | 15 | 32 | 2D | 15 (right 14) |
| 18 | 1 | 0 | 0 | 46 |
| 19 | 1 | 36 | 0 | 58 |
| 3F, 44, 49 | 1 | 29 | 0 | 21 |
| 40 | 1 | 35 | 0 | 34 |
| 41, 46, 4B | 15 | 29 | 2D | 10 |
| 42 | 15 | 35 | 2D | 31 |
| 08 | 0 | 28 | 0 | 16 |
| 25 | 0 | 28 | 0 | 16 |
| 1F, 26 | 0 | 28 | 2D | 11 |
| 09 | 0 | 26 | 0 | 19 |
| 0A | 15 | 26 | 2D | 19 |

- Unused by the table: 43 (Acc 0, Sfx 36, D 34), 45 (1, 35, 34), 47 (0, 35, 31), 48 (0, 36, 34), 4A (1, 35, 34).
- Boxes in the `anim2d` files: none for 08, 09, 0A, 13, 1F, 25, 26, and none for 0x17 down and up.
- Charge (0x18, 0x19) and spawn behaviours are E20.f.

## 4. Tests

**Hosting rigs**
- `JumpHeroRig` (`AlundraJumpTestSupport.cs:229`, `Build` :282, `Step` :252, `SetAnimSet` :271):
  - It has the real controller settings, flat or custom cells (`FlatCells.Create(groundProperty:)`, :360), a free pad (`freePad: true`), and the E20.a self-driven tick loop.
  - Pad edges come from `hold` only. The `pressed` argument of `Step` is not used by the tick pad (`AlundraEntityScriptProxy.cs:1393-1396`), and a release is made by dropping `hold` on the next tick.
  - Its anim sets are only 0, 1, 2, 43, 44, 45 (:319-327). Add 16, 20 and similar with `SetAnimSet(new AnimSetEntry{Anim=16, Speed=0, Acceleration=1, Sfx=29})`.
  - **Gap:** it has an `AnimatedSpriteComponent` with no animations and no end bridge. `LogicalClockSprite` is null, so `StepAnimationClock` returns before reserving ticks and no Chain ever fires. To test the swing end, the rig needs a sprite animation of D ticks plus an `AnimationEndByAnimDirection` entry per direction, and `SubscribeAnimationEndBridge` (pattern: `JumpNpcRig.BuildWithSprite`, :79-160, which only fills direction 0).
  - The rig's `ContactHost` has a fresh `AlundraGameState` (WeaponId 0 gives record 0) and a settable `SoundPlayer` (:56 of `AlundraContactTestSupport.cs`); use `RecordingSoundPlayer` (:192).
- `ArcRun` with the real prefab (`ArcSpec(..., RealController: true, Prefabs: true, Arrival: ...)`, `AlundraArcSupport.cs:52`):
  - It has the real sprite, bridge, anim sets and a free pad (`Press`, `HoldDirections`).
  - It needs a map and arrival where the hero is not locked, plus a sound recorder: `Inject(arc)` in `AlundraAnimationSoundTests.cs:61`, used at :275-300.
  - The A10J test (`AlundraHeroJumpArcTests.cs:24`) does not leave the hero free.

**Comparing per-tick traces to an oracle**
- Pattern from `AlundraHeroFallAndPadJumpTests.cs`: UH-1 (:71-103) and UH-2 (:201-221) loop `rig.Step(...)` per tick and assert `PosX`, `PosZ`, `ForceX`, `ForceZ`, `IsOnGround`, `CurrentAnimationId` and `TargetAnimationId` against arrays or formulas copied from the oracle (`JumpHeights` :29, `WalkingJumpPosX` :35).
- The oracle numbers to pin are in section 1: swing timeline, `ForceX` 79872 then 0, air `ForceX` decay by 4, and 75244 and 112886 after the chain.
- Oracle scenarios can be re-run with the inline pattern above by driving `oracle.setup()` and `world.tick(m, pad, hold)`. To equip another weapon in the oracle, call `m.call(0x8004E530, item)` and `m.call(0x8004E484, slot)`.

**Giving the hero another weapon in a DLL test**
- Set `state.NumberOfItems[item * 2 + 1] = 1` (pattern at `AlundraInventoryDirectorTests.cs:474`) and call `AlundraPlayerManager.SetPlayerWeaponId(state, tables, slot)`.
- `tables` comes from `new AlundraItemTables(projectPath)` (pattern at `AlundraItemInventoryTests.cs:91`), or from `AlundraItemTables.GetOrCreate(EngineEnvironment.ProjectPath)`.
- UNKNOWN until designed: how `PlayerTryAttack` gets `tables` in a `ContactHost` rig. Options are a default `ItemTables` member on `IAlundraScriptHost`, or the `GetOrCreate` call.

## 5. The three P4 from E20.a (confirmed lines)

1. `Alundra.Tests/AlundraHeroJumpArcTests.cs:173-176`:
   - The false sentence is at :174-175, "it was F0+241 to F0+244 (4 images)... the landings start 3 images sooner relative to it".
   - Truth (`docs/plan-e20-annexe/e20-a/epingles.md:57-64`): the 2.0 px run was already 7 images before E20.a (F0+238..244, after one 147456 step at F0+237). The old test only asserted F0+241..244. The only change is the one-image shift (now F0+237..243).
   - The loop and threshold at :177-181 are correct.
   - The same wrong claim sits in the annex row `epingles.md:26` ("run grew... not isolated"). The correction paragraph at :57-64 already contradicts it.
2. `Alundra/Scripts/AlundraPlayerManager.cs:284-288`:
   - The sentence at :285 says "(TryUseItem/PlayerTryAction/PlayerTryAttack stay unported no-ops, E2's scope)".
   - They are named stubs since E20.a A4, and the code just below calls them (:293-306).
   - The same staleness is at :478-479 ("stay no-ops of the port"), and in the class doc at :27-31, :40-46 and :61-72.
3. `Alundra.Tests/AlundraTickStructureTests.cs:295-325`:
   - The test `ThePublishedState_IsTheStateAfterTheLastTickOfTheSameFrame_ForOneAndTwoTicks` asserts the NPC's published root position (:315-316) but, for the hero (:319-321), only that `TargetAnimationId == CurrentAnimationId`.
   - The hero moves because `rig.Hold = Right` (:308), yet its published position is never asserted.
   - UNKNOWN: the exact expected form. The hero has a controller (`BuildHeroPawn`), so root X is the truth and `Pos*` is pulled from it, not written back from `Pos*` (`AlundraFrameSyncPasses.cs:371-376`). The plan text only says "une assertion sur la position publiée du héros".
