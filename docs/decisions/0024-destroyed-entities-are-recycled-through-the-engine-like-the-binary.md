# ADR-0024: Destroyed entities are recycled through the engine like the binary

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2p E19.r, task R3, D-E19-45 (decision of the author, recipe of 2026-10-03)

## Context

- The binary recycles destroyed entities every tick: `UpdateDestroyedEntities` (`0x80038634`) is the first call of `UpdateEntities` (`0x8003B3A0`), under the gate `g_playerControlFlags & 0x48`, after `RunMapEvents`. It covers each slot in the state 4 (`FlagToDestroy`) with the template (`0x80134368`: state 0, `+0x48` = -1).
- The search by raw id (`0x8003C954`) does not test the state of the candidate, so a corpse stays found until the recycling.
- The DLL only flagged the entity (`FlagToDestroy`, invisible) and never removed it: `0x2C [2]` kept finding the Murgg of record 2 on map 15, and the scene in front of Tarn's manor (`B[1] @108`-`@111`) never gave the control back. The same wait blocks other maps without combat (for example map 6, `B[2] @239`).
- The engine already supports the removal: `World.RemoveEntity` calls `Entity.Destroy()` and the world update removes the entity and releases what its components hold (engine ADR-0037).

## Decision

- `AlundraWorldProxy.RecycleDestroyedEntities` runs right after each map-events pass of the tick loop, gated by `PlayerControlFlags & 0x48` read after the pass.
- Each spawned proxy in the state `FlagToDestroy` (never the hero) is put back to the template (`Status` `Destroyed`, `EntityRefId` -1), leaves the DLL lists, and its entity is handed to the engine with `World.RemoveEntity`.
- Nothing else is cleared: a reference kept on a recycled slot sees a slot in the state 0, as in the binary.
- This replaces the scope note "invisibility, not removal" of `DestroyEntity`.

## Consequences

- Waits on destroyed entities (`0x2C`, raw-id searches) resolve like the binary's; other maps without combat may be unblocked.
- A corpse left by an entity script is seen by exactly one map-events pass; one left by a map program disappears in the same image.
- Arc pins that observed a corpse were re-pinned (A0, A0b, A1, A11, A17, A10J) to the recycled state.
- Known gap, not treated: the binary reuses a freed slot at the next spawn, the DLL appends new entities at the end of the list.
