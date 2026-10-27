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

## Added by T0 (2026-10-27, HEAD `45a1b50a`)

- `t0/`: the replays (`replay/`), the predictions written before the probes (`predictions.md`), the facts read in the keys (`key_facts.*`, `expected.json`), the table of the bytes read by the vectors (`mapping.md`), the probes (s1) to (s12) of the DLL
  (`probes/`), the starting suite (`suite/`), the experiment on the sub-pixel of the spawn (`spawn-subpixel/`) and `results.md` (the verdict: **STOP**, see its section 0). `t0/SHA1SUMS` lists every file.
- `rockfix/`: the reading of 2026-10-25 and the four "corrected" keys of D-H4C2-4 (**indicative, not faithful to the byte**; the keys of `disc/keys/` and `critic/keys/` stay the oracle of the original). `rockfix/SHA1SUMS` lists every file.
- The `SHA1SUMS` of this folder still lists the 179 files of the reading of 2026-10-18 only.

- `t1/` (2026-10-27, HEAD `4c7d7a47`): the phase A of E20.h4c2 - the table of the executable (`table_80023504.*`, `dump_table.py`), the independent model of the camera step (`camera_kick_model.py`), the replay of the probe (s2) after E20.e0a6 (`s2-replay/`), the red list before the code, and `results.md` (59 test cases, red before / green after, the measured final positions of (s10b), the deviations). `t1/SHA1SUMS` lists its files.
