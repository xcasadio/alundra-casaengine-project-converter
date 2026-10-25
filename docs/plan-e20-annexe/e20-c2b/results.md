# E20.c2b T0: versement, prerequisites, oracle replay, new vectors, inventory by execution (2026-10-25)

Branch `chantier/theme2-attaques-objets`, HEAD at the start of T0: **`7df47c2c`** (parent repository; the engine submodule is not touched by c2b).

## (a) Prerequisites

E20.c2-0 (`2655dd50`), E20.c2a (`f7f609db`), E20.i (`f05171c9`), E20.d1 (`e613b2e5`), E14.a (`451ff8be`, a1a to a1c CONFIRMED) and E20.g1a (`44ce16c8`)
are done and CONFIRMED on the branch. **E20.e0d** (`bd88b2cc`, ADR-0094) and **E20.b2** (`7df47c2c`, ADR-0095) are CONFIRMED and present at the HEAD of start.
All met; no stop.

## (b) Versement

`../e20-c2b-disc/` (README, `SHA1SUMS.txt`, 99 files + README): the three reports, the critic, their scripts and outputs; no annex copy (`binary/oracle/` equal to
`../e20-c2/oracle/`, left out) and no `__pycache__`.

## (c) Replay of the oracle and new vectors (files `oracle/`, `replay/`, `oracle/out/`)

- **E20.c2 oracle** replayed from a COPY of the annex (`docs/` laid out as in the repository, `python -B run_c2.py`, 269 s, exit code 0, every verdict PASS, the manifest of 60 files
  identical to the stored one). Outputs **A1, A2, B1, B2, C1, D1, E1** (`.json` and `.txt`) byte for byte equal to `../e20-c2/oracle/out/` (`replay/copy-md5.txt` = `replay/repo-md5.txt`).
  (The run needs `docs/plan-e19-d2c-valeurs.md` beside the annex: `compare.py` reads it.)
- **`W7-hurt-shore`** replayed from the copy with `sscen.py W7 --cross`: `.txt` and `.json` equal to `../e20-d/oracle/out-swim/w7/` (the whole `w7/` folder is identical; `replay/w7-*.txt`).
- **`oracle/vectors_entry.py`** (new): 105 vectors of the pure function, each by ONE call of the real `0x80031A68` in the c2 hurt-hero world (no tick), with every changed byte of the hero,
  the toucher and the stats block read back. `oracle/out/vectors_entry.txt` and `.json`; `oracle/out/vectors_entry-crosscheck.txt`: every direct vector equals the tick-level oracle (A2 armour x stats, A4 bearings, B1 cost x boots, B2 floor).
  What the vectors say (all read from the output):
  - toucher branch, Orc (attribute 0x02, power 4): life loss kit 4, leather 3, ancient 2, silver 1, no armour 1, bracelet 3 (floor 0); the return is the direction (24 east, 8 west, 0 south, 16 north, 27 SE, 5 SW, 21 NE, 11 NW, 23, 7, 31, 15; the 1-px bearings 24, 8, 0, 16, 28, 12) and the ONLY byte written is the hero `+0x14`;
  - **toucher with `+0x1CC` = 1, 0x40, 0x77, 0x78**, hero `+0x1CC` = 1, 0x77, 0x78, hero `+0x1D0` = 25, both, boots level 3, hero `+0x180` = 0x180 with a toucher set: the branch tests none of them, the same loss (12 -> 8 on the kit) and direction are returned (`T-toucher1CC-*`, `T-hero1CC-*`, `T-hero1D0`, `T-both`, `T-boots3`, `T-bits180`);
  - **(0, 0)** (hero and toucher on the same point) returns **24** (`B-same-0-0`);
  - response `0x80` (Charm of Nava, attribute 0x06): life unchanged, direction 8 returned; the same attack without the charm: 10 -> 6; sheet byte poked to `0xC0` or `0x80`: life unchanged, direction returned; poked to `0x40`: life 0, direction returned; attribute 0: unchanged; stats 1/10 hit: life 0 and the return is the direction (not -1);
  - cell branch (`+0x224` = 0): cost `{0, 1, 2, 3}[(+0x180 & 0x180) >> 7]`, boots levels 0, 1, 2 lose it and **level 3 returns -1 and writes nothing** (`H-*-b1c`); boots level 2 and level 3 on a cell with the silver armour: lost (costs 1 and 3) and spared (`H-boots2-silver-*`, `H-boots3-silver-*`); the armour is ignored (all seven inventories lose 3 on a cost-3 cell); floor 0; the return is `(+0x8C + 0x10) & 0x1F` (0 -> 16, 5 -> 21, 8 -> 24, 16 -> 0, 24 -> 8, 31 -> 15);
  - hero `+0x1CC` = 1, 0x77, 0x78 on a cell: **-1, nothing written**; no toucher and `+0x180` = 0, 0x87F or 0x800: -1, nothing written; `+0x180` = 0x100 alone: cost 2.
