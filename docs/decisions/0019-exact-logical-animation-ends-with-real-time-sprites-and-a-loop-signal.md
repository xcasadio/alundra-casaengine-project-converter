# ADR-0019: Animation ends become exact on a logical clock while sprites stay in real time, and loops are counted

- **Status**: Accepted
- **Date**: 2026-10-01
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-17 to D-E19-20, taken with the author on 2026-10-01 after the verification of slice E19.c1), §1.2e (verification dispositions), §1.2f (E19.c2); refines decision D-E19-16 of ADR-0018

## Context

- ADR-0018 (D-E19-16) planned an exact animation clock in ticks in the engine. The first design advanced the sprites themselves on logic ticks, which would also change their frames on logic ticks and freeze them during a portal departure.
- Slice E19.c1 ported opcodes `0x1C` and `0x1D` from the binary. The binary counts every wrap of a Loop animation (`UpdateAnimation`, `0x80038D70`-`0x80038D7C`); the DLL receives no such signal. Opcodes that were skipped before E19.c1 now wait forever on a Loop: on map 172 (Inoa), Wendell takes the player's control (`0x10 @530`), plays his Loop talk animation and waits one wrap (`0x1C @539`), so `0x11 @547` is never reached and the player stays locked. The corpus has 208 such waits in 53 maps, 36 of them holding the player's control.
- In the engine's real-time path, the float32 time of 197 of the 5205 exported Loop animations lands exactly on the duration at 0.02 s per frame and then runs past it forever, so the sprite shows the hidden end key.
- The engine branch of slice E19.a2 has been merged into the engine's `main` by the author.

## Decision

- **D-E19-17** — Sprite rendering stays in real time. Only the logical animation ends (Hold end, Chain end, Loop wrap), as seen by scripts and by the Chain target switch, become exact to the original's tick. This refines D-E19-16.
- **D-E19-18** — The Loop-wrap signal comes with slice E19.c2 and fixes the lock introduced by E19.c1. E19.c1 is not merged without E19.c2.
- **D-E19-19** — The real-time Loop defect of the engine is fixed in slice E19.c2.
- **D-E19-20** — The engine branch of E19.c2 starts from the engine's `main`.

## Consequences

- An animation has two clocks: the rendered frame (real time) and the logical end (ticks). They can differ by about one tick; a Hold pose or the last frame of a Chain can show slightly early or late, and Loop phases are not locked to the counted wraps.
- The engine gains an opt-in logical end clock driven by the game layer (engine ADR-0046); other engine users keep the real-time behaviour, plus the loop fix.
- About 200 corpus programs frozen since E19.c1 run again once E19.c2 lands; the story path after map 163 (Wendell's talks on 165, 172 and 179) releases the player at the original's tick.
- Under catch-up frames (two or more logic ticks in one rendered frame), map-event programs and programs acting on another entity keep bounded timing residuals, in the same class as D-E19-13.
