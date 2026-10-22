# dll-structure

Tooling of the restructuring of the Alundra gameplay DLL (`docs/plan-restructuration-dll.md`, ADR-0075). Python 3, standard library only,
always run with `python -B`. Read-only on the repository except `gen_table.py` (writes `table.json` and `table.md`) and `apply_r2.py` (the working tree of the R2 slice).

| Script | Role |
|---|---|
| `scan_types.py` | Reader of C# type declarations (regular expressions, comments and literals stripped): the DLL, the engine, MGUI and NvgSharp, the NuGet package XML docs, the BCL reference pack. |
| `gen_table.py` | Generates `table.json` and `table.md`: for every top-level type of the DLL at the pinned revision `BASE_REVISION`, its file, namespace and name at stages `r2` (moved and split, no rename), `r3` (renamed) and `final` (after the nine data-cited types move in R4). Replayed, it gives the same bytes (`git diff --exit-code tools/dll-structure`). It fails if a file is unmapped, a file has no known main type, two split types aim at one path, or a name occurs twice in a namespace (a partial type excepted). |
| `check_names.py --table` | The planned names against the engine, MGUI, NvgSharp, packages and BCL: type collisions, namespace segments that would mask a type, a type named like a segment of the DLL. Lists every finding; exit 1 if any. |
| `check_names.py --tree --stage r2\|r3\|final [--root <folder>]` | The tree of the DLL (working tree, or `<folder>/Alundra`) against the table at that stage: absent file, `.cs` outside the table, type missing under its name, in another namespace or file; then the collision checks on the names really declared. Lists every deviation; exit 1 if any. |
| `selftest.py <work folder outside the repository>` | Builds synthetic trees from the table and proves the checks fire: conforming tree, wrong namespace, wrong name, extra file, missing file, and a final name changed to `Vector2` (`--table`). |
| `apply_r2.py <Domain> --move\|--fix` | R2: step 1 `git mv` of the files of a domain to their `r2` path; step 2 the `namespace` lines, the cut of the split types (byte for byte, in a new file), the `using` lines, the `Alundra.Scripts.<T>` qualifications and the source paths read by a test, recomputed from the current location of each type. Writes the working tree (not read-only); commits nothing. `--domains` lists the order; `--dry-run`; `--allow-dirty`. |
| `prove_r2.py --domain <D> --kind move\|fix [--commit REV]` / `--all --base REV` | R2 proof of a commit (renames at 100 % by the table; cut blocks equal to the bit; every changed line in an allowed class; justified `using` lines) or of the whole range plus the end state. Read-only. |
| `rename-tool/` | R3: a small C# console (net9.0, outside the solution; Roslyn 4.8.0 and `Microsoft.Build.Locator`, restored offline with `dotnet restore --source C:/Users/casad/.nuget/packages`, built with `dotnet build -c Debug`). `RenameTool <root> <Domain> [--dry-run]` renames the types of a domain of the table (`r2` name to `r3` name) with Roslyn in `Alundra`, `Alundra.Tests` and `tools/AlundraTestSaves`, checks that every reference and every other identifier keeps its binding and that no compilation error appears, and otherwise refuses the domain without writing. `--rename Old=New` replaces the domain by explicit pairs (proofs). |
| `r3_literals.py gen\|check\|apply <Domain>` + `r3-literals.tsv` | R3: the survey of the string literals that carry a type name (class 1, 1b, 2, 3, 4, 5, one entry per file, line and name) and the script that applies the entries of a domain or checks the survey against a tree. |
| `prove_r3.py --domain <D> --kind move\|rename [--part 1\|2] [--commit REV \| --worktree \| --before-dir X --after-dir Y]` / `--all --base REV` | R3 proof of a commit: moves at 100 % by the table; renames occurrence by occurrence (code, cref, literal by the survey, comment untouched). Read-only. |
| `apply_r4.py move\|fix\|literals` / `literals-gen` / `literals-check [--stage before\|after]` + `r4-literals.tsv` | R4: `move` = `git mv` of the 10 files of `Alundra/Scripts/` to their `final` path (T1); `fix` = the `namespace` line of the 10 files, the `using` lines and the `Alundra.Scripts.<T>` qualifications of the moved types in the DLL, `Alundra.Tests` and `tools/AlundraTestSaves` (T2 step 2; the types are renamed first, with `rename-tool/ --rename Old=New` on the nine explicit pairs); `literals` = the class 1 and 1b entries of the survey (T2 step 3, a literal is found by its text, not by its line). The survey `r4-literals.tsv` (51 entries naming a cited type; class 1: 40, 1b: 8, 4: 3 kept) is generated once at the T0 state. |
| `prove_r4.py --kind move\|code\|end [--commit REV \| --index]` | R4 proof: `move` = the 10 renames at 100 % by the table; `code` = the T2 diff occurrence by occurrence (the nine names, the qualifications, the survey, the `using` lines justified, the `namespace` line of the 10 files, the six converter files and the six `design.json` by their named change), nothing else; `end` = no `Alundra.Scripts` left. `--index` proves the staged tree against HEAD. Read-only. |
| `r5-discovery/` + `r5-files.tsv`, `r5-types.tsv`, `r5-literals.tsv`, `r5-qualified.tsv` | R5 (tests in the mirrored tree), T0a: the discovery scripts (`common.py`, `evidence.py`, `classify.py`, `decisions.py`, `couplings.py`, `build.py`, `extras.py`, `ilscan/`, the IL evidence `il_refs.tsv`) and the tables they produce, versed in their values of BEFORE R5. `decisions.py` holds the structural and subject entries of the author's answer to Q-R-3 (D-R-13, D-R-19, ADR-0080); `build.py` applies the rule (structural, then the name, then the subject, then the references) and P-R5-1 (one main type per file; the public and the shared secondary types cut into their own file). Replayed with `R5_WORK=<folder outside the repository>`: `python -B evidence.py`, `couplings.py`, `build.py`, `extras.py`, then the four tables are copied here. |
| `r5_lib.py [--check-tables]` | R5: the tables, the occurrence finders (a literal, a qualified reference, a capture are located by file, text and rank, never by line) and what `apply_r5.py` and `prove_r5.py` share. `--check-tables` re-verifies the tables against the tracked files (each `.cs` once, each type once, no path twice, namespace = folder, no collision, no type named like a segment). |

