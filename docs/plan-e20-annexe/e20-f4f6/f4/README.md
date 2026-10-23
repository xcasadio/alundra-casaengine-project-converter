# E20.f4 T0 (2026-10-23): oracle, probes, predicted lists

Plan: `docs/plan-e20-heros.md` section 1.20 ("Revision au HEAD" rules) and 1.20.1. Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Repository HEAD at the start: `c7c77eed` (branch `chantier/theme1-porter-plateforme`). T0 writes no DLL code: the three probes of the DLL ran in a throwaway test file
(`probes/ZzF4T0Probes.cs.txt`), removed before the commit. The annex above (`../bin`, `../critic`, `../dll`, `../tests`, `../state-3c923cf`) is frozen and unchanged.

- `results.md` (French): the result of each point of T0, the values the tests read, the measured list of the moving tests, the deviations from the plan.
- `oracle/`: the scripts of f4 (they import the frozen harness of `../bin` and write under `oracle/out`; run each from a COPY of the annex, never in place):
  - `run_all.sh`: the complete run (the frozen replay `bin/run_charged.py`, `bin/sweep_wall_charged.py`, then the two scripts below), about one minute, foreground;
  - `run_charged_pf.py`: the keys `charged7_{east_wall,grass,map_edge,vs_target}_pf` (suffix `_pf`): the charged shot with the step of the generator of the end of the frame
    (`s <- s * 0x7D2B89DD + 0xE06A02E7`, `0x8002BD0C-0x8002BD34`) applied after the record of `rng1` of each tick and before the read of `rng0` of the next one (rule of the plan, point 3);
  - `mutants_f4.py`: 13 one-word mutants of C94, E1 and of the data the arrow reads, on the real machine (patched RAM), each compared with its control: `out/mutants_f4.{txt,json}`;
  - `manifest.py <copy>`: the SHA-1 of every output of the run, sorted (`manifest.txt` is the manifest of the two identical runs).
- `oracle/out/json/*_pf.json`, `oracle/out/traces/*_pf.txt`: the new keys. The frozen keys the tests also read stay in `../bin/out/json/` (`charged7_*.json`) and
  `../bin/out/sweep_wall_charged.txt` (the 12 starts of the wall).
- `probes/`: `ZzF4T0Probes.cs.txt` (the probes, as written) and `probes-out.txt` (their output).
- `SHA1SUMS.txt`: SHA-1 of every file of this folder (LF).
