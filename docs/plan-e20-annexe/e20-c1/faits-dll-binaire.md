# DLL and binary facts for E20.c1

> Report returned by one agent of the E20.c1 discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

# Facts for E20.c1 (read-only; no repo file touched, one scratch file `sites.json` written in the session scratchpad)

Labels: **F** = fact (file:line, or address with instruction words), **I** = inference, **U** = unknown. Binary addresses were re-decoded with capstone (`ALUN_CD.EXE`). Paths are under `Alundra/Scripts/` unless noted.

## 1. DLL insertion points

**Tick order and hook**
- F `AlundraWorldProxy.cs:2625-2694` (`RunEntityBlock`) runs the passes in this order: MovePlayer, programs, pending re-scan, `RunCountersPass` (2671), lists (2676), clock (2680), physics (2686), `RunHitPassHook` (2692). `BeginSource(tick,1)` is active, so `_effectStage.Emit` works from the hook.
- F `RunHitPassHook` (2731) only calls `HitPassHookForTests`.
- F `RunCountersPass` (2705-2728) loops all `_spawnedEntities` with no status filter. It does `FrameCounter++`, decrements `DamagedTickCounter` and `FrameCollisionTickCounter` when non-zero, then clears `TouchingEntity` and `HitCounter`.
- F `RunEntityBlock` is called only when `DrivesEntityTicks` is true (`:2309`, `:651`, which needs `_world.Game != null`). A closed gate gives lists only, with no hit pass, same as the binary (`0x8003B394`). Self-driven rigs never reach the hook, so c1 tests need the world or the test seam.

**Hit-pass list**
- F Binary `0x800384F4` builds the list at `0x80127B28` (count `0x80127D28`) over slots 0..`[0x80134360]`. A slot enters if status−2 <u 2 (`0x80038550`) and `+0x20 == 0` (`0x80038560`).
- F The list is built after the programs and counters, before clock and physics. The clock (`0x80038E18` → `0x80038AB4`) and physics (`0x80038364`) run over this same list. The hit pass sees the statuses as they were at list-build time.
- F DLL `RefreshUpdateProxiesAndCollidables` (`:3135`) builds `_updateProxies` from all spawned entities with no filter. It is not this list.
- F The hero is added to `_spawnedEntities` first (`:2033`), then records (`:872`) and dynamic spawns (`:3026`) in creation order. `CollectTickEntities` (`:2592`) also puts the hero first.
- I c1 needs its own list: `Status.IsActive() && BlockedByEntity == null`, built at the "lists" step, hero first. `_tickEntities` is built before the programs and is stale for statuses.
- I Gap: the binary reuses the first free slot (`FindFreeEntitySlot`). The DLL appends and recycles by removal (`:2915`). "First attacker wins" order can differ after mid-map recycling.

**Animation switch**
- F `StepAnimationClock` (`AlundraFrameSyncPasses.cs:215-268`) detects a switch per tick (`:229`): `PendingChainRestartFlag != 0 || TryResolveAnimationTarget` (`:441`). That function is true for a changed animation or a changed direction, like the binary's `0x80038B08`. It acts at `TakeZImpulse` (`:240`, `:266` for a chain end).
- F `CurrentAnimationId`, `AnimationDirection`, `AnimFlags` (`:146-149`) and the sprite swap (`:163-171`) are written only in `SyncAnimation`. It runs at frame end from `PublishEntityFrame` (`AlundraWorldProxy.cs:2748`), by decision D-E19-13. All of these lag the binary within the frame. This includes the `AnimFlags & 0x40` skip on a target, which the binary updates at the switch (`0x80038B68`).
- F Binary switch block `0x80038B18-0x80038BAC`. It resets `+0xA8`, sets frame pointers `+0xA0/+0xA4`, sets `+0xB4 = byte 0xD`, then loads `CurrentAttack`. `a2` is `+0x88` (the target animation), `a1 = +0x1C4`, `count = [a1+0xE]`. The result is null if count is 0, `Attacks[a2+1]` if `a2+1 < count`, else `Attacks[0]`. Attacks sit at `+0xF + 2k`, so it depends on the target animation, not the direction.
- F A chain end jumps straight into the switch block (`0x80038D54-0x80038D68`).
- F `InitializeEntity` also runs `UpdateAnimation` (`0x80039EA8`) and `HitBox` (`0x80039F30`). So the attack and box exist from the spawn tick.
- I Recommended: snapshot (effective animation, direction) in `StepAnimationClock` when it sees a switch, and reload `CurrentAttack` there. Do it also for the spawn animation, which `TakeZImpulse` skips (R2).

