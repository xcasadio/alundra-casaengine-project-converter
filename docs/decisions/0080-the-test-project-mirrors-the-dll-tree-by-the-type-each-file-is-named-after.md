# ADR-0080: The test project mirrors the DLL tree by the type each file is named after

- **Status**: Accepted (text corrected on 2026-10-22, the day it was written, before review: "one main type per file" follows ADR-0075's reading of rule (3))
- **Date**: 2026-10-22
- **Source**: this chantier: `docs/plan-restructuration-dll.md`, section 0 (D-R-13, D-R-16, D-R-19: the author's answers of 2026-10-22 to Q-R-3 and Q-R-3b) and section 9 (slice R5); R5 discovery of 2026-10-17 (`7ce82c2`, replayed at `211a757`)

## Context

- ADR-0075 (D-R-1) puts the tests in the same tree as the DLL, without the `Alundra` prefix. Applying that rule to `Alundra.Tests` leaves structural choices that the rule does not settle. The R5 discovery counted:
  - 333 test files (308 in `Alundra.Tests`, 20 in `Alundra.Tests.UI`, 5 in `Alundra.Tests.Scripts`) and 432 top-level types, 298 of them prefixed;
  - 44 files that hold several types;
  - 79 files (593 tests) named after a type but mostly exercising other domains;
  - tests named after a type the restructuring renamed (`AlundraMusicPlayerTests` for `BgmPlayer`);
  - about 40 files that drive `ArcRun` inside a domain, besides the 26 end-to-end scenarios and the trace harnesses;
  - 5 xUnit collection definitions (the music one used by 98 files) and helper hosts declared inside test files;
  - the data file `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`, read by the story-chain audit;
  - two export-guard tests that have no DLL type (`CornerTrack`, `ZeroDurationLoop`).
- The author answered each of these choices (Q-R-3, then the precisions of Q-R-3b).

## Decision

- A test file goes to the domain of the type it is named after, not to the domain it exercises most.
- A test class named after a renamed type only loses the prefix (`AlundraMusicPlayerTests` → `MusicPlayerTests`, not `BgmPlayerTests`).
- `Arcs/` holds the 26 end-to-end scenarios and the trace harnesses, nothing else: a file that drives `ArcRun` inside a domain stays in that domain.
- One main type per file, as in the DLL, under the reading of rule (3) written in ADR-0075. A public secondary type goes to its own file, and an internal secondary type stays with the main type. A shared type declared in a test file (an xUnit collection, a helper host) goes to its own file whatever its visibility, in the arrival folder of its original file, not to `Support/`. A file in which no top-level type carries its name takes the name of its first declared public type (P-R5-1 in the plan, the session's reading).
- `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` stays where it is.
- The export guards without a DLL type go to `ExportGuards/` (namespace `Alundra.Tests.ExportGuards`).

## Consequences

- R5 applies these rules through its tables (`r5-files.tsv`, `r5-types.tsv`) and the file cut of R2, and through no other mechanism.
- The `using` directives follow the current place of each type, for example `using Alundra.Tests.Audio` in the files that use the music collection.
- A file named after a type of one domain but exercising another may look out of place; the rule stays predictable and avoids a debate file by file.
- New test files follow the same rules, and R7's `check_names.py --rules` guards them after the chantier.
