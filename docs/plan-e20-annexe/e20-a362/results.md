# E20.A362 T1: results (2026-10-26)

Code commit `1cbbf977` (the arc, the preset, the content test, `docs/test-saves.md`); this folder holds the measures. Branch `chantier/theme2-attaques-objets`. No production file changed but
`Alundra/SaveGames/TestSaves.cs` (the preset). Test support: `ArcRun.OneFrameWithTheDialogueButton` takes the time of the frame (`elapsed`, default 0.02f) so that the dialogue button works at k ticks per frame.

## The arc `Alundra.Tests/Arcs/OlenBombLidArcTests.cs`

Each test plays the scene once per cadence k = 1, 2, 3 (k ticks per frame, `k / 50f`; the scene of a cadence is played once and shared by the tests, a timeline is plain data). A tick is counted by the head of the
tick of the world proxy (`pad`), every state is read at the end of its tick (`inventory`, after the hit pass). 46 cases: A1 to A11 (the two pins of A9 are `A9_TheLidStaysInStatus3_E19Missing` and
`A9_TheBombStaysInStatus3Invisible_E12Missing`), N1 (without G248) and N2 (the sword bench, positive and negative), all green at the three cadences. Every expected tick of the table of `t0a.md` (d) held at the
three cadences, except the rows the table itself marks INFERENCE (see the deviations). The measured values are in `t1-timeline.txt` (one block per cadence):

| row of the table | measured (identical at k = 1, 2, 3 unless said) |
|---|---|
| 1, 2 | `@558` to `@562` 11 ticks, `@562` to `@569` (S) 31, S to `@582` (T4) 31 |
| 3 | the bomb absent at S - 1, present at S at Olen's (X, Y) = (204, 137.5) and Z + 32 px, direction `0x10`, animation 0; its first program call (`@628`) at S + 2 |
| 6 | `@635` (the bomb sees T4) in the very tick of `@582` |
| 7 (INFERENCE) | the Y decreases by 1 px per tick from the start m to the rest r (k = 1: m = S + 32, r = S + 66; k = 2: S + 32, S + 66; k = 3: S + 33, S + 67), rest (204, 103, 32), unchanged to A |
| 8, 9 | `@608` and `@641` in the same tick (A = S + 140 / 140 / 141), the animation 1 at A (2 at A - 1), the sound 46 requested once, at A |
| 10, 11 | the frame box false up to A + 15, true at A + 16 = H; at H the lid touched by the bomb, counter 25 (24 at H + 1), bomb hit counter 1, one call of the spark seam at H, three sparks on the reservoir (effect 9) from the frame of H |
| 12 | D[1] `@728 @731 @734 @737` and the End `@738` of the same run, all at H + 1; T6 first set at H + 1; the lid Normal at H, Deactivated at H + 1 |
| 13 to 16 | `@171` H + 2, `@172` H + 3, `@179` H + 64, the births H + 125, 156, 187, 218 at (204,104), (216,104), (204,104), (216,104) |
| 18 | `@222` (H + 249) not reached when the run stops at H + 248 (k = 1); at k = 2, 3 the last frame runs one or two ticks further: `@222` ran at H + 249 exactly |
| 19 | the lid present, Deactivated, at (212, 100, 32) from H + 1 to the end; no sound 230, no effect 11 slot at any frame |
| 20 | the bomb Normal from A to A + 50, Deactivated at A + 52 and A + 53 and to the end, present in the world |
| 21 | the hero never touched, life 10/10 all along (Hp and stats), at (204, 184, 32) at H: 81 px from the bomb |
| 22 (n1) | without G248: the dialogue 131 (`@527`), the loop (`@532`); `@536` and `@569` never run, no record 5 and no T3 for 330 ticks after the talk |