**Reading the frame box at a tick (D6 rule)**
- F `proxy.LogicalClockSprite` (`AlundraEntityScriptProxy.cs:162`) → `.CurrentAnimation.Animation2dData.CollisionKeyframes` (`Animation2dCollisionKeyframeData`: `TimeSeconds`, `Fixtures` of `ColliderFixture` with a `Box`).
- F The rule, proven on 173408 ticks (`D6-boites.md`): take the last keyframe with `round(TimeSeconds*50) <= LogicalTick`. Empty `Fixtures` means no box. Size = `Box.Size` (X, Y, Z in px). Offset = `LocalPosition − Size/2`.
- F `AnimatedSpriteComponent.cs:276-381`: `LogicalTick` is 0 at a switch, wraps for Loop, saturates for Once. Engine collision bodies follow real time and must not feed the hit pass.
- F Switch-tick reservation: the clock reserves the switch tick (`:249-254`). `SyncAnimation` (`:173-185`) restarts the sprite at tick 0. With 0 reserved ticks it sets `AnimationSwitchTickOwed`. With n>1 it calls `AdvanceLogicalTicks(n−1)`.
- F In a one-tick frame, at the hit pass of the switch tick the sprite still holds the old animation. The same is true for the remaining ticks of a catch-up frame after the switch tick.
- I At the switch tick, read frame 0 of the target animation. Resolve it with `TrySelectAnimationByNameSuffix`.
- I For later ticks of the same catch-up frame, keep a per-proxy (animation, tick) cursor maintained in `StepAnimationClock`. Otherwise `sprite.LogicalTick` is right once `SyncAnimation` has run.
- I Pre-bake per animation (offsets, sizes, start tick) to avoid per-tick allocation.
- F Raw conversion: the binary box is offsets `<<16` (signed bytes, `0x80038C48-0x80038C70`) and sizes `(n<<16)−1` (`0x80038C78-0x80038CB0`). For n=0 the attacker size is −1 and the target size is 0 (`0x80039CC8`).
- F Offset 0xFFFF stores `+0x1D4 = 0` (`0x80038CB8`).

**PosZ +1 convention and positions**
- F `HitBox = Pos(+0x114..) + CollisionOffset` (`0x80038094-0x800380B0`), not `ModdedPos`. It is recomputed after movement (`0x800384B8` → `0x80038064`) and only when a box exists.
- F `ModdedPos = Pos + Mod` is refreshed at the head of physics (`0x8003839C-0x800383E0`).
- F In the DLL, `ModdedPosX/Y/Z` is written only at spawn (`AlundraEntitySpawnFactory.cs:686-688`). It is stale after the first move.
- I c1 must compute `Pos + Mod` and `Pos + CollisionOffset` on the fly.
- F The DLL's PosZ equals the binary's minus 1 (ADR-0026). Attacker box and target `ModdedPos` use the same shift, so entity-vs-entity differences are correct as is.
- I Add +1 only when comparing a DLL Z with an absolute binary Z. `0x95`'s rectangle Z origin is an example.
- I Controller-driven entities are re-derived from float roots each frame (`:1329-1335`). That can introduce a few raw units of error against an overlap test exact to one unit.

**Field map**

