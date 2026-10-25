# E20.h0a T1 - results (tests, then code)

Branch `chantier/theme2-attaques-objets`. Tests commit `802d38fd`, code commit `b85aa183`. Plan: `docs/plan-e20-heros.md` section 1.30.1, T1; T0 results in `results.md`.

## 1. What was written

- `Alundra.Tests/Hero/HeroItemUseTests.cs` (new, 140 test cases): (a1) to (a7) and (a9). A bare hero with a `ContactHost` (real item tables, recording sound player), one `MovePlayer` call per tick, the three
  seams of D-H-4 (`ItemStartForTests`, `ItemDriverForTests`, `ItemCancelForTests`) and a journal of the stubs (`ItemStubCallsForTests`, D-H-3). (a9) builds two real worlds (`ArcRun`, map 103) and checks the second hero.
- `Alundra.Tests/UI/Inventory/InventoryDirectorTests.cs`: (a8), `Trigger_Refused_WhileAnItemIsInUse_OpensOnceItIsFreed` (refused with an item in use, the same press opens once it is freed).
- Code: `EntityScriptProxy` (`ItemInUse`, `ItemUseCounter`, `ItemUsePhase`, D-H-1); `PlayerManager` (`TryUseItem` + `StartItem` + the stub starts/drivers/cancels of D-H-3, `UpdateItemUseDriver`, `CancelItemMode`, the call in the locked
  branch after `UpdatePlayerCarriedEntity(1)` and before the entry of the damage, the `else if (SwimHandlerOf(state) == Skip)` at the end of the main chain); `InventoryDirector` (the guard).

## 2. Red before the code, green after

Tests commit run (Release, skipcopy), with the three fields and the seams declared INERT (so that the tests compile): 141 selected (140 + (a8)), **133 red, 8 green**. The 8 green before and after are the negative witnesses
(each is paired in the same test class with a positive that is red before):
`A6_AStateThatIsNotInTheTableOfTheStatesWithoutHandler_DoesNotCancelTheItem_...` x6 (states 0x00, 0x03, 0x20, 0x31, 0x32, 0x39: paired with `A6_TheStatesWithoutHandler_CancelTheItem_...` and the locked-branch test, red before),
`A7_TheTableHasTheFiftyOneStatesOfTheDll_...` (the lists themselves), `A9_TheHeroOfANewMap_HasNoItemInUse_...` (default 0, paired with the in-use cases of (a4)). After the code: 141 of 141 green.

## 3. Proof of retraction (real mutations by script, undone, `git diff` empty after)

- The call of `CancelItemMode` removed from the locked branch: exactly 1 red, `A6_TheLockedBranchCancelsTheItem_...`.
- `TryUseItem` back to the stub (`return 1` before `StartItem`): the reds are the tests that start or refuse an item through a front of the Circle ((a1), (a2), (a3), (a4), the front of (a5), (a7) positives and their paired
  edges); the tests that never press the Circle ((a5) driver-only, (a6)) stay green. (a8) stays green too: the test sets `ItemInUse` directly, it is not reached through `TryUseItem` (the plan listed (a8) among the reds of this
  retraction). Only the first 30 red names were read, not counted exhaustively.

## 4. Suites

Release (`skipcopy.targets`), five slices because the whole suite is above 10 minutes (T0 section 4; `~Ai.` alone 6 min 41 s): `Hero` 1 845, `Arcs|Maps|Support|Combat` 686, `Audio|Cameras|Cells|Data|Dialogues|Entities|Events` 1 759,
`Ai.` 903, the rest 1 869: **7 062 tests, 0 failure** (6 921 of the HEAD + 141). Debug, the same five slices (built last): 7 062, 0 failure.
`cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical (the Debug build deposits it; the file is ignored by git).
`python -B tools/dll-structure/check_names.py --rules`: 0 finding (the 1 warning is `TextBoxGpu.cs`, old).
`docs/hero-trace-389-*.txt` rewritten by the suites (end-of-line only, `git diff --ignore-cr-at-eol --quiet` clean), restored by `git checkout`, never staged.

## 5. Tests moved

None. No existing test went red with the code, so no existing test moved (T2 will remove the sound-3 exceptions of `HeroRunTests.cs`, `HeroSwimTests.cs` and fix the comment in `RunMontage.cs`; they were not touched here).

## 6. Deviations from the plan text

1. The tests commit carries the INERT declarations (three fields, three seams, the stub journal) so that it compiles and the tests are red, not uncompilable. The behaviour is all in the code commit.
2. The pilot is called at `UpdateItemUseDriver` sites already in place (free tick, and the injury block of c2b); no test of the injury-tick call was added (c2b's `ItemUseDriverHookForTests` tests cover that site; the hook stays called first).
3. `TryUseItem` returns the answer of the start (0 when the item is put in use), as the binary does ("returns its result"); the tick of a start therefore ends in Idle/Moving (asserted by (a4)).
4. The stub driver answers 1 ("finished"), unreachable while every start is a stub (only a seam puts an item in use).
5. (a7) covers the 51 states of the DLL (T0 section 1) and the 17 states without reaction plus 0x50 and 0x7F; the 12 states carried by other slices (cape, tomes, deaths) are not asserted.
6. (a9) is a world-level check (two `ArcRun` of map 103); the weak default check is its negative half.
