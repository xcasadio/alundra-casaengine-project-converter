# E20.f4 to E20.f6b - state at the HEAD 3c923cf (2026-10-18)

Read-only survey of the plan sections 1.20 to 1.22 (`docs/plan-e20-heros.md`) against the DLL after the restructuring, one survey per
slice (`state.md`) and its counter-check (`check.md`, which prevails where they differ). The revision block at the head of section 1.20
("Revision au HEAD (2026-10-18)") is written from these files.

- `f4/`, `f5/`, `f6/`: the survey and the counter-check of each slice, with the small scripts they ran (line mapping old -> HEAD,
  oracle row readers, name checks).
- `f5check/`, `f6check/`: the counter-checks' own scripts.

Not versed (each can be rebuilt from git): `f4/tree-proof/` (a `git archive` of the HEAD plus one probe file), `f6check/old9/` and
`f6check/oldbe/` (the old DLL files at `9ecc7d5` and `be9a067`), and the copies of the plan sections (`f4/sec120.txt`,
`f4check/s120_*.txt`, `f6/s12x-*.md`, `f6/plan-9ecc7d5.md`, `f6check/old*.md`, `f6check/new_s.md`).

The scripts were run from the session scratchpad; their paths are absolute to it. `SHA1SUMS` lists every file here.
