# E20.g2a (throwing) - execution record

Plan: `docs/plan-e20-heros.md` section 1.24.2 and the block "Revision au HEAD (2026-10-18)" (which prevails over the text of 1.24.2 where they differ). Branch of the parent
`chantier/theme1-porter-plateforme`. Start HEAD of the execution: `44ce16c8` (E20.g1a done and CONFIRMED, ADR-0089). The commun T0 is `docs/plan-e20-annexe/e20-g/` (oracle, 20 mutants, probes P-G-1, P-G-4, P-G-5);
this file is the T0 of g2a proper: the probes P-G-2 and P-G-3 and the predicted list.

## T0 - the probes of the DLL (temporary tests, run on the start HEAD, then removed; the predictions were written before the runs)

The probes were a temporary test class on `CarryRig` (the g1a bench: the real hero controller, the exported animation sets, a crate of the original, the host that plays each frame tick by tick over all the
entities), at 1, 2 and 3 ticks per frame. Nothing of the DLL, the engine, the converter or the data was changed (the one real mutation below was reverted: `git diff` empty on `Alundra/Physics/ScriptedMotion.cs`).
Raw outputs: `probes-with-line.txt`, `probes-without-line.txt`.

### P-G-2: the value of `BoxDirection` at the tick of the throw

Prediction: the direction of the PREVIOUS tick (the binary reads `+0x98` of the hero, which the animation clock of the previous tick wrote), never the direction of the tick being played (`TargetDirection`, the binary's `+0x8C`,
which the carrying group writes from the pad just before the Square branch) and, at 2 ticks per frame, not the end-of-frame `AnimationDirection`.

Scene: the hero carries the crate (state 0x0C), faces east (`BoxDirection` 3, `TargetDirection` 24); from the tick 10 the pad holds Left (a turn), in a second variant with the Square at the same tick 10. The values are read
right after `MovePlayer` of each tick (where the throw is written), before the clock:

| tick | BoxDirection | AnimationDirection | TargetDirection | state (turn alone / turn + Square) |
|---|---|---|---|---|
| 9 | 3 (east) | 3 | 24 | 12 / 12 |
| **10 (the turn, and the Square)** | **3** | 3 | **8** (west, written by the group at this tick) | 7 / 12 (the Square stub keeps the state) |
| 11 | 2 (west) | 3 at 2 ticks per frame, 2 at 1 and 3 | 8 | 7 / 7 |

Result: **holds**, identical at 1, 2 and 3 ticks per frame. At the tick of a turn and a Square the direction to read is 3 (east), the previous one, while `TargetDirection` already says west; and at 2 ticks per frame the tick 11
shows `BoxDirection` 2 against `AnimationDirection` 3 (the end-of-frame value lags): the right field is `BoxDirection` (D-F3-2, D-G-10), as the shot of the weapon reads it (`PlayerManager.SpawnWeaponProjectile`). `tests (t2)` of T1 pin it.

### P-G-3: the speed rule of the thrown crate (the plan's one-value prediction)

Prediction (plan 1.24.2): X of the thrown crate at the tick T is **109.0 without the extension of the speed key to the marker, 110.5 with it**; another value is a stop.

Finding at the start: **the line already exists**. `ScriptedMotion.KeyAnimationOf` (`Alundra/Physics/ScriptedMotion.cs`, the key of `TickScriptedNpc`) reads
`entity.IsPlayer || (entity.JoinsBirthTick && entity.CurrentAnimationId == ~0u) || entity.HasLogicalZ ? entity.TargetAnimationId : entity.CurrentAnimationId`: the `HasLogicalZ` clause is in the HEAD (written by E20.f5/e0k2; the g1a README
had noted it). So g2a writes no line in `ScriptedMotion.cs` (the plan: "Si la regle de f5 lit deja `HasLogicalZ`, la ligne existe et g2a ne l'ecrit pas (T0 le constate)"). The retirement proof of the rule is made by removing that clause.

Scene: the crate stands on the head of the hero, in his hands (`PutInHands`), the hero at (109, 100) immobile; at the tick T (after `MovePlayer`, where the throw is written) the probe writes by hand what `ThrowCarried` will write:
the direction word of the crate 24 (east), its X and Y the hero's, its Z the hero's plus 0x200000, `+0x24` = 1; C23 (the native program, run by the montage in the programs pass) then turns it into a projectile (animation 3, links cleared,
`0x332374`). The crate has the animation sets 0 (speed 0), 1 (256), 3 (128), 4 (384), impulse 1024 (the sheets of the export). Two bench facts had to be set for the crate to move at all (see "Bench facts" below).

