# E20.e0a6 T0 - results (annex, no code)

Run at the starting HEAD `926ee6ae7122e3be8f1695c096718d72a0fe0051` (branch `chantier/theme2-attaques-objets`, 2026-10-27). Plan: `docs/plan-e20-heros.md` section 1.16.6 (revision 0, READY),
D-E20-92, D-E0A6-1 to D-E0A6-3, D-H4C2-5. Nothing under `Alundra/`, `Alundra.Tests/`, `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/`, `data-extracted/` is changed by this commit
(the temporary wiring of section 3 was undone: `git diff -- Alundra Alundra.Tests` empty).

## Verdict: no stop. The binary passes the 16.16 position of the crate; the predicted list (B5 alone, its three rows) is the measured list.

## 1. The starting HEAD and the suite

`git rev-parse HEAD` = `926ee6ae`. Build then suite in Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=.../skipcopy.targets`), `--blame-hang-timeout 60s`, in four parts
(A `Ai.Projectiles`; B the other `Ai`, `Arcs`, `Audio`, `Cameras`, `Cells`, `Combat`, `Dialogues`; C `Entities`, `Events`, `ExportGuards`, `Hero`, `Items`; D the rest):
**332 + 2 011 + 3 340 + 2 036 = 7 719 tests, 0 failure** (`suite/start.{A,B,C,D}.summary`, the `.red` files are empty). The traces `docs/hero-trace-389-*.txt` were rewritten with CR at the end of
lines: `git diff --ignore-cr-at-eol --quiet -- docs/` true, restored by `git checkout`.

## 2. The reading of the binary: the birth of the loot of E2 (a broken crate)

Binary `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read with capstone by `bin/fdis.py` (copy of `../../e20-e0c/posed-rev3/fdis.py`; `bin/exelib.py` is the
copy of `../../e20-e0c/check/exelib.py`, which reads the binary at its absolute path). Listings: `bin/dis_80032b90_80039f58.txt`, `bin/dis_80039d04.txt`.

- `DestroyEntity` `0x8003A59C` calls `0x80032B90` (the contents drop, `SpawnContents`) at `0x8003A5B0` with `$a0` = the dying entity (`move $s0,$a0` ... `jal 0x80032b90`).
- `0x80032B90`: `s1` = the entity; `s0 = [s1+0x3C]` (the contents drawn at the creation; 0 = nothing); veto `0x80032A00`; then, at `0x80032BCC-0x80032BF0`:
  `[sp+0x10] = [s1+0x118]` (Y), `[sp+0x14] = [s1+0x11C]` (Z), `[sp+0x18] = 0` (direction), `$a3 = [s1+0x114]` (X), `$a2 = s0+0x1E` (sprite index), `$a0 = $a1 = 0` (parent none, global page), `jal 0x80039f58`
  (`SpawnWarpEntity`). **The position passed is the 16.16 position of the entity (`+0x114`, `+0x118`, `+0x11C`), read whole, with no shift: the fraction of the pixel is passed.**
- `0x80039F58` forwards `$a3` (X) to `0x80039D04` as `[sp+0x18]`, Y from `[sp+0x70]`, Z from `[sp+0x74]`; `0x80039D04` stores them unshifted: `+0x114 = [sp+0x48]` (X) at `0x80039E88`,
  `+0x118 = [sp+0x4C]` (Y) at `0x80039E94`, `+0x11C = [sp+0x50] - [+0x1EC] + 1` (Z) at `0x80039EAC`. The apparition keeps the fraction of X and Y.
  (The caller `E2` reaches `DestroyEntity` the same way; the stop "the loot birth at the binary does not pass the 16.16 position" is not reached.)

D-E0A6-2 holds: the dropped object is born at the 16.16 position of the crate (143.5 px in X in B5), not at its whole pixel.

## 3. The predicted list and the temporary wiring

Prediction (plan): with `keepSubPixel: true` at the site, only `LiftableBreakWorldTests.B5_TheContentsOfTheCrateAreDroppedAtItsBreak_WhereAContentOfZeroDropsNothing` moves (its 3 rows: 1, 2, 3 ticks per frame).

Wiring (`suite/wiring.diff`, one argument): `WorldProxy.cs`, in `SpawnEntityBySpriteIndex` (line 3629 at this HEAD), `spawnedProxy?.PushLogicalPositionToRoot();` becomes
`spawnedProxy?.PushLogicalPositionToRoot(keepSubPixel: true);`. The two other textual copies of the call (the map-load loop, line 1122, and the record spawn, line 3504) are NOT changed (D-E0A6-1).
Build and the entire suite in Release, same four parts (B split in two runs to stay under the 10 minutes of the tool: `Ai.Objects.` 635 tests and the rest 1 376, 2 011 in all):

| Part | Result |
|---|---|
| A | 332 passed, 0 failure |
| B1 `Ai.Objects.` | 632 passed, **3 failed** (the three rows of B5 only) |
| B2 (the rest of B) | 1 376 passed, 0 failure |
| C | 3 340 passed, 0 failure |
| D | 2 036 passed, 0 failure |

Total 7 719 tests, 3 failures: **`LiftableBreakWorldTests.B5_TheContentsOfTheCrateAreDroppedAtItsBreak_WhereAContentOfZeroDropsNothing(ticksPerFrame: 1, 2, 3)`**, exactly the predicted list
(`suite/wired.*.summary`). No other test moves.

Wiring undone: `git checkout -- Alundra/Maps/WorldProxy.cs`, `git diff -- Alundra Alundra.Tests` empty; traces restored as in section 1.
