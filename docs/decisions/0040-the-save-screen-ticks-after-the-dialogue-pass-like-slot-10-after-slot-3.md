# ADR-0040: The save screen ticks after the dialogue pass, like slot 10 after slot 3 in the binary

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, "E19.f3c" and D-E19-93 (the author, O-E19-69), with the evidence in `docs/plan-e19-save-order-annexe/` (`notes.md`, `verify.md`, `hostmodel.py`, `slotorder*.py`)

## Context

- The binary's `RenderScene` (`0x8002BD60`) runs the memory-card state machine (`0x8005EC98`), then the callback dispatcher (`0x80048054`), which walks the slots 0 to 12 in ascending order; `Update(0)` (pad, map events, entities) follows. Slot 3 is the choice box and slot 10 the file menu of the save screen: the menu opens the question, then polls the result word written by slot 3, in the same dispatcher loop.
- The world proxy ticked the save screen in its first per-tick loop and the dialogue pass (text box, then choice box) in the second: an answer given late reached the screen one tick later than in the binary (`DownDuringTheQuestion` 20 instead of 19); an answer given at the earliest reached it in the same tick.

## Decision

- `AlundraWorldProxy.Update` ticks the save screen (its state machine, then its transition's render) and its presenter in the second loop, right after the choice presenter and before the gameplay gate, so the `MenuOpen` it posts still freezes the map events of the same tick.
- In that loop `GameState.TickPad` only holds the edges of the last tick of the frame: the screen receives the pad words of its own tick, recorded per tick by the proxy, through `AlundraSaveScreenDirector.Tick(uint justPressed, uint justPressedByInterval)`. The argument-less `Tick()` keeps reading `TickPad` for the hosts that drive the director by hand.
- The existing test that moved: `DownDuringTheQuestion_ThenOui_WritesTheFrozenSlot_Once` 20 to 19. The test helpers follow the new order. New tests drive the real proxy in frames of 1, 2 and 3 ticks.

## Consequences

- The question's timings now match the binary's (earliest accepted answer at S+37 for OUI, S+38 for NON; late answers one tick earlier than before).
- The screen's state machine also runs after the dialogue pass. No real flow can tell: the book waits 61 ticks after closing its box before starting the screen, and scripted boxes cannot open under `MenuOpen`.
- `AlundraInventoryDirector` reads the screen's `IsActive` in the first loop: on ticks 1 and following of a multi-tick frame it no longer sees the screen state of the same frame. Negligible.
- The age of the pad word the screen reads stays a known gap (binary 19 samples between the press that opens the question and the earliest press the choice can take, port 18): O-E19-75, D-E19-99, a separate slice.
