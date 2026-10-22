# ADR-0077: Pickup sparks and grass-cut effects go through the effect queue

- **Status**: Accepted
- **Date**: 2026-10-22
- **Source**: this chantier: the author's answers of 2026-10-22 to Q-E0C-2 and Q-E2 (`docs/plan-e20-heros.md`, section 2; decision D-E20-59 in section 0.1)

## Context

- The DLL queues every effect operation on the effect stage and applies it in the effects pass of the tick that emitted it (ADR-0046). ADR-0057 makes one exception: the hero creates and frees its own effects at once (D-E20-28).
- When a dropped item is picked up (E20.e0c, C255), `GiveItem` creates up to four sparks; each created effect takes two steps of the shared random generator, eight for four sparks (`docs/plan-e20-heros.md`, section 1.26).
- When the grass is cut (E20.e, `CheckAndTriggerTileEffect`), the original creates an effect, rolls the loot and plays a sound, for the hero as for the sword wave C61.
- The binary has no effect queue: it creates these effects at once, so its random draws come in an exact order. The plans of E20.e0c and E20.e had retained, as reversible defaults, to extend ADR-0057 to them (create at once) to keep that order.
- Asked whether the sparks are created at once or through the effect queue, the author answered that the port follows the CasaEngine workflow: the effect queue, for the pickup and for the grass cut alike.

## Decision

- The pickup sparks (E20.e0c) and the grass-cut effect (E20.e) are emitted on the effect queue (ADR-0046), like every effect that is not one of the hero's own.
- ADR-0057 is not extended: creating an effect at once stays limited to the hero's own effects (D-E20-28).
- Only effects are queued: the loot entity and the sound of a cut keep their own paths. Their order relative to the queued effect is settled in E20.e's revision.

## Consequences

- The random draws of these effects happen when the queue is applied, not at the pickup or the cut. An entity that draws after them in the same tick therefore gets other numbers than in the original. Nothing looks wrong by itself, but the port no longer matches the original draw for draw after a pickup or a cut.
- The revisions of E20.e0c and E20.e write their oracle keys in the queue's order and record this deviation in the code and the plan. The e0c mutant "sparks through the queue instead of at once" is turned around.
- A later wish to match the original's draws exactly would need a new record superseding this one.
