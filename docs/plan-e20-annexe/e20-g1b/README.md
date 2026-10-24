# E20.g1b - Lifting (the activation): execution record

Plan: `docs/plan-e20-heros.md` section 1.24.4 "E20.g1b" (revision 2, READY, with the block "Heritage de g3"). Branch of the parent `chantier/theme1-porter-plateforme`.
Start HEAD of the execution: `5eccf614` (E20.g4a done). Binary: `ALUN_CD.EXE` (France), the oracle scripts read it at `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`.

## T0 - result: STOP (the contact of the DLL differs from the binary at ticks the plan compares)

T0 (a) is done and found a map. T0 (b) measured the contact tick by tick: the air, the lines A, C, E2, F, G of the ground and the case O-G3-3 give the contact the plan expects up to a
point, but **the lines B, D and D2 differ at the tick t10 (the tick the direction is released)** and **O-G3-3 differs by one tick** (already flagged by g3, ADR-0091). The plan says
"un contact qui diffère aux ticks comparés est un arrêt". Nothing was changed in the DLL (no code commit); the probes were temporary test classes, removed, kept here as `.cs.txt`.

### Sites of the plan re-located by content at the start HEAD

| Plan site | Found at the start HEAD |
|---|---|
| `PlayerTryAction` stub (the body of the "bouchon") | `Alundra/Hero/PlayerManager.cs:955` (`private static int PlayerTryAction(EntityScriptProxy player, in PadState pad) => 0;`), called from `:437` and `:456` after `TryUseItem`; the other `TryUseItem` calls (`:580`, `:612`, `:622`, `:684`, `:690`) are the escalade/swim exits |
| `AlundraTestSavesTests.cs` | `Alundra.Tests/SaveGames/TestSavesTests.cs` (class `TestSavesTests`); the "second table" is not a table: one Fact per preset (`TheSwimPreset_...`, `TheElevatorPreset_...`, `TheBarricadePreset_...`); the lists are `AllPresetNames` (`:86`) and `EveryPresetName` (`:90`) |
| presets | `Alundra/SaveGames/TestSaves.cs` (`Presets`, `barricade-m392` is the last, `:155`) |
| `ArcsThatSlide` | `Alundra.Tests/Support/ArcRun.cs:311` |
| hero contact `XCollisionEntity` writer | `Alundra/Entities/EntityScriptProxy.cs` (`H2Obstacle ?? H1Obstacle`), unchanged since g3 |

No difference of substance with the plan on these sites.

### (a) The map of the recipe: map 72 (Coastal Cave), preset `porter-m72`

