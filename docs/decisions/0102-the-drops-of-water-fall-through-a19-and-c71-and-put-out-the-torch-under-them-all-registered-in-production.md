# ADR-0102: The drops of water fall through A19 and C71 and put out the torch under them, all registered in production

- **Status**: Accepted
- **Date**: 2026-10-26
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.5 (E20.g2c3, revision 2; D-G2C3-1 to D-G2C3-8, defaults of Q-G2C3-1 to Q-G2C3-7 taken by D-E20-83); `docs/plan-e20-annexe/e20-g2c3/` (`results.md`, `t1-results.md`, `t2-results.md`, `t3.md`); reading of `ALUN_CD.EXE` (France): A19 `0x800619D0`, C71 `0x8007B834`

## Context

- Before this change the drops of water (sprite type 391, 56 of them on the maps 87, 88, 90, 92, 94 and 158) hung motionless: the DLL held neither `(A, 19)` nor `(C, 71)`, so no torch was ever put out.
- The oracle (MIPS replay of C71, 22 golden vectors plus four vectors of the state 2) and the probes S-D1 to S-D8 (1, 2 and 3 ticks per frame) fix the ticks: period 88 for a native drop, 603 ticks for B[1] on the map 92. The DLL equals the binary at every sampled tick.
- The store `[0x8013DA20] = 1` of the state 4 is dead in the binary (D-G2C3-2). The accumulation of the four drops of B[1] at each return to the back of the map 92 is already the original's behaviour and is left to O-G2C3-2.

## Decision

- `WaterDropAi` (`Alundra/Ai/Objects/WaterDropAi.cs`) holds A19 (slot A) and C71 (slot C) for the type 391 only. A19 keeps the low byte of the program C in `Bytes[1]`. C71 is a pure step on the state `Bytes[0]`: 0 arms the wait (40), 1 counts down and starts the fall (animation 1, Gravity), 2 splashes at the landing (either ground flag), 3 takes `Collidable` off and advances to 4 before the test of the torch (D-G2C3-3), 4 restores at the end of the animation (state 0, animation 0, `ForceZ` 0, Gravity off, `Collidable` on, program C given back, 160 px up, root pushed); from the state 5 on it does nothing.
- The torch is the object the hero carries when the drop rides the hero, otherwise the support itself; only a type 393 is put out: status 3 and the mark `Bytes[0] = 1`, so that the E20 of the torch leaves no flame (D-G2C3-5). The drop writes no damage.
- `NativeSpriteRegistry.CreateForProduction` calls `WaterDropAi.Register`; a type is never wired without its key. Removing that call reverts the change (the drops hang again).
- The test save `gouttes-m92` (map 92, tile (38, 10), level 5, arrival from the map 91) is added (D-G2C3-6).

## Consequences

- Drops fall in cycles, splash, and put out a carried or posed torch without a flame; the four drops of B[1] on the map 92 put out the four torches of the top row after about 12 seconds at the back of the room.
- Pinned by the tests (a1) to (a5), (r1), (w1) to (w8) and (w10), the witness (w9), and by 30 real mutants, all killed (`docs/plan-e20-annexe/e20-g2c3/t3.md`). Six existing tests were rewritten by derivation (five registry sums and the preset list).
- A thrown torch that hits a drop is not reported as a contact by the flight in the DLL (noted limit, `t2-results.md`); the case is rare.
