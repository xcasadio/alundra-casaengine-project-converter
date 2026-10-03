# ADR-0032: The cellular rain respawn draws the C library rand() of the binary

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.m2" (decision D-E19-66, taken by the session in AUTO mode under the author's rule "the binary decides"; open items O-E19-48 and O-E19-49). It replaces the choice of the random stream made by decision D7 of `docs/plan-e9d-mode-cellulaire.md` (line 245). Engine side: ADR-0050 of CasaEngineMonogame.

## Context

- The respawn of a type 2 (`FallRespawn`) cellular cell, the rain of maps 31 and 391, was wired to `AlundraRandom` (`AlundraWorldProxy.InitializeWithWorld`, `CellularLayerComponent.RandomSource`), the game's shared stream, because decision D7 and the decompilation (`GraphicManager.cs:1172`) say the original shares one seed across every random draw.
- The binary `ALUN_CD.EXE` (France) does not: the routine at `0x8005D05C` calls the C library `rand()` (`0x80081E6C`, `0x8005D31C`: `s = s * 0x41C64E6D + 0x3039`, state at `0x801EEB48`, result `(s >> 16) & 0x7FFF`) and sets `posX = rand() / 102` with a signed, truncating divide (`0x8005D324`-`0x8005D340`), 0 to 321. The state is 0 at each process start (BSS cleared at `0x8008B548`), `srand` (`0x80081E9C`) has no caller, and `rand()` has exactly three callers: this respawn and the two memory-card block writes (`0x80061150` in `0x80060E20`, `0x80061584` in `0x8006122C`: 36 values `r & 0xFF` at offset `0x1FB0` of the block, before the checksum).
- The only cells of type 2 in the export are on maps 31 and 391 (55 rain cells each); on those maps the walk reaches no other `AlundraRandom` consumer.
- The DLL's saves go through the engine save service and draw nothing.

## Decision

- A new static generator `AlundraLibcRandom` (`Alundra/Scripts/AlundraLibcRandom.cs`) reproduces the C library `rand()`: state 0 at process start, never reseeded, never reset in production, `Next()` = the formula above. `AlundraWorldProxy.InitializeWithWorld` wires `CellularLayerComponent.RandomSource` to it (contract of the delegate: the next `rand()` value, 0 to 0x7FFF; the engine divides by 102, ADR-0050).
- `AlundraRandom` stays the game's shared stream for every other draw; the engine still owns no generator and the DLL owns the stream, its state and its reseed policy (the split of D7 holds, only the choice of stream changes).
- Known gap, kept until the author decides (O-E19-49): the original draws 36 values from this stream at each memory-card block write, so after a save the rain positions of maps 31 and 391 follow another point of the stream than here; the DLL's saves draw nothing. Reproducing it would need 36 draws per save write and a mapping of the DLL's save actions to the two binary write paths, which is not established.

## Consequences

- The rain of map 391 (on the story chain) takes other abscissas than before, from the binary's stream, and can reach 320 or 321 (off screen, wrapped on the next tick); `AlundraRandom` no longer advances about 3 draws per tick on that map, so later draws of its consumers (random opcodes, direction modes 4 and 5) differ from before, in the direction of the original.
- The generator is process-wide static state: the tests that touch it run in the `AlundraRandom static state` collection and restore what they find.
- A save-then-rain sequence differs from the original until O-E19-49 is decided.
