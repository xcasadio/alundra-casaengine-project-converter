# ADR-0057: The hero creates its own effects at once, attached effects sort with their anchor, and the stale aura handle is fixed

- **Status**: Accepted
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 0.1 (D-E20-28 to D-E20-30, the author's answers of 2026-10-10 to Q-N4, Q-N5 and O-E20-15) and section 1.9 (E20.f1b); discovery `docs/plan-e20-annexe/e20-f1/` (`effect-model.md`, `depth-key.md`, `check.md`)

## Context

- The charge aura (effect 3 attached to the hero) is created by `UpdatePlayerWeaponEffect` (`0x8002F49C`) when its handle is null or its slot's `+0x7D` (destroy flag) is set, and freed below a charge of 11; `MovePlayer` reads the slot at the tick (`0x8002F570`-`0x8002F58C`). The DLL queues every effect operation and applies the queue after all ticks of the frame (ADR-0046), which assumes that scripts never read the reservoir.
- The binary draws an attached effect just after its anchor (key `+0x1BC` + 1); the DLL sorts effects on `100 × ay` within the 16-px bucket (ADR-0049), which would put the aura, 16 px higher, under the hero sprite.
- When the aura expires during a lock and its slot is reused, the binary's free path (`0x8002F730`-`0x8002F744`) frees whatever effect now occupies the slot: a dangling pointer.

## Decision

- **D-E20-28** (amends ADR-0046 for the hero only). The hero creates and frees its own effects at once (`CreateNow`, `CreateAttachedNow`, `FreeNow`), keeps the slot as its handle and reads `Destroying` at the tick; a slot created at tick k is updated by `RunFrame` from tick k on. Every other effect operation stays queued. Exact at one tick per frame; the deviations for frames of several ticks are written in the code and the plan.
- **D-E20-29** (amends ADR-0049). An anchored slot sorts on the coordinate of its anchor's sprite plus its `DepthOffset`, so the aura draws just above the hero, as in the binary; unanchored slots keep ADR-0049's rule.
- **D-E20-30.** The stale aura handle is a defect of the original and is fixed: the free applies only if the slot still holds the aura (a generation number set by the reservoir's creation).

## Consequences

- E20.f1b ports the aura with an anchor on the hero (depth key `((PosY + (IDSV << 16)) & 0xFFFF0000) + (short)(PosZ >> 16)`, equal to the binary's sort on the oracle).
- `MovePlayer` may read the reservoir for the hero's own effects; no other script does.
- The aura can no longer free another effect after a lock.