- **`oracle/scen_new.py`** (new): A3, A5 and the carried object, tick by tick (`oracle/out/A3.*`, `A5.*`, `C-carried.*`, `summary-new.txt`), real code, inputs only (pad words and the A6 harness write PosX := 232):
  - **A3** charm: touched at tick 1, hurt at 2 (`0x31`, `0x1C` from 6, Idle at 17 like A1), life unchanged; without the charm 10 -> 6; the charm against the real Orc (response 0x0F): 12 -> 9.
  - **A5**: walking (A1 geometry) touched 17, hurt 18; sword swing (Square at 1, anim 0x10 for ticks 1..21): contact before tick k gives hurt k+1 for k = 2, 8, 14, 21 (`0x31` replaces the swing at once), k = 22 (Idle) hurt 23; jump (Cross at 1, rise ticks 1..20, apex 10 and 11, landing 21): contact while rising (tick 2) hurt 3 with `0x31`, `0x1C`, **animation 1 at tick 18, then `0x2D` (fall)**, Idle 22; **contact at the apex with the Beannoid on the ground does not touch the hero (no contact before tick 19)**; a Beannoid raised by 8 touches at 10, hurt 11 in the air; contact at 19, 20, 21, 22 gives hurt 20, 21, 22, 23 (the hero lands at 21: `0x31` at 22; contact at 22 on the ground: Idle at 22 then `0x31` at 23); holy sword charged (Square held from tick 1): charge counter 48 at tick 70, **0 at tick 71 (the hurt) and after**, whereas the control keeps counting (49, 50 ...). The pending-shot pointer `[0x80127110]` is 0 from tick 2 in every run, also after a harness poke in the charge state (`A5-charge-wptr`: cleared at once even without a hurt): **its reset by the hurt block is not observable in the oracle**.
  - **C-carried** (the PutInHands core written by the harness: hero `+0x2C` := the object, object `+0x28` := the hero, hero `+0x88` := 0x0C): the hurt tick writes object `+0x24` = -1 and object `+0x8C` = **the hero's `+0x8C` of BEFORE the tick** (24, the new one is 8: `UpdatePlayerCarriedEntity(2)` runs before `+0x8C = d`); hero `+0x2C` is NOT cleared; the carry counter `[0x80098F28]` stays 0. With the object's own program inert (it does not run here) the hero returns to **state 0x0C at tick 18** and the object keeps following him: what clears `+0x2C` and breaks the object belongs to the object's program (E14), not to the entry. The walking variant (state 7) is hurt at 18 and is back to 0x0C at 34.

## (d) Inventory by execution (`inventory/`)

