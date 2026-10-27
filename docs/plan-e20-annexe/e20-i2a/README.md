# E20.i2a - T0 results (2026-10-27, revision 4 of docs/plan-e20-heros.md section 1.14.2)

T0 resumed after the first pass stopped at (c) (`first-pass/t0-measures.md`, kept as written). Everything below was redone at HEAD `66f0a548`
(`Alundra/` unchanged since `00516f6d`), and the outputs of the second pass are byte-identical to the first pass's (`cmp` on `wired-dump.txt`,
`wired-full.txt`, `unwired-dump.txt`). Nothing of `Alundra/` or `Alundra.Tests/` is committed by T0: the temporary wiring was applied,
measured and reverted (`git status` clean for both directories).

Layout: `code/` (the temporary wiring `temp-wiring.patch.txt`, the probe test `I2aT0TempProbe.cs.txt`, `check_rev4.py`, `run_part.sh`; all `.txt`/script copies, never compiled
from here), `runs/` (the gap dumps and the `check_rev4.py` output), `runs-Release/` (the logs of the Release suite, the partitions of (c)), `replay/` (the logs of (b)),
`first-pass/`. The lecture is in `../e20-i2-disc/`.

## (a) The deposit

`../e20-i2-disc/` completed (README, `SHA1SUMS.txt`, 58 files, `sha1sum -c` all OK, no `__pycache__`, no copies of repository files: `reading/head/` and `reading/w1-ref/` dropped, see its README).

## (b) The replays (from a copy of `e20-d/oracle/`, from the repository root, `python -B`)

- `sscen.py W1 W2 W5 W10` (1 min 9 s) then `rscen.py G9` (8.5 s): `out-swim/w1`, `w2`, `w5`, `w10`, `out/g9`, `summary-W1/W2/W5/W10.txt`, `summary-G9.txt` identical to the repository's (`diff -rq` / `cmp` empty).
  The `check/xcheck-*` and `cov-G9` files differ only because the replay was run without `--cross` (they are the cross-interpreter reports); no trace depends on them.
- The lecture's probes (`../e20-i2-disc/reading/s*.py`, `critic/r_s*.py`, run with `python -B -I` against a copy of the harness): `s5`, `s6`, `s7`, `s9` outputs equal `out_belt_probe.txt`, `out_belt_probe2.txt`, `out_belt_wall_probe.txt`,
  `out_belt_target_push_probe.txt`; `s4` equals `out_drift_tables.txt`; `s8` equals `out_portal_deep.txt`; `s1_scan.py 0x188` and `0x4334` equal `out_scan_188.txt` and `out_scan_4334.txt`; `s3_callers.py` on the eight addresses of `out_callers.txt` equals it;
  `s2` prints the total line of `out_census_belts.txt` (`111 ... nolock=119`); the critique's `r_s5`, `r_s6`, `r_s7`, `r_s8`, `r_s9` equal its `out_r_*.txt` and `out_portal_deep.txt`. Logs in `replay/`.

## (c) The inventory by execution

The temporary wiring (`code/temp-wiring.patch.txt`, never committed): the pseudo-bit `CellsCollisionField.DeepWaterWallMaskBit = 0x10000`, the test in `GetGroundSample`
(`(walkabilityMask & bit) != 0 && (tileFlags & 0xE00) == 0x800` makes the cell not walkable), a call in `PlayerManager.Tick` that sets or clears the bit in `Controller.Settings.WalkabilityMask`
by `BootsLevelOf(gs) < 2` before `ScriptedMotion.TickPlayer`, and `BlockedCorners` reading `entity.Controller.Settings.WalkabilityMask`.

### The suite, Release (copy neutralised, `--no-build` after a Release build with the neutralising targets, `--blame-hang-timeout 60s`)

Partitions (disjoint, filters in the commands below), 7 490 tests = 7 489 of HEAD + the probe test:

| Part | Tests | Red |
|---|---|---|
| p1a `Ai.Projectiles.` | 332 | 0 |
| p1b `Ai.Objects.` | 634 | 0 |
| p1c rest of `Ai.` | 275 | 0 |
| p2 `Hero.Hero*` | 1 299 | 1 |
| p3 rest of `Hero.` (550 + the probe) | 551 | 0 |
| p4 `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2 208 | 0 |
| p5 the rest | 2 191 | 1 |

(p1a to p1c add up to the 1 241 `Ai.` tests of the first pass, which ran in one 9 min 10 s call.)

### The tests that move: two, both predicted

1. `Alundra.Tests.Hero.HeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22` (predicted: the deviation test, inverts). With the wiring disabled (`I2A_OFF=1`) it passes (`runs-Release/offHero.log`, 417 of 417 green: `HeroSwim*` and `PrefabArcSupportTests`).
2. `Alundra.Tests.Support.PrefabArcSupportTests.PrefabMode_OnMap478_TheHeroIsTheRealPrefabOfTheHero` (predicted since revision 3): line 126 pins the live hero mask to `0x41`; sans bottes it is `0x10041` (65 601), the pseudo-bit. Rewritten by derivation at T1.

Tests that change when the preset is added (T1, not moved by the wiring alone; counted at HEAD): `TestSavesTests.TheThirtyFourPresets_AreListed_InTheOrderOfTheTable` (the name and `AllPresetNames` gain `mur-m14`: 34 to 35 rows),
and the three theories over `EveryPresetName` (`ABuiltPreset_IsAcceptedByTheRealRules`, `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule`: 34 rows each, 35 after), and
`TheOtherPresets_LeaveTheDeathCounterAtZero` (its loop over `AllPresetNames`): rows added or loop extended, never red. Total moved: 2 (+ the 5 mechanical preset tests at T1), under the stop of 10.

No test of a story chain (389 to 392, 179, intro) moves; the water files (`CarryLockSwimAndStatesTests.SwimScene`, the arcs of the 416 and of the 14, the 40 files that speak of water or `Slope_18c`) are all green.

### The tests with boots stay green, and only L0 and L1 move

Green with the wiring (p2): every `HeroSwim*` test but the deviation test above, among them `R9_TheRunIntoDeepWater_Floats_ThenSwims_WholeTraceEqualToTheOracle` (`g9/R9-deepwater-boots2`),
`R9_AHeroInTheWater_FloatsAtTick1_SwimsAt2_...` (`g9/R9-allwater-start-R`, `g9/R9-allwater-start-T-R`), `R9_ATriangleHeldInTheWater_...` (`g9/R9-allwater-start-T`), `W1_AWalkIn_Slope4At20_Floats21_Swims22` (`w1/W1-walk-L2`, `L3`),
`W1_ARunIn_IsTheOracle_NoRunInTheWater` (`w1/W1-run-L2`, `L3`), `W2_TheOtherSwims_AreTheOracle` (`w2/W2-L3-R`), `W7_TheOtherHurts_AreTheOracle_AtTheFirstContact` (`w7/W7-hurt-idle-L3`), and the montage inventories
`b1b` (object 0x1B, level 2) and `b1c` (object 0x1C, level 3) they run on (`RunMontage.cs:83-89`; `b1a`, object 0x1A, level 1, is the L1 case and is blocked). With the wiring off (`I2A_OFF=1`) the same tests are green too (`offHero.log`), so no trace of level 2 or 3 moves;
the 15 traces below are the only ones the wiring changes (the unwired gap of all 15 is non-zero: 195, 502, 502, 296, 296, 416, 416, 732, 732, 112, 112, 502, 502, 610, 410 diffs at tolerance 16 in `runs/unwired-dump.txt`, at most the (t1)/(t2) gaps above once wired).

### The gap of each of the 15 traces to the DLL, wired (`runs/wired-dump.txt`, `runs/wired-full.txt`), and the subset of the closed list (D-I2A-6) each one employs

The comparison is the one of `HeroSwimTests.Equal` (`CompareRows`, the standard sound stub `StubbedSounds`, which is (t4) and is common to every trace, the probe's lambda being the same expression); gaps in raw units (1/65536 px).

| Trace | Gap wired | Subset of D-I2A-6 |
|---|---|---|
| `g9/R9-deepwater-noboots` | rebound `0x3E` at tick 86 (oracle 87), `ForceAdjusted` at 85 (oracle 86); `pos_x` 1 unit at tick 84; with the pad shifted from tick 85 by 1: 0 diffs at tolerance 1, 1 diff at tolerance 0 | (t1), (t4): shift from 85 by 1, tolerance 1 |
| `w1/W1-run-L0` | identical to R9 | (t1), (t4): shift from 85 by 1, tolerance 1 |
| `w1/W1-run-L1` | identical to R9 | (t1), (t4): shift from 85 by 1, tolerance 1 |
| `w1/W1-walk-L0`, `w1/W1-walk-L1` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w1/W1-jump-L0`, `w1/W1-jump-L1` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w1/W1-fall-walk-L0`, `w1/W1-fall-walk-L1` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w2/W2-L0-inside-R`, `w2/W2-L1-inside-R` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w5/W5-L0-wall-walk`, `w5/W5-L1-wall-walk` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w10/W10-shallow-to-deep-L0` | 0 at tolerance 0 | (t4) only, tolerance 0 |
| `w5/W5-L0-wall-diag` | 97 entries, ticks 45 to 140: `pos_x` 1 unit at tick 45 only (4 915 200 against 4 915 199); `pos_y` 73 872 at tick 45 (3 350 672 against 3 276 800), then 149 167 at every tick from 46 (75 296 steps against 1 424 then 1 unit, then 75 296: a constant offset; `W5` ticks 46: 3 425 968 against 3 276 801; 47: 3 501 264 against 3 352 097) | (t2) as written by revision 4: tick 45, X at most 1 and Y at most 73 872; from tick 46, X exact and Y at most 149 167; (t4). No (t1), no (t3), no shift |

