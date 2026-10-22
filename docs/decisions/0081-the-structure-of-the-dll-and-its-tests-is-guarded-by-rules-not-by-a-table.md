# ADR-0081: The structure of the DLL and its tests is guarded by rules, not by a table

- **Status**: Accepted
- **Date**: 2026-10-22
- **Source**: this chantier: `docs/plan-restructuration-dll.md`, section 0 (D-R-15, D-R-22) and section 12 (slice R7: P-R7-1 to P-R7-8)

## Context

- ADR-0075 fixes the rules of the Alundra DLL (one folder, one namespace; no `Alundra` prefix; one main type per file; a simple name once) and ADR-0080 the mirror of the tests. During the chantier these rules were checked against a table (`tools/dll-structure/table.json`, `r5-files.tsv`, `r5-types.tsv`) by `check_names.py --table`, `--tree` and `--tests`.
- A table-bound check refuses every file it does not list (`[hors table]`), including a conforming new one; ADR-0075 says that a file added during the chantier is not part of the table. Once the chantier is done, those modes can no longer guard the tree.
- The discovery of R7 (2026-10-22, read-only) found no CI and no hook running `check_names.py`; the plans of E19 and E20 name it as a prerequisite ("Q-R-7", "the end of the chantier").
- The older reader (`scan_types.parse`) does not model raw string literals, sees no declaration that does not start its line, and the packages it reads do not cover 13 packages of the test project (`xunit.assert` among them) nor the engine project `CasaEngine.Compiler`.

## Decision

- `check_names.py --rules [--root <folder>]` checks the working tree of `Alundra/` and `Alundra.Tests/` against the rules, with no table: (1) a folder is a namespace and a file holds one `namespace`; (2) no `Alundra` / `IAlundra` prefix on a type (nested included), a file or a folder; (3) one main type per file, with the exception written in ADR-0075 and the `Name.Part.cs` files of partial classes; (4) a simple name once (engine, MGUI, NvgSharp, packages, BCL, the DLL and the tests), no type named like a namespace segment, none of the folder names that ADR-0075 rules out; (5) every test folder has its counterpart in the DLL, except `Arcs`, `Support` and `ExportGuards` (ADR-0080).
- A new folder is allowed (no closed list of domains); a nested type named like a namespace segment is a warning (ADR-0075); the exit code is 0 when only warnings remain.
- The module `rules.py` is new; `scan_types.py` and the older functions of `check_names.py` do not change, because frozen proofs import them. `rules.py` masks raw string literals, reports `[lecture]` when braces do not balance or a type declaration does not start its line (the reader would not see it), and adds the packages and the project missing from rule (4) (`rules-extra-types.tsv`, the sources of `CasaEngine.Compiler`).
- The table-bound modes, `selftest.py` and the proofs `prove_r2.py` to `prove_r5.py` become the archive of the chantier: they replay at their closing commits. From R7 on, "the collision check is green" means `check_names.py --rules` at 0.
- The proof of the guard is versioned: `selftest_rules.py` (mutation cases on throw-away copies) and `cmp_typedef.py` (the reader against the TypeDef table of the built assemblies).

## Consequences

- A slice that adds a conforming file passes without touching any table; a name or a folder outside the rules is refused with a tag (`[namespace]`, `[nom]`, `[principal]`, `[collision]`, `[segment]`, `[doublon]`, `[dossier]`, `[lecture]`, `[miroir]`).
- This completes ADR-0075 without replacing it: its sentence on files added during the chantier stays true for the chantier.
- Limits, written in `tools/dll-structure/README.md`: the collisions of nested types, member names, the existence of the nine types that the data cites, `tools/AlundraTestSaves/` and non-`.cs` files are not checked; `rules-extra-types.tsv` is a snapshot to regenerate when a test package changes; three packages without XML documentation and three BCL types without XML stay unread (the build judges).
- Nothing runs the guard automatically: it is called by the slices that need it. A CI or hook would be a separate decision.
