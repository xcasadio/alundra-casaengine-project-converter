# E20.e0c, T3 (phase C): results (2026-10-23)

Base: HEAD `173ffdb9` (T2), branch `chantier/theme1-porter-plateforme`. Start of the suite: 5 224 tests in Release, all green (T2). Binary `ALUN_CD.EXE` (France).

## What is delivered

- `Alundra/Maps/WorldProxy.cs`, `DestroyEntity(EntityScriptProxy, int)` (`0x8003A59C`): ONE line, the first act of the destruction: `LootDrop.SpawnContents(this, GameState, entity)` (`0x80032B90`), before the status 4 and the letter -1. The single-argument overload (the light destruction, `0x8003A774`) is untouched and drops nothing. The doc comment no longer calls the loot a named stub.
- Tests, written BEFORE the line (all in the REAL world of the map 14, the only kind of world in which an object appears; a bare `new WorldProxy()` refuses every apparition by index):
  - (d1) `DestroyEntityBreakEffectTests`: the pin of the stub `TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` is rewritten as `TheDestruction_DropsTheContents_AtTheEntity_DrawsNoNumber_AndPlaysTheSound84_WhileAContentOfZeroDropsNothing` (contents `0x45` drops the type 99 at the entity, one raw unit up, kind "dropped", life 600, sound 84 last, the generator not moved, the contents kept; paired with the same destruction of a content of 0: nothing, no sound). Three new tests join it: the veto (`TheDestruction_DropsTheObjectsOfTheMagic_OnlyWhenTheHeroHasMagic`: `0x26` with `MpMax == 0` drops nothing, with `MpMax == 5` it drops), the order (`TheDrop_IsTheFirstAct_...`: when the object appears, read from `CreationFaultHookForTests`, the entity still has its status `Normal` and its letter 4) and (d3) `TheLightDestruction_DropsNothing_WhileTheTwoArgumentDestructionOfTheSameContentsDropsIt`.
  - (d2) `BeannoideOracleTests` and `MurutaOracleTests`, `E1_TheVectors_...` and `E1_TheCallsOfTheScenarios_...`: C7 is no longer "no entity from the loot". The new helper `Alundra.Tests/Ai/Enemies/EnemyDeathLoot.cs` makes the DLL's world answer the oracle's journal call by call: as many objects as `spawn_39F58` entries (type, position, the binary's Z of the drop equal to the container's, the kind "dropped"), the sounds asked end with the journal's `sound` entries (84), and nothing is played when nothing dropped. Counts asserted: 80 drops (and 80 sounds), 48 vetoes (contents `0x26`, `0x51`, `0x52`, the hero without magic as in the oracle), 16 empty contents, in each of the two species; the scenarios drop 3 (Beannoid) and 6 (Muruta) objects.
  - (d3b) `PickupAiDropWorldTests.D3b_...` (6 cases, 1, 2 and 3 ticks per frame, the object of a higher and of a lower slot): the first-tick measures of (w9) on the real path, the object made by the `DestroyEntity(e, -1)` of a container in the program C of an arrow (`BornByAnotherEntity(..., throughDestroyEntity: true)`): program A at the birth tick + 1, first C255 at + 2, `IsOnGround` 0, the force positive, the life 600.
  - (d4) `Alundra.Tests/Arcs/BeannoideLootArcTests.cs` (2 arcs): the Beannoid of the record 1 of the map 14 is killed with the sword on the real hit pass (the policy of `BeannoideGrateArcTests`); its drawn object (`0x47`, 10 gildas) appears at it (type 101, kind "dropped", sound 84), rises, falls, bounces; either the hero takes it (money +10, the sound of the object, the object gone after the state 3, the state 2 never reached) or it is left alone and vanishes at the 600th tick of its life (measured: born at frame 143, gone at frame 745; the series starts at life 600; the mark at 120 is crossed).
- The engine, the converter, the data and `Alundra/Entities/EntityScriptProxy.cs`: no diff.

## Red before, green after

