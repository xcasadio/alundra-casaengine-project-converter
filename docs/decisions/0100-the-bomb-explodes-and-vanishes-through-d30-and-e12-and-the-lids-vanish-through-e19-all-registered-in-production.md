# ADR-0100: The bomb explodes and vanishes through D30 and E12, and the lids vanish through E19, all registered in production

- **Status**: Accepted
- **Date**: 2026-10-26
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.30.2 (E20.h3a, revision 3; D-H-6, D-E20-83); `docs/plan-e20-annexe/e20-h3a/` (`README.md`, `results.md`, `t4.md`); `docs/plan-e20-annexe/e20-h3/` (oracle and critic); `docs/plan-e20-annexe/e20-a362-disc/` (keys `k_w1_away.txt`, `k_w3_chain_away.txt`, `k_w1_17east.txt`); reading of `ALUN_CD.EXE` (France): D30 `0x8007E79C`, E12 `0x8007F27C`, E19 `0x8007F690`

## Context

- A bomb at rest (sprite type 2) is held by nothing in the DLL before this change: no handler sets it off, and it never leaves its slot. The lids of the bomb scene of Olen (types 241 to 243) stay in the status 3 forever once their program disables them.
- The oracle (MIPS replay of the real code, 25 vectors per handler plus the world keys of h3 and of the A362 reading) fixes the values.

## Decision

- `BombAi` (`Alundra/Ai/Items/BombAi.cs`) holds D30 (slot D) and E12 (slot E) for the type 2 only. D30: when the attack of the touching entity has the attribute low nibble 4, 6 or 10, the status becomes Deactivated (3); no frame box or zero byte is tested. E12: destruction with `DestroyEntity(e, -1)` when the animation asked is 1 and its end flag is 1, or when the hole bit `+0x184 & 4` is set; otherwise the grass cut is called (`TileEffect.Trigger`, at every such call), the animation asked becomes 1 and the flags become `(flags | 0x40) & ~0x30`.
- `BombLidAi` (`Alundra/Ai/Props/BombLidAi.cs`) holds E19 (slot E) for the types 241 to 243 only: the sound `0xE6`, then `DestroyEntity(e, -1)`. The map-page types 314 and 315 are not registered.
- `NativeSpriteRegistry.CreateForProduction` calls `BombAi.Register` and `BombLidAi.Register` (phase B, reverted by removing the two calls). The start, the pilot, the cancel and C62 are E20.h3b's.

## Consequences

- A bomb in the status 3 explodes (animation 1, sound 46), hurts targets at the overlap of its boxes, cuts grass, sets off a neighbouring bomb, and disappears; the bomb of Olen disappears after its explosion without cutting grass; a disabled lid disappears with its break effect and its sound.
- Pinned by 29 unit tests, `BombWorldTests` (w1 to w4, w6) and the (w5) case of `OlenBombLidArcTests` at the ticks of the keys, and by 18 real mutants, all killed (`docs/plan-e20-annexe/e20-h3a/t4.md`).
- Five registry pins and two pins of the arc A362 were rewritten by derivation (`BombAi.Keys.Count`, `BombLidAi.Keys.Count`).
