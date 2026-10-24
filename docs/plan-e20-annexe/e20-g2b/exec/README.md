# E20.g2b (breaking) - execution record

Plan: `docs/plan-e20-heros.md` section 1.24.3 and the block "Revision au HEAD (2026-10-18)" (which prevails where they differ), 1.24.0 (the common T0, done). Branch of the parent
`chantier/theme1-porter-plateforme`. Start HEAD of the execution: `5d63c6d8` (E20.g2a done and CONFIRMED, ADR-0089 amended). Baseline: `dotnet test --list-tests` (Release) at the start HEAD gives
**5 913 tests**.

## T0 - re-reading of the sites and the prerequisites at the start HEAD

Prerequisites (all stated by the plan as firm), checked in the code and not only in the plan:

| Prerequisite | State at `5d63c6d8` | Evidence |
|---|---|---|
| g2a (the throw) | done | `CarryRules.ThrowCarried`, `CarryThrowWorldTests` (84 cases) |
| E14.a1b (`DestroyEntity(e, -1)` with the break effect of the header) | done | `WorldProxy.DestroyEntity(entity, effectId)` (`Alundra/Maps/WorldProxy.cs`), `EntityScriptProxy.BreakEffect` |
| E20.f4 (D-F4-7, the arena switch) | done | `EventProgramRunner.NativeSpriteTypeFilterForTests` (`Alundra/Events/EventProgramRunner.cs:64`, read at `:308`) |
| E20.f6a (death in deep water of a marked entity) | done | `Alundra.Tests/Entities/MarkedEntityGroundSlopeTests.cs`; `PickEventTrigger` reads `Slope_18c == 4` with `DestroyOnSlidingSlope` |
| E20.e0c (the loot) | **done** | `WorldProxy.DestroyEntity(entity, effectId)` calls `LootDrop.SpawnContents` first; `Alundra.Tests/Rendering/Effects/DestroyEntityBreakEffectTests.cs:205` pins it. So the plan's "e0c faite" reading of (b5) applies: the object drawn at the creation (`ContentsItemId`) is born at the break, derived from `ContentsItemId` and never pasted; the "e0c not done" half (nothing born, next to `Contents` 130) does not apply and is not written |
| E20.e0k2, E20.e0b2 | done | the marker `HasLogicalZ` and the creation roll exist |
| T0 common (`docs/plan-e20-annexe/e20-g/`) | done | oracle, 20 mutants, probes |