| Binary | DLL |
|---|---|
| +0x10 Status, +0x14 Hp, +0x18 HpMax, +0x20 | `Status`, `Hp`, `HpMax`, `BlockedByEntity` (`:69-73`) |
| +0x6C Flags | `Flags` (`:118`) |
| +0xB4 | `AnimFlags` (`:299`, lagging) |
| +0x1C4 BalanceRecord, +0x1C8 CurrentAttack, +0x1D4 FrameCollision | **commented out** (`:409,410,413`) |
| +0x1CC | `DamagedTickCounter` (`:411`) |
| +0x1D0 | `FrameCollisionTickCounter` (`:412`); misleading name, it is the 0x19 hit cooldown |
| +0x1D8 | `ModdedPosX/Y/Z` (`:414`, stale) |
| +0x1F0/1F4/1F8 (X/Y/Z size) | `Width/Height/Depth` (`:416`); set in `SetEntityDimensions` `AlundraEntitySpawnFactory.cs:727-729` as `(n<<16)−1` or 0, for the hero too (`AlundraWorldProxy.cs:1771`) |
| +0x1FC | `HitBoxX/Y/Z` (`:417-419`, never written) |
| +0x208 | `CollisionOffsetX/Y/Z` (`:420-422`) |
| +0x214/218/21C (X/Y/Z size) | `CollisionWidth`, **`CollisionDepth` (Y)**, **`CollisionHeight` (Z)** (`:423-425`) |
| +0x220 | `HitCounter` |
| +0x224 | `TouchingEntity`, typed `Entity?` (engine Entity). Use `attacker.Owner` and read back through `.GameplayProxy`. |

- F Naming trap: for targets `Height` is Y and `Depth` is Z. For attackers `CollisionDepth` is Y and `CollisionHeight` is Z. The export's `Box.Size` is (X, Y, Z).
- F New fields must be added to `Clone` (`:2397-2493`) only if they are set before spawn.
- F `HitBoxOriginX/Y/Z` (`:384`) is unrelated to `+0x1FC`.

## 2. Balance records

- F `Data/balance.json` (`UiWriter.cs:255-293`, recopy of `BALANCE.BIN.json` minus `FileName`) has one key, `EntriesBySpriteIndex`: 512 entries.
- F Each entry holds `BalanceLevel`, `RecordSize`, `MaxHp`, `DamageResponses[11]`, `AttackCount`, `Attacks` (null if 0), `FileOffset`, and `NextLevel`, which is nested or null.
- F Chain lengths: 499 entries have one record and 13 have chains (7×13, 2×6, 2×2, 1×3, 1×4). Chains end at level 255.
- F `Attacks[k]` has `AttackType`, `UsesEquipmentPower`, `AttackAttribute` (low nibble = type, bit 7 = equipment), and `Power`.
- F Response of attribute nibble k is the record byte `k+2`, so `DamageResponses[k−1]`. Nibble 0 reads `MaxHp`, but no data attack has that case.
- F Selection `0x80044550(index)`:
  - Index in 0x1F..0x7F is remapped to 0x1E (`0x80044558-0x80044560`).
  - The record is `0x80143DF8 + u16[index]`.
  - It then advances `rec += rec[1]` while `rec[0] < [0x80143DF0]` (`0x8004458C`).
  - The threshold is stored by `0x80044520` (`0x80044524`). Its only caller is `0x8002C3A0`, with `a0 = lbu 0xB([0x800E4334])` (`0x8002C39C`).
- F The exported tilemap carries `D` (e.g. 4, 0) in its `custom_properties` as a string. `BalanceLevel` (always 2) is byte +9 and is not the threshold. Use `tileMapData.CustomProperties["D"]`, the same way `ResolveMapGravitySettings` reads `Gravity` (`AlundraEntitySpawnFactory.cs:753`).
- F The sprite index is `SpriteType` (`EntityRecordMapper.cs:226`: `SpriteTableIndex | 0x100` when `SpriteDirection & 0x80`, else the plain index). For the hero it is 0.
- F Hero record 0: MaxHp 10, 77 attacks, all responses 0. Hero attack of sword anim 0x10 is `Attacks[0x11]` (attr 129, power 1).
- F Levers use index 305 (49|0x100) and the bascule 308. Their responses are 16 for nibbles 1-7 and 128 for 8-11, with MaxHp 1 (D=0 on 115/116/117, D=2 on 44).
- F Nothing reads `balance.json` today.
- F Loading pattern: `AlundraItemTables.GetOrCreate(projectPath)` (`AlundraItemTables.cs:227`) is session-cached and degrades to zeros with a warning. `AlundraWorldProxy.ItemTables` (`:276`) is an assignable field, and the host exposes it through `IAlundraScriptHost.ItemTables` (`IAlundraScriptHost.cs:107`).
- I Add a `GetOrCreate` balance table the same way, plus a host seam.
- F Creation: binary `0x80039E30-0x80039E4C` does `+0x1C4 = rec`, `+0x18 = +0x14 = rec[2]` (MaxHp). The DLL sites are `ApplySpawnInitialization` after `proxy.SpriteType = …` (`AlundraEntitySpawnFactory.cs:636`, with `tileMapData` available) and `ApplyHeroSpriteHeader` (`AlundraWorldProxy.cs:1746-1771`).
- F `ApplySpawnInitialization` returns early when the header does not resolve (`:564-569`). The binary has no such case.

