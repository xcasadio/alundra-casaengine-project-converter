# ADR-0105: The tomes start and are cast through a start function, the states 0x32 to 0x34 and a slope guard, inert in production

- **Status**: Accepted
- **Date**: 2026-10-27
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.31.1 (E20.h4a, revision 2; D-H-3, D-H-4, D-H-5, Q-H4-1); `docs/plan-e20-annexe/e20-h4a/` (`README.md`, `results.md`, `t3.md`); `docs/plan-e20-annexe/e20-h4/` (the reading of the binary)

## Context

- The eight tomes (items `0x2B` to `0x32`) share one start in the binary (`0x800348E0`), one cast tick for the states `0x32` and `0x33` (`0x800325A4`), one tick for the end state `0x34` (`0x80032680`) and one guard of the slope at the head of the drivers (`0x800351B8`). Until E20.h4a the DLL had only the stubs of the table of the starts of E20.h0a for these items and no handler for the three states.
- The binary starts the spell at 0 magic for a non-staff weapon only through the decompilation (D1); the executable refuses it with the sound 3. The staff asks no magic and takes none.
- The tracks of music launched by the spells are not played yet (Q-H4-1: they will be, with E19.sm7).

## Decision

- `PlayerManager.TomeStart` is the start of the tomes in the binary's order: refusal (sound 3, answer 1) for an object in the hands, an animation other than Idle or Moving, an item in use, or 0 magic without the staff; otherwise the magic is paid by `DecreaseMp` (bounded to `[0, MpMax]` through `SetPlayerMp`), the track of the tome is journalled (`TomeTrackCallsForTests`, a stub), the state is `0x32`, the answer 0, the tome is not consumed.
- The states `0x32`/`0x33` (`TomeCastTick`) and `0x34` (`TryUseItem` only) sit in the main chain of `MovePlayer` before the D-H-5 test of the default; the hero does not move and the pad is not read. `0x33` is held while a tome is in progress, then Idle on the ground or `0x2D` in the air; `0x34` is held for its animation.
- `PlayerManager.TomeSlopeGuard` answers 1 and cancels the item in deep water or outside `0x32`/`0x33`, otherwise 0.
- The slice is inert in production: the tome entries of the table of the starts stay the stubs of h0a; the tests install `TomeStart` by `ItemStartForTests`. Each element's slice wires its own driver and cancellation.

## Consequences

- No behaviour changes in the game; the first element slice (h4b to h4f2) wires the entries and gets the start, the states and the guard ready.
- 69 tests (`TomeStartTests`, `TomeStatesTests`, `TomeOracleTests`) pin them, against the oracle for the vectors, the four cases of the staff, the eight swim traces and the beginning of the cast; 20 real mutants of the code are all killed (`docs/plan-e20-annexe/e20-h4a/t3.md`).
- Deferred: the playing of the tracks (E19.sm7), the drivers, handlers and cancellations of the elements.
