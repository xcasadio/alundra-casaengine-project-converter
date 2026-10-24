# E20.g1a - measured results

Start HEAD `b62726ff`; the state at the start and the predicted list are in `README.md`.

## T1 (phase A) - tests first, the skeleton, then the code

Commit of the code of T1: see `git log` (message `feat(e20-g1a): T1 ...`). Files: `Alundra/Hero/CarryRules.cs`, `Alundra/Ai/Objects/LiftableAi.cs`, `PlayerManager.PutInHands` (in `Alundra/Hero/PlayerManager.cs`),
`EntityScriptProxy.CarryLockCounter` (in `Alundra/Entities/EntityScriptProxy.cs`), and the tests below. The stubs `PlayerManager.PlayerTryAction` and `PlayerManager.UpdatePlayerCarriedEntity`, `MovePlayer`, `TickMotion`,
`WorldProxy.DestroyEntity` and `NativeSpriteRegistry.CreateForProduction` are not touched: phase A changes no behaviour.

### New tests (68 test cases in 5 classes and a bench), red on the skeleton, green on the code

The skeleton (every method `throw new NotImplementedException()`, same signatures, `LiftableAi.SpriteTypes` and `Keys` empty) compiled; the new tests run on it: **68 failed, 0 passed**. On the code: **68 passed**.

| Class (folder mirror of its subject) | Cases | Plan item |
|---|---|---|
| `Hero/CarryRulesStepTests` | 7 | (c1) and (c2), the pure half: `StepTowards`, `FreeOffset`, `LockedPull`, `CanDestroyLocked` |
| `Hero/CarryRulesModesTests` | 32 | (c1) mode 0 (7 vectors of `c1_equality`, the trace of 32 calls with X at the 12th, nothing carried, a carried entity without proxy), (c2) mode 1 (counter 0-4 and -3, the fifth call, the thresholds 0x1FFFF / 0x20000 on four triples each, `AttackCount` 0 against 1, no world), (c3) mode 2 (and 255) |
| `Hero/CarryGroupTests` | 13 | (c6) the table of the carrying group (0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30), the lift gestures 0x05 and 0x0B, `LiftAnimation` |
| `Hero/PutInHandsTests` | 5 | (c5) |
| `Ai/Objects/LiftableAiTests` | 11 | (c4) C23 (not carried / carried without a code / the four codes / the table / the flags / a carrier without proxy), the seven types against the export, the registration in a registry of a test |
| `Hero/CarryBench` | - | the bench (two real proxies of two real engine entities, the sheet, a world that records `DestroyEntity`) |

