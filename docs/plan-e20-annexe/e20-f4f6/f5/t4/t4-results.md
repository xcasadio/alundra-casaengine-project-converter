# E20.f5, T4 (2026-10-23): results (ADR, mutants of the DLL code, suites, checks)

HEAD at the start of T4: `19751cc6` (phase B). Branch `chantier/theme1-porter-plateforme`. The two fresh verifiers are launched by the main session (not part of this pass).

## 1. ADR

ADR-0084 (the one the plan names "common to f4, f5 and f6") is **extended**, no new number: sections Source, Decision (four bullets: E17, the search, the speed key, phase B) and Consequences. The last ADR number taken on all branches is still 0084 (`git ls-tree` of the branches, nothing above). The index line of `docs/decisions/README.md` is unchanged (same title).

## 2. Mutants of the DLL code (applied by script, reverted in a `finally`, `git diff` empty on each mutated file afterwards)

Script `t4/t4mut.py`, log `t4/mutants.txt` (focused Release run with `skipcopy.targets`, 219 tests of the arrow, charged arrow, seam, registry, wave, birth tick, logical Z and map state classes). **27 mutants run: 25 non-equivalent, all KILLED; 2 equivalent (survive, written); 1 survivor of the first run fixed (below).**

| Mutant | Edit | Killed by (failed tests) |
|---|---|---|
| A01 | `DamagedTickCounter` filter removed | 3 |
| A02 | `HitAnimByte & 0x40` filter removed | 2 |
| A03 | class mask filter removed | 3 |
| A04 | type 429 filter removed | 2 |
| A05 | type 429 changed to 0x1AC | 2 |
| A06 | self filter removed | 1 (E2 itself skipped) |
| A07 | hero excluded from the sweep | 1 (E2 itself skipped / hero), **after the fix below** |
| A08 | sweep stops one slot early | 20 |
| A09 | status filter added (FlagToDestroy skipped) | 2 |
| A10 | overlap exclusive on X | 1 |
| A11 | overlap exclusive on Z (the vertical reach) | 1 |
| A12 | bounce direction + 8 | 28 |
| A13 | gravity bit forgotten (0x40 only) | 28 |
| A14 | bounce phase not recognised | 25 |
| A15 | end of the bounce without the animation-end flag | 25 |
| A16 | status 3 at the fallback (not found) | 12 |
| A17 | destruction with an effect (the touch) | 20 |
| A18 | destruction with an effect (end of the bounce) | 7 |
| A19 | bounce test on `HitCounter` inverted | 51 |
| B01 | speed key on the current animation (D-F5-5 line removed) | 24 (P3 and the lives) |
| B02 | E17 registered for the type 7 | 1 (E5) |
| B03 | E17 not registered | 37 (the registry pin, E5, E7 lives) |
| B04 | the production set contains 5 | 1 (S5) |
| B05 | the set without 3 and 4 (removal proof) | 5: `ArrowProductionTests` P1 x2, P2 x2 and S5 (derived from the classes, as in f4) |
| B06 | the set without 4 | 3 (P1 and P2 of type 4, S5) |
| E01 | `ResyncControllerFromFlags` removed | **SURVIVES: equivalent**, as the plan writes (D-F5-4: the walkability mask reads the classes only, the controller's gravity is always null here) |
| E02 | `ResyncControllerFromFlags` called twice | **SURVIVES: equivalent** (idempotent) |

The plan's list is covered: filters removed one by one (A01 to A04, A06), type 429 changed (A05), hero included (A03: without the mask the hero, whose flags hold no class, is found) or excluded (A07), sweep one slot short (A08), status filter added (A09), exclusive overlap (A10, A11), direction + 8 (A12), gravity forgotten (A13), bounce phase not recognised (A14), end without the flag (A15), status 3 at the fallback (A16), destruction with an effect (A17, A18), speed key on the current animation (B01), E17 for 7 (B02), 5 in the set (B04), Resync twice (E02, equivalent).

**One survivor found and fixed.** A07 survived the first run: the hero cases of `ArrowAiTests.E2_TheArrowItselfIsSkipped_…` used a target without `IsPlayer`, so excluding players changed nothing. Fix: `IsPlayer = true` on the target of both scenes (hero flags `0x3118C` not found; with the class A posed, found), `Alundra.Tests/Ai/Projectiles/ArrowAiTests.cs`, 3 lines; green on the unmutated code, red on A07.

Removal proofs of the plan: removing {3, 4} turns red exactly the four production cases (p1, p2) plus S5 (B05); removing the speed key line turns P3 and the lives red (B01); removing the (E, 17) key turns E5 and the lives red (B03); removing `Resync` turns nothing red (E01, equivalent, written). The mutant "E17 falls into the bounce after the destruction" is equivalent (T1 section 5, point 2): the destruction returns.

## 3. Suites

- `Alundra.Tests` **Release** (`skipcopy.targets`): **5 428 passed, 0 failed** (8 min 58 s).
- `Alundra.Tests` **Debug** (last; it deposits the DLL in `alundra-project/`): **5 428 passed, 0 failed** (8 min 15 s).
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll`: identical.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 known warning (`TextBoxGpu.cs`).
- The four `docs/hero-trace-389-*.txt` rewritten by the suites: `git diff --ignore-cr-at-eol` empty, restored by `git checkout`, never indexed.
- Engine (`CasaEngineMonogame`), analyser, converter (`tools`), `data-extracted/`, `alundra-project/`: `git diff b0116485 HEAD --raw` empty on them; the two `m` flags of the submodules are the author's earlier local modifications, untouched.

## 4. Comment (carry-over of f4, P4)

`Alundra/Hero/PlayerManager.cs`: the comment of `SpawnWeaponProjectile` (about line 1208, "the three waves of the sword since E20.f3c") cites the type 7 of f4 (T1 did it) and, since phase B, the types 3 and 4 as wired "since E20.f5" (the doc line 1198 said "come with E20.f5": corrected too); E20.f6 stays "adds".

## 5. Moved tests: predicted list (`results.md` section 3) against measured

Predicted 9 (7 in phase A, 3 in phase B, S5 in both). Measured (T1, T3, full suites): the three registry pins (`NativeSpriteRegistryTests`, `WaveSwordLifeTests.V3`, `ChargedArrowAiTests.C2`), `WeaponSpawnSeamTests.S5` (A and B), `MapStateTests.P3`, `BirthTickTests.AnEntityWithoutTheMarker…`, `LogicalZWorldTests.Z5…`, `WeaponSpawnSeamTests.S1` (first block), `ChargedArrowProductionTests.P1`: 9 distinct tests, **measured equals predicted, nothing outside the list moved**, under the stop threshold (10). In T4 the only test touched is `ArrowAiTests.E2` (a test of f5 itself).

## 6. Deviations from the plan

1. The removal of {3, 4} turns S5 red as well as (p1) and (p2) (S5's production set is derived from the classes, as in f4, T3 already noted); (p3) is not a test of f5 (the negative witnesses are the phase-A rewrites).
2. The ADR is extended (the plan says it is common to f4, f5, f6), not new.