## 3. Sparks and effects

- F Spark routine `0x80039188`, three iterations of `0x8003BDD8(0, 9, 0, cx, cy, cz)`. Only when a slot is returned (`0x80039204`) it draws r1, r2, r3 from `x'=x*0x7D2B89DD+0xE06A02E7`. It sets slot `+0x54` = hi(r1·0x20001)−0x10000, `+0x58` from r2, `+0x5C` from r3.
- F This is the same formula as `AlundraNativeEffects.cs:52-58` (the life vessel), but all three axes use 0x20001 and there is no z offset.
- F The centre is `(max(min corners) + min(max corners)) / 2` per axis, truncating toward zero (`0x80039684-0x80039744`).
- F Effects 4, 5 and 9 exist in `Data/effects/effects-global.json` (29 effects).
- F Attachment `0x8003BE74(0, 4|5, 0, target, depthOffset 1, 0,0,0)` (`0x80039604-0x80039660`). Nibbles 6 and 10 give effect 4, and 7 and 9 give effect 5. Neither happens for a `0x80` response.
- DLL API mapping:
  - Sparks: `AlundraEffectReservoir.Create(false, 9, 0, cx, cy, cz)` (`AlundraEffectReservoir.cs:201`), with forces set after. Add `AlundraNativeEffects.CreateHitSparks(reservoir, cx, cy, cz)` and queue it with `_effectStage.Emit(...)`, as `AlundraEventProgramRunner.cs:3058` does.
  - Attachment: `CreateAttached(false, 4|5, 0, anchor, 1, 0, 0, 0)` (`:205`).
- F Gap: `IAlundraEffectAnchor` (`:11-25`) is implemented by no entity.
  - `PosX/Y/Z` and `Status` (0 gone, 4 destroyed, else alive) exist on the proxy.
  - `DepthKey` (binary `+0x1BC`) is computed by no code in the DLL. `ZUpperBound` (`:407`) is unused.
  - The binary computes it in the sort pass `0x800397AC` family. U: its exact formula and how it should map to `ComputeEntityElevation`.
- F Queued ops apply before the update of their own tick (`AlundraEffectStage.cs` `RunFrame`). Sparks of the hit pass of tick N are updated in tick N, like the binary's effects pass after the entity block.
- F RNG: `AlundraRandom` (`AlundraRandom.cs`) uses seed `0xB017C93D` (the image value at `0x80098708`) and the same multiplier and increment as the binary (`0x800391B8-0x800391C8`).
- F It is a static global, never reset in-game and never advanced per tick (`0x8002BD14`). No `RandomSeed` reference exists outside the class.
- I Same stream function, not the same state sequence. Spark draws happen when the op is applied in `RunFrame`, after synchronous script draws of the same frame. Parity is exact only after E20.e0.

## 4. Damage

- F `ComputeNewHp` `0x8003A374` has 37 callers: the hero's entry `0x80031A90` and 36 native D handlers (`0x8007D9B0-0x8007ECA8`). None is ported. `ResolveBalanceTarget` is called only from `ComputeNewHp` (`0x8003A528`).
- F No opcode calls `ComputeNewHp`.
- Consequence in c1:
  - A hit changes `TouchingEntity`, `HitCounter` and `+0x1D0`, and makes sparks.
  - No `Hp` changes anywhere in the game. A Beannoid's native D is a counted no-op (`AlundraEventProgramRunner.cs:290-327`).
  - Port `ComputeNewHp` and `ResolveBalanceTarget` as pure functions, covered by the D2 oracle. They have no production call site until E14.a (native D handlers) and E20.c2 (hero entry).
  - The D2 oracle covers both functions, six cases, 3000 random sets and 13 mutations.
