# E20.e0c revision 3: the placed-object branch read in the binary and counted in the data (2026-10-22)

Provenance: read-only reading of 2026-10-22 at HEAD `1d0b6953`; binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`, read with capstone and run in the annex's MIPS interpreter. Nothing in the repository or the
binary was changed. Committed with revision 3.1 of the plan (`docs/plan-e20-heros.md` §1.26). `../SHA1SUMS.txt` lists the files.

- `report.md`: the reading (French), verbatim: binary facts, corrections to revision 2, data census, DLL state, points for the author,
  risks.
- `run_c255.py`, `run_c255_out.txt`: eight directed scenarios of C255 and A255 run on the real code (journal per tick).
- `census_placed.py`, `census_placed_out.txt`, `placed.json`: the 152 placed objects counted on `data-extracted/`.
- `census_exported.py`, `census_exported_out.txt`: the same count on the exported project (what the DLL reads).
- `fdis.py` (whole-function disassembly) and its output `c255_full_fdis.txt`; `fulldis.py` (full linear listing, regenerated, not kept).
- The harness the scripts import (`exelib.py`, `lh.py`, `models.py`, `c255model.py`, `give_model.py`, `mipsdis.py`, `emu/`) and the
  1 500-vector check `t_c255.py` were byte-identical copies of `../work/` (checked when this folder was committed): rerun from a copy
  of `../work/` with these files added. The scripts read the binary and the repository at absolute paths and write next to themselves.

The SHA-1 in `../SHA1SUMS.txt` are those of the working-copy files when this folder was committed. The script outputs written on Windows
(`*_out.txt`, `placed.json`, `c255_full_fdis.txt`) carry CRLF line endings there, the other files LF; git stores LF (`core.autocrlf=true`). Compare with line endings
normalized, or check out with the same setting.
