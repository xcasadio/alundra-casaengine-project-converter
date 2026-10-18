# E20.h4d2 - fire 1 (item 0x2F): the driver 0x80035A84, the column, C63, E3 for type 23 and the cancel 0x80034D2C (2026-10-18)

Provenance: read-only discovery of 2026-10-18 for `docs/plan-e20-heros.md` section 1.31.6, while the DLL restructuring waits for the author.
Repository read at `ed60a0eb` (no change under `Alundra/`, `Alundra.Tests/` or `alundra-project/Data` since `f81304b`); binary `ALUN_CD.EXE`
(France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or the binary was changed.

- `disc/`: the discovery (`report.md`, the agent's final message, kept verbatim, in French), its listings (`lst/`), models and vector tools (`tools/`),
  the four vector files (`vec/`), the 30 remade world keys (`keys/`, `manifest.txt`, `SENSITIVITY.txt`), the corrected h4 bench (`h4o/`, with
  `fire1_lib.py`, `fire1_make_keys.py`, `probe_count.py`), the e20-h4b vector machine copy (`vech/`) and the DLL state (`dll/state.md`).
- `critic/`: an independent adversarial critic with its own MIPS interpreter (`scripts/myemu.py`) and listings (`check.md`, its final message).
  **Its verdict prevails**: all 22 binary facts CONFIRMED, the keys confirmed; two P2 (the identity words have no DLL member: the adapter rules; no
  relief key: `keys_proposed/` holds three) and P3 corrections, applied in the plan.

Left out: the discovery's unused reference copy of the f4f6 bench (`fcopy/`) and the critic's working copies of the bench (`run_mycpu/`,
`run_theirs/`; its regenerated keys were byte-identical, `results/manifest_rerun_*`). The scripts read the binary at the absolute path they name
(outside the repository) and write next to themselves: rerun them from a copy, never in place. `SHA1SUMS` lists every file here.