Temporary wiring on the HEAD (`inventory/temp-wiring.patch.txt`, NEVER committed, reverted: `git status` shows no `Alundra/` change): the entry (toucher branch with `Damage.ComputeNewHp` and the
direction, cell branch with boots < 3 and `+0x1CC` = 0), the block in `MovePlayer` after `PlayerAnimationEffects.Create(FreeMode)` (charge, weapon, `UpdatePlayerCarriedEntity(2)`,
pilot, `0x3A` on the slope retained at the head else `0x31`, `+0x8C`, `+0x1CC` = 0x78, `EndOfMovePlayer`), the `== 0x20` path of the locked branch, and the blink at `PublishEntityFrame`
(hidden when `(DamagedTickCounter & 3) == 3`, restoring only what it hid), with a journal (`C2B_LOG`: HURT lines with HP before and after and the toucher type, BLINK lines with the test) and three switches
(`C2B_NO_TOUCHER`, `C2B_NO_CELL`, `C2B_NO_BLINK`; the patch file shows the first version, the switches were added afterwards). Release, neutralised copy, `--blame-hang-timeout 60s`, the partitions of `../e20-e0d/suite-wired/run.sh` (`inventory/run.sh`).

| partition | total | failed |
|---|---|---|
| p1 (Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.) | 1 557 | 28 |
| p2 (Entities. Events. ExportGuards. Hero. Items.) | 3 024 | 1 |
| p3 (the negation) | 1 890 | 0 |
| total | 6 471 (the count at HEAD, all green before) | 29 cases, 13 methods |

Moved tests, classified by cause by re-running the four classes with one cause at a time (`inventory/sub-*.red`: all causes 29, toucher only 8, cell only 21, blink only 0):

- **Injury (toucher), 8 cases / 6 methods**: `Combat/HitPassTests`: `H1both_...`, `H3_...HeroDoesNotLoseLifeYet`, `TheReachBehindTheHero_...`, `TheReachInX_AtDyZero_...` (dx -8, -9, 20), `WhileTheContactLasts_...`; `Entities/StoredHitBoxTests.TheWorldsTick_StoresTheBoxOfAnEntityWithNoAttack_AfterThePhysics`.
- **Hazard cell, 21 cases / 7 methods**: `Ai/Objects/C4WorldTests` M1 (8 cases: index 2..5 x moving), M2 (4), M3 (4), M6 x 2 (the hero of `C4Run` stands on cost-2 cells of the real maps: 34 `HURT cell` lines, 10 -> 8); `Ai/Objects/PitFallWorldTests` P3 (index 2) and P3b (index 2, "corner" and "cornerexit").
- **Blink: no test moves** (the full suite with only the blink wired: 6 471 green). The journal lists 922 entity-ticks hidden (thrown objects of `C4Run`, `HitEffectWorldTests`, the dropped objects of `BeannoideLootArcTests` and `PickupAiDropWorldTests.W4`, `HeightTargetSitesTests`, `ControllerlessVerticalStepTests`, ...): none is asserted.
- Tests the plan predicted but that do NOT move: `HeroRunArenaTests` (G7, cut before the hurt), `HeroSwimTests` W7, `HeroRunTests`, all `ArcRun` (444 and 36 included). Tests that move **outside the plan's predicted list**: `C4WorldTests` and `PitFallWorldTests` (the cost-2 cells of the C4 maps); to be rewritten by derivation in T2/T4 (not by recopying the output).
- HP and `TouchingEntity` journal: `HitEffectWorldTests.Run` 11 touches with 10 -> 10 (no `HeroSheet`: D-C2B-9, unchanged), `HitPassTests.FirstContactTick` 4 (10 -> 9), `CreationSwitchClockTests.Run` 3 (10 -> 0, test unchanged), `C4WorldTests.M11_Witness...` 3 toucher and 1 cell (10 -> 9, 10 -> 8, test unchanged), `HeightTargetSitesTests...` and `StoredHitBox...` 1 each. Chain of history (389 to 392, 179, intro): no test of it moved. `docs/hero-trace-389-*.txt` were rewritten (EOL only) by the suites and restored.
- The presets list test (`TestSavesTests.The...Presets_AreListed_InTheOrderOfTheTable`) does not move with the wiring; it moves mechanically when `blessure-m15` and `blessure-m14` are added (D-E0D-12), as the plan predicts.

