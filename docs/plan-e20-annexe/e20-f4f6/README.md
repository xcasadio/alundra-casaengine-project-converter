# E20.f4, f5, f6: the charged arrow (C94, E1), the arrows (E17) and the fire and ice balls (C60, E9) read in the binary (2026-10-15)

Provenance: read-only discovery of 2026-10-15 at HEAD `be9a067` (sections 1.20 to 1.22 of docs/plan-e20-heros.md): three readings (binary,
DLL impact, tests inventory) shared by f4, f5 and f6, then a shared adversarial critic. Binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository, the binary or the data was changed. These files lived only in the
session scratchpad (`disc-e20f4f6/`) until 2026-10-18, when they were copied here unchanged (caches `*.pkl` left out) because the slices'
T0 and tests read their keys: frozen evidence; paths and line numbers refer to that state; `SHA1SUMS.txt` lists the files.

- `bin/`: the binary reading: harnesses (`fw.py`, `run_arrows.py`, `run_charged.py`, `run_balls*.py`, `run_flames*.py`, ...), censuses,
  `out/` (text and `out/json/` keys such as `charged7_*.json`), `f3tools/` (a copy of `../e20-f3/oracle/tools/` the harnesses import),
  `disasm/`, `src/`.
- `dll/`, `tests/`: the DLL impact and the tests inventory.
- `critic/`: the critic's own models, scenarios and mutants (`bin/`, `mine/`).

The scripts read the binary and the data at the absolute paths they name and write next to themselves: rerun them from a copy, never in place.
