# E20.g1d - results of T1 (the tests first, then the code)

Plan: `docs/plan-e20-heros.md` section 1.24.8 (revision 1, READY). T0 is in `README.md`. Date: 2026-10-27. Two commits: the tests (with the empty declarations they compile against), then the code.

## What was written

| Piece | Where |
|---|---|
| The pure rule (D-G1D-1 to D-G1D-5): `EntityDepthKey.ResolveDrawTie(role, partnerCoordinate, objectKey, partnerKey)`, the constant `HeldDrawOffset` (K = 14), `AnchorY(renderY, coordinate)`; `RaiseToCarrier` no longer gated on the collidable flags | `Alundra/Entities/EntityDepthKey.cs` |
| The pass: roles Plain / Carried / Thrown, the mark dropped by the pass, `ApplyDrawTie` writes `SortAnchorLocal`, `LocalSortOffset` and `DrawnSortCoordinate` on every visited entity | `Alundra/Maps/Loop/FrameSyncPasses.cs` |
| The mark of the throw: `EntityScriptProxy.ThrownBy` (set by `CarryRules.ThrowCarried`, not copied by `Clone`), `DrawnSortCoordinate` read by `IEffectAnchor.SortCoordinate` (D-G1D-6) | `Alundra/Entities/EntityScriptProxy.cs`, `Alundra/Hero/CarryRules.cs` |
| The comment of the deviation (ADR-0049 point 3) | `Alundra/Cells/WallPlacementOverlay.cs` (`ApplyEntitySortKey`) |

The engine is untouched: the rule goes through the existing `SortAnchorLocal` and `LocalSortOffset` of `DepthSortable2DComponent`.

## Decisions taken inside the plan

