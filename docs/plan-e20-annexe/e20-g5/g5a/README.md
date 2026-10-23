# E20.g5a: the pushable pillars (C76), measured results

Plan: `docs/plan-e20-heros.md` section 1.24.6, sub-section "E20.g5a" (revision 0, READY). Discovery annex: `../disc/`. Start HEAD: `2eff6b46` (branch
`chantier/theme1-porter-plateforme`, after E20.e0c2). This file is written task by task; this first version is T0 (the commit of docs) and T1 (phase A).

## T0 - re-reading, oracle replay, prediction, montages

### Oracle replayed from a copy (scratch, outside the repository)

`disc/` and the MIPS machine `../../e20-g1a/work/` were copied to a scratch folder (the scripts write next to themselves and read the binary
`ALUN_CD.EXE` (France) at the absolute path they name).

| replay | command | result |
|---|---|---|
| C76 and its helper against the real code | `python -B -X utf8 c76_full.py 6000 mut gold` | 6 000 vectors, 0 mismatch, instruction coverage 82/82, 14 outcome classes, 73 one-word mutants: 56 killed, 17 survivors (the same 17 as the annex: 10 stack frame, 4 `move` to `subu`, 3 on the `-1` constant of the helper, dead code since C76 pre-tests the contact); the output equals `../disc/c76_full_out.txt` (line endings aside) |
| goldens | same run | `c76_goldens.json` rewritten, byte for byte equal to `../disc/c76_goldens.json`, SHA-256 `c80ffbe3def37599c6671f32bf2020153c6b782c235707eceecce09f4ad90d5a` |
| scenarios P1 to P11 on the real `MovePlayer`, spawner, C76, physics | `python -B -X utf8 g5_scen.py` (2 min 23 s) | equal to `../disc/g5_scen_all_out.txt` (the annex file only ends with one more line, `done`, written by the shell wrapper), and the 22 tables of `out/` are equal file by file |

### The registry, its pins and the movement probe at the start HEAD

- `NativeSpriteRegistry.CreateForProduction` (`Alundra/Ai/NativeSpriteRegistry.cs`) registers `BeannoideAi`, `MurutaAi`, `WaveSwordAi`, `PickupAi`,
  `ChargedArrowAi` and `ArrowAi` in that order; `Register` throws on a taken key and returns an `IDisposable` that removes the entry.
- The pins of the count of the production registry, three of them, all written as a sum of the counts of the classes:
  `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` (`6 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count`),
  `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185` (`V3_...`, same sum with `3 + 3 + 1`),
  `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:225-234` (`C2_TheProductionRegistry_...`, a registry `sum` built by hand with one `Register` per class, then
  `Assert.Equal(sum.Count, production.Count)` and the same literal sum).
  `ArrowAiTests.E5_...` counts only `ArrowAi`'s own keys and does not move.
- The movement probe: `EntityScriptProxy.MoveControllerAndPullPosition` (`Alundra/Entities/EntityScriptProxy.cs`, near line 2424) clears `XCollisionEntity`, moves the
  controller, and sets `XCollisionEntity` to the proxy of `LastContact.H2Obstacle ?? H1Obstacle` (E19.d2b B4). The pillar is not ignored by the probe, so the hero
  stops against it and C76 reads `hero.XCollisionEntity == pillar`.
- The fields C76 reads, by their names in the DLL: `+0x1D8` is `ModdedPosX` (rewritten at the head of the tick for the active entities nothing blocks,
  `WorldProxy.cs` near line 2993, as the binary does; `PickupAi.Overlaps` reads it the same way), `+0x1F0` is `Width` (the same `(size << 16) - 1`),
  `+0x114` and `+0x118` are `PosX` and `PosY`, `+0x88` and `+0x8C` are `TargetAnimationId` and `TargetDirection`, `+0x274` is `Bytes[0..3]`.
  The discovery's prototype read `PosX + ModX` for the first one; the port reads `ModdedPosX`, the word the binary reads.

### Prediction of the existing tests that move, measured by a temporary wiring

Wiring: the discovery's prototype `PillarAi.cs` copied as is to `Alundra/Ai/Objects/` and `PillarAi.Register(registry);` after `ArrowAi.Register(registry);`
in `CreateForProduction`. Release build with the neutralised copy, then the whole `Alundra.Tests` suite in four partitions (a full run is 10 min 51 s, over the
foreground limit): baseline at the start HEAD 5 434 tests, 0 failures.