Method (all read-only, `t0/`): `census_recipe.py` + `census_dirs.py` read the exported tileMaps (`alundra-project/Maps/*/*/tilemap`) and the sprite records, take every map with at least one wired liftable (header
`ProgramTick == 23`, pickup kind != 0: the (C, 23) key of g1a, "Cruche generique" and the turtle rock), every platform-like placement (names of `census_platforms.py`), resolve the tick program of the placement
(`EventCodesC_TickIndex & 0x7F`, the rule of `EventProgramRunner.InitializeEventData`) in the events export with `decode_events.py` (copied unchanged from `e20-g3/dll/`, the opcode sizes are the DLL's table),
and follow the direction through the program (record facing `SpriteDirection & 3` -> 0, 0x10, 0x08, 0x18 = down, up, left, right; `0x08` add, `0x09` set, `0x0A` reverse = +16). 42 platform programs with a walk
exist on maps that also have a wired liftable (`out/census-dirs.txt`); 5 walk on the horizontal only (`out/census-pure-horizontal.txt`): maps 23 and 100 (a "Bloc a pousser" gated by a flag), **72, 74**, and 346 (gated
by a wait only, one jar at z 176).

**Map 72, entity 2, "Plateforme flottante (grande)" at (576, 240), z 64** (`out/map72-platform-program.txt`): record facing 3 (east), tick program C[3] = `0x1A anim 2; 0x1E Walk 96 px; 0x08 +16 (reverse); 0x02 Goto -5` = **96 px east, 96 px
west, for ever, no test of a flag, an entity, the pad or the hero** (the program is `Set anim`, `Walk`, `Add to TargetDirection`, `Goto`; it starts at the load of the map, its load program A is the empty program 0).
- A wired liftable: 8 "Cruche generique" and "Rocher tortue" with the header (C, 23) (entities 37 to 44), among them the jars 37 (204, 200, z 48) and 38 (396, 200, z 48).
- A top of 16 px within 100 px of a jar: the barrel "Tonneau generique" (entity 1, 21 x 15 x 16, collidable, header C4: not wired, so it also serves as the m5 witness "a liftable outside the set gives the sword") at (180, 200, z 48), 24 px
  from the jar 37; the jars themselves are 16 px high too.
- Free at the arrival: no `0x10` (player loses control) nor `0x11` in any program of the map (`out/census-map-72-74-346.txt`, "lock/gain ops in the programs: []"); B[1] (the region (0, 6)-(35, 28)) tests the pad (`0x2F`) and the hero's area (`0x3B`) and
  plays sounds, nothing more. Arrivals: from the portals of the maps 70 and 72 at (36, 160), (60, 472), (144, 88), (432, 464), (408, 96), (480, 128).
- Geometry read in the export (cells 24 x 16 px, `Map.MapTiles`): the platform floats over water cells (ground property set) of the tiles x 18 to 28, y 10 to 17, between a west bank and an east bank of the height byte 3 (= z 48, the
  level of the jars); its top (z 72) is 24 px over the banks. This is a reading of the data, not a play-through: the walk from the arrival to the platform, with the object, is for the arc (m2) of T2.

Other candidates and what they lack, in game terms: **map 74** (jars on the high ground at the west, far from the platform at the south, switches that make other platforms fly; no lock): the platform and the jars are 500 px apart, the
route needs the puzzle; **map 346**: one jar high up (z 176) far from a platform that waits 40 ticks between its legs, and two lock ops (`0x10`) in the map's programs; **maps 27, 69, 343, 86, 140**: the platform moves by rectangles
(vertical legs and horizontal legs), no pure horizontal shuttle; **maps 23, 100**: "Bloc a pousser" gated by a flag, and no 16 px top nearby. Map 72 is the only one that has the three conditions at once.

### (b) The probe of the DLL (temporary, not committed): `t0/probes/`

Tools: `critic-scenarios/c3_contact_ticks.py` (written for this T0, in `e20-g1a/critic-scenarios/`, same scenarios and pad scripts as `c3_contact.py`, the contact of each tick printed in addition;
its verdicts are **equal** to `out/c3-contact.txt`: `out/c3-contact-ticks-verdicts.txt` has the same SHA-1 `0b9ab493...`), `t0/air_ticks.py` (the script of g15 a) without Square, tick by tick), `t0/og33_square.py` (the route R7 of
`e20-g3/bin/a3_routes.py` with a Square at each tick), `t0/probes/G1bT0ProbeTests.cs.txt` and `G1bT0ReleaseProbeTests.cs.txt` (the temporary xunit classes, run in Release with the neutralised copy, 1, 2 and 3 ticks per frame,
removed after the run), `t0/compare_contacts.py` (the comparison, `out/compare-contacts.txt`). The oracle outputs were replayed from copies in the scratch directory, never in place.

**(b1) In the air** (`out/air-ticks.txt`, `probes/dll-probe.txt`): hero from (100, 100), crate at (130, 100), RIGHT x3, RIGHT+CROSS at t4, no Square. Contact = the crate at t5 and t6, nil at t7, t8, t9; z 5.0, 9.5, 13.5, 17.0, 20.0,
22.5; animations 2, 44, 44 at t4, t5, t6; x 108.5312, 109.0, 109.0, 111.2969...: **the DLL is equal to the oracle at 1, 2 and 3 ticks per frame** (contact, z and animation at t4 to t9, x also).

**(b2) On the ground**, lines A to G of `c3` (the contact of each tick of the hero, present or nil), ticks compared as the plan lists them, at 1, 2 and 3 ticks per frame (the three give the same result):

| Line | Ticks compared | Result |
|---|---|---|
| A (Square t10) | t1..t9 | equal |
| B (Square t11) | t1..t10 | **different at t10** |
| C (Square t10, RIGHT held) | t1..t9 | equal |
| D (Square t15) | t1..t16 | **different at t10** |
| D2 (Square t16) | t1..t15 | **different at t10** |
| E (Square t5) | t1..t15 (see below) | equal |
| E2 (Square t6) | t1..t5 | equal |
| F (Square held) | t1..t16 | equal |
| G (UP, Square t10) | t1..t16 | equal |

Note on E: the plan asks t1 to t16 but the oracle script of E has 15 ticks (4 + 1 + 10); the comparison is capped at t15 (no difference of substance).

