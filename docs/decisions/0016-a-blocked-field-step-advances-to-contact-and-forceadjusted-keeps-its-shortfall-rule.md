# ADR-0016: A blocked step on the cell field advances to contact, and ForceAdjusted keeps its shortfall rule

- **Status**: Accepted
- **Date**: 2026-09-29
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, E19.a "Recette en jeu T7" and slice E19.a2 (author decisions D-E19-8 to D-E19-11 of 2026-09-29), after a diagnosis on the real map 390 with the hero's real controller; engine side: `CasaEngineMonogame/ai-agent/tasks/field-move-to-contact-tasks.md` and engine ADR-0045

## Context

- In the author's in-game recipe of E19.a, Alundra stays next to his bed in the cabin of map 390, and map 476 never loads.
- The cabin script (map event B2) marks fifteen cells with opcode `0x54` (`W |= 1`). The hero carries ClassB, so his walkability mask is `0x41`, as in the original `GetCollisionFlagsWithPlayer` (`0x80037488`): these cells stop him and form rails. The script then walks him exactly 80, 48, 80 and 32 px, each walk waited on by `0x1E` (complete when the distance truncated to whole pixels reaches the operand).
- The second north walk ends against the rail at cell (42,12):
  - the original advances a blocked step to contact by halving the force (`ComputeXYPosition`, `0x80037730`), ends at y = 215.0, and the walk measures 80 px;
  - the engine's character controller rejects a blocked axis step whole on the cell field (a simplification decided as C5 in `docs/plan-e3-collisions.md`), stops the hero at y = 216.34, 78.8 px, and `0x1E` waits forever.
- Placing the hero at the original's contact point lets the whole scene finish in a diagnostic.
- Modern character controllers also sweep up to the contact point and do not reject the whole step. The rigid-collider sweep of the same engine controller already does.
- The DLL raises `ForceAdjusted` when a step falls short of the request by more than 0.01 px. The original raises it only on a tick with no accepted advance at all (`0x80037d54`, skipped by the guard at `0x800379a4`).
- The original also slides an entity along a wall when only one corner touches (`didAdjustForObstacle`). The engine does not.

## Decision

- **D-E19-8** — The fix goes into the engine. On the cell field, a blocked step advances to the farthest unblocked position, with no margin on the grid (engine ADR-0045).
- **D-E19-9** — The original's corner slide is not ported with it; it comes with slice E19.h.
- **D-E19-10** — The DLL keeps its shortfall rule for `ForceAdjusted`. With contact, the flag rises on the contact tick, one tick earlier than in the original. This is a recorded deviation, to be aligned with the binary in E19.h together with the slide.
- **D-E19-11** — In the engine's contact report, `H1Curtailed`/`H2Curtailed` mean "the requested step was shortened on this axis", whether it ends at zero or not.

## Consequences

- The cabin walk completes, because the walks there are bounded by walls and the contact is exact; the scene reaches map 476.
- The hero and every NPC with a controller now stop up to one step (1.25 to 2.44 px) closer to walls, which is closer to the original. Tests pinned on the old stop and the four hero reference traces are measured again.
- The margin in the cabin is 0.16 px: any skin or rounding on the grid contact would break it again, so a test pins the exact contact position.
- Until E19.h, a diagonal push into a corner still differs from the original, and consumers of `ForceAdjusted` (`0x1F`, `0x24`, the walk detour, deactivation on impact) see the flag one tick early at contact.
- The E19.a arcs ran bare entities without a controller and could not see this defect; the cabin is now also tested with the hero's real controller.
