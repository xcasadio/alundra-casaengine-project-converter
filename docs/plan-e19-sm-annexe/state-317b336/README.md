# E19.sm0 to sm4 - the citations translated to HEAD and re-checked (2026-10-18)

Provenance: read-only "current state" pass of 2026-10-18 for `docs/plan-e19-opcodes.md` section 1.4 (E19.SM), after the DLL restructuring
(R0 to R4 and R6) and before R5. Three translators (one per group: sm0, sm1+sm2, sm3+sm4) mapped every DLL and test citation of section 1.4,
of this annex and of `sm1-sm4/` to HEAD, re-checked every statement about the DLL and listed what blocks an executable slice; a second,
independent agent re-checked each translation (`reports/check_*.md`). **The checks prevail.** Repository read at `b20eb8ce` to `317b3360`
(only `docs/plan-e20-heros.md` changed between them).

- `reports/translate_sm0.md`, `check_sm0.md`: the safety net (sm0); the census recounted at HEAD (483 maps, 110 140 instructions, 510 sites
  in 96 maps, the key multiset equal to `../skipped-sites.tsv`).
- `reports/translate_sm12.md`, `check_sm12.md`: sm1 (0x60 0x61 0x3C 0x3D) and sm2 (0x6A to 0x6D): REVISE.
- `reports/translate_sm34.md`, `check_sm34.md`: sm3 and sm4: REVISE.
- `sm0/`, `sm12/`, `sm34/`: the scripts of both agents (the recount is a copy of `../census.py` pointed at HEAD paths; `verify/` holds the
  checker's own scripts) and their outputs.

The scripts read the repository and the export (`alundra-project/`) at the paths they name and write next to themselves: rerun them from a
copy, never in place. `SHA1SUMS` lists every file here.
