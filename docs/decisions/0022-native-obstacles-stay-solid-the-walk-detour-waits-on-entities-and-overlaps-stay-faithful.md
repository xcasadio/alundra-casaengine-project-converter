# ADR-0022: Native-destruction obstacles stay solid, the walk detour waits on entities, and overlaps stay faithful

- **Status**: Accepted
- **Date**: 2026-10-02
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-34 to D-E19-37, taken with the author on 2026-10-02 before the plan of slice E19.d2b), §1.2h.2 (E19.d2b). Refines D-E19-28 and D-E19-30 (ADR-0021); amends the E4.d detour of opcode `0x1E` (deviation E4-D5, `docs/plan-e4-deplacement-scripte.md`) for entity contacts only.

## Context

- ADR-0021 makes entity contacts block walks universally, and excludes liftable and breakable entities until E14. Every breakable record (native E index 2) is also liftable (`Flags & 0x600`): the exclusion is `Flags & 0x600` alone.
- About 600 other collidable records are removed by native code the DLL does not port (E14): 271 iron-ball walls on 28 maps, 157 flaming brambles, 73 ice columns, 76 rolling rocks and bomb-plant lids, 29 breakable pillars. On the story chain they stand on maps 14 and 15 (day 2, on the way to the scripted combat of map 15, which needs E14) and on map 362 (the next dungeon).
- In `ALUN_CD.EXE`, neither `0x0B` (`0x8003D468`) nor `0x1E` (`0x8003D8D8`) reads `ForceAdjusted`: a walk stopped by an entity waits. The DLL gives both the E4.d navigation detour, which engages on `ForceAdjusted` (`UpdateWalkDetour`).
- In the binary, a mover that already overlaps an obstacle at the start of a tick is blocked by every step that does not leave the overlap entirely. A static scan finds 28 overlapping pairs at map load (13 maps) and 87 portal arrivals that land in an entity box, flags not applied.
- On map 10, scene `B[9]` (chapter 16) stalls in an emulation of the binary when the player enters its trigger tile (37,46) from the south: the parked hero blocks Septimus's approach walk. Whether the south entry is reachable depends on ladders and stairs that were not modelled.

## Decision

- **D-E19-34** — The native-destruction obstacles stay solid until E14 ports their destruction, as in the original before the player has the item or power. Only liftable entities are excluded (D-E19-28).
- **D-E19-35** — D-E19-30 extends to `0x1E`: one rule in `UpdateWalkDetour` lets the detour engage only when no entity is in contact. The detour keeps its role on cell contacts.
- **D-E19-36** — A mover that already overlaps an entity stays blocked as in the binary. No exit rule is added; the arrivals the story chain uses are checked by arcs.
- **D-E19-37** — The south-entry stall of scene `B[9]` on map 10 is reproduced and recorded: a counter-test arc documents it, and the open point stays until chapter 16 can be played.

## Consequences

- Passages closed by iron-ball walls, brambles, ice and rocks stay closed until E14; the scripted platform of map 346 stops against its first wall, as in the original.
- A platform or NPC on a `0x1E` rail no longer leaves its rail around an entity.
- A hero or NPC placed inside an entity freezes, as in the original; the arcs of each played map catch such placements.
- Scene `B[9]` of map 10 can stall for a player who reaches its trigger from the south, until a later decision.
