# E20.h1 - the five instant items (herb, L potion, extract, cure ale, S potion) (2026-10-18)

Provenance: read-only discovery of 2026-10-18 for `docs/plan-e20-heros.md` section 1.30.5, while the DLL restructuring waits for the author.
Repository read at `e8087d74` (the critic at `317b3360`; no change under `Alundra/`, `Alundra.Tests/` or `tools/` between them); binary
`ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or the binary was changed.

- `disc/`: the discovery (`report.md`, the agent's final message, kept verbatim, in French) and its oracle (`oracle/`: listings, the vector
  generator and replay, the model and its 141 mutants, the 30 world keys with the slot index written and the per-frame step, the 80-state scan,
  the Start + Select replay; `bash oracle/run_all.sh` regenerates everything in `oracle/out/`).
- `critic/`: an independent adversarial critic with its own loader, listings, MIPS interpreter (`c_cpu.py`) and model (`c_keycheck.py`)
  (`check.md`, its final message). **Its verdict prevails**: every behaviour fact CONFIRMED; one P2 (15 inventory-table-size vectors cannot be
  adapted) and P3/P4 corrections, applied in the plan; deep water and walking measured (`c_swim.py`, `c_move.py`, `out/`). `rerun/oracle_copy/`
  is its working copy of the discovery's harness (its scripts import it); the full rerun output (`rerun/oracle_full/`) is left out, its
  checksums being identical to `disc/oracle/out/SHA1SUMS_vectors_keys.txt`.

The scripts read the binary at the absolute path they name (outside the repository) and write next to themselves: rerun them from a copy, never
in place. `SHA1SUMS` lists every file here.
