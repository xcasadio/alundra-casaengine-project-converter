# ADR-0084: The charged arrow of William is born in production with C94 as a native handler, E1 in line and the cut of the grass called at every tick

- **Status**: Accepted
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.20 and 1.20.1 (E20.f4, "Revision au HEAD" points 1 to 10, D-F4-1 to D-F4-10 as revised, D-E20-58); `docs/plan-e20-annexe/e20-f4f6/` (oracle `f4/oracle/`, results `f4/results.md`, `f4/t1` to `f4/t4`); `ALUN_CD.EXE` (France): C94 `0x8007C0D8`, E1 `0x8007ED10`, `CheckAndTriggerTileEffect` `0x8003A7B0`, the trail creator `0x8003BDD8` called at `0x8007C128`

## Context

- The charged bow of William fires the sprite type 7 of the global page. Its header carries a tick program C94 (slot C, code 94), a deactivation program E1 and a break effect byte of 8. Until E20.f4 the type was not wired: `PlayerManager.ProductionWiredWeaponTypes` held `{27, 28, 29}` (ADR-0067, ADR-0072).
- The oracle (the MIPS machine running the binary) gives the life of the arrow: born at `S = R + 9`, 6 pixels per tick at constant Z, status 2 from S to S+42, status 4 at S+43, slot free at S+44, a trail of the global effect 25 every fourth tick, the break effect 8 attached at the death, no number drawn by the trail or the break effect, the shot passes through targets (a hit every 25 ticks).
- C94 calls `CheckAndTriggerTileEffect` at every call (ADR-0083: one entry `TileEffect.Trigger` for the four callers), then, when the counter `+0x1C` is a multiple of four, creates the trail. E1 has the same body as E0 and E10: `DestroyEntity(e, -1)`, which reads the break effect byte of the header (E14.a1b).
- The oracle harness does not model the step of the generator at the end of each frame (`0x8002BD0C-0x8002BD34`); the DLL advances one step per tick (ADR-0066).

## Decision

- **C94 is a native handler** of the registry, key (C, 94) for the type 7 only (`ChargedArrowAi`, registered in `NativeSpriteRegistry.CreateForProduction`). No key A0: the program A0 of the type only writes `AIValues[0]`, which nothing reads (D-F3-7, kept).
- **The cut is called first.** `ChargedArrowAi.CheckAndTriggerTileEffect` appends the entity to a per-thread journal (null in production) then calls `TileEffect.Trigger`, the same call as `WaveSwordAi`, at every tick, before the trail. The grass cut is therefore real for the arrow as soon as it is wired (it replaces the stub of D-F4-3 and D-F4-10 of the first plan).
- **The trail** (D-F4-2) is made only in the status Normal, when `FrameCounter & 3 == 0`: the global effect `0x19`, animation = the index of the direction of the entity (the table `{0, 0x10, 8, 0x18}` read backwards from `TargetDirection`; equal to `BoxDirection` at the four directions, probe P-F4-1), at (X, Y, Z + 1 + `0x80000`) of the arrow (the Z of the binary is the DLL's plus one, ADR-0026), with the forces `(-ForceX) >> 1` and `(-ForceY) >> 1` (arithmetic shift of the opposite, so -1 stays -1), no force in Z, no number drawn, no sound. The values are captured at the emission and the effect is created when the stage applies its queue.
- **E1 stays in line** (D-F4-4): no change of the runner. The break effect 8 comes from the header.
- **The arena switch** (D-F4-7): `EventProgramRunner.NativeSpriteTypeFilterForTests` (per thread, null in production) lets a test run the handlers of chosen sprite types while the others stay inert; the hit arena runs C94 and leaves the Beannoids still.
- **The wall tick** (D-F4-9, Q-K2 default A): a test of an arrow against a wall expects the tick of arrival plus one; it equals the oracle at 8 starts of 12 and precedes it by one tick at 4 (x0 = 98, 101, 104, 107).
- **Two phases** (D-F4-1): phase A registers everything with nothing visible in production; phase B adds the line `7` to `ProductionWiredWeaponTypes` (`{7, 27, 28, 29}`), the only line of behaviour, removable on its own.
- **The pins are derived, not copied** (D-F4-5, D-F4-8, plan point 5 and 6): the registry count is the previous count plus `ChargedArrowAi.Keys`; the production set of S5 is derived from the classes that register handlers; the negative clauses of S5 take an empty registry.
- **The generator in the keys** (plan point 3): where a drawn value decides a result (the loot of a cut), the tests read the keys `_pf` (the step of the end of the frame added after the record of `rng1` and before the read of `rng0` of the next tick), at one tick per frame only.

## Consequences

- A charged arrow of William now appears 9 ticks after the release in the real game, leaves a trail, goes through targets, cuts the grass it crosses (with the loot and the sounds of ADR-0083) and dies at a wall or the map edge with the break effect 8. The invariant of ADR-0067 holds for the type 7 with the production registry (C94 registered, E1 in line).
- The production registry has one key more; the production set has the type 7. The arrows (type 3 and neighbours, E20.f5) and the balls (E20.f6) are still unwired; this ADR is the common one for f4, f5 and f6, which extend it.
- The cut of the arrow runs on the world context only: an entity made by hand without it cuts nothing (the journal still records the call).
- Known limits: the wall flag of the DLL comes one tick after the arrival at the wall and the status 4 one tick after the flag (D-F4-9); at two and three ticks per frame the values drawn by an immediate draw do not see the intermediate steps of the generator (ADR-0066), so the tests check only what does not depend on them; the tir in the air of the charged arrow is out of scope.