- The 16 tests of `t3-red-names.txt` were run RED against the unchanged code (the line absent): d1 x4 (the rewritten stub and its three companions; the light-destruction test is red through its positive pair), d2 x4, d3b x6, d4 x2. Each fails for the reason it tests (no object born, "the oracle dropped 1 object(s), the DLL 0", "the collection was empty", "the Beannoid is not dead at the frame limit; hp 0, status Destroyed" for the arcs: the kill works, nothing drops).
- Green after the line: the same 51 tests of the five classes (35 that were green before and stay green, 16 that were red).
- Two real mutations of the line, applied by script and undone byte for byte (the file compared with the pristine copy after each):
  - the loot AFTER the status 4: killed by `TheDrop_IsTheFirstAct_TheEntityStillHasItsStatusAndItsLetterWhenTheObjectAppears` alone;
  - the loot also in the light destruction: killed by `TheLightDestruction_DropsNothing_...` and the six `D3b_...` (their cleanup).
  The 24 mutants of T5 are not part of this task.

## Tests that moved (against the predicted list of T0 (e), phase C: 5 certain, 2 families of arcs to measure)

| predicted | now | change |
|---|---|---|
| `DestroyEntityBreakEffectTests.TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` | `...TheDestruction_DropsTheContents_AtTheEntity_DrawsNoNumber_AndPlaysTheSound84_WhileAContentOfZeroDropsNothing` | rewritten by derivation (the stub becomes the drop), renamed (the old name would be false) |
| `BeannoideOracleTests.E1_TheVectors_FollowTheProjectionOfThePlan` | same name | C7 compares `spawn_39F58` and the sound 84 (the world of the call is the real map 14) |
| `BeannoideOracleTests.E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan` | same name | same |
| `MurutaOracleTests.E1_TheVectors_FollowTheProjectionOfThePlan` | same name | same |
| `MurutaOracleTests.E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan` | same name | same |

Measured for the two families of arcs: `BeannoideGrateArcTests.G116_...` and `MurutaHallArcTests` did NOT move (green, unedited, in the full suites): the objects their kills now drop do not change what they assert. Five moved tests, all in the predicted list (the ceiling of the plan is 10). The other tests of `DestroyEntityBreakEffectTests` (bare world: the apparition is refused, a content set on a victim there drops nothing) did not move.

## Suites

| suite | tests | failures |
|---|---|---|
| base (T2), Release, neutralised copy | 5 224 | 0 |
| after T3, Release, neutralised copy | 5 235 | 0 |
| after T3, Debug (last) | 5 235 | 0 |

5 235 = 5 224 + 11 (the stub test becomes 4: +3, the six `D3b_` cases, the two arcs). `docs/hero-trace-389-*.txt` rewritten (line endings) by the suites: `git diff --ignore-cr-at-eol --quiet -- docs/` empty (rc 0), then restored. `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical.
`python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 warning (the one of the start: `TextBoxGpu.cs:266`).

## Facts and deviations from the plan

1. **The oracle tests need the real world.** The bare `new WorldProxy()` of `RunE1Call` refuses every apparition, so comparing `spawn_39F58` needs an initialised world with the catalogue: the tests now use `PickupDropRun.OnMap14` (one per test), reloading the effect bank at each call and removing the objects born after each call. The Z cannot be compared to the oracle's absolute value (the world is the map 14, not the map of the call): the binary's Z of the drop is compared to the container's (equal), the object's Z must be at least the container's plus one raw unit (D-E0A-1); the exact value is pinned in the (d1) test, with a container above the floor. Where the floor of the map 14 is above the container the apparition lifts the object (MU-hall-kill tick 59: Z `9437184`), which is the apparition of E20.e0a1.
2. **The sounds.** The sound of the appearance of the object, if its sprite has one, comes before the sound 84 (`sounds at birth [..., 64, 84]` in the arc); the tests compare the END of the list with the journal.
3. **The Muruta scenarios drop 6 objects**, the Beannoid's 3 (the counts of the vectors, 80 / 48 / 16, hold for both species).
4. **(d4) preconditions read from the data**: the Beannoid of the record 1 of the map 14 draws `0x47` at the load under the seed of the arc; the test asserts it first, so a drift of the roll fails loudly instead of changing the arc.
5. **A slip of the session, repaired**: one early Release build was run without the neutralised copy (a wrong path for the targets file), which copied the Release DLL into `alundra-project/` at 00:18; the Debug DLL and PDB of the previous build were copied back at once (`cmp` identical) and the Debug build at the end of the task redeployed them. Nothing else under `alundra-project/` changed.
6. **The trailer** of the commit names `Claude Opus 5.5`, as the task text says; the commits of T1 and T2 name `Claude Sonnet 5.5`.
