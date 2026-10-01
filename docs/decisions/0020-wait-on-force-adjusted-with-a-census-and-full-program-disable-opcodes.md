# ADR-0020: Opcode 0x24 is ported with a census of its waits, and opcodes 0x40/0x41 are ported in full

- **Status**: Accepted
- **Date**: 2026-10-01
- **Source**: this chantier: `docs/plan-e19-opcodes.md` §0.1 (decisions D-E19-21 to D-E19-23, taken with the author on 2026-10-01 before the plan of slice E19.d), §1.2g (E19.d)

## Context

- In `ALUN_CD.EXE`, opcode `0x24` (`0x8003DB70`) suspends its program until the logic entity's `ForceAdjusted` (`+0x13C`) is set, that is, until a walk meets an obstacle. It has no other exit. The DLL skips it today. On map 163 it waits for Jess to reach a wall before the wake-up cutscene gives control back. The corpus holds 395 reachable sites in 76 maps; 160 of them run while the program holds the player's control.
- Slice E19.c1 turned opcodes that used to be skipped into real waits and introduced a permanent lock that its own arcs could not see (opcode `0x1C` on a Loop animation, map 172). Porting `0x24` changes the corpus in the same way: wherever a DLL wall differs from the original, the wait never ends.
- Opcodes `0x40` (`0x8003E7B8`) and `0x41` (`0x8003E7E4`) write the logic entity's program index tables. `0x40` also asks `RunScript` to clear a program state: the running one at the end of the call when the logic entity is the owner, otherwise the logic entity's own state at once; the flag is reset after every check (`0x80042310`). Neither handler bounds its slot operand. The corpus has 426 sites of `0x40` (slot 2 to 5, value 0) and 85 of `0x41`; none uses a slot of 6 or more. The envelope had left the other-entity case to slice E19.j.
- On map 392, crates stand between the arrival room and the exit portal. The DLL lets the hero pass through them, because entity bodies are sensors that controller sweeps ignore.

## Decision

- **D-E19-21** — `0x24` is ported exactly as the binary, on the logic entity, with no detour and no timer, together with a static census of its 395 sites (acting entity, animation speed, direction, a ray through the cell grid for the entity's walkability mask). A site on the story path that may never end stops the work and goes to the author before it is committed.
- **D-E19-22** — `0x40` and `0x41` are ported in full in slice E19.d, both clearing cases included, with the flag reset after every check as in the binary. A slot operand of 6 or more writes nothing and logs one warning: the original's out-of-bounds write is corrected. Slice E19.j keeps only the out-of-zone rearm of map events.
- **D-E19-23** — The arc of map 392 places the hero in the portal corridor once the script gives control back, instead of walking him through the crates.

## Consequences

- Waits on `0x24` end at the original's tick when the DLL's cells stop the walker as the original's do; the census lists every site at risk (speed zero, no wall on the path, undetermined) for later slices (E19.h for entity contacts, riders and force clamps).
- About 200 entities in 40 maps whose script the original disables become inert, as in the original, until their native behaviour is ported (E14).
- The arc of map 392 does not test the walk through the crates; crossing them in game remains a known deviation until carrying and throwing are ported.

## Amendment (2026-10-01, after the first census stop)

The first run of the census stopped on the rule of D-E19-21 (26 of the 59 reachable story-path sites outside "wall found"). A read-only follow-up showed that the model, not the port, was wrong, and the author decided (`docs/plan-e19-opcodes.md` §0.1, O-E19-16):

- **D-E19-24** — The census model is corrected and rerun before the commit of `0x24`: each actor starts where it really appears (`0x8A`, or `0x2D` then `0x64`, including writes made by other programs), the hero starts from its real arrivals (`0x53`, portals, the tile of a `0x3B` box, the contact of an interaction), a `0x0B` that the cell model blocks is not clipped (the rest of the program is "not reached"), and so is what depends on a flag that no reached program sets. The not-reached sites are separate risks and no longer stop the commit; a story-path site that is still "no wall" or "indeterminate" does.
- **D-E19-25** — The screen-border clamp of `ApplyEntityForces` (O-E19-17) is ported in a separate, later slice.
- **D-E19-26** — Slice E19.d2 ("Inoa after the first book") follows E19.d, before E19.e: the villager scene of map 10 (O-E19-18) and the scripted jump of the hero with `0x25` (O-E19-19).

Result of the corrected census (`docs/census-0x24-waits.md`): 395 reachable sites in 76 maps; on the story path 59 reachable sites, 31 "wall found", none "no wall" or "indeterminate", 28 not reached.
