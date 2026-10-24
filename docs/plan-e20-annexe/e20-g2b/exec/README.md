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

## T1 (phase A) - measured

Start HEAD of T1: `bca3ade1` (the T0 docs commit). Files: `Alundra/Ai/Objects/LiftableAi.cs` (`BreakCode`, `BreakSpriteTypes`, `BreakKeys`, `RegisterBreak`, `Break`; `Register` and `Keys` of C23 untouched), `Alundra.Tests/Ai/Objects/BreakRun.cs` (the bench and `BreakHook`),
`Alundra.Tests/Ai/Objects/LiftableBreakTests.cs` (7 tests), `Alundra.Tests/Ai/Objects/LiftableBreakWorldTests.cs` (30 cases). `NativeSpriteRegistry.CreateForProduction`, the runner, `WorldProxy`, `PlayerManager`, `CarryRules`: untouched, so phase A changes no behaviour
(`CreateForProduction` does not call `RegisterBreak`: that line is phase B).

### The bench

A real exported map through the arcs' production path (`ArcRun`: real prefabs, effect stage, sound player, slot table), emptied of every entity but the hero, on a synthetic field (`SyntheticField`), native handlers off and the arena switch of D-F4-7
(`NativeSpriteTypeFilterForTests`) on for the ten types of E2 only (a Beannoid stays inert). The crate (map-page index 36, map 67 "Coal Mine"; the jar, index 41, map 70 "Coastal Cave") is born by `SpawnEntityBySpriteIndex` on the head of the hero and put in his hands by `PutInHands`; the
real hero throws it with the Square (T = the tick 1 of the run), at 1, 2 and 3 ticks per frame. The hook of E2: `BreakHook.Install` registers (E, 2) in the runner's registry only when no type of the ten has the key (the same tests hold after phase B).

### Chronology measured (the plan's table, flat ground, no direction): identical at 1, 2 and 3 ticks per frame

| K | crate (type 292) | jar (type 297) |
|---|---|---|
| T+21 | Normal, 4.5 px up, E2 not called | same |
| T+22 | Normal, z 0, on the ground, flags 0x332374, E2 not called | same (0x232374) |
| T+23 | Deactivated, animation asked 2, flags 0x332344, E2 1st call, sound 27 at this tick | 0x232344, sound 31 |
| T+24 | validated animation 2, end flag (+0xAC) 1, E2 2nd call | same |
| T+25 | status 4 (still in the world), E2 3rd call, the break effect attached at the end of the frame that holds this tick: 10 | effect 2 |
| T+26 | out of the world, slot -1 | same |

The sounds of the run: the throw (26) at T, the break animation's at T+23, and (a content other than 0) the drop sound 84 at T+25. The table matches the plan (arrival T+22, E2 T+23, status 4 T+25, free T+26, effects 10 and 2): no stop.

### The other rows of the table

- Beannoid at x = 165, thrown with a direction held (3 px a tick): hit counter 1 and one spark at T+21, E2 first call at T+22, status 4 at T+24, free at T+25 (1, 2, 3 ticks per frame). Beannoid at x = 150: flown over (36 to 50 px), no hit and no spark in the whole run, arrival T+22, E2 at T+23, status 4 at T+25.
  The plan's two rows are those of the throw WITH a direction (the crate is at 178 px at T+22 in the free flight); without a direction the crate lands at 143.5 and touches a Beannoid at 150 at T+21 (measured while exploring, not pinned).
