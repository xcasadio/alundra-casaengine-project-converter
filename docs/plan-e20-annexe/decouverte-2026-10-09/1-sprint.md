# Survey 1: the hero sprint (Triangle run)

> Read-only discovery of 2026-10-09 for E20 (docs/plan-e20-heros.md, section 0.2). Raw output of one workflow
> agent, kept as evidence. Where this file and its binary check disagree, the check and the plan section 0.2 win.

# Hero SPRINT (Triangle run): findings from the decompilation and ALUN_CD.EXE

Labels: [F] fact, [I] inference, [U] unknown. Binary addresses are for `ALUN_CD.EXE` (France). Decompilation means `PlayerManager.cs` (`alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/`).

## 0. Summary

- [F] Sprint is a state machine inside `MovePlayer` (0x80031B50): PrepareSprint 0x29, Sprint 0x03, StopSprint 0x2A, SprintDash 0x04, SprintAgainstWall 0x3E.
- [F] The DLL has none of this. Triangle is bound and reaches `ButtonsHold`, but nothing reads it. `Alundra/Scripts/AlundraPlayerManager.cs:63-69` and `:418-424` state that the run stays a no-op.
- [F] Generic kinematics (speed/acceleration from AnimSets), animation chain/hold bridging, `AnimCompleteCounter`, the sound-on-switch rule and the Z impulse already work for any animation id. Those are the pieces the scripted sprint at map 392 uses today.
- [F] No MP or stamina cost, no camera effect, and no timer that ends a sprint.
- [F] The decompilation matches the binary on every sprint branch, with two differences listed in section 8.

## 1. Entry condition

- [F] Common tail 0x80031EA8, in this order: `jal 0x8002EAF4` (PlayerTryAttack; any non-zero result ends the tick), then `lw 0x144(v1)` (IsOnGround; zero goes to the air branch 0x80031F44).
- [F] Then Cross edge (`lhu 0x12(a0)`, `andi 0x40`): jump 2 or 0x2B, unless `VramOR & 0x4000`.
- [F] Then Triangle held (0x80031F14 `lhu 0x10(a0)`; 0x80031F1C `andi v0,v0,0x10`). It writes `24020029`/`ac620088` (anim 0x29) and `ac6000b0 sw zero,0xb0(v1)` (AnimCompleteCounter = 0) at 0x80031F28-0x80031F30. Pad struct at 0x80126F18: +0x10 hold, +0x12 just-pressed.
- [F] Only the hold bit is tested, not an edge. A Square edge (attack) or a Cross edge (jump) in the same tick wins over Triangle.
- [F] Ground only. The tail is reached from Idle/Moving (case 0x80031E38) and from the jump states 2/0x2B/0x2C/0x2D (0x80031E84, so also on the landing tick).
- [F] Slope 4 (deep water) skips the land states and goes to the swim switch at 0x8003260C (slope table 0x80022D1C). Slope ≥ 8 ends the tick. No cell flag forbids a sprint start.
- [F] PrepareSprint (0x80031F54), each tick: TryUseItem 0x8002ED64, PlayerTryAction 0x8002EEAC, PlayerTryAttack 0x8002EAF4, in that order; any stop value ends the tick. The first test of the tick is airborne, which gives 0x2D.
- [F] Every tick of the wind-up writes `TargetDirection = pad direction` (`ac91008c` at 0x80031F94).
- [F] Triangle released sends it to Idle (0x80032594).
- [F] With Triangle held it needs `AnimCompleteCounter != 0` (`8c8200b0`, 0x80031FBC) and a valid `g_dashDirections[hold>>12]` (table 0x80022CAC, read at 0x80031FD8). Then it writes anim 3 and that direction.
- [F] The only valid pad combinations are one cardinal bit. Up gives 0x10, Right 0x18, Down 0x00, Left 0x08. All 12 other nibble values are -1, diagonals included.
- [I] The wind-up is about 24 ticks. Anim 0x29 is a Loop with 6 keyframes at 0.08 s (4 ticks each), and 1 tick = 1/50 s. The last frame's delay is not read.

