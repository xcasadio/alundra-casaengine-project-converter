# ADR-0098: The fire and ice balls run C60 and E9 as native handlers, the keys are registered in production, and the wired set of the shot grows to the types 11, 12, 14 and 15

- **Status**: Accepted
- **Date**: 2026-10-25
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.22.2 (E20.f6b; D-F6-3 to D-F6-10, D-F5-5, D-F4-7, "Revision au HEAD" and the correction of 2026-10-25 of the R of (p1)); `docs/plan-e20-annexe/e20-f4f6/f6b/` (`results.md`, `oracle/`, `t1/` to `t5/`); reading of `ALUN_CD.EXE` (France): C60 `0x8007A8A0`, E9 `0x8007EF50`, master table `0x80098F4C`

## Context

- The fire and ice balls (sprite types 11 and 12 fire, 14 and 15 ice; 10 and 13 share the codes but have no weapon sheet) are driven in the binary by two native handlers: C60 in the slot C (the trail) and E9 in the slot E (the explosion, then the puffs). Their A0 only writes `AIValues[0]`, which no code of the balls reads.
- The three flames of the map 309 are entities of the type 12 placed by the records of the map; once C60 is registered for the type 12 they emit the effect 17, exactly as the real byte-code runner `0x8004205C` replays them (T0, `flames309_real`).
- The oracle (MIPS replay of the real code, 64 frozen keys identical byte for byte, manifest identical on two runs) fixes the values: the explosion draws ten numbers for five effects, a puff draws two, E9 has two branches (first call and later calls) and the later one does NOT return after the destruction on deep water.
- The charged rods are released at the tick 96 of the oracle's calendar (R = 96), the charged ball being born at 107 at one tick per frame and 108 at three; the arena releases at 91 with a charge counter of 58 that gives no charged shot (T0 stop, corrected in the plan on 2026-10-25).

## Decision

- `BallAi` (`Alundra/Ai/Projectiles/BallAi.cs`) holds C60 (`Trail`) and E9 (`Explode`) and registers the keys (C, 60) and (E, 9) for the types 10 to 15 only; no A0 key (D-F3-7). E9 follows the binary literally: slopes 1 and 4 (`Slope_18c`) destroy with the effect 6 and nothing else on the first call; on later calls the destruction is followed by the end test (`+0xAC == 1`, destruction with -1, return) or by the puff every 8 ticks, so a ball on deep water at a multiple of 8 dies and puffs in the same tick.
- The effects are queued on the stage with the values captured at the call; the numbers are drawn when the stage applies its queue (D-E0B-3(a)). The trail is the effect 17 for the types 11 and 12 and 18 for the others, at the binary's Z (`PosZ + 1`), with a Z offset of `0x80000` for the fire only, and the speed opposite to the ball's forces.
- Phase B: `NativeSpriteRegistry.CreateForProduction` calls `BallAi.Register`; the only change of behaviour is that the three flames of the map 309 emit their sparks. Phase C: `PlayerManager.ProductionWiredWeaponTypes` becomes `{3, 4, 7, 11, 12, 14, 15, 27, 28, 29}`; the only change of behaviour is that the rods of fire and ice shoot. Each phase is reverted alone (remove the call, empty the four types).
- The speed rule of the entities born by the interface (D-F5-5, the key animation is the one asked) and the ground slope of the marked entities with gravity (ADR-0087) are the existing mechanisms; f6b adds no new rule to them.

## Consequences

- The balls fly, land, explode with five sparks, puff, extinguish on deep water (slopes 1 and 4) and touch targets at the ticks of the keys at one, two and three ticks per frame; the explosion lands at x = 166 (fire) and 163 (ice).
- Pinned by tests (`BallAiTests`, `BallLifeTests`, `BallFlamesTests`, `BallProductionTests`) and by 24 real mutants of the code (`docs/plan-e20-annexe/e20-f4f6/f6b/t5/`), all killed.
- Deferred: the sense of the slope 1 (O-F6-1), the full reservoir of 128 effects tested only directly (O-F6-2), the torch of E20 that copies the type 12 (O-F6-3, E20.g2c2), and the vertical force of a flame placed by a record at the moment of C60 under the old fall rule (O-F6-4, E19.h2).
- Four registry pins and `S5` were rewritten by derivation (`BallAi.Keys.Count`, the classes' own sets), not by pasted values.