Finding tags: `[collision]`, `[segment]`, `[nom]`, `[namespace]`, `[fichier]`, `[absent]`, `[hors table]`; warnings: `[avertissement]`.

## Where the data comes from

- DLL files for the table: `git ls-tree` at `BASE_REVISION` (the parent HEAD at the end of R0, `5434cf6`), so the table does not move when R2 to R4
  move the files. DLL files for `--tree`: `git ls-files --cached --others --exclude-standard -- Alundra` (no `bin/`, `obj/`, `artifacts/`) read in the
  working tree, or every `.cs` under `<root>/Alundra` when `--root` is given.
- Engine, MGUI, NvgSharp: the HEAD of their submodule (the author's uncommitted changes there are not read).
- Packages and BCL: the XML documentation files (`~/.nuget/packages`, `Microsoft.NETCore.App.Ref` 9.0). The list of packages is in
  `scan_types.PACKAGES`; a listed package whose XML is not found is an **error**.

## Limits

- Declarations are read by a regular-expression reader, not a compiler; nested types are ignored by the name checks (they never collide with an
  imported name).
- `Portable.Xaml` ships no XML documentation (`scan_types.PACKAGES_WITHOUT_XML`, named explicitly): its types are not checked by the script. A
  clash there would be an ambiguous reference (`CS0104`) and the build is the judge.
- Warnings, not failures: a type nested in `Alundra.Tests` named like a namespace segment (today `Session` in `TextBoxGpuHarness.cs`) only masks the
  segment inside its own class body.