## 2. States and transitions

AnimSet data come from `alundra-project/Data/sprite-records.json`, hero record `192c2eeb-bdda-5bb4-92d6-39a4ae298307`. The `Acceleration` field is the whole byte 0x0D: the low nibble is the shift, 0x20 is the sound bank bit, 0x40 is invulnerable.

| State | Speed | Acc | Sfx | Z impulse | End | Length |
|---|---|---|---|---|---|---|
| 0x29 PrepareSprint | 0 | 0 | 0 | - | Loop | ~24 ticks per loop [I] |
| 0x03 Sprint | 336 | 2 | 13 | - | Loop | no limit |
| 0x04 SprintDash | 0 | 6 | 83 | - | Chain to 0 | 32 ticks [I] |
| 0x2A StopSprint | 0 | 5 | 14 | - | Chain to 0 | 16 ticks [I] |
| 0x3E SprintAgainstWall | 256 | 0x40 (shift 0) | 28 | 1024 | Hold | 10 ticks [I] |

For reference, Moving 0x01 is speed 208 with acc 1, and JumpMoving 0x2C is speed 196 with acc 1.

**Sprint 0x03** (0x80031FF4):
- [F] PlayerTryAction; non-zero ends the tick. TryUseItem is not called.
- [F] Airborne gives 0x2D (0x8003279C).
- [F] `ForceAdjusted != 0` (`8e02013c`, 0x80032018): effect `CreateEffectEntity(0, 9, 0, PosX, PosY, PosZ+0x100000)` (`24050009`, `jal 0x8003BDD8`, 0x80032048), then anim 0x3E (`2403003e`, 0x80032054) and `TargetDirection = (dir+0x10)&0x1F`.
- [F] No d-pad bit (`bnez s3`, 0x8003206C): StopSprint with `[0x80126FF4] = 1`.
- [F] Triangle released (0x80032094): StopSprint with `[0x80126FF4] = 0`.
- [F] The pad direction must equal one of the 3 words at `0x80022CEC + AnimationDirection*12`. The rows are {0x1C,0x00,0x04} for down, {0x0C,0x10,0x14} for up, {0x04,0x08,0x0C} for left and {0x14,0x18,0x1C} for right. Outside that cone: StopSprint with `[0x80126FF4] = 0`.
- [F] Otherwise `ButtonsHold & 0xE0` (Circle, Cross or Square; `304200e0`, 0x80032104) gives anim 4 (`24020004`, stored at 0x80032910). With none of those, it stays in Sprint.
- [F] Sprint never writes `TargetDirection`. The run is a straight cardinal line even while diagonals are held. A pad combination with value -1 falls back to the current direction, which is inside the cone.

**StopSprint 0x2A** (0x80032118):
- [F] TryUseItem and PlayerTryAction first, then airborne gives 0x2D.
- [F] If `[0x80126FF4] == 0` it stays.
- [F] If counter ≠ 0 and not blocked and Triangle held, the counter increments (0x8003218C; `slti 0xb` at 0x80032190).
  - While the counter is ≤ 10: no pad bit means wait; any pad bit clears the counter.
  - At counter > 10: a valid cardinal direction gives Sprint again, possibly in a new direction.
  - Triangle released or blocked clears the counter.
- [F] Only MovePlayer touches `[0x80126FF4]` (accesses at 0x80032088, 0x800320F8, 0x80032158, 0x8003218C, 0x800321D0).
- [I] The Sprint-resume window is about StopSprint ticks 10 to 16. It then chains to Idle.

**SprintDash 0x04**:
- [F] The group handler at 0x80031E18: airborne gives Jump, then TryUseItem. The pad is otherwise ignored until the chain to 0.

**SprintAgainstWall 0x3E** (0x80032558, shared with DamageKnockBack 0x1C):
- [F] `ForceResetAnimationFlag == 1` ends it. The direction is flipped back; on the ground it gives Idle, in the air it writes the value 1 (see section 8).
- [F] A TryUseItem result of 0 flips the direction again.

