# E20.g1a and g2a: carrying and throwing read in the binary (2026-10-17)

Provenance: read-only discovery of 2026-10-17, run while the DLL restructuring was paused (`docs/plan-restructuration-dll.md`, Q-R-2), to
prepare the first slices of the author's fallback themes of 2026-10-16. Repository read at `2010ec3`; binary `ALUN_CD.EXE` (France), SHA-1
`a705f8dab665645af5dfaf8fb348c7926b5caa58`, read with capstone and executed in MIPS interpreters. Nothing in the repository or the binary was
changed. Frozen evidence: paths and line numbers refer to that state; `SHA1SUMS.txt` lists the copies.

- `report.md`: the reading agent's report (French), kept verbatim (its own report file could not be written; the final message is the report).
- `check.md`: an independent critic's report (French): it re-disassembled the decision-carrying facts and ran the real handlers in its own
  MIPS interpreter against the reading's models; its verdict, corrections and additions are binding over the report where they differ.
- `work/`: the reading agent's scripts, listings and outputs; `check/`: the critic's (caches `*.pkl`, `cache/`, copied inputs and a 6 MB full
  disassembly listing are left out: the scripts regenerate them). The scripts read the binary at the absolute path they
  name (outside the repository) and write next to themselves: rerun them from a copy, never in place.
