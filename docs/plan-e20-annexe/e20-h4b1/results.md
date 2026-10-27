# E20.h4b1 T1 - results (tests, code, retirement proof)

Starting HEAD `c0d14e1e` (branch `chantier/theme2-attaques-objets`). Plan: `docs/plan-e20-heros.md` section 1.31.2 (revision 2, READY). The T0 results are in `README.md`. Commits of this pass:
`175e99d1` (tests, with an inert `Find`) and `cfe61bab` (the code). This file is the third (docs) commit.

## 1. What was written

- `Alundra.Tests/Combat/TargetQueryVectorAdapter.cs`: the adapter image to proxies (D-H4B1-3). It loads the 120 vectors of `e20-h4b/bin/vec/vec_query_8003AF70.json` and the 10 of `e20-h4b/critic/vec_extra/vec_extra_query.json`
  (the sentinel among them), and builds one `EntityScriptProxy` per entity slot a vector reads, plus the caller (always), with `Slot`, `Status`, `Flags`, `HitAnimByte`, `PosX`, `PosY`, `DamagedTickCounter`,
  `FrameCollisionTickCounter`, `BlockedByEntity` (an `Entity` when `+0x20` is not 0) and `BalanceRecord` (a sheet whose `ByteAt(type + 2)` answers the byte the vector read at `sheet + type + 2`). It throws on a read of
  another class (a non-entity read that is neither the count word, the fifth argument, nor a one-byte sheet read at `sheet + type + 2`). The result of the real code is the list of slots
  `(pointer - 0x80127D30) / 0x294` of the pointers it wrote in `0x800CC058`, read from the vector's own `writes` and `ret`.
- `Alundra.Tests/Combat/TargetQueryTests.cs`: 27 tests, (q1) 1, (q2) 1, (q3) 14, (q4) 6, (q5) 3, (q6) 2.
- `Alundra/Combat/TargetQuery.cs` (new): `TargetQuery.Find(entities, count, caller, mask, type, inclusiveBound = InclusiveBound, sortThreshold = SortThreshold)`, the constants `InclusiveBound = true` and `SortThreshold = 2`
  (the production values of Q-H4B-2 and Q-H4B-3), and the private Hoare sort of `0x8003AE04`. No caller in production; `EntitySearchService` is untouched.
  The settings are two `const` parameters with the production values as defaults (no new type: the binary mode of the vectors is `inclusiveBound: false, sortThreshold: 3`).

## 2. Red before the code, green after

At `175e99d1` `Find` answers an empty list (the settings are there). The 27 tests: **26 red, 1 green** (the green one is (q6) the values of the constants). At `cfe61bab`: **27 green**, among them (q1) the 130 vectors (the binary
lists and their order, the sentinel `[3, 2, 1]` with the slot 4 = count live and eligible but never returned) and (q2) the 130 corrected lists of `vec_query_corrected.json` (the test also asserts that exactly 8 corrected lists differ from the binary's, as the T0 README says: 1 for the bound,
7 for the threshold).

Tests red at `175e99d1` (and again with the body neutralised, section 4): Q1, Q2, the 14 (q3) tests, the 6 (q4) tests, the 3 (q5) tests, `Q6_TheDefaultCall_UsesTheProductionSettings`. Green: `Q6_TheProductionSettings_AreTheCorrectedDefaultsOfQH4B2AndQH4B3`.

## 3. Suites

Disjoint partitions by namespace prefix (`Alundra.Tests.`), `--blame-hang-timeout 60s`, `--no-build`, Release with `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` (build and test), then Debug last (it copies the DLL to the project).
The same seven partitions as `e20-g2c2/t3.md` (script outside the repository).

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 634 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 275 | 0 | 0 |
| `Hero.Hero*` | 1331 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2235 | 0 | 0 |
| everything else | 2206 | 0 | 0 |
| total | 7632 | 0 | 0 |

7632 = the 7605 of E20.h4a (its T1 note) + the 27 new tests. No existing test moved (the predicted list was empty; recounted at the starting HEAD in `README.md` section 3). The suites rewrote the line ends of the four
`docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical.
`python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 existing warning (`TextBoxGpu.cs`), after each of the two commits.

## 4. Proof of retirement

The body neutralised by a real edit (script outside the repository: `return System.Array.Empty<EntityScriptProxy>();` inserted as the first statement of `Find`, declarations and settings kept), Release build with the neutralised copy,
the class `TargetQueryTests`: **26 red, 1 green**, the same set as section 2; undone in a `finally`, `git diff -- Alundra` empty after. Red: (q1), (q2), the (q3) cases (each of the 14 tests keeps at least one candidate, so none
stays green with an empty list), (q4), (q5) and the default-call test of (q6); green: the values of the constants.

## 5. Notes against the plan

- (q3): each filter is next to its opposite inside one test (the kept and the discarded entity in the same world), so no (q3) test is satisfied by an empty list; the plan's remark that the cases where the candidate is
  discarded stay green with an empty body does not apply to these tests (all 14 are red). This is stricter, not looser.
- (q4): the "n = 3 equal keys" order `[3, 2, 1]` and the "4 results with ties" order `[2, 3, 4, 1]` are the plan's; the keys for the latter are (4, 1, 1, 4) (found by running the critic's model over small squared distances, `x` = 2, 1, 1, 2).
- (q5): the key being at most 2^31 for 16.16 positions in 32 bits, the modulo 2^32 shows as the unsigned order of a key of 2^31 (slot at (-32768, -32768)); the 32-bit difference shows with the caller at `0x7FFF0000` and the slot at
  `-0x7FFF0000` (dx = 2 in 32 bits, -65534 in 64 bits). The pivot wrap `(K[lo] + K[hi]) mod 2^32` only happens when both keys are 2^31, where the binary reads memory before the array: not testable, not tested (the code
  keeps the unsigned wrap).