## 3. Speed, acceleration, distances

- [F] `UpdateEntityPhysics` 0x80036614 computes target = `offsetX/Y[dir] * AnimSet.Speed` (tables 0x80023654 and 0x80023694: ±768 horizontal and ±512 vertical at cardinals) and step = `|target - Force| >> (byte0xD & 0xF)`.
- [F] The target and step are recomputed only when speed, direction or acceleration change (0x80036628-0x80036638).
- [F] Walk: 159744 (2.44 px/tick) horizontal, 106496 (1.63 px/tick) vertical.
- [F] Sprint: 258048 (3.94 px/tick, 197 px/s) horizontal, 172032 (2.63 px/tick) vertical. That is 1.615 times walking speed. It takes 4 ticks to reach full speed.
- [F] PrepareSprint (speed 0, shift 0) stops the hero in 1 tick.
- [F] Ice (`VramOR & 0x20`) divides the step by 16. Shallow water (`VramOR & 0x08`, no boots) halves the target. Both are already in the DLL (`AlundraScriptedMotion.cs:445-520`).
- [I] From full speed on flat ground: SprintDash slides about 94 px horizontally (about 63 px vertically); StopSprint about 47 px.
- [I] The bounce recoils about 3 px/tick for 10 ticks (about 34 px with the Idle tail) and hops about 18 px for roughly 17 ticks. Measure these on the existing emulator before pinning tests.
- [F] The Z impulse applies for exactly one `UpdateAnimation` call (`sw zero,0xf8(s0)` at 0x80038AE4; the write is only at 0x80038B64). `IsZForceApplied` is 1024, so the hop starts at `1024<<8` (4 px/tick) (0x800368E4).
- [F] During the bounce the hero is invulnerable: `AnimFlags & 0x40` makes the frame-collision pass skip it (0x8003943C).
- [F] Map header bytes (477 exported tileMaps): `C` = 7 on every map (dust effect id), `BalanceLevel` = 2 (tile-break effect), `SlideEffectId` = 0 except two maps at 1. Those two maps add a shift on the force (maps 159 and 160).

## 4. What stops it

| Cause | Result |
|---|---|
| All d-pad bits released | StopSprint with counter 1 (resumable window) |
| Triangle released, or pad direction outside the cone | StopSprint with counter 0 (not resumable) |
| Ledge or airborne | 0x2D, no momentum carried (0x2C then runs at speed 196) |
| Wall, entity or map edge (`ForceAdjusted`) | Effect 9, then 0x3E |
| PlayerTryAction | Pick-up or carry |
| Deep water (slope 4) | SwimmingStill 0x1D (decoded jump table 0x80022E7C) |
| Slope 6, Up held and facing 0x10 | Climbing 0x0E, tested before the animation switch at 0x80031D20 |

- [F] `ForceAdjusted` is set only when no sub-step of the move is accepted (`PhysicsEngine.cs:808-815`; map-edge clamp in the binary at 0x80036798 and 0x800367D4). The bounce therefore happens the tick after contact.
- [F] If exactly one lateral body corner is blocked, a straight move slides at ±0xC000 per tick (`PhysicsEngine.cs:621-641`). The binary was not re-read for this, and the DLL leaves it to E19.h.
- [F] Blocked by an entity (`candidate != null`) also sets `ForceAdjusted`, so it bounces. Which entities block the hero in the DLL today is partial: liftable and breakable records are traversable (O-E19-23).
- [U] Pushing movable blocks while sprinting: no sprint-specific code found in the decompilation.

## 5. Cross, Square, Circle during a run

