# ADR-0083: The cut of the grass keeps the binary's draws and order and queues only the effect

- **Status**: Accepted
- **Date**: 2026-10-23
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.27 (E20.e, revision 3), decisions D-E20-49, D-E20-59, D-E20-60, S-E1 to S-E7, the author's answers of 2026-10-22 to Q-E1 to Q-E5; reading of `ALUN_CD.EXE` (France) in `docs/plan-e20-annexe/e20-e/` and the measures of `docs/plan-e20-annexe/e20-e/rev3/` (`results.md`, `oracle/out/zedge.txt`, `oracle/out/latency.txt`)

## Context

- `CheckAndTriggerTileEffect` (`0x8003A7B0`) is called by the hero (15 attack states), the sword wave C61, the charged arrow C94 and the bomb E12. For each of the four corners of the stored frame box whose cell has the bit `0x02` inside the band of Z, it cuts the cell, creates the global effect given by the byte 9 of the map header, rolls the loot (`SpawnRandomLoot(0xFF, ...)`, 1 or 2 words of the generator, sound 84 if an object appears) and plays the sound 31.
- ADR-0077 sends the grass-cut effect through the effect queue and leaves the order of the loot and the sound to E20.e. The binary has no queue and its effect creation draws no number (measured on the 4 000 vectors of the oracle: a corner costs 1 or 2 words, all of them the loot's).
- The binary's table of columns covers 0 to 1247 pixels. Outside it the binary reads memory that is not defined. The decompilation differs from the binary on the south row (divided twice), the truncating division above the map, and a `+0x80000` on the Z of the effect and of the loot; the binary decides.

## Decision

- The effect goes through the queue (ADR-0077, refined here) and nothing else does: the cut of the cell, the loot and the sound 31 are done at once, in the binary's order, for each cut cell: cell, effect (queued), loot (and its sound 84), sound 31. The queued effect draws no number, so the generator sees the same words as in the original and the sound 84 precedes the sound 31. The effect takes its slot in the effects pass of the same tick, not in the middle of the entities pass.
- The four corners are visited one at a time on the cells as the previous corners left them (a cell cut by an earlier corner is not cut twice); the effects, loots and sounds are then done for each site in order. This two-step form equals the binary's interleaving on the 4 000 vectors, because neither the effect, the loot nor the sound changes a cell.
- The frame of Z (Q-E6, measured on the machine): the DLL's `HitBoxZ` is the binary's minus one (ADR-0026), and a cell of height `h` (`zc = (h << 20) + 0x80000`) is cut iff `HitBoxZ + 1 <= zc <= HitBoxZ + 1 + height`. The edge `HitBoxZ == zc` skips, as in the binary.
- Outside the binary's table (Q-E7): a pixel before 0 is the column 0; a pixel past 1247 keeps the formula `p / 24`. The cell read is clamped to 0..51 by 0..59 and the position of the effect and of the loot keeps the unclamped column and row (the row above the map is -1, floor division, so the effect is at Y = -0x80000). The Z of the effect is the height byte shifted by 20, without the `+0x80000`.
- A cell without a stack of tiles (100 of the 9 089 cuttable cells; Q-E4) is cut as any other: the bit goes, the effect, the loot and the sound happen, and nothing is rebuilt in the visual.
- The grass is not persisted (Q-E5): a map entry reads its cells again and the bushes grow back.
- The effect id is the map property `TileCutEffectId` (ADR-0079), read once at the map load, 2 when it is missing or not a number.
- A host without the world context, the cell mutator or the collision field cuts nothing and plays nothing; a host with a mutator and no effect stage cuts, rolls the loot and plays the sound without the effect.
- The one entry for the four callers is `TileEffect.Trigger(EntityScriptProxy)`; the arrow (E20.f4) and the bomb (E20.h3) add only the call.

## Consequences

- The cut matches the binary draw for draw (unlike the pickup sparks of E20.e0c, whose two words per spark move to the queue's moment, ADR-0077); the only difference is the slot index of the effect, which can differ from the original when the hero creates an effect at once in the same tick.
- An object born from a cut by the wave, in the programs pass, may run one tick later than in the binary when its cell is higher (Q-E0C-8 of the plan, E20.e0c), kept and documented.
- The test project holds the oracle (`Alundra.Tests/Support/CateVectors.cs`, `docs/plan-e20-annexe/e20-e/rev3/vectors/`); a change to the cut must keep the 4 000 vectors and the oracle `cut_m14` equal.
- The pace, the dust and the landing sound stay for E20.e2; the opcode `0x9E` and the persistence are out of scope.