- F `r` rules (`0x800446B4-0x800446BC`):
  - `r & 0xC0 == 0x40` gives HP 0.
  - Other non-zero top bits leave HP unchanged. This includes `0xC0`.
  - Otherwise `dmg = ((power + Σ equipment) * r) >> 4`, minimum 1.
- F The hero animation is latched at the head of every hit pass, even with an empty list (`0x80039344/48`, to `0x80146DF8`). `ComputeNewHp` reads it one tick later.
- F Equipment records at `0x80146E00 + 8k`, k=0..2, are filled each tick by `0x800307E8`. For `attr & 0x80`, `ResolveBalanceTarget` adds each non-null record's `Attacks[g+1 < count ? g+1 : 0].Power`.
- Minimal part of equipment for c1:
  - F From `balance.json`, power comes only from the weapon record (item n → entry n+0x1E; sword items 1-4 give +1, +2, +4, +8; item 8 gives 50) and from the ring (0x33 and 0x34 give +3). Armors 0x11-0x14 have zero attacks and only matter for the hero's defence (c2).
  - I The DLL already has `GetItemIdFromCurrentWeapon` (`AlundraPlayerManager.cs:1117`) and the ring's `SetItemIdFromCurrentItemId` (`:1250`, which has the `ItemId` rewrite side effect). Missing: the item category table `byte[0x800C5F7C+8n+6] & 0x7F` (1: items 1-10, 14, 16; 2: 0x11-0x14; 3: 0x33-0x38) and the balance entry n+0x1E (`0x800445C0`).
  - F New game: the sword gives +1 and the hero's neutral hit on a response-16 target is `((1+1)*16)>>4 = 2`.
- F The hero as a target: the pass reads his response from `+0x1C4` (record 0, all zeros, never immune). Enemy attacks therefore set the hero's `TouchingEntity` and `+0x1D0`, make sparks, and the hero ignores them in c1. His `+0x1CC` stays 0, so a contact repeats every 25 ticks.

## 5. Opcodes 0x86, 0x87, 0x95

Handler table base `0x80098FAC`.
- F **0x86** (`0x8003FFD4`, size 4): `[0x86, search, lo, hi]`. `n = 0x8003C954(.., search)`. For each match it does `+0x1CC = lo | hi<<8` (**set**, not add). No Result. Returns 4.
- F **0x87** (`0x80040048`, size 3): `[0x87, search, typeIdx]`. `nibble = byte[0x80098FA4 + typeIdx]`, table = [1,2,3,4,5,5,6,7].
  - Result is 1 iff some match has non-null `+0x224`, whose `+0x1D4` and `+0x1C8` are non-null, whose `attack[0]` is non-zero and `(attack[0] & 0xF) == nibble`.
  - Otherwise Result is 0. Returns 3.
  - It tests at program time and does not read `HitCounter`.
- F **0x95** (`0x800407C0`, size 10): `[0x95, x, y, z, wx, hy, _, maskLo, maskHi, typeIdx]`.
  - Origin X = x·24 px, Y = y·16 px, Z = z·16 px.
  - Size X and Z = wx·24 px (shared), size Y = hy·16 px. Byte 6 is unused.
  - mask = `maskLo | maskHi<<8`, zero mask gives Result 0. nibble = table[typeIdx].
  - It scans slots 0..N inclusive for status 2/3, `+0x20 == 0`, box, attack with non-zero attr, `Flags & mask != 0`, nibble match, and `HitBox` overlap against the rectangle. Result 1 on the first hit, else 0. Returns 10.
