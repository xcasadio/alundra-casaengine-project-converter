# E20.e0d, T0 (2026-10-25): prerequisites, deposit, replay, probes of the DLL, preset cells, predicted moved tests

Branch `chantier/theme2-attaques-objets` at `7fbbf2dd` (= `main`; the theme 1 branch `chantier/theme1-porter-plateforme` is an ancestor of `main`). No change to
`Alundra/`, `Alundra.Tests/`, the engine, the converter or the data: the probes and the temporary wiring were applied, measured and undone
(`git diff -- Alundra` empty afterwards; the scratch test folder `Alundra.Tests/ScratchE0d/` deleted, never indexed).

## (a) Prerequisites

| prerequisite | measured |
|---|---|
| theme 1 merged in `main` | `git merge-base --is-ancestor chantier/theme1-porter-plateforme main`: yes |
| theme 2 branch created from `main` | HEAD `7fbbf2dd` = `main` |
| E20.e0c (1.26), E20.e0c2 (1.26.1), E20.g1b (1.24.4) done | headers of the plan: all three marked done and CONFIRMED |
| D-E20-78 | present in section 1.0 of the plan |
| next free ADR number | 0094 (the highest on any branch is 0093, `git ls-tree` on all branches) |

## (b) Deposit

- `docs/plan-e20-annexe/e20-e0d-disc/`: the chest reading (`report-main.md`, `critic-main.md`, scripts, outputs, listings), 58 files, without `emu/`, `e0c_copy/`, `__pycache__`.
- `docs/plan-e20-annexe/e20-theme2/`: the theme 2 inventory (`report-main.md`, `critic-main.md`, scripts), 36 files, without `__pycache__`.
- Each folder has a `README.md` and a `SHA1SUMS.txt`; this folder (`e20-e0d/`) has its own `SHA1SUMS.txt`.

## (c) Replay from a copy

The whole reading folder (with its `emu/`) was copied to a scratch folder, the absolute scratchpad path replaced by the copy's, and the four scripts run again
(Python 3.14, `python -B`). Outputs against the reading's, `cmp` (`replay/cmp.txt`, the new outputs are `replay/*.replay.out`):

| script | result |
|---|---|
| `f255_oracle.py` | identical (12 000 vectors, 0 gap) |
| `seed_oracle.py` | identical (MpMax 0 to 5 table) |
| `chest_sequence.py` | identical (the three cases: herb in stock 3, herb in stock 9, the seed) |
| `chest_anim_oracle.py` | identical (rows by direction: chain 2 to 1 at tick 61 for the direction 0, 2 to 0 for the directions 1 and 2, 81 ticks for the direction 3) |

## (d) Probes of the DLL (throwaway test, never committed; `probe-dll/E0dProbeTests.cs.txt`, outputs `probe-dll/probe*.txt`)

Release build with the neutralised copy, `ArcRun` worlds (real controller, prefabs), one logic tick per frame (`0.02 s`).

