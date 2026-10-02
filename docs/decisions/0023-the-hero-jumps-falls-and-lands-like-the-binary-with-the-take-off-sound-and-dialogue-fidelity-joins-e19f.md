# ADR-0023: The hero jumps, falls and lands like the binary, with the take-off sound, and dialogue fidelity joins E19.f

- **Status**: Accepted
- **Date**: 2026-10-02
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-38 to D-E19-44, taken with the author on 2026-10-02 after the recipe of slice E19.d2b and the discovery of slice E19.d2c), §1.2h.3 (E19.d2c) and its value annex `docs/plan-e19-d2c-valeurs.md`. Extends D-E19-31 (ADR-0021); moves the E12.c slice of `docs/plan-e12-dialogues.md` into E19.f.

## Context

- ADR-0021 ports scripted jumps (D-E19-31): the impulse of the animation switch, the hero's airborne state owned by the logic tick, `0x25` and `CollidedWithEntityZ`. The discovery of E19.d2c ran the real code of `ALUN_CD.EXE` (France) in two independent MIPS interpreters, `MovePlayer` (`0x80031B50`) and the physics pass (`0x80038364`) included.
- In the binary, the Cross button jumps. `MovePlayer` shares one tail (`0x80031EA8`) between Idle/Moving and the jump states 2, `0x2B`, `0x2C`, `0x2D`: in the air, `0x2C` or `0x2D`; on the ground, a Cross edge starts a jump (2 when a direction is held, `0x2B` otherwise) unless the cell forbids it (`VramOR & 0x4000`). The DLL has no Cross jump. Seven maps off the story chain (61, 62, 63, 64, 66, 68, 329) release the hero in animation 44 after a scripted jump; without the jump states the hero keeps running at speed 196.
- In the binary, falling off a ledge uses the same code as a jump: the same air states, gravity and landing (`0x80031F44`). The DLL leaves falls to the engine's gravity.
- A hero who jumps lands on chests, floating platforms and switches (`0x80036BFC`), and a moving platform carries its rider (`0x80037364`, rider recomputed every tick by `0x80038998` and `0x800364C8`). 96 prefabs (2,012 records on 270 maps) can be stood on; opcode `0x3E` ("the player rides this entity") has 458 sites on 82 maps. Without landing on tops, a jumping hero passes over an object, sinks into it and stays stuck (D-E19-36).
- The engine snaps a controller entity to the ground within 4 px. An NPC whose last airborne height is 4 px or less is therefore on the ground one frame before the binary, and its `0x25` returns one frame early.
- Program `B[2]` of map 478 lifts the hero 8 px per tick (crest 60 px) while L2 or R1 are held alone from the arrival; today the DLL ignores the hero's `0x1B`.
- The binary plays the animation's sound (`animSet+0xC`) on every animation switch through `PlaySoundEffect` (`0x800490FC`); the jump animations 2 and `0x2B` carry sound 10. Sound 10 is in the exported system bank (`Sounds/sfx-manifest.json`, id 10), in the id space the DLL's sound player already uses.
- E19.f (faithful text box and name box) and E12.c (portraits, typewriter, pagination, cursor, blips and voices, `0x4C`/`0x4D`, the shared `map_alundra` table) draw the same dialogue screens.

## Decision

- **D-E19-38** — E12.c is done with E19.f, as one slice; the order stays E19.d2c, E19.e, then E19.f with E12.c.
- **D-E19-39** — The Cross jump and its states (standing jump, moving jump, landing) are part of E19.d2c, with the scripted jump: one slice for the whole hero jump. They also bring the hero back to Idle or Moving after the scripted jumps of maps 61 to 68 and 329.
- **D-E19-40** — The `0x25` of an NPC returns one frame before the original because of the engine's 4 px ground snap. This deviation is accepted until E19.h, which ports the NPCs' airborne state with the Z contacts; the pre-written values reflect it.
- **D-E19-41** — The hidden lift of map 478 stays as in the original: porting the hero's `0x1B` makes it work, with no exception.
- **D-E19-42** — The hero lands on the tops of objects (chests, platforms, switches) and is carried by a moving platform (the binary's rider rule, rider recomputed every tick), in E19.d2c. Ceilings stay in E19.h.
- **D-E19-43** — Falling off a ledge uses the same mechanism as the jump, as in the original: same airborne state, gravity and landing. The two "spawn" reference traces of the hero are regenerated to values written in advance.
- **D-E19-44** — Alundra's take-off sound (sound 10 of the original) is played in E19.d2c: it is in the DLL's sound bank.

## Consequences

- E19.d2c ships in two sub-slices that merge together: E19.d2c1 (scripted jumps of every entity, `0x25`, `CollidedWithEntityZ`, water, ice, the lower jump on `0x18` cells, the take-off sound) and E19.d2c2 (the Cross jump and its states, falls, landing on tops and the rider rule). Each sub-slice is planned, reviewed and verified on its own.
- The hero's vertical belongs to the logic tick whenever the hero is airborne (jump, `0x1B`, fall); the engine keeps it on the ground. The two "spawn" golden traces change by construction in E19.d2c2; every other trace stays byte for byte.
- The player can cross map 10's one-level cliff alone, as in the original, and leave single-level `0x3B` zones by jumping.
- Two scene waits of the day-1 visit of map 165 and of map 179 last about 19 frames longer each, as in the original; the NPC deviation of D-E19-40 stays visible to future readers of `CollidedWithEntityZ` (`0x26`, `0x47`, `0x20` to `0x23`) until E19.h.
- Dialogue fidelity work waits for E19.f.
