# E20.g1a, g2a, g2b: the slices' and the binary read's DLL and test citations re-checked at HEAD a3ccb75 (2026-10-18)

Provenance: read-only workflow of 2026-10-18, run after the DLL restructuring changed the paths and names that sections 1.24.0 to 1.24.3 of
docs/plan-e20-heros.md and ../report.md, ../check.md cite. Repository read at `a3ccb75` (branch `chantier/restructuration-dll-r3`). Nothing
was built, run or changed. Frozen evidence; `SHA1SUMS.txt` lists the files.

- `g1a/state.md` (French): every citation of 1.24.0, 1.24.1 and the D-G/O-G points they rely on, translated to its HEAD file:line; the
  verdict at HEAD of every claim; the pinning tests; the consequences of section 1.28 (E20.g3); the final names. `g1a/check.md`: an
  independent re-check, binding where it differs (`g1a-verif/`: its scripts).
- `g2/state.md` and `g2/check.md` (French): the same for 1.24.2 (g2a) and 1.24.3 (g2b) (`g2v/`: the checker's scripts).
- The scripts read the repository at absolute paths: rerun them from a copy.
