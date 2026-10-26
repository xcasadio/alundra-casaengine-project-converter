# E20.g2c3 - T2 results (2026-10-26): phase B, the world tests, the inscription

Plan: `docs/plan-e20-heros.md` section 1.24.5, E20.g2c3 T2. Branch of the parent `chantier/theme2-attaques-objets`. Two commits: the world tests (red before), then the inscription (`WaterDropAi.Register(registry)` in `NativeSpriteRegistry.CreateForProduction`), the preset `gouttes-m92` and the five pins derived.

## The tests (`Alundra.Tests/Ai/Objects/`, mirror folder, ADR-0080)

| File | Tests | Plan item |
|---|---|---|
| `WaterDropRun.cs` | bench | the probe bench of T0 made a support: the REAL world of the map (92 of Magyscar, 158 of the Fairy cave), real prefabs and controllers, emptied but for the hero, synthetic flat field, native handlers OFF but for the types asked for (`NativeSpriteTypeFilterForTests`); a drop is born like the opcode `0x8A` (`BornAt`); every tick of every frame sampled at its `inventory` pass (state, saved byte, counter, animation, flags, ground flags, `ForceZ`, `PosZ`, program C, who the drop rides, root vs logical position) |
| `WaterDropWorldTests.cs` | 29 (theory rows included) | (w1) 3 cadences, (w2) 3, (w3) 3, (w4) 3, (w5) 3, (w6) 8 rows, (w7) 2, (w9) 1, (w10) 3 |
| `WaterDropMap92WorldTests.cs` | 3 | (w8) 3 cadences, the stay and the leave in the same test |

