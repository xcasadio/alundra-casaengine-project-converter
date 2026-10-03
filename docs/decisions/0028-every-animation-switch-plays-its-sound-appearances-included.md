# ADR-0028: Every animation switch plays its sound, appearances included

- **Status**: Accepted
- **Date**: 2026-10-03 (decisions D-E19-57 and D-E19-61 of the author, 2026-10-03)
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2r E19.t (rules T-R1 to T-R5), D-E19-57, D-E19-61, O-E19-41. Replaces the "take-off only" scope of ADR-0023 (D-E19-44).

## Context

- The binary asks for the sound of an animation at one place: the switch block of `UpdateAnimation` (`0x80038AB4`), at `0x80038BB0`-`0x80038BC8`. It reads the byte `0xC` of the animation set, adds `0x100` when the bit `0x20` of the byte `0xD` is set, and calls `PlaySoundEffect` (`0x800490FC`). `UpdateAnimation` has two callers: the per-tick pass (`0x80038E4C`) and the appearance (`0x80039EA8`); `InitializeEntity` writes `Current = ~Target` (`0x80039DAC`), so every appearance plays the sound of its first animation. A switch is a new target, a new direction row or the end of a chain (a self-chain included); a loop turn, a hold and the same animation asked again play nothing.
- The DLL played only the take-off sound of the hero (ADR-0023, R8): the sound of the animation that gives an impulse, looked up through `ZImpulseSfxOf`, which read the byte `0xC` alone.
- The export carries the byte `0xC` as `Sfx` and the byte `0xD` as `Acceleration`. The decompilation (`EntityManager.cs:241-245`) reads the byte `0xB` (`Flags`) for the bank bit, which is wrong against the binary on 91 of the 2405 animation sets of the export. 161 prefabs of 395 and 471 sets of 2405 carry a sound; 80 sets have the bank bit.
- At a map load the binary initializes the entities (`0x8002C3AC`) before it sets the new sound group (`0x8004A09C`), so the sounds of the appearances at the load are looked up in the group of the previous map. The DLL builds the world's sound player, with the new map's group, before the appearances.
- The value audit of 2026-10-03 found no real record that is loaded at the map load and sounds on its animation 0 (the bit `0x40` of its `SpriteDirection` is missing for the 20 such records of the 483 maps); on the story chain the group rule only touches the hero's arrival in the animation 55 (sound 229, bank 4).

## Decision

- **D-E19-57** - The sound of every change of animation is played for every active entity, appearance included. The identifier is `Sfx + (Acceleration & 0x20 ? 0x100 : 0)`, asked of the world's sound player at the tick of the change, and nothing when it is not positive. The byte `0xD` carries the bank bit, not the byte `0xB` of the decompilation.
- **D-E19-61** - The sounds of the appearances at a map load are resolved with the sound group of the new map (the original's defect, a lookup in the previous map's group, is corrected).
- The request lives in the one place the DLL already acts on a switch at its tick (`AlundraFrameSyncPasses.GiveImpulse`, reached by a pending switch, a validation owed by a frame without tick, and the end of a chain); the dedicated take-off request and `ZImpulseSfx` are removed, with no double request.
- An appearance asks its sound once, from the sites that create it: the map-load loop and `AdoptPlayerPawn` (the hero's arrival, after its animation sets are known) with the world's player, and `SpawnEntityByRecordId` (`0x2D`, `0x8A`, `0x8B`) at the tick of the opcode. The DLL's exemption of the first switch of an appearance (R2) is kept, so the first switch asks no second request.
- The gates of `AlundraSoundPlayer.PlaySfx` (BGM countdown, `id <= 0`, per-frame duplicate table, disabled row, voice ceiling) are unchanged.

## Consequences

- Sounds appear wherever a sounding prefab switches animation (161 prefabs): Melzas's growl at `0x8A @534` and `@553` of map 476 (219, resolved to 864 in group 62), the hero's sprint on `0x5B @64` of map 392 (13), the page of the book on maps 178 and 179 (204, once each), the hero's take-off (10) as before.
- A switch cancelled by a second tick of the same frame is not heard (accepted, as for R1).
- The per-frame duplicate table is per rendered frame, not per tick (existing gap).
- An animation absent from the entity's set asks nothing; the original would read past its table (open point: map 10 `@5651`, `0x1A [16]` on record 78).
- The request of an entity runs before the world proxy's own pass in the same frame, so a switch written by a map-event program is heard one frame after the opcode (D-E19-64).
- Rolling back is a revert of the commit (DLL only, no export).
