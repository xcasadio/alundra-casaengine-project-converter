# E20.e0a6 T2 - results (2026-10-27)

Branch `chantier/theme2-attaques-objets`, start HEAD `aa63f92e` (T1 done). No source file changed in this task (the mutant is undone, `git diff` empty after it).

## Suites (zero failure)

Seven disjoint partitions by namespace prefix (`Alundra.Tests.`), `--blame-hang-timeout 60s`, `--no-build`; Release with the neutralised copy (`skipcopy.targets`, build and test), then Debug last (copies the DLL). Runner as in `../../e20-g1d/t2.md`.

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 635 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 332 | 0 | 0 |
| `Hero.Hero*` | 1331 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2247 | 0 | 0 |
| everything else | 2225 | 0 | 0 |
| total | **7721** | 0 | 0 |

7721 = 7719 (T0) + 2 new tests (S1, S2). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 findings (1 old warning, `Alundra.Tests/UI/Shared/TextBoxGpu.cs`).

## Mutant

`keepSubPixel: true` -> `keepSubPixel: false` at `WorldProxy.SpawnEntityBySpriteIndex`, applied by `sed`, Release build (neutralised copy), filter `WorldProxySpawnSubPixelTests | S1_AnEntityBorn | LiftableBreakWorldTests` (32 tests), undone by `git checkout`; `git diff` empty after. Output: `mutant.txt`.
Killed: 5 red of 32: S1, S2, B5 (ticksPerFrame 1, 2, 3). Survivors: none.

## ADR

No new ADR (D-E0A6-3): section "Amendment (E20.e0a6, 2026-10-27)" appended to ADR-0103.
