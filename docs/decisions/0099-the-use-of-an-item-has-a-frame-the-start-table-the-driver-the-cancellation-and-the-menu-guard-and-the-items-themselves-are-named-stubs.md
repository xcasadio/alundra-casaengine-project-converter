# ADR-0099: The use of an item has a frame (start table, driver, cancellation, menu guard) and the items themselves are named stubs

- **Status**: Accepted
- **Date**: 2026-10-26
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.30.1 (E20.h0a; D-H-1 to D-H-6, D-H-7 void since D-C2B-11); `docs/plan-e20-annexe/e20-h0a/` (`results.md`, `results-t1.md`, `results-t2.md`, `t3.md`); reading of `ALUN_CD.EXE` (France): `TryUseItem` `0x8002ED64`, start `0x8003499C`, driver `0x8003634C`, cancellation `0x800350C0`, default handler `0x80032758`, locked branch `0x80031BFC`, menu guard `0x8002BC90`

## Context

- Before E20.h0a, `PlayerManager.TryUseItem` was a stub answering 1, the item driver and `CancelItemMode` were empty, and the inventory trigger treated the item-use lock as always zero.
- In the binary, a rising edge of the Circle under any bit of the control word plays the sound 3; otherwise the item of the Circle is promoted and refused (sound 3) when it is `>= 0x62`, when another item is in use, outside `0x1F`-`0x32`, or in a hole of the start table (`0x21`, `0x22`, `0x28`, `0x2A`). The same item already in use is not refused.
- The 15 `Skip` states of the swim table and every state from `0x50` reach a default handler that cancels the item in use; 21 other states without a ported handler do not.
- The items themselves belong to other slices (h1 healings, h2 cape, h3 bomb and seeds, h4 tomes).

## Decision

- The hero proxy carries `ItemInUse`, `ItemUseCounter` (saturated at `0x7FFFFFFF`) and `ItemUsePhase`, reset with each rebuilt hero (D-H-1).
- `TryUseItem` reads the game state through the hero's host (D-H-2); the start table has one entry per item `0x1F`-`0x32`; the entries of unported items are named stubs that answer 1 without sound and are journalled (D-H-3). Each later slice replaces its own entries.
- The driver runs once per free tick before the pad is read and at the tick of an injury; the counter is incremented after the driver of the item, so the driver sees 0 at its first call; the item is freed at the tick its driver answers non-zero.
- The cancellation runs the item's cancellation while it is still in use, then frees it. It is called by the locked branch (after `UpdatePlayerCarriedEntity(1)`, before the entry of the damage), by the swim `Skip` group and by a test of `SwimHandlerOf(state) == Skip` in the main chain, never by a final `else` (D-H-5).
- The inventory trigger is refused while an item is in use.
- Test seams: `ItemStartForTests`, `ItemDriverForTests`, `ItemCancelForTests` and the stub journal (D-H-4).

## Consequences

- The only visible change is the sound 3 of the refusals; an item whose start is not ported stays silent.
- Pinned by `HeroItemUseTests` (140), the inventory director test and the oracle traces `W3-O-*`, `W9-circle-*`, `R7-circle-windup` and the four holes `W9-item21/22/28/2A`.
- 16 mutants of the plan and 12 more were applied by real script and all killed except one equivalent (the driver called after the cancellation in the locked branch: the item is already freed, the driver returns at once); see `t3.md`.
- Deferred: the 16 object-start traces wait for their slices; the exit cell and the death sites of the cancellation (E18).
