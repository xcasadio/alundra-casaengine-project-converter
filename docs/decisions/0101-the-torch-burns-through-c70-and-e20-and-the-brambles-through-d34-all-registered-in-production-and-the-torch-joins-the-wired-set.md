# ADR-0101: The torch burns through C70 and E20 and the brambles through D34, all registered in production, and the torch joins the wired set of the lift

- **Status**: Accepted
- **Date**: 2026-10-26
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.5 (E20.g2c2, revision 2; D-G2C2-1 to D-G2C2-9, D-E20-64 and its erratum, D-E20-83); `docs/plan-e20-annexe/e20-g2c2/` (`results.md`, `t1-results.md`, `t2-results.md`, `t3.md`); `docs/plan-e20-annexe/e20-g2c2-disc/`; reading of `ALUN_CD.EXE` (France): C70 `0x8007B7B0`, E20 `0x8007F6C8`, D34 `0x8007E8AC`

## Context

- Before this change the torch (sprite type 393) was an object nothing lifted, and the brambles (types 238 and 239) were indestructible obstacles. The DLL held neither `(C, 70)`, `(E, 20)` nor `(D, 34)`.
- The oracle (MIPS replay of the real code, 90 vectors of the three handlers) fixes the values. E20 erases the six native codes of the flame it spawns (the premise of D-E20-64 that the flame keeps the handlers of the fire ball is false for the torch).
- The rain of torches of the lever of the map 92 lands one tick early on the record-born path (D-G2C2-9, a consequence of D-E20-37 until E19.h2); it is not a defect of this change.

## Decision

- `TorchAi` (`Alundra/Ai/Objects/TorchAi.cs`) holds C70 (slot C) and E20 (slot E) for the type 393 only. C70 does nothing while the torch is not carried, otherwise releases through `LiftableAi.TickCarried` with the new policy `ReleaseFlagsPolicy.C70` (flags `(flags | 0x34) & ~0x80`); the `switch` on the policy has one explicit case per policy and throws on an unknown one (no `_ =>` that would silently mean C4). E20 asks the animation 2 with flags `(flags | 0x40) & ~0x30` (raising the mark `+0x274` when the contact is a drop of water, type 391); once the animation is at its end it spawns the flame (type 12) unless marked, erases its six native codes, sets its animation 3 and flags `(flags & ~0x10) | 2`, then destroys the torch. A full entity table gives no flame and the torch is destroyed all the same (D-G2C2-2).
- `BrambleAi` (`Alundra/Ai/Objects/BrambleAi.cs`) holds D34 (slot D) for the types 238 and 239: when the attack of the touching entity has the attribute low nibble 6, animation 1 and flags `(flags | 0x46) & ~0x80`.
- `NativeSpriteRegistry.CreateForProduction` calls `TorchAi.Register` and `BrambleAi.Register` (phase B), and `PlayerManager.ProductionWiredLiftableTypes` gains the type 393 (phase C, 26 types); a type is never wired without its key. Each phase is reverted by removing those calls.
- The test save `torche-m92` (map 92, tile (34, 11), level 5) is added.

## Consequences

- Alundra lifts and throws a torch; it burns on landing or on its first contact, leaves a fire patch that lights the 16 switches of the map 92 and burns the brambles, which stop blocking and vanish. The lever of the map 92 rains four torches that ignite on the ground.
- The drops of water (A19, C71) are not ported: torches are not put out until E20.g2c3.
- Pinned by the tests (a1) to (a5), (r1) and (w1) to (w10), and by 26 real mutants, all killed (`docs/plan-e20-annexe/e20-g2c2/t3.md`). Nine existing tests were rewritten by derivation (five registry sums, the wired-set list, two non-wired witnesses moved to the bomb, the preset list).
