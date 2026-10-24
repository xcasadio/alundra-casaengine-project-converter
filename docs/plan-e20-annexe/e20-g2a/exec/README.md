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