- **The mark is a proxy reference (`ThrownBy`), set unconditionally by `ThrowCarried`** (a thrower without `LogicContextEntity` is simply not ordered by the pass), so the throw tests of `CarryRulesThrowTests` need no change and a unit test can see the mark on bare proxies.
- **Who drops the mark.** The pass: object on the ground (`IsOnGround`), status other than normal, or held again (`PlatformEntity` set and the code of the throw `Flags2` back to 0). A throw not yet consumed by the program of the object (`PlatformEntity` still set, `Flags2` 1 to 3) keeps its mark and is ordered as a held object for that frame: the program of the object may run after the pass in a frame, the mark must survive until it releases the object.
- **Rendered poses.** The coordinate of the partner is `BuildSortKey` of the partner's own `DepthSortable2DComponent` on its rendered translation (probe P2: the translations are final when the pass runs); the anchor is set so that the engine's own `round(-(y + anchor) * 100)` gives exactly the wanted integer (`AnchorY` works from the integer coordinate, not from a difference of floats). An entity or a partner without a rendered pose (the bare proxies of the older unit tests) gets the offset alone and `DrawnSortCoordinate` null.
- **Thrown: the high halves are the arithmetic `>> 16` of the signed keys** (`(int)key >> 16`).
- **Written deviation of D-G1D-2** (in `RaiseToCarrier`'s doc): every held object is raised to the carrier's key, the binary raises the collidables only.

## The tests

New (43 tests, names in the list of the reds below): `Entities/EntityDrawTieTests` (10: (k1) and (p1)), `Maps/Loop/CarriedSortKeyPassTests` (13: (k1) in the pass, (w5), (g1), (e1), the mark, `ThrowCarried`), `Maps/Loop/CarriedSortKeyOracleTests` (5: (o1) against the four key files of T0 (c) plus the pin of the tick 162), `Ai/Items/BombHandsDrawOrderTests` (14: (w1) 8, (w3) 1, (w4) 4, (w5) 1), `Ai/Objects/CrateHandsDrawOrderTests` (1: (w2)). Support: `Support/DrawOrderRig.cs`, `AnnexFiles.DrawOrderKeys`.

(o1) compares, row by row, the sign of the key the renderer builds (the pass, then `BuildSortKey` on the rendered translations of two entities placed at the values of the row) with the sign of the binary: 119 + 241 + 239 + 134 rows compared (`bomb_keys`, `bomb_keys2`, `crate_keys`, `throw_probe`), 0 differences. The two written exceptions: the rows at equality are skipped, the tick 162 of `bomb_keys2` is expected FRONT (D-G1D-2); the first row of `bomb_keys` (object key 0, not in the list yet) is skipped. The pinned counts are the exact numbers of rows compared (`Assert.Equal`): 119, 241, 239 and 134.

## Red before, green after

Red run (the tests commit `fb524a29`; the declarations return the neutral values: K = 0, no rule, no anchor, no mark): 39 failed, 5 passed, of the 44 tests of the filter `EntityDrawTieTests | CarriedSortKey | DrawOrderTests` (the output of the run is in the scratchpad of the session, not in the repository). The five that passed in the red run: the old `CarriedSortKeyTests` test (rewritten in the code commit); the pin of the tick 162 (`TheTick162OfTheJumpFacingUp_IsTheOneRowWhereTheBinaryHasTheHeldBombBehind`, it reads the oracle file only); `AnEntityNeitherHeldNorThrown_HasNoAnchorAndNoOffset` (the neutral value is already the answer); `AnObjectInFlight_AtTheSamePixelAsItsThrower_GetsTheOffsetOfTheSignOfItsKey` with sign 0 (offset 0 and equal coordinates are what the stub leaves); and (g1) `AnEntityFlaggedToDestroy_StaysIgnored_HeldOrThrown` (the stub pass writes nothing, so the skip cannot be seen red: the T2 mutant "flagged entities visited" is what it must kill). The 39 reds, by name:

- `EntityDrawTieTests`: `AHeldObject_TakesTheCoordinateOfItsCarrier_AndPlusK`, `AThrownObject_IsDeltaPixelsFromItsThrower_InHundredths` (3 cases), `AThrownObjectAtTheSamePixel_GetsPlusKOrMinusK_AsItsKeyIsAboveOrBelow` (2), `AThrownObjectWithTheSameKeyAsItsThrower_GetsNoOffset`, `TheAnchor_MakesTheEngineSortCoordinateExactlyTheWantedOne`, `K_IsAboveEveryDrawOrderOfTheHeroAndOfTheLiftableObjects_OfTheExport` (K 0 must be above the hero's 13).
- `CarriedSortKeyPassTests`: `AHeldObject_IsAtTheSortCoordinateOfItsCarrier_OnTheRenderedTranslations_WithTheOffsetPlusK_AndInFrontOfHim`, `AHeldObject_IsRaisedToTheElevationOfItsCarrier_CollidableOrNot` (2 cases), `AnObjectThatLeavesTheHandsWithoutBeingThrown_GetsAZeroAnchorAndOffsetAtTheNextFrame`, `AnEntityNeitherHeldNorThrown_IsWrittenAZeroAnchorAndAZeroOffset`, `AnObjectInFlight_IsDeltaPixelsFromItsThrower_AndKeepsItsOwnElevation`, `AnObjectInFlight_AtTheSamePixelAsItsThrower_GetsTheOffsetOfTheSignOfItsKey` (2: +1 and -1), `TheMarkOfTheThrow_FallsAtTheGround_AtAnotherStatus_AndWhenTheObjectIsHeldAgain`, `ThrowCarried_MarksTheObjectWithItsThrower_AndARefusedThrowDoesNot`, `TheEffectAnchoredToAHeldOrThrownObject_SortsOnTheCoordinateOfThePass`.
- `CarriedSortKeyOracleTests.TheDrawOrder_OfTheHeldAndOfTheThrownObject_HasTheSignOfTheKeysOfTheBinary` (4 files: 119/119 rows differ in `bomb_keys`, 69 of 134 in `throw_probe`).
- `BombHandsDrawOrderTests`: `W1_...Standing` (4: the bomb behind, e.g. `down Y=100 tick 40`: elevations 102/102, coordinates 6800/10000), `W1_...WhileHeWalks` (4), `W3_...` (1), `W4_...` (4), `W5_...` (1).
- `CrateHandsDrawOrderTests.W2_...` (`Y=100 frame 1`: elevations 102/102, coordinates 6800/10000).

After the code: the 44 tests of the filter pass (43 new + the rewritten one), then the whole suite in Release (copy neutralised, `--blame-hang-timeout 60s`, `--no-build`, the six partitions of T0 (a)):

| Partition | Tests | Red | T0 count |
|---|---|---|---|
| `Ai.Objects.` | 635 | 0 | 634 (+1: W2) |
| `Ai.` apart from `Ai.Objects.` | 664 | 0 | 650 (+14: `BombHandsDrawOrderTests`) |
| `Hero.Hero*` | 1331 | 0 | 1331 |
| `Hero.` apart from `Hero.Hero*` | 619 | 0 | 619 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2246 | 0 | 2236 (+10: `EntityDrawTieTests`) |
| everything else | 2224 | 0 | 2206 (+18: `CarriedSortKeyPassTests` 13, `CarriedSortKeyOracleTests` 5) |
| total | **7719** | **0** | 7676 (+43) |

The Debug construction and its suite, the mutants and the ADR are T2.

## The existing tests that moved (against the predicted list of T0 (e))

One, as predicted: `Maps/Loop/CarriedSortKeyTests.AnObjectInTheHandsOfTheHero_IsSortedAtLeastAsItsCarrier_WhereTheOthersKeepTheirOwnKey`. Run with the code and the old assertions it fails (`Expected: 102, Actual: 166`: the not-collidable and the no-collision held objects are now raised); rewritten by derivation: the two objects now expect the carrier's `Elevation` (`hero.Depth.Elevation`); the three other cases (loose, already in front, collidable) keep their values. No other existing test moved: the six partitions above are green with no other test file edited (the only other edit of an existing test file is `AnnexFiles.cs`, one added method).

## Deviations from the plan

- **The tests commit carries the empty declarations** (`HeldDrawOffset = 0`, `ResolveDrawTie` returning the neutral value, `AnchorY` returning 0, the two proxy fields) so that it builds and the tests are red by assertion, not by compile error. The code commit replaces them.
- **The row counts of (o1)** were first written as minimums from the sign tallies of the T0 README (which count the equality rows and the rows not compared); the first green run gave the true numbers and they are pinned with `Assert.Equal` in the code commit.
- The Debug construction and suite, the 12 mutants, the ADR, `cmp` and the verifiers are T2 and T3, not done here.
