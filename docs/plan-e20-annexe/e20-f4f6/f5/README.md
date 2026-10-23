# E20.f5 T0 (2026-10-23): oracle, probes, predicted lists

Plan: `docs/plan-e20-heros.md` section 1.21 / 1.21.1 ("Revision au HEAD" rules, points 1 to 10, bullets "f5"). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Repository HEAD at the start: `b0116485` (branch `chantier/theme1-porter-plateforme`). T0 writes no DLL code of its own: the probes of the DLL ran in a throwaway test file (`probes/ZzF5T0Probes.cs.txt`), removed before the commit.
The annex above (`../bin`, `../critic`, `../dll`, `../tests`, `../state-3c923cf`, `../f4`) is frozen and unchanged.

- `results.md` (French): the result of each point of T0, the values the tests read, the measured list of the moving tests, the deviations from the plan.
- `oracle/`: the scripts of f5 (they import the frozen harness of `../bin` and write under `oracle/out`; run each from a COPY of the annex, never in place):
  - `run_all.sh`: the complete run (the frozen replay `bin/run_arrows.py`, `run_arrows_extra.py`, `run_pointblank.py`, `sweep_wall.py`, then the two scripts below), about one minute, foreground;
  - `run_f5.py`: the NEW keys (`oracle/out/json/arrow3_target_filter_b4`, `arrow3_target_filter_class`, `arrow3_target_dead_same_dispatch`, `arrow3_target_higher_slot`, `arrow3_zreach_16`, `arrow3_zreach_17`, `arrow3_target_sprite136`,
    `arrow3_air_east`, `arrow4_air_east`, `arrow3_map_edge_south`) each in two forms (plain, and suffix `_pf` = with the step of the generator that ends every logical tick of the binary, applied after the record of `rng1` and before the read of `rng0`
    of the next tick: rule of the plan, point 3), plus the `_pf` replay of the 17 frozen `arrow*` keys (the frozen keys themselves stay in `../bin/out/json/`);
  - `mutants_f5.py`: 15 one-word mutants of E17 and of the data the arrow reads, on the real machine (patched RAM), each compared with its control: `out/mutants_f5.{txt,json}`;
  - `manifest.py <copy>`: the SHA-1 of every output of the run (the frozen arrow keys and the wall sweep of `bin/out` that the run rewrites in the copy, and `f5/oracle/out`), sorted; `manifest.txt` is the manifest of two identical runs.
- `probes/`: `ZzF5T0Probes.cs.txt` (the probes, as written) and `probes-out.txt` (their output).
- `SHA1SUMS.txt`: SHA-1 of every file of this folder but `oracle/out` (the manifest covers it).
