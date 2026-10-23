# ADR-0087: The ground slope of the entities born by the interface is fed in the active statuses, for the marked entities with gravity only

- **Status**: Accepted
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.22.1, sub-section E20.f6a (D-F6-2, O-E0K-2, "Revision au HEAD" point "f6a"); reading of `ALUN_CD.EXE` (France): the tile-attribute pass `0x80038064` (four corners, minimum of the masked ground properties, `>> 9`), replayed on the MIPS machine; measured results in `docs/plan-e20-annexe/e20-f4f6/f6a/results.md`, `t1/t1-results.md` and `t2/t2-results.md`

## Context

- In the binary the pass `0x80038064` writes the tile attributes of every entity of the physics list: `+0x180` (OR of the four corners), `+0x184` (AND), `+0x18C` (the slope, `t1 >> 9`) and `+0x190` (the former `+0x18C`). The choice of the program destroys an entity that carries `DestroyOnSlidingSlope` when `Slope_18c` is 4 (deep water) in status 2.
- Before this slice the DLL computed `Slope_18c` for the hero only; every other entity kept 0. A fire or ice ball born by the interface, landing on deep water, therefore took the impact status instead of being destroyed.
- The marker `HasLogicalZ` (E20.e0k1/e0k2) is set for every entity born by the interface and means "the vertical motion belongs to the tick". No entity born by the records of a map has it; no sprite type 10 to 15 is born in production before f6b, so the change is inert in production.

## Decision

- `EntityScriptProxy.TickMotion` calls `UpdateGroundSlope()` for an entity that has the marker `HasLogicalZ`, in the `IsActive` branch (statuses 2 and 3, as the physics list of the binary), right after `ScriptedMotion.TickScriptedNpc`; not in `EvaluateEntitySupport`, which runs in every status.
- Only `+0x18C` is fed. `+0x180`, `+0x184` and `+0x190` stay 0 for these entities; their readers are listed in the T0 results with their owner (O-E0K-2).
- Entities born by the records of a map keep 0 (a negative witness test pins it).
- The comments made false (the "player only" restriction of `UpdateGroundSlope`, `TickPhysics`, `TickMotion`) are corrected.

## Consequences

- A marked ball landing on deep water has `Slope_18c` 4 and is destroyed by the existing choice of the program (effect 6); on a slope 1, 2, 3, on a half-wet footprint, with a dry corner or on a step it reads the value of the machine. The ball has no handler until f6b.
- The hero's behaviour is unchanged (the existing ground-slope tests of the hero stay green).
- Revert: remove the one call line; no data and no format change.
