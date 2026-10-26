# E20.g2c2 - the torches and the brambles: T0 results (2026-10-26)

Plan: `docs/plan-e20-heros.md` section 1.24.5 "E20.g2c2" (revision 2, READY). Branch of the parent `chantier/theme2-attaques-objets`, start HEAD `6b704287` (E20.h3a done). Binary: `ALUN_CD.EXE` (France).
T0 is read-only for the product: **no code was committed**. The throwaway port (`probes/TempTorch.cs.txt`), the throwaway probe tests and the temporary wiring (`temp-wiring.diff.txt`) were applied, measured, then removed
(`git status` clean on `Alundra/` and `Alundra.Tests/`; the suite subset that moved is green again after the removal, 327 cases).

## Verdict

**STOP on one point (P1, the numbering of the rain of the map 92 on the production path), everything else equals the binary.** Details in "P1" and "Stop". The count of moved tests is 9 (limit 10): no stop on that.

| Item | Result |
|---|---|
| (a) prerequisites | g1b, g2c1 (plan 1.24.4 / 1.24.5, "faite et CONFIRMED"), c2b (1.12.1 done and CONFIRMED), f6b (1.22.2 done and CONFIRMED, ADR-0098); tree clean on `Alundra/`, `Alundra.Tests/`, `docs/plan-e20-annexe/` (only the author's untracked `tools/` files and the two modified submodule pointers, untouched) |
| (b) deposit | `docs/plan-e20-annexe/e20-g2c2-disc/` (`g2c2/`, `critic/`, README, SHA1SUMS.txt; 69 files, no `__pycache__`, no `work/` copies) |
| (c) replay | identical (table below); 90 vectors of C70, E20, D34 on the machine, replayed by models with zero difference |
| (d) probes | P1 to P5 below |
| (e) cell | `torche-m92`: map 92, tile (34, 11), level 5 |
| (f) predicted list | 9 existing tests move (table below) |

## (c) Replay (from a copy of the scripts, `work/` = `docs/plan-e20-annexe/e20-g1a/work/` unchanged, `diff -rq` empty)

| Script | Outcome |
|---|---|
| `c4_scen.py s8_torch` | `cmp` identical to `g2c2/replay/out/s8_torch.txt` and to the critic's (`replay/s8_torch.txt`): torch moving: t61 throw, t84 status 3 + sound 142, t87 flame born, t88 torch freed + sound 41, flame at x = 178.0; torch still: flame at 143.5 |
| `f1_flame.py` (both variants) | `cmp` identical to `g2c2/out/replay_f1_flame.txt` (zero: flame t87..t125 = 39 ticks, codes all 0, no effect 17; nozero: effect 17 every 4 ticks, 76 ticks); the critic's script identical to `critic/out/replay_f1_flame_critic.txt` |
| `p_hitswitch.py`, `p_rain.py`, `p_selfhit.py` | `cmp` identical to `critic/out/p_*.txt` (`replay/p_*.txt`) |

### The vectors (`vectors/`)

`my_goldens_g2c2.py` (from a copy of `e20-h3/critic/run`, the rig and the method of `my_goldens_e19.py` of e20-h3a): **30 structured vectors each of C70 (`0x8007B7B0`), E20 (`0x8007F6C8`), D34 (`0x8007E8AC`)**, produced on the REAL code in the interpreter; every
data word READ is recorded (328 words in all), every word WRITTEN (188), the journal of the stubbed calls (spawn, destroy, sound). Coverage by branch: C70 (not carried; carried without code; codes -1, 1, 2, 3 and 4 to 7 = the words around the table;
carrier = the hero or another entity); E20 (animation 2 and `+0xAC` 1 with `+0x274` 0, 1, 5, 0x100, 0xAB; null spawn = the binary writes through a null pointer; anim/reset not both; the contact of type 0x187 and of other types, with `+0x274` byte write `sb`);
the flame's entity with SIX non-zero codes (`k % 3 != 2` random non-zero, others `1 + random`) so that both ends of the clearing loop (`+0x70`, `+0x84`) are visible; D34 (nibble 6 with the high nibble 0, 8, 9, F, 6; nibbles 0 to 15 apart from 6; the flags 0xC3, 0x47, 0x80...).
`replay_g2c2.py` replays them through models written from the disassembly, reading only the words the vector lists: **0 difference** (`vectors/replay_g2c2.out.txt`); sanity: five one-word mutants of the model (`| 0x30`, five codes, nibble 5, `| 0x42`, type 0x188) give 12, 3, 14, 8 and 7 differences. The vectors carry the predicted values of the tests (a1) to (a3) of T1.

## (d) Probes of the DLL (throwaway test, `probes/G2c2T0ProbeTests.cs.txt`; outputs `probes/out/`)

Bench: the real world of the map 92 (`ArcRun`, real prefabs and controllers) on the synthetic flat field, every entity but the hero removed, native handlers off but for the types 393 and 12 (`NativeSpriteTypeFilterForTests`), a throwaway port of C70, E20 and D34
(`probes/TempTorch.cs.txt`: E20 with `SpawnEntityBySpriteIndex(self, false, 12, X, Y, Z, dir)` at the Z of the DLL, the six codes cleared, animation 3, `(flags & ~0x10) | 2`, then `DestroyEntity(self, -1)`; `+0x274` through `Bytes[0]`), 393 added to the wired set.

| Probe | Compared with | Result |
|---|---|---|
| P5 thrown torch (Square at tick 1, still and walking, 1, 2 and 3 ticks per frame) | `s8_torch` (landing T+22, status 3 T+23, flame T+26, torch freed T+27, sounds 142 and 41, flame x 143.5 / 178.0) | **equal at the 4 cadences**: landing t23 (T+22), Deactivated + animation 2 + flags `0x332346` + sound 142 at t24, FlagToDestroy + flame born at t27 (codes 0/0/0/0/0/0, animation 3, flags `0x132146`, x 143.5 still / 178.0 walking), destroyed + sound 41 at t28 |
| P2 carried torch, 100 ticks, 1 and 3 ticks per frame | `p_selfhit` (HitCounter 0, `+0x224` 0, the hero never touched) | **equal**: torch HitCounter 0, the hero touches nothing, hp 10, torch status Normal, 0 sparks (the frame box exists: `HasFrameBox` true, the plate does not reach the hero) |
| P1 rain, record 68 of the map 92 (the production `0x8A` path, `SpawnEntityByRecordId`), `Flags \|= 0x10` (the `0x62 [98, 68, 16, 0]`) at the birth, 1, 2 and 3 ticks per frame; control without the bit | `p_rain` (landing t15, status 3 + animation 2 t16, flame born / torch status 4 t19, torch freed t20) and "without the bit the torch stays inert" | **D-G2C2-4 holds**: the entity takes the status 3 at the ground (animation 2, flags `0x3323C2`, sound 142, flame born with codes 0, animation 3, `0x132146`, at the landing point, sound 41); without the bit nothing happens for 60 ticks. **But one tick earlier than the binary** after the landing at 1 and 2 ticks per frame (equal at 3), see below |
| P1b the same rain, the torch born by `SpawnEntityBySpriteIndex` (the marker `HasLogicalZ`) | the same | **equal to `p_rain`**: landing t15, Deactivated t16, flame t19, freed t20 (1 and 3 ticks per frame) |
| P3 loot of a bramble (the types 238 and 239, `Contents` 255), 300 seeds of the generator, `DestroyEntity(bramble, -1)` | (no oracle: the globals are not in the map JSON) | the draw at birth gives `ContentsItemId` 0 in 263 of 300 cases, and 69, 70, 71, 84, 85 otherwise; at the destruction 263 of 300 produce nothing and 37 produce one entity of the sprite types 99, 100, 101, 114 or 115 (the e0c pickups): the existing path of `DestroyEntity`, no code to write |
| P4 hero carrying a torch that is destroyed (`DestroyEntity(torch, -1)` at the head of tick 11, 30 ticks) | (no oracle: C71, g2c3) | the carrier link is cleared at once (`CarriedEntity` null), the hero asks the animation 0 the same tick and validates it the next one, `CarryLockCounter` stays 0, direction kept, hp 10; **no stuck posture** (the sonde that g2c3 needs) |

### P1, the one point that does not line up (STOP)

Binary (`replay/p_rain.txt`): the torch (bit 0x10 posed at t4 at z = 55) is at ground = 1 and status 2 at t15, **status 3 + animation 2 at t16**, flame born and torch status 4 at t19, torch freed at t20.
DLL, the production `0x8A` path (`SpawnEntityByRecordId`, no marker, the prefab's controller drives the fall; `probes/out/p1_rain_{1,2,3}_bit.txt`): ground = 1 **and** status Deactivated + animation 2 + sound 142 at t15 (the tick of the landing),
flame born / torch FlagToDestroy at t18, torch freed + sound 41 at t19: **one tick earlier than the binary** at every step after the landing (15 / 15 / 18 / 19 against 15 / 16 / 19 / 20) at 1 and 2 ticks per frame; at 3 ticks per frame the DLL gives 15 / 16 / 19 / 20, equal to the binary (the status read at the end of the frame that holds the landing).
The same torch born by `SpawnEntityBySpriteIndex` (the marker `HasLogicalZ`, which is also the state of the thrown torch) gives exactly 15 / 16 / 19 / 20. So the shift is the sampling of the ground by the controller path of an entity without the marker (the pick of the state reads `IsOnGround` of the same tick),
which is the path of every entity born by the opcodes `0x2D`, `0x8A` and `0x8B`; it is not specific to the torch and no code of g2c2 can be at fault (C70 and E20 are not involved before the landing).
By the letter of the rule (a probe that differs from the binary at a listed tick) this is a stop. What it changes in the plan: (w5) "the rain of the lever: four flames on the ground" does not pin ticks, so it is not contradicted; the tick-exact pins of (w1) (thrown) are equal.
Recommendation: keep D-G2C2-4 and (w5) as written, record the one-tick advance of the rain flames as a documented deviation of the record-born path (engine / controller sampling, out of the scope of g2c2), and write (w5) without the tick of the status 3 (it asserts four flames born on the ground, each at its landing).

## (e) The cell of the preset `torche-m92`

Map 92 (Magyscar), tile **(34, 11), level 5**, no flag, no map table entry, no item count, the new game's kit: the cells (30..40, 10..11) of `data-extracted/data/map_92.json` are walkable at Height 5 (rows 10 and 11), the four torches of the group of the switches 39 to 42
(records 43 to 46) stand on the row 10 at the tiles x = 31, 33, 35, 37 (the hero stands between the torches of the tiles 33 and 35, one tile south of them, on a ledge 4 levels above the room of the four switches at the tiles (31, 15), (34, 14), (34, 17), (37, 15), Height 1).
Slot `test-torche-m92` (15 characters). Recipe: lift a torch (Square), throw it south onto a switch; then the lever (record 14, tile (2, 19)) drops its four torches on the east room. The recipe for the brambles is the save `tools/Saves/160-magyscar.sav` (map 86).

## (f) Predicted list of the existing tests that move (temporary wiring, then undone)

Temporary wiring (`temp-wiring.diff.txt`, `probes/TempTorch.cs.txt`): `(C, 70)`, `(E, 20)` for {393} and `(D, 34)` for {238, 239} registered in `CreateForProduction` (**21 keys become 24**), 393 added to `ProductionWiredLiftableTypes` (**25 types become 26**), the preset `torche-m92` added at the end of the table (27 presets become 28).
Suites in Release with the neutralised copy, partitions disjoint by whole namespace with the final dot, `--blame-hang-timeout 60s` (summaries and red lists in `runs/`):

| Partition | Tests | Red |
|---|---|---|
| `Ai.` | 1030 | 5 |
| `Hero.Hero*` | 1299 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 2 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 1 |
| everything else (negation of the three above) | 2159 | 0 |
| `SaveGames.` (with the preset added) | 412 | 1 |

The 9 red tests (all green at the start HEAD: the first four partitions ran without the preset, the preset added only for the last run):
1. `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (count 21 -> 24)
2. `Alundra.Tests.Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (21 -> 24)
3. `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (21 -> 24)
4. `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (the tuple 21 -> 24)
5. `Alundra.Tests.Ai.Projectiles.BallFlamesTests.M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys` (21 -> 24) - **the fifth sum** the plan's text calls "four"
6. `Alundra.Tests.Hero.LiftPureTests.TheProductionSet_IsTheTwentyFiveTypesOfC23C4AndTheShopArticles_AndTheTestSeamReplacesIt` (the list of 25 becomes 26)
7. `Alundra.Tests.Hero.LiftWorldTests.M5_AnObjectOutsideTheSet_GivesTheSword_NextToTheCrateOfTheSameSceneThatLifts` (the witness on the type 393)
8. `Alundra.Tests.Arcs.LiftMap72ArcTests.M5_AnObjectOutsideTheSet_GivesTheSword_TheJarOfTheSameSceneLifts` (the witness on the type 393)
9. `Alundra.Tests.SaveGames.TestSavesTests.TheTwentySevenPresets_AreListed_InTheOrderOfTheTable` (renamed `TheTwentyEightPresets_...`, `AllPresetNames` gains `torche-m92` at the end)

Not moves but rows that grow with the list (no body edit): `ABuiltPreset_IsAcceptedByTheRealRules`, `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule` (`EveryPresetName`: one row each), `TheOtherPresets_LeaveTheDeathCounterAtZero` (walks the list).
9 <= 10: no stop. The two witnesses (m5) of g1b are derived on the bomb (global 2, genre 1, no key, outside the set after g2c2). `C4RegistrationTests` and `C4WorldTests` (maps 86, 87, 93 with torches and drops) stay green. The suites rewrite the line ends of `docs/hero-trace-389-*.txt`
(checked with `git diff --ignore-cr-at-eol --quiet -- docs/`, no real change, then restored).

## (w8) The natives at rest

`native-torches-86-94.txt`: the 44 torches born at the load (sprite 137, direction 0xC0) on the maps 86 to 94 (86: 6, 87: 5, 88: 3, 89: 4, 90: 3, 92: 15, 93: 4, 94: 4); the 9 others are born by script (87 recs 54 to 56, 90 rec 4, 94 recs 16 to 19, 92 rec 68).

## Next free ADR number

Recounted on all the branches (`git ls-tree` of each): the highest is 0100 (E20.h3a); **0101 is free**.

## Stop

- Rule: "une sonde qui contredit un fait" -> P1: the rain of the lever on the production path (record-born, no marker) is one tick ahead of the binary after the landing at 1 and 2 ticks per frame (15/15/18/19 against 15/16/19/20; equal at 3 ticks per frame). Understood (the controller path samples the ground in the tick of the landing), identical to the binary when the torch carries the marker, without consequence for the design of g2c2 and for the tests as written except a tick-exact pin of the rain, which (w5) does not have. The main session decides whether (w5) stays untimed.
- 9 moved tests (<= 10); no need to touch the engine; no loop.

## State at the end of T0

`git status`: only the two modified submodule pointers (the author's), the untracked `tools/` files of the author, and this annex (`e20-g2c2-disc/`, `e20-g2c2/`), committed by the T0 commit. Nothing under `Alundra/` or `Alundra.Tests/`.
