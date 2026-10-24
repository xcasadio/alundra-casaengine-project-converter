# ADR-0086: The pushable pillar is ported from C76 and its stop of the sound is a named stub until g5c

- **Status**: Accepted; the stub of the stop and the claim that the push sound is short amended by ADR-0090 (E20.g5c)
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.6, sub-section E20.g5a (revision 0, D-G5-1 to D-G5-6, D-E20-63); reading of `ALUN_CD.EXE` (France): C76 `0x8007C024-0x8007C0D4` and `GetPushablePillarPushDirection` `0x8003AC9C`, replayed on the MIPS machine (`docs/plan-e20-annexe/e20-g5/disc/report.md`, `c76_full.py`, 6 000 vectors, 14 goldens); measured results in `docs/plan-e20-annexe/e20-g5/g5a/README.md`

## Context

- The pushable pillar (sprite type 430) is decided by one program C, code 76. When the hero is blocked by the pillar for 32 ticks it starts to move (animation 1 carries the speed, the motion itself is the generic one of the entities); it stops at a wall, falls from a ledge and plugs a pit. The 13 pillars of map 423 keep a scripted program C that runs first, so they stay inert.
- The binary stops the push sound with `StopSoundEffect` (`0x80049634`) when the push ends; the decompilation renders that call as a second `PlaySoundEffect`. `ISoundPlayer` cannot stop one sound yet (g5c).

## Decision

- `Alundra/Ai/Objects/PillarAi.cs` ports C76 and its direction helper as two pure functions (`Step`, `Direction`) and a thin `Tick`; the key `(C, 76)` is registered for the sprite type 430 alone, and the production registry gains `PillarAi.Keys` (D-G5-1).
- The counter `+0x274` is a signed 32-bit word held little-endian on `Bytes[0..3]` by helpers of its own; `EntityScriptProxy` does not change (D-G5-2).
- The push sound (25) is asked at every tick where the counter is at 31 and the hero pushes. `StopSoundEffect(0x19)` is a named, journalled stub (`StopSoundStubCallsForTests`), plugged by g5c (D-G5-3).
- The DLL runs one tick after the machine at the first step and at the landing in a pit; the final states are equal. The world tests pin the values measured on the DLL (D-G5-4).
- The program C scripted of the 13 pillars of map 423 runs before the native one, as in the original (D-G5-5). The hero has no push pose (D-G5-6).

## Consequences

- Pillars of maps 26 and 347 can be pushed as in the original; the pit puzzle of map 347 can be solved.
- The sound of the push is not stopped when the push ends until g5c; it is short.
- Other sprite types that carry code 76 in their record are not ported by this slice.
- Revert: remove the registration line (phase B stands alone); phase A has no production caller.
