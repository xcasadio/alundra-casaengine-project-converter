# ADR-0067: The hero's shot reads the direction of the tick and wires types through a set that is empty until their handlers exist

- **Status**: Accepted
- **Date**: 2026-10-15
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 1.19 (E20.f3, D-F3-1 to D-F3-12) and 1.19.1 (E20.f3a, revisions 1 and 2, READY at its closing review); evidence in `docs/plan-e20-annexe/e20-f3/` (`oracle/`: 56 tools of the MIPS machine, 45 outputs in text and JSON, 12 one-word mutants all killed, the flame probe `flame_probe.json`); decisions of the main session of 2026-10-15 (mode AUTO, Q-F3-1 and Q-F3-2). D-F3-1 to D-F3-4 are implemented by E20.f3a (this ADR is written with it); D-F3-5 to D-F3-12 are PLANNED for E20.f3b and E20.f3c and recorded here so that one ADR covers the three sub-slices.

## Context

- The binary has one site where a weapon is born, `0x8002F818` in `UpdateWeaponStepProgression` (`0x8002F768`, called by `MovePlayer` at `0x80031D70`). It passes the hero as owner, table 0, the type `ptr[1]`, the hero's X and Y, his Z plus `ptr[3] << 16`, and the direction `[0x80023644 + 4 * (+0x98)]` (`{0, 0x10, 8, 0x18}`); the return is ignored, so a full slot table loses the shot silently and the pointer is cleared all the same. Nothing counts the live projectiles.
- `+0x98` is the direction index that `UpdateAnimation` (`0x80038B40`) rewrites at every tick; the shot of tick S reads the value written at the end of tick S-1 (the oracle keys `dirtest` and `dir_extra`: in the air, Square + Left at tick 8 shoots at tick 9 with 8; Left alone from tick 9 gives `0x18`; on the ground the four directions give `0x10`, 0, 8, `0x18`).
- The DLL wrote the direction from `AnimationDirection`, which `SyncAnimation` updates only when a frame ends. In a frame of two ticks the shot of the second tick therefore read the direction of the frame before (phase A of E20.f3a, test (d1): 2 ticks per frame gave `0x18` instead of 8). `BoxDirection` (the value of the tick, D-E14-6) and `TickFacing` hold the same value at the tick of the shot (test (d4), 4 directions at 1, 2 and 3 ticks per frame).
- The DLL's call of the shot was a counted, journaled stub. The apparition by sprite index exists since E20.e0a (ADR-0063, ADR-0065), but no entity is to be born by it until the handlers of its programs exist: the sprite of a type carries codes C and E in its header, and an unhandled code would leave the entity alive and mute.

## Decision

- **D-F3-1.** E20.f3 is cut into f3a (the direction and the wiring seam, no type wired), f3b (the hit effects 4 and 5 of the hit pass) and f3c (the wave of the sword, types 27 to 29, handlers C61 and E10), in this order; f3c waits for the logical-Z rule of E20.e0k.
- **D-F3-2.** The direction of the shot is `SpawnDirections[BoxDirection]`, read in `MovePlayer` before the tick writes anything, never `AnimationDirection`. A test states `BoxDirection == TickFacing` at the tick of the shot at 1, 2 and 3 ticks per frame. The index is clamped to the table (test (s6)).
- **D-F3-3.** `SpawnWeaponProjectile` keeps, in this order: the counter, the journal, then the test hook, which REPLACES the production call (never two apparitions); otherwise, for a type of the set `WiredWeaponTypes` only and when the host is an `IEntityWorldContext`, `SpawnEntityBySpriteIndex(hero, false, type, X, Y, Z, direction)`. A null return is tolerated (the shot is lost, the pointer cleared, as the binary). The set is empty in production in f3a, becomes {27, 28, 29} in f3c, and a per-thread test set replaces it for the tests.
- **D-F3-4.** An invariant, mechanically tested: for every type of the wired set, every non-zero C and E code of its header is handled (a key of the native registry for that type, or the inline E branch). A fake set {3} fails on `(E, 17)` (negative case in the same test). No type is wired before its handlers exist (D-F3-4 is why f3a wires none).
- **Planned for f3b and f3c (not implemented by f3a).**
  - D-F3-5: E10 is handled like E0 and E1, in the inline branch of `RunSpriteEvent`, outside the switch of the native handlers (same body, `DestroyEntity(e, -1)`; only types 27 to 29 carry it), so that the arena pin stays at 2 contacts.
  - D-F3-6: C61 is a native handler of the registry (key (C, 61), types 27 to 29) calling the named stub `CheckAndTriggerTileEffect` (`0x8003A7B0`) with a per-thread journal; the grass is not cut before E20.e (Q-F3-2); the binary draws at least one word per cut cell, so the stream drifts after any wave over grass (documented deviation, O-F3-1).
  - D-F3-7: A0 of types 27 to 29 is not registered (it writes `AIValues[0]` only, never read); f4 to f6 will widen the Muruta A0 registration to a shared set of types.
  - D-F3-8: an entity born by the interface without the gravity bit keeps a logical Z (rule delivered by E20.e0k, marker `HasLogicalZ`, D-E0K-1 and D-E0K-4; fallback B of Q-F3-1 dropped).
  - D-F3-9 and D-F3-10: `AlundraHitPass.Run` gains an optional `attachEffect`; the gate `(r & 0xC0) != 0x80` is the gate of the `TouchingEntity` store; nibbles 6 and 10 give effect 4, nibbles 7 and 9 give effect 5; the call precedes the store and the sparks; no draw; the hero target is not filtered (its balance record 0 has null responses).
  - D-F3-11 and D-F3-12: the hit arena keeps its native handlers off; comparisons read the oracle by key; the binary's Z is the DLL's Z plus one raw unit (ADR-0026); for the stream only the differences count (local step, D-E0B-8).

## Consequences

- The shot's direction is exact at any grouping of ticks; the only observable change of f3a is the `Direction` field of the journal of the stub (the eight cases of the journal that pin `0x18` do not move). No entity is born in production by f3a.
- The seam is inert (empty set) and removable on its own: the line of the direction is one line, the wiring is a guarded call. The test hook stays until the shot is real for every type (E20.f6).
- `SpawnWeaponProjectile` is `internal` (it was `private`) so that the bound of the direction index can be tested directly: no tick of the game produces a `BoxDirection` outside 0 to 3, so a mutant that removes the clamp cannot be caught by playing.
- The invariant makes the order "handler before wiring" a failing test instead of a convention; f3c, f4, f5 and f6 each extend the set and must make it pass.
- Known limits, deferred: the real grass cutting (E20.e), the effects 4 and 5 and the wave (f3b, f3c), the charged arrow, the arrows and the balls (f4 to f6).
