# E20.b2, T0 (2026-10-25): deposit, oracle keys, probes of the DLL, predicted moved tests

Branch `chantier/theme2-attaques-objets` at `8174c0cb`. No change to `Alundra/`, the existing tests, the engine, the converter or the data: the probes
were a scratch test folder (`Alundra.Tests/ScratchB2/`, deleted after the run, never indexed; `git diff -- Alundra` empty afterwards).

## (a) Deposit

`docs/plan-e20-annexe/e20-b2-disc/`: the reading of 2026-10-24 for the object 008 (`report-main.md`, `critic-main.md`, the scripts and outputs of the
part B, the critic's scripts), 37 files and a `README.md`, with a `SHA1SUMS.txt`; without the copies of annexes (`c1b/`, `f4f6bin/`, `critic/w/c1b_oracle/`),
the part A files and every `__pycache__`. This folder (`e20-b2/`) has its own `SHA1SUMS.txt`.

## (b) The legend inventories in the c1b oracle (K1, K2)

`legend_keys.py` (this folder) imports the frozen oracle tools from a COPY (`docs/plan-e20-annexe/e20-c1b/oracle/` copied to the scratchpad), appends the
inventories `legend` (kit 1, 17, 25 + item 8, slot 1) and `legend_holy` (kit + Elnard 4 + item 8), and runs the real code of `ALUN_CD.EXE` (France) in
the oracle's interpreter: `K1-legend.txt/.json`, `K2-legend.txt/.json`, `control.txt`. The run: `python -B legend_keys.py <copy>/oracle/tools <out>`
(Python 3.14 with capstone 5.0.9 on `PYTHONPATH`). Result: PASS.

| fact | measured (both inventories, identical) |
|---|---|
| `GetItemIdFromCurrentWeapon` (`0x8004E030`) | 8 |
| equipment record [1] | item word 38 (= item 8 + 0x1E), AttackCount 1, `[(6, 50)]`, responses `0x40` |
| hero sheet | equal to the sheet of the kit and of Elnard (the defence does not change) |
| K1 against the independent model | records, sheet, boots, weapon, second call: EQUAL; no unwritten-byte read |
| K2 natural hit on a Beannoid of response `0x10` | HP 100 -> 49 (damage 51); HP 9 -> 0 (damage 9, returns 1) |
| K2 other lines (responses 0x00/0x40/0x80/0xC0, no equipment bit, nibble 4, A as attacker, hero as target) | 22 lines per inventory, all EQUAL to the model (44 EQUAL, 0 DIFFERENT) |

Control (the frozen inventories replayed in the same process): `kit` and `holy` give a K1 and a K2 equal to the frozen `K1.json` and `K2.json` of the c1b
annex (`control.txt`). The expected values for the T1 tests are therefore 51, 49, "dead", `[1] = item 8`, `[(6, 50)]`; they equal the reading's
table (`e20-b2-disc/c1b_legend.txt`).

## (c) Probes of the DLL (throwaway test, never committed; `probe-dll/B2ProbeTests.cs.txt`, outputs `probe-dll/probe*.txt`)

Release build with the neutralised copy, `ArcRun` world of the map 7 (`Overworld/Overworld 1,2-7`, real controller, prefabs), one logic tick per frame. The
counter is `GameState.Instance.DeathRetryCount`, set right after the arc is built.

1. **Geometry (`probeA`).** The `XPos`/`YPos`/`Height` of an entity record are in HALF-TILES: the "Blocs transparents" records 28 and 29 (written (50, 32) and
   (52, 32) in the plan) stand on the tiles **(25, 16) and (26, 16), level 7** (pixels (612, 264) and (636, 264), z 112); the record 31 is on the tile
   (2, 4, 0). The cells (24..27, 14..17) all have height 7: the hero arriving at the tile **(25, 17, 7)** stands on the ground at pixel (612, 280, 112)
   with the block straight north (16 px). Same for (26, 17, 7).
2. **How the scene starts (`probeD`).** Walking north into the block: with the Up held from the first frame the hero goes from y 280 to 279 and the program
   F[7] runs at tick 3 (`If flag on` @1820, `Goto by hero animation direction` @1825, `Flag on [0,128]` @1834, `End` @1837), B[10] leaves its wait
   (@1268) at tick 4 (pc 1271, 1272, 1273...). Pressing the Square alone, the hero facing north (arrival direction 16) and not walking: nothing runs in 20
   ticks. So the recipe of `legende-m7` is "walk north", no Square.
3. **`Result` at pc 1508 and 1510 of `B[10]` at the HEAD (`probeB`): 0.** The counter 0, 19, 20 and 21 give the same trace: `0xC2` @1508 is `UnknownSkipped`
   with `Result` 0 (the first record of the program, `0x36` Wait until flag on @1268, already shows 0: the state of the program starts at 0 and nothing before pc 1508 writes it); `0x03` @1510 does not jump (`Result` 0), the refusal runs: `Dialog [129]` @1513 (tick 926 after the start of the arc),
   `Disable effect`, `Destroy entity [31]` @1539 (which sets `Result` to 1: the copy exists), `Player gain control` @1562, `Goto` @1567, `End` @1663 at tick 1234.
   In every run `0x82` @1623 never runs, the item 8 count stays 0, the flag 1787 stays off, the player is locked from the walk until tick ~1227 and free
   after. The same with the arrival facing south and the Up held (`c19_walk`, `c20_walk`).
   **Consequence for T3 (the plan's own rule): `Result` measured at pc 1510 is 0, so the case that is red before T2 is the counter 20 (the DLL refuses
   although the original gives), and the guard (green before and after) is the counter 19, killed by the mutant "the flag 1787 set at the refusal".**
4. **The scene does not replay inside the same visit (`probeB`, round 2).** After `End` @1663 the program B[10] stays on that `End`: 640 ticks of a second
   round show only pc 1663; the flag `[0,128]` is set by F[7] @1834 and cleared by no program of the map 7 (the only users are B[10] @1268 and F[7] @1834,
   scan of every program of the map 7 for the opcodes 0x05, 0x06, 0x30, 0x31, 0x36 on `[0,128]`). A refusal therefore leaves the flag 1787 off (so F[7] @1820 does not jump to its `End` at the next visit) and the
   scene is played again at the next entry into the map, not in the same visit. **The plan's wording "à 19 ... la scène se rejoue" (T3, acceptance) cannot
   be observed inside one `ArcRun`: the observable of the guard at 19 is the state after the refusal (item 8 = 0, flag 1787 off, the dialogue 129 shown, no
   `0x82`), which "the flag 1787 set at the refusal" breaks.** To be settled by the plan (see the report).
5. **The record 31 of the map 7 (`probeB`, `probeC`).**
   - It is not spawned at the load (`SpriteDirection` 0: no automatic appearance bit `0x40`; the records 28, 29 have 64) and is not in the entity list at
     any time outside the scene.
   - The two `0x8A [31, ...]` of `B[10]` (pc 1411 at tick 581, pc 1483 at tick 914) make a copy at the tile (26, 18, 11), pixel (624, 300, 175), 63
     pixels above the hero and 21 away, `ProgramIndexes` = `[0,0,133,0,0,0]` (a script at the slot C: C[5], `Clear entities flags [128,0,1]`, `Fly [0,0]`, `End`),
     `SpriteProgramIndexes` = `[255,0,255,0,0,0]` (the sprite 38 has A255 and C255); the hero is LOCKED (`Player lose control` @1272) while the copy exists
     and the copy is destroyed (`0x2E [31]` @1539 or @1600) before the player gets control back (@1562) or before the item is given (@1623). The item 8 count
     stays 0 during the whole scene.
   - Forced into the world (`SpawnEntityByRecordId(31)` on a walk-free arrival, gravity off, then placed ON the hero): 90 ticks later the item 8 count is 0, no
     dialogue opened, the entity is unchanged (Normal, not collidable). The DLL's dispatch gives the hand to the map script when the slot's index `& 0x7F` is not 0
     (`EntityScriptProxy.RunPickedEvent`), so the native C255 that gives a pickup never runs for it.
   - **In the binary** (`dis_picked.py`, `dis_800386D0.txt`, capstone on `ALUN_CD.EXE`): the phase 2 of `0x800386D0` reads, for each entity whose trigger
     (`+0x228`) is not -1, `word[entity + 0x4C + 4 * slot] & 0x7F` (`0x800388FC-0x80038900`); if not zero it calls `0x8004205C` (the map script,
     `0x8003890C`), else it calls the handler of the sprite table at `+0x70` (`0x80038920-0x8003893C`). The record 31 has the index 133 at the slot C (&0x7F = 5), so the
     script C[5] runs and C255 does not. No other program of any map holds `0x82 [8]` (reading, `out_sources_item8.txt`).
   - **Outcome: (c1).** Not collectible in the DLL and not in the binary (the hero cannot even reach it: locked while it exists). T3 adds the guard "at the
     counter 19, touching the record 31 does not give the object 8" (forced spawn, placed on the hero, as `probeC`).

## (d) Predicted moved tests (D-B2-6)

The only existing test that changes: `Alundra.Tests/SaveGames/TestSavesTests.cs`, `TheTwentyTwoPresets_AreListed_InTheOrderOfTheTable`: its expected list gains
`legende-m14` and `legende-m7` (after the four chest presets) and its name follows the count (`TheTwentyFourPresets_AreListed_InTheOrderOfTheTable`). The
theory data `EveryPresetName` (built from the same array) gains two cases per theory that uses it: new cases, not moved tests. `rg` finds the
presets list in no other test. No other test is expected to move.

## Cells for the presets (T2, from (c) 1 and 2)

| preset | map | arrival tile (x, y, z) | notes |
|---|---|---|---|
| `legende-m14` | 14 | (8, 6, 0) | `WeaponPreset("legende-m14", 14, 8, 6, 0, 1, 8)`: the kit and the object 8, slot 1 |
| `legende-m7` | 7 | (25, 17, 7) or (26, 17, 7) | under the block 28 (25, 16) or 29 (26, 16); walk north (Up) into it; `DeathRetryCount` 20; the flag 1787 off |

## T1 (2026-10-25): selection, equipment and damage tests (all green guards, no gap found)

The code was already right: every T1 test is a guard, green before any change to `Alundra/` (no file of the DLL touched). Each guard is to be killed by a mutant of T3.

| test (file) | what it pins | value |
|---|---|---|
| `ItemInventoryTests.GetItemIdFromSlotId_OwningTheItems1_4_And8_GivesTheLegendarySword` (Items) | slot 1 with {1, 4, 8} | 8 |
| `ItemInventoryTests.GetItemIdFromSlotId_OwningTheItems1And8_GivesTheLegendarySword_AndTheItem8AloneToo` | {1, 8} and {8} | 8, 8 |
| `ItemInventoryTests.GetItemIdFromSlotId_TheLegendarySword_BeatsEverySwordOfTheSlot` | {1, 2, 3, 4, 8} | 8 (rank 4) |
| `ItemInventoryTests.TheEquippedSlot1_OfAHeroWhoOwnsTheItem8_ResolvesToTheItem8` | new game + item 8, `SetPlayerWeaponId(1)` | 8 |
| `DamageTests.B2_K1_TheEquipmentRecordsOfTheLegendaryInventories_EqualTheBinary_...` (x2: `legend`, `legend_holy`) | the three records and the second tick against `K1-legend.json` | equal |
| `DamageTests.B2_TheEquipmentRecord1_OfAHeroWhoOwnsTheItem8_IsTheItem8_WithTheAttack6Power50` (x2) | record [1] = item 8, `[(6, 50)]`, armour 17 | by hand |
| `DamageTests.B2_K2_TheLifeAfterEveryLineOfTheOracle_WithTheLegendarySword_IsTheBinarys` (x2) | 22 lines of `K2-legend.json` per inventory | equal |
| `DamageTests.B2_K2_TheNaturalHit_OfTheLegendarySword_OnAGroundedBeannoid_Takes51_AndKillsTheNineLifeOne` (x2) | swing 0x10, attack (129, 1); 100 -> 49, 9 -> 0 | 49, 0 |
| `LegendarySwordArenaTests.TheLegendarySword_OnTheGround_...TakesFiftyOneLifeFromARealBeannoid` (Hero) | real Beannoid of map 14, native D1, life 100 | 49, record 11, contact animation 0x10 |
| `LegendarySwordArenaTests.TheBasicSword_OnTheGround_TakesTwoLifeFromARealBeannoid` | same, item 1 | 98, record 0, 0x10 |
| `LegendarySwordArenaTests.TheLegendarySword_InTheAir_SwingsThe0x14_AndTakesFiftyOneLifeFromARealBeannoid` | jump at tick 1, Square at tick 17 (contact at tick 20, hero 5 px up) | 49, 0x14 |
| `LegendarySwordArenaTests.TheBasicSword_InTheAir_SwingsThe0x14_AndTakesTwoLifeFromARealBeannoid` | same, item 1 | 98, 0x14 |
| `LegendarySwordArenaTests.AHeldSquare_WithTheLegendarySword_CountsNoCharge_MakesNoAuraAndNoWave` | Square held 90 ticks then released, 140 ticks | charge counter 0, 0 auras, 0 charge effects, no wave |
| `LegendarySwordArenaTests.AHeldSquare_WithTheSteelEdge_...TheControlOfTheLegendaryHold` | the same run with item 2 | counter 60, aura, >= 16 effects, wave: the observables do see a charge |

Notes. (1) `ItemTablesFixture.LoadReal()` is a reduced table (items 0 to 4, 17 and 25 only): the new selection tests use the exported tables (`ItemTables.GetOrCreate(ProjectRootFinder.Find())`) as `DamageTests` does. (2) `HitArena` gained
`EnableNativeHandlers()` (the arena keeps the Beannoids as the oracle's stubs, native handlers off, until a test turns them on after the setup: with them on from the construction the Beannoid walks and the arena's
"animation 0" assertion fails). (3) The hit by the legendary sword on the real Beannoid: contact at tick 5 (ground), life 100 at the tick of the contact, 49 at the next tick; the second contact is blocked by the 25 ticks of delay.
(4) The exploration of the air swing: the hero rising never touches a Beannoid on the ground (the box is too high: z 20 to 27 px); coming down (Square at tick 17) the contact is at tick 20 whatever the distance 24 to 40 px.

## T2 (2026-10-25): the opcode 0xC2, the field DeathRetryCount and the two presets

Binary (`dis_picked.py 80041D34 34`, capstone on `ALUN_CD.EXE` France): `lbu` of the parameter byte, `lbu` of `0x801EBA3E`, `slt counter, parameter`, `bnez` to the store of 0, else the store of 1 in `Result` (`+0x2C`): `Result = counter >= parameter`, both unsigned. Size 2.

Tests first (commit `5e67722d`), red before the code, in `Alundra.Tests/Events/DeathRetryCountOpcodeTests.cs` (the program `0xC2 [20]`, `0x1A 9`, `Result` preloaded to the value opposite to the expected one):

| test | red before | green after |
|---|---|---|
| `Opcode0xC2_Param20_WithTheCounterAt19_AnswersZero_OverAStaleOne` (preload 1, expects 0) | red | green |
| `Opcode0xC2_Param20_WithTheCounterAt20_AnswersOne_OverAStaleZero` (preload 0, expects 1) | red | green |
| `Opcode0xC2_Param20_WithTheCounterAt21_AnswersOne_OverAStaleZero` (preload 0, expects 1) | red | green |
| `Opcode0xC2_ComparesTwoUnsignedBytes` (200 vs 20 -> 1; 200 vs 255 -> 0; 255 vs 255 -> 1; 0 vs 0 -> 1) | red | green |
| `Opcode0xC2_TakesOneParameterByte_ThePcAdvancesByTwo` (the `0x1A 9` that follows ran) | GREEN (guard: the size-table fallback already advances by 2), to be killed by the mutant "two parameter bytes" | green |

Presets (tests in `TestSavesTests`, red before: unknown preset): the list test `TheTwentyTwoPresets_AreListed_InTheOrderOfTheTable` became `TheTwentyFourPresets_AreListed_InTheOrderOfTheTable` (D-B2-6, the only moved test); the theories over `EveryPresetName` and `WeaponNames` gained the cases `legende-m14` (both) and `legende-m7` (the `EveryPresetName` ones); new tests `TheLegendM7Preset_...` (3), `TheOtherPresets_LeaveTheDeathCounterAtZero`, and (code commit) `ADeathRetryCountOutsideItsByte_IsRefusedByCheckPreset_NotWrapped` (-1, 256) and `ADeathRetryCountAtTheEdgesOfItsByte_IsAccepted_AndReachesTheSave` (0, 1, 255).

Code: `case 0xC2` in `Alundra/Events/EventProgramRunner.cs`; `TestSaves.Preset.DeathRetryCount` (`int?`, 0 to 255 else refused, applied to `GameState.DeathRetryCount` before the capture); `WeaponPreset("legende-m14", 14, 8, 6, 0, 1, 8)` and `new Preset("legende-m7", ..., 7, 25, 17, 7, ..., DeathRetryCount: 20)` (cells of T0); `docs/test-saves.md` (the optional field and a "Legendary Sword presets" table).

Measures: focused run (opcode, presets, save-game tests, event runner tests): 698 passed, 0 failed. Full Release suite (neutralised copy, `--blame-hang-timeout 60s`, one run in 11 min 31 s): 6468 passed, 0 failed, 0 skipped. The Debug suite is left to T3 (last). `check_names.py --rules`: 0 findings. Moved tests against the T0 (d) list: only the presets list test (renamed); no other existing test changed behaviour. `git diff --ignore-cr-at-eol` of `docs/hero-trace-389-*.txt` empty (restored).