**The difference** (lines B, D, D2; `out/c3-contact-ticks.txt`, `probes/dll-probe.txt`): the hero pushes the crate for 9 ticks, then releases RIGHT at t10. In the binary the contact is **still the crate at the end of t10** (the force of the walk
falls from 2.4375 to 1.2188 px for one tick and that half step is refused again: `adj` 1) and clears at t11; a Square at t11 lifts (line B: LIFT). In the DLL the force falls to 0 at t10, no step is tried, the contact is nil at the end of t10: a Square
at t11 would give the sword. The cause is not the contact code but the **release of the walk**, a general one: with no crate at all the binary moves one more half step at the release tick (x 120.7188 -> 121.9375,
`out/oracle-release-open-vs-crate.txt`) and the DLL does not (x 120.7188 stays, `probes/dll-release-probe.txt`). This is an existing difference of `MovePlayer`/the walk of the hero (not in the scope of g1b, whose files are
`PlayerManager.cs` `PlayerTryAction`/`TryLift`, the escalade calls, the preset): lines B, D, D2 are exactly the "la force residuelle garde le contact un tick" of the table of expected values of the plan.

**(b3) O-G3-3, the hero on rec5 of the map 392 pushing south into the pile** (`out/og33-square.txt`, route R7 of g3 replayed on the MIPS machine; the DLL: `probes/dll-probe.txt`, section b3):
- binary: DOWN from t37: t37 y 296.812, t38 y 297.000 with **contact nil**, t39 contact rec10. A Square at t37, t38, t39: sword (animation 16, no lift); at t40 and after: **lift of rec10** (animation 5).
- DLL (g3, ADR-0091, measured again): tick 1 y 296.8125, tick 2 y 297.0 with **contact rec10** (one tick earlier), `ForceAdjusted` from tick 3.
- So a Square on the tick right after the arrival (t39) reads, in the DLL, the contact set by the arrival tick and **would lift rec10 once PlayerTryAction is wired, where the binary gives the sword**. That is the case the plan
  names as a stop.

### (c) Predicted list of the existing tests that move (presets)

`TestSavesTests` lists the presets in `AllPresetNames` (14 entries at the start HEAD: `day3-after-dream`, `day4-meeting`, the 6 weapon presets, `swim-m14`, `pickup-m11`, `loot-m14`, `herbe-m14`, `elevator-m22`,
`barricade-m392`); the recipe preset `porter-m72` makes it 15 (and `docs/test-saves.md`). Existing tests that move (all in `Alundra.Tests/SaveGames/TestSavesTests.cs`):
1. `TheThirteenPresets_AreListed_InTheOrderOfTheTable` - red before the preset exists, rewritten by derivation (its name says 13 since g4a made it 14: renamed, 15 entries);
2. `ABuiltPreset_IsAcceptedByTheRealRules`, `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` - the three theories of `EveryPresetName`: one data row more each (red before: the preset is
   unknown; green after).
New: one Fact for the preset (like `TheBarricadePreset_...`). Not moved: `PresetNames` (the story presets), `WeaponNames` (the preset carries no weapon rows), the tests of the weapon loadouts. Test counts: +3 theory rows, +1 Fact.
To watch in T1/T3 (outside the preset list, not predicted as moving but they press the Square near a liftable or a break): `Ai/Objects/LiftableBreakWorldTests`, `Ai/Objects/BreakRun`, `Arcs/LiftMap22ArcTests`,
`Cells/TileEffectCutWorldTests`, `Hero/CarryGuardTests`, `Hero/CarryThrowWorldTests`, `Hero/FlailBounceTests` (they contain both "Square" and a crate/jar/grass word); the plan's stop "a test outside the predicted list moves" applies.

### What was run

- Oracle (MIPS machine, `ALUN_CD.EXE` France): `c3_contact_ticks.py` (4 s), `air_ticks.py`, `og33_square.py` (11 s), `oracle_release.py`; the replay of `a3_routes.py` R7 from a copy equals `e20-g3/bin/out/a3_routes.txt`.
- DLL: `dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=<skipcopy.targets> --filter FullyQualifiedName~G1bT0ProbeTests --blame-hang-timeout 60s`: 10 tests, 0 failure (they assert nothing); then the release probe: 2 tests.
  The probe classes were deleted afterwards: `git status` shows no change under `Alundra/` or `Alundra.Tests/`.

## Questions for the author (game terms)