- [F] Cross has no jump in 0x29, 0x03 or 0x2A. Held, it only triggers the `& 0xE0` dash. This matches F19 in `docs/plan-e19-d2c-valeurs.md:362`.
- [F] Square, Cross or Circle during Sprint gives SprintDash. Circle is swallowed: Sprint never calls TryUseItem, and the edge is gone by the next tick.
- [F] Square during PrepareSprint gives a normal weapon attack (PlayerTryAttack runs before the sprint test).
- [F] SprintDash carries the hero's Tackle attack. In `balance.json`, record 0, `Attacks[5]` is attr 133: type 5 "Tackle", power 2, and bit 7 adds the equipped items' power. Sprint 3 and the other sprint states have no attack entry.
- [F] SprintDash (and weapon attacks) call `CheckAndTriggerTileEffect` 0x8003A7B0 via the table at 0x800228AC.
  - It destroys breakable tiles under the hit box: walkability bit 2, tile height inside the hit-box Z range.
  - It plays sound 0x1F, drops an item, and creates effect `Info+9`.
- [F] Tackle response, DamageResponses index 4, over the 512 first-level entries: 288 are 0x80 (immune), 118 are 0x10, 91 are 0x40 (kill), 15 other.

## 6. Sounds and native effects

- [F] Animation-switch sounds are 13 (Sprint), 83 (SprintDash), 14 (StopSprint) and 28 (bounce). Files `sfx_0013`, `0014`, `0028` and `0083` exist. The DLL already plays them on any switch (ADR-0028, `AlundraFrameSyncPasses.cs:280-288`).
- [F] `CreatePlayerAnimationEffects` has its real entry at 0x8002FB14 (called from 0x80031BDC with mode 1 in the locked branch, and from 0x80031C3C with mode 0). The 0x8002FB1C in the effects census is the prologue.
- [F] PrepareSprint: a step sound every 8 ticks (`FrameCounter & 7`, id `g_prepareSprintParameters[Slope]` = {12,21,19,16,21,0,12,0}), even though the hero is stationary.
- [F] PrepareSprint dust: effect `Info+0xA` (7 on all maps), or effect 6 on slopes 1, 2 and 4. The interval masks come from `AnimCompleteCounter-1` clamped to 0..3, with masks 7, 7, 3, 3 on land. Forces come from `g_prepareSprintParameters[8..39]`.
- [F] Sprint step sound: every 8 ticks, only while `ForceX` or `ForceY` is non-zero (gate at 0x8002FDD0). Sprint dust every 4 ticks on land and every 8 in water, thrown at -0.5 times the hero force.
- [F] SprintDash and StopSprint have dust (forces times 1/8) and no sound (0x8002FE68 sets `s0=1` and skips the sound).
- [F] The effect code is not gated by mode, so the scripted sprint on map 392 produces these steps and dust in the original. Map 392's `0x5B [0x81,3,0x43] @64` only sets anim 3.
- [F] The camera follows `PosX/Y/Z` only (`GameEngine.cs:1747-1751`). No lookahead or animation dependence was found; this was not checked in the binary.
- [F] The charge timer (`g_playerWarpTimer`) keeps counting in states 0x29, 0x03, 0x04, 0x2A and 0x3E when Square is held. This follows from the per-animation byte at `0x80028C78+0x9C+2*anim`, which is 1 for those animations.

## 7. What the DLL has and lacks

Has:
- Hero prefab (`alundra-project/Entities/Alundra/bankalundra_0_anim{3,4,41,42,62}_{down,left,right,up}.anim2d`) and exported AnimSets.
- Kinematics for any animation id, including ice and water.
- Chain and Hold bridges and `AnimCompleteCounter` increments on loop and chain turns. It is not reset on a switch, as in the binary (`AlundraEntitySpawnFactory.cs:262-330`).
- `GiveImpulse`, `ForceAdjusted` with the binary's no-progress rule (E19.a3), the slope-6 climb gate, an effect reservoir (`AlundraEffectReservoir.cs:200-223`, `AlundraNativeEffects.cs:28`), and the pad binding (`PlayerSetupWriter.cs:140`).

