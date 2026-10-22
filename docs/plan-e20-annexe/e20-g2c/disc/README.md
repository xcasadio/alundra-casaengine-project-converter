# E20.g2c: read-only T0 of 2026-10-22 (C4, the plate objects, the torches)

Provenance: read-only discovery of 2026-10-22 at HEAD `1d0b6953` (branch `chantier/theme1-porter-plateforme`); binary `ALUN_CD.EXE`
(France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, disassembled with capstone and run in the MIPS machine of
`../../e20-g1a/work/`. Nothing in the repository or the binary was changed. Plan: `docs/plan-e20-heros.md` §1.24.5. `SHA1SUMS.txt` lists
the files.

- `report.md`: the discovery's report (French), verbatim: verdicts, census, the decoded body of C4, replays, plates, touch, refused
  headers, torches, the DLL at HEAD, proposed split, corrections to the sketch, questions, unknowns, how to reproduce.
- `c4_full.py` (model of C4 against the real code: vectors, mutants, goldens), `c4_full_out.txt`, `c4_goldens.json` (1 244 vectors,
  SHA-256 `59167009656660d68b4847485b01f1b7a8426f914429525bcccb74d6b267fe90`), `c4_scen.py` (scenarios S1 to S10) and `out/` (their
  outputs, hashed in `out/MANIFEST_out.sha256`).
- Censuses: `census_c4.py`/`.txt`, `census_scripted.py`, `cell_census.py`, `rest_census.py`/`.txt`, `refused_headers.py`/`.txt`,
  `names.json`, `dump_map.py`, `dump_prog.py`. Listings: `dis_c4.txt`, `dis_c70.txt`, `dis_e20.txt`, `dis_phys.txt`, `disasm_range.py`.
- `work/`: only the 0 files of the discovery's MIPS machine that differ from (or are absent in) `../../e20-g1a/work/`
  (203 files were byte-identical and are not copied). To rerun: copy `../../e20-g1a/work/` to a scratch folder, overlay this `work/`,
  put the scripts next to it as in the report's §14. The scripts read the binary and the repository at absolute paths.

The SHA-1 in `SHA1SUMS.txt` are those of the working-copy files when this folder was committed (outputs written on Windows may carry
CRLF there; git stores LF with `core.autocrlf=true`): compare with line endings normalized.
