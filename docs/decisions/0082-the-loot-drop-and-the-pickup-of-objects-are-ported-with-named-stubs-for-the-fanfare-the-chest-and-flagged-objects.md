# ADR-0082: The loot drop and the pickup of objects are ported, with named stubs for the fanfare, the chest and flagged objects

- **Status**: Accepted; amended by ADR-0085 (the music of the key objects)
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.26 (E20.e0c, revision 3.1), decisions D-E0C-1 to D-E0C-13, D-E20-47, D-E20-55, D-E20-59 and the author's answer of 2026-10-22 to Q-E0C-5; reading of `ALUN_CD.EXE` (France) in `docs/plan-e20-annexe/e20-e0c/`

## Context

- In the binary, an enemy or a container drops the object drawn at its creation (`SpawnContents`, `0x80032B90`, first act of `DestroyEntity`); the object runs the native handlers A255 (`0x80061BD4`) and C255 (`0x8007C174`) and is taken on contact with the hero (`GiveItem`, `0x80033F00`).
- Objects placed on maps run the same handlers when the record has no code of its own at slot C: 59 of the 152 placed objects. The other 93 keep their map script.
- Before this slice the DLL had a named stub for the loot in `DestroyEntity`, no pickup handler, and nothing that wrote `BlockedByEntity`.
- The fanfare of the key objects needs CD track 11, which the port does not play yet (ADR-0076, E19.sm7). The chest (`+0x274 == 2`) belongs to E14. The placed object that holds a flag (`+0x40 != 0`) does not occur in the data (guard test).
- The placed object's stray `SetGameFlag(-1)` writes outside the flag bank (a proven defect of the original).

## Decision

- The drop (`LootDrop`), `GiveItem` (`ItemEffects`), the heal bodies (`HeroHeals`, reused later by E20.h1) and the handlers A255/C255 (`PickupAi`) are ported natively, dropped and placed objects alike (D-E0C-1). A map script at slot C still wins.
- The 32-bit words `+0x274`, `+0x280`, `+0x284` have accessors local to `PickupAi`; `EntityScriptProxy` is not changed (D-E0C-13, D-E20-55).
- `Block(self)` / `Unblock(self)` are implemented by `WorldProxy`; the animation clock and the physics skip a blocked entity, the hero included (D-E0C-6). `ModdedPos` is refreshed each tick before the physics.
- The pickup message is composed ("name" + " gained!") and opened by an additive member of the dialogue director, without touching the engine (D-E0C-4, D-E0C-5).
- Pickup sparks go through the effect queue (D-E0C-8, ADR-0077).
- Named stubs: the fanfare (a journaled stub, the music is not touched, D-E0C-7), the chest branch (D-E0C-2), the flagged placed object (D-E0C-3), the E11 recursion until E14.b (D-E0C-11).
- Not ported, as a documented deviation: the placed object's `SetGameFlag(-1)` (D-E0C-12, Q-E0C-5). The register `[0x80149CBC]` is ported as the binary writes it (D-E0C-9).

## Consequences

- Enemies and containers drop objects, and the 59 native placed objects are taken in game; the hero can no longer stand on a placed object.
- E19.sm7 plugs the fanfare into three private functions of `PickupAi`; E14 takes the chest; E20.e reuses `SpawnRandomLoot`; E20.h1 reuses `HeroHeals`.
- The 93 placed objects with a map script are not touched.
- A message degrades to empty (no box) when the ETC strings are not loaded.