| tpf | X at T with the clause (HEAD) | X at T without the clause (real mutation, reverted) | X at T+1, T+2... |
|---|---|---|---|
| 1 | **110.5** | **109.0** | +1.5 per tick, 143.5 at T+22 with the clause |
| 2 | **110.5** | **109.0** | the same; without the clause the first step is lost, 109.0 for T and T+1 at 2 ticks per frame |
| 3 | **110.5** | **109.0** | the same; without the clause 109.0 for T, T+1, T+2 (the clock validates `CurrentAnimationId` at the frame end) |

Result: **holds**: 110.5 with, 109.0 without, at 1, 2 and 3 ticks per frame, and the force of the tick is 98304 raw (1.5 px) at T with the clause against 0 without. No stop condition.

### Bench facts found by the probes (for T2, not for the code)

- A crate moved by the engine mover from the very position of the hero does not move in this bench: the physical box of the montage (the 18 x 12 x 32 ghost body of `ContactWorld.AddEntity`) overlaps the hero's body and the step is refused
  (`ForceAdjusted` 1, no displacement), the program choice then takes E for the crate (`Status` Deactivated one tick later). The probe took the mover out (`Controller = null`, the kinematic tick then integrates `Pos += FinalForce`).
  T2's world tests must do the same, or give the crate a root that does not meet the hero's body, as `CarryTopsAndPlatformTests.PlatformScene` does for its platform ("offset from its root so that the physical box never meets the hero's body").
- The crate of `CarryRig.AddCrate` has no animation sets and no map-edge bounds until the test gives them (`AnimSetsByAnim`, `EntitySpawnFactory.SetEntityDimensions`): without the bounds the clamp of the step (E20.e0k3) holds a marked entity at 0.
- The bench crate has no gravity flag, so Z stays at its launch value after the impulse: the Z series of the plan table (36; 39.5; ...) is a world test of the map flags, not of this bench; the horizontal series is.

## T1 design decisions (local, none changes the plan)

- `CarryRules.ThrowCarried(EntityScriptProxy hero, bool ground, bool directionHeld)` writes the throw of the binary (`0x8003237C-0x80032404`) on the object of `hero.CarriedEntity` and returns whether it threw:
  the object's direction word (`+0x8C`) from the table indexed by the hero's `BoxDirection` (the direction of the previous tick), its position (X, Y of the hero, Z of the hero + `HeadHeight`), its `+0x24` (`Flags2`) 1 or 2 on the ground and 2 or 3 in the air
  (no direction held / a direction held), the hero's state 0x09 on the ground and 0x0A in the air. The guard `CombinedVramFlagsOR & 0x2000` writes nothing. The links, the flags and the marker of the object are NOT touched (C23 does that at its tick).
