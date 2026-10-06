# ADR-0047: The save screen reads the pad word of the previous tick, like the dialogue pass

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, "E19.f3d", D-E19-99 and O-E19-75, with the evidence in `docs/plan-e19-pad-age-annexe/` (`notes.md`, `verify/verify.md`, `patch-proposal.diff.txt`, `probe-tests.cs.txt`)

## Context

- The binary's main loop (`0x8002C3F4`) runs `RenderScene` (`0x8002C3FC`) and then `Update(0)` (`0x8002C404`). The pad is sampled once per frame, by the first `Update(0)` (`0x8002BB5C`, then `0x8002E38C`, then `0x8002E250`).
- Every reader of the pad word on the save screen path runs inside `RenderScene`: slot 0 (the Square of the text box), slot 3 (the choice), slot 10 (the file menu: Cross, Down, Up) and the memory-card process `0x8005EC98` (state `0x3F5`, the Square at `0x800601D0`). They all read the sample taken by the previous frame's `Update(0)`.
- On the real code, a Cross pressed at sample P is read by slot 10 at P+1. The earliest answer the choice box accepts is at P+19 (P+14 to P+18 are never accepted) and is taken at P+38; the wait for the Square ends at P+20 (counter `0x800C4978` against `0x13`).
- The dialogue pass of the port already reads the tick before (`_choicePadOfTick[tick - 1]`, `_choicePadOfLastTick` at tick 0). The save screen read the pad words of its own tick (ADR-0040): 18 samples instead of 19, and a wait for the Square of 19 ticks instead of 20.

## Decision

- Rule F3D-R1: in `AlundraWorldProxy.Update`, the save screen reads `tick == 0 ? _choicePadOfLastTick : _choicePadOfTick[tick - 1]`, the same tuple as the dialogue pass. Nothing else changes in production: the dialogue pass, the square tuples (F2-R1) and the recording of the choice words (F3A-R3) keep their behavior.
- The director's own test helpers keep presenting their words at age 0, by design: the age is proved at the world proxy, through `AlundraSaveScreenTickOrderTests` (14 new tests, the grid test and the end-to-end book test reshaped).

## Consequences

- The screen reacts one tick later (20 ms at one tick per frame) to Cross, Up, Down and Square. The earliest accepted answer is at the 19th sample after the opening press, as in the binary.
- A second Cross within 18 samples of the opening press is dropped, as in the binary (the choice box is still sliding in); a Right pressed at the 18th sample is lost as well.
- ADR-0040 is superseded in part: the screen no longer receives the words of its own tick, it receives those of the tick before. The rest of ADR-0040 (the screen ticks after the dialogue pass) stands.
- The main inventory (slot 6) and the sub-inventory (slot 4, `0x80053328`) also read their pad words inside `RenderScene`, one sample old, while the port reads the current tick: the same class of gap, left open as O-E19-77 and not changed here.
