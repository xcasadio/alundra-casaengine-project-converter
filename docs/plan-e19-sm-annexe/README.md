# E19 SM: per-opcode coverage of the event-program interpreter (2026-10-17)

Provenance: read-only discovery of 2026-10-16/17 (the author's fallback themes of the night of 16 to 17 October: "le joueur doit pouvoir
transporter un objet et monter sur une plateforme", "toutes les attaques du joueurs doivent etre implémentés", "implementer toutes les IA des
ennemis", "finir le moteur de script"), run while the DLL restructuring was paused (docs/plan-restructuration-dll.md, Q-R-2). Repository state
read: parent `7d776b3` (branch `chantier/restructuration-dll`), DLL under `Alundra/Scripts/`. Nothing in the repository was built, run or
changed by the discovery. The files are frozen evidence (D-R-6 style): line numbers and paths refer to that state; `SHA1SUMS.txt` lists the
copies. The plan holds the reconciled facts; where the critic refuted or could not verify a claim, the plan says so.

- `report.md`: the agent's final report (French; its own report file could not be written, the message is kept verbatim): structure,
  counts per state, the 29 opcodes skipped by size with their 510 reachable sites, partial and deliberate no-op opcodes, interpreter-level
  features, debts, proposed slices SM0 to SM10.
- `opcodes.tsv`: one row per opcode 0x00-0xFF (28 columns). `skipped-sites.tsv`: the 510 reachable skipped sites. `census-*.tsv`,
  `cycles.tsv`, `size-check.tsv`, `result-check.tsv`, `handler-addresses.tsv`, `decomp-unported.txt`, `tables.txt` and the JSON files:
  intermediate outputs. `census-sites.tsv` (110 140 reachable instructions, 1.7 MB) is not copied: `census.py` regenerates it.
- `check.md`: the fresh critic's verdict: reliable for the skipped sites and the counts; "FAITHFUL" only means "a case exists in Dispatch",
  not a line-by-line check against the binary; `0x40` documents a deliberate gap (`v1 >= 6`).
- Scripts: `census.py` (a port of `AlundraStoryChainOpcodeAudit.cs:128-338`), `aggregate.py`, `build_tsv.py` and the others; `sizecheck.py`,
  `resultcheck.py`, `binevidence.py` read `ALUN_CD.EXE` at the path they name (outside the repository). They write next to themselves: rerun
  them from a copy.
