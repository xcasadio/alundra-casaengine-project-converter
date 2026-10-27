# E20.g1d - results of T0 (versement, oracle, sondes, liste prédite)

Plan: `docs/plan-e20-heros.md` section 1.24.8 (revision 1, READY). T0 writes no code: docs and measures only. Date: 2026-10-27.

## (a) The HEAD of start and the suite

HEAD of start: `5516fd36` (branch `chantier/theme2-attaques-objets`), working tree clean apart from the author's untracked files and the two modified submodule pointers (`CasaEngineMonogame`, `alundra-datas-analyser`: not touched).

Suite in Release (copy neutralised: `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets` on build AND test), `--blame-hang-timeout 60s`, `--no-build`, six disjoint partitions by namespace prefix (a single call of the whole suite exceeds the 10 minutes of a foreground call):

| Partition | Tests | Red |
|---|---|---|
| `Alundra.Tests.Ai.Objects.` | 634 | 0 |
| `Alundra.Tests.Ai.` apart from `Ai.Objects.` | 650 | 0 |
| `Alundra.Tests.Hero.Hero*` | 1331 | 0 |
| `Alundra.Tests.Hero.` apart from `Hero.Hero*` | 619 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2236 | 0 |
| everything else | 2206 | 0 |
| total | **7676** | **0** |

