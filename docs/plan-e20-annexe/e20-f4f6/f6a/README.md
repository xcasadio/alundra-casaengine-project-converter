# E20.f6a T0 (2026-10-23): oracle, probes, predicted list

Plan: `docs/plan-e20-heros.md` section 1.22 / 1.22.1 ("Revision au HEAD" rules, points 1 to 10, bullet "f6a"). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Repository HEAD at the start: `e295875a` (branch `chantier/theme1-porter-plateforme`). T0 writes no DLL code of its own: the probes of the DLL ran in a throwaway test file (`probes/ZzF6aT0Probes.cs.txt`), removed before the commit.
The annex above (`../bin`, `../critic`, `../dll`, `../tests`, `../state-3c923cf`, `../f4`, `../f5`) is frozen and unchanged.

- `results.md` (French): the result of each point of T0, the values the tests read, the measured list of the moving tests (empty), the deviations from the plan.
- `oracle/`: the scripts of f6a (they import the frozen harness of `../bin` and write under `oracle/out`; run each from a COPY of the annex, never in place):
  - `run_all.sh`: the complete run (the frozen replay `bin/run_balls.py`, `run_balls_mixed.py`, `run_flames.py`, then the two scripts below), about one minute, foreground;
  - `run_f6a.py`: the NEW keys (`fire11_dry_corner_c6r5`, `_c7r5`, `_c6r6`, `_c7r6`, `fire11_step16_water`, `ice14_step16_water`): `oracle/out/json` and `oracle/out/traces`;
  - `mutants_f6a.py`: 10 one-word mutants of the tile-attribute pass `0x80038064` (the four-corner rule), on the real machine (patched RAM), each compared with its control: `out/mutants_f6a.{txt,json}`, all killed;
  - `manifest.py <copy>`: the SHA-1 of every output of the run (the ball keys and traces of `bin/out` that the run rewrites in the copy, and `f6a/oracle/out`), sorted; `manifest.txt` is the manifest of two identical runs (79 lines).
- `probes/`: `ZzF6aT0Probes.cs.txt` (the probes, as written; they use `BallRun` and `BallOracle` of `Alundra.Tests/Ai/Projectiles/`, committed by T1) and `probes-out.txt` (their output).
- `SHA1SUMS.txt`: SHA-1 of every file of this folder but `oracle/out` (the manifest covers it).