- Wall (direction held, column 6): X 133 from T+8, E2 first call at T+9, status 4 at T+11, free at T+12 (1 and 2 ticks per frame).
- Step of 16 px: on the top (z 16) at T+20, E2 at T+21, status 4 at T+23, free at T+24 (1 and 2 ticks per frame). Step of 48 px: measured, pinned relative to the tick it first stands on the top (E2 the next tick, destroyed 2 ticks later).
- Deep water under the arrival (a cell of ground property 8 from the column 5): lands at T+22 (Normal), destroyed at T+23 by the pick phase itself (DestroyOnSlidingSlope, effect 6), the program E never runs, no effect 10.
- Loot: the object of ContentsItemId is born at the break (type item + 0x1E, at the crate's whole pixel, the sound 84 at the tick of the destruction); ContentsItemId 0 drops nothing and plays no 84. As e0c is done, the "e0c not done" half of (b5) (nothing born, next to Contents 130) does not apply and is not written.
- A crate at rest (a second one, far from the first) stays Normal with the flags and the animation it had for 100 ticks while the thrown crate makes the 3 calls of E2 counted by the runner.

### Deviation measured (not a stop): 3 ticks per frame shifts the wall and step rows by one tick when the first call is not on the last tick of its frame

The DLL validates the animations at the END OF THE FRAME (the engine's frame clock) and +0xAC (the end of the animation 2) comes from the engine's end-of-animation event, as the plan notes ("pas de l'horloge du tick"). The oracle runs one tick per frame. For the flat landing (first call at T+23, the last tick of its frame at 1, 2 and 3 ticks)
the destruction is at T+25 as the oracle at every rate. When the first call falls on the first tick of a frame of 3 (the wall: T+9, the step of 16 px: T+21) the animation is validated one frame end later and E2 makes a 4th call: the destruction is at T+12 (wall) and T+24 (step) instead of T+11 and T+23, the slot free a tick later. The rule, measured and applied
to every rate by the tests: destroy K = max(first call K + 2, last tick of the frame of the first call); equal to the plan's K at 1 and 2 ticks per frame (asserted), one tick later at 3 for the two rows whose first call is the first tick of its frame. The first call of E2 itself is at the plan's tick at every rate. This is the existing frame clock, not E2's code (E2 only reads the flag):
the plan's stop line names the flat chronology (T+22, T+23, T+25, T+26), which holds.

### Red before, green after

Skeleton (Break throwing NotImplementedException at its first line, same signature): 35 red, 2 green of the 37. The 2 green (they do not call Break): `B1_TheCodeIsE2_TheSlotIsTheDeactivation_AndTheTenTypesAreTheOnesOfTheExport` and `B1_RegisteredInARegistryOfATest_TheKeyIsE2_ForTheTenTypesAlone_AndOnlyInTheSlotE_AndTheDisposableTakesItOut`
(the constants and the key). On the code: 37 green.

| Test | Cases | What it pins |
|---|---|---|
| LiftableBreakTests.B1_TheAnimation2AtItsEnd_Destroys_... | 1 (7 vectors) | the first line next to six vectors one step short (+0xAC 0, 2, -1; another animation asked) |
| B1_TheBit4OfTheGroundPropertyOfTheFourCorners_Destroys_... | 1 | the second line, whatever the animation, next to eight other values of +0x184 |
| B1_Otherwise_TheAnimation2IsAsked_AndTheFlagsBecomeOr0x40AndNot0x30_... | 1 (10 vectors) | the third line (a vector with the bit 0: ~0x31 is not equivalent), next to the call that destroys |
| B1_AnEntityWithoutAHost_... | 1 | no host, no exception |
| B1_TheCodeIsE2_..., B1_RegisteredInARegistryOfATest_..., B1_TheHandlerOfTheRegistryIsTheBreak_... | 3 | the ten types derived from the export, the key (E, 2) alone, the runner path |
| LiftableBreakWorldTests.B2_* | 3 + 3 | the chronology of a crate (effect 10, sound 27) and a jar (effect 2) |
| B3_* | 3 + 3 | the touch at x = 165, the flight over x = 150 |
| B4_* | 3 + 3 + 3 | the wall, the steps of 16 and 48 px |
| B5_* | 3 | the loot derived from ContentsItemId, next to a content of 0 |
| B6_* | 3 | deep water (effect 6, no E2) next to the dry landing (effect 10) |
| B7_* | 3 | the object at rest never runs E2, next to the thrown one that does |

### Real mutations of the code (script g2b-exec/mut.py in the session scratchpad: apply, build Release, run the 37 tests, restore the bytes; the file is the phase A edit alone afterwards)

18 mutants, 18 killed, plus the skeleton (35 red): ~0x30 to ~0x31 (1 red: the vector with the bit 0, so it is not equivalent); |0x40 forgotten (1); ~0x30 forgotten (11); +0xAC ignored (31); +0xAC != 0 (1); the validated animation read instead of the asked one (3); +0x184 & 4 forgotten (1), read as & 8 (1), as
any bit (1); destruction with the effect 6 (24); the animation 3 asked (35); the destruction falling through to the writes (1); || to && (34); the type 373 left out (1); a type outside the ten added (2); the key registered in the code 3 (32), in the slot C (32), for the types of C23 only (1).

### Suites

Release with the neutralised copy (-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets on build and test), then Debug last without it; --blame-hang-timeout 60s; the three partitions of g2a by test-name substring (part.sh; 3 = the exact negation of 1 and 2).

| partition | tests | failures Release | failures Debug |
|---|---|---|---|
| 1 | 1 282 | 0 | 0 |
| 2 | 2 878 | 0 | 0 |
| 3 | 1 798 | 0 | 0 |
| sum | 5 958 | 0 | 0 |

The sum is 8 above the 5 950 tests of the suite for the reason the g2a record gives (eight tests carry both Tests.Ai./Tests.Arcs and Tests.Hero in their name and run in both partitions 1 and 2). `--list-tests`: 5 913 names before, 5 950 after, none missing, 37 added (the new ones). Zero failure everywhere.

- Existing tests moved: none. Predicted list for phase A: empty; measured: empty.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 warning, existing: TextBoxGpu.cs).
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: equal after the last Debug build.
- The four docs/hero-trace-389-*.txt files were rewritten by the suites (line endings): `git diff --ignore-cr-at-eol --quiet -- docs/` exit 0, then restored by `git checkout`, never indexed.
- The engine, the converter, the data and alundra-datas-analyser are untouched.

### Choices of the executor

- RegisterBreak, BreakKeys and BreakSpriteTypes are separate from Register, Keys and SpriteTypes of C23 so that phase A moves no pin; phase B adds the line to CreateForProduction and BreakKeys.Count to the four pins.
- The loot test does not paste the object: it fixes ContentsItemId (Gildas, 0x45) and derives the type from LootDrop's own rule (item + 0x1E).
- The spark positions are never read (D-E0B-8: they draw random words); the tests count the hit counter and the sparks.
