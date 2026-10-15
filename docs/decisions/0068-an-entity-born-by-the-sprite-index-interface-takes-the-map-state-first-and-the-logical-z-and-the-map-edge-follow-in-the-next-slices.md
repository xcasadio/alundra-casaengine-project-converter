# ADR-0068: An entity born by the sprite-index interface takes the map state first; the logical Z, the vertical step and the map edge follow in the next slices

- **Status**: Accepted
- **Date**: 2026-10-16
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.18 (E20.e0k) and 1.18.1 (E20.e0k1, "Résultats"); decisions D-E0K-1 to D-E0K-10, Q-K1 and Q-K2 (AUTO-mode defaults); oracle `docs/plan-e20-annexe/e20-e0k/` (88-file manifest, 18 mutants); ADR-0063 to ADR-0065 (the interface, the slot table, the marker)

## Context

- The entities that are not the hero and are born during play (arrow, balls, wave, thrown bomb or crate) are created by `AlundraWorldProxy.SpawnEntityBySpriteIndex` through `AlundraEntitySpawnFactory.CreateEntityFromSpriteIndex`, a core that had no record and no tile map. The lines of `ApplySpawnInitialization` that read the tile data of the map (the gravity and viscosity words, the walkability mask of the flags, the external ownership of the vertical, `MinMoveDistance` 0) were therefore never run for them.
- Measured at the phase A commit (probes P1 to P6 of the plan, E20.e0k1 T2): without that state a ball born 32 px above a flat field stays at 32 px (its `ForceZ` is bounded to 0 by a gravity word of 0), an arrow crosses a wall that only the walkability mask blocks, and a rising entity is cancelled by the engine's own vertical.
- The emulated binary (`ALUN_CD.EXE`, France) gives the reference: ball Z series 31.5, 30.5, 29, 27, 24.5, 21.5, 18, 14, 9.5, 4.5, then 0 at tick 11; arrow stopped at X = 349 px with the wall flag at tick 43 or 44 according to the start (66.8 % "+1", 33.2 % "+2" over 20 000 raw gaps of the model of the halving refinement).
- With the state alone, the DLL's Z is the oracle minus TWO raw units during the fall (the root truncates the position to the pixel at birth and `PosZ` is pulled back from the root at tick 1), where the plan predicted one. That is the defect of the logical Z (D-F3-8), not of the state.

## Decision

- **D-E0K-2, delivered by E20.e0k1**: the state is written by one shared helper, `AlundraEntitySpawnFactory.ApplyMapDependentState`, extracted unchanged (same lines, same order) from `ApplySpawnInitialization` and called by it. `CreateEntityFromSpriteIndex` takes an optional `TileMapData?` (null: nothing is read) and calls the helper right after the header initialisation; `SpawnEntityBySpriteIndex` passes the world's `_tileMapData`. The wiring line is the only change of behaviour and the only branching; removing it removes the slice. The record path does not change by a byte (guarded by a test per record and by a mutant).
- **D-E0K-10**: every pin that moves is rewritten by a derivation (a value computed in the test, or the oracle key minus a raw unit), never by a pasted constant. In E20.e0k1 no existing test moved.
- **Planned, not delivered here** (recorded so that the next slices do not re-decide them; the sub-slices keep their own tasks and may refine them):
  - **D-E0K-1**: a dedicated marker `HasLogicalZ`, posed by `SpawnEntityBySpriteIndex` only (in the block of the marker of ADR-0065, after the helper), never cloned, never posed by the record path nor for the hero (not `EntityRefId == -1`, not `JoinsBirthTick`).
  - **D-E0K-3**: one law of force, extracted from the controller path and shared with the step of the logical Z.
  - **D-E0K-4 and D-E0K-5 (E20.e0k2)**: `PosZ` belongs to the tick for a marked entity, the root follows it, the vertical step runs before the XY, with the strict landing, the absolute ceiling and the 3 px magnet; `StepHeight` is the prefab's only with gravity and `ForceZ == 0`; the entity declares itself in the air at every tick.
  - **D-E0K-6 and D-E0K-7 (E20.e0k3)**: the engine's mover and `SlideAlongWall` stay; for marked entities only, the step is clipped to the map edge before the mover and the flag is raised the same tick and kept through the slide. The hero and the record entities stay under D-E19-25.
  - **D-E0K-8, D-E0K-9**: the out-of-scope items have a proposed owner in the plan (O-E0K-1 to O-E0K-9); no shadow phase (the marker bounds the reach, no production caller exists).
- **Provisional AUTO-mode defaults, to be confirmed or reversed by the author**: **Q-K1 option A** (only what is born during play follows the exact fall rules; what the map places keeps its older rule until E19.h2) and **Q-K2 option A** (accept the one-tick gap of the wall flag against the binary, documented; the full halving loop for marked entities, E20.e0k4, is built only on the author's request). Probe P4 measured the gap on the integer starts: 5 of the 10 starts of the family C1 as given (2 of the 6 distinct whole-pixel starts) flag one tick earlier than the binary.

## Consequences

- Entities born by the interface now behave on the map as a record entity does for gravity words, walk mask, vertical ownership and move threshold: the world tests (arrow stops at X = 349 and raises the flag, ball lands at the oracle's tick 11, map 159 barely falls, fields equal to the record's) turn from red to green with the wiring line and nothing else moves (4 898 of 4 898 in Release and Debug).
- Until E20.e0k2 the Z series of such an entity is the oracle minus two raw units during a fall and minus one at rest; the tests of E20.e0k1 do not pin the raw unit. E20.e0k2 keeps the "oracle minus one" criterion and must make it true through the logical Z.
- There is no production caller of the interface yet (the shot is E20.f3, the items E20.h3): the change is inert in play until then.
- Risks: a later change of the helper reaches both creation paths; the guard test of the record path and the mutants listed in the plan catch a drift. If Q-K1 or Q-K2 is answered with option B, the sub-slices e0k2/e0k4 are re-planned, not this decision.
