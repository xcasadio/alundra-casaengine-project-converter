# E20.e, T4 (ADR, mutants, suites): measured results (2026-10-23)

Start HEAD `009bbbcb` (T3), branch `chantier/theme1-porter-plateforme`. The two fresh verifiers of T4 are launched by the main session after this commit. (The T0 file `results.md` of this folder is the T0 report and is left as it is; this file is the T4 report.)

## The ADR

`docs/decisions/0083-the-cut-of-the-grass-keeps-the-binarys-draws-and-order-and-queues-only-the-effect.md`, Accepted, 2026-10-23. Number recounted now over all local branches, remotes refs and worktrees: the highest present is 0082, so 0083 is free. It refines ADR-0077 for the cut: no draw in the effect, the order of S-E1
(cell, queued effect, loot and its sound 84, sound 31), the frame of Z (Q-E6), the bound outside the table (Q-E7), the cells without a stack (Q-E4), no persistence (Q-E5), the property `TileCutEffectId` read once. The status line of ADR-0077 now ends "refined by ADR-0083 for the cut of the grass"; the index
`docs/decisions/README.md` has the row.

## The mutants (`mutants/mutate.py`, `mutants/mutants.tsv`)

34 real mutations, each applied to the source by the script (an edit that must match exactly once), built in Release (neutralised copy), run against the tests of the cut (filter: `TileEffect`, `TileCutEffectId`, `HeroAttackTests`, `TestSavesTests`, `CellStore`, `WaveSword`, `PlayerAnimationEffects`, `BalanceTables`; 244 tests then,
245 now), then reverted by `git checkout -- <file>`; after every one `git diff` of the file is empty (logged). The plan lists 15 families (at least 16 mutants required); the 34 cover them and more.

| plan family | mutants | result |
|---|---|---|
| south row divided twice (D3) | M01 | killed |
| `+0x80000` on the Z of the effect (D4) | M02 | killed |
| truncated division (D9) | M03 | killed |
| column bounded for the position | M04 (and M05 for the row) | M04 **survived at first**, killed after the new test below; M05 killed |
| a corner forgotten, order of the corners | M06, M07, M08, M09 | killed |
| bit `0x01` instead of `0x02` | M10 (read), M11 (cleared) | killed |
| strict band of Z, box of the current tick | M12, M13 (naive edge without the +1), M14 (current box) | killed |
| stack kept, collision changed by the cut | M15, M16, M17 | killed |
| effect created at once instead of the queue | M18 | killed |
| sound 31 before the 84 | M19 | killed |
| sound forgotten | M20 | killed |
| the four flags read before the first cut | M21 | killed |
| effect, loot and sound without clearing the bit | M22 | killed |
| property read under `BalanceLevel`, fallback forgotten | M23, M24 | killed |
| effect taken in the map table | M25 | killed |
| added: id fixed to 2, property not read at the load, frame-box gate removed, the hero or the wave not calling the port, loot code, clamps off by one | M26 to M31, M33, M34 | killed |
| **equivalent** | M32 (the `(short)` cast of the west column removed) | survives, and cannot be killed: `x >> 16` of a 32-bit integer always fits in 16 bits, so the cast of the binary's `(s16)` changes nothing in the DLL |

The first full pass: 32 killed of 34, 2 survivors: M04 and M32. M04 (the position of the effect uses the clamped column) survived because no test put a box past the table of 1248 pixels (column 52 and more), where the DLL keeps the formula for the position (Q-E7, documented in the class and in the ADR).
Fix: one test added, `Alundra.Tests/Cells/TileEffectTests.cs`, `C2_PastTheTable_TheCellIsTheLastColumn_ButTheEffectKeepsTheUnclampedColumn_Q_E7` (box at 1250 pixels, bush at the cell (51, 10): the cell is cut, the effect is at column 52). Red on M04 (and on M34), green on the real code. After it, **33 of 34 killed, M32 equivalent.**

## The suites

| suite | tests | failures |
|---|---|---|
| Release, neutralised copy (build then test, `--blame-hang-timeout 60s`), after the new test | 5 330 | 0 |
| Debug, last | 5 330 | 0 |

5 330 = 5 329 (end of T3) + 1 (the test above). `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 findings, 1 known warning (`TextBoxGpu.cs:266`).
The traces `docs/hero-trace-389-*.txt` were rewritten with CRLF by the suites; no content difference with `--ignore-cr-at-eol`; restored by `git checkout --`, never indexed.

## No diff from `9c1f6fe3`

`git diff 9c1f6fe3` on `CasaEngineMonogame` (the engine pointer), `alundra-datas-analyser` (the pointer), `data-extracted/`, `alundra-project/`, the converter projects: empty. (The two submodules show "m" in `git status`: local content of the author, unchanged by this task.)

## The existing tests that moved: predicted (T0 (e)) against measured

Predicted: 2 (`HeroAttackTests.RealPrefab_TheHeroOfTheExport_...AtTheTicksOfTheOracle` and the preset-list test of `TestSavesTests`). Measured at T3 (first run after the wiring): exactly these 2, none outside the list (plan ceiling 10). T4 changes no production code and moves no existing test.
The only test change of T4 is the added one above.

## Deviations from the plan

1. A test was added in T4 (the plan puts tests in T1/T2): required by the plan's own rule "mutants, all killed"; the survivor M04 showed a missing test of the column bound (Q-E7).
2. The T4 report is `t4-results.md`, not `results.md` (which is the T0 report, as the pattern `t1-results.md`, `t2-results.md`, `t3-results.md` of this folder).
3. M32 is an equivalent mutant, not killed (proved above); not in the plan's list.
4. The two fresh verifiers are not run here (the main session launches them); the plan status is not edited (main session).
