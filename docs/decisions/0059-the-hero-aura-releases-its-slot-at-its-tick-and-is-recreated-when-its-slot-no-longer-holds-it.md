# ADR-0059: The hero's aura releases its slot at its tick and is recreated when its slot no longer holds it

- **Status**: Proposed (a refinement by the main session within the author's decision D-E20-28; reported to the author on 2026-10-10)
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.9 (E20.f1b, revisions 1 and 2, after two plan reviews); ADR-0057

## Context

- ADR-0057 (D-E20-28) lets the hero create and free its own effects at once and read `Destroying` at the tick, an exception to the
  queued model of ADR-0046. The DLL applies the queued operations of a frame after all of its ticks (`AlundraEffectStage.RunFrame`).
- An immediate free at tick k released the aura's slot before `RunFrame` applied queued creations of earlier ticks of the same frame:
  at 2 or 3 ticks per frame the burst of tick 91 took slot 0, which the binary never does (`TheReservoirAfterTheChargeAndTheRelease_
  DoesNotDependOnTheNumberOfTicksPerFrame` would fail).
- At 3 ticks per frame, the aura's renewal frame (ticks c+96, c+97, c+98) frees the destroyed aura inside `RunFrame` and then applies the
  charge spark of tick c+98, which takes the freed slot: the hero's handle would point at a live spark, and no aura would be recreated
  until that spark ends.

## Decision

- **Release at its tick.** The hero clears its aura handle and records the slot's generation at once, but the slot is released by a
  queued, generation-guarded operation (`FreeAtTick`) applied by `RunFrame` at the position of the release tick, as the binary's effect
  pass does after `MovePlayer`.
- **Recreation when the slot no longer holds the aura.** Besides the binary's condition (null handle, or `Destroying` set), the hero
  recreates the aura when the slot under its handle no longer carries the aura's generation or is inactive. At one tick per frame this
  never differs from the binary; for 3 or more ticks per frame it is a written deviation that keeps the aura bound to its own effect.

## Consequences

- ADR-0057's D-E20-28 stands; its "frees at once" is refined: the handle side is immediate, the slot side is in tick order.
- E20.f1b's acceptance gains a 3-ticks-per-frame renewal case (c+96 first in the frame, a spark at c+98) and a mutation that removes the
  generation test from the creation condition.