Each "without effect" case is in the same assertion or the same test as its positive neighbour (counter 0-4 next to 5, thresholds 0x1FFFF next to 0x20000, `AttackCount` 0 next to 1, nothing carried next to carried, not carried next to
carried, the object on a head next to the object on another entity's head, the null-equals-null hero next to the real one).

Not done in T1, by the plan: (w1) to (w12) (T2), the branchings (T3), the registration and (r1), (r2) (T4), the ADR and the mutants of the DLL code (T5). The mutants below are a check of the T1 tests, not the T5 list.

### Real mutations of the T1 code (script `mut.py` in the session scratchpad: apply, rebuild, run the 68 tests, restore the bytes)

30 mutants of `CarryRules`, `LiftableAi` and `PlayerManager.PutInHands`; 28 killed by at least one of the 68 tests, 2 equivalent (proved):

- killed: mode 0 `<` to `<=` (7 red); free step 1 px to 2 px (10); locked step 2 px to 1 px (6); destroy limit 0x1FFFF to 0x20000 (4); counter 5 to 4 (1); counter not reset in mode 0 (9); `AttackCount` rule dropped (2); mode 2 without -1 (3); mode 2 clearing the
  link (2); counter signed to unsigned (1); mode 1 destroy resetting the counter (6); offset read from the offset words (20); group with the heavy test before the Square (1); states 0x06 and 0x2E swapped (4); `NoJump` ignored (1); `NoThrow` ignored (1); lift gesture
  0x0B not leaving at the ground (1); C23 flags not cleared (7); C23 carrier link kept (5); C23 table 2 to 3 (4); C23 riding kept (4); C23 without its carried test (5); `PutInHands` without the marker (1); with the riding kept (1); with null equals null for "on the head" (1);
  with the ground inverted (2); without the Z of the offset (2); head height 32 px to 31 px (9).
- equivalent: `StepTowards` with `gap <= step` (when the gap equals the step, `current +- step` is the target, so both branches return the target for any step > 0) and `StepTowards` with the gap taken by `Math.Abs((long)difference)` cast to `uint`
  (for every 32-bit difference, `|d|` fits an `uint` and equals `(uint)-d` / `(uint)d`).

### Suites

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets` on build and test), then Debug built last (the copy to `alundra-project/Alundra.dll` not neutralised); `--blame-hang-timeout 60s`; disjoint partitions by test namespace
(`FullyQualifiedName~Alundra.Tests.<ns>.`): 1 = Arcs, Ai, Audio, Cameras, Cells, Combat, Dialogues; 2 = Entities, Events, ExportGuards, Hero, Items; 3 = the negation of the twelve.

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 | 1 224 | 0 | 0 |
| 2 | 2 713 | 0 | 0 |
| 3 | 1 793 | 0 | 0 |
| total | 5 730 (= 5 662 at the start HEAD + 68, equal to `--list-tests`) | 0 | 0 |

- Existing tests moved: **none**. `--list-tests` before (5 662 names) and after (5 730): none of the 5 662 is missing, the 68 added are the new ones. Predicted list for T1: empty; measured: empty.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- The four `docs/hero-trace-389-*.txt` files were rewritten by the suites (line endings); `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0 after the Release and after the Debug suites, then restored by `git checkout`, never indexed.
- No diff of `RunHeroVerticalTick` or `SnapHeroAtApex`; the engine, the converter and the data are untouched.

### Decisions of T1 (local design, none changes the plan)

- `CarryRules.UpdateCarried(hero, mode, IEntityWorldContext? world)` takes the host that destroys in mode 1 as a parameter (phase B passes `player.ScriptHost as IEntityWorldContext`, the plan's rule): the direct tests then need a world of 5 members, not a full `IScriptHost`.
- A sheet of balance absent (`BalanceRecord` null, a bare montage) is read as `AttackCount` 0 (the binary always has a sheet; a bare proxy has none), so mode 1 is mode 0 there. Phase B tests that need the destruction give a sheet with an attack.
- A code of throw other than 1, 2, 3 and -1 does not release (`LiftableAi.ReleaseAnimation` returns null): the binary reads the words around the table `0x80027400`; no writer produces such a code (mode 2 writes -1, the throw of g2a 1 to 3).
- `PutInHands` lives in `PlayerManager` (D-G-2, "couture interne"), the pure choice of the state in `CarryRules.LiftAnimation`. The marker `HasLogicalZ` is posed by it and never removed.
- Mode 2 writes the raw fields of the object only (like the binary); whether the root of a marked entity follows at once is a question of phase B ((w8)).
- Any `mode` other than 0 and 1 is mode 2 (the model `g1a_models.py upce` and the critic's vector with the mode 255).

### Owed to the slice (not T1)

- The commun T0 (`docs/plan-e20-annexe/e20-g/`: oracle copy and manifest, `c4_climb_lift`, the 16 one-word mutants, the probes P-G-1 to P-G-5) is not on the branch; see `README.md`. T2's (w2), (w4) and (w5) take their series from the plan table and
  from the models of `docs/plan-e20-annexe/e20-g1a/work/`, which the critic replayed; the oracle acceptance line stays open.

## T2 - the world tests, red before T3

Start state: HEAD `f13637d8` (T1). The T2 commit adds tests and test support only; the production files are those of T1 (the stubs `PlayerTryAction` and `UpdatePlayerCarriedEntity`, the T1 pure rules, no branching).

### The bench (local design decisions of T2)

- **`CarryRig`** (`Alundra.Tests/Hero/CarryRig.cs`): `JumpHeroRig` (the real hero controller settings, a field of synthetic cells, the animation sets and the Chains of `HeroAttackClock`, to which T2 appends the eight carrying states 0x05, 0x06, 0x07,
  0x0B, 0x0C, 0x2E, 0x2F and 0x30 with the speeds, accelerations, impulses, sounds, Chains and lengths read in the export: `sprite-records.json` AnimSets and IdsvAnimDirs, and the `.anim2d` of the hero; 36 ticks for 0x05, the table's "5 from L to L+35,
  12 at L+36"). A frame of 1, 2 or 3 ticks is **played tick by tick over all the entities** (`ContactHost.DrivesTicks`, an opt-in explicit implementation of `IScriptHost.DrivesEntityTicks`; the host class lost its `sealed`): `MovePlayer` of the hero, the
  programs (the pick phase of every entity and the native C23 of the crate, run by the montage's host as the registry of T4 will), the lists, the animation clock, the physics pass in the order of the list (the hero first) with a new pass number per
  tick, the end pass, then the publication of the frame - the order of `WorldProxy.RunEntityBlock`. The self-driven mode of the other montages plays each entity's whole frame in turn, which cannot test a walk or a jump that an object follows at 2 and 3
  ticks per frame.
- **`CarryHost`**: `ContactHost` that is also the `IEntityWorldContext` of the single-argument `DestroyEntity` (mode 1 reaches it through `player.ScriptHost`); it records the entity and the tick, flags it (status 4) and clears the carrier as the binary
  does. The production clearing is `WorldProxy`'s (W5) and is tested alone (`DestroyEntityCarrierTests`), so (w7) does not depend on it.
- **The tick L**: `CarryRig.LiftNow()` puts the crate in the hands INSIDE the tick, after `MovePlayer` and before the clock and the physics (where `TryLift` lifts in the binary): the mode 0 of L found nothing, the series of the lift starts at L+1, and the
  animation clock of L starts the gesture (5 for L..L+35, 12 at L+36, as the table says). Putting the crate in the hands after the physics of L shifts the Chain by one tick; the first draft did that and the table's L+36 caught it.
- **The scenes of the walks, the jumps, the lock, the water and the kind** (w3, w4, w5, w7, w8, w11) start with the crate on the head of the hero (32 px over him, `RidingEntity` the hero, `LiftNow(fromTheHead: true)`): `PutInHands` then gives the
  carrying stand 0x0C at once (D-G-2) and the offset (0, 0, 32) is there without the series of the lift. These tests do not depend on the draw-in of mode 0, which only (w2) pins, so removing the body of (W1) turns (w2), (w7), (w8) red and not (w3),
  (w4), (w5), (w11), as the plan's acceptance says (first drafts that lifted from 21 px east made them depend on it).
- **The oracle values used**: the lift series (w2) of the table (offset (21, 0, k) to L+11, (32-k, 0, k) to L+32, (0, 0, 32) after; the hero's offset words equal; the gesture); the walk at 2.4375 px (159 744 raw) from the FIRST tick (sheet 7, no
  acceleration; the free walk of the sheet 1 has a ramp: its first step is smaller and it reaches the same pace after 8 ticks); the blocking at 133.0 against a wall of 16, 32 and 48 px; the standing jump 46 / 48 / 12 and the walking one 6 / 47 / 7 with
  the heights of the sheet (5, 9.5, 13.5, 17, 20, 22.5, 24.5, 26, 27, 27.5, 27.5, 27, ..., 5 px for T..T+19, 0 at T+20) and the object 32 px over the hero at every tick; the lock destroying at the 22nd call (the counter at 5 after it; the offset
  32 px for the calls 1 to 5 then 30, 28 ... 0 px for the calls 6 to 21); deep water (the hero 0x1D, the crate with animation 3, no carrier and `0x332374` at the tick after the slope 4); the heavy object (kind 3) at the stand, deaf to the direction and
  to the Cross.
- **(w5)**: the pins are the DLL's own for the free hero, and the carried scene must equal them. The top of an object of 16 px: the free hero lands on it at T+17 (foot at 16 px, `IsOnGround` 1) and rides it from T+18 (the pin of UH-10, a jump started
  from x = 120; here from x = 109 plus 0, 3 or 6 ticks of walk); the plan's table says "19 ticks after" (t66 to t85) in the oracle's own tick convention, one more than the DLL's UH-10 value: the test pins the DLL's value and the equality with the free hero
  (support, height, ground flag) from T to T+20. The hero with the object walks on the top later than the free one (no ramp), so the comparison stops at T+20. The platform: the montage of UH-14 (`HeroPlatformRideTests`); D5b is merged at the start HEAD, so
  (w5) is pinned in PHASE 0, without a tick of delay: the hero's X minus its start is `-n px` at every tick n, for 50 ticks, with and without the crate, the platform carries the hero (`RidingEntity`) the whole time, the crate stays (0, 0, 32).
- **(w8)** starts the hero IN the deep water (x = 178: the cells from 144 px have the ground property 8, the slope 4, under the hero and under the whole box of the crate) with the crate lifted in the first tick; the swim branch of the next tick lets it go. So (w8)
  depends neither on the walk (W4) nor on the follow (W3): a first draft that walked into the water turned red when either was removed, which the plan's retreat sets do not say.
- **(w6) and (w12)** are the two guards the plan says are green from T1; they are green on the T1 production (below).
- **(w10)**: the site of the draw sort is `FrameSyncPasses.RunWallInterleaveSortKeyPass` (the pass that writes `DepthSortable2DComponent.Elevation` of every entity of the frame, from `PosY` and the IDSV); `EntityDepthKey` serves only the anchors of the
  effects. (W6) therefore has a site and (w10) stands (no "tombe" clause, no open point).

### Files

New (tests): `Alundra.Tests/Hero/CarryRig.cs` (`CarryHost`, `CarryRig`), `CarryScenes.cs`, `CarryWorldTests.cs` (w1, w2), `CarryWalkAndJumpTests.cs` (w3, w4), `CarryTopsAndPlatformTests.cs` (w5), `CarryLockSwimAndStatesTests.cs` (w7, w8, w11),
`CarryGuardTests.cs` (w6, w12), `Alundra.Tests/Maps/DestroyEntityCarrierTests.cs` (w9), `Alundra.Tests/Maps/Loop/CarriedSortKeyTests.cs` (w10). Modified (test support): `ContactHost.cs` (unsealed, `DrivesTicks`), `JumpHeroRig.cs` (an optional
`hostFactory`), `JumpNpcRig.cs` (the eight rows of `HeroAttackClock.Table`). `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing).

### Measured on the T1 production (the stubs): 53 new test cases, 47 red, 6 green

Run: the eight production files T3 touches checked out at `f13637d8`, `Alundra.Tests` built in Release with the neutralised copy, the 53 cases run (`--blame-hang-timeout 60s`), T3 restored afterwards. The red ones fail for the reason of the missing branching:

| Test (cases) | Red / cases | First failure on the stubs |
|---|---|---|
| (w1) `CarryWorldTests.W1_TheCarriedObject_IsPlacedAtTheHeroPlusTheOffset_EveryTick_WithNoForceOfItsOwn` (1, 2, 3 ticks per frame) | 3 / 3 | tick 5: adjusted 0, collidedZ 0, ground 1, and the carried crate is in the collidable list (the position equality itself holds: nothing moves) |
| (w2) `W2_TheLiftTrace_RisesBy1PixelATick_...` | 3 / 3 | L+1: offset (21, 0, 0), expected (21, 0, 1) |
| (w3) `CarryWalkAndJumpTests.W3_TheWalkWithAnObject_StartsAtFullSpeed_...` | 3 / 3 | tick 1: walked 0 px, expected 2.4375; animation 12, expected 7 |
| (w3) `W3_TheWalkIntoAWallOf16_32Or48Pixels_IsStoppedAt133_...` (3 heights x 3) | 9 / 9 | the hero with the object stops at 109 |
| (w4) `W4_TheStandingJumpWithAnObject_...` | 3 / 3 | T+0: animation 12, expected 46; height 0, expected 5 |
| (w4) `W4_TheWalkingJumpWithAnObject_...` | 3 / 3 | T+0: animation 12, expected 6; step 0 px, expected 2.4375 |
| (w5) `CarryTopsAndPlatformTests.W5_TheJumpOntoAnObjectOf16Pixels_...` | 3 / 3 | offset 0, T+0: height 0 against 5 without the crate (the hero does not walk, so does not jump) |
| (w5) `W5_TheJumpAgainstAnObjectOf32Pixels_...` | 3 / 3 | T+0: height 0 against 5 |
| (w5) `W5_ThePlatformThatMovesWest_...` | 3 / 3 | carried: tick 1: the crate is at (1, 0, 32) (it stays where it was while the hero is carried west) |
| (w7) `CarryLockSwimAndStatesTests.W7_ALockOfControl_...` | 3 / 3 | call 6: offset (0, 0, 32), expected (0, 0, 30) |
| (w8) `W8_TheDeepWater_...` | 3 / 3 | the tick after the lift in the water: the hero still holds the object (the stub of mode 2 lets nothing go) |
| (w9) `DestroyEntityCarrierTests.TheDestructionOfTheObjectInTheHands_ClearsTheLinkOfTheCarrier_...` (two overloads) | 2 / 2 | the carrier still holds the object |
| (w9) `TheClearing_HasNoTestOfIdentity_...` (two overloads) | 2 / 2 | the carrier still holds the other object |
| (w10) `CarriedSortKeyTests.AnObjectInTheHandsOfTheHero_IsSortedAtLeastAsItsCarrier_...` | 1 / 1 | expected elevation 166, actual 102 |
| (w11) `W11_AHeavyObject_KeepsTheHeroAtTheStand_...` | 3 / 3 | the neighbour of kind 1: expected animation 7, actual 12 |

Green on the stubs (6 cases): (w6) `CarryGuardTests.W6_TheLadderIsRefusedToAHeroWhoCarriesAnObject_...` and (w12) `W12_TheSquareWithAnObjectInTheHands_ChangesNothing_...` (the two guards, green from T1 as the plan says); (w9)
`ACarrierWithoutAProxy_IsLeftAlone_WithoutAnException` (2) and `AnObjectNobodyCarries_LeavesTheLinkOfAHeroWhoCarriesAnotherOne` (2), the "without effect" cases of (w9), each next to a red positive case above.

## T3 - phase B, the six branchings

Commit of the code of T3: see `git log` (message `feat(e20-g1a): T3 ...`). Inert in production: nothing but `PlayerManager.PutInHands` (no production caller before E20.g1b) puts an object in the hands, so no existing path sees a carried object.

### What was wired

| Branching | Where | What |
|---|---|---|
| (W1) the body of `UpdatePlayerCarriedEntity` | `PlayerManager.UpdatePlayerCarriedEntity` (called with 0 at `0x80031D78`, with 2 by `SwimTick`) | `CarryRules.UpdateCarried(player, mode, player.ScriptHost as IEntityWorldContext)`; a bare `Entity` in `CarriedEntity` (the ladder test) is left alone, no exception |
| (W2) the mode 1 in the locked branch | `PlayerManager.MovePlayer`, locked branch | `UpdatePlayerCarriedEntity(player, 1)` right after the two clears (charge timer, pending-spawn pointer) and before `EndOfMovePlayer` (the item cancel `0x800350C0` stays h0's stub) |
| (W3) the follow | `EntityScriptProxy.TickMotion` (+ `FollowCarrier`) | first branch of the non-hero motion: an active entity whose `PlatformEntity` has a proxy has no kinematic step and no support; forces 0 (`ForceX/Y/Z`, `TargetForceX/Y`, `FinalForceX/Y/Z`), `ForceAdjusted` 1, `CollidedWithEntityZ` 1, `XCollisionEntity` none, `IsOnGround` 0, floor and terrain heights copied from the carrier, position = carrier + the carrier's offset words, `ModdedPos*` recomputed, then `PushLogicalPositionToRoot` (D-G-3) |
| (W4) the states | `PlayerManager.MovePlayer` chain (+ `LiftGestureTick`, `CarryGroupTick`) | 0x05 and 0x0B: `TryUseItem` then `CarryRules.LiftGestureNext` (no direction write); 0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30: the direction is written, `TryUseItem`, then `CarryRules.CarryGroup` (heavy = the carried proxy's `Flags & PickupKindMask` all set; throw = the named stub of g2a; `NoThrow`/`NoJump` = `CombinedVramFlagsOR & 0x2000` / `& 0x4000`) |
| (W5) the carrier's clearing | `WorldProxy.DestroyEntity` (both overloads, `ClearCarrierOf`) | `PlatformEntity` has a proxy: its `CarriedEntity` is cleared, no test of identity; the object keeps its own `PlatformEntity` |
| (W6) the sort key | `FrameSyncPasses.RunWallInterleaveSortKeyPass` (+ `EntityDepthKey.RaiseToCarrier`) | a collidable entity (flag `0x80`, animation flag `0x80` clear) in the hands of another takes the carrier's key when its own depth value is below it (signed), as `0x8003981C-0x80039858`; any other entity keeps `PosY + (IDSV << 16)` |

Local decisions: `FollowCarrier` does not call `EnsureMovedThisPass` (D-G-3: "le héros passe avant dans le tick, pas de récursion de porteur"; the hero is first in the list of the world and of the montage); a `PlatformEntity` without a proxy (the bare entities of the oracle
montages of E14, `new Entity { Name = "platform" }`) is no carrier the binary could follow: the entity moves as any other (the Beannoid and Muruta oracle tests stay green, see below). The T0 clause "a carried object at (0, 0, 32) is not brought down to the ground by `ClampToGround`"
holds: `PushLogicalPositionToRoot` raises a position under its ground and never lowers one, and (w1) and (w4) pin the crate at +32 px over the hero on 40 ticks and over a jump of 27.5 px, its root equal to `ResolveLogicalPosition` of its logical position at every tick. No stop.

Comments that g1a made false, corrected: `EntityScriptProxy.HasLogicalZ` (also posed by `PutInHands`), `EntitySupport` class doc (`PlatformEntity` is assigned since g1a), `ScriptedMotion.ClampStepToMapEdge`/`RunOneKinematicTick` doc (a record entity put in the hands is marked), `EntityPickupKind` (kinds 2 and 3 both need the
power glove, item 0x3B; kind 3 is the heavy one that keeps the stand), the stub clause (6) of `WorldProxy.DestroyEntity`, `PlayerManager` (class doc, locked branch comment, `SwimTick` doc), `TerrainHeightTests` (the platform inheritance paragraph), `LogicalZStepTests` (the marker). The docs of the registry
(`NativeSpriteRegistry`, `EventProgramRunner.NativeSprites`) are about the production keys: they become false at T4 (phase C), not before. `EntitySupport.cs:36-37` and `:68-70` stay to g3, as the plan says.

### Measured

- The 53 new cases: **53 green** (47 were red on the T1 production, 6 green). `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing).
- Suite, Release with the neutralised copy, then Debug built last, `--blame-hang-timeout 60s`, the same three partitions as T1 (by test namespace, disjoint): 

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 (Arcs, Ai, Audio, Cameras, Cells, Combat, Dialogues) | 1 224 | 0 | 0 |
| 2 (Entities, Events, ExportGuards, Hero, Items) | 2 757 | 0 | 0 |
| 3 (the rest) | 1 802 | 0 | 0 |
| total | 5 783 (= 5 730 at the end of T1 + 53) | 0 | 0 |

- **Existing tests moved: none.** The predicted list of `README.md` for phase B, all expected green and all green: `BeannoideOracleTests`/`MurutaOracleTests` (`RunE1Call`, a carrier without a proxy in `DestroyEntity` and in the motion), `LadderClimbTests.CaseSixGate_CarryingEntity_DoesNotEnterClimbing` (a bare `Entity` in
  `CarriedEntity`), `EntityDestructionTests.TR2_...`, `LootRollWorldTests.Rolls.M3_...`, `EntitySearchServiceTests` (functions 4 and 11), `NpcCharacterControllerMoverTests.EntitySearchService_Searches5And6_...`, `EntityCollisionTests`, `MovementObstacleProbeTests`, `TerrainHeightTests`,
  `LogicalZStepTests`, `MapEdgeWorldTests`, the arcs. No test name removed or renamed (the only edits to existing test files are two doc comments).
- The four `docs/hero-trace-389-*.txt` files were rewritten by the suites (line endings); `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0 after the Release and after the Debug suites, restored by `git checkout`, never indexed. No diff of `RunHeroVerticalTick` or `SnapHeroAtApex`;
  the engine, the converter and the data are untouched. `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- **Proof of retreat** (real mutations applied by script to the T3 code, `Alundra.Tests` rebuilt, the 53 cases run, the bytes restored; `git diff` unchanged after): removing the follow (W3) turns red exactly (w1), (w2), (w3), (w4), (w5), (w7); the two state branches (W4): (w3), (w4), (w5) (the wall and the
  top tests; the platform test needs only the follow), (w11); the body of (W1): (w2), (w7), (w8); the mode 2 branch of `CarryRules.UpdateCarried` alone: (w8); the call of mode 1 in the locked branch (W2): (w7); the clearing in both `DestroyEntity` (W5): (w9); the raise of the sort key (W6): (w10). These are the
  sets of the plan's acceptance line, case for case (a first draft of (w8) that walked into the water, and drafts of (w3) to (w11) that lifted from 21 px, also reddened (w3) to (w5) or (w8) under (W1), (W3) or (W4): reshaped, see T2).

### Deviations from the plan text, none changes a decision

- (w5), the top of the object: the DLL's free hero lands at T+17 and rides from T+18 (UH-10); the plan's table counts 19 in the oracle's convention. The test pins the DLL's value and the equality with the free hero (T to T+20); beyond T+20 the carried hero, who walks without a ramp, is no longer at the
  same place as the free one.
- The scenes of (w3), (w4), (w5), (w7), (w8), (w11) start with the object on the head (D-G-2: the carrying stand at once), not with a lift from 21 px: that is what makes the plan's retreat sets true. The lift series is (w2)'s, and (w2) also pins the Chain of the gesture at L+36.
- The plan's T0 common (`docs/plan-e20-annexe/e20-g/`: oracle copy, the 16 one-word mutants, the probes P-G-1 to P-G-5) is still not on the branch (see T1). The probes P-G-2 to P-G-5 concern g2a; nothing of T2 or T3 reads them.
- `ContactHost` lost its `sealed` (test support) and `JumpHeroRig.Build` gained an optional `hostFactory`; both are opt-in and change no other montage.

### Left for the next tasks

T4 (phase C): `CreateForProduction` registers (C, 23) for the seven types; the pins of the registry (`NativeSpriteRegistryTests`, `WaveSwordLifeTests`) move there by derivation; (r1) and (r2). T5: the ADR, the mutants of the DLL code (the list of 18), the two fresh verifiers, and the plan's commun T0 owed.

## T4 - phase C, the registration

Start HEAD `b0a627f2` (T3). Files: `Alundra/Ai/NativeSpriteRegistry.cs` (one line, `LiftableAi.Register(registry);` in `CreateForProduction`, and its doc), `Alundra/Events/EventProgramRunner.cs` (doc of `NativeSprites`, which listed only five of the nine classes), `Alundra/Ai/Objects/LiftableAi.cs` (class doc: registered since phase C); tests `Alundra.Tests/Ai/Objects/LiftableAiRegistrationTests.cs` (r1) and `LiftableAiMap392Tests.cs` (r2).

### The tests, written before the line

- **(r1)** `LiftableAiRegistrationTests` (16 cases): `R1_TheProductionRegistryHoldsC23_ForEachOfTheSevenTypes`, `R1_TheRunnerOfProduction_HoldsTheKeyToo`, `R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (the sum of the `Register` of the nine classes, derived), and `R1_TheKeyIsNotRegisteredForAnyOtherSpriteType` (13 neighbour types, green before and after, each next to the three red positive cases).
- **(r2)** `LiftableAiMap392Tests` (2 cases), on the real map 392 (Ship Klark, night, inner; the arc spec of `ShipCorridorArcTests`, real prefabs, real controller): `R2_TheCratesAtRestOfTheMap392_KeepEveryFieldFor100Frames_WhileTheHandlerRunsForThem` (every primitive/enum property and field of every liftable object of the map equal after 100 frames but the six clocks every entity advances - `AnimCompleteCounter`, `BoxTicks`, `FrameCounter`, `MotionTickCount`, `PhysicsDoneInPass`, `SupportTickCount`, measured as the only six that differ - and `NativeHandlerRunCounts[(C, 23)]` at least crates x frames / 2), next to `R2_ACarriedCrateOfTheSameMap_IsReleasedByTheCodeOfTheThrow_WhichChangesItsFields` (`PutInHands`, code -1: animation 3, code 0, links cleared, flags changed).

### Red before the line, green after

Measured on the production without the line (full suite, Release, neutralised copy): **8 red, nothing else**: the five new ones (`R1_TheRunnerOfProduction_HoldsTheKeyToo`, `R1_TheProductionRegistryHoldsC23_ForEachOfTheSevenTypes`, `R1_TheKeyIsInTheSlotCOnly_...`, `R2_TheCratesAtRest..._` (C23 never ran), `R2_ACarriedCrate...`) and the three pins, which are already rewritten (they expect +1): so the same 8 are red with the line removed from the final tree, which is the plan's removal proof (r1 and r2 exactly, plus the pins). (w8) and every world test of T2 stay green without the line (they register C23 themselves only when the key is absent).

### Existing tests moved: three, all in the predicted list, rewritten by derivation (the old count plus `LiftableAi.Keys.Count`)

| Test | Rewrite |
|---|---|
| `Ai/NativeSpriteRegistryTests : ProductionRunner_RegistryHoldsTheHandlersOf...` | `+ LiftableAi.Keys.Count` |
| `Ai/Projectiles/WaveSwordLifeTests : V3_TheProductionRegistryHoldsTheKeysOfItsClasses...` | `+ LiftableAi.Keys.Count` |
| `Ai/Projectiles/ChargedArrowAiTests : C2_TheProductionRegistry_HoldsTheKeyForTheType7...` | `LiftableAi.Register(sum)` and `+ LiftableAi.Keys.Count` |

Deviation from the predicted colour: `ChargedArrowAiTests.C2_` was listed "own-class count, expected green"; it asserts the sum of the classes (a third pin of the registry), so it moved. It was named in the list, so the stop "a test outside the list moves" does not apply. `PillarAiRegistrationTests` and `ArrowAiTests.E5_` stayed green as predicted. No other existing test moved (arcs on maps with crates, `EntityDestructionTests.TR2_`, `LootRollWorldTests.Rolls.M3_`, `EntityContactArcTests.TH4_`, `VisionAndCoastArcTests.A3_` green).

### Measured

Suite in three disjoint partitions by test namespace (as T1 and T3), `--blame-hang-timeout 60s`, Release with the neutralised copy then Debug built last: 1 242 + 2 757 + 1 802 = **5 801 tests, 0 failure** in both (5 783 at the end of T3 + 18). `docs/hero-trace-389-*.txt` rewritten by the suites (line endings) and restored, `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0 after both; `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `alundra-project/Alundra.dll`: equal. `check_names.py --rules`: 0 finding. No diff of `RunHeroVerticalTick`, `SnapHeroAtApex`, the engine, the converter or the data.

### Left for T5

The ADR (next free number to recount over the branches), the mutants of the DLL code, the two fresh verifiers, and the commun T0 owed (`docs/plan-e20-annexe/e20-g/`).

## T5 - ADR, mutants of the DLL code, suites (the two fresh verifiers are launched by the main session)

Start HEAD `203b2632` (T4). Files: `docs/decisions/0089-the-object-in-the-hands-follows-the-hero-by-a-carried-branch-and-c23-is-registered-for-the-seven-liftable-types.md` and its row in `docs/decisions/README.md`;
`Alundra.Tests/Hero/CarryFollowAndGestureTests.cs` (9 cases, written because of the mutants below); the script and the raw results of the mutants in `exec/t5/`. No production file is touched by T5.

### ADR

ADR-0089. Recount over every branch (`git branch -a`, `git ls-tree --name-only <branch> docs/decisions/` for each, the worktrees' branches included): the last number taken anywhere is 0088 (E19.d2c2 D5b); 0089 is free.

### Mutants of the DLL code: 33 real mutations, 32 killed, 1 survivor outside the plan list (explained)

Method (`exec/t5/mut5.py`, results `exec/t5/mutants-final.txt`, the first pass `exec/t5/mutants-pass1.txt`): each mutation replaces one exact text of one production file (the count of the text must be 1), `Alundra.Tests` is rebuilt in Release with the neutralised copy, the 179 tests of the
carrying classes are run (`CarryGroupTests`, `CarryGuardTests`, `CarryLockSwimAndStatesTests`, `CarryRulesModesTests`, `CarryRulesStepTests`, `CarryTopsAndPlatformTests`, `CarryWalkAndJumpTests`, `CarryWorldTests`, `CarryFollowAndGestureTests`, `PutInHandsTests`, `LiftableAiTests`,
`LiftableAiRegistrationTests`, `LiftableAiMap392Tests`, `DestroyEntityCarrierTests`, `CarriedSortKeyTests`, `NativeSpriteRegistryTests`, `EntityDestructionTests`), the file is restored by the script from its bytes. `git diff` over `Alundra/` is empty after the run (checked). A mutant is killed when at least one test is red.

The 20 mutants of the plan's list (`docs/plan-e20-heros.md` 1.24.1, T5), all killed (red counts; the full names of the red tests are in the results file):

| # | Plan item | Mutant | Red | Killed by (first) |
|---|---|---|---|---|
| M01 | `<` for `<=` at the equality of mode 0 | `horizontal <= error` | 10 | `CarryRulesStepTests` (equality), `CarryRulesModesTests` C1, (w2) |
| M02 | step of 1 px changed | `FreeStep` 2 px | 13 | C1 traces, (w2) |
| M03 | threshold `0x1FFFF` changed | `DestroyLimit` `0x20000` | 7 | C2 threshold cases, `CanDestroyLocked` |
| M04 | the 5 of the counter | 4 | 4 | C2 under five calls, (w7) |
| M05 | counter not reset | mode 0 without the reset | 9 | C1 nothing carried / traces |
| M06 | mode 1 -> mode 0 forgotten for `AttackCount` 0 | rule dropped | 5 | C2 sheet without attack |
| M07b | destruction with two arguments | `(world as WorldProxy)?.DestroyEntity(item, -1)` in mode 1 | 9 | C2 threshold, C3, (w7) (see the survivor below) |
| M08 | offset read on the triplet instead of the positions | `Difference` reads the offset words | 20 | C1 |
| M09 | mode 2 without the -1 | `Flags2 = 0` | 6 | C3 |
| M10 | mode 2 clears `CarriedEntity` | added | 2 | C3 |
| M11 | follow without the copy of the heights | lines removed | 3 | `F1_...` (new, see below) |
| M12 | follow keeps the forces | the eight zeroings removed | 3 | `F1_...` (new) |
| M13 | `XCollisionEntity` not cleared | line removed | 3 | `F1_...` (new) |
| M14 | carried object left in `Collidables` | `PlatformEntity == null` gate removed from `EntitySupport.IsEligibleSubject` | 18 | (w1), (w4), (w5), (w11) |
| M15 | clearing of the carrier forgotten, two-argument overload | call removed | 2 | (w9) positive and no-identity cases |
| M16 | clearing forgotten, single-argument overload | call removed | 2 | (w9) |
| M17 | clearing with a test of identity | `ReferenceEquals(carrier.CarriedEntity, entity.LogicContextEntity)` | 2 | (w9) `TheClearing_HasNoTestOfIdentity_...` (both overloads) |
| M18 | states `0x06` and `0x2E` swapped | | 10 | C6, (w4) |
| M19 | marker not posed | `PutInHands` | 12 | C5, (w2), (w4), (w7), (w8) |
| M20 | `RidingEntity` not cleared | `PutInHands` | 1 | C5 |

Twelve more, beyond the list (the six branchings and the registration, one by one, and some inner parts): M21 sort key not raised (1 red, (w10)); M22 mode 1 not called in the locked branch (3, (w7)); M23 body of `UpdatePlayerCarriedEntity` removed (9); M24 lift gestures branch removed (3,
`F2_...` new); M25 carrying group branch removed (27); M26 heavy test "any kind bit" (27); M27 follow branch removed (39); M28 follow without the Z offset (36); M29 follow without the push to the root (30); M30 C23 not registered (6: (r1), (r2), the pin); M31 C23 keeps the carrier's link (9);
M32 C23 keeps the riding relation (4).

First pass, before the tests of T5 (32 mutants, M07b not yet written): 27 killed, five survivors: M07 (below) and four holes, M11, M12, M13 and M24 (the follow's reset of the forces, the contact entity and the heights; the wiring of the lift gestures). They were true holes of T2 (every object of the world tests was lifted at rest, standing on the hero's own heights, and the
gesture never ended before its chain). `CarryFollowAndGestureTests` closes them, each case green on the code and red on its mutant (`mutants-final.txt`): `F1_TheFollow_ZeroesTheForces_ClearsTheContactEntity_AndTakesTheHeightsOfTheCarrier_...` (1, 2, 3 ticks per frame: the crate is lifted with forces, a contact entity, `ForceAdjusted`/`CollidedWithEntityZ`/`IsOnGround`
reversed and floor/terrain heights different from the hero's; from the lift tick on and for 6 ticks the forces are 0, the contact entity is none, the flags are 1/1/0 and the heights are the hero's; the hero's heights are checked different from the preset ones, so the test can fail), `F2_TheLiftGestureOnTheGround_PassesByTheFallStateWhenTheObjectIsLost_AndStaysWhileItIsHeld_...`
(the gesture 0x05 keeps 0x05 while the object is held, and the object lost passes by 0x2D, the fall state; the binary's rule `0x800322F0`), `F3_TheLiftGestureInTheAir_StaysWhileTheHeroFalls_AndBecomesTheStandOnTheGround` (lift in the air at 20 px: 0x0B while falling, 0x0C on the ground; green with and without M24, because the stand on landing is also reached
by the animation clock: it pins the observable behaviour, it does not kill M24).

The survivor: **M07**, not in the plan's list and not a mutant of the code of the slice. It rewrites the existing single-argument `WorldProxy.DestroyEntity(entity)` (E19) to call the two-argument overload (`DestroyEntity(entity, -1)`, which emits the break effect of the sprite header). No test has a
`WorldProxy` with a sprite whose header holds a break effect and a stage that records the emission for the single-argument path: the "no effect, no loot" property of that overload is existing behaviour, tested by no test of the repository, and the g1a claim (mode 1 destroys with no effect) rests on the interface method being the single-argument one, which M07b kills
(the montage's double is not a `WorldProxy`, so a two-argument call destroys nothing and (w7), C2 and C3 go red). Flagged for the verifiers, not fixed here (a test of E19's overload is out of the perimeter).

### Suites

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets` on build and test), then Debug built last without it; `--blame-hang-timeout 60s`; disjoint partitions by test namespace as T1, T3 and T4 (1 = Arcs, Ai, Audio, Cameras, Cells, Combat, Dialogues; 2 = Entities, Events,
ExportGuards, Hero, Items; 3 = the negation of the twelve); the three of one configuration cover the whole project.

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 | 1 242 | 0 | 0 |
| 2 | 2 766 | 0 | 0 |
| 3 | 1 802 | 0 | 0 |
| total | 5 810 (= 5 801 at the end of T4 + the 9 of `CarryFollowAndGestureTests`; = `--list-tests`) | 0 | 0 |

- `--list-tests` against the list at the start HEAD `b62726ff` (5 662 names): none of the 5 662 is missing or renamed; 148 added = 68 (T1) + 53 (T2) + 18 (T4) + 9 (T5).
- **Existing tests moved, measured: three, against the predicted list of `README.md`.** Predicted: the two pins of the registry (phase C), the three tests that "read the keys of their own class" (expected green), and the witnesses whose comments the slice corrects. Measured: `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOf...`, `WaveSwordLifeTests.V3_...` and
  `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7...` rewritten by derivation (`+ LiftableAi.Keys.Count`; C2 is a third pin of the registry that the plan did not know, it was in the predicted list as "own-class, expected green"); the doc comments of `TerrainHeightTests` and `LogicalZStepTests` corrected; test support
  (`ContactHost` unsealed with an opt-in `DrivesTicks`, `JumpHeroRig` optional `hostFactory`, `JumpNpcRig` eight rows of `HeroAttackClock`) with no change for the existing montages. Everything else of the predicted list stayed green and untouched (`PillarAiRegistrationTests`, `ArrowAiTests.E5_`, the oracle tests of the Beannoid and the Muruta,
  `LadderClimbTests`, `EntityDestructionTests.TR2_`, `LootRollWorldTests.Rolls.M3_`, `EntitySearchServiceTests`, the arcs): 3 moved against 5 predicted names (plus the comment-only witnesses), none outside the list, far below the stop of 10.
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
- The four `docs/hero-trace-389-*.txt` were rewritten by the suites (line endings); `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0 after the Release and after the Debug suites, then restored by `git checkout`, never indexed.
- Since `b62726ff`: no diff of `RunHeroVerticalTick` or `SnapHeroAtApex`; `git diff b62726ff..HEAD` touches only `Alundra/`, `Alundra.Tests/` and `docs/`; the engine pointer is `92d8e847` at both (the author's local change to `CasaEngine.Launcher/Program.cs` in the submodule, and the converter, the analyser and
  `data-extracted/` are untouched); `alundra-project/` has no diff (the DLL is deposited by the Debug build only).

### Owed to the slice, not done by T5

- The commun T0 (`docs/plan-e20-annexe/e20-g/`: oracle copy and manifest, `c4_climb_lift`, the 16 one-word mutants of the binary, the DLL probes P-G-1 to P-G-5): not on the branch since T1; the acceptance line "Oracle: manifeste identique ; 16 mutants" stays open.
- The two fresh verifiers (one on the pins: predicted list against measured list, above).
- The plan (`docs/plan-e20-heros.md`) is updated by the main session.

## Resumption 2026-10-24: finding F1 of the verifier (the commun T0 missing)

Cause: T0 (section 1.24.0) was never written; T1 flagged it and T5 left the acceptance line "Oracle: manifeste identique ; 16 mutants" open. Fix (commit of `docs/plan-e20-annexe/e20-g/`, docs only, no code, no test, no change of the DLL):

- `e20-g/oracle/`: the oracle tools copied byte for byte (`cmp` against `e20-g1a/work/orc` and `critic-scenarios`: no difference, `e20-g/SHA1SUMS.txt`), plus `run_json.py`, `run_t0.py`, `run_mut.py`, `mut_t0.py` and two scenarios, `c4_climb_lift.py` and `c5_status4.py`.
- Two complete runs of the 23 scenarios in two fresh copies: `out/manifest.txt` (60 text outputs, 24 JSON files) identical (`cmp`); the old outputs of g1..g18 and c1..c3 are reproduced byte for byte.
- `c4_climb_lift`: prediction (lifted, `anim_t` 5 or 11) holds: 11 in the climb, 5 on the ground; no lift without the contact; the contact word is the scenario's only poke (the climb never produces a contact by itself).
- Mutants: 20 one-word mutants of the binary, 20 killed, none to argue (`e20-g/oracle/mut_result.txt`); M06 is killed by `c5_status4` (the whole-tick scenarios cannot reach a status-4 entity standing on the hero).
- Probes of the DLL, run on the end state `b0a39aa9` with temporary tests, removed: P-G-1 holds (animation 0 and 2 of the crate have no box, 1 has 11 ticks with a box, 3 and 4 have 2), P-G-4 holds (`crate.PosZ - hero.PosZ` = 0x200000 exactly before, at and after the release, 1, 2 and 3 ticks per frame; the stop of g1a is not triggered), P-G-5 holds
  (riding at the tick after the landing). P-G-2 and P-G-3 are g2a's and stay in its T0.
- Acceptance line "Oracle: manifeste identique ; 16 mutants tués ou prouvés équivalents (T0 commun)": now met. Details in `docs/plan-e20-annexe/e20-g/README.md`.
- A1 (the plan not updated) is the main session's. A2 (the one-argument `DestroyEntity` creates no loot or effect, mutant M07 of the verifier survives) is E19 behaviour, outside the slice; flagged.
