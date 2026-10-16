# ADR-0070: The step of an entity born by the sprite-index interface is bounded to the map edge before the mover and flags the same tick

- **Status**: Accepted
- **Date**: 2026-10-16
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.18 (E20.e0k, D-E0K-7) and 1.18.3 (E20.e0k3, "Résultats"); the forces pass of `ALUN_CD.EXE` (France) at `0x800366FC`-`0x800367E0` (strict `slt` at `0x80036774`, `0x80036788`, `0x800367AC`, `0x800367C0`); oracle `docs/plan-e20-annexe/e20-e0k/oracle/` (keys `arrows2-map-edge`, `edge-push-east`, `edge-push-diag`, `edge-exact`); ADR-0068 (the map state), ADR-0069 (the marker `HasLogicalZ` and the logical step)

## Context

- The DLL's kinematic tick (`AlundraScriptedMotion.RunOneKinematicTick`) did not port the binary's clamp of the step to the grid of the map (52 x 60 cells, bounds `NegModX/Y` and `ScreenClipX/Y`, already stored by `SetEntityDimensions`): an arrow or a walker pushed against the edge of the map went past it, and the flag `ForceAdjusted` (`+0x13C`) was not raised on the tick of the clamp.
- The binary compares the arrival with each bound STRICTLY (two `slt` per axis): a step that arrives exactly on a bound is neither bounded nor flagged. The oracle confirms it (`edge-exact`: tick 1 on the bound, flag 0; tick 2 bounded, flag 1).
- `SlideAlongWall` rewrites `ForceAdjusted` after the mover (0 on an axis that progressed), so a flag raised before it is lost on a diagonal push and on the first tick of a cardinal clamp.
- The hero and the entities born from a record keep their own rule (O-E19-17, D-E19-25); only the entities born by the interface carry the marker `HasLogicalZ` (ADR-0069).

## Decision

- **D-E0K-7**: `AlundraScriptedMotion.ClampStepToMapEdge(entity, stepX, stepY)` is a pure function (it reads the position and the four bounds, moves nothing) returning the step to take on each axis and whether an edge bounded it. Per axis the arrival `Pos + step` is compared strictly: under the low bound it is brought back to it, else over the high bound it is brought back to it, and the flag is set; the axes are independent.
- `RunOneKinematicTick` calls it for an entity marked `HasLogicalZ` only, on `FinalForceX/Y`, BEFORE the mover (with or without a controller), and writes the bounded step back to `FinalForceX/Y` (the binary writes `+0xDC`/`+0xE0`). The flag is raised AFTER `SlideAlongWall` (and after the controller-less integration), so that the slide cannot erase it, and the same tick as the clamp.
- The bound is that of the entity's own box (`NegMod*` and `ScreenClip*`), not the raw grid: the offsets of the prefab's box are part of it.
- The bounce of the arrow at the edge (oracle `arrows2-map-edge`, tick 8: direction 8, flag 0) belongs to its E17 handler (`0x8007F420`), not ported; in this slice the arrow keeps its direction and is bounded again, and the tick-8 pin is carried by E20.f5.

## Consequences

- A marked entity pushed east or north-east against the edge follows the oracle series to the raw unit (`edge-push-east`: bound at tick 7, flag the same tick and kept; `edge-push-diag`: X bounded at tick 10 while Y goes on), and an arrival exactly on the bound is left alone.
- The hero, the entities from a record and every unmarked entity are unchanged (pinned by world tests that put each of them against the same edge next to a bounded marked arrow). No existing test moved.
- There is still no production caller of the interface until E20.f3 (the shot) and E20.h3 (the items): the change is inert in play until then.
- Limits: the edge for the entities from a record and for the hero (O-E0K-4); the drift of the tile and the fraction of the pixel (O-E0K-3, O-E0K-7); the halving loop of `ComputeXY` for marked entities (E20.e0k4, on request).
- Risks: a later move of the flag raise before `SlideAlongWall`, or of the clamp after the mover, breaks the same-tick contract; the world tests B1 and B2 and the mutants listed in the plan catch each.