- F **0x71** (`0x8003FA10`, size 1) sets Result = `HitCounter`.
- F Sites (my linear census over `EventCodes` tables with `EventOpcodeSizeTable` sizes; totals match the plan):
  - 0x86: 76 sites, 24 maps (tables B 37, C 39).
  - 0x87: 182 sites, 40 maps (D 181, B 1).
  - 0x95: 43 sites, 30 maps (C 34, B 9).
  - Maps 115, 116, 117, 44, 362: 115 has 0x86 @242 `[22, 600 ticks]` and @248 `[22, 0]`, 44 has 0x86 @940 `[16, 120]` and @2268 `[33, 30]`, 362 has 0x87 @728 `[128, 3]` (own entity, bomb). 116 and 117 have none.
  - Story-chain 0x95 sites are all bomb checks: map 10 @6418, map 14 @494 and map 15 @902, each with mask `0x0808` and typeIdx 3. 0x87: map 10 @6688/@6704 (typeIdx 1, sword), map 14 @544 (typeIdx 3, bomb).
- F DLL: none has a `case`. `Dispatch` falls to `UnknownOpcode` (`AlundraEventProgramRunner.cs:2026`, `:2965-2990`), which skips by `EventOpcodeSizeTable` (`:182,183,197`) with one warning and leaves Result unchanged. So 0x87 and 0x95 never succeed, and 0x86 never sets the timer.
- Provable arcs in c1, hero placed directly, hit pass plus bytecode D:
  - I 115 NW lever (rec 2, D `ProgramIndexes[3] = 129`), 116 levers, 117 bascule and 44 levers all need only the hit pass and `TouchingEntity`.
  - I The 362 lid needs 0x87 plus a bomb nibble 4. Prove the 0x87 predicate with a test attacker with `CurrentAttack` nibble 4. The real bomb needs E20.h/E12.
  - I 0x86 only affects timers on 115 rec 22 and the 44 records. Its effect shows only with their targets (E14).
  - U: whether E20.h is needed to put the bomb on the lid.

## 6. What a hit does now, and readers

- F Nothing writes `HitCounter`, `TouchingEntity` or `+0x1D0`. The writers are the future hit pass and the counters pass (clear/decrement).
- F `PickEventTrigger` (`AlundraEntityScriptProxy.cs:1539-1592`), for Normal status:
  - Destroy flags are tested first.
  - E (status → Deactivated) if `DeactivateOnImpact`, or `DeactivateOnHit && HitCounter != 0`, or `DeactivateOnAnimationEnd`. E beats D.
  - Else D if `TouchingEntity != null`, else C (F if the active-collision condition holds).
  - `RunPickedEvent` (`:1610`) runs bytecode when `ProgramIndexes[trigger] & 0x7F != 0`, else the native `RunSpriteEvent`.
- F Native D is a counted no-op (`AlundraEventProgramRunner.cs:320-326`). Only the save book and E indexes 0 and 1 (destroy) are handled.
- F Bytecode D (levers, bascule) runs normally. A hit coinciding with an E trigger is lost (`+0x224` is cleared by the counters pass).
- F Hit lands at the end of tick N, programs read it at N+1, then the counters pass clears it (binary `0x80038998` after `0x800386D0`).
- F DLL readers of `HitCounter`: opcodes 0x47/0x48 (`AlundraEventProgramRunner.cs:743-753`) and the `DeactivateOnHit` pick (`:1555`). 0x71 is not ported.
- F DLL readers of `DamagedTickCounter` (+0x1CC) and `FrameCollisionTickCounter` (+0x1D0): none beyond the decrement in `RunCountersPass`.
- F Binary readers of `+0x1D0`: only the hit pass (`0x8003941C`). My scan found `0x8003442C` but it is a biased base, not `+0x1D0`.
- F Binary readers of `+0x1CC` by displacement: `0x80031ABC` (hero entry), `0x8003942C` (hit pass target skip), `0x800622C8`, `0x80062690`, `0x80070400` (native AI, E14). Also the counters pass and the flicker test (`0x800385E8`, base-biased).

## Pitfalls to carry into the c1 plan

1. `ModdedPos` is stale in the DLL and `AnimFlags`/`CurrentAnimationId` lag within the frame; `HitBox` is never maintained.
2. The hit-pass list needs status and `BlockedByEntity` filters; the existing lists have none.
3. Naming of size fields differs between targets (`Width/Height/Depth`) and attackers (`CollisionWidth/Depth/Height`).
4. `TouchingEntity` is an engine `Entity`, not a proxy.
5. Effect anchors and their depth key are missing.
6. The item category table is missing.
7. `D` is a string tilemap custom property.