`code/check_rev4.py runs/wired-full.txt` (output in `runs/check_rev4.out`): 97 entries, ticks 45 to 140, the only X entry is tick 45 (1 unit), the largest Y gap from tick 46 is 149 167, **out of the revision 4 bounds: none**. (t3) is employed by no trace.

## (d) The pseudo-bit

`CellsCollisionField.DeepWaterWallMaskBit = 0x10000` (bit 16): `tileFlags = walkability | groundProperty << 8` is below `0x10000` (`WalkabilityMaskFor` is at most `0x1041`), so no existing bit collides.
No type or file of that name exists (`rg`, Alundra, Alundra.Tests, tools); `python -B tools/dll-structure/check_names.py --rules`: 0 findings, 1 warning (`Alundra.Tests/UI/Shared/TextBoxGpu.cs:266`, pre-existing, unrelated), no new type needed.

## (e) The preset cell

`mur-m14` = `new Preset("mur-m14", "test-mur-m14", 14, 40, 48, 1, [], [], [], [])` (the kit alone, boots level 0, no `CounterIndex(0x1B)`), `swim-m14` minus its boots counter.
Read again from `data-extracted/data/tiled/map_14.alundra.json`: cell (40, 48) has height 1 and ground property `0x10` (not water, `0x1000 & 0xE00 = 0`), cell (40, 49) height 0 and `0x18` (`0x1800 & 0xE00 = 0x800`, deep water), (40, 50) the same. The hero faces the pond at one cell of the wall.
The table goes from 34 to 35 presets.

## Commands (from the repository root; `SKIP` = `-p:CustomAfterMicrosoftCommonTargets=<scratchpad>/e20e0a3b-exec/skipcopy.targets`)

```
git apply code/temp-wiring.patch.txt ; cp code/I2aT0TempProbe.cs.txt Alundra.Tests/Hero/I2aT0TempProbe.cs        # temporary
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release $SKIP
I2A_FULL=1 I2A_OUT=runs/wired-full.txt dotnet test ... --filter "FullyQualifiedName~I2aT0TempProbe"           # also wired-dump.txt without I2A_FULL, unwired-dump.txt with I2A_OFF=1
code/run_part.sh p1a "FullyQualifiedName~Alundra.Tests.Ai.Projectiles."   # p1b ...Ai.Objects.; p1c Ai. minus both; p2 Hero.Hero; p3 Hero. minus Hero.Hero; p4 the eight namespaces; p5 the rest by !~ on Ai, Hero and the eight
git checkout -- Alundra/Hero/PlayerManager.cs Alundra/Physics/CellsCollisionField.cs Alundra/Physics/ScriptedMotion.cs ; rm Alundra.Tests/Hero/I2aT0TempProbe.cs
```

The four `docs/hero-trace-389-*.txt` files the suites rewrite (end of line only, `git diff --ignore-cr-at-eol --quiet -- docs/` clean) were restored and are not staged.

## Stops of the plan: none reached

No test of a story chain moved; 2 tests moved (limit 10); no gap outside the bounds of revision 4; no test looped.
