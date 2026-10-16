# ADR-0072: The sword wave is born in production, E10 is handled in line and C61 calls a named stub

- **Status**: Accepted
- **Date**: 2026-10-16
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.19.3 (E20.f3c, "Résultats" of T1, T3 and T4) and the head of section 1.19 (D-F3-5, D-F3-6, D-F3-7); `docs/plan-e20-annexe/e20-f3/` (oracle outputs `wave_*`, README); `ALUN_CD.EXE` (France): E10 `0x8007F23C` (same body as E1 `0x8007ED10`), C61 `0x8007A958`, `CheckAndTriggerTileEffect` `0x8003A7B0`.

## Context

- E20.f3a (ADR-0067) put a seam on the hero's shot: a set of wired sprite types, empty in production, and an invariant that no type is wired before every C and E code of its header is handled. E20.e0a5 (ADR-0071) then fixed the clock and the frame box of an entity born by the sprite-index interface, the two defects that had stopped the first attempt at the wave.
- The charged sword (steel, demon, holy: sprite types 27 to 29 of the global page) fires a wave at tick `R + 21`. The oracle (the MIPS machine of E20.f3a) gives its life: it lives 26 ticks, dies at S+26 (status 4) and its slot is free at S+27, with the frame box sweeping from S.
- The header of these types carries a tick program C61 and a deactivation program E10. The binary's E10 has the same body as E1 (`DestroyEntity(e, -1)`); the break effect byte of the wave's header is 0. C61 calls `CheckAndTriggerTileEffect` and nothing else. A0 only writes `AIValues[0]`, which no code of the wave reads.
- The hit arena (`HitArena`) switches the native handlers of the registry off, but the arena pin (two contacts of the charged swing) must hold with the wave alive in it.

## Decision

- **D-F3-5.** E10 is handled like E0 and E1: in the inline branch of `RunSpriteEvent`, outside the switch of the native handlers (`IsInlineDeactivateCode` accepts 0, 1 and 10), with the same body `DestroyEntity(e, -1)`. The wave therefore dies at the right tick in the arena as well, and the arena pin stays at two contacts. Deviation of form: the binary has two addresses, the DLL one branch.
- **D-F3-6.** C61 is a native handler of the registry (key (C, 61), types 27 to 29) that calls the named stub `CheckAndTriggerTileEffect(entity)` and writes a per-thread journal (`AlundraWaveSwordAi.StubCallsForTests`). The grass is not cut before E20.e. Deviation, documented in the code: the binary draws at least one random word per cut cell, so the stream drifts after any wave over grass (O-F3-1).
- **D-F3-7 (A0 part kept, last sentence refuted).** A0 of the types 27 to 29 is not registered. The sentence "f4 to f6 will widen the Muruta A0 registration to a shared set of types", written in ADR-0067, is refuted by the discovery of E20.f4 to f6 (section 1.20): A0 is dead data for those types, so no registration is added and the Muruta's file is not changed. O-F3-3 is closed.
- **Wiring.** `ProductionWiredWeaponTypes` becomes `{27, 28, 29}`; this is the only line of behaviour of the slice and it removes on its own (empty the set).

## Consequences

- A charged sword now fires a wave in the real game: it appears 20 ticks after the release, lives 26 ticks, hits class A targets (never the hero), and does not cut the grass (stub). The invariant of ADR-0067 holds for the three types with the production registry (C61 registered, E10 in line).
- The production registry has seven keys; `AlundraNativeSpriteRegistryTests` pins `6 + 1` by derivation, and `AlundraWeaponSpawnSeamTests.S5` pins the production set to `{27, 28, 29}`.
- The tests of the life of the wave read the oracle by key (`wave_<sword>_<direction>.json`, tick by tick, at one, two and three ticks per frame); the break effect of E10 is observed through the active slots of the effect reservoir (the creation hook of the tests does not see it).
- Limits, deferred: the real grass cutting and the loot (E20.e, E20.e0c), the sub-pixel rounding of X and Y of the newborn (O-F3-2), the random stream drift over grass (O-F3-1), the other wired types (E20.f4 to E20.f6).
