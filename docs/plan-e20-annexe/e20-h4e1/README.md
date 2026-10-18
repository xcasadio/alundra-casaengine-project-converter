# E20.h4e1 - wind 1 (item 0x31): the driver 0x80035EB0, the distance key and the birth floor (2026-10-18)

Provenance: read-only discovery of 2026-10-18 for `docs/plan-e20-heros.md` section 1.31.7, while the DLL restructuring waits for the author.
Repository read at `ed60a0eb` (no change under `Alundra/`, `Alundra.Tests/` or `alundra-project/Data` since `f81304b`); binary `ALUN_CD.EXE`
(France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or the binary was changed.

- `disc/`: the discovery (`report.md`, the agent's final message, kept verbatim, in French), its scripts (`wind1_*.py`), tools, listings and
  checks (`out/`), the 42 remade world keys (`keys/`, `MANIFEST.sha1`), the 800 vectors (`vec/`), the harness copies (`h4o/`, `h4b_h/`) and the
  h4 keys remade without the slot index (`stale/`).
- `critic/`: an independent adversarial critic with its own MIPS interpreter (`mymips.py`) and model (`check.md`, its final message). **Its verdict
  prevails**: the driver, query, sort, keys and vectors CONFIRMED; the discovery's "dead branch" (D9, the on-ground test at birth) REFUTED (the
  creation writes `+0x144` through 0x80038064; `work/cliff_scen.py`); the twelve keys it adds are in `critic_keys/` (cliffs, the gate-off
  interruptions and full table, a second seed).

Left out: the critic's full rerun of the 42 keys (`keys_rerun/`, byte-identical to `disc/keys/`, its manifest `MANIFEST_rerun.sha1` kept), its
copy of the stale keys (`keys_stale/`, the same as `disc/stale/`) and its regenerated vector file (`vec_regen.json`, SHA-1 identical to
`disc/vec/vec_wind1_80035EB0.json`). The scripts read the binary at the absolute path they name (outside the repository) and write next to
themselves: rerun them from a copy, never in place. `SHA1SUMS` lists every file here.
