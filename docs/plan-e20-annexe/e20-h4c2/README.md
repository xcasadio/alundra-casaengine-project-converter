# E20.h4c2 - earth 2 (item 0x2C): the driver 0x800354D0, E13, the cancel 0x80034BDC and the camera kick (2026-10-18)

Provenance: read-only discovery of 2026-10-18 for `docs/plan-e20-heros.md` section 1.31 (E20.h4, the magic tomes), while the DLL
restructuring waits for the author. Repository read at `023b33ce` (no change under `Alundra/` or `Alundra.Tests/` since `f81304b`); binary
`ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Nothing in the repository or the binary was changed.

- `disc/`: the discovery (`report.md`, the agent's final message, kept verbatim), its scripts, listings, keys and vectors.
- `critic/`: an independent adversarial critic with its own MIPS interpreter and models (`check.md`, its final message). **Its verdict prevails.**

Notes: keys remade with the slot indexes and the per-frame generator step (`disc/keys/`, `MANIFEST.md5`, `driver_2c_cases.json` 609 cases); the critic adds the mid-map and plateau keys and the cancel sounds (`critic/keys/`) and corrects the camera kick (a lasting drift, not a bump that returns).

Common finding (all three slices): the world keys of `docs/plan-e20-annexe/e20-h4/work/results/` are one tick late for the entities born in a
never-used slot, because the h4 harness zeroes the 64 slots instead of writing the slot index (`+0 = i`, map-load init 0x8003B2BC); the keys of
this annex are remade with the index. The duplicate copies of the e20-h4b vectors made by the agents are left out (they are in
`docs/plan-e20-annexe/e20-h4b/bin/vec/`). The scripts read the binary at the absolute path they name (outside the repository) and write next to
themselves: rerun them from a copy, never in place. `SHA1SUMS` lists every file here.
