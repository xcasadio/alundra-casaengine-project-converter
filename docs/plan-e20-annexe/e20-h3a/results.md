# E20.h3a T1 (phase A): results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `d43d8989` (T0). Two commits: the tests (`4311b7ad`), then the handlers and this file. Nothing is registered in production (`NativeSpriteRegistry.CreateForProduction` is untouched).

## What was written

- `Alundra/Ai/Items/BombAi.cs` (namespace `Alundra.Ai.Items`): `Touch` = D30 (slot D, code 30), `Deactivate` = E12 (slot E, code 12), `CheckAndTriggerTileEffect` (journal `StubCallsForTests`, then `TileEffect.Trigger`, the form of D-H-6 and of the wave), `Keys`, `Register(registry)` returning the disposable of the two keys (type 2 alone).
- `Alundra/Ai/Props/BombLidAi.cs` (namespace `Alundra.Ai.Props`): `Break` = E19 (slot E, code 19): `PlaySfx(0xE6)` then `DestroyEntity(e, -1)`; `Keys`, `Register(registry)` (types 241 to 243).
- Tests: `Alundra.Tests/Ai/Items/BombAiTests.cs` + `BombVector.cs` (the loader of the vectors), `Alundra.Tests/Ai/Props/BombLidAiTests.cs`, `AnnexFiles.BombAnnex` (finds a file under `docs/plan-e20-annexe/`).

Model followed (the critic's `my_models.py`, checked against the 25 + 25 vectors): E12 = `if +0x88 == 1 and +0xAC == 1 -> destroy; elif +0x184 & 4 -> destroy; else cate, +0x88 = 1, flags = (flags | 0x40) & ~0x30`. The cut is therefore called at every call that does not destroy (never on the destroying call). D30 reads `Attribute & 0xF` of the toucher's current attack with neither a frame box nor a zero-byte test (the helper of the opcodes tests both; the binary does not).

## Tests (Release, `skipcopy.targets`)

| Group | Tests | Content |
|---|---|---|
| (b1) D30 | 3 | the 25 vectors replayed word for word (4 set the status 3, 21 leave it; `Wrote(+0x10)` = status Deactivated); all 256 attribute bytes (low nibble 4, 6, 10 only); the plan's 11 nibbles side by side; no frame box needed; no toucher / no attack / only the status is written |
| (b2) E12 | 1 + 4 (theory) + 1 + 5 (theory) + 4 (theory) + 1 | the 25 vectors (8 cut, 17 destroy) on flags, animation, journal; first call on four flag words; the cut at every call (5 calls) then the destroying call without a cut; animation 0/2/5 with end 1 and animation 1 with end 0/2 keep the bomb; holes `0x04 0x84 0xFFFFFFFF 0x5` destroy, `0x00 0x01 0x80 0xFB` do not; no host |
| (b3) E19 | 1 + 3 (theory) + 1 | the 25 vectors of T0 (journal `sound 0xE6`, `destroy2 E -1`, no word read, none written) against the handler, the sound before the destruction (`DestroyedBefore = 0`); the three types; no host / no sound player |
| (b4) keys | 3 + 2 | D30 and E12: type 2 only (0, 1, 3, 4, 241, 243, 258 refused), neighbour keys absent, the registered lambdas run the bomb handlers, the disposable removes both; E19: 241 to 243 only (0, 2, 240, 244, 314, 315, 258 refused); both classes share the slot E on one registry (3 keys), a second `BombAi.Register` throws and leaves 3 keys |

Counts: **29 tests, 29 green** (`dotnet test --filter "FullyQualifiedName~BombAiTests|FullyQualifiedName~BombLidAiTests"`).

Red before: with the tests alone the test project does not build (`CS0234`: the namespaces `Alundra.Ai.Items` and `Alundra.Ai.Props` do not exist); the 29 tests are red by construction until the handlers exist. Green after: 29 of 29.

Full suite, Release: **4370 tests, 0 failure, 0 skipped** (9 min 47 s; the runner printed `Série de tests abandonnée` and a vstest socket error AFTER the `Réussi` line, when the 595 s bash timeout closed the data collector: the result line is complete). The four `docs/hero-trace-389-*.txt` were rewritten (end of lines only: `git diff --ignore-cr-at-eol --quiet -- docs/` true) and restored by `git checkout`.

`python -B tools/dll-structure/check_names.py --rules`: 0 finding (one pre-existing warning on `TextBoxGpu.cs`).

## Mutants (real, applied by script then undone, `git diff` empty after)

12 applied to the production files with `h3a-mut.py` (kept outside the repository), all killed (failures in the 29 tests): attribute 6 forgotten (1), 10 forgotten (2), 5 added (1), status written as Normal (4), `0x40` forgotten (3), `0x30` not cleared (5), destruction at animation 0 instead of 1 (6), hole ignored (5), cut called on the first call only (4), E19 without the sound (5), E19 destroyed with the effect 11 instead of -1 (4), D30/E12 registered for the type 1 (2). The T4 list of the plan (12 mutants, with the "cut called once only" shape) is covered; T4 replays it with the world tests.

## Predicted list

No existing test moved: the registry of production is untouched, so the five pins (rows 1 to 5 of the README), the two pins of the arc A362 and `LogicalZWorldTests` Z4/Z7 are unchanged and green in the full run above. They move at T3 only.

## Flags for T2 and T3

1. **The hole word `+0x184` for a bomb born by the interface.** `UpdateVramFlags` feeds `CombinedVramFlagsAND` for the player and for the entities marked `HasLogicalZ` ("the entities born in play", E20.g2b2); a bomb born by `SpawnWarpEntity` should be marked, but the unit tests cannot say: they set the word by hand. T2 (w1) must measure it: a bomb on flat ground must read 0 here (a hole bit would destroy it at its first E12 call).
2. The unit tests drive the handlers directly (`BombAi.Touch`, `BombAi.Deactivate`, `BombLidAi.Break`); the order "D30 in slot D, E12 in slot E" and the status gates (slot E only for the status Deactivated) are the runner's, tested by the world tests (T2).
3. `ResyncControllerFromFlags()` is called after the flags write (as `BallAi.Explode`; `LiftableAi.Break` does not). A bomb with a controller keeps its walkability mask in step with the flags; the bits touched (`0x10`, `0x20`, `0x40`) are not part of the mask in the cases seen, so the call is neutral. Revisit if T2 shows a difference.
4. No stop fired: no test outside the predicted list moved, no name refused, no engine file touched.
