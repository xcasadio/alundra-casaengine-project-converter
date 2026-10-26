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

---

# E20.h3a T2 (tests of the world, red before T3): results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `a7d76151` (T1). One commit of tests, then this file. **No file of `Alundra/` changed**: the registry of production is untouched, so the new tests run on it and are red.

## What was written

- `Alundra.Tests/Ai/Items/BombRun.cs`: the bench of the bomb born posed (the model of `BallRun`). The real world of map 14 (`HitArena`), the synthetic field of the oracle on the same records as its cell store (`SyntheticField`, `CutBench`), every entity of the map but the hero and the targets removed,
  the native handlers of the world ON (`HitArena.EnableNativeHandlers`: the registry of PRODUCTION; **nothing is registered by the test**), the bombs born by `SpawnEntityBySpriteIndex(hero, false, 2, x, y, 0, 0)` (no hands, no C62), the first one put in status 3 by the harness at the end of the tick
  `StatusTick` = 5 (the tick k+0 of the keys, before that tick is read, as the scripts print the state after the poke). It records per tick the state of each bomb, of the hero and of the targets (`HitTickSample`), the sounds with their tick, the effects the stage creates
  (`NativeEffects.CreatedForTests`), the calls of the spark seam, and the generator after each frame with the steps it made. With `seedAtStatusTick` the generator is posed at the seed of the key at the head of the tick k+1 (the key's `rng0(k+1)`; the per-frame step is the engine's, so the state after the frame ending at k+n equals the key's `rng0(k+n+1)`, as the probe of T0 (f) showed).
- `Alundra.Tests/Ai/Items/BombKey.cs`: reads the keys from their files of the annex (nothing is retyped): `LoadOneBomb` (k_w1_away, k_w1_17east), `LoadTwoBombs` (k_w3_chain_away), `LoadGrass` (grass_placed_pf). The scripts print a line only when the state or the journal changes, so a tick without a line holds the state of the last line and an empty journal.
- `Alundra.Tests/Ai/Items/BombWorldTests.cs`: (w1), (w2), (w3), (w4), (w6). `Alundra.Tests/Arcs/OlenBombLidArcTests.cs`: (w5) added as `W5_...` on the bench of A362 (the shared scene `Scene(k, true)`; no change to the existing tests of that class).

| test | cases | key | what is compared |
|---|---|---|---|
| (w1) `W1_...` | 3 (1, 2, 3 ticks per frame) | `../e20-a362-disc/discovery/out/k_w1_away.txt` | every tick k-4 to k+56: present, status, target animation, end of the animation (`+0xAC`), frame box; the sound 46 once at k+1; hero 10 points, never touching, no spark |
| (w2) `W2_...` | 87: the grid (25) and the scans (54) of `zone_335_grid.txt` / `zone_335.txt` at 1 tick per frame, plus 8 rows (centre, two corners just inside, two just outside in X and in Y) at 2 and 3 ticks per frame | `zone_335*.txt` | the Beannoid of the map 14 (record 335, put dormant as its program A leaves it, at (300 + dx, 300 + dy) after the synthetic field is installed): the first tick it is touched (animation 5 or life change; the oracle's tick 4 is the status 3), its life at the end, ONE change of life; a target outside the bounds keeps 9 and the status 2 |
| (w3) `W3_...` | 3 | `k_w3_chain_away.txt` | both bombs, every tick to k+74: status, animation, end, hit counter, box; sounds 46 at k+1 and k+19; hero 10 points |
| (w4) `W4_...` | 2 (1 tick per frame) | `grass_placed_pf.txt` | the five cells cut (bit 0x02 cleared, the 20 others untouched), the five effects 2 at their place and tick, the sounds (46, then one 31 per cut; the 84 of a dropped object is not in the key), the generator after every frame (state and steps) from k+1 to k+53; the same bomb on a field without grass cuts nothing and still leaves its slot |
| (w5) `W5_...` | 3 | `k_w5_lid.txt` | the lid of the 362 in status 3 at H+1 (k+0), status 4 at H+2 with the sound 230 once and ONE effect 11 in the frame of that tick, out of the world from H+3 |
| (w6) `W6_...` | 3 | `k_w1_17east.txt` | the bomb and the hero every tick to k+54: hero life, `0x31`/`0x1C`/rest, touching, the counters 119/25, position to the hundredth; the facts of the plan (10 to 5, sound 23, -33.09375 px, one set of sparks at k+17) |

## Red before, then what was verified to make them valid

Red run on the HEAD (registry of production, no registration): `dotnet test ... --filter "BombWorldTests|OlenBombLidArcTests"` gives 147 tests, **51 failures, 96 passes**. The 51 failures are exactly: (w1) 3, (w3) 3, (w6) 3, (w4) 2 (the field with no grass fails too: the bomb never leaves its slot without E12), (w5) 3, and 37 rows of (w2) (the rows whose target is INSIDE the bounds: the target is never touched). The 50 other rows of (w2) are the
rows just OUTSIDE the bounds: they pass before and after (they are set next to the positive rows in the same theory, as the plan asks), and the 46 existing cases of `OlenBombLidArcTests` stay green. The full suite in Release gives the same 51 failures and nothing else: **7246 tests, 51 failed, 7195 passed** (17 min 15 s; the `bash` timeout moved the run to a task, which finished). The four `docs/hero-trace-389-*.txt` were rewritten (end of lines only: `git diff --ignore-cr-at-eol --quiet -- docs/` true) and restored.

**The benches were validated green before the commit** (a throwaway registration, never committed): the two lines `BombAi.Register(registry)` and `BombLidAi.Register(registry)` added to `CreateForProduction` by a script, the same 147 tests run: the 98 tests of `BombWorldTests` and the 3 of (w5) pass, every tick of every key held (no tolerance); the only failures were the two pins of A362 (`A9_TheLidStaysInStatus3_E19Missing`, `A9_TheBombStaysInStatus3Invisible_E12Missing`, 3 cadences each = 6), which T3 rewrites. The registration was undone (`git checkout`), `git diff` of `Alundra/` empty.
So the (w) tests are red for the right reason only (the missing registration), and their greens are reachable with the two calls alone: **no other lack was found**.

## Deviations from the plan (flagged)

1. **The hero is the hero of the new-game kit** (T0 finding): the bench does not empty his inventory; (w6) measures 10 to 5 on the real bomb, as the key.
2. **The current animation (`+0x90` of the key) is not compared.** The DLL's `CurrentAnimationId` is validated at the end of the frame (it reads -1 before the first validation and lags one frame behind the target animation: at k+1 the key has 1, the DLL 0 at every cadence, even at one tick per frame). The target animation (`+0x88`), the end flag (`+0xAC`), the status and the box, which are what E12, D30 and the chronology read, are compared at every tick and held at the three cadences.
3. **The targets of (w2) are put after the synthetic field is installed** (`HitArena.Place`), because the real map has walls and pits at (300, 300); they are the Beannoids of the map (natural positions at the construction), set in the dormant animation 2 that their program A (`BeannoideAi.Load`) gives them at birth in production (the world is built with the handlers off, so A1 has not run), which keeps the walk of C1 from moving them. The oracle's target is born by its real program A.
4. **(w5) is a test of `OlenBombLidArcTests`**, not a new class: the scene, its timeline and its sound recorder are private to that class (the bench of A362, T0 (e)).
5. The sound 84 (an object a cut lets fall) is left out of the comparison of (w4), as `ChargedArrowLifeTests` does: it is not in the key; the generator comparison, which holds to the word at every frame from k+1 to k+53, shows the draws of the loot are those of the key.
6. (w2) `first` is compared relative to the status 3 (the oracle's tick 4, its fuse shortened to 2), as the file says; no other value of the oracle depends on the tick of the status 3.

## Predicted list (T0 (d)) at T2

Nothing moves: no existing test changed; the five pins of the registry, the two pins of A362 and `LogicalZWorldTests` Z4 and Z7 are green. They move at T3 only (the five pins and the six cases of A362; Z4 and Z7 predicted neutral).

---

# E20.h3a T3 (phase B, the two registrations): results (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `4d98a658` (T2). One commit: the two calls and the rewritten pins.

## What changed

- `Alundra/Ai/NativeSpriteRegistry.cs`: `CreateForProduction` calls `BombAi.Register(registry)` and `BombLidAi.Register(registry)` after `BallAi.Register` (14 `Register` calls; 18 keys -> 21), the two usings, the two doc comments.
- The five pins of the registry (predicted list rows 1 to 5): the sums gain `+ BombAi.Keys.Count + BombLidAi.Keys.Count` (`NativeSpriteRegistryTests` production runner, `WaveSwordLifeTests.V3`, and in `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `BallFlamesTests.M3` both the `sum` registry (two `Register` lines) and the explicit sum on the next line, which the T0 table had not listed as a second edit).
- The two pins of the arc A362, rewritten on their expected value after h3a, renamed without "Missing":
  `A9_TheLidIsDestroyedOneTickAfterItsStatus3_WithTheSound230AndTheBreakEffect11` (status 3 at H+1, status 4 at H+2, sound 230 once at H+2, ONE effect 11 in the first frame that holds it, out of the world from H+3) and
  `A9_TheBombIsDestroyedAtTheEndOfItsExplosion_E12` (Normal from A to A+50, status 4 at A+52, slot free from A+53; the values were read from the run, they are the ones the old comment of the pin predicted). Cadences k = 1, 2, 3 each.

## Red before / green after

- Before T3 (T2): the 51 (w) failures, see above. After: `BombWorldTests` + `OlenBombLidArcTests` + the (b) tests + the five pins: **261 tests, 261 green** (first run: 9 red = the 3 pins whose explicit sum I had missed (R1, C2, M3) and the A9 lid pin whose effect count was a standing count of the stage; fixed in the tests, not in the DLL).
- Removal proof (the two calls deleted by script, same 261 tests, then restored, `git diff` of `Alundra/` back to the T3 change): **62 red** = (w1) to (w4), (w6) in `BombWorldTests` (48 rows) + `W5` (3) + the 5 pins of the registry + the 6 A9 cases of the arc A362 (lid 3, bomb 3). Exactly the predicted set; nothing else red.

## Full suite

Release (`skipcopy.targets`), three disjoint partitions (`e20-e0d/suite-wired/run.sh` copy; p1 Arcs/Ai/Audio/Cameras/Cells/Combat/Dialogues, p2 Entities/Events/ExportGuards/Hero/Items, p3 the complement): p1 2046, p2 3229, p3 1971 = **7246 tests, 0 failure** (p1 took 10 min 1 s). The same 7246 as the T2 run (no test added or removed by T3). Predicted list respected: the only existing tests that moved are the five pins and the two A9 pins (six cases); `LogicalZWorldTests` Z4 and Z7 stayed green (neutral, as predicted). The four `docs/hero-trace-389-*.txt` were rewritten (end of lines only, `git diff --ignore-cr-at-eol --quiet -- docs/` true) and restored. `check_names.py --rules`: 0 finding (the same pre-existing warning).