| partition (filter on `FullyQualifiedName`) | tests | failures with the wiring |
|---|---|---|
| `Alundra.Tests.Arcs` | 76 | 0 |
| `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 084 | 3 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 485 | 0 |
| everything else | 1 789 | 0 |

The three failures are the three pins above, and only them:
`NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn`,
`WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem`,
`ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses`.
So the arc TN-3 of the map 346 (a pillar where the hero stays parked) and `ArrowAiTests` (type 430 as a neutral value) do not move. The wiring was removed
afterwards (`git status` clean, the four `docs/hero-trace-389-*.txt` restored).

**Predicted list for T2** (the files and tests that change): the three pins above, rewritten by derivation (`+ PillarAi.Keys.Count`; for the `ChargedArrowAiTests`
test, one more `PillarAi.Register(sum)`); nothing else. T2 measures it again with the final code (it reads `ModdedPosX`, not the prototype's `PosX + ModX`).

### Montages chosen for the world tests (T2)

All through `ArcRun` with `ArcSpec(... RealController: true, Prefabs: true ...)` (the real controller and the real prefabs), `PlaceHero`, `HoldDirections`,
`EntityByRecord`, as the discovery probe did (`../disc/probe_G5ProbeTests.cs.txt`, cases in `../disc/cases*.json`).

| test | map and record | montage |
|---|---|---|
| (w1) east, (w2) north, (w3) speed and release, (w4) vertical offset, (w8) sound | map 26 (`Ancient Shrine-26`), pillar record 29 | hero at (300, 424, 16), `R` for 160 frames (east, stop at 350, hero 329); hero at (324, 452, 16), `U` (north, stop at 406); release and press again for (w3); (w4) the hero placed at `|dy| = 12` and `14` from the pillar |
| (w5) a plate or a switch on the ground blocks, two aligned pillars do not chain | map 26 and a built bench | a plate (type 108) or a switch (type 45) spawned on the path; a second pillar beyond the first |
| (w6) the pit | map 347 (`Lake Shrine (inner)-347`), pillar record 16 | the blockers, records 6 and 11, removed by a write of the harness (`DestroyEntity`), hero at (168, 232, 48) east or (240, 232, 48) west; plus one test with no removal (the pillar stays blocked) |
| (w7) the 13 inert pillars | map 423 (`Nirude (inner)-423`), records of type 430 | the hero pushes against each; no native run, no motion |

## T1 - phase A, the pure functions (tests first)

Files: `Alundra/Ai/Objects/PillarAi.cs` (new; no production caller, the key is not registered before T2), `Alundra.Tests/Ai/Objects/PillarAiStepTests.cs` (a1),
`PillarAiDirectionTests.cs` (a2), `PillarAiWordTests.cs` (a3), `PillarAiGoldenTests.cs` (the 14 goldens through `PillarAi.Tick`), and `AnnexFiles.PillarAnnex`
in `Alundra.Tests/Support/AnnexFiles.cs` (finds `docs/plan-e20-annexe/e20-g5/disc/c76_goldens.json` above the test binaries).

Design: `PillarAi.Step(counter, contact, animation)` returns a `CounterStep` (the counter after the tick and four flags: sound, animate, stop, clear);
`PillarAi.Direction(pillar, hero)` is `GetPushablePillarPushDirection` (it reads `ModdedPosX`, `Width`, `PosX`, `PosY`, as the binary reads `+0x1D8`, `+0x1F0`,
`+0x114`, `+0x118`); `ReadCounter` and `WriteCounter` hold the signed word `+0x274` little-endian on `Bytes[0..3]`; `Tick` is the thin handler (the contact is
`hero.XCollisionEntity == pillar`). The stop of the sound is the named stub `StopSoundEffect`, journalled in `StopSoundStubCallsForTests` (thread-static).

Red on the skeleton (the signatures compile and throw `NotImplementedException`; Release, neutralised copy, `--filter FullyQualifiedName~PillarAi`):
11 tests, 10 failed, 1 passed (the one that pins the SHA-256 of the golden file, which needs no code).

| red before | green after |
|---|---|
| `PillarAiStepTests.A1_WithTheContactUnderTheThreshold_TheCounterGoesUpByOne_AndNothingElseHappens_AtTheThresholdTheSoundAndTheAnimationStart` | green |
| `PillarAiStepTests.A1_TheThresholdIs31Exactly_30IsStillCounting_32AndMoreAreAlreadyPushing_NoOverflowAtTheTop` | green |
| `PillarAiStepTests.A1_WithoutTheContact_TheCounterIsReset_TheAnimationIsCleared_AndTheStopOfTheSoundIsAskedOnlyIfTheAnimationWas1` | green |
| `PillarAiDirectionTests.A2_TheFourSides_EastWestNorthSouth_AreTheValues_0x18_8_16_0` | green |
| `PillarAiDirectionTests.A2_TheWidthBounds_AtTheWidthTheColumnsStillOverlap_OneMoreIsTheHorizontalSide_ForBothSigns` | green |
| `PillarAiDirectionTests.A2_TheAxisIsPickedOnTheModdedPositions_TheSenseOnThePositions_AndAnEqualPositionTakesTheSecondValue` | green |
| `PillarAiDirectionTests.A2_TheWrapOfTheOffset_MinusTheMinimumStaysTheMinimum_SoTheColumnsOverlap_NextToTheOrdinaryHorizontalSide` | green |
| `PillarAiWordTests.A3_TheWordRoundTrips_ForZero_31_MinusOne_AndTheLimits` | green |
| `PillarAiWordTests.A3_TheBytesAreLittleEndian_AndOnlyTheFirstFourAreTouched` | green |
| `PillarAiGoldenTests.TheGoldens_AreFollowedWordForWord_TheCounterTheAnimationTheDirectionTheSoundAndItsStop` | green |
| `PillarAiGoldenTests.TheGoldenFile_IsTheOneTheDiscoveryPinned_14Vectors_10WithTheContactAnd4Without` (green before and after) | green |

After the implementation: 11 tests, 0 failed. The golden test plays the 14 vectors (10 with the contact, 4 without) through `Tick`: the three written words, the sound
journal (8 sounds) and the stop journal (2 stops) equal the machine's, the four directions 0, 8, 16, 24 all appear, and the read-only fields are unchanged.
`python -B tools/dll-structure/check_names.py --rules`: 215 DLL files, 452 test files, 0 finding, 1 warning (the one at the start, `TextBoxGpu.cs:266`).
No existing test moved (nothing registers the key yet); the mutants of the plan are run in T3 once the registration exists.

## T2 - phase B, the registration and the world tests

Files: `Alundra/Ai/Objects/PillarAi.cs` (`Keys`, `Register`), `Alundra/Ai/NativeSpriteRegistry.cs` (`PillarAi.Register(registry)` after `ArrowAi`), the tests `Alundra.Tests/Ai/Objects/PillarAiWorld26Tests.cs` (w1, w2, w3, w4, w8),
`PillarAiBenchWorldTests.cs` (w5), `PillarAiPitWorldTests.cs` (w6), `PillarAiInertWorldTests.cs` (w7), `PillarAiRegistrationTests.cs` (the key for the type 430 alone), the world `PillarRun.cs` (an `ArcRun` with the real prefabs and controller, the sounds
recorded by a `RecordingSoundPlayer` put in the world, the stop of the sound journalled by the named stub, one sample per frame), and the three pins rewritten by derivation.

### Red before the registration, green after (Release, neutralised copy, `--filter FullyQualifiedName~PillarAi`)

The red run is the working tree of T2 with the registration line removed (a real removal by script, then put back): 50 tests, 14 failed, 36 passed (the 11 of T1 and 25 that cannot be red, see below).

| red before (14) | green after |
|---|---|
| `PillarAiWorld26Tests.W1_PushedEast_...` | green |
| `PillarAiWorld26Tests.W2_PushedNorth_...` | green |
| `PillarAiWorld26Tests.W3_TheSpeedIs075PixelPerTickInXAnd05InY_...` | green |
| `PillarAiWorld26Tests.W4_AHeroOffsetByUpTo12PixelsInY_PushesThePillar_...` (dy -12, 12, 14: 3 cases) | green |
| `PillarAiWorld26Tests.W8_TheSound25IsAskedAtEveryTickOfPush_...` | green |
| `PillarAiBenchWorldTests.W5_WithNothingOnItsPath_...`, `W5_ASteppingSwitchOnTheGroundBlocksThePillar_...`, `W5_TwoAlignedPillarsAreNotPushedTogether_...` | green |
| `PillarAiPitWorldTests.W6_PushedEast_...`, `W6_PushedWest_...`, `W6_WithTheBlockersOfThePuzzleLeftInPlace_...` | green |
| `PillarAiRegistrationTests.TheKeyIsC76_AndTheProductionRegistryHoldsItForTheType430` | green |

Green before and after, by nature (each "no effect" case sits beside a positive case that is red before): `W4_AHeroOffsetByMoreInY_...` (dy -14 and 16), `W6_WithoutThePillarInThePit_...` (the control of the pit),
the 13 inert pillars of the map 423 (`W7_...`: 12 Theory cases and the pillar 24), `TheKeyIsNotRegisteredForAnyOtherSpriteType` (8 types: 298, 229, 345, 335, 7, 0, 429, 431) and `TheRegistrationIsAKeyOfItsOwn_...`.
After the registration: the 50 tests of `PillarAi` are green.

### The suite (Release, neutralised copy, four partitions as in T0)

| partition | tests | failures |
|---|---|---|
| `Alundra.Tests.Arcs.` | 76 | 0 |
| `Ai.`, `Audio.`, `Cameras.`, `Cells.`, `Combat.`, `Data.`, `Dialogues.` | 1 134 (1 084 + the 50 of `PillarAi`) | 0 |
| `Entities.`, `Events.`, `ExportGuards.`, `Hero.`, `Items.` | 2 485 | 0 |
| everything else | 1 789 | 0 |

Total 5 484 = 5 434 + 50, zero failure. `check_names.py --rules`: 215 DLL files, 458 test files, 0 finding, the same 1 warning. The four `docs/hero-trace-389-*.txt` rewritten by the suite are equal but for the line endings
(`git diff --ignore-cr-at-eol --quiet` clean) and were restored. The Debug build comes with T3.

### The existing tests that move: the predicted list, and nothing else

The three pins of the count of the production registry, rewritten by derivation (`+ PillarAi.Keys.Count`; for `C2_TheProductionRegistry_...`, one more `PillarAi.Register(sum)`):
`NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOf...`, `WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_...`, `ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_...`.
`ArrowAiTests` (the type 430 as a neutral value) and the arc TN-3 of the map 346 do not move, as T0 measured.

### Measured values (the DLL, frames numbered from 1 = the first frame the direction is held)

| case | contact | animation 1 and sound | first step | stop |
|---|---|---|---|---|
| map 26 east, hero (300, 424, 16) | frame 2 | 34 (= 2 + 32), direction 24 | 35 (324.75) | x 350 from 69, hero 329 from 70 |
| map 26 north, hero (324, 452, 16) | 9 | 41 (= 9 + 32), direction 16 | 42 (423.5) | y 406 from 77, hero (324, 421) |
| map 347 east, hero (168, 232, 48), records 6 and 11 removed | 7 | 39, direction 24 | 40 | leaves the edge at 134 (z 47.5), lands at 144 on (278, 232, 16) |
| map 347 west, hero (240, 232, 48) | 8 | 40, direction 8 | 41 | leaves the edge at 135, lands at 145 on (130, 232, 16) |

The machine (`disc/out/p11_*.txt`, `p1_*.txt`): contact 7 and 8, animation 1 at 39 and 40, first displacement at 39 and 40, landing at 143 and 144; the DLL is one tick later at the first step and one tick later at the landing.
Speed 0.75 pixel per tick in X and 0.5 in Y. Release at the frame 61: the hero is free at 62, the pillar still asks the sound at 62, the animation goes to 0 and the stop of the sound is asked (once) at 63, the pillar ends at 345.75;
a new push gives the contact at 72, the animation at 104 (= 72 + 32), the first step at 105. Sound 25 asked at each of the 127 ticks of the push between the frames 34 and 160; the stop is never asked during a continuous push, nor for a pillar that was never pushed.
Bench of the map 26 (the pillar carried to (260, 424), the hero west of it): a stepping switch at (310, 424) stops it at 288 (= 310 - 12 - 10), a second pillar there stops it at 290 (= 310 - 20) and is never pushed (counter 0, animation 0);
with nothing on the path it runs to 350. Over the pit of the map 347 the hero keeps the old floor (z 48) with the pillar as its support at each of the 16 frames over the pit; with the pillar removed after the landing it drops to z 16.

### Gaps against the plan text (none against the machine's final states)

- **Landing: +1 tick, not +2 or +3.** D-G5-4 and the discovery report say two or three ticks more at the landing in a pit (the report quotes "frames 146 against t144 and t143"). The report's own probe files (`probe_out_proto/pillar347_*_open.txt`) and the replay of T2 give
  the landing at the frame 144 (east) and 145 (west) against the machine's t143 and t144: one tick, the same as the first step. The tests pin the values measured on the DLL.
- **w4, the vertical offset.** The plan says "contact and push at |dy| = 12, nothing at 14". On the DLL, as on the machine (`disc/out`, P5 of `g5_scen_all_out.txt`), the offset is not symmetric: -12 and +12 push, +14 still pushes, -14 does not (-13 pushes), +16 does not.
  The tests pin -12, +12, +14 (push) and -14, +16 (no contact, nothing pushed, no sound, no stop).
- **w5, the plate.** The plate of the puzzle rooms (sprite 108) is in the page of the map 27, which holds no pillar (the page of the map 26 holds 174 and 45, not 108): the stepping switch (sprite 45, same collidable slab) stands for it. The bench carries the real pillar to
  (260, 424), because from its place (324, 424) only 26 pixels are free before the wall and a switch spawned in the wall column lands on the top of the wall (z 80).
- **w7, the thirteenth pillar.** The pillar 24 of the map 423 has its own scripted A and C: pushed from the north side it is moved by that script (about 12 pixels in 70 ticks), never by C76 (no native run, counter 0, no sound). The twelve others (records 12 to 23, a program C that does nothing) do not move.
  "The 13 pillars stay inert" is true of the native handler for all 13, and of the motion for 12.
- Inert and negative cases cannot be red before the registration (see above); each is paired with a positive case that is.