Sites re-read by symbol (the plan's line numbers date from before the restructuration of the DLL):

- E2 = `0x8007ED30` (E0/E1 = `0x8007ED10` is a bare `DestroyEntity(e, -1)`, already in line in the runner: `EventProgramRunner.IsInlineDeactivateCode`). The model of the binary (`docs/plan-e20-annexe/e20-g1a/work/g1a_models.py`, function `e2`, 6 000 vectors without gap in `check.md`):
  `if ((+0x88 == 2 && +0xAC == 1) || (+0x184 & 4)) DestroyEntity(e, -1); else { +0x88 = 2; +0x6C = (+0x6C | 0x40) & ~0x30 }`. In the DLL: `+0x88` is `TargetAnimationId`, `+0xAC` is `ForceResetAnimationFlag`, `+0x184` is `CombinedVramFlagsAND`,
  `+0x6C` is `Flags`; the binary reads the TARGET animation, not the validated one.
- The pick phase (`EntityScriptProxy.PickEventTrigger`): a `Normal` entity goes `Deactivated` (slot E) on `DeactivateOnImpact` (`0x10`) with `ForceAdjusted`/`IsOnGround`, on `DeactivateOnHit` (`0x20`) with `HitCounter`, on `DeactivateOnAnimationEnd` (`0x40`) with
  `ForceResetAnimationFlag`; a `Deactivated` entity runs the slot E at every tick. Deep water: `DestroyOnSlidingSlope` (`0x100000`) with `Slope_18c == 4` destroys with the effect 6 in the `Normal` branch (the jar `0x2323E4` has not `0x100000`).
- `LiftableAi` (`Alundra/Ai/Objects/LiftableAi.cs`) holds C23 for the 7 types `SpriteTypes`; `Register(registry)` is called by `NativeSpriteRegistry.CreateForProduction`.
- The 10 types of E2 (census `docs/plan-e20-annexe/e20-g1a/report.md`, "Types a enregistrer"; the map page is `0x100 +` the index): 234, 290, 291, 292, 293, 294, 297, 304, 373, 438, i.e. the 7 types of C23 plus 290 (index 34, stone), 293 (index 37, barrel) and
  373 (index 117, trunk), the three of the C4 family that carry E2. The tests derive the list from the export (every header with E2) and compare it.
- `NativeSpriteRegistry.Register(slot, code, types, handler)` returns the `IDisposable` that removes the key and throws on a key already taken (the test registers only when the key is absent, after phase B never).

Facts the plan could not know and that the design uses:

- `LiftableAi.Keys` (the C23 pin) is read by three pins of the registry by derivation. The E2 keys go in a SEPARATE list (`BreakKeys`) so that phase A, which registers nothing in production, moves no pin; phase B adds `BreakKeys.Count` to each pin.
- The bench of the throw (`CarryRig`) has no effect stage, no slot table and no sound player: the chronology (statuses, the effect attached, the sound 27, the slot freed) needs the real `WorldProxy`; the world tests of this slice use a real exported map
  (the arcs' production path, `ArcRun` with the real prefabs) in which crates are born by the interface (`SpawnEntityBySpriteIndex`, map page) on a synthetic field (`SyntheticField`), as `ArrowRun` does.

## Predicted list of the existing tests that move in the slice

Recount at the start HEAD, by symbol (the grep covers the registry pins, the E slot, `LiftableAi`, the destruction and the 10 types). The plan names two pins and, "if e0c is done", `DestroyEntityBreakEffectTests.cs:205`.
The recount finds two MORE pins than the plan names (a deviation of the plan's list, reported here before any code):

| Test (file : method) | Phase | Why / expected |
|---|---|---|
| `Ai/NativeSpriteRegistryTests : ProductionRunner_RegistryHolds...` (`:55-58`, the plan's `AlundraNativeSpriteRegistryTests.cs:55`) | B (T3) | the count of the production registry gains the keys of E2: red until rewritten by derivation (the count before plus `LiftableAi.BreakKeys.Count`) |
| `Ai/Projectiles/WaveSwordLifeTests : V3_TheProductionRegistryHoldsTheKeysOfItsClasses_...` (`:181-185`, the plan's `AlundraWaveSwordLifeTests.cs:181-183`) | B (T3) | same pin, same rewrite |
| `Ai/Projectiles/ChargedArrowAiTests : C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (`:222-236`) | B (T3) | **not in the plan's list**: it sums the classes (`LiftableAi.Register(sum)`) and the keys; red until `LiftableAi.RegisterBreak(sum)` is added and the sum is rewritten by derivation |
| `Ai/Objects/LiftableAiRegistrationTests : R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` | B (T3) | **not in the plan's list**: same sum, same rewrite (its `E23` assertion is about the code 23 and stays true) |
| `Rendering/Effects/DestroyEntityBreakEffectTests` (loot test, `:205`) | none expected | it calls `WorldProxy.DestroyEntity(e, -1)` directly; E2 is not on its path. If it moves, it is a stop |
| `Ai/Objects/LiftableAiTests : C4_RegisteredInARegistryOfATest...`, `C4_TheCodeIsC23...`, `LiftableAiMap392Tests` (R2) | none expected | `Register` and `Keys` stay C23 only; R2's carried crate lives one frame (the flight does not reach the ground) |
| `Hero/Carry*` world tests, `CarryThrowWorldTests` (84 cases) | none expected | `CarryRig` has its own runner (C23 only): E2 is never registered there |
| `Entities/EntityDestructionTests : TR5_*` | none expected | they use the sprite types 0, 1, 2, 3, 255, -1 of an entity of type 0: outside the 10 types |
| `Arcs/*` on maps with crates (the sword hits a crate: `DeactivateOnHit` with `HitCounter`, then E2 breaks it) | none expected, **measured in T3** | no arc is known to hit a crate; a crate at rest never raises E; any arc that moves is a stop and is reported |

Phase A (T1) registers nothing in production: its predicted list is **empty**.

## Start counts

`dotnet test --list-tests` at the start HEAD (Release): 5 913 tests. Partitions as g1a/g2a (`part.sh`): by test namespace, disjoint, partition 3 the exact negation of the other two.
