# E20.e0d: the treasure chest and the magic seed read in the binary (2026-10-24)

Provenance: read-only discovery of 2026-10-24 (repository HEAD `43a47432`), paid into the repository by T0 of E20.e0d (2026-10-25, branch
`chantier/theme2-attaques-objets`). Binary `ALUN_CD.EXE` (France), read with capstone and executed in the MIPS interpreter copied from the E20.e0c
annex. Nothing in the repository or in the binary was changed by the discovery. Frozen evidence: paths, addresses and line numbers refer to that state.

- `report-main.md`: the reading agent's report (French), kept verbatim.
- `critic-main.md`: a fresh critic's report (French). Verdict CONFIRMED; its corrections are binding over the report where they differ.
  Notably: `Block` does not remove entities from the visible list (they stay drawn, only their updates stop); `[0x800DC4C4]` is the `+4` field of a
  map-change request structure at `0x800DC4C0`, not a "game ending" flag.
- `critic/`: the critic's scripts (replays of F255, C255 and the seed; census), `H.json`.
- `*.py`, `*.out`, `*.dis`, `*.tsv`: the reading agent's scripts, outputs and disassembly listings (`f255.dis`, `a255.dis`, `c255_whole.dis`,
  `t5_initcontents.dis`, `t9_seed.dis`, `vessel_body.dis`, ...). `f255_vectors.tsv` is the 244-vector table of F255 for the tests of the DLL;
  `t7_chests.tsv` is the census of the 420 chests.

Left out on purpose (as announced by the plan, section 1.26.2, T0 (b)): `emu/` and `e0c_copy/` (copies of the MIPS interpreter and of helpers that
already live in `docs/plan-e20-annexe/e20-e0c/work/` and `.../check/`; `emu/emu.py` and `emu/lib.py` equal `e20-e0c/work/emu/emu.py` and `lib.py` once
the line ends are ignored), and every `__pycache__`.

The scripts still carry the absolute paths of the session's scratchpad (`HERE = '.../scratchpad/t2-chest'`, `sys.path.insert(... + '/emu')`) and read
the binary and `data-extracted/` at absolute paths. To replay them, copy this folder next to a copy of `emu/` (from `e20-e0c/work/emu/`) and replace
the scratchpad path (see `../e20-e0d/results.md`, section "(c) replay", for the exact run and its result). `SHA1SUMS.txt` lists the files.