`dotnet test --list-tests` on the same binaries gives 7679 names = 7676 + the 3 throwaway probes of (d): the count of the HEAD of start is 7676 (the h4c1 T2 record says 7675 before its T3 commit `abed9330`, which added tests). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` clean, restored by `git checkout`, never indexed.

## (b) The discovery and its critique

Poured to `docs/plan-e20-annexe/e20-g1d-disc/` (see its README): the two reports, the scripts, the disassemblies and the outputs; without the oracle copies and the `__pycache__`.

## (c) The keys, replayed twice

`keys/`: `bomb_keys.py`, `bomb_keys2.py`, `crate_keys.py`, `throw_probe.py` (the scenarios of the discovery and of the critique, unchanged, but EVERY tick printed in one common format by `keyrows.py`), `run_all.sh` (copies the three oracles from the annexes, never edited, into a scratch directory, runs each script twice and compares byte for byte), `out_*.txt` (the keys), `SHA1SUMS` (the hash of each output and of its second run: identical).

Columns: `scenario | tick | hero: anim dir idsv y z flags afl | obj: anim idsv y z flags afl carrier | hero key | obj key | slot hero | slot obj | sign` (`FRONT` = object key above the hero's, drawn later; `BEHIND`; `EQUAL`: the binary does not order two equal keys). Z is printed as the binary has it (the DLL's minus one unit, ADR-0026, hence `0.00002`).

Cross-check with the discovery's own outputs: the key columns agree on every row printed there (`out_bomb_keys2.txt` 50 rows, `out_bomb_keys.txt` 18 rows, 0 difference); the sign counts are the reading's.

| Key | Rows | Signs |
|---|---|---|
| `bomb_keys` | 120 | FRONT 119, BEHIND 1 (the tick 1: the bomb is born, key `00000000`, not yet in the draw list) |
| `bomb_keys2` | 294 | carried 241: FRONT 240, BEHIND 1 (tick 162, the take-off of the jump facing up: hero IDSV 6, bomb 5, `00C90005` against `00C80025`); not carried 53: BEHIND 8 (ticks 255 to 262, the explosion in the hands and the wound, no throw in this scenario), FRONT 45 |
| `crate_keys` | 270 | `crate_east`, `crate_south`: FRONT 90 each; `crate_north`: BEHIND 10 (before the lift), EQUAL 1 (the tick 11, the lift: the crate raised to the carrier's key `00750000`), FRONT 79 |
| `throw_probe` | 172 | right and left: FRONT 24 then EQUAL 19 from the tick 72 (the landing, equal keys `00CD0000`); down: FRONT 43 (the bomb lands below the hero); up: FRONT 5 then BEHIND 38 from the tick 53 (the 4th tick of the flight) |

What (o1) of T1 must treat (decided here, from the rows): it compares the CARRIED rows (carrier word set) and the rows of a flight after the Square of `throw_probe`; it skips the rows at equality (`crate_north` tick 11, the `EQUAL` rows of `throw_left`/`throw_right`), the first row of `bomb_keys` (object key 0: not in the draw list yet) and the rows of `bomb_keys2` from the tick 255 (the bomb is not carried and was not thrown by the hero: it exploded in his hands; the rule gives it (0, 0) and 0 there); the tick 162 is expected FRONT (D-G1D-2).

## (d) The probes of the DLL

The predictions were written before any probe ran: `probes/predictions.md` (14:42 +01:00). The probes are throwaway tests (never committed; the code is kept as `probes/G1dT0ProbeTests.cs.txt`) on the bench of `BombHandsWorldTests` (`BombHandsRun`: the real hero, the real world of map 14, the real registry). P2 needs to look inside the pass: a ONE-LINE static hook at the entry of `FrameSyncPasses.RunWallInterleaveSortKeyPass` was added for the run and reverted (`git checkout`, the diff is kept as `probes/temporary-hook.diff.txt`); `git diff -- Alundra` is empty after. Outputs and hashes: `probes/out_p1.txt`, `out_p2.txt`, `out_p3.txt`, `out_p4_draworder.txt`, `probes/SHA1SUMS`.

| Probe | Prediction | Measured | Verdict |
|---|---|---|---|
| P1 the bomb in the hands, 16 rows of Y, 4 directions, ticks 40 to 44 | down: BEHIND 15 / FRONT 1; left, right: BEHIND 16; up: BEHIND 13 / FRONT 3 | down 15 / 1 (the front row is Y = 107, `Y mod 16 = 11`); left 16 / 0; up 13 / 3 (Y = 107, 108, 109); right 16 / 0. Every row: hero and bomb `Elevation` 102 against 118 only in the front rows, coordinates `100 Y` against `100 (Y - 32)` | held (the discovery's 15 of 16 for the face down is exact) |
| P2 the order of the passes | the sort pass runs after the rendered translations are updated: equal to the bit at every frame | 137 frames with the bomb (carry, walk east from tick 50, jump with the Cross at tick 80, hero Z up to 27.5 px, fractional steps 13.5, 22.5, ...): 0 frame where the translations of the hero and of the bomb at the entry of the pass differ from those at the end of the frame (what the renderer reads) | held: `PublishEntityFrame` (line 2647) precedes the pass (line 2692) and `FollowCarrier` pushes the carried root inside the tick; the anchor of D-G1D-2 is computed on the rendered translations inside the pass, no fallback on the logical positions |
| P3 `ThrowCarried` then the program | the hands are free and `PlatformEntity` null from the tick 50; `IsOnGround` 0 during the whole flight, 1 first at the tick 72 (the oracle's Z = 0 and the equal keys), for the four directions | right, left, down, up: tick 50 hero state 9, bomb `PlatformEntity` null, `Status` Normal (2), `IsOnGround` 0; Z 36.0, 39.5, ... 17.0 (69), 4.5 (71) as the oracle; `IsOnGround` 1 at the tick 72 and after; Z 0 at 72; no relift | held: a mark set by `ThrowCarried` is readable from the tick 50 and falls with `IsOnGround` at the tick 72 |
| P4 the `DrawOrder` | hero 13, bomb 6, the 17 records at most 6 | hero 13 (376 files), bomb 6 (12 files), the 17 liftable records: 0 for 15 of them, 2 for the torch (137), 6 for the bomb (alundra_2); maximum 6; `K = max(14, 6 + 1) = 14` | held |

No probe contradicted its prediction: no stop.

## (e) The predicted list of the tests that move

Method: every test source of `Alundra.Tests` that names `SortAnchorLocal`, `LocalSortOffset`, `SortCoordinate`, `RaiseToCarrier`, `EntityDepthKey`, `RunWallInterleaveSortKeyPass`, `ApplyEntitySortKey`, `ComputeEntityElevation`, `ThrowCarried`, `BuildSortKey`, `.Elevation` or `IEffectAnchor` was read at the HEAD of start (the list is by symbol, not by line number).

**Predicted to move (1 test, rewritten by derivation):**
- `Maps/Loop/CarriedSortKeyTests.AnObjectInTheHandsOfTheHero_IsSortedAtLeastAsItsCarrier_WhereTheOthersKeepTheirOwnKey`: its last assertions keep the `notCollidable` object (flag `Collidable` clear) and the `noCollision` object (animation flag `0x80`) at their own `Elevation`; D-G1D-2 raises the `Elevation` of EVERY carried object, collidable or not, so both become the carrier's. The three other cases (the carried object already in front, the collidable one raised, the loose one) keep their values.

**Predicted NOT to move** (read, with the reason):
- `Maps/WorldProxyEntityManipulationTests` (the `FlagToDestroy` skip and the relaunched-animation bias): entities that are not carried and not thrown; the pass writes (0, 0) and 0 (the defaults they already hold) and still skips `FlagToDestroy`. Note for T1: these entities carry no `RenderProjectionComponent` and the hero of `CarriedSortKeyTests` neither: the rule must compute nothing from translations for an entity that is neither carried nor thrown, and the carried case of `CarriedSortKeyTests` has no projection either (its test must keep the Elevation path independent of the translations).
- `Rendering/Effects/FrozenAnchorTests`, `DestroyEntityBreakEffectTests`, `Ai/Enemies/BeannoideOracleTests`, `MurutaOracleTests`, `Entities/EntityDepthKeyTests`: they read `EntityDepthKey.Compute(...)` / `IEffectAnchor.SortCoordinate` of entities that are not carried; `Compute` is not modified.
- `Rendering/Effects/EffectQuadPushTests` (key of the aura: elevation 214, coordinate 11100, local offset), `Hero/HeroAuraWorldTests` B5, `Combat/HitEffectWorldTests`, `EffectStageNowTests`, `NativeEffectTests`: the hero carries nothing there.
- `Cells/CellVisualSyncTests`, `Cells/WallPlacementOverlayTests`, `Maps/WorldProxySpawnInitializationTests`: the overlay tiles and `ApplyEntitySortKey`/`ComputeEntityElevation`, whose signatures stay.
- `Ai/Objects/C4WorldTests` and `LiftableAiMap392Tests` (snapshots of the primitive and enum fields of a proxy before and after): a new field on the proxy that is a reference (the thrower mark) is not read by them, and a primitive one is unchanged at rest.
- `Hero/CarryRulesThrowTests`, `Ai/Objects/LiftableBreakProductionTests`, `BreakRun`, `C4Run`, `Ai/Items/BombHandsWorldTests` (they call `ThrowCarried` and compare the object's and the hero's fields, positions, states, effects): the mark is a new field, the fields they compare keep their values.
- `SaveGames/SaveGameTests` (reads the public fields of the save record by reflection): not the proxy.

The remaining ~7670 tests do not touch the sort path.

## Deviations from the plan

- The suite ran in six partitions (a foreground call is limited to 10 minutes), not in one call.
- The key files carry every tick in one common format instead of the filtered rows of the discovery's outputs (same scenarios, same oracle, same keys; the cross-check above).
- Nothing else; no stop condition met.
