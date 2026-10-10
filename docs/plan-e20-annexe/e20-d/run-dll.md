> Report returned by one agent of the read-only discovery for E20.d, the run (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

# run-dll: the DLL side of E20.d, at HEAD dd775ca on `chantier/e20-heros`

**Method.**
- Read-only. I ran no build and no test.
- HEAD moved during the read, from 52d52b8 to dd775ca. Commit 976995f (E20.f1a T4) added the charge and pointer clears to the `+0x20` and locked branches of `MovePlayer`.
- Every line number below is `git show HEAD:<path>` at dd775ca. I relied on no uncommitted line.
- Scratch folder: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20d/run-dll/`
  - It holds `*.head2.cs` snapshots, `mipsdump.py` (capstone, skipdata, file offset = 0x800 + addr - 0x80020000) and `tables.py`.
- One read-only inline python check of the exported `.anim2d` tracks went through a Bash heredoc by mistake. It wrote nothing.

## Things that change the plan

1. **The stick rule (D-E20-8 / D-E20-15) cannot ride on "an override nibble" alone.**
   - FACT: the tie band keeps "the axis of the current facing" (D8 rule 3). Only `MovePlayer` knows the facing (`player.AnimationDirection`).
   - A pre-resolved nibble cannot encode "tie", and 0 is also the valid "no direction" nibble.
   - Carry the raw sample instead: digital direction bits D, stick bits S, signed x and y, or a 4-valued hint {none, horizontal, vertical, tie}. Resolve it inside `MovePlayer` at the two dash lookups only.
   - Touched files: five production files and two test files (§4).
   - `AlundraTickPad` itself should not change. It is hold-only and shared by the dialogue, choice, inventory and save passes.

2. **`ComputePadState` throws away exactly what D-E20-15 needs.**
   - FACT: stick and D-pad are OR-ed into the same four bits (`AlundraPlayerController.cs:91-121`, `:263-290`).
   - It reads only `IsKeyPressed` and `IsKeyJustPressed`, and drops `ButtonState.Value`.
   - FACT: the engine already supplies the value, so no engine change is needed.
     - `ButtonState.Value` exists (`CasaEngine/Engine/Input/ButtonState.cs`).
     - `InputMapping.Update` sets `Value` for an AnalogInput as the signed axis after `Invert`, and `Pressed = Value >= DeadZone` (`InputMapping.cs`).
     - `InputMappingManager.GetButtonState` returns it.
   - So x = `Value("MoveRightStick")` and y = `Value("MoveUpStick")`, both signed. The mapping dead zone is 0.5 (`PlayerSetupWriter.cs` `LeftStickDeadZone`).
   - FACT: MonoGame's mode is `IndependentAxes` (`CasaEngine/Engine/Input/GamePad.cs:98`). The stick scaling is still UNKNOWN.

3. **The cone and the resume input, `AnimationDirection`, is a per-frame value in the DLL but a per-tick value in the binary.**
   - FACT: `+0x98` is read by the cone check in 0x03 (`0x800320A0`: `lw $a0, 0x98($s0)`, row `0x80022CEC + 12 * facing`).
   - FACT: the DLL assigns `AnimationDirection` only in `AlundraFrameSyncPasses.SyncAnimation` at the end of a frame (`AlundraFrameSyncPasses.cs:143`).
   - `StepAnimationClock` (`:218`) and `TryResolveAnimationTarget` (`:454`) only detect a pending switch. They never assign it.
   - In a frame with 2 or more ticks (below 50 fps), the 2nd tick sees the previous frame's facing.
   - FACT: the binary refreshes `+0x98` in the clock pass of every tick (`0x80038B08`, table `0x800237F4`).
   - Decision needed: accept this as a documented deviation, or compute the would-be facing at the tick with `TryResolveAnimationTarget`.
   - This also resolves D8's UNKNOWN. The DLL's `AnimationDirection` order (0 down, 1 up, 2 left, 3 right) is the order the cone rows use (`AnimationTables.cs:20-24` against `tables.py` output). The cone row can be indexed directly.

4. **O-E20-12 is more than "read `IsOnGround` per tick". I recommend a slice of its own.**
   - FACT: the ground read is the head-of-frame pull. Once per rendered frame, `PullRootPose` (`AlundraEntityScriptProxy.cs:1305`) sets `IsOnGround = Controller.IsGrounded` (`:1350`) and `HeadPullGroundTrusted` (`:1354`).
   - FACT: `RunHeroVerticalTick` consumes the flag once per frame as the "fall" entry (`AlundraScriptedMotion.cs:251-253`).
   - FACT: the per-tick formula already exists, but only inside the air state (`AlundraScriptedMotion.cs:213-216`).
   - FACT: a coupled rule exists. The binary glues a descent of 3 px or less back to the ground (`0x80037848`). The DLL gets that rule on the ground from the engine's `GroundSnapDistance` (UH7b, `AlundraHeroFallAndPadJumpTests.cs:380-383`). The remaining 4 px gap is O-E19-40.
   - So a per-tick read must also port that glue rule.
   - Pins that move (details in §6):
     - UH7;
     - AT8, AT8n and AT8b;
     - the highground hero traces.

5. **No test exercises the hero against a wall at run speed, and the map-edge clamp is not ported.**
   - FACT: the wall tests (`AlundraHeroSlideTests`) impose the walk force of -159744 (speed 312 / 208). The run's 258048 is untested.
   - FACT: the binary's `ApplyEntityForces` tail (`0x80036744-0x800367E0`) clamps the adjusted force to the bounds at `+0x108/+0xFC` (X) and `+0x10C/+0x100` (Y), and sets `ForceAdjusted := 1` (`0x80036798`, `0x800367D4`).
   - FACT: the DLL has the fields `ScreenClipX/Y/Z` and `NegModX/Y/Z` (`AlundraEntityScriptProxy.cs:311-312`) but never reads them. The `RunOneKinematicTick` doc says the clamp is not ported.
   - FACT: out-of-grid samples are clamped to the nearest cell and therefore have ground (`AlundraCellsCollisionField.TrySampleGround`, `:284-286`).
   - INFERENCE: a run into a map border that has no blocking cell gives no bump in the DLL, where the original bumps.
   - FACT (plan, D-E19-28/46): crates and jars do not block the hero until E20.g.

6. **Nothing stops the run on deep water.**
   - FACT: `Slope_18c == 4` is computed (`UpdateGroundSlope`) but unused by `MovePlayer`.
   - FACT: the binary skips the land states on slope 4 and goes to the swim switch (`0x8003260C`).
   - Triangle on deep water would make the hero run on the surface at 3.94 px/tick until E20.i.
   - The plan's recipe says only "le héros marche encore". Decide: guard the Triangle entry on slope 4 (keeps walking, a documented deviation), or accept the run.

7. **The test montages cannot host the run states.**
   - FACT: `HeroAttackClock.Table` (`AlundraJumpTestSupport.cs:359-376`) has no 0x03, 0x04, 0x29, 0x2A or 0x3E.
   - FACT: it cannot express a Hold end. `Install` builds Once animations only with `ChainTo >= 0` and otherwise Loop, and never emits an `AnimationEndKind.Hold` entry.
   - FACT: the hero record's end kinds are `3` Loop, `4` Chain to 0, `41` Loop, `42` Chain to 0, `62` Hold (`sprite-records.json`).
   - So 0x3E needs either a `Hold` kind added to the table, or real-prefab arcs (`ArcRun` with `Prefabs: true`).

8. **The bump (0x03 to 0x3E) needs a native effect created from `MovePlayer`. It is small, and E20.d cannot wait for E20.e.**
   - FACT (`0x80032018-0x80032068`): when `ForceAdjusted != 0`, the binary calls `0x8003BDD8(0, 9, 0, PosX, PosY, PosZ + 0x100000)`. It then sets `+0x88 = 0x3E` and `+0x8C = (+0x8C + 0x10) & 0x1F`.
   - FACT: no other instruction follows the call. There are no forces and no caller-side random draws.
   - The pattern exists: `ReleaseChargedAttack` uses `player.ScriptHost?.EffectStage` and `stage.Emit(reservoir => ...)` (`AlundraPlayerManager.cs`, release branch), and `AlundraNativeEffects` has `reservoir.Create(false, 9, 0, x, y, z)` in `CreateHitSparks`.
   - Missing: a one-effect creator for effect 9. Z is the binary's frame, `PosZ + 1` plus `0x100000`.

9. **Only one existing test is certain to move, and no arc holds Triangle.** Details in §3.

10. **0x1C shares the 0x3E handler (`0x80032558`).**
    - Wiring both ids is one dispatch entry. 0x1C is only reachable from the hurt chain 0x31 to 0x1C (E20.c2), or from a script.
    - The plan does not say which slice owns it. I suggest E20.d wires both, with a comment.

11. **Map 392's scripted sprint is not affected.**
    - FACT: the export (`.../Ship Klark (night, inner, break)-392.events.json`, `Codes`) reads: `16 ...` (0x10, lock), then `91 129 3 67` (0x5B, hero anim 3), `31 48` (0x1F walk 48), `91 129 0 64` (anim 0), then `17` (0x11, release).
    - The sprint is set inside the lock. The locked branch returns before the state switch (`AlundraPlayerManager.cs:221-226`).
    - `AlundraAnimationSoundTests.T1_Map392_0x5B_At64_AsksTheHeroSprintSound13_Once` should keep its single sound 13. Not run.

## 1. What `MovePlayer` handles today for the run states

FACT, `AlundraPlayerManager.cs` at HEAD:
- `MovePlayer` is at `:172`.
- Dispatch is an if/else chain on `TargetAnimationId`:

  | Branch | Lines |
  |---|---|
  | LoadingMap | `~291-297` |
  | Idle and Moving | `:303-321` |
  | Jump states 2, 0x2B, 0x2C, 0x2D | `:325-332` |
  | Climbing and ClimbStill | `:336` |
  | Ground attack group | `:445` |
  | Air attack group | `:455` |

- Nothing exists for 0x29, 0x03, 0x2A, 0x3E or 0x1C. An unknown id keeps its value, so a hero set to one of them by a script would stay there.
- 0x04 is already in `IsGroundAttackGroup` (`:565`, with 0x08, 0x09, 0x10-0x13, 0x18, 0x19, 0x25, 0x3F, 0x40, 0x44, 0x49).
  - The group handler (`:445-453`) does what the binary does for 0x04: in the air it gives 0x2D, then `TryUseItem`.
  - 0x04 has no entry yet. Its exit is the Chain to 0.
- The common tail `ApplyGroundOrAirTail` (`:778-805`) runs in the binary's order: `PlayerTryAttack`, the air test (0x2C / 0x2D), the Cross edge, then Moving or Idle.
  - There is no Triangle test. It belongs between the Cross block (`:795-802`) and the final write (`:804`).
  - Binary check (`0x80031EA8-0x80031F34`): the `& 0x4000` case ends the tick before the Triangle test.
    - The DLL's early `return` at `:797` matches.
  - Binary: `0x80031F14-0x80031F30` is `andi 0x10`, then `+0x88 = 0x29` and `+0xB0 = 0`.
- Stubs are named and in place:
  - `TryUseItem` returns 1 (`:476`).
  - `PlayerTryAction` returns 0 (`:479`).
  - `PlayerTryAttack` is ported (`:500`). Its second call site, in 0x29 (`0x80031F74`), is E20.d.
  - `UpdateItemEffectState` is called at `:471`.

Binary facts re-read for the port (`ALUN_CD.EXE`, capstone):
- **0x29, `0x80031F54`.**
  - Order: `TryUseItem` (0 ends the tick), `PlayerTryAction` (non-zero ends it), `PlayerTryAttack` (non-zero ends it).
  - Then `+0x8C := s1` (`0x80031F94`, in the delay slot, executed in both outcomes).
  - Then `+0x144 == 0` gives `+0x88 = 0x2D`.
  - Then Triangle not held (`[0x80126F28] & 0x10`) gives Idle (`0x80032594` stores 0 to `+0x88`).
  - Then `+0xB0 == 0` ends the tick.
  - Then the dash table `0x80022CAC[nibble]`: -1 ends the tick, otherwise `+0x8C := value` and `+0x88 = 3`.
- **0x03, `0x80031FF4`.**
  - Order: `PlayerTryAction`, then `+0x144 == 0` gives 0x2D.
  - Then `+0x13C != 0` gives the bump (item 8 above).
  - Then no pad nibble gives `0x2A` and `[0x80126FF4] = 1`.
  - Then Triangle released gives `0x2A` and the counter 0.
  - Then the cone `0x80022CEC + AnimationDirection * 12` against the 8-way `dir`; a miss gives `0x2A` and the counter 0.
  - Then `Hold & 0xE0` gives `+0x88 = 4`.
  - `TryUseItem` is not called. `TargetDirection` is never written, except by the bump.
- **0x2A, `0x80032118`.**
  - Order: `TryUseItem`, `PlayerTryAction`, then `+0x144 == 0` gives 0x2D.
  - Then counter 0 ends the tick.
  - Then `ForceAdjusted != 0` or Triangle released clears the counter.
  - Otherwise counter + 1. Below 11: no pad bit waits, any pad bit clears the counter.
  - At 11 or more: the dash table, with -1 ending the tick without clearing.
- **0x3E and 0x1C, `0x80032558`.**
  - `TryUseItem` (0 goes to `0x8003270C`). Then `+0xAC == 1`, otherwise end.
  - Then flip `+0x8C` by 0x10. On the ground `+0x88 = 0`. In the air `+0x88 = 1`.
  - `+0xAC` is `ForceResetAnimationFlag`, the hold flag. `OnAnimationFinished` sets it to 1 on a Hold end (`AlundraEntitySpawnFactory.cs:~321`). `SyncAnimation` clears it on a switch.
- **Tables, from the exe (`tables.py`).**
  - Dash `0x80022CAC` by nibble: 1 gives 0x10, 2 gives 0x18, 4 gives 0, 8 gives 0x8, everything else -1.
  - Cone rows by facing: down {0x1C, 0, 4}, up {0xC, 0x10, 0x14}, left {4, 8, 0xC}, right {0x14, 0x18, 0x1C}.
  - Neither table is in the DLL. A grep for `22CAC` and `22CEC` finds nothing.

## 2. Hero fields and the global counter

- FACT: the hero's binary globals live on the proxy as hero-only fields (`AlundraEntityScriptProxy.cs:396-406`). Examples: `WeaponRecordPointer` (0x80127110), `WeaponStepCounter` (0x80127114), `WeaponChargeTimer` (0x80126FFC).
- The StopSprint counter `[0x80126FF4]` has no field yet. A new hero field next to them is the natural place.
- FACT: only `MovePlayer` writes it (per the sprint survey).
- INFERENCE: it is not cleared by the `+0x20` branch or the locked branch in the binary.
- `AnimCompleteCounter` (`+0xB0`) exists (`AlundraEntityScriptProxy.cs:154`).
  - It is incremented on a Loop turn and a Chain end (`AlundraEntitySpawnFactory.cs:271-277`, `:304-326`).
  - It is never zeroed on a switch, as in the binary.
  - The Triangle entry must zero it (`0x80031F30`).
  - The event runner's `0x1C/0x1D` also zero it, for the entity they target.

## 3. The pinned test and what would move

**Pinned test.**
- `Alundra.Tests/AlundraHeroJumpStatesTests.cs:216-219`, inside `SJ3_OnTheGround_TheAirAnimationsGoBackToTheIdleMovingOrJumpAgain`.
- It is a Theory with two rows, `AirMoving` (0x2C) and `AirStill` (0x2D), at lines 191-195.
- It runs on a bare proxy (no controller, no host). Today `Move(triangle, Pad(AlundraPadState.Triangle))` asserts Idle.
- After E20.d it must give 0x29, `AnimCompleteCounter == 0`, and `TargetDirection` kept (`dir` is -1, so the fallback applies).

**Everything else I looked at is not certain to move.**
- FACT: Triangle appears in tests only in SJ3 and in the inventory tests, which tick the directors directly:
  - `AlundraInventoryDirectorTests.cs:704-737`;
  - `AlundraSubInventoryDirectorTests.cs:645-653`;
  - `AlundraPlayerControllerTests.cs:163`, which only checks `ComputePadState` bits.
- FACT: arcs never hold Triangle.
  - `ArcRun.Press(button)` is called only with Square, Cross, Up and Down.
  - `HoldDirections` takes direction bits.
  - The hero-trace harness uses Left and Right only (`HeroTraceHarnessTests.cs:477-479`).
- FACT: no world-level test holds Triangle with a hero present.
  - `AlundraInventoryPortraitWiringTests.Press` uses Start.
  - `Closing_Triangle_*` ticks the director alone.
- INFERENCE: closing an inventory with Triangle and keeping it held starts PrepareSprint at once, as in the binary (the test is hold, not edge).
- Pads built by object initializer (about 60 sites) are safe if the new pad fields default to 0.

**Candidates that move only if O-E20-12 is placed in E20.d** (not run):
- `AlundraHeroFallAndPadJumpTests.UH7_...` (`:330-377`). It pins `IsOnGround == 1` through tick 14 and the fall from tick 15.
- `AlundraHeroAttackTests.AT8` and `AT8n` (`:317-323`). `Compare` uses `onGroundLag` (`:113-150`) to compare against the previous oracle row.
- `AT8b` (`:325-346`). Its ignore list covers ticks 7-15 for `force_z`, `pos_z` and `on_ground`, and tick 15 for the animations.
- `HeroTraceHarnessTests.cs ~755-790` and the two `docs/hero-trace-389-highground-*.txt` traces: first airborne frame 221, PosZ 2064384, landing frame 231.
- Tests that read `HeadPullGroundTrusted` or `HeroAirborne`: `AlundraHeroObjectTopsTests`, `AlundraLadderClimbTests`, `AlundraLoadingMapAirArrivalTests`, `AlundraPlayerManagerTests`.

**Unchanged by E20.d proper** (per the plan and my reading, not run):
- `AlundraTickStructureTests` (A8);
- the intro traces;
- `AlundraFlailBounceTests`. They call `AlundraPlayerAnimationEffects.Create` directly (`:403-423`, `:506`).

## 4. Pad, tick pad and the stick (D-E20-8 / D-E20-15)

Path today (FACT):
1. Engine `GamePad.LeftStickX/Y` = `ThumbSticks.Left`, `IndependentAxes`.
2. Four `AnalogInput` mappings (`MoveUpStick`, `MoveDownStick`, `MoveRightStick`, `MoveLeftStick`). `Pressed` is `Value >= 0.5`.
3. `ComputePadState` sets the same four PSX bits as the D-pad and discards `Value`.
4. `BuildPadState` runs once per rendered frame inside the hero's `Update`: `GameState.LastPadState = padSource.BuildPadState()` (`AlundraEntityScriptProxy.cs:1237`).
5. Each logic tick, `GameState.TickPad.Update(GameState.LastPadState.ButtonsHold)` (`AlundraWorldProxy.cs:2286`). It takes hold bits only.
6. `AlundraPadState.FromTickPad(GameState.TickPad)` is built at `AlundraWorldProxy.cs:2676`. It passes hold, pressed and released.
7. The self-driven path does the same at `AlundraEntityScriptProxy.cs:1423-1430`.

Where the rule goes:
- `AlundraPlayerController.cs`:
  - `AlundraPadState` gains the sample fields, with defaults of 0 meaning "no stick" so initializers keep their meaning.
  - `ComputePadState` keeps separate digital and stick direction masks and reads `Value` for the two relevant half-axes.
  - A missing action name is already skipped.
  - `PadStateProviderForTests` and `HeroPad.State` build pads without stick data, which gives today's behaviour.
- `AlundraWorldProxy.cs:2676` and `AlundraEntityScriptProxy.cs:1428`: attach the frame's stick sample (from `LastPadState`) to the tick pad. All ticks of a frame share one sample.
- `AlundraPlayerManager.cs`: use the sample at the two lookups only (start in 0x29, resume in 0x2A when the counter is 11 or more). Everything else keeps the OR-ed nibble: walking, `s3 == 0`, the cone, the early window.
- The facing for the tie: `player.AnimationDirection` (0/1 vertical, 2/3 horizontal). See item 3 about its lag.
- The rule needs a stick measurement to settle ε = 1/16. Nothing in the DLL logs the stick, and `Program.cs` is off limits.
- Tests: `AlundraPlayerControllerTests` (stick tests today set `IsKeyPressed` with `Value` 0, `:197-223`), a new sweep test, and `HeroPad` / `JumpHeroRig.Step` if the rig must carry a stick.

## 5. Animation data for the run states

FACT, from the hero record `192c2eeb-bdda-5bb4-92d6-39a4ae298307` in `alundra-project/Data/sprite-records.json`:

| Id | Speed | Acc byte | Sfx | IZF | End | Frames/dir | Boxes |
|---|---|---|---|---|---|---|---|
| 0x03 | 336 | 2 | 13 | 0 | Loop | 6 | none |
| 0x04 | 0 | 6 | 83 | 0 | Chain to 0 | 8 | `collision_keyframes` |
| 0x29 (41) | 0 | 0 | 0 | 0 | Loop | 6 | none |
| 0x2A (42) | 0 | 5 | 14 | 0 | Chain to 0 | 4 | none |
| 0x3E (62) | 256 | 64 (0x40) | 28 | 1024 | Hold | 6 | `collision_keyframes` |

- All four directions exist for ids 3, 4, 41, 42, 62 and 28 (`alundra-project/Entities/Alundra/bankalundra_0_anim{N}_{dir}.anim2d`). The sounds `sfx_0012/0013/0014/0028/0083.wav` exist.
- The hero's `AnimSetsByAnim` and end table come from the same record (`AlundraWorldProxy.cs:1790`). The end bridge is wired at `AlundraWorldProxy.cs:2034`.
- Already generic, no code needed:
  - Speed and acceleration: `RunOneKinematicTick` reads `AnimSetsByAnim[TargetAnimationId]` for the hero. It also does the ice and water modifiers. The 0x3E byte 64 gives shift 0 through `& 0xF`.
  - The switch sound: 13, 83, 14 and 28 through `GiveImpulse`. The 0x3E hop: the 1024 impulse starts `RunHeroVerticalTick` (`AlundraScriptedMotion.cs:237`).
  - Invulnerability: `HitAnimByte & 0x40` (`AlundraHitPass.cs:118`, `AlundraEntityScriptProxy.HitPass.cs:105`) covers 0x3E.
  - The tackle attack for 0x04: `CurrentAttack` is reloaded on a switch (E20.c1a). Its box exists in the export. No oracle scenario of the hero in 0x04 exists.
  - Charge counting in these states: `AlundraWeaponTables.AnimationCountsCharge` is true for 0 to 4, 0x0E, 0x29 to 0x2D, 0x35, 0x3E.
  - Effects dispatch: `AlundraPlayerAnimationEffects` already routes 0x29 to `PlayerRunStepEffects`, 0x03 / 0x04 / 0x2A to `PlayerWalkStepEffects`, and 0x04 to `PlayerGrassCut`. They are named stubs for E20.e.
- UNKNOWN: the exported tick length of the run animations against the binary. `HeroAttackClock` states it checked swing ids offline, not these. The sprint survey infers about 24, 32, 16 and 10 ticks.

## 6. Ground contact (O-E20-12), mechanism and cost

FACT:
- Per rendered frame, in `Update`, when the gameplay gate is open: `PullRootPose()` (`AlundraEntityScriptProxy.cs:1258`). It sets `IsOnGround` from `Controller.IsGrounded`, which the engine computed at the head of `World.Update` from the root left by the previous frame's ticks.
- `HeadPullFlushOnEntity` (`:1359`) is the flush-on-entity-top special case.
- In the air state, the tick owns `PosZ` and `IsOnGround` (`AlundraScriptedMotion.cs:213-216`).
- `IsHeroInTheAir` (`AlundraPlayerManager.cs:810`) reads `IsOnGround == 0` for a hero with a controller, and `HeroAirborne` for a bare proxy.
- Consequence: at 1 tick per frame the decisions of `MovePlayer` are exact (AT8 comment, `AlundraHeroAttackTests.cs:140-146`), and only the observable column lags.
- INFERENCE: in a frame of 2 or more ticks, the 2nd tick reads a ground state that is one tick old. This also hits the landing guard of `AlundraPlayerAnimationEffects.Create` (`IsOnGround != 0 && ForceZ <= 0`).
- Where a per-tick read would sit: `RunHeroProbes` (`AlundraEntityScriptProxy.cs:1505`), or the tail of `RunOneMotionTick` for every hero tick, with the 3 px glue rule (item 4).
- INFERENCE: the run leaves ledges often, so E20.d's recipe ("sauter d'un rebord") exposes the lag more than the walk does. I would keep it out of E20.d's core tasks and give it its own acceptance (UH7, AT8, AT8n, AT8b, highground traces re-derived from the oracle).

## 7. Mover: walls, bump, entities

FACT:
- `RunOneMotionTick` clears `ForceAdjusted` at the head of every hero physics tick (`AlundraScriptedMotion.cs:180`).
- It is set within the tick by:
  - `MoveControllerAndPullPosition` when an axis made no progress (`AlundraEntityScriptProxy.cs:2226-2260`, rule `AxisMadeNoProgress` at `:2380`, epsilon 0.01 px);
  - `SlideAlongWall` (`AlundraScriptedMotion.cs:574`) for the table `0x80023734`. A cardinal push gets one corner slide, and the oblique rule differs.
- `MovePlayer` at the next tick reads the flag. This is the same tick relation as in the binary.
- Entity contact also sets it (`XCollisionEntity`, `EntityBlockCount`). `NoObstacleSlide` is honoured.
- O-E19-42 (known): the DLL stops at the contact in 16.16 units where the binary can stop 1 to 3 units short, so `ForceAdjusted` can appear one tick later. D-E19-58 kept the DLL behaviour.
- The run direction is always cardinal. `TargetDirection` is set by the dash lookup and never by 0x03, so the oblique-push differences do not apply to the run itself.
- Gaps:
  - run speed is untested for contact and slide (item 5);
  - the map-edge clamp is not ported;
  - liftables and breakables are traversable (E20.g owns the fix);
  - a run into an NPC bumps only if that NPC blocks the controller;
  - `CheckEntityInteraction` is not called from the run states, as in the binary.
- UNKNOWN: whether the D1 oracle's synthetic field (`d1-oracle-moveplayer/tools/world.py`) can hold a wall for a run scenario. I did not open it.

## 8. Ladder priority

FACT: the gate is `AlundraPlayerManager.cs:271-279`, before `UpdatePlayerWeaponEffect` (`:283`) and before the dispatch, as in the binary (`0x80031D20-0x80031D64`).
- Conditions: `Slope_18c == 6`, nibble non-zero, `dir == 0x10`, `TargetDirection == 0x10`, `ForceAdjusted != 0`, `CarriedEntity == null`. It then sets `ClimbingAnimationId` whatever the current state.
- It already wins over any run state. A run up into a slope-6 wall climbs, provided the mover raises `ForceAdjusted` at run speed (item 5).
- `Slope_18c` is refreshed per tick (`RunHeroProbes`, `:1505`), so the run sees the slope left by the previous tick, like the binary.
- Needs a test, not code.

## 9. Gaps and the files a port touches

**Production, `Alundra/Scripts/`:**
- `AlundraPlayerManager.cs`
  - New branches for 0x29, 0x03, 0x2A and 0x3E/0x1C, with the exits in §1.
  - The Triangle entry in `ApplyGroundOrAirTail`, and the `Hold & 0xE0` to 0x04 step.
  - The second `PlayerTryAttack` call in 0x29.
  - The tables (dash, cone) and the bump effect call.
  - The two stick lookups, and optionally a slope-4 guard.
- `AlundraEntityScriptProxy.cs`
  - A hero resume-counter field next to `:396-406`.
  - The pad assembly at `:1423-1430` and `:1516`.
  - Only if O-E20-12 is in scope: `RunHeroProbes`, the ground read, `HeadPull*`.
- `AlundraPlayerController.cs`
  - `AlundraPadState` sample fields and `FromTickPad` (`:54`).
  - `ComputePadState` (`:263`).
- `AlundraWorldProxy.cs`
  - `:2286` and `:2676`, to carry the stick sample into the tick pad.
- `AlundraNativeEffects.cs`
  - A one-effect creator for effect 9.
- `AnimationTables.cs` or a new small table class, for the run tables.
- Only if scoped in:
  - `AlundraScriptedMotion.cs` (per-tick ground, map-edge clamp);
  - `AlundraFrameSyncPasses.cs` (tick-fresh `AnimationDirection`).
- No change expected: `AlundraTickPad.cs`, `AlundraPlayerAnimationEffects.cs` (its stubs fire on their own), `AlundraHitPass.cs`, `AlundraWeaponTables.cs`, the engine, the converter, `Program.cs`.

**Tests, `Alundra.Tests/`:**
- Invert SJ3 (`AlundraHeroJumpStatesTests.cs:216-219`).
- Extend `HeroAttackClock.Table` with the five states and a Hold kind (`AlundraJumpTestSupport.cs:359-376`, `Install`).
- Add the stick cases to `AlundraPlayerControllerTests`.
- New hero-run tests:
  - tick traces against the D1 oracle for flat ground, if it can run a Triangle scenario;
  - real-prefab arcs for the wind-up length, the StopSprint resume window and the cone exit;
  - a ledge exit;
  - a wall bump at 336;
  - the slope-6 priority;
  - a tackle on the map 14 Beannoïde;
  - Triangle together with Square, Cross and Circle.
- If O-E20-12 is in scope: re-derive UH7, AT8, AT8n, AT8b and the highground traces.

**Concrete gap list, short form:**
- G1 the four state handlers and the Triangle entry.
- G2 the dash and cone tables.
- G3 the resume counter.
- G4 the effect-9 bump creator.
- G5 the stick carrier and the two lookups.
- G6 the facing source for the cone.
- G7 the ground read (O-E20-12).
- G8 wall contact at run speed, and the map-edge clamp.
- G9 deep water.
- G10 the montage table and the Hold kind.
- G11 the ownership of 0x1C.
- G12 an oracle scenario for 0x04 against a target.

## 10. Not done, and UNKNOWNs

- I did not run the build, any test, or the D1 oracle. Every "would move" is a read of pins, not a measurement.
- I did not decode `0x8003270C` (the 0x3E path where `TryUseItem` returns 0). It is unreachable while `TryUseItem` is a stub.
- UNKNOWN: MonoGame's stick scaling.
- UNKNOWN: the real tick lengths of the run animations against the binary.
- UNKNOWN: whether a script outside map 392 sets the hero to 0x04, 0x2A, 0x3E or 0x1C. The discovery census lists only map 392 for animation 3, and maps 389, 478 and 135 for pickup, hold and throw.
