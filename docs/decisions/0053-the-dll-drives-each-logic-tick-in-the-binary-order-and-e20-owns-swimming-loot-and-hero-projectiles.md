# ADR-0053: The DLL drives each logic tick in the binary's order, and E20 owns swimming, loot and the hero's projectiles

- **Status**: Accepted
- **Date**: 2026-10-09
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 0.1 (D-E20-11 to D-E20-18, the author's answers of 2026-10-09 to the questions raised by the E20.0 discovery), section 0.2.10 and `docs/plan-e20-annexe/e20-0-rapports/` (D7, D8, D9 and their checks)

## Context

- In `ALUN_CD.EXE` one frame reads the pad (`0x8002BB5C`), then runs `UpdateWorld` (`0x8002E058`): the map events (`0x8003C67C`), then the entities (`0x8003B388`), then the animated-effect pool (`0x8003C410`). Inside the entities, `MovePlayer` runs first, then the other entities' programs, then the counters pass (`0x80038998`), the lists, the animations, the physics, the entity effects and the hit pass (`0x80039300`). The pseudo-random state (`0x80098708`) also advances once per frame after `UpdateWorld` (`0x8002BD14`).
- In the DLL the engine updates every entity, each running all its logic ticks of the frame, before the world proxy runs the map events; the hero's `MovePlayer` runs once per rendered frame on the per-frame pad. There is no world-wide pass once per tick. The author had accepted the resulting one-tick shift for the text box (D-E19-64, ADR-0029).
- E20 needs world-wide passes once per tick in the binary's order: the hit written at tick N is read by the events of tick N+1 and cleared by the counters pass of that tick.
- The discovery also left four owners open: swimming (slope 4), the mine cart, the loot with the pickable item and the entity spawn by global sprite index (`SpawnWarpEntity`, 102 callers), and the hero's projectile handlers.

## Decision

- **D-E20-11.** Each logic tick runs the map events before the entities, like the binary. E20.a fixes the order, after measuring which pinned tests move. This supersedes the one-tick shift accepted by D-E19-64 (ADR-0029).
- **D-E20-12.** The DLL itself drives one loop of logic ticks in the binary's order (pad, the box passes, map events, the entity block, the inventory block). The engine keeps its variable frame; no fixed 1/50 s step (the M3 migration stays deferred).
- **D-E20-13.** The counters pass does not clear `RidingEntity` while the DLL recomputes the carrier only once per frame. Documented deviation.
- **D-E20-14.** `UpdateItemEffectState` (defence table, boots level, regeneration, HP) and the item-use driver `0x8003634C` are named no-op stubs in E20.a and are ported in E20.c2.
- **D-E20-15.** The analog-stick snap of D-E20-8: only at the start and the resume of the run, when the keyboard and the D-pad give no direction and the stick gives a diagonal, the run takes the stick's dominant axis; within ε of 45° it keeps the axis of the current facing. ε = 1/16, tuned in E20.d from a measurement of the stick. Walking is unchanged.
- **D-E20-16.** Swimming becomes slice E20.i. The mine cart stays outside E20 (it has nothing in `MovePlayer`); it has no owner yet, the author will name one.
- **D-E20-17.** A dedicated slice E20.e0, before E20.e, ports the entity spawn by global sprite index, the loot (rolled at spawn, dropped at death, with its veto) and the pickable item. E14 reuses it.
- **D-E20-18.** The hero's projectiles are shared: E20.f ports the spawn and the hero-only handlers (wave sword C61, charged arrows C94); the bomb (E12) goes to E20.h; the handlers enemies may share (arrows E17, ice C60, fire C54) go to E14.

## Consequences

- E20.a owns the DLL's tick structure: a per-entity tick body driven by the world in production, with the entity's own loop kept for test rigs and fake hosts. The text-box order pins of ADR-0029 that rested on the old order change, each with its binary reason; the intro and hero reference traces are re-measured.
- Once E20.a lands, a map-event write (a lock, `0x5B`, `0x64`) is seen by `MovePlayer` and the entity programs in the same tick, and the E19.v2 first-frame mechanism changes; the first frames of the real game are probed again (O-E19-80 moved to E20.a).
- Until E20.c2 the hero's HP, defence table, boots level and regeneration keep their current behaviour.
- `RidingEntity` keeps a per-frame value; clearing it per tick waits for a per-tick riding computation.
- The stick snap is a documented port addition limited to the analog stick and to two lookups of the run.
- E20 grows two slices (E20.e0 and E20.i). E14 receives the projectile handlers enemies may share (E17, C60, C54) and reuses E20.e0; the mine cart has no owner until the author names one.
