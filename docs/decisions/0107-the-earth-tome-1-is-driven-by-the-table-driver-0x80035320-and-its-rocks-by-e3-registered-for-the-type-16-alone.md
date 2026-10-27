# ADR-0107: The earth tome 1 is driven by the table driver 0x80035320 and its rocks by E3, registered for the sprite type 16 alone

- **Status**: Accepted
- **Date**: 2026-10-27
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.31.3 (E20.h4c1, revision 2; D-H4C1-1 to D-H4C1-3; defaults of Q-H4C1-1, Q-H4C1-2, Q-H4C1-5); `docs/plan-e20-annexe/e20-h4c1/` (`t0/results.md`, `t1/results.md`, `t2/results.md`, `t3/results.md`); `docs/plan-e20-annexe/e20-h4/` (the reading of the binary)

## Context

- The item `0x2B` (the earth tome 1) is driven in the executable by the function `0x80035320` and a table of 32 entries at `0x8002343C`; the rocks it throws (sprite type 16) run the native handler E3 (`0x8007EDA0`, slot E) once they have hit the ground. Until E20.h4c1 the DLL had the start, the states and the slope guard of the tomes (ADR-0105) and the stubs of the table of the item seams for `0x2B`.
- The decompilation was not the oracle: the executable (France) was read and its code replayed by an index-writing machine (689 driver vectors, 384 more for the base directions 4, 12, 20 and 28, 224 vectors of E3), then the keys of the original run were stored.
- The animation row and the current animation of a rock equal the key from the tick after its birth; at the birth tick the DLL leaves them at 0 and `0xFFFFFFFF` because it validates the animation change at the end of the frame (the one-frame lag already accepted by D-E19-13, ADR-0018) (D-H4C1-3).

## Decision

- A new file `Alundra/Ai/Items/EarthTome1Ai.cs` holds the 194 bytes of the table (32 entries of `u16` tick, `s16` Z speed, `s16` direction delta, and the sentinel `0xFFFF`), the driver, the cancellation and E3 (D-H4C1-1).
- The driver: phase 0 (slope guard, then for each entry whose tick is the counter a rock is born at the hero by `SpawnEntityBySpriteIndex` with the direction `(TargetDirection + delta) & 31`, and `ForceZ = speed << 8` only if a rock was given; the phase is 1 once the 32 entries have passed); phase 1 (guard, then at the counter 100 the animation `0x34` and the phase 2); phase 2 without guard (0 while the animation is `0x34`, else 1). The hero is not moved.
- E3 destroys the rock (`DestroyEntity(rock, -1)`) when the animation asked is 1 and its end flag is 1; otherwise it asks the animation 1 and sets the flags to `(flags | 0x40) & ~0x30`. It is registered by `EarthTome1Ai.Register` for the sprite type 16 alone (not 17, not 23: those are the business of h4d2 and E14.b0) and is part of `NativeSpriteRegistry.CreateForProduction` (D-H4C1-2).
- The cancellation `0x800351A0` is neutral: the rocks already born live and die alone, the item is freed by `CancelItemMode`. The three entries `0x2B` of the item seams (start, driver, cancellation) of `PlayerManager` are wired in the same slice.

## Consequences

- The earth tome 1 works as in the original in the game: 32 rocks, two every 5 ticks from the tick 22 to 97, the sound 46 at the impact, the object freed at the tick 129, a rock lost in deep water destroyed, the cancellation stopping only the births. Q-H4C1-1, -2 and -5 were taken at their defaults.
- The five sums of the keys of the production registry (`NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`) are derived from `EarthTome1Ai.Keys`, not retyped. E14.b0, which makes the registration cumulative, takes the entry `(E, 3)` of the type 16 as it is.
- 29 real mutants of the slice were applied by script and undone (`docs/plan-e20-annexe/e20-h4c1/t3/mutants.py`, results in `t3/results.md`): 28 killed, plus the declared equivalent (`DestroyEntity(e, 0)`) killed by one test alone (E1). Two survived at first (a Z speed written on the hero for a refused birth, a cancellation that writes the hero's Z force) and each got a test (D2 completed, D5 added).
- Deferred: the other elements of the tomes (h4c2 and following), E3 for the other rock types, the loot.
