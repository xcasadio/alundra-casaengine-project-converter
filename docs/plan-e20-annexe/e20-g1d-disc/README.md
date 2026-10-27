# E20.g1d - the discovery and its critique (read-only, 2026-10-27)

Poured by T0 of E20.g1d (`docs/plan-e20-heros.md` section 1.24.8) from the scratchpad of the main session (`t2-feedback/`).

- `disc_carried-draw.md` - the discovery (report); `critic_carried-draw.md` - its fresh critique (CONFIRMED with four corrections, all taken in the plan).
- `carried-draw/` - the discovery's scripts (`bomb_keys.py`, `bomb_keys2.py`, `crate_keys.py`, `order_model.py`, ...), their outputs (`out_*.txt`), the disassemblies (`dis_*.txt`) and the reading copies of the HEAD (`*.head.cs`, `engine/`).
- `carried-draw-critic/` - the critique's scripts, probes and outputs (`throw_probe.py`, `neighbour_probe*.py`, `out_*_rerun.txt`, ...).

Left out on purpose: the copies of the oracles (`oracle_h3`, `oracle_h3bin`, `oracle_g1a`: they are the annexes `docs/plan-e20-annexe/e20-h3/critic/run`, `e20-h3/bin/run` and `e20-g1a/work/orc`, never edited) and the `__pycache__` directories. The scripts of this folder find their oracle in a sibling directory of those names; the replayed keys of the slice are in `../e20-g1d/keys/` (they take the oracles from the annexes through `G1D_ORC`).
