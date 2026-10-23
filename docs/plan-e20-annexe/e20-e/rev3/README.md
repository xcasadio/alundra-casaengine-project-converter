# E20.e revision 3, T0 (2026-10-23): oracle, measurements, predicted lists

Plan: `docs/plan-e20-heros.md` section 1.27 (revision 3, READY). Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Repository HEAD at the start: `9c1f6fe3`
(branch `chantier/theme1-porter-plateforme`). No DLL code is written by T0: the two probes of the DLL (`probes/ZzT0Probes.cs.txt`) ran in a throwaway test file, removed before the commit.
The earlier reading (`../report.md`, `../check.md`, `../work/`, `../check/`, `../state-978608d/`) is frozen and unchanged.

- `results.md` (French): the result of each point (a) to (k) of T0, the next ADR number, the predicted list of the existing tests that move, the deviations from the plan.
- `oracle/`: the scripts. `tools/` is a byte copy of the machine of the E20 oracles (`../../e20-f3/oracle/tools`: `lib emu machine mips bank world c1world pworld wave_run`); `vec/` holds byte copies of
  `../work/emu_cate.py` and `../work/exelib.py` (the mini MIPS interpreter that runs the real instructions `0x8003A7B0-0x8003A9DC`). Run each script from a COPY of `oracle/` (they write `./out/`), with
  `python -B <script>.py`, foreground; they read the binary and `data-extracted/data/map_N.json` at the absolute paths `tools/lib.py` and `cutlib.py` name.
  - `cutlib.py`: shared helpers (real map cells, spies on the loot and on the cut, the step of the generator that ends every logical tick, the loot model `choose`).
  - `export_vectors.py` (25 s): the 4 000 vectors, `../vectors/cate-vectors.json` (2.4 MB); also checks that the two-step form of the plan (S-E3) equals the interleaving of the binary.
  - `cut_m14.py` (10 s): the oracle of the world tests, `out/cut_m14.json` and `.txt`.
  - `zedge.py` (13 s): Q-E6, the frame of Z of the band test. `latency.py` (3 s): the one-tick latency of the stored box. `loot_check.py` (1 s): the real roll costs 1 or 2 words.
  - `heroattack_m10.py` (1 s): the existing test `HeroAttackTests` of the map 10, run on the binary.
- `vectors/cate-vectors.json`: the oracle read by the tests of T1 (`Alundra.Tests/Support/CateVectors.cs`).
- `probes/`: the DLL probes and their outputs (the visual completeness of the maps 14, 10 and 16; the generator tick by tick in the emptied bench).
- `t1-results.md`, `t1-red-names.txt`: T1 (phase A). `t2-results.md`, `t2-red-names.txt` (2026-10-23): T2, the world tests of the cut on the real map 14 (`Alundra.Tests/Cells/TileEffectCutWorldTests.cs`), red before T3, and the two scenarios `beside` and `idle` added to `oracle/cut_m14.py`.
- `t3-results.md` (2026-10-23): T3, phase B (the two production calls, the preset `herbe-m14`), the tests that moved against the predicted list, the suites.
- `SHA1SUMS.txt`: SHA-1 of every file above (LF).
