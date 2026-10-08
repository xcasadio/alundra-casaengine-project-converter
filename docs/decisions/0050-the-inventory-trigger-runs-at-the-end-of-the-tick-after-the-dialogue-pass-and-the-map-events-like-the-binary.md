# ADR-0050: The inventory trigger runs at the end of the tick, after the dialogue pass and the map events, like the binary

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, "E19.u" (section 1.2y) and D-E19-103 (open point O-E19-78), with the evidence in `docs/plan-e19-u-annexe/` (`notes.md`, `verify/verify.md`, `bin-out.txt`, `probe-out/`, the disassembly `dis-*.txt`)

## Context

- The binary's main loop runs `RenderScene` (`0x8002C3FC`: the callback slots 0, 3, 4, 6 and 10; the release of the text box, `0x8004501C`, clears bits `0x18` of the control flags `0x800DC4B8`) and then `Update(0)` (`0x8002C404`): the pad sample (`0x8002BB5C`), `UpdateWorld` (`0x8002BC4C`: the map events `0x8003C67C`, the entities, the effects, each reading the mask `0x48` at its head), the countdown of the warp delay, and last the inventory trigger (`0x8002BC6C`-`0x8002BD00`), which tests the whole word of the control flags (`0x8002BC70`).
- The world proxy ran the whole inventory block in the FIRST loop of its ticks, before the pass of the dialogue box, the save screen and the map events of the same tick. Measured with the real code of the binary and with the real proxy (`DialogueBoxMontage`): the map events froze one tick too early (ticks P..P+36 instead of the binary's P+1..P+37 for an inventory opened at P and closed at P+19, and the same shift through the sub-inventory), the gates of the effects and of the pending triggers were one tick early too, a Start sampled on the tick the dialogue box is released was lost, and a map event that opened a box on the tick of the trigger lost against the inventory.
- Convention (`AlundraInventoryDirector` class doc): the tick k of the port is `Update(k)` plus `RenderScene(k+1)` of the binary.

## Decision

- The pad pass (`TickPad.Update` and the recording of the Square and choice words) is the first thing of each tick of the loop of the passes in `AlundraWorldProxy.Update`; the former first loop of ticks is removed (U-R1).
- The inventory block (the portrait head point, `AlundraInventoryDirector.Tick`, `AlundraSubInventoryDirector.Tick`, the post-process, the portrait step, the two presenters) runs at the end of tick k, after the effect gate of that tick, at every tick, whether the hero exists and the gate is open or not (U-R2).
- The pending event triggers read the gate as it stood after the map events of the last tick, before that tick's inventory block (U-R3). The inventory's behaviour per tick does not change (U-R4; the age of the pad word stays D-E19-99).
- Two internal test seams on the proxy: `EffectFrozenOfTickForTests` and `PendingTriggersGateForTests`.

Gaps recorded (the slice's list):

1. In `RenderScene(k+1)` the binary does the slots 0, 3, 4, 6, 10 and then the end; the port does 4, 6 and the end at the end of tick k and 0, 3 and 10 at the head of tick k+1. The existing guards exclude any interaction: the trigger refuses while a control flag is set or the save screen is active, the head refuses while a box is open, the slots 6 and 10 are never active together, and the inventory never opens a box (the binary's inventory code only calls `OpenSlot(4)` and `OpenSlot(6)`).
2. **Behaviour change**: a map event that starts a warp on the tick Start is pressed now starts the warp, and the trigger refuses Start through its warp guard (D-E13D-30). In the binary the warp starts and the inventory stays armed (the original defect that D-E13D-30 corrects); before this slice the port opened the inventory first and froze the map events, so that warp did not start on that tick. The comment of the warp guard in `AlundraInventoryDirector` is revised.
3. The sounds of the inventory now come after those of the map events of the same tick, and the portrait head point follows a hero moved by a map event of the same tick, as in the binary.
4. Not confirmed in the binary: a Start on the tick the save screen closes now opens the inventory (it was lost before; the memory-card process of the binary, `0x8005EC98`, is not emulated).

## Consequences

- The map events, the effects and the pending triggers freeze on the binary's ticks around an inventory (measured at 1, 2 and 3 ticks per frame, and for closings at +19, +24, +30 and through the sub-inventory). Eight new tests (`AlundraInventoryTriggerFrameOrderTests`: seven written red first, plus the warp case of gap 2, added with the fix) pin the order; no existing test moved.
- The traces of the intro and of the hero do not guard this order (their harnesses do not run `AlundraWorldProxy.Update`); only the new tests do.
- A frame of several ticks runs the whole inventory block at each tick; before, it ran in a loop of its own ahead of the passes. The presenters of the inventory are now wired before their first tick (the wiring calls precede the loop of the passes).
- To recheck in game (recipe U-4): Start pressed exactly when a dialogue box closes (the inventory opens); a dialogue started by a map event while Start is pressed (the box wins); a map passage crossed while pressing Start (the warp starts, the inventory does not open).

## Amendment (E19.m, 2026-10-08)

The first consequence reads "measured at 1, 2 and 3 ticks per frame, and for closings at +19, +24, +30 and through the
sub-inventory". The E19.u verification measured the closings at +24 and +30 and the run through the sub-inventory at 1, 2 and 3
ticks per frame; the closing at +19 was measured at 1 tick per frame only. The decision does not change.
