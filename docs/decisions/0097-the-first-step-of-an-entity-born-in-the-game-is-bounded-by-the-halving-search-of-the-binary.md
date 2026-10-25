# ADR-0097: The first step of an entity born in the game is bounded by the halving search of the binary

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: `docs/plan-e20-heros.md` section 1.18.5 (E20.e0k5; D-E20-80, D-E0K5-1 to D-E0K5-3, O-E20-24, O-E0K5-1); `docs/plan-e20-annexe/e20-e0k5/results.md` and `t3.md`; `docs/plan-e20-annexe/e20-step-search/` (model of `ComputeXYPosition` `0x80037730`)

## Context

An entity born in the game (an arrow, a charged arrow) is queued by `World.AddEntity` until the head of the next `World.Update`. Until then the mover of the engine adds the step without any wall test, so an arrow shot point-blank against a wall travelled up to 3 px into it (measured by the T0 probes: hero at 108 and 109, x = 111 and 112 at the end of the birth tick, where the binary stops at 109). The binary tries the whole step, then halves X and Y together and accumulates the accepted sub-steps; this is not "the largest free position" (start 107 and step 3 px end at 109 minus one raw unit, 7143423). The step search was replayed against the real code of the binary on 13 000 vectors (449 of 449 instructions) in the discovery.

## Decision

- `HalvingStepSearch.Search` (`Alundra/Physics/`) is a pure function of the step, a cardinal flag and a wall predicate, in 16.16 raw units; it returns the final step and whether a sub-step was accepted.
- `ScriptedMotion.RunOneKinematicTick` calls it, with `BlockedCorners` (the cell field of the host) as the predicate, only while the entity has a controller, a logical Z, a non-zero step and is not yet in a world (`SelfEntity?.World == null`), at every unintegrated tick of a frame. The bounded step replaces the final force; `ForceAdjusted` is raised when no sub-step is accepted.
- After the integration nothing changes: the mover of the engine decides (D-E19-58, D-E20-38). The resulting one-tick gap of the wall flag (x0 = 107, one and two ticks per frame: flag at S+1 where the binary raises it at S+2) is kept and pinned by a test.
- The east side only is covered (O-E0K5-1). The engine is unchanged; its gap ("`Move` before the integration tests nothing") is recorded in `docs/plan-e20-annexe/e20-e0k5/engine-gaps.md`.
- `ScriptedMotion.BlockedCorners` is `internal` so that the corner height test can be pinned directly.

## Consequences

- Arrows shot against a wall stop where the binary stops them and bounce or break on the same tick as the keys, at 1, 2 and 3 ticks per frame, except for the pinned flag gap.
- The wall test of the predicate (`BlockedCorners`) is shared with the rest of the step code; a change to it moves these tests.
- The other sides and the entities other than projectiles are deferred (O-E0K5-1); the search after the integration stays with the engine (D-E20-80).
