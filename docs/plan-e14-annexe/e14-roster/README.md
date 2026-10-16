# E14 roster: the complete census of the native sprite AI (2026-10-17)

Provenance: read-only discovery of 2026-10-16/17 (the author's fallback themes of the night of 16 to 17 October: "le joueur doit pouvoir
transporter un objet et monter sur une plateforme", "toutes les attaques du joueurs doivent etre implémentés", "implementer toutes les IA des
ennemis", "finir le moteur de script"), run while the DLL restructuring was paused (docs/plan-restructuration-dll.md, Q-R-2). Repository state
read: parent `7d776b3` (branch `chantier/restructuration-dll`), DLL under `Alundra/Scripts/`. Nothing in the repository was built, run or
changed by the discovery. The files are frozen evidence (D-R-6 style): line numbers and paths refer to that state; `SHA1SUMS.txt` lists the
copies. The plan holds the reconciled facts; where the critic refuted or could not verify a claim, the plan says so.

- `report.md`: the agent's full report (French): rule of the native dispatch re-established in the binary and the data, counts, classification,
  unported enemies and bosses, handler shapes, shared prerequisites, proposed slices.
- `roster.tsv` (395 sprite types, 39 columns), `handlers.tsv` (206 handler keys, 34 columns), `slices.tsv` (25 proposed slices).
- `check.md`: the fresh critic's verdict (reliable for the dispatch rule, the totals and the list of unported enemies; reservations listed).
- Scripts: `run_all.py` runs `extract_maps.py`, `exe_handlers.py`, `census.py`, `scan41.py`, `build_outputs.py`, `slices.py`, `report_gen.py`;
  `exelib.py` and `mipsdis.py` read `ALUN_CD.EXE` (France) with capstone at the path written in `exelib.py` (outside the repository). They
  write next to themselves (a `cache/` folder and the outputs): rerun them from a copy, never in place.
