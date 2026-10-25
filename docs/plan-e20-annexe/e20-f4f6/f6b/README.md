# E20.f6b T0 (2026-10-25): oracle, probes, predicted list

Plan: `docs/plan-e20-heros.md` section 1.22 / 1.22.2 ("Revision au HEAD" rules, points 1 to 10, bullet "f6b"). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Repository HEAD at the start: `133ebb99` (branch `chantier/theme2-attaques-objets`). T0 writes no DLL code of its own: the probes of the DLL and the temporary wiring that measures the moved tests ran in throwaway files (`probes/`), removed before the commit.
The annex above (`../bin`, `../critic`, `../dll`, `../tests`, `../state-3c923cf`, `../f4`, `../f5`, `../f6a`) is frozen and unchanged.

**T0 stopped on one probe of the plan** (bullet "R de (p1)": the charged rod shot in the arena of the charge is born at the tick 102): measured, the arena releases at 91 with a charge counter of 58 and the charged ball does not exist; it is born at 107 for the oracle's calendar (held to 95, released at 96). See `results.md`, first section.

- `results.md` (French): the result of each point of T0, the values the tests read, the measured list of the moving tests, the deviations from the plan.
- `oracle/`: the scripts of f6b (they import the frozen harness of `../bin` and write under `oracle/out`; run each from a COPY of the annex, never in place):
  - `run_all.sh`: the complete run (the frozen replay `bin/run_balls.py`, `run_balls_extra.py`, `run_balls_mixed.py`, `run_pointblank.py`, `run_flames.py`, `run_flames_noA.py`, then the three scripts below), about three minutes, foreground;
  - `run_f6b.py`: the NEW keys, each in two forms (plain, and suffix `_pf` = with the step of the generator that ends every logical tick of the binary, applied after the record of `rng1` and before the read of `rng0` of the next tick: rule of the plan, point 3),
    plus the `_pf` replay of the 32 frozen keys (30 balls + 2 flames; the frozen keys themselves stay in `../bin/out/json/`): `fire11_branchA_water_on_puff_tick`, `ice14_branchA_water_on_puff_tick`, `fire11_branchA_slope1_on_puff_tick`, `fire11_branchA_slope2_on_puff_tick`,
    `fire11_branchA_water_off_puff_tick`, `fire11_branchA_water_on_end_tick` (an injection of `+0x18C`, documented in the title of each key), `fire11_flush`, `fire12_flush`, `ice14_flush`, `ice15_flush`, `fire11_near105`, `fire11_x107` (the hero flush against, or near, the wall column 5: E20.e0k5),
    `flames309_real` (the three flames of the map 309 with the REAL bytecode runner `0x8004205C`); read by `BallOracle.Load("<name>")` (T1 teaches it the folder `f6b/oracle/out/json`);
  - `mutants_f6b.py`: 27 one-word mutants of C60, E9 and of the creation Z on the real machine (patched RAM), each compared with its control: `out/mutants_f6b.{txt,json}`; 25 killed, 2 proved equivalent by the machine (E9-5b, E9-9);
  - `check_plan_values.py`: the table of values of the plan and its facts checked against the keys (54 assertions): `out/check_plan_values.txt`;
  - `manifest.py <copy>`: the SHA-1 of every output of the run (the ball and flame keys and traces of `bin/out` that the run rewrites in the copy, and `f6b/oracle/out`), sorted; `manifest.txt` is the manifest of two identical runs (183 lines).
- `probes/`: `ZzF6bT0Probes.cs.txt` (P-F6-1 and P-F6-2 on the map 309, P-F6-3 and the wall / point-blank scenarios at the three rates), `ZzF6bT0ProbesP1.cs.txt` (the rods in the arena of the charge), `TempBallAi.cs.txt` (the throwaway C60 and E9 of the measurement wiring), `temp-wiring.diff.txt`
  (the throwaway edits of the tracked files), their outputs (`probes-balls-out.txt`, `probes-flames-out.txt`, `probes-p1-out.txt`) and `check-names-proposed.txt` (`check_names.py --rules` with the proposed files added).
- `suite-wired/`: the red lists and summaries of the three partitions of the suite with the wiring of phase B (`suiteB-*`) and of phase C (`suiteC-*`), and `runpart.sh` (the one of `../../e20-e0k5/suite-wired/`).
- `t1/` (phase A, T1): `results.md`, `red-on-skeleton.txt`, `mutate_f6b_t1.py`, `summarize_trx.py`.
- `SHA1SUMS.txt`: SHA-1 of every file of this folder but `oracle/out` (the manifest covers it).