## (e) Presets and the real hazard square

Derived from `data-extracted/data/map_*.json` (tile = (XPos / 2, YPos / 2) for an entity record, tile Z = the cell `Height`, walkable cells `Walkability` 0).
- **`blessure-m15`**: map 15 (Overworld 3,2), **tile (7, 33, 7)**: the Orcs (sprite 85) are the records at (14, 60) and (14, 72), i.e. tiles (7, 30) and (7, 36), Height 14 (Z 7), three tiles (48 px) north and south of the arrival; the cells (7, 28..36) are walkable at Z 7 (a third Orc at tile (21, 36)). 10/10 life, the four armours, the bracelet, the charm and the level-3 boots in the bag.
- **`blessure-m14`**: map 14 (Beannoids arena), **tile (5, 29, 5)**: the Beannoid (sprite 79) record 0 at (10, 52), tile (5, 26), Z 5, three tiles north; the cells (5, 27..29) are walkable at Z 5 (the record 4 at (12, 62), tile (6, 31), is the second).
- **Real hazard square**: map 36 (a real map, 180 hazard cells), **tile (12, 44, 1)**, cost 1 (Walkability 128, Height 1; the cells (10..14, 43..45) are all cost 1 at Height 1). Costs 2 and 3 on flat ground: map 0 (the test map), tile (4, 53, 0) cost 2 and tile (10, 53, 0) cost 3 (Height 0). The maps 14 and 15 have no hazard cell.

## (f) The file of the entry

`Alundra/Combat/HeroInjury.cs`, namespace `Alundra.Combat`, `internal static class HeroInjury` (no `Alundra` prefix, ADR-0075; neither the name nor the file exists in the DLL, the tests, the engine or MGUI); tests in the mirror folder `Alundra.Tests/Combat/HeroInjuryTests.cs` (ADR-0080). `tools/dll-structure/` needs no table change (the table is the archive of R2 to R5; `check_names.py --rules` is the live guard, to run after T1). Next ADR number: the highest on this branch is 0095 (to recount on all branches at T5).

## Stops

None. Facts of the plan confirmed: the entry (58 instructions, two callers, the toucher branch tests neither `+0x1CC`, `+0x1D0`, boots nor state), the table `{0, 1, 2, 3}`, the knock-back and its total, the blink rule. Noted for the next tasks: (1) the pending-shot pointer reset is not observable (above); (2) after the hurt a carried hero returns to `0x0C` while the object's program does not run (above); (3) the C4 and pit-fall tests move through hazard cells (above).

# T1 and T2 (2026-10-25, HEAD of start of the passage `2f82b6e6`)

## T1 - the entry (`Alundra/Combat/HeroInjury.cs`, tests `Alundra.Tests/Combat/HeroInjuryTests.cs`)