1. **Sound 51 and the chain 2 to 1 in 61 ticks on a real chest** (`probeA`, record 37 of the Debug zone, `Target` set to 2 by the probe before the first tick): at tick 1
   `Current = 2` and the sound 51 is requested (exactly one request, the only one); the animation clock reads `LogicalTick` 59 at tick 60 and 60 at tick 61; at tick 62
   `Target = Current = 1`, `LogicalTick = 0`. So the animation lasts 61 ticks and the chain shows at the 62nd tick, as the binary replay (`chest_sequence.out`: "t = 62
   chest animation 2 ends, chain to 1"). The fact holds, no contradiction. The direction of the chest is 0.
2. **An entity without gravity rising by `ForceZ`** (`probeB`, a dropped object made inert with `+0x274 = 2`, Collidable and Gravity cleared, `ForceZ = 0x8000`): `ForceZ` stays
   `0x8000`, `PosZ` rises by exactly `0x8000` per tick (`0x8000` after tick 1, `0x100000` after tick 32, `0x200000` = 32 px after tick 64), `IsOnGround` 0, status Normal. The rise of
   the object born by the chest works with no change.
3. **What the DLL draws and animates during a `Block`** (`probeC`, the Beannoid arena, map 14, 23 animated entities, `Block` called with a dropped object as the blocker): every
   entity (and the hero) gets `BlockedByEntity != null`; the logic is frozen (position, `LogicalTick` 19 stays 19); the entities stay drawn (`Entity.IsVisible` true); **but the
   drawn animation keeps running**: `CurrentAnimationTimeSeconds` goes 0.400, 0.420, 0.440, ..., 0.780 (tick 20), wraps to 0.380 (tick 40) while `IsPlaybackPaused` is false. The
   binary freezes the image (the entity leaves the active list, its animation update stops). **Gap measured: (b8) is red before the fix.** The fix is on the DLL side (no engine
   need): the engine already exposes `AnimatedSpriteComponent.IsPlaybackPaused` (set by `GameplayFreeze` for the open menu). Note for the plan: the place is where `BlockedByEntity` is
   written (`WorldProxy.Block`/`Unblock`, `Alundra/Maps/WorldProxy.cs`), or a pass that applies the pause from `BlockedByEntity != null` the way `GameplayFreeze.Apply` does for the mask,
   keeping an animation paused by something else paused (`AnimationWasPaused`); `FrameSyncPasses.cs` has the animation sync but no `IsPlaybackPaused`.
4. **Is an object held in Loaded drawn?** (`probeD`, the status put back to Loaded after the programs pass of every tick, as A255 of the object does with `+0x10 = 1`): the DLL keeps
   it Loaded (the pick sets Normal before it runs the slot A, `EntityScriptProxy.PickEventTrigger`, so a status written by A255 persists), it is not simulated (`PosZ` fixed although
   `ForceZ = 0x8000`; the animation time and clock stay 0), **but `Entity.IsVisible` stays true: it is drawn**. The binary does not draw a Loaded entity. **Gap measured: (b9) is red
   before the fix.** Only `FlagToDestroy` hides an entity today (`FrameSyncPasses.SyncAnimation`). Control: the same object left alone is promoted to Normal at the first tick (visible).
5. **The equivalent of `[0x800DC4C4]`** (`probeE`): `WarpDirector.Instance.IsTransitionInProgress` is false before; it is true from the call of `BeginDeparture` /
   `BeginDepartureFromChangeMapOpcode` (the request of a map change) and stays true on the 24 ticks measured; `InstallForMapEntry` lowers it. It is the gate `InventoryDirector` and
   the save director already read. `IsWarpDelayRunning` is a different variable (the 0.2 s window after a map entry, 5 ticks measured, the binary's `g_warpDelayFrames`) and is not it.
   The F255 guard is therefore `WarpDirector.Instance.IsTransitionInProgress`.
6. **Square against a chest at HEAD** (`probeH`): after walking down into the chest and pressing Square, `ActiveCollisionEntity` is null and the chest does not change (Target 0,
   no dialogue): the state of today ("nothing happens"), as the plan says.
7. **Spawn zones** (`probeF`, `probeG`): the records of the map 115 are spawned according to the hero's arrival tile (the spawn zone box): a run started on the default tile has 3 entities and
   no chest; an arrival (`LeverArcs.Spec`, `Arrival`) at the tile (39, 10, 0) spawns the vase chest (record 10, tile (39, 9, 0), contents 83, flag 659) and at (43, 46, 1) the herbs chest
   (record 11, contents 36, flag 658). The chain test (b6) must use an arrival, not a start tile.

## (e) Cells of the presets (arrival tile = the tile north of the chest; a save cannot carry the direction, always south, so the chest is straight ahead)

The entity tile of a chest is `(XPos / 2, (YPos + 1) / 2, Height / 2)` of the record in the census: for example record (70, 83, 10) is the tile (35, 42, 5) (the Y is rounded up). The hero
arrives on the tile north of it, same level. Measured through the production arrival (`probeH`): the chest is present, the hero stands (`IsOnGround` 1) on the arrival tile.

| preset | map | arrival tile (x, y, z) | the chest in front | notes |
|---|---|---|---|---|
| `coffre-m15` | 15 (Overworld 3,2) | (35, 41, 5) | record 4: the magic seed (contents 80), flag 1741, tile (35, 42, 5) | the same map has 8 other chests: records 5, 18, 19, 20 (30 florins, tiles (13..16, 16, 9)), 11 (the golden hawk, (5, 44, 9)), 21, 22, 23 (herbs, (4, 51, 4), (2, 51, 4), (5, 53, 4)); the preset should start with MpMax between 0 and 3 (the seed adds 1 up to 4) |
| `coffre-m11` (vase) | 11 (Debug zone) | (7, 23, 13) | record 30: the life vessel (contents 83), flag 0 (refilled at every entry) | the other chest of the map: record 37, the herbs, flag 2000, tile (17, 35, 10), arrival (17, 34, 10) |
| `coffre-m0` | 0 (Test Map) | (13, 2, 0) | record 2: the Epee (contents 2), flag 0 | 32 chests in a grid of 5 tiles; weapons at the rows 3 (records 2, 3, 5, 9: tiles (13, 3), (18, 3), (28, 3), (48, 3)) |
| `coffre-m117` | 117 (Tarn's Manor unused broken) | (42, 40, 2) | record 19: the Key (contents 61), flag 260, tile (42, 41, 2) | the only chest of the map |

The vase chest of the map 115 (record 10, tile (39, 9, 0)) and the herbs chest (record 11, (43, 46, 1)) are not presets (the chain is tested by arcs).

## (f) Tests moved by a temporary wiring (undone)

A temporary wiring, an approximation of the three phases (`probe-dll/temp-wiring.diff.txt`, built by `probe-dll/apply-temp-wiring.py.txt`; 254 diff lines, undone by `git checkout`): A255 registered for
the type 30 beside the 87 object types (the chest sets `Target = 1` when empty), the key `(F, 255)` for the type 30 (the guards, the birth of the object with the chest as parent, `Block`, the words of
the object), the kind 2 branches of A255 and C255 of the object, the body of the seed in `ItemEffects`, and the pause of the sprite playback in `Block`/`Unblock`. It does not touch the
`0x82` of the interpreter (see below).

Three partitions of the whole suite (Release, neutralised copy, `--blame-hang-timeout 60s`; whole namespaces with the final dot, the third the exact negation of the two others plus the scratch
folder; script `suite-wired/run.sh`, summaries and red lists in `suite-wired/`):

| partition | total | failed | duration |
|---|---|---|---|
| p1 (Arcs. Ai. Audio. Cameras. Cells. Combat. Dialogues.) | 1 515 | 7 | 6 min 1 s |
| p2 (Entities. Events. ExportGuards. Hero. Items.) | 2 956 | 1 | 3 min 58 s |
| p3 (negation) | 1 858 | 0 | 50 s |
| total | 6 329 | 8 cases (7 methods) | |

The 6 329 is the count at HEAD (the commit of g2b2 says 6 329 green): no test was added or removed by the wiring.

Moved (red with the wiring):

1. `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` (registry count)
2. `Alundra.Tests.Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses` (registry count)
3. `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` (registry count)
4. `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (registry count)
5. `Alundra.Tests.Ai.Objects.PickupAiDropWorldTests.W7_AChest_DoesNothing_WhileTheSameObjectMadeDroppedIsTaken` (2 cases: kind 2 with 1 and 3 ticks per frame)
6. `Alundra.Tests.Ai.Objects.PickupAiPlacedTests.P8_TheChest_HasNoHandlerInTheRegistry_AndTheBranchOfTheChestInC255IsANamedStubAtEveryState`
7. `Alundra.Tests.Items.ItemEffectsTests.TheObjects0x50_IsANamedStub_ThatChangesNothing_WhileAnotherObjectOfTheSameRangeIsGiven` (the seed)

Not moved by this wiring but predicted by reading (the wiring did not touch what they pin):

8. `Alundra.Tests.Ai.Objects.PickupAiPlacedTests.P1_A255_OfADroppedObject_ReturnsAtOnce_AndTheChestIsANamedStub_TheirFlagsAndWordsAreKept` (the kind 2 variant of A255 of the object: the real branch
   reads the chest through the parent and will change the status or the words; the other two `P1_` tests of the file, the placed object and the placed object with a flag, stay).
9. `Alundra.Tests.Items.ItemAndMoneyOpcodesTests.Give_0x82_TheOtherHeroSubCases_StayDegraded_AnswerOne_AndWriteNothing` (the case `id = 0x50` of the theory only: phase C stops degrading `0x82` for the seed).

So 9 methods, 10 test cases (the theory counts per case), not more than 10: **the stop condition "more than 10 existing tests moved" does not trip** (at the limit by cases, below it by methods).
The plan's list ("the pins of the registry, `PickupAiDropWorldTests` w7, `PickupAiPlacedTests` P1 and P8, `ItemEffectsTests` of the seed") is complete; the registry pins are the four
count tests above (1 to 4), and `Give_0x82_...` (9) is the one the plan's list does not name (the interpreter test of the degraded `0x82`). Every other test of the three partitions stayed green.

The fix of D-E0D-4 (pause of the blocked sprites) and of D-E0D-5 (hide a Loaded entity) are approximated here only for the chest's object (visibility) and for `Block`/`Unblock` (pause): no existing test
pins `IsVisible` of a Loaded entity or `IsPlaybackPaused` of a blocked one (`rg` on `Alundra.Tests`: only `GameplayFreezeTests` pins `IsPlaybackPaused`, for the menu freeze, and
`WorldProxyEntityManipulationTests` pins `IsVisible` of a flagged entity); a fix that hides every Loaded entity (not just the chest's object) is a wider change to measure in T2.

## Stops

None. No probe contradicted a fact of the plan; two gaps were measured that the plan expected as possible (b8: the blocked entities keep animating on screen; b9: a Loaded object is drawn), so the tests
(b8) and (b9) are red before their fix; no need for the engine; at most 10 existing tests moved (9 methods, 10 cases).

## Files

`replay/` (the four new outputs and `cmp.txt`); `probe-dll/` (`E0dProbeTests.cs.txt`, the 21 probe outputs, `temp-wiring.diff.txt`, `apply-temp-wiring.py.txt`); `suite-wired/` (`run.sh`, `p1/p2/p3.summary` and `.red`); `SHA1SUMS.txt`.