`ArcRun.SkippedOrExceeded` is empty (the `0x8E` and `0x8F` of B[4] are executed, none is skipped); no unexpected error. The sounds: 44 (Olen) at S, 26 at T4, 46 at A, then 205 four times (the Murutas' births).

## Deviations from the plan (flagged)

1. **The arrival (204, 144) of D-A362-3 cannot start the talk.** From there the walk of 48 px south ordered by B[3] (`0x0B [1, 48, 0]` `@149`) is stopped by the block of the record 1 (192, 200) after 40 px
   (the hero stops at y 184) and the radius is never passed: B[3] never ends, T3 is never cleared, Olen waits for ever at `@543`. The preset's recipe ("walk up to Olen and press the button") is what the arc does:
   it arrives as D-A362-3 says, holds Up until the contact with Olen stops the hero (y 134), presses Square while pushing, and B[3] then walks him 50 px to y 184 (the radius 48 is passed). The cell of the preset
   (8, 9, 2) and the table of ticks are unchanged: only the distance press to S is longer (426 to 438 ticks, the dialogue 132 types for about 320 ticks). The recipe of the preset (walk up, then press) is therefore a
   necessity, not a convenience: pressing the button from the preset's cell is not possible (Olen is two tiles away).
2. **The hero of the bench (n2) at (176, 100, 32) is not standing at z 32.** T0 (d2) chose it from the boxes alone: at y 100 the footprint of the hero (21 x 15, y 93 to 108) reaches the row 5 of the cells
   (y 80 to 95, a wall of height 5 = 80 px), so the ground under him is 80 and his sword is above the body of the lid (`PlaceHero(176, 100, 32)` gave z 80; the first run of the bench saw the counter 0). The bench uses
   **(176, 105, 32)**, inside the overlap window of T0 (d2) (y 86 to 107), footprint in the rows 6 and 7 (walkable, height 2); the expected centre of the sparks is re-derived by `bench_geometry.py` (same model, same data,
   no run of the DLL): raw (12910591, 6979583, 2588671) = (196.99998, 106.49998, 39.49998) px instead of (12910591, 6815743, 2588671). The controls are the same place facing west (no frame of the stroke overlaps) and 30 px south
   facing east (`bench_geometry.out.txt`); the T0 controls (50 px west, 20 px north) stand on walls (z 80). A first control facing north (the facing 16) hit the lid at the tick 12 of the stroke: the DLL takes the row 1
   of the sword's animations for the facing 16, and the model of `bench_geometry.py` predicts the same hit for the row 1 (first overlapping frame 12, centre y 100.5), which confirms the model; the control became the facing 8.
3. **Row 7 is recorded, not asserted** (the note (iv) of the table: rows 4, 7 and 17 (Z) are INFERENCE): the start of the slide is S + 32 at k = 1, 2 and S + 33 at k = 3 (the table says S + 31, S + 32 at the latest), the
   rest S + 66 / S + 66 / S + 67 (the table: by S + 66). The test asserts the 1 px per tick, the rest in (204 +/- 1, 103 +/- 1, 32) by S + 70, and its stability up to A.
4. **The effect slots of the reservoir are read by `TargetEffect`** (9 for the sparks, 11 for the break effect), as `HitPassTests` does; `RecordIndex` is -1 for global effects.
5. **A + 51 and A + 52 are not asserted** (the table): the bomb is Deactivated at A + 52 here.
6. **The invisibility of the bomb (A9 pin) is not read**: the test pins the status 3 and the presence of the bomb in the world; that the last frame of the animation 1 draws nothing is a fact of the data (A4 of the reading).

## The preset `olen-m362`

`Alundra/SaveGames/TestSaves.cs`: map 362, tile (8, 9, 2), flag 248 (word 7, bit 24 = `0x01000000`), slot `test-olen-m362`, the last of the table (27 presets); `docs/test-saves.md` has its section.

## Tests red before, green after

Red before the preset (6 cases of `TestSavesTests`): `TheTwentySevenPresets_AreListed_InTheOrderOfTheTable`, `TheOlenPreset_IsTheNewGameInOlensDream_OnTheCellBelowOlen_WithTheFlag248Alone`,
`TheOtherPresets_LeaveTheDeathCounterAtZero`, and the cases `olen-m362` of `TwoConstructions_AreEqual_ByteForByte`, `TheSlotOfAPreset_IsTestDashItsName_AndFollowsTheSlotNameRule`, `ABuiltPreset_IsAcceptedByTheRealRules`;
all green after (162 cases of `TestSavesTests`). The 46 cases of the arc are new (green at their first full run once the harness points above were fixed); the mutations that kill them are T2.

## Existing tests that moved (the list predicted by T0 (f))

Only `TestSavesTests.TheTwentySixPresets_AreListed_InTheOrderOfTheTable`, renamed `TheTwentySevenPresets_AreListed_InTheOrderOfTheTable` (its expected list gains `olen-m362`); the theory data of three tests gained the row
`olen-m362` (no edit of their bodies). No other existing test changed.

## The suite

By partitions of disjoint namespaces (the shape of `docs/plan-e20-annexe/e20-e0d/suite-wired/run.sh`; the first group split in two to stay under 10 minutes), `--blame-hang-timeout 60s`:

| partition | Release (`skipcopy.targets`) | Debug (last) |
|---|---|---|
| Arcs | 165 / 165 | 165 / 165 |
| Ai, Audio, Cameras, Cells, Combat, Dialogues | 1751 / 1751 | 1751 / 1751 |
| Entities, Events, ExportGuards, Hero, Items | 3229 / 3229 | 3229 / 3229 |
| the rest | 1971 / 1971 | 1971 / 1971 |

Zero failure in both configurations (7116 cases each). The four `docs/hero-trace-389-*.txt` were rewritten (end of line only) by the suites and restored with `git checkout`; none indexed. `cmp
alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical (the Debug build deposited it). `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 old warning, `TextBoxGpu.cs`).
