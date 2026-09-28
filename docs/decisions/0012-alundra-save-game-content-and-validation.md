# ADR-0012: The Alundra save game holds the original's saved state and is validated field by field before it is applied

- **Status**: Accepted
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e16-etat-partie.md`, slice E16.c (D-E16-22, D-E16-23, D-E16-31, D-E16-32, choices C1 to C8 of the detailed plan, approved by the author on 2026-09-28), §2 (E16.0 measurements, Q3, Q4 and Q6) and the E16.c security review (SC1 to SC12)

## Context

- The original saves one 0x758-byte structure, `g_saveData` (`ALUN_CD.EXE` `0x801EB2E8`, §2 Q3 of the plan): `SlotData`, `LastMapId`, `CurrentFlagName`, `GameStateDescription`, `GameTime`, `InitialMapId`, `CameraTileX/Y/Z`, `GameFlags` (64 words), `MapIdToInternalMapIndexTable` (500), the nine player stats, `NumberOfItems` (256), `SaveSlotIndex` and `Field_757`. `LastMapId` and `Field_757` have no reader; the decompilation's `Offset` field does not exist in the binary; `g_temporaryFlags`, the text category index and the four `\V` variables live outside the structure.
- `SaveSlotIndex` counts the retries after death (one byte, capped at `0xFF`, read by opcode `0xC2`). The game time counter adds one unit per displayed frame and the binary displays it in sixtieths of a second (`UpdateMenuStatusText`, `0x80030FC8`).
- The engine's save-game service (engine ADR-0044) only guarantees each value's shape and C# type range, and fills a new instance of the type the game names. A save file is untrusted input in both formats: JSON is edited by hand, and the binary format's CRC-32 can be recomputed.
- The DLL keeps both flag banks on 1024 words and seeds the map table to the identity on 500 entries, while `world-index.json` holds only the keys 0 to 482. It had no game time (its logic clock runs at 50 Hz) and no retry counter.
- The engine's service constructors are internal and `SaveGameArchive` cannot be subclassed outside the engine, so the DLL's tests can neither create a service on a temporary folder nor serialize without one.

## Decision

- **Content** (data version 1, in this order, since the binary format is positional): `gameTime`, `initialMapId`, `cameraTileX`, `cameraTileY`, `cameraTileZ`, `gameFlags` (the 64 words of the original), `mapIdToInternalMapIndexTable` (500), a `playerStats` object (`hp`, `hpMax`, `mp`, `mpMax`, `money`, `weaponId`, `itemId`, `falconTemp`, `falcon`), `numberOfItems` (256), `deathRetryCount`.
- **Not saved**: `TemporaryFlags`, `TextCategoryIndex` and `GameVariables` (outside `g_saveData`), `SlotData`, `LastMapId`, `Field_757` and `Offset`. The chapter and the `HP xx TIME hh:mm:ss` summary go into the slot's metadata (`chapter` on four digits, `summary` as the binary builds it), not into the data.
- **D-E16-22** — `SaveSlotIndex` is saved and restored as `DeathRetryCount`, 0 to 255; nothing increments it before E18.
- **D-E16-23** — The game time counts 60 units per real second, capped at `0x14996C4` (99:59:59): a deliberate deviation from the original's one unit per displayed frame.
- **Validation**: after loading, every field is checked against its domain (the table of the plan's E16.c, corrected by its C3), and a single value out of its domain refuses the whole load with a message naming the field. The data version must be 1. A map-table entry is a key of `world-index.json` or its own index. The start tile is checked against the size of the start map.
- **D-E16-32** — That size is read from the map's `tilemap/<name>.tileMap` (`map_size`), reached through the trusted `world-index.json`; the save only supplies the integer map id.
- **Application**: only a validated object is applied. The whole in-memory `GameFlags` bank is cleared before the 64 saved words are copied; nothing else of the session is touched at this step (the session reset belongs to the load, E16.d).
- **Capture**: the current map comes from the world name's trailing id and the tile from the hero's proxy, as `UpdateSavedData` does; a capture is validated before it is written.
- **D-E16-31** — The DLL's tests do not round-trip through the service. They cover capture, validation and application; the two formats are covered by the engine's tests.

## Consequences

- A hand-edited or crafted save cannot put an out-of-domain value into the live game state, in either format; the refusal names the field.
- `Serialize` (field names, order and lengths) is first exercised by E16.d's in-game recipe, in JSON and in binary, including one hostile slot per format.
- Adding or reordering a field needs a new data version and a branch on `archive.DataVersion`.
- Accepted residual risks (single-player game): flag meanings cannot be validated, so an edited save can break the story, block the player or freeze the script interpreter; a tile inside the bounds but inside a wall is accepted; map-table values are not checked against the asset catalogue; `Hp = 0` is accepted until E18 decides otherwise.
- The game time does not advance while a world is loading.
