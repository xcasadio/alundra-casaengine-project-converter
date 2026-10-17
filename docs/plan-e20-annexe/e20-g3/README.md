# E20.g3 and E20.g4: liftables solid again, and the barricade of map 392, read in the binary and the DLL (2026-10-17/18)

Provenance: read-only discovery of the night of 17 to 18 October 2026 (the author's fallback theme "le joueur doit pouvoir transporter un objet
et monter sur une plateforme"), run while the DLL restructuring waited for the author (docs/plan-restructuration-dll.md, Q-R-3). Repository
read at `c84fd72`..`b6a2f92` (no DLL source changed in between); binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`, read with capstone and executed in the MIPS interpreters of `docs/plan-e20-annexe/e20-g1a/work/`
(copied, unmodified; the copies are left out here). Nothing in the repository, the binary or the export was changed. Frozen evidence: paths and
line numbers refer to that state; `SHA1SUMS.txt` lists the files.

- `bin/`: the binary reading (French `report.md`): the collidable list and the entity obstacle test, the contact `+0x130` and the lift rule,
  the release flags of C23/C70/C4/C73, map 392 (geometry, the Y window of the jump and of the lift, full routes), the 17 liftable headers.
- `dll/`: the DLL and tests reading (French `report.md`): the one production site of the deviation, every path that treats liftables, the
  tests that pin it, the chain maps and arcs near liftables, the converter (`all_records.json` is left out: `census_liftables.py` rebuilds it).
- `critic/`: an independent re-derivation of the decision-carrying binary facts (its corrections R1-R6 are binding over `bin/`).
- `critic-recon/`: a rule-by-rule reconciliation of the binary with the DLL at HEAD.
- `critic-complet/`: a completeness critic for writing the slices (its corrections are binding over both readings; `dllcopy/` left out).

The scripts read the binary, `data-extracted/` and `alundra-project/` at the absolute paths they name and write next to themselves: rerun them
from a copy, never in place; the oracle scripts expect a copy of `docs/plan-e20-annexe/e20-g1a/work/` as `g1a/` next to them.
