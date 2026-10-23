# ADR-0088: The passengers follow their carrier in the order forces then moves, with the XY force of a pile added once per carrier

- **Status**: Accepted
- **Date**: 2026-10-24
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2h.3.2b (D5b, D-D5B-1 to D-D5B-11, D-E19-117, D-E19-124, D-E19-126); discovery `docs/plan-e19-d5b-annexe/exec-2026-10-22/report.md`; reading of `ALUN_CD.EXE` (France): the counters pass `0x80038998` (clears `+0x12C` at `0x800389DC`), the physics `0x80038364`, the passenger sweep `0x800364C8`, the forces `0x80036828`, the copy `0x80037364`, the ceiling `0x80036D94`, the end pass `0x80038064`

## Context

- In the binary a passenger is an entity with `(flags & 0x4100) == 0x100` (gravity, not `NoRiders`) in the list of the collidables. The passes run in this order: the counters pass clears `RidingEntity`, the sweep sets it again for every passenger, the forces of all the entities are computed, then the copy `0x80037364` gives each passenger the vertical force of its carrier (a passenger without an impulse takes the carrier's force for both `ForceZ` and `FinalForceZ`, recursively on the carrier of the carrier) and adds the carrier's `AdjustedForce` XY to its own step, then every entity moves (a landing or a ceiling on a target that has not moved yet moves that target first), then the end pass reads the ground flag.
- Before this slice the DLL read the force of the carrier with one tick of delay, did not move the target first, computed `IsOnGround` before the carrier moved, kept the carrier of the previous tick in `RidingEntity`, and had no ceiling for the carriers. Measured on the real DLL: Alundra went through the lift of the map 22 and ended at the bottom of the hollow; a crate, a barrel or an NPC on a rising platform fell through it; only the hero took the XY of its carrier, one tick late.
- The binary's recursion re-adds the force of the carrier of the carrier to the carrier itself, so on a pile (hero on a crate on a platform moving by 1 px per tick in X) the crate receives 2 px per tick and the hero 0 (measured on the replayed binary, `vec_d5b_out.txt`). In Z the copy is an assignment: no defect.

## Decision

- The passengers follow their carrier in the order of the binary (D-D5B-1 to D-D5B-6):
  - a physics pass identifier (`IScriptHost.PhysicsPassId`, default member 0 = no tracking; `WorldProxy` increments it before its physics loop) and `EntityScriptProxy.TickPhysicsOnce`, which runs an entity's physics at most once per pass;
  - one function of the vertical force law (`PeekTickForceZ`) shared by the tick and by the pure calculation `CarrierTickForceZ` (recursive, bounded to 8 levels), and `CopyCarrierForceZ` for every passenger without an impulse on the three vertical sites of the entities that are not the hero (path A, the entities with a controller; path B, the entities marked `HasLogicalZ`; path C, the entities without a controller) and in the hero's air state;
  - "move the target first" (`EnsureMovedThisPass`) and the ceiling of the carriers together, in the same change, on every vertical site (a ceiling alone would stop a platform under a passenger that has not followed yet);
  - the entry rule: a hero carried by an entity whose force of the tick is not 0 enters the state held by the tick, whatever the engine says of the ground;
  - an end pass (`EndPhysicsTick`) that reads `IsOnGround` again after every move, for the hero and for the passengers that moved in the pass;
  - the counters pass clears `RidingEntity` (M4), which closes O-E20-9.
- The XY force of a pile is corrected (D-D5B-7, D-E19-117): every passenger, the hero included, adds ONCE the planned `AdjustedForce` of EACH carrier of its chain (planned, as the binary, even for a carrier a wall then stops; depth bounded to 8). `ScriptedMotion.PeekAdjustedForce` computes it without writing, from the function `PlanForce` shared with the tick, so they cannot diverge. It replaces the `LastTickDelta` read of the hero (phase 0 as the binary); `LastTickDelta` stays for the magnet gates of the apex.
- Documented difference from the binary: the double force of the pile is not reproduced.
- Out of this decision: the status 4 in the list of the collidables (Q-D5B-6), the non strict landing of the NPCs on the lifts (D5c), the model of the entities of the map (E19.h2).

## Consequences

- On the map 22 the hero rises with the platform with a zero gap (foot minus top) at every tick; a crate, a barrel, an NPC or a marked object on a moving platform follows it in the very tick, whatever the order of the cases; a pile follows; the two chests of the map 344 and the stone of the map 444, put on platforms that move, are expected to follow them instead of falling (census of the discovery; not played in the game yet, the recipe is the author's).
- A platform with a non-zero force and a passenger that does not follow would stand still (the ceiling): the three paths therefore go together, and a passenger with an impulse leaves the platform by its own force.
- One existing pin moves besides the re-pinned UH-14 (`-tick x 65536`, the value of the binary): `PillarAiPitWorldTests.W6_PushedEast_*`, 16 to 15 frames over the pit (D-D5B-11): the pillar still carries its planned push for one frame after the animation reset that the DLL makes one tick later than the binary (D-E19-13, D-G5-4), and the hero resting on it gains 0.75 px once; the final position is unchanged.
- A host that does not track the passes (`PhysicsPassId` 0) keeps the plain tick; a self-driven montage names a frame by a pass and guards the first tick of the frame only.
- Revert: each task of the slice is reverted alone in reverse order (T6 before T5, T4 not without T5 and T6); no data or format change.
