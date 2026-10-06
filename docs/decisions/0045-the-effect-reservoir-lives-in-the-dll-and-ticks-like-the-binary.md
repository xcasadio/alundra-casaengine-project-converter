# ADR-0045: The effect reservoir lives in the DLL and ticks like the binary

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G3a, section 1.2o.6, D-E19-96, D-E19-97); annex `docs/plan-e19-g1g3-annexe/` (`dll-notes.md`, `binary-data-notes.md`, `fxpool.py`, `g3a-golden.py`)

## Context

The binary (ALUN_CD.EXE, France) keeps every map and object effect in a reservoir of 128 slots of 0x80 bytes (`0x80138608`): the smallest free slot wins, a full reservoir
creates nothing, and the effects are updated once per frame after the map events and the entities (`0x8002E058`), then drawn. `PlayerControlFlags & 0x48` freezes the update but
the slots are still drawn. A map entry clears the pool, then creates the effects of the records that carry the load flag (`0x40`) and whose inclusive zone holds the tile of the hero.
No effect is ever saved. E19.g G1 (ADR-0043) exports the data; nothing read it. The engine has no free quad yet (G2b) and must not learn Alundra's rules: the animation machine, the
slot lifetimes and the zone rules are the game's.

## Decision

- The reservoir (`AlundraEffectReservoir`, pure logic, no engine type), the bank loader (`AlundraEffectBankLoader`, the map companion found through `Maps/world-index.json` like the
  backdrops, the global one cached once per game) and the stage (`AlundraEffectStage`, owned by `AlundraWorldProxy` as a field initializer) live in the DLL. The engine does not know
  them.
- One world, one reservoir. The load-time spawn happens once, at the first `Update` of the proxy, before its tick loop and before any queued operation, for the tile the hero had when it
  was created (captured in `InitializeWithWorld` right after `AdoptPlayerPawn`): the binary writes the hero's tile before `0x8003C1A4`, and the scripts and the physics of the hero rewrite it
  from the tick 0. Without a hero the tile is (0, 0), with one warning.
- The tick runs in its own loop in `AlundraWorldProxy.Update`, after the map events and the pending triggers and before the camera: one pass per logic tick, 0 tick during a warp
  departure (like the backdrops, ADR-0034), the gate `PlayerControlFlags & 0x48` read per tick after the map events of that tick (the `continue` paths included).
- Because the scripts of the entities run before the proxy in the engine's order (the reverse of the binary), an operation on the reservoir is queued with the logic tick that emits it and
  a source rank, and applied before the update of that tick in the order (tick, rank, emission); the queue is empty at the end of every frame.
- A `Destroy` animation of duration S (the sum of its delays) is drawn on the ticks 0 to S and freed on the tick S + 1, as the binary does (130 of 130 animations checked on the real code).
- Where the binary would print and halt (an effect, an animation, an image set or an end the data does not hold) the DLL warns once per reservoir and destroys the effect: never reached on
  the real data.

## Consequences

- Nothing is visible yet: the draw is E19.g G3c (after the free quad of G2b-1 and the effect service of G2e), the nine opcodes are G3b, the native creators (modes 1 and 3, `0x82 0x53`) G3d.
  The reservoir already exposes the active slots in the order of the slots with their current image set, position and depth key.
- No game trace changes: nothing reads the reservoir. The golden table `g3a-golden.tsv` (generated from the oracles of the binary) pins the 363 animations, the 245 load slots of the 79 maps
  with effects at load and the three scenarios.
- The effects are not saved: a load starts a new world and a new reservoir, as in the original.
- The order of two slots with equal sort keys is the slot order, where the binary goes through an unstable quicksort: nothing observable on the real data.
