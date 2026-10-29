# E20.h4d2 T3 (mutants, ADR) - results (annex)

Run on 2026-10-29 at HEAD `e7d1f1fa` (branch `chantier/theme2-attaques-objets`) plus the survivor test of section 3. Plan: `docs/plan-e20-heros.md` section 1.31.6, T3 (the mutants of the plan, the 36 of the discovery's models, `disc/report.md` section 5, the 18 of the critic, `critic/results/extra_mutants_vs_vectors.txt`, and the "Plus" list); ADR-0111.

## 1. Method

`mutants.py` (the table: id, family, description, the exact text edits) and `run_mutants.py` (the runner), both kept here. For each mutant the runner: applies the edits to the source (each `old` text must occur exactly once), runs `dotnet test Alundra.Tests -c Release` with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, build and test, `--blame-hang-timeout 60s`) on the filter `FireTome1|EarthTome1|TomeWords|NativeSpriteRegistryTests|LiftableAiRegistrationTests|ChargedArrowAiTests|WaveSwordLifeTests|BallFlamesTests` (230 tests; the two mutants of `PlayerManager.TomeSlopeGuard`, D13 and D14, add `HeroItemUse|Tome`), restores the files in a `finally`, and checks `git diff --quiet -- Alundra Alundra.Tests`. One file per mutant: `<id>.txt` (verdict, the failing tests, `git diff empty after: True`). `table.md`: the summary, generated from the files.

## 2. Result: 71 mutants, 69 killed, 2 equivalent (declared by the plan, critic section 4)

By family: pilot D01-D14 (14), cancellation X01-X11 (11), E3 E01-E07 (7), C63 C01-C19 (19), "Plus" P01-P12 (12), critic K01-K08 (8). Every mutant compiled (X11 needed a second form of the edit: `DestroyEntity` with the effect id is on the host, not on the world interface; the first run was a build error and is not counted; D13 needed a unique anchor).

Survivors:
- X10 (the column destroyed before the effect is created): declared equivalent by the plan. The effect takes the coordinates read before (`x`, `y`, `z` locals), so the order has no observable effect.
- C15 (the turn threshold `<= 4` instead of `< 4`; `< 5` is the same): declared equivalent by the plan. At a gap of exactly 4 both give `cur` plus or minus 4 = `d`.
- D06 (the word `+0x278` not cleared at the birth of a ball), in the first run: SURVIVED (`D06_first_run_survivor.txt`). Cause: the interface builds a fresh entity, so the clear writes what is already there; the binary clears the memory of a recycled slot. Not an equivalent of the binary, an equivalent of the DLL's current spawn. Resolved by a test in the scope (commit separate, `D2_ANewbornBall_HoldsNoTarget_...` in `FireTome1DriverTests`): the host double primes the newborn with a slot word 77 and a reference, the pilot must leave 0 and null. Green on HEAD (231), red against D06 (1 red, `D06.txt`).

Killers: see `table.md` (the tests per mutant). The narrow ones: C07 and C08 by `FireTome1AiTests.K1`/`K5`/`C63` (the type nibble and the mask); C11 by `C63` and `K1` (the status 3 target is forgotten); E04 by `EarthTome1AiTests.E1` (the two conditions of the destruction); E07 and P01 by `E2` (the sets of types); P05 by `K4` and `W3` (the count given is the one used); P07 by `C63`/`K1` (the target is the reference, not the slot); P08, P09, P10 and K03 by `D2` (the identity of the column: the three ways of reading the death, and the pointer stored before the test of nullity); X07 by `C2` (a status 3 ball is left alone).

## 3. Mutants of the plan's lists without a C# analogue (not applied)

- "the cancellation also touches the entities of the map page": the DLL's `Cancel` walks `IEntityWorldContext.SpawnedEntities` only and an entity does not carry a map-page marker; there is no branch to add.
- "count read as unsigned (count <= 0 not tested)" (critic): the loop is a `foreach` over the born entities, with no count test.
- "target identity compared with the ball id instead of +0x27C" (critic): the DLL has no identity word; the target is a reference (D-H4D2-4). The nearest ones are C10, C11 and P07 (the three ways the reference/status test can be weakened), all killed.
- The discovery's "loop exclusive of the last slot" is ported as X06 (the last entity of the list skipped).
- K02, K04, P11 and P12 are ported in the DLL's shape (the draw before the birth is a property of the code, so K02 draws after the birth and K04 stores the pointer after the phase 1 ran).

## 4. Other

- Tests: the filter 231 green (Release), the partition 3 (`Alundra.Tests.Ai.`, minus Objects and Projectiles) 512 = 511 + 1 green in Release and in Debug; the other partitions are not touched by a test added in one file. Total 7 931 (7 930 of T2 plus one).
- `check_names.py --rules`: 0 finding (the old warning on `TextBoxGpu.cs` stays). `cmp` of the Debug `Alundra.dll` and `alundra-project/Alundra.dll`: identical (no source of the DLL changed). `git diff --ignore-cr-at-eol --quiet -- docs/` clean after the suites.
- The ADR: `docs/decisions/0111-...md` and its line in `docs/decisions/README.md` (the number recounted on every branch: the last was 0110).
- Not done here (the plan's T3 also lists two fresh verifiers: the session's job, not the executor's).
