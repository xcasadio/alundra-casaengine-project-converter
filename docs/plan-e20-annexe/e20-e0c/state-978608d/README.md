# E20.e0c: the binary read's DLL and test citations re-checked at HEAD 978608d (2026-10-17/18)

Provenance: read-only workflow of 2026-10-17/18, run after the DLL restructuring (R0 to R4, R6; docs/plan-restructuration-dll.md) changed
the paths and names the binary read (../report.md, ../check.md) cites. Repository read at `978608d` (branch `chantier/restructuration-dll-r3`).
Nothing in the repository was built, run or changed. Frozen evidence: paths and line numbers refer to that state; `SHA1SUMS.txt` lists the files.

- `state.md`: one agent's translation (French) of every DLL and test citation of the read to its HEAD file:line, the verdict at HEAD of
  every claim about the DLL, the tests that pin the stubs, and what changed since the read.
- `check.md`: an independent agent's re-check (French) of every row of `state.md`; its errors, omissions and corrections are binding over
  `state.md` where they differ.
- `scripts/` (and `check-scripts/` for e0c): the read-only scripts of both agents; they read the repository at absolute paths: rerun them from a copy.
