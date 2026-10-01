# ADR-0018: The NPC animation lag is kept, arcs load real prefabs, and the engine gets an exact animation clock

- **Status**: Accepted
- **Date**: 2026-10-01
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-13 to D-E19-16, taken with the author on 2026-10-01 before the plan of slice E19.c), §1.2e (E19.c1), §1.2f (E19.c2), open points O-E19-3 and O-E19-5

## Context

- In `ALUN_CD.EXE`, each tick runs the map events, then the entity scripts, then `UpdateAnimation` (`0x80038E18`, called at `0x8003B3D8`), then the physics (`0x80038364`, called at `0x8003B3E0`). Physics reads the speed of the animation resolved by `UpdateAnimation` in the same tick. The DLL moves an NPC with the speed of `CurrentAnimationId`, which `SyncAnimation` refreshes once per frame after the motion. A walk that starts from rest therefore takes one tick more than in the original, and a stop applied on the tick a walk ends moves one more step at the old speed: sailor 12 of map 389 descends 1.25 px, and the camera block of map 476 overshoots 0.75 px after each pan. Fixing it would move pinned points of the intro (flag `0x83EA` from frame 1034 to 1030, `0x83E9` from 1202 to 1198, the end of the intro from 1704 to 1698, sound 61 from 1087 to 1082).
- The chain arcs of the test suite spawn every record as a bare entity: the test game has no asset manager, so the prefab load throws and the spawn falls back to a bare entity, without controller or animation. A bare entity never moves in Z, so the rising camera block of map 478 cannot be tested that way.
- The binary refreshes `TileZ` after the vertical move of the tick (`UpdateTileAttributes`, `0x80038064`); the DLL refreshes it before. A rising or falling entity's `TileZ` lags its `PosZ` by one tick in the DLL.
- The engine advances animated sprites in float32 seconds of real time, before the scripts and outside the gameplay freeze. At 0.02 s per frame, 1010 of the 4413 exported Once animations end one tick early, and 3109 of the 5202 Loops wrap one tick late. The integer delays of the original survive in the export (every key time is within 1.02e-4 tick of the 50 Hz grid).

## Decision

- **D-E19-13** — The one-frame lag of `CurrentAnimationId` for NPC motion is kept. The intro pins do not move. Arc values include the lag.
- **D-E19-14** — The arcs can load the exported prefabs through an asset manager built by the test (entity, 2D animation and UI screen loaders, ids resolved from `AssetInfos.json`), with no change to the DLL. Every record then goes through the production spawn path, with its controller and its animated sprite.
- **D-E19-15** — The order of the `TileZ` refresh is corrected in slice E19.h, with the Z waits and contacts. Until then, arc values use the current order and are announced to shift by one frame.
- **D-E19-16** — The engine gets an exact animation clock in ticks in slice E19.c2, so that animation ends and frame changes fall on the original's tick. The design is settled with that slice's plan and recorded in an engine ADR.

## Consequences

- Scripted NPC walks keep one extra tick when they start from rest, and the overshoot after a stop stays. A Hold restart by opcode `0x1C` on a moving NPC gives one tick at zero speed. No site of the story chain is in that case.
- Prefab arcs exercise the real controllers, cells and animation-end events, and can reveal blockers the original does not have; such a blocker is submitted to the author before any fix. They cost more time and log volume than bare arcs.
- Values pinned on map 478 before E19.h move by one frame when `TileZ` is reordered.
- Until E19.c2, opcode `0x1C` counts animation ends on the engine's float clock, up to one tick off the original.
