# E20.h4d1 - fire 2 (item 0x30): the driver 0x80035C64, E28, the cancel 0x80034E08 and the shake buffer (2026-10-18)

Provenance: read-only discovery of 2026-10-18 for `docs/plan-e20-heros.md` section 1.31 (E20.h4, the magic tomes), while the DLL
restructuring waits for the author. Repository read at `023b33ce` (no change under `Alundra/` or `Alundra.Tests/` since `f81304b`); binary
`ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or the binary was changed.

- `disc/`: the discovery (`report.md`, the agent's final message, kept verbatim), its scripts, listings, keys and vectors.
- `critic/`: an independent adversarial critic with its own MIPS interpreter and models (`check.md`, its final message). **Its verdict prevails.**

Notes: the discovery's keys (`disc/keys/`, `vec_e28_8007FB58.json` 202 vectors); the critic REFUTES its chronology (the slot-index artifact: one tick late) and gives the faithful keys (`critic/keys_faithful/`, made by `critic/c/h4o/fire2_key_c.py` and `fire2_targets_c.py`), the zero-limit preset key and the edge targets.

Common finding (all three slices): the world keys of `docs/plan-e20-annexe/e20-h4/work/results/` are one tick late for the entities born in a
never-used slot, because the h4 harness zeroes the 64 slots instead of writing the slot index (`+0 = i`, map-load init 0x8003B2BC); the keys of
this annex are remade with the index. The duplicate copies of the e20-h4b vectors made by the agents are left out (they are in
`docs/plan-e20-annexe/e20-h4b/bin/vec/`). The scripts read the binary at the absolute path they name (outside the repository) and write next to
themselves: rerun them from a copy, never in place. `SHA1SUMS` lists every file here.
