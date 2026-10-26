# E20.g2c3 - T1 results (2026-10-26): phase A, tests first

Plan: `docs/plan-e20-heros.md` section 1.24.5, E20.g2c3 T1. Two commits on `chantier/theme2-attaques-objets`: the tests (with a compiling skeleton of `Alundra/Ai/Objects/WaterDropAi.cs`, every method `NotImplementedException`) and the code. Nothing is registered in production.

## Tests (35, in `Alundra.Tests/Ai/Objects/`, mirror folder of the class, ADR-0080)

| File | Tests | Plan item |
|---|---|---|
| `WaterDropAiGoldenTests.cs` | 4 | (a1): the pins of the two golden files (SHA-256 `40071ab5...6214b`, 22 vectors over 11 classes; `b9f8585c...16ef`, the four state-2 vectors), the 22 goldens word for word (state, byte `+0x275`, counter `+0x282`, flags, animation, ForceZ, PosZ, program C, only-read words, the torch hit and the candidates left alone), the four vectors `(CollidedWithEntityZ, IsOnGround)` = (1,0), (0,1), (1,1), (0,0), the two vectors of the state >= 5 |
| `WaterDropAiStepTests.cs` | 19 | (a2) A19 (6 rows: low byte kept, nothing else touched); (a1) the pure step for each state and the states 5, 6, 255; (a4) the state 4 with `+0xAC` = 0 (nothing) and with 1, 2, -1 (the whole restoration, 160 px, the program C given back) |
| `WaterDropAiTargetTests.cs` | 10 | (a3): the hero carrying a torch, a non-torch, nothing (even a hero of the torch sprite type), a posed torch, a torch in each of three statuses (the status 4 included: written anyway), a non-torch support, no support, a torch carried by another entity (not put out), no sound by the drop; the advance and the removal of `Collidable` in every case |
| `WaterDropAiRegistrationTests.cs` | 2 | (r1) the keys on a registry of their own (A19, C71 for {391} alone) and the production registry (A19 and C71 for exactly {391}, E20 for exactly {393}) |
| `WaterDropVector.cs` | helper | lays a vector out on `CarryBench` entities in the machine's slots (0 hero, 3 drop, 5 to 7 candidates; stride `0x294`) |

(a5) is the existing `TorchDataGuardTests.A5_A19AndC71_AreHeldByTheDropOfWaterAlone_TheType391_NotPortedHere` (g2c2): green, unchanged.
`AnnexFiles.WaterDropAnnex(relative)` is new (the golden files stay where T0 deposited them).

## Red before (the skeleton commit), green after

- Before: `dotnet test --filter FullyQualifiedName~WaterDropAi` (Release, copy neutralised): 35 tests, **34 red, 1 green** (the pin of the two golden files). The 34 red: 31 `NotImplementedException` from the skeleton, the aggregated failures of the two `Assert.All` / theory wrappers around it, and `R1_...` (a real red: no key in production).
- After the code: **34 green, 1 red**: `WaterDropAiRegistrationTests.R1_TheProductionRegistryHoldsA19AndC71ForExactlyThe391_AndE20ForExactlyThe393`, red as planned until the phase B (T2) registers `WaterDropAi`. `TheKeysAreA19AndC71_OnTheType391Alone` (the class on a registry of its own) is green.
- Partition `Alundra.Tests.Ai.Objects.` (Release): 602 tests, 601 green, 1 red (the one above); the existing tests of the namespace are unchanged and green. No existing test was moved or rewritten (nothing is registered).
- `check_names.py --rules`: 0 finding. Debug build of `Alundra.Tests` (and so of the DLL): 0 error.

## Notes

- The target of the state 3 is looked up only in the state 3 (a pure read; `Step` receives the answer "the target is a torch").
- The restoration always hands the flags to the controller (`ResyncControllerFromFlags()`), also when they already hold the values; the position goes to the root by `PushLogicalPositionToRoot()` right after `PosZ += 160 px` (a no-op without a controller: the world tests of T2 (w1) assert the root).
- The store `[0x8013DA20] = 1` of the state 4 is not ported (D-G2C3-2), the comment is at the site.
