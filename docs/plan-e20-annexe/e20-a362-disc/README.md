# E20.A362: the reading of the scene of the map 362 (2026-10-25)

Provenance: read-only discovery of 2026-10-25 (HEAD `8174c0cb`, production code identical up to `5e67722d` and to `bf1dcef5` for the lines used), paid into the repository by
T0a of E20.A362 (`docs/plan-e20-heros.md` section 1.30.1a, branch `chantier/theme2-attaques-objets`). Frozen evidence: paths and line numbers refer to that state; the
scripts still carry the absolute paths of the session's scratchpad (`D:/development/repo/...` for the data and the binary).

- `discovery/`: the report (`report-main.md`, facts A1 to A8, B1 to B12, C1 to C10, D1 to D6), its manifest, the decoder and scans (`bx.py`, `runpy.py`, `scan_*.py`),
  the outputs (`out/`, among them `map362_programs.txt`, the decoded programs of the map, and the oracle replays) and the new keys (`h3a-new/k_*.py`).
- `critic/`: the fresh critic (`report-main.md`, verdicts and corrections; **binding over the report where they differ**: the slide of the bomb, the tick of `0x8E`, the
  pc of F[3]), its scripts (`src/c_*.py` and `src/k_*.py`) and outputs (`out/`, among them `crit_olen_real_*`, `crit_olen_lid_flatfield_passthrough.txt`,
  `crit_olen_flight_slide_no_C62.txt`, `crit_w5_lid.txt`).
- `omitted.tsv`: what is NOT copied and why: the `head/` folders (the sources of `Alundra/` at HEAD `8174c0cb`, recoverable with `git show 8174c0cb:<path>`) and every file whose
  sha1 is already in `docs/plan-e20-annexe/` (the copies of the E20.h3 oracle, `../e20-h3/`, with the path of the twin). To replay a script of `critic/src/`, put the oracle files of
  `../e20-h3/critic/run/` beside it. Every `__pycache__` is left out.
- `SHA1SUMS.txt`: the sha1 of every file of this folder.

The replay of the critic's outputs and the table written before the execution are in `../e20-a362/`.