Lacks:
- All sprint state logic.
- The three calls (TryUseItem, PlayerTryAction, PlayerTryAttack) in their binary order. They are no-ops.
- `FrameCounter` (+0x1C): it is declared at `AlundraEntityScriptProxy.cs:72` and copied, but never incremented.
- `CreatePlayerAnimationEffects`, `CheckAndTriggerTileEffect`, and the hit pipeline.
- A tick-aligned `MovePlayer`. It runs once per rendered frame (`AlundraEntityScriptProxy.cs:1380-1428`), while the binary runs it every tick. The StopSprint counter must count ticks, as `JumpStartTickStamp` does for the jump.
- Analog input mapping: left stick axes are thresholded per axis at 0.5. A near-diagonal stick position produces two bits, which never starts a sprint under the original rule.

## 8. Decompilation versus binary

1. [F] **0x3E and 0x1C end while airborne.** The binary stores `ForceResetAnimationFlag`, which is 1 (Moving), at 0x8003259C/0x800325A0. The decompilation writes Jump 0x2D and leaves a commented-out `(uint)ForceResetAnimationFlag`. Moving then resolves to 0x2C or 0x2D one tick later.
2. [F] **`AnimCompleteCounter` is not reset on an animation or direction switch in the binary.** `UpdateAnimation` 0x80038AB4-0x80038E10 touches +0xB0 only at the increments 0x80038D6C and 0x80038D7C, one of which is on the chain path. The decompilation resets it at `EntityManager.cs:223` and does not increment on a chain.
   - Consequence: after one full wind-up loop, turning during PrepareSprint keeps the count. A new cardinal direction can sprint at once, instead of needing another loop.
3. [F] `ApplyEntityForces`: the binary shifts `(ForceX + XForceTable[tile])` by header byte +8. The decompilation shifts only the table term. It only matters on maps 159 and 160.

## 9. Unknowns

- [U] The binary frame delays of 0x29, 0x2A, 0x04 and 0x3E (the bank data is not in the EXE); the lengths above are inferred from exported keyframes.
- [U] Real tackle damage with an equipped sword (the equipment records for anim 4 were not read).
- [U] Whether corner sliding applies to the sprint exactly as to walking.
- [U] Pushing movable blocks while sprinting.
- [U] The effect-table values for slopes 1, 2 and 4 in detail.

## Open questions for the author

1. Does the Sprint task own the tackle consequences (Tackle damage `Attacks[5]` and breakable-tile destruction), or are they delivered with the attack task's hit pipeline (FrameCollision hit boxes)?
2. Keep the original's cardinal-only sprint start, which a stick near a diagonal never satisfies, or add an angle snap for stick input?
3. May the sprint counters be driven by logic ticks with a tick stamp (as E19.d2c2 did for the jump), or should the hero's state pass itself run per tick? The second choice also changes how the Square and Cross edges are consumed in the attack task.
4. Is native-effects work (`CreatePlayerAnimationEffects`, which also covers the walk footsteps absent today and the landing dust) a separate slice for walk, run and landing, or sprint-only?

## Suggested slice boundaries

- **SP0, groundwork shared with the attack task, no visible behaviour.**
  - Call TryUseItem, PlayerTryAction and PlayerTryAttack in the binary order, as explicit stubs.
  - Increment the hero `FrameCounter` per tick.
  - Add the tick-stamp pattern.
  - Zero `AnimCompleteCounter` on entering 0x29.
- **SP1, sprint state machine.**
  - The 5 handlers, the Triangle entry in the common tail, the `& 0xE0` entry to anim 4, and the exits (air, water, slope 6).
  - Acceptance: tick traces computed from the binary with the existing emulator (`docs/plan-e19-h2-annexe/tools`). Cover the wind-up to run state sequence, the StopSprint resume window, cone exit, ledge, wall bump (hop and recoil values), water, and the slope-6 priority.
  - Take the binary's behaviour for the 0x3E exit (section 8, point 1).
- **SP2, native effects and sounds.**
  - Footstep sounds, dust and splash tables, effect 9 on a bump, and the locked-branch call (this changes map 392's scripted sprint).
  - Needs `FrameCounter` (SP0), `AlundraRandom` and the effect reservoir.
- **SP3, tackle consequences (with the attack task).**
  - Tackle damage and breakable-tile destruction, together with the hit pipeline.
