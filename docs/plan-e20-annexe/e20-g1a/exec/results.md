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
