# E20.h4d1 T3 - results (mutants, ADR)

Branch `chantier/theme2-attaques-objets`, 2026-10-29, starting HEAD `aeabd026`. Plan: `docs/plan-e20-heros.md` section 1.31.5, T3. No production or test code was changed in this task (documents only): the mutants are applied and undone by the script `mutants.py`.

## 1. Method

`mutants.py <name>...` applies the edits of one mutant (an `assert` checks that each pattern occurs exactly once, the line ends of the file are kept), runs the targeted suite (418-test filter of T2 narrowed to the 128 tests of the slice: `FireTome2*`, `CameraSwayBuffer*`, the five registry sums), in Release, with the copy neutralised (`skipcopy.targets`), `--blame-hang-timeout 60s`, and restores the files in a `finally`. The failed tests of each mutant are in `out_<name>.txt`. After the 28 mutants: `git diff --ignore-submodules` is empty (tracked tree identical), and the clean tree gives 128 of 128 green again.

## 2. Mutants (28 applied, 28 killed, 0 survivor, 0 not compiling)

The plan lists 25 items (24 of them applicable to the code); the 28 are the list below, the plan's items one by one and the variants that the code makes possible. "Plan" names the item; "Killed by" lists all the tests that went red (short names; the full names are in `out_*.txt`), the test the plan names being among them.

| Mutant | Plan item | Killed by (count) | Tests |
|---|---|---|---|
| `idx_k` | index k instead of 4k | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `no_0x320` | `+0x320` removed | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `bound_0x1800` | bound `0x1800` | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `signed_product` | signed product (the draw taken as a signed high product) | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `xy_swap` | X and Y swapped | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `z_plus_0x100000` | Z with `+0x100000` | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `z_no_plus1` | Z without +1 | 9 | E1, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `anim1` | animation 1 | 11 | E1, E6, W1, W2, W4, W4b, W5, W7, W7b, W8, W10 |
| `effect18` | effect 18 | 10 | E1, E3, E6, W1, W2, W4, W4b, W5, W8, W10 |
| `draw_skipped_full` | draw skipped when the reservoir is full | 1 | E2 (alone) |
| `destroy_other_effect` | destruction with another effect (8) | 3 | E1, E2, E3 |
| `destroy_minus1_removed` | `-1` removed (0) | 3 | E1, E2, E3 (named: E3) |
| `capture_after_flag` | capture after the write of `Flag` | 11 | D1, D4, W1, W2, W4, W4b, W5, W7, W7b, W8, W10 |
| `capture_in_phase1` | capture in phase 1 (each tick) | 10 | D1, W1, W2, W4, W4b, W5, W7, W7b, W8, W10 |
| `shifts_swapped` | `>> 4` and `>> 6` swapped | 12 | D1, D4, D5, D6, W1, W2, W4, W4b, W5, W7, W8, W10 |
| `speed_ne_limit` | `Speed` != `Limit` | 12 | D1, D4, D5, D6, W1, W2, W4, W4b, W5, W7, W8, W10 |
| `identity_status4` | identity tested by the status 4 | 1 | D5 (alone) |
| `restore_guarded_nonzero` | restoration guarded by "buffer not zero" | 11 | B2, C1, D1, W1, W2, W3, W5, W7, W7b, W8, W10 |
| `restore_missing_at_death` | restoration missing at the death of the column | 8 | D1, D5, W1, W4, W4b, W5, W7, W8 |
| `no_0x34` | `0x34` not set | 8 | D1, D5, W1, W4, W4b, W5, W7, W8 |
| `phase2_no_wait` | phase 2 that does not wait for `0x34` | 8 | D1, D5, W1, W4, W4b, W5, W7, W8 |
| `guard_in_phase2` | guard called in phase 2 | 8 | D1, D5, W1, W4, W4b, W5, W7, W8 |
| `cancel_by_table_index` | cancellation by `SpriteTableIndex` (named: the map index 21) | 3 | C1, C2, D1 |
| `cancel_statuses_to_3` | statuses 1, 3, 4 sent to 3 (`!= Destroyed`) | 2 | C1, D1 |
| `blocker_left` | `+0x20` left | 2 | C1, D1 |
| `birth_late` | the sprite 21 outside the tick of its birth (one tick late) | 13 | D3, D4, W1, W2, W4, W4b, W5, W6, W6b, W7, W8, W9, W10 (named: W1) |
| `offsets_reach_not_restored` | `Offsets`/`Reach` not restored (named: W4) | 12 | B2, C1, C2, D1, D5, W1, W2, W4, W7, W7b, W8, W10 |
| `pointer_after_null_test` | (extra) the pointer kept after the test of nullity | 1 | D3 (alone) |

The five registry sums, the cancellation entries and the entry wiring are covered by the removal proof of T2 (`../t2/results.md`, section 3), not repeated here.

## 3. Not applied

- "The box that stops growing before the image 18 (killed by the pairs of the edge)": the growth of the box of the column is a property of the data of the sprite 21 (its animation), read from the exported project; it has no line in the code of the slice, and mutating it would mean touching `alundra-project/` / `data-extracted/`, which this task must not. The pairs of the edge of `W6b` (200 touched, 201 never, 186 touched, 187 never) read the real data and pin the growth; no mutant of it was run.
- The "signed product" mutant: a signed 32-bit product of the (signed) table and the (non-negative) radius has the same low 32 bits as an unsigned one, so a cast would be equivalent; the mutant applied is the one the discovery names ("high seeds"): the draw of the radius taken as a signed high product, `(long)(int)draw * bound >> 32`, which gives a negative radius for a high seed.

## 4. Survivors

None. No test was added and no equivalence proof was needed.

## 5. ADR

ADR-0110 (`docs/decisions/0110-...md`, index updated). Number recounted on all branches (local and remote refs): the highest was 0109 (`chantier/theme2-attaques-objets`), 0110 free.

## 6. Notes

1. The mutants that differ only by the guard or the animation after the death (`restore_missing_at_death`, `no_0x34`, `phase2_no_wait`, `guard_in_phase2`) are killed by the same eight tests: each changes the tick at which the item is freed or the animation left at the end, and `W1`, `W4`, `W4b`, `W5`, `W7`, `W8` compare every tick of the whole cast. `guard_in_phase2` goes red because the guard answers non-zero in the end animation `0x34` (it cancels the item), which the plan's "phase 2 without guard" forbids.
2. The two submodules `CasaEngineMonogame` and `alundra-datas-analyser` show local modifications of the author in `git status`; they were not touched.