1. **The release of the walk.** In the original, when you let go of the direction the hero still slides a half step (about 1.2 px) before stopping; in this port he stops dead. Consequence for lifting: in the original, if you push a crate,
   let go, and press Square on the very next tick, you still lift it; in the port you would get the sword (a tick, 1/50 s, not perceptible by hand but different from the original). Options: (A) fix the half step of the walk release in
   the hero's walk first (a separate slice, it changes every stop of the hero by 1.2 px and will probably move the traces and arcs that pin the stops), then run g1b; (B) accept and write down the one-tick gap (the plan says a difference is a stop and
   "the expected values never change to follow the DLL", so this needs your decision); (C) skip the lines B, D, D2 from the comparison. Recommendation: A, as its own slice, because it is a faithful-port defect of the walk, not of the lift.
2. **The tick after you land on the top of a crate and push into the next one (O-G3-3).** The port sets the contact one tick earlier than the original, so a Square on that exact tick would lift the neighbouring crate where the original
   swings the sword. Options: (A) delay the contact by one tick in that situation (a change of the movement obstacle probe, g3's area, which g3 consigned as a known gap, D-E19-58 family); (B) accept and consign it (the plan makes it a
   stop, so your decision). Recommendation: A only if (1) is done, since both are "the contact follows the force of the tick".
3. **The recipe map**: map 72 (Coastal Cave) is proposed; a pure east-west shuttle platform, jars and a barrel on the same bank, no lock. If you prefer a map where the platform links the jars to a goal the hero reaches by walking,
   say so; the arc (m2) will tell if the walk from the arrival to the platform with a jar is feasible.

## T1 - Phase A (2026-10-24): `TryLift` and `PlayerTryAction` as pure functions, DONE

Start HEAD `8fde4404`. Production does not change: the stub `PlayerTryAction(player, pad)` (`Alundra/Hero/PlayerManager.cs`, called after `TryUseItem`) still answers 0, nothing reads the new set, the climb state is untouched.

### Commits
1. `6fce52a8` `test(e20-g1b)`: the red tests (`Alundra.Tests/Hero/LiftPureTests.cs`, 37 cases) on a compiling skeleton (`TryLift` and `PlayerTryActionCore` answering 0, `ProductionWiredLiftableTypes`, `WiredLiftableTypesForTests`, `WiredLiftableTypes`). Run before the code: **23 red, 14 green** (the 14 are the "nothing happens" cases that a skeleton answering 0 satisfies; each is next to a positive case that is red). Red: the TryLift eight cases (genre 1 with and without the object, genres 2 and 3 with it), the target animation in the four ground/air x head/not-head cases, the offset on three axes, the wired set (genres 1 to 3 in the set), carried (ground, air), contact and front, contact without front to the loop, the loop (no button, air, status 3, blocked, first lift wins, genre 2 with `0x3B`), the hero without a logic entity.
2. `af05ff2d` `feat(e20-g1b)`: the bodies. `TryLift(hero, contact, ownsPowerGlove, wiredTypes)` and `PlayerTryActionCore(hero, pad, entities, ownsPowerGlove, wiredTypes)` (internal static, explicit context, D-G-6); `ProductionWiredLiftableTypes` created EMPTY (model of `ProductionWiredWeaponTypes`), `WiredLiftableTypesForTests` (ThreadStatic) and `WiredLiftableTypes`. The 37 tests are green.

### Decisions
- The genre is `EntityFlags.GetPickupKind(contact.Flags)` (bits `0x600`); genre 0 or a type outside the set gives 0 (the same 0 for both); genres 2 and 3 give 0 without `ownsPowerGlove` (the sense of the binary, `0x8002EDF8`); a lift is `PutInHands` and 1.
- `PlayerTryActionCore` order as `0x8002EEAC`: carried -> state `0x0C`/`0x30` and 1; `XCollisionEntity` set and `ButtonsJustPressed & Square` -> the answer of `TryLift`, even 0, the loop not run; otherwise the loop over the entities with no button: status 2 or 3, `BlockedByEntity` (`+0x20`) null, `RidingEntity` (`+0x12C`) the hero's `LogicContextEntity`; the first non-zero wins. A hero with a null `LogicContextEntity` returns 0 before the loop (the "null equals null" trap). The count `[0x80134360]` of the binary is the length of the list passed by the caller (phase B passes `IEntityWorldContext.SpawnedEntities`).
- Expected values come from the plan's binary facts (g1a report 1.1 and 1.2), never from the DLL output. `ownsPowerGlove` and the set are explicit parameters; phase B computes the first from `GetNumberOfItem(state, 0x3B)`.

### Results
- Suite `Alundra.Tests`, Release, `skipcopy.targets`, partitions disjoint by full namespace with the final dot (1 = Arcs Ai Audio Cameras Cells Combat Dialogues; 2 = Entities Events ExportGuards Hero Items; 3 = the exact negation of the 12), `--blame-hang-timeout 60s`: **1 322 + 2 916 + 1 830 = 6 068 tests, 0 failure** (the previous count 6 031 + the 37 new). Debug not built (the main session does it at the end of the slice).
- `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` rc 0 (line endings only), restored by `git checkout --`.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning, `TextBoxGpu.cs`).

