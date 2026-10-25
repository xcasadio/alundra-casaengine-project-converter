# ADR-0092: C4 is the program of the transportable objects of the family C4, the key (C, 4) is registered for 19 types, and the wired set of the lift grows to 25 types

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.5 (E20.g2c1, D-G2C1-1 to D-G2C1-11, D-E20-75); reading of `ALUN_CD.EXE` (France) in `docs/plan-e20-annexe/e20-g2c/disc/` and `docs/plan-e20-annexe/e20-g2c1-disc/` (the program C4 `0x80066984` to `0x80066BF4`, the corner pointers `0x800370C4`, the pick phase `0x80038754`); measured results in `docs/plan-e20-annexe/e20-g2c1/` (`results.md`, `t2-results.md`, `t3-results.md`, `t4/`)

## Context

- After E20.g1b the lift worked for the seven types of C23 only (`ProductionWiredLiftableTypes`, ADR-0089). The barrel, the stone, the trunk and the four objects of the plates (the 7 header types of C4) were solid blocks that could not be lifted; the four ice blocks and the 26 shop articles that the map scripts switch to C4 at run time (`0x41 [C, 4]`) had no handler either, so a lifted one would have stayed stuck to the hands.
- C4 (157 words) was modelled in the T0 of 2026-10-22: the model equals the real code on 20 000 vectors (157/157 instructions; goldens `c4_goldens.json`, 1 244 vectors). Its carried half is the one of C23 with the same release table; it differs in the flags (it leaves them for the codes 1 to 3, and for the code -1 writes `(f | 0x10) & ~0x80` only when the native E code `+0x80` is 2), so a thrown C4 object stays collidable. Its free half bounces off a wall in the air, glides on a band, plays a sound on the landing or the touch, and floats the trunk on water.
- The corner words C4 tests are read by a second function, distinct from `UpdateGroundSlope`: 16 bits per corner, no qualification by height, mask `0x1001`.
- The eight trunks the maps 70 and 73 place on the water float from the first run of C4 in the binary, but the DLL fed the ground slope (`+0x18C`) only for the marked entities.

## Decision

- `LiftableAi.C4Tick` ports C4. The carried half is the shared `TickCarried` with a flags policy per handler (`ReleaseFlagsPolicy.C23` / `C4`); C23's behaviour does not change (D-G2C1-8).
- A new reader `CornerTileWords` (in `Alundra/Physics/`) gives the four corner words at the current position, unqualified, with the geometry of `UpdateGroundSlope` (D-G2C1-3). The native E code is read live from `SpriteProgramIndexes[ProgramEDeactivate]` (D-G2C1-4). The bounce effect 9 goes through `EffectReservoir.Create` and the sound `0x18` through `ISoundPlayer.PlaySfx` (D-G2C1-11).
- The key `(C, 4)` is registered for 19 types (`LiftableAi.C4ProductionTypes`): the 7 of the header, the ice block 304 and the 11 shop articles 48, 50, 66, 67, 68, 69, 71, 81, 83, 85 and 113 (D-G2C1-9, Q-SHOP default A). A type is never wired without a key.
- The wired set (`PlayerManager.ProductionWiredLiftableTypes`) is the 7 types of C23 plus `C4ProductionTypes`: 25 types (the 304 was already in) (D-G2C1-5). The torches (393) stay outside; the witnesses of ADR-0089 are derived on that type.
- `LiftableAi.FeedsGroundSlope(type)` (the trunk, 373, only) lets the hook of f6a feed `+0x18C` for the trunks the map places; the eight trunks on the water float from the first run of C4, which is the tick 2 of the load (the binary plays the program A of a loaded entity at the first tick, `0x80038754`) (D-G2C1-1).
- Not ported: C64 and C96 (the barrel 113 rec 11 and the stone 473 rec 0; in the binary the barrel never leaves the hands under C64, O-G2C1-1); the plate reaction (E19.sm1); the exit guard (`0xB2`) and the loop of Lurvy (`0x61`) (theme 3). The 43 scripted trunks never run C4 and stay obstacles (D-G2C1-2).

## Consequences

- The barrel, the stone, the trunk, the plate objects, the ice blocks and the shop articles lift, carry and throw; a thrown barrel, stone or trunk hurts an enemy and breaks (E2); the shop purchase path (lift, throw on the counter, the price dialogue) runs on the map 258.
- 7 existing tests moved (the 4 registry pins, the wired-set pin and the two witnesses of g1b), all in the list predicted by T0 and rewritten by derivation.
- A thrown C4 object stays collidable (a barrel plugs a corridor and can be lifted again), as in the original.
- Verification: 25 real mutants (applied by script, undone, `docs/plan-e20-annexe/e20-g2c1/t4/`), all killed, among them the 14 of the plan.
- Revert: phase C alone (the 18 types out of the wired set), then phase B (the key removed); phase A changes no behaviour.