Every tick is taken from `expected-ticks.md` (T0 (d)), measured equal in the DLL by the probes S-D1 to S-D8 at 1, 2 and 3 ticks per frame (`results.md`). The cadence-dependent observations of T0 are not asserted (the torch `+0xAC` at 3 ticks per frame, the hero's `CurrentAnimationId`).
- (w1) asserts at each restoration (t89, t177, t265): the state 0, the animation 0, `ForceZ` 0, `Flags` 0x32080 (Gravity out, Collidable back), `PosZ` 160 px, the program C given back, and the position of the ROOT equal to the logical one (`RootFollows`, exact); plus every state and the counter tick by tick of each cycle (R+1 to R+40 counter 40 down to 1, R+41 fall and Gravity, R+65 the two ground flags at the same tick, R+66 state 3 and the sound 145 once, R+67 state 4 and `Collidable` out, R+87 the end of the animation) and the sound ticks 67, 155, 243 alone.
- (w2) the record 28 (`37 [30]; 40 [2,0]`, program 157): A19 at t1, S = 34 (the first state 1, observation 1 of `results.md`), the cycle from S, the restoration at S+87 = 121 with the program 157 back, the script restarted from its first opcode (the `40` clears the program at S+87+N+2, the next state 1 at S+N+90 = 154 and not before).
- (w3) the hero carries a torch: the drop rides the hero at 65 (sound 145), the torch is status 3 with the mark at 66, animation 2 at 67 with the sound 142 once, `FlagToDestroy` at 70 (the hero's carried pointer cleared the same tick), freed at 71, no flame in any tick, the hero's life unchanged at every tick, the hero standing (animation 0) from 71.
- (w4) no torch: the sound 145 alone (the only sound of the run), states 3 at 65 and 4 at 66 with `Collidable` out, the end of the animation at 86, the restoration at 87, nothing else changes (hero, effects, flames).
- (w5) a torch posed: the drop rides the torch at 66, status 3 and the mark at 67, the sound 142 at 68, destroyed at 71, freed at 72, no flame, the drop on the floor at 74 and restored at 88.
- (w6) the window, 8 edge rows: dx 12 and -11 put the torch out at 66, dx 13 and -12 do not; dy 8 and -8 do, dy 9 and -9 do not.
- (w7) the map 158 (record 42, program C 170: `00 ; 3B [2,10,39,59,0,59] ; 04 -8 ; 37 [60] ; 40 [2,0]`), 1 and 3 ticks per frame: the drop hangs in the state 0 for 120 ticks with the hero at the tile (20, 10) (A19 kept 170), the hero put at the tile (5, 45) at the tick 121 starts it, the cycle, the restoration with the program 170 back, the next state 1 at S + N + 91 (one more tick than a simple wait: the box test) while he stays, not before.
- (w8) the arc of the map 92 in its REAL world (production registry, every entity): arrival at the tile (38, 10) level 5, the four drops born at the tick 603 at (756..900, 168), the four torches status 3 with the mark at 669, the four sounds 142 at 670, `FlagToDestroy` at 673, freed at 674, no flame, life unchanged; and the same arrival with the hero put at the tile (10, 10) at the tick 301: no drop, the torches stay lit.
- (w9) the witness, green before and after: a torch thrown (Square + Right) against a drop (sprite 135 posed on the path, its handlers OFF so that it does not depend on the inscription) is put out with the mark and no flame (burning, `FlagToDestroy` three ticks later, freed the tick after). **The contact is posed by the test** (`torch.XCollisionEntity = drop` at every frame): in the DLL `XCollisionEntity` is written only by the scripted step of the controller (`MoveControllerAndPullPosition`), not by the flight of a thrown torch, so a drop on the path (tried at x 150, 165, 178, Z 0 to 60) is not reported as a contact: the torch only burns at its landing (tick 24) without the mark. This is the "montage synthetique" of the plan, as the vectors of E20 pose the contact; it is a noted limit of the port, not a defect of g2c3 (the case is rare in the game: the torch rises to about 67 px, the drops are posed from 136 to 224 px).
- (w10) after the state 3 the drop goes through the hero (no support from 66, Z strictly decreasing), is on the ground at 76 with both flags, stays in the state 4 at Z 0 until 86 and is restored at 87 with `PosZ` exactly 160 px and the root with it.

## Red before, green after

- Before the inscription (commit `f4d57a07`, `dotnet test --filter WaterDropWorldTests|WaterDropMap92WorldTests`, Release, copy neutralised): 32 tests, **31 red, 1 green**: the witness (w9). Red for the right reason (the drops hang): (w1)/(w4)/(w10) the state 2 never comes, (w2)/(w7) A19 does not run (`Saved` 0), (w3)/(w5)/(w6) no torch is put out (the state of the drop), (w8) the torches are never `Deactivated`.
- After: the 32 green; `WaterDropAi*` 35 green (including `R1_...` of T1, red until now).

## The inscription and the preset

- `Alundra/Ai/NativeSpriteRegistry.cs`: `WaterDropAi.Register(registry)` after `BrambleAi`; doc comments follow. The registry goes from 24 to 26 keys.
- Preset `gouttes-m92` (D-G2C3-6; `torche-m92` is at (34, 11), not at the arrival (38, 10), so a new preset is needed): `Alundra/SaveGames/TestSaves.cs`, `docs/test-saves.md` (a section), and `TheDropsPreset_IsTheNewGameAtTheArrivalFromTheMap91IntoTheMap92_WithNoFlag` in `TestSavesTests.cs` (the content test: map 92, tile (38, 10, 5), no flag, the new game's counters, life 10/10, validated by the real rules).

## The existing tests that moved (predicted list of T0 (g): 6; measured: 6, all in the list)

1. `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoid...` (24 -> 26): `+ WaterDropAi.Keys.Count` added to the sum of the classes.
2. `LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses`: the sum, and `WaterDropAi.Register(sum)`.
3. `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_...`: the sum, and `WaterDropAi.Register(sum)`.
4. `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_...`: the tuple's sum.
5. `BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_...`: the sum, and `WaterDropAi.Register(sum)`.
6. `TestSavesTests.TheThirtyTwoPresets_AreListed_InTheOrderOfTheTable` -> `TheThirtyThreePresets_...`: `DropsPresetName` ("gouttes-m92") added to `AllPresetNames`; `EveryPresetName` (3 theories) and `TheOtherPresets_LeaveTheDeathCounterAtZero` grow by one row/preset without a body edit.

Each is derived from the key counts of the classes (as g2c2 did), not from a literal. No other test moved.

## Differences of the DLL from the binary

None in the ticks of the table: every state tick, landing, sound, destruction and freeing equals `expected-ticks.md` at 1, 2 and 3 ticks per frame. One observation, not a difference:
- The **break effect 10** is queued by `DestroyEntity` at the tick of the `FlagToDestroy` (70 carried, 71 posed; the binary attaches it at that tick, `fxattach` of the journal) and the effect stage makes it at the end of its frame, so a sampler at the `inventory` pass sees it at the tick after at 1 and 2 ticks per frame and at the first tick of the next frame at 3 (73 for both). (w3) and (w5) assert the effect exists once, in `[destroy + 1, destroy + ticksPerFrame + 1]`; no tick of the binary is claimed. Cause: `WorldProxy.DestroyEntity` -> `_effectStage.Emit(...)` (queued on the stage, made when the stage runs its frame).

## Results of the runs (Release, copy neutralised, `--blame-hang-timeout 60s`, partitions disjoint by namespace)

| Partition | Tests | Red |
|---|---|---|
| `Ai.Objects.` | 634 | 0 |
| `Ai.` apart from `Ai.Objects.` | 527 | 0 |
| `Hero.` | 1849 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 |
| everything else | 2187 | 0 |
| total | **7405** | **0** |

7334 (T0 HEAD, which excluded the throwaway probe class) + 35 (`WaterDropAi*`, T1) + 32 (the world tests) + 4 (the content test of the preset and the three rows of the `EveryPresetName` theories) = 7405. `check_names.py --rules`: 0 finding. The suites rewrote the line ends of `docs/hero-trace-389-*.txt` (restored by `git checkout`, never indexed).