- The guard is also in the pure table of the group (`CarryInput.NoThrow` makes `CarryStep.Throw` false, g1a); the function repeats it, as the plan lists it among the direct tests of `ThrowCarried`. The direction held is the same fact the group reads (`buttonsHold != 0`).
- `PlayerManager.SpawnDirections` and `PlayerManager.NoAttackVramFlag` become `internal` (a visibility change only, in the plan's perimeter) so that `CarryRules` reads the same table and the same bit as the weapon shot and the attack.

## Predicted list of the existing tests that move in the slice (recount at the start HEAD, by symbol; the baseline is 5 810 tests, `--list-tests`)

| Test (file : method) | Phase | Why / expected |
|---|---|---|
| `Hero/CarryGuardTests : W12_TheSquareWithAnObjectInTheHands_ChangesNothing_WhereTheHeroWithoutAnObjectSwingsHisWeapon` | B (T3) | the named stub of the throw is replaced: the Square with an object in the hands now throws. Rewritten by derivation (its "free hero swings" half stays) |
| `Hero/CarryGroupTests : C6_TheSquareThrows_...`, `C6_TheSquareComesBeforeTheKind_...` | none expected | pure table of the decision (`CarryStep.Throw` stays as g1a made it, the branch it triggers changes): expected green unchanged. The plan expected the Square rows rewritten; if the measure shows them unchanged, that is the deviation to report |
| `Hero/Carry*` world tests (W1 to W11), `Hero/LadderClimbTests` | B | none presses the Square with an object (rg `Square` on `Alundra.Tests/Hero/Carry*.cs`: only W12): expected green |
| `Ai/NativeSpriteRegistryTests`, `Ai/Projectiles/WaveSwordLifeTests` (the two registry pins) | none | g2a registers nothing (E2 is g2b) |
| `Maps/MapStateTests : P3_AMarkedEntity_KeysItsKinematicsOnTheAnimationItAsks_...`, `Entities/RecordBirthTickTests : B6_R_TheKeyOfTheKinematics_...` (the pins of the speed key) | none | the clause of the key exists at the HEAD, g2a does not write it |
| `Arcs/*` | none | nothing is carried in an arc, so no throw |

Anything outside this table that moves is a stop.

## Start counts

`dotnet test --list-tests` at the start HEAD (Release): 5 810 tests. Partitions as g1a (`results.md` of `e20-g1a/exec`): by test namespace, disjoint.

## T1 (phase A) - measured

Start HEAD of T1: `f9652340` (the T0 docs commit). Files: `Alundra/Hero/CarryRules.cs` (`ThrowCarried`, `ThrowGroundState`, `ThrowAirState`), `Alundra/Hero/PlayerManager.cs` (`SpawnDirections` and `NoAttackVramFlag` from `private` to `internal`, nothing else),
`Alundra.Tests/Hero/CarryRulesThrowTests.cs` (19 cases: (t1) and (t2)). `MovePlayer`, `CarryGroupTick` (the Square branch is still the named stub of g1a), `TickMotion`, `WorldProxy`, the registry: untouched, so phase A changes no behaviour.

### Red before, green after

The skeleton (`ThrowCarried` throwing `NotImplementedException` at its first line, same signature): the 19 cases give **18 red, 1 green** (the green one only pins the two state constants, which the skeleton already carries). On the code: **19 green**.

| Test | Cases | What it pins |
|---|---|---|
| `T1_TheObjectIsPlacedAtTheHeroXAndY_AndAtHisZPlus0x200000_RawToTheUnit_...` | 1 | X, Y, Z + 0x200000 raw to the unit, the object elsewhere before (21 px east, 5 px south, 10 px up) |
| `T1_TheObjectTakesTheDirectionWordOfTheTable_...TheFourIndicesTwoByTwo` | 4 | table 0, 0x10, 8, 0x18, each next to the other index of its pair |
| `T1_TheCodeOfTheThrowAndTheStateOfTheHero_...TheFourCombinations` | 4 | `+0x24` 1 / 2 on the ground, 2 / 3 in the air, states 9 and 0x0A |
| `T1_TheStatesAreTheBinarys_9OnTheGroundAndTenInTheAir` | 1 | constants |
| `T1_TheThrowTouchesNeitherTheLinks...` | 1 | links, flags, marker, offset, counter untouched, next to the code written |
| `T1_TheGroundPropertyOf0x20ForbidsTheThrow_...` | 5 | 0x2000 and 0x6000 write nothing; 0, 0x4000, 0x1FFF throw |
| `T1_NothingInTheHands_...`, `T1_AnObjectOfTheHandsWithoutAProxy_...` | 2 | nothing to throw, each next to a throw |
| `T2_TheDirectionOfTheObjectIsTheOneOfTheTickBefore_...` | 1 | `BoxDirection` against `AnimationDirection` and the pad's `TargetDirection`, both ways |

### Real mutations of the code of `ThrowCarried` (script `g2a-mut.py` in the session scratchpad: apply, rebuild, run the 19 tests, restore the bytes; the file is byte-identical afterwards)

18 mutants, 18 killed: skeleton (18 red); table index permuted (5); end-of-frame `AnimationDirection` read (5); pad direction word as index (5); Z without the head height (1); Z with half of it (1); X not written (1); Y not written (1); guard inverted (18); guard 0x4000
applied to the throw (1); guard dropped (2); ground codes swapped (5); air codes swapped (2); ground state 10 (5); air state 9 (2); link cleared (1); flags cleared (1); the hero's direction word written (1); an object without a proxy counted as thrown (1).
(A first run of the script replaced the first occurrence of the X, Y and Z lines, which is in mode 2 of `UpdateCarried`, not in `ThrowCarried`: four survivors that were not mutants of the function; the script now cuts at the function, and the scene of the tests puts the object away from the
throw's destination on all three axes. All killed on the second run.)

### Suites

Release with the neutralised copy, then Debug last; `--blame-hang-timeout 60s`; the three disjoint partitions by test namespace of g1a (1 = Arcs, Ai, Audio, Cameras, Cells, Combat, Dialogues; 2 = Entities, Events, ExportGuards, Hero, Items; 3 = the negation of the twelve).

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 | 1 242 | 0 | 0 |
| 2 | 2 785 | 0 | 0 |
| 3 | 1 802 | 0 | 0 |
| total | 5 829 (= 5 810 at the start + 19, equal to `--list-tests`) | 0 | 0 |

- Existing tests moved: **none**. `--list-tests` before (5 810 names) and after (5 829): none of the 5 810 is missing, the 19 added are the new ones. Predicted list for T1: empty; measured: empty. (The W12 and the Square rows of C6 of the predicted list are for phase B.)
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: `TextBoxGpu.cs`).
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- The four `docs/hero-trace-389-*.txt` files were rewritten by the suites (line endings); `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, then restored by `git checkout`, never indexed.
- The engine, the converter and the data are untouched.

## T2 (world tests v1 to v9) - written before the branch, red on the named stub

Start HEAD of T2: `9545acd4` (T1). Files: `Alundra.Tests/Hero/CarryThrowWorldTests.cs` (84 cases), and the bench of the tests (`Alundra.Tests/` only, all additions opt-in with a default that keeps every earlier test as it was):
`Support/JumpNpcRig.cs` (`HeroAttackClock.Table`: the two states of the throw, `0x09` ground and `0x0A` air, 19 ticks in the four directions from the exported `bankalundra_0_anim9_*` and `anim10_*`, sound 26, acceleration 0 and 15, chains 9 to 0 and 10 to 45 (`0x2D`)),
`Support/HeroWorldFixture.cs` + `Support/JumpHeroRig.cs` + `Hero/CarryRig.cs` (`kineticBody` / `kineticHero`: the hero's body is the Kinetic ghost of the real prefab, `Alundra.entity` `"physics_type": "Kinetic"`), `Support/ContactWorld.cs` + `Hero/CarryRig.cs` (`bodySize` / `bodyCenter` / `prefabBody`: the 21 x 15 x 16 box at (0.5, 0.5, 8) of the real crate prefab `Caisse en bois generique.entity`).

### Bench facts found (the second one corrects the T0 note)

- The T0 note said a crate thrown from the hero's position cannot move in the bench because "its ghost body meets the hero's". Measured: the 18 x 12 x 32 body of `ContactWorld.AddEntity` is a Kinetic ghost the sweep does not meet (a ghost `AddObject` at the same place does not block it); what blocks is the
  HERO'S body, which `HeroWorldFixture.BuildHeroPawn` builds as a SOLID box (default physics type) while the real prefab's is Kinetic. A crate at the hero's X and Y is refused at any height up to 100 px (the probe at z 100 moves, at z 40 does not), the program choice then takes E (`Deactivated`)
  and the object never moves again. So the montage of the throw asks for the real prefab's body (no `Controller = null`, which would also remove the gravity of the object and the cell field: `RunLogicalZPreXyStep` needs a controller). Nothing else was changed in the DLL, the engine or the data.
- The crate of the oracle (sprite 36) has a body 21 x 15 x 16 at (0.5, 0.5, 8) and a logical box (-10, -7, 0) 21 x 15 x 16 (`sprite-records.json`, the entity file): with it the cell wall stops the object at X = 133.0, as the oracle (the bench's default 18-wide ghost would stop it at 135).
- The gravity of the object needs `MapGravityRaw` 128 and `MapZViscosityRaw` 4096 (set on the crate by the scene) and the animation sets 0 (speed 0), 1 (256), 3 (128), 4 (384) with the impulse 1024 (the sheets of the export, as the T0 probes); then the Z series is the plan's, to the half pixel.

### What the 13 tests (84 cases) pin (all at 1, 2 and 3 ticks per frame; T = tick of the front of the Square)

| Test | Cases | Pins (values of the plan table / the oracle `g4`, `g4b`, `g6`) |
|---|---|---|
| `V1_...WithoutADirection_FliesAt1Point5PixelsATick...` | 3 | stand 12 and the object linked at T-1; at T hero 9 and carries nothing, object unlinked, animation 3, `+0x24` 0, flags `0x332374`; X 110.5 + 1.5 k to 143.5 at T+22; Z 36, 39.5 ... 0 (T to T+22); hero 9 for T..T+18, 0 at T+19 |
| `V2_...WithADirectionHeld_FliesAt3PixelsATick...` | 3 | animation 1 (not 3), X 112 + 3 k to 178 at T+22, next to the throw without a direction (1.5) |
| `V3_...InTheAir_...` | 6 (with / without a direction) | hero at 27.5 at T-1, object from 59.5, animation 1 / 4, 3.0 / 4.5 per tick, the oracle's 13 height samples (67.0 at T+1 ... 1.0 at T+25), landing at T+26 (not T+25), hero 10 from T to T+10 and 0 at T+11 |
| `V4_...TowardsTheGaze...` | 24 (4 gazes x held / not) | direction word 0x18 / 0x10 / 8 / 0 of the PREVIOUS tick, speeds east and west 1.5 and 3.0, north and south 1.0 and 2.0, Z arc |
| `V4_TurningAndThrowingOnTheSameTick...` | 3 | Up, Left or Down pressed with the Square: the object goes east (old gaze), at 3.0 |
| `V5_...0x2000_ForbidsTheThrow...` | 3 | ground property `0x20`: nothing, the state 12 and the links stay, next to the free ground that throws |
| `V5_...0x4000_ForbidsTheJump...` | 3 | ground property `0x40`: the Square throws (X 110.5, Z 36, X 143.5 at T+22), the Cross does not jump, next to the free ground where it does |
| `V6_...OnTheGround...`, `V6_...InTheAir...` | 3 + 3 | the Square (twice) during the gesture `0x05` (36 ticks) or `0x0B`: nothing is thrown, the state and the links stay; in `0x0C` it throws |
| `V7_...FromTheTickOfTheThrow...` | 6 | at T the validated animation is still 0, the asked one is 3 / 1, and X already moved by 1.5 / 3.0 (the rule of the speed key on the marker) |
| `V8_...AgainstAWall...` | 6 | the cell wall (column 6): X < 133 before the contact (T+7 held, T+15 not), X = 133.0 after, the Z arc unchanged, nothing destroyed |
| `V8_...OntoAStep...` | 9 | steps of 16, 32, 48 px: the object ends on the top (Z 16, 32, 48, on the ground), never leaves it, past x = 144, nothing destroyed |
| `V9_...GoesThroughAnotherCrate...` | 12 | a second crate at x = 160, 172, 178, 184 (collidable, probe on): the flight is the free flight (178.0 at T+22), no support, the target keeps its place and status |

### Red before the branch

The branch of the Square was left as g1a made it (the named stub). Run (Release, the neutralised copy) of `CarryThrowWorldTests`: **84 failures, 0 pass** - every case is red for the stub (the hero stays in the state 12 with the object linked: `Expected: Tuple (9, False, False)` for the throws, `T+0: x 109, z 32` for the flights, `Expected: Tuple (8, 1)` for the gazes).

### Findings of the slice (not stops: no value of the plan is contradicted; both are outside the plan's table)

- **F1 - the object takes the whole pixel of the hero's position.** `CarryRules.ThrowCarried` writes the hero's raw X and Y into the object, but the object's ROOT (the engine's position, which the mover steps from and the logical position is pulled back from) is the whole-pixel `ResolveLogicalPosition` the follow of the hands wrote. A hero stopped on a fraction of a pixel
  (after a walk: 68.78125) throws an object whose first step starts from 68.0 (observed T+0 x 66.5 instead of 67.28125, on the three tick rates). The oracle keeps the fraction (the binary copies `+0x114` whole). Invisible in the plan's values (the hero at 109.0); the scene of (v4) therefore sets the hero on a whole pixel before the throw. A fix would write the root by a displacement (no teleport) in `ThrowCarried`, a change of g1a's follow rather than of this slice: flagged, not made.
- **F2 - without E2 (g2b) the landed object keeps gliding.** The arrival is E2's (animation 2, speed 0 at T+23); until g2b the force of the flight is never cleared, so on the ground the object slides on at 1.5 / 3.0 px a tick (status `Deactivated` from T+23, nothing destroyed) until a wall or the edge of the map. "Reste" of the plan is read as "is not destroyed, keeps its height": (v8) asserts that, and the wall X = 133.
