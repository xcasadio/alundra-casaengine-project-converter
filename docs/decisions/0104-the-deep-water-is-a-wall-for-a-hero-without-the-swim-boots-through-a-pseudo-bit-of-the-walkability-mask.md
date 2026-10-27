# ADR-0104: The deep water is a wall for a hero without the swim boots, through a pseudo-bit of the walkability mask

- **Status**: Accepted
- **Date**: 2026-10-27
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.14.2 (E20.i2a, revision 4; D-I2A-1 to D-I2A-6, D-E20-83, D-E20-87, Q-I2-2, Q-I2-6); `docs/plan-e20-annexe/e20-i2a/` (`README.md`, `t1-results.md`, `t2.md`); `docs/plan-e20-annexe/e20-i2-disc/` (the reading of the binary)

## Context

- O-E19-22: the original stops a hero whose boots level is under 2 at the edge of the deep water (corner test of the binary at `0x80037488`, `0x80037504`, `0x80037558-0x80037560`, reading `[0x80127000] < 2`); the port let him swim either way.
- The condition is an equality on `0xE00` (`(tileFlags & 0xE00) == 0x800`), which no bit of the 16-bit walkability mask expresses: the ladder (`0xC00`) has the bit `0x800` too.
- `ResyncControllerFromFlags` rebuilds the mask from the flags and resets the gravity and the fall speed of the controller to 0, so it cannot be the way the hero's mask is refreshed.

## Decision

- `CellsCollisionField.DeepWaterWallMaskBit = 0x10000`, above the 16 bits: a mask that carries it makes a cell not walkable when `(tileFlags & 0xE00) == 0x800`, whatever its height. It is never part of `WalkabilityMaskFor`.
- `PlayerManager.RefreshHeroWalkabilityMask` sets or clears the bit by `BootsLevelOf(state) < 2`; called by `PlayerManager.Tick` before the movement and by the adoption of the pawn (`WorldProxy`); it never calls `ResyncControllerFromFlags`, which keeps the bit it finds. The same function is where the cape of E20.h2 will add its own pseudo-bit.
- `ScriptedMotion.BlockedCorners` reads the live mask of the controller, so the slide along the edge judges the mover's corners.
- A hero already in the water without the boots is stuck, as in the original (Q-I2-2, default A). The preset `mur-m14` is `swim-m14` without the boots (Q-I2-6, default A).
- Deviation noted: the binary reads the boots level cached at the end of the previous tick, this port the inventory of the tick; they differ only on the tick where the boots change, which only the menu does, the world stopped.

## Consequences

- Without the boots the hero stops at the edge of the pond of map 14; with the boots of level 2 or 3 the swim of E20.i is unchanged. Entities other than the hero are never blocked by the bit.
- The 15 traces without boots are within the closed list of tolerances of D-I2A-6; the 15 mutants of `docs/plan-e20-annexe/e20-i2a/t2.md` are all killed.
- Not ported: the currents (O-E20-16), the splash, the wake and the ripple (E20.e).
