# ADR-0021: Entity contacts, scripted jumps and the hero's water and ice rules follow the binary, and test saves feed the recipes

- **Status**: Accepted
- **Date**: 2026-10-02
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-27 to D-E19-33, taken with the author on 2026-10-02 before the plan of slice E19.d2), §1.2h (E19.d2). Refines D-E19-6 (ADR-0015) and D-E19-26; lifts the deferral of entity blocking recorded as D-E12D-1 (`docs/plan-e12d-interaction-joueur.md`); amends E3.d (`docs/plan-e3-collisions.md`) for the hero's airborne state.

## Context

- In `ALUN_CD.EXE`, `ComputeXYPosition` (`0x80037730`) calls `FindEntityCollisionCandidate` (`0x80036F34`) on every probe, before the cell test. A mover that is itself collidable (`Flags & 0x80`, `AnimFlags & 0x80` clear, not carried) stops exactly at contact with any entity of the per-tick collidable list (`0x800384F4`, same gates): half-open box in 16.16 units, plus a Z overlap. The contact raises `ForceAdjusted` and never slides. The DLL ports that function for detection only: entity bodies are ghosts that the controller ignores.
- Slice E19.d ported opcode `0x24` (wait on `ForceAdjusted`). Scripts that walk an actor to an entity (a villager, an invisible "Bloc transparent") now walk it to the next wall instead, and the `0x0B` walk that follows cannot start. This stalls the villagers' scene of map 10 (chapter 18), the hero's scene B[9] of map 10 (chapter 16) and the meeting of map 185 on day 4, which is the only way to the next dungeon.
- A scripted jump is an animation switch. `UpdateAnimation` (`0x80038AB4`) writes the animation set's `IsZForceApplied` (`+0xF8`) on the switch tick, and the force step turns it into a vertical impulse. Opcode `0x25` (`0x8003DB7C`) then waits for `CollidedWithEntityZ` (`+0x140`) or `IsOnGround` (`+0x144`). The DLL ports neither, so the hero's jump on map 10 (day 3) never clears its 16 px ledge.
- The binary hero's force step (`0x800369CC`-`0x80036A50`) halves the target force on water cells (`VramOR & 0x08`) without boots, divides the acceleration step by 16 on ice (`VramOR & 0x20`), and lowers the jump impulse on `0x18` cells. The boots level (`0x80127000`) is refreshed every tick from the items the hero owns. The DLL ports none of it.
- About 2,250 collidable records are liftable or breakable (crates, jars, barrels), on 241 maps. The DLL can neither lift nor break them (E14).
- The story after the dream of map 44 cannot be reached in game without combat (E14). The only load path is the Debug key F9, which loads the most recent readable slot. No tool writes a save.

## Decision

- **D-E19-27** — Entity contacts block walks universally, as in the binary: the hero and every NPC stop at contact with any collidable entity, in free play and under scripts. The engine gets an optional dynamic-obstacle predicate in the character controller's field stage, installed per world like the collision field, so that the existing bisection (engine ADR-0045) finds the exact contact. The DLL implements it with the binary's rule. This lifts D-E12D-1's deferral of blocking to E14.
- **D-E19-28** — Until E14 ports lifting and breaking, liftable entities (`Flags & 0x600`) and breakable ones (native E index 2) are not obstacles. The hero walks through them, as today.
- **D-E19-29** — Blocking ships with what it needs to stay correct: an entity marked for destruction leaves the obstacle set as in the binary; the native E handler of indexes 0 and 1 (`0x8007ED10`, destroy) is ported without its loot and break effect; `AnimFlags` is loaded from the animation set (byte `0xD`, already exported); the entity the player touches for dialogue and pick-up comes from the blocking report, as `+0x130` does in the binary.
- **D-E19-30** — When an entity blocks a `0x0B` walk, the E4.d detour does not engage: the walk waits, as in the binary, where `0x0B` never reads `ForceAdjusted`. D-E19-6 keeps the detour for cell contacts. `0x24` ends on an entity contact, as in the binary.
- **D-E19-31** — Scripted jumps are ported faithfully for every entity: the impulse on the switch tick (never on the first switch of a spawn or an arrival), the hero's airborne state owned by the logic tick (an amendment to E3.d, like the climb), `0x25` with both of its terms, and `CollidedWithEntityZ` aligned on the binary (moved forward from E19.h).
- **D-E19-32** — The hero's water and ice rules and the lower jump on `0x18` cells are ported now, with the boots level computed every tick from the items owned. Deep water blocking and floor damage, the other boots rules, stay open.
- **D-E19-33** — Recipes past the first dream use test saves: a console tool builds preset saves from the New Game state plus the preset's flags, map table entries and position, validates them with the game's own rules and writes them into the game's save folder, where F9 loads them. The author runs it outside the Claude app.

## Consequences

- The hero no longer walks through NPCs, doors or invisible blocks. Talking keeps working through the contact report. Intro pins that move because of a contact stop the work and go to the author.
- Crates, jars and barrels stay walkable until E14; scenes that rely on them as obstacles, if any, keep the DLL's current behaviour.
- Deactivated NPCs are destroyed as in the original; their loot and break effect wait for E14.
- Animations with `IsZForceApplied` now lift their entity, on 220 script sites and for 131 sprite records (the dogs of map 478 included). Arcs that pin vertical positions are re-measured in the slice that ports the jump.
- The hero is slower in shallow water and slides on ice, on 50 and 6 maps. On the story path, only maps 10 and 416 have such cells.
- A new console project builds against the game DLL; building it redeploys the DLL into the exported project, like any build of the DLL.
