# E20.h4d2 - fire 1 (item 0x2F): the driver 0x80035A84, C63 + E3 for the type 23, the cancel 0x80034D2C (2026-10-18)

Read-only discovery for `docs/plan-e20-heros.md` section 1.31 (E20.h4). Repository read at `ed60a0eb` (no change under `Alundra/`, `Alundra.Tests/` or `alundra-project/Data` since `f81304b`); binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked). Nothing in the repository or the binary was changed; everything here was written under this folder. Python is always run with `-B`, from a COPY of a harness.

- The report (French) is the final message of the session (the tool refused report files); the evidence is in the folders below.
- `lst/` : my own capstone listings (`tools/mydis.py`) of the functions of the slice, and `tables_checked.txt` (jump tables, handler tables, XA byte table read from the executable).
- `tools/f1model.py` : MY OWN Python models of the driver, the guard + general cancel, the fire-1 cancel, C63 (with the query, its sort and the turn), E3, written from my listings (not from the e20-h4 models).
- `tools/vec_fire1.py` : records the replayable vectors on the REAL MIPS code (TM machine of `vech/`, copy of e20-h4b/bin/h) and compares them with `f1model.py`; replays every file from an EMPTY memory.
  `tools/vec_selftest.py` : uses the vector files the way a C# adapter will (reads -> sparse memory -> model -> compare writes / journal / return) and runs 36 mutants of the models against them.
  `tools/turn_stats.py` : the turn events of the world keys classified by the numeric gap (the long way).
- `vec/` : `vec_fire1_driver_80035A84.json`, `vec_cancel_fire1_80034D2C.json`, `vec_e3_8007EDA0.json`, `vec_c63_8007AC60.json` (format of `e20-h4b/bin/vec/README.txt`; extra fields `case`, `kind`, `binary_only`).
- `h4o/` : copy of the e20-h4 harness with the slot-index correction (`e20-h4d1/critic/c/h4o`). New: `fire1_lib.py` (the bench: slot index written, `+0x48 = -1`, per-frame step `_pf`, targets, interruptions, patches),
  `fire1_make_keys.py` (writes the keys + `manifest.txt`), `probe_count.py` (stored count versus on-demand highest slot at every call of the query).
- `keys/` : the 30 world keys (`fire1_*_pf.json`), `manifest.txt` (MD5), `SENSITIVITY.txt`. `keys_run2/manifest.txt` : the second run (identical).
- `vech/` : copy of the e20-h4b machine and tools used by the vector generator. `fcopy/` : copy of the f4f6 machine (`e20-h4c1/disc/fcopy`), kept for reference (not used by the final keys).
- `dll/state.md` : the DLL at HEAD (file:line), what exists, what moves.

Replay (from COPIES, never in place):
    cd h4o && python -B fire1_make_keys.py OUTDIR      # ~4 min, twice: identical manifest.txt
    cd tools && python -B vec_fire1.py OUTDIR          # ~2 min, twice: identical MD5 of the four files; prints coverage and the model-versus-binary result
    cd tools && python -B vec_selftest.py VECDIR       # mutants of the models killed by the vectors
The scripts read the binary and the data at the absolute paths they name (outside the repository) and write only where they are told.
