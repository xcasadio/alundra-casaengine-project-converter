# ADR-0046: The effect opcodes queue their operation on the effect stage and apply it at the tick that emitted it

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` (E19.g G3b, section 1.2o.6, G3B-R1 to R3); annex `docs/plan-e19-g1g3-annexe/` (`dll-notes.md` section 4, `binary-data-verify.md`, `g3b-golden.py`)

## Context

Nine opcodes write the effect reservoir of ALUN_CD.EXE (France): `0x90` and `0xA2`/`0xA3` create an effect of a map record, `0x91` frees every slot of a record at once, `0x92` aims an
animation, `0x93`/`0xA0`/`0xA1` place the slots, `0x94` sets their forces. They act on every active slot whose record index is the operand (the one in its destruction tick included),
return no `Result` and never suspend. The binary runs the map events, then the entities, then the effects in one frame, so an effect created by a script is updated and drawn the same tick.
The DLL's frame order is the reverse for the entities (their own `Update` runs before the proxy's), and a rendered frame can carry several logic ticks.

## Decision

- The handlers live in `AlundraEventProgramRunner.Dispatch` and reach the reservoir only through `IEntityWorldContext.EffectStage` (a default interface member, null by default, implemented by
  `AlundraWorldProxy`). A host without a stage degrades: the trace says `Degraded`, the program advances by the size of the opcode.
- A handler does not write the reservoir. It queues the operation on the stage (`AlundraEffectStage.Emit`), marked with the logic tick that emitted it and the rank of its source (0 the map events,
  1 the entities, 2 the pending triggers: the order of the binary). The loops that run the scripts mark the tick (`BeginSource`/`EndSource`: the map-events loop and the pending-triggers loop of the
  proxy, the tick loop of each entity). At tick t of the effects loop the operations marked t are applied in the order (tick, rank, emission) before the update of that tick; an operation of a
  frozen tick (`& 0x48`) is applied too, only the update is skipped; what is left at the end of the frame is applied in the same order without update; the queue is empty when the frame ends. The
  scripts never read the reservoir, so the deferral is invisible to them, and at one tick per frame it is the order of emission.
- The operations are methods of the reservoir (`FreeRecord`, `SetAnimation`, `PlaceRecord`, `AddPosition`, `SetForces`, `MoveRecordToEntity`, `SpawnPlaced`, `SpawnAtEntity`), ported from the
  handlers: operands in the high word (`u16 << 16`, two's complement), `+ 1` on z for `0x93` and `0xA2` only, forces `s16 << 8`. `0xA2` and `0xA3` place only the slot they just created. The entity
  position of `0xA1`/`0xA3` is read when the opcode runs (the binary reads it at once), the first match of the search decides, and no match means nothing is created.
- Where the binary would read past its tables the DLL warns once per reservoir and ignores the operation: an animation outside the table of the effect (`0x92`); a record outside the table of
  records is a no-op, as in the binary.
- `AlundraEffectStage` becomes public: an interface member of a public interface carries it.

## Consequences

- The 99 effect rows of the closed list of the story chain leave it, `MapsWithoutSkippedSite` goes from 19 to 28 maps, and the arcs that pinned the skips (A2, A4, A6, A20, A10J, T-A10v, T-B9) assert
  the reservoir instead: the aura of the 476 never freed, the ten slots of the 391 at the two named instants, the eighteen slots of the 162. The values come from `g3b-golden.tsv`, generated from the
  real programs and the model of the binary.
- Nothing is visible yet (the draw is G3c) and no game trace changes: the effects have no game effect in the binary (no collision, no sound).
- The two G3a wiring tests of the 391 count the slots after the first frame of the real arc: the four `0xA2` of the first tick of B1 now add four slots (see the plan, G3b, the stop recorded there).