- `HeroInjury.Enter(hero, sheet, equipment, animationLatch, bootsLevel)`: the toucher branch (`Damage.ComputeNewHp`, then `ScriptHelper.GetDirectionToTarget(hero - toucher)`, 24 for (0, 0)), the cell branch (`+0x1CC == 0`, boots below 3, cost `(+0x180 & 0x180) >> 7` = the table `{0, 1, 2, 3}`, floor 0, return `(+0x8C + 0x10) & 0x1F`), else -1.
- Tests first (`119c2e90`, entry stubbed to -1): **111 cases, 99 red, 12 green** (the 12 are the vectors whose expected answer is -1: `N-nothing`, `N-other-bits`, `N-bit80-only-water`, `H-080-b1c`, `H-100-b1c`, `H-180-b1c`, `H-boots3-silver-080`, `H-boots3-silver-180`, `H-hero1CC-01/77/78` and the bare hero of e4); the list is `t1/before-stub.txt`. Code (`8466dc90`): **111 of 111 green**.
- Every case is one of the 105 vectors of `oracle/out/vectors_entry.json` (the real code in the emulator; the vector `T-beannoid-real` is not driven: its attack is the spawner's, it is covered by the arena tests) or a direct fact (the cost table over three words and four boots levels, the armour loss 4, 3, 2, 1, 1, 3, the D-C2B-9 cases). The returned direction and the life are the vector's; the test also checks that nothing else is written (direction, counters, positions of the hero and of the toucher).

## T2 - the hurt block, the states, the locked path (`Alundra/Hero/PlayerManager.cs`)

Code: the slope retained at the head of `MovePlayer` (`slopeAtHead`), `EnterInjury` (the sheet, the equipment and the boots level of the records of the world, the latch of the world), the hurt block (`HurtBlock`: charge and pending shot zeroed, `UpdatePlayerCarriedEntity(2)`, the item driver, the state `0x3A` if the slope retained is 4 else `0x31`, the facing, `+0x1CC = 0x78`, `EndOfMovePlayer`), the locked path (`PlayerControlFlags == 0x20` exactly: the entry then `+0x1CC = 0x78` alone), `case 0x31` (`TryUseItem` only), the constants `HurtAnimationId` and `HurtInvulnerabilityTicks`, the stale comments.

Tests first (`81b019d7`): **74 of the 358 cases of the touched classes are red before the code** (`t1/t2-before-code.txt`; HeroHurtTests 31 of 36, HeroHurtCarryTests 9 of 12, HeroRunArenaTests (G7) 15 of 19, HeroSwimTests (W7) 15 of 106, HitPassTests 4 of 47; the C4 and pit-fall tests 138 of 138 green, StoredHitBoxTests 4 of 4 green). The 5 green ones of HeroHurtTests are the locks `D1-24`, `D1-30`, `D1-10`, `D1-04`, `D1-A0` (the hit is lost: green before and after, they kill the mutant "`0x34` lets the hit through"), the 3 green ones of HeroHurtCarryTests are the witness b8.

New tests (all green after): `HeroHurtTests` 36 cases = b1 `A1` x 3 cadences, `A1-pad0` x 3, b3 `A5-charge` and its control x 3, b4 nine control words of `D1` (`D1-00`, `D1-80`, `D1-20`, `D1-24`, `D1-30`, `D1-10`, `D1-04`, `D1-A0`, `D1-20-release`), b5 x 3, b6 `E1-hp1-bean` x 3, `A4` twelve bearings; `HeroHurtCarryTests` 12 cases = b3 (object in the hands, with and without the program of the object, charge and pending shot) and the witness b8, x 3 cadences; `HeroSwimTests.W7_AHurtOnTheShore...` (b2). After every tick the DLL is compared to the oracle at 1, 2 and 3 ticks per frame: the target animation, the facing, the life (entity and stats block), the charge counter, `+0x1CC`, `+0x1D0`, the touch, the force, the position (exact, no tolerance), the columns of the target, and the sound 23 (once, in the frame of the hurt tick). Recoil of A1: the forces of ticks 18 to 33 sum to **-2 168 832 raw**, and the hero of the DLL moves by exactly that.

Existing tests re-based (not moved by the entry, extended): the W7 tests that posed the state by an access point enter the hurt through the real entry (a toucher posed where the hit pass would put it): **all 15 W7 cases equal the oracle** (0x3A on the slope 4, 0x31 on the slope 0); the three G7 tests and the tackle sweep of `HeroRunArenaTests` are compared to the LAST tick of their traces (they were cut before the hurt), with the facing, the force, the life and `+0x1CC` added: equal.

### Moved tests against the prediction of T0 (d)

After the wiring and before any edit of a test, the suite had **29 red cases, the same 29 as the predicted list** of T0 (`inventory/sub-all.red`; `diff` of the names: identical; `t2/moved-observed-after-wiring.red`). None outside the list. Treatment:
- **4 cases cleared by the montage `HitArena` alone (D-C2B-10)**, no edit of their expectations: `TheReachInX_AtDyZero` (dx -8, -9, 20) and `StoredHitBoxTests...`. The targets of the arena attack once the hero has settled (their attacks are put back after the reset of the hit state); before, the hero carried into the body of a Beannoid was hurt by the settling frames.
- **4 cases rewritten by derivation from the oracle**: `H3` (the whole `H3` trace, the hero's columns of tick 18 included: 0x31, life 9, `+0x1CC` 119, latch 0x31), `H1-both` (tick 2: 0x31), `TheReachBehindTheHero...` (the sweep of the oracle has no contact at dx -10: the DLL now agrees, it was tick 12), `WhileTheContactLasts...` (now one touch, the ticks derived from the cooldown column of `A1-pad0`; it was 1, 26, 51, 76).
- **21 cell cases** (`C4WorldTests` M1 x 8, M2 x 4, M3 x 4, M6 x 2; `PitFallWorldTests` P3 and P3b x 2): the hero of the bench `C4Run` stands on cost-2 hazard cells of the real maps and was thrown back 7 px by the hurt, which moved the object he threw (185 for 178, 150.5 for 143.5...). The oracle of C4 had a hero who stood where he was put: the bench gives him the boots of level 3 (item 0x1C), which spare him from the cells exactly as the entry does; **the expected values are untouched** (the C4 oracle's), all 138 cases of the two classes green. The case on the real hazard cell itself (the cost, the period, the facing, the armour ignored, the boots 2 and 3) is T4's (h1).
- The blink moves no test (T0 (d)): it is not in this passage.

### Suite

Release, copy neutralised (`skipcopy.targets`), `--blame-hang-timeout 60s`, the partitions of `inventory/run.sh`: p1 1 668 green, p2 3 070 green, p3 1 890 green: **6 628 tests, 0 failure** (6 471 at the start + 111 `HeroInjuryTests` + 46 cases of T2); the 3 cases of the charge test were added after that run and run green (HeroHurtTests 36 of 36). `check_names.py --rules`: 0 finding. The suites rewrote the EOL of `docs/hero-trace-389-*.txt` (identical by `diff --ignore-cr-at-eol`), restored. Not run in this passage: the Debug suite, the mutants (T5), the blink (T3), the cells and presets (T4).

### Deviations from the plan

1. **The cell branch is wired in T2**, not T4: it is the second branch of the same function (`0x80031A68`) and the entry called by `MovePlayer` answers both. T4 keeps (h1) on the real map, (h2) and the presets. "T4 se retire seule (les cases redeviennent inoffensives)" no longer holds literally: the cells are harmful from T2 on.
2. The tests commit of T1 holds a stub of `HeroInjury.Enter` so that the tree builds; the tests commit of T2 holds the two test seams of `PlayerManager` (`TryUseItemCallCountForTests`, `ItemUseDriverHookForTests`) and the two constants (`HurtAnimationId`, `HurtInvulnerabilityTicks`) the tests name: the only production lines before the code commit. (b5) needs the seams: the item driver is an empty named stub.
3. **C1 is not replayed on its own**: no water arena exists in `HitArena`; the `0x3A` entry in deep water is proved by the W7 traces of deep water (`W7-hurt-idle-L2` and the others) through the real entry, and the choice by the slope by `W7-hurt-shore`.
4. (b3) the carried object and the pending shot: the facts of `C-carried` are asserted on `CarryRig` (the code -1, the old facing, the link kept; with the program of the object: released) rather than compared column by column to `C-carried.json`; the pending-shot pointer is poked (its reset is not observable in the oracle, T0). The charge is the real `A5-charge`.
5. `A5-charge-nocontact` (the control) is compared to tick 79 only: the pad of a multi-tick frame is the one of its first tick, so the release of the button at tick 80 would be seen a tick late at 2 and 3 ticks per frame, which has nothing to do with the hurt.
6. The animation table of the hero montage (`HeroAttackClock.Table`) gains `0x31` and `0x1C` (the lengths of the oracle) for the shore trace.