### Gaps
None found in phase A. The T0 stop (Q-G1B-1, Q-G1B-2) still blocks T2 to T4; phase B (T3) is not started.

## T0b - T0 (b) replayed on the montage aligned on the real sheet (2026-10-24): DONE, no stop

Plan: section 1.24.4 "Correction (2026-10-24)". The reading of the release (`walk-release-disc/`) found that the T0 probe montage (`CarryRig`, by `JumpNpcRig.HeroAttackClock`) gave the idle animation (0) the
acceleration byte 0, where the real hero sheet has 1 (the Idle of `RunMontage`, `Alundra.Tests/Support/RunMontage.cs:65`).

**Correction of the T0 text above ("in the port he stops dead", "Dans la DLL la force tombe a 0", question 1 of the T0 stop):** it was the montage that stopped dead, not the game. With the idle at the byte 1 the DLL makes the binary's
half step at the release (see below). The lines B, D, D2 were never a difference of the game.

### What was run (probes temporary, never committed)
The two T0 probe classes (`t0/probes/*.cs.txt`) copied as `Alundra.Tests/Probes/*.cs`, with ONE line added right after `CarryRig.Build` (`rig.Rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 1 })`; the files with
the line are `t0b/probes/*.cs.txt`), run in Release with the neutralised copy (`--filter FullyQualifiedName~G1bT0ProbeTests`: 10 tests, 0 failure; then `G1bT0ReleaseProbeTests`: 2 tests, 0 failure; they assert nothing),
then the copies removed (`git status`: no change under `Alundra/` or `Alundra.Tests/`). Outputs: `t0b/probes/dll-probe-b.txt`, `t0b/probes/dll-release-probe-b.txt`. The oracle outputs are those of T0 (`t0/out/`,
`../e20-g1a/critic-scenarios/out/c3-contact-ticks.txt`, unchanged). The comparison: `t0b/compare_contacts_b.py` (the T0 script reading `dll-probe-b.txt`), output `t0b/out/compare-contacts-b.txt`, exit code 0.

### Result (contact per tick at 1, 2 and 3 ticks per frame)
| Line | Ticks compared | T0 | T0b |
|---|---|---|---|
| A, C, E, E2, F, G | as the plan | equal | equal |
| **B** (Square t11) | t1..t10 | different at t10 | **equal** |
| **D** (Square t15) | t1..t16 | different at t10 | **equal** |
| **D2** (Square t16) | t1..t15 | different at t10 | **equal** |
| air (b1), t4..t9 (contact, z, animation) | | equal | equal |

All 27 ground comparisons and the 3 air comparisons are equal. At t10 of the line B the DLL has the contact of the crate, `fx` 1.2188, `adj` 1 (the binary: the same), and the contact clears at t11 (`xcoll=-`), where a Square at t11 lifts in the oracle (line B).
The release without crate (`dll-release-probe-b.txt`, "in the open"): t10 x 121.9375, `fx` 1.2188, equal to the oracle (`t0/out/oracle-release-open-vs-crate.txt`) line by line (was 120.7188 and 0 with the idle at 0).
The release against the crate: x, `fx`, contact and animation equal to the oracle at every tick t1..t13; **one remark, not a difference of the compared field**: `adj` (`ForceAdjusted`) is 1 at t6 in the DLL and 0 in the oracle (the oracle
shows 1 from t7); the contact is the same at t5 and t6, `adj` is not compared by the plan and not read by `PlayerTryAction` (it reads `XCollisionEntity`). Same in T0 (`t0/probes/dll-release-probe.txt`).

### O-G3-3 on the aligned montage
The case is a map 392 arc (`ArcRun`, `RealController`), whose hero is the real one, not a `CarryRig`: the probe `B3_OG33_FromTheTopOfRec5_ContactPerTick` is unchanged by the line and was replayed in the same run: **identical to T0, line by line**
(`dll-probe-b.txt`, section b3: tick 1 y 296.8125 contact nil, **tick 2 y 297.0 contact rec10**, `ForceAdjusted` from tick 3), against the binary (`t0/out/og33-square.txt`: t38 contact nil, t39 contact rec10). So O-G3-3 stays the gap of one tick
consigned by D-E20-77 (the contact is set one tick earlier in the DLL; a Square on that tick would lift rec10 where the binary swings the sword); to be pinned by a named test, the code unchanged (engine slice later). It does not depend on the montage.

