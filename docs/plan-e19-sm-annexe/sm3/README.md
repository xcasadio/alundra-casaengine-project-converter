# E19.sm3 - preparation of the executable slice: 0xA9 0xAA 0xB2 0xB4 0xB5 0xB6 0xB7 0x9E, the contact, group and ForceZ predicates (42 sites) (2026-10-18 to 2026-10-22)

Provenance: read-only preparation for `docs/plan-e19-opcodes.md` section 1.4 (E19.SM), after the state pass at HEAD
(`../state-317b336/`) and E19.sm0 (READY, section 1.4.1). Repository read at `36ac0686` to `2b0c3f8b` (no change under `Alundra/`,
`Alundra.Tests/`, `alundra-project/` or `tools/` between them); binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Nothing in the repository or the binary was changed.

- This folder: the preparation (`report.md`, the agent's final message, kept verbatim, in French): the execution rules at HEAD names, the
  frozen value vectors (`vectors/`, with their README and byte-to-member map), the named tests with their expected and red-today values, the
  real-map checks, the removal proofs, the mutants, the corpus-list consequences, the questions; its tools (read the binary at the absolute
  path they name, outside the repository).
- `critic/`: an independent adversarial critic with its own listings, its own MIPS interpreter and models, written from scratch
  (`critic/check.md`, its final message). **Its verdict prevails**; its corrections are applied in the plan.

The scripts write next to themselves: rerun them from a copy, never in place. The vector files are read by the slice's tests from this folder
(the slice decides the path); a line-ending normalisation of git may change their bytes: the tests hash them after normalising CRLF to LF
(critic of sm4, C-1). `SHA1SUMS` lists every file here.