### Consequence for the plan
T0 (b2) holds: no stop remains. The expected values are not changed. T2 to T4 may start; every `CarryRig` test of the lifting (m1, m7 and the others) must run on the idle at the byte 1, i.e. the line is to be put in `CarryRig.Build` (the one line of
`walk-release-disc/report-main.md` section 5, 0 existing test moved according to that reading, to be re-measured when it is done) - not done here (no code commit in T0b).

## T2 - the world tests (m1) to (m8), RED before T3 (2026-10-24): DONE

Plan: section 1.24.4 "E20.g1b", task T2. Start HEAD `86d421b5`. Branch of the parent `chantier/theme1-porter-plateforme`. Production unchanged except the preset `porter-m72` (`Alundra/SaveGames/TestSaves.cs`): the stub `PlayerTryAction` still answers 0 and `ProductionWiredLiftableTypes` is still empty.

### What was written (every expected value is the binary's, from the oracle files named in each class comment, never the DLL's)
| File | Tests | Content |
|---|---|---|
| `Alundra.Tests/Hero/CarryRig.cs` | 0 | ONE line in `Build`: the idle of the real sheet at the acceleration byte 1 (`walk-release-disc/report-main.md` section 5). **0 existing test moved.** |
| `Alundra.Tests/Hero/LiftWorldTests.cs` | 39 | (m1) the 9 lines A, B, C, D, D2, E, E2, F, G of `c3` x 1, 2, 3 ticks per frame = 27; the contact at t5 and the release keeping it one tick (B) x 3; (m7) the air, Square at t6, t7, t8 (3); (m4) the object on the head (1); (m5) the object of the family C4 gives the sword next to the crate that lifts (1); (m6) the person talks, the crate lifts (1); (m8) the climb state at z 6 and 14 with the contact lifts (2), without the contact lifts nothing (1). |
| `Alundra.Tests/Arcs/LiftArcTests.cs` | 3 | (g1) the release of the walk on the real sheet (x moves 20.71875 in 9 ticks, 21.9375 at the release tick, `ForceX` 1.21875, then 0); (m3) the very heavy stone (map 11, record 27, sprite 234, genre 3) lifted with the object `0x3B`, the sword without it. |
| `Alundra.Tests/Arcs/LiftMap72ArcTests.cs` | 2 | (m2) the arc of the recipe from the start of `porter-m72`; (m5) the barrel of the map 72 (C4, outside the set) gives the sword, then the jar of the same scene lifts. |
| `Alundra.Tests/Physics/CrateSolidityMap392Tests.cs` | 1 | (g2) `OG33_FromTheTopOfRec5_TheDllSetsTheContactOfRec10OneTickBeforeTheBinary_PinnedNotFollowed`: the DLL's rows (tick 2: y 297.0 and contact rec10), the binary's value beside them (t38: no contact; t39: rec10), the reference to O-G3-3 / D-E20-77 in the comment. |
| `Alundra/SaveGames/TestSaves.cs`, `Alundra.Tests/SaveGames/TestSavesTests.cs`, `docs/test-saves.md` | +4 | the preset `porter-m72` (map 72, tile (10, 12), level 3, the new game's kit, no flag); the list of names (15), the 3 theories of `EveryPresetName` gain a row, a Fact for the preset; `TheThirteenPresets_...` renamed `TheFifteenPresets_...` (derived from the list). |

### Red before T3, green after (the witnesses are green in both states)
The reference implementation of T3 used to prove that the tests can go green is `t2/t3-reference.patch` (applied to a working copy, NEVER committed; it is not the activation: the climb call and the world access are crude). With it every test below is green. Without it (the committed state):
- RED (25): `LiftWorldTests.M1_...` for the lines A, B, C, D2, E2 (a lift expected) x 3 cadences = 15; `M4_...`; `M5_...`; `M6_...`; `M7_...` for the Square at t6 and t7; `M8_...Lifts` at 6 and 14 px; `LiftArcTests.M3_...IsLifted...3B`; `LiftMap72ArcTests.M2_...` and `M5_...` (they fail at the first lift: the contact, the Square and the sword before it are green).
- GREEN, the witnesses of the sword and of the stub: `M1_...` for the lines D, E, F, G (x 3); `M1_TheHeroReachesTheCrate...` x 3; `M7_...` for the Square at t8; `M8_...WithoutTheContact...`; `LiftArcTests.G1_...` and `M3_...Without...3B` (the sword, animation 16); `OG33_...`; the preset tests.

### Suite (Release, `skipcopy.targets`, partitions disjoint by full namespace with the final dot, `--blame-hang-timeout 60s`), committed state
- Partition 1 (Arcs Ai Audio Cameras Cells Combat Dialogues): 1327 tests, 3 failures (M2, M5 of the map 72, M3 with the object).
- Partition 2 (Entities Events ExportGuards Hero Items): 2955 tests, 22 failures (all in `LiftWorldTests`).
- Partition 3 (the exact negation of the two): 1836 tests, 0 failure.
- Total 6118 (6068 of T1 + 50 new); 25 failures, all named above; every other test, the witnesses (the arcs of the levers 115, 116, 117 and 44, A5, A5r, the arcs of g4a, the chain) included, is green.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding.
- Count note: the new tests identified are 49 (39 + 3 + 2 + 1 + 4); the 50th of the difference with T1 (6118 against 6068) is not attributed (the three partitions of T1 gave 1322 + 2916 + 1830).

### Existing tests moved, against the predicted list of T0 (c)
Predicted: `TestSavesTests` (the list of names, the 3 theories of `EveryPresetName` one row more, a Fact for the preset, the rename of `TheThirteenPresets_...`). Actual: exactly those, and no other: aligning `CarryRig` moved no test (the files that mix the Square and a liftable are green). `CrateSolidityMap392Tests.cs` only gained the new Fact (g2).

### Findings of the recipe arc on the map 72 (read before the recipe of the author, and before T4)
1. **The arc plays on the real world, with one teleport.** From the start of the preset (tile (10, 12), level 3 = (252, 200, 48)), on foot: the jar of the record 37 (204, 200) is flush at x = 225.0, the Square lifts it, the gesture of 36 ticks ends in the carrying stand 12, the walk west carries it to the barrel of the record 1 (flush at x = 201.0), Cross + Left lands on the barrel top (z 64, `RidingEntity` the barrel).
2. **The platform cannot be boarded on foot from the west bank, by the data and by the probe.** The platform (record 2, (576, 240), z 64, top 72) shuttles 96 px east and 96 px west, for ever, 1.5 px per tick (128 ticks per cycle, at its west end x = 576 at the ticks 0, 128, 256...). Its west end touches the peninsula (cells 17 to 22 of row 16 and 21, 22 of row 15, height 5 = z 80), but every way up to it from the bank (z 48), the lagoon floor (z 16, walkable; the platform floats 48 px over it) or the east bank is a rise of 32 px, and the jump rises 27.5 px (`t2/out/explore-peninsula-from-bank.txt`: stopped at x = 397.0, the apex z 75.5 against the 80 of the cell). A reachability over the cells with a rise limit of one height unit (16 px) stays in the west area; with two units (32 px) the peninsula, the east bank and the north plateau join. So the recipe step "(4) get on the platform" is **not feasible on foot from `porter-m72`** with the DLL as it is: the arc puts the hero on the peninsula (540, 248) with the jar over his head (the one teleport; the bench of the arc puts the carried object with him), waits for the platform at its west end, walks east onto it (it lands on the top at z 72, `RidingEntity` the platform) and rides: the hero and the jar follow the platform to the pixel. Whether the original lets one reach the peninsula some other way (stairs of the slope bytes, a sequence of the map events) is NOT established here: it is a question for the plan (the recipe of the author, step 4) and for a reading of the map, not something this task decides.
3. The jar thrown from the platform (Square in the carrying stand): the hero takes the throw state 9, the jar flies east, lands on the lagoon floor (x about 709, z 16) and breaks (status 4 then 0, 20 to 60 frames after the throw).
4. An `Arrival` is required for a map 72 arc: the entities spawn from the zone that contains the tile of the hero at the load; with the tile of the `ArcSpec` only (the new game's tile at the load) the barrel, the platform and the jars 37 and 38 are not even created. The arc starts from the preset's tile as a portal arrival, which is what the game does with the save.

### Differences from the plan text
- (m2) includes the teleport above (finding 2); everything else of the recipe is played on foot.
- (m3) is played on the map 11 (the Debug zone, record 27 at (996, 808, z 16), flat ground around), one of the four maps the plan allows (3, 4, 7, 11).
- (m6) uses the contact word as the one poke of the scene (as the oracle `c4_climb_lift` does for the climb): the contact of a person cannot be produced by the pad in the bench; the talk is read from the latch of the interaction (`GameState.InteractLatchEntity`) because the programs pass of the same tick releases `ActiveCollisionEntity`.
- (m8): the bench has no ladder; the hero is put in the climb state (0x0E) with his feet off the ground (z 6 and 14 px over a floor at 0, `IsOnGround` 0) and the contact poked at the Square tick, as the oracle's rows A and C do. The row B ("same Square without the contact") is asserted as "nothing lifted" only: the climb itself cannot go on without ladder cells.
- `t2/probes/Map72ExploreProbe.cs.txt` (a temporary exploration test driven by a pad script) and `explore-recipe-script.txt` are kept for the record; they are not tests.

## T3 - Phase B, the activation (2026-10-24): DONE

Start HEAD `42cd834a`. Branch `chantier/theme1-porter-plateforme`.

### Commits
1. `e151f997` `feat(e20-g1b)`: the three lines together in `Alundra/Hero/PlayerManager.cs` (the body of the stub `PlayerTryAction` replaced by `PlayerTryActionCore(player, pad, IEntityWorldContext.SpawnedEntities, GetNumberOfItem(state, 0x3B) != 0, WiredLiftableTypes)`; the climb state (0x0E, 0x35) calls `TryUseItem` then `PlayerTryAction`, a non-zero retval ends the tick; `ProductionWiredLiftableTypes` = `LiftableAi.SpriteTypes`, the seven C23 types 234, 291, 292, 294, 297, 304, 438). The T1 pin `TheProductionSet_IsEmpty_...` became `TheProductionSet_IsTheSevenTypesOfC23_...` (it asserted the phase A state by design).
2. test fix `(m8)`: the T2 version of `M8_...Lifts` set the climb state BEFORE two UP ticks; the bench has no ladder, so the hero left the climb state (animation 44) and the lift went through the air-state call: the test stayed green with the climb calls retired (found by the removal proof). The state is now put right before the Square tick. Green with the real code, red (2 rows) with the climb calls retired.

The reference patch `t2/t3-reference.patch` was applied then refined (comments, the set read from `LiftableAi.SpriteTypes`).

### Tests (Release, neutralised copy, partitions disjoint by full namespace with the final dot, `--blame-hang-timeout 60s`)
- (m1) to (m8) (25 test rows red in T2) are green: partition 1 = 1327, partition 2 = 2955, partition 3 (exact negation) = 1835: **6117 tests, 0 failure**; Debug (last, with the copy to `alundra-project`): 1327 + 2955 + 1835, 0 failure. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `alundra-project/Alundra.dll`: equal.
- Count: T2 reported 6118 (partition 3 = 1836); the three partitions and `--list-tests` now give 6117 = 6068 + 49 new tests identified; the T2 figure was a one-off miscount of partition 3 (nothing else changed in T3 but test renaming; no test added or removed).
- `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` rc 0 (line endings only), restored. `check_names.py --rules`: 0 finding. Engine, converter, data: no diff from `5eccf614` (`git diff --stat 5eccf614 HEAD` on `CasaEngineMonogame`, `alundra-datas-analyser`, `data-extracted`, `alundra-project`, `tools`: empty).
- Existing tests moved by T3: only `LiftPureTests.TheProductionSet_...` (renamed, its assertion is the point of phase B) and the T2 `M8` setup. Predicted list of T0 (c) (`TestSavesTests`) moved in T2 only.

### Removal proofs (real mutations applied by `t3/mut.py`, run by `t3/run.sh`, undone by `git checkout`; outputs `t3/out/`)
- Empty `ProductionWiredLiftableTypes` (`empty`): 26 red = the 25 rows of (m1) to (m8) of T2 (partition 1: `LiftArcTests.M3`, `LiftMap72ArcTests.M2`, `M5`; partition 2: 22 rows of `LiftWorldTests` M1, M4, M5, M6, M7, M8) **plus** the pin of the production set (`LiftPureTests.TheProductionSet_IsTheSevenTypesOfC23_...`, which asserts the set itself, by construction); partition 3: 0.
- Stub back (`stub`, the body returns 0, the set kept): exactly the 25 rows (the same names as above, without the set pin); partition 3: 0.
- Climb calls retired (`climb`, after the m8 fix): exactly the 2 rows of `M8_...Lifts_AnimationElevenInTheAir` (6 and 14 px); partitions 1 and 3: 0. (Before the m8 fix this mutation turned nothing red: the finding above.) The mutations 1 and 2 were run before the m8 fix; m8 was red in both (no lift without the set or the body).

### Gaps from the plan
- The pin of the production set is red with the empty set too (the plan says "exactly (m1) to (m8)"): by construction of that pin; named here.
- The m8 test was changed in T3 (above).
