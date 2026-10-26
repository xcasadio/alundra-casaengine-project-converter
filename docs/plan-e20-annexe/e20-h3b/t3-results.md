# E20.h3b T3, phase B: the entries 0x23, the registration of (C, 62), the preset (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `c51504fb` (T2). One commit: the code, the tests moved, the preset, `docs/test-saves.md` and this file. `CasaEngineMonogame`, `alundra-datas-analyser`, `tools/`, `data-extracted/` untouched.

## What was written

- `Alundra/Hero/PlayerManager.cs`: the entries `0x23` of the three tables of h0a (`BombItem`): the start (`BombAi.Start`), the driver (`BombAi.Drive`) and the cancellation (`BombAi.Cancel`); the seams of the tests still win; the other items stay named stubs.
- `Alundra/Ai/Items/BombAi.cs`: `(C, 62)` joins `BombAi.Keys` and `BombAi.Register` (the T0 choice: the five sums of the registry follow by themselves, 26 keys become 27, no edit in them); `Start` poses the two resolutions below.
- `Alundra/SaveGames/TestSaves.cs` and `docs/test-saves.md`: the preset `bombe-m14` (map 14, tile (31, 43, 1), the weapon loadout, `Item: 35`, `Weapon: 1`), the last of the table.

## The two causes the T2 report left to T3, resolved inside the DLL (no engine file, no change of the spawn)

1. **The birth tick**: `BombAi.Start` poses `bomb.JoinsBirthTick = false` right after the birth. The oracle's bomb is `status 1` at the tick of the Circle and `2` at the next, the fuse counting from the third; with the marker the whole timeline of (w1) was one tick early (T2's measure). One line; the entities of the other births keep the marker.
2. **The sub-pixel**: two writes. (a) `EntityScriptProxy.PushLogicalPositionToRoot(bool keepSubPixel = false)` (new optional parameter, false by default: every other caller is unchanged); `FollowCarrier` (an entity in the hands of another) calls it with `true`, so the root X and Y keep the fraction of the logical position and the head-of-tick pull gives it back (before, the whole-pixel root truncated the carried object's position every tick: the bomb at 206.0 where the key says 206.09375, and the thrown bomb left from there). (b) `BombAi.Start` pushes the newborn bomb's root once with the fraction, because the first tick of the bomb pulls the whole-pixel root of its creation before the carrying takes over (the only difference left at the tick 5 of (w11)). This touches `FollowCarrier`, so every object carried by the hero (the crates, the barrels, the torches) now keeps the hero's fraction: **no existing test moved** (the suites below), no key of the other slices is whole-pixel. Not an engine change and not the spawn of all entities; flagged below.

## Tests moved (against the predicted list of the T0 README, section (d))

| Test | Why | Edit |
|---|---|---|
| `BombAiTests.B4_TheBombRegistersC62D30AndE12_...` (renamed from `...D30AndE12...`) | row 1: 3 keys, `(C, 62)` present for the type 2 only | count 2 -> 3, `Keys` gains `(C, 62)`, neighbours (C, 61), (D, 62), (E, 62) absent |
| `BombAiTests.B4_TheBombAndTheLidShareTheSlotE_...` | row 2 | counts 3 -> 4 |
| `BombRun` (the bench of h3a: `BombWorldTests` W1 to W6, 34 cases) | row 4: a bomb born by the interface has the fuse 0 and C62 would explode it at once | the bench poses the fuse 240 after the birth (`BombAi.SetFuse`), no assertion changed |
| `LogicalZWorldTests.Z4_...` and `Z7_...` | rows 5 and 6 | the fuse posed to 240 right after `Born`, the oracle's Z values untouched |
| `HeroItemUseTests.A4_TheStub...` | row 7: `0x23` is no stub any more | the cape `0x20` (h2, still a stub) in its place: `start 0x20` journalled |
| `TestSavesTests`: `TheThirtyFourPresets_AreListed_...` (renamed), the four `EveryPresetName` theories (one row more), `TheOtherPresets_LeaveTheDeathCounterAtZero`, new `TheBombPreset_IsTheWeaponLoadoutOnTheCellOfTheGrassPreset_...` | row 8 | the name in `AllPresetNames`, the content test |
| `BombHandsWorldTests.W11` (T2's own test) | its key file prints the journal after the tick 240 only; the oracle replayed with the whole journal (`sc_hurt.py 222`, `j[0] > 0`, from a copy) plays the sound 24 at 73 (the landing of the first bomb, thrown at 50) | the expected sounds gain `(73, 24)`; the key's file is untouched |

The five sums of the registry (`NativeSpriteRegistryTests`, `LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`, `WaveSwordLifeTests.V3`, `BallFlamesTests.M3`, and their secondary sums) write `BombAi.Keys.Count`: green with no edit. `OlenBombLidArcTests` (47 cases, the bomb of Olen on the map 362): **neutral, as T0 predicted**, green (its C is a map program, the registry is not consulted for it). No test outside the list moved.

## (w1) to (w11): red before, green after

`BombHandsWorldTests`, 26 cases: 26 red at `c51504fb` (T2), 26 green after: W1 (1), W2 (1), W3 (4 directions + the air = 5), W4 (the bounce + 2 feet of a wall = 3), W5 (1), W6a (1), W6b (1), W7 (2), W8 (1), W9 (5), W10 (4), W11 (1). No tolerance anywhere (tick by tick, the position to the 0.1 px of the key).

## Proof of removal (real mutations by script, `git diff` restored by `cmp` against a backup after each)

| Mutation | Red |
|---|---|
| the entries `0x23` removed (`BombItem = 0x2300`: the three table entries fall back on the stubs) | **exactly the 26 cases of (w1) to (w11)**, in `Ai.Items.` (200 tests: 26 red); `Hero.Hero*` 1299, `Hero.` apart 550, `Arcs. Audio. ... Events.` 2208, everything else 2191: 0 red. (`Ai.Objects.`, `Ai.Projectiles.` not run: they hold no bomb test.) |
| the registration of (C, 62) removed (the key moved to `TickCode + 1000`) | in `Ai.Items.` (275 in `Ai.` apart from the two others): 20 red = **19 cases of (w1), (w2), (w3) x5, (w4) x3, (w6b), (w8), (w9) x2 (the two that open after 243), (w10) x4, (w11)** + `B4_TheBombRegistersC62D30AndE12_...` (the pin of the key by name, which names (C, 62)); (w5), (w6a), (w7) and the first three cases of (w9) stay green, as the plan says. The six other partitions (all seven were run): 0 red. |

## Suites (zero failure)

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, build and test), `--no-build --blame-hang-timeout 60s`, seven disjoint partitions (the driver is `run_part.sh` of the scratchpad), then Debug last:

| Partition | Tests | Release red | Debug red |
|---|---|---|---|
| `Ai.Objects.` | 634 | 0 | 0 |
| `Ai.Projectiles.` | 332 | 0 | 0 |
| `Ai.` apart from the two above | 275 | 0 | 0 |
| `Hero.Hero*` | 1299 | 0 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 0 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 | 0 |
| everything else | 2191 | 0 | 0 |
| total | **7489** (= 7485 + the content test of the preset + the 3 rows of the theories) | **0** | **0** |

The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet` clean for them, restored by `git checkout`, never indexed. `cmp` of `Alundra/bin/Debug/net9.0-windows/Alundra.dll` and `.pdb` against `alundra-project/`: identical. `python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 existing warning (`TextBoxGpu.cs`).

## Flagged for the main session

- **`FollowCarrier` now keeps the sub-pixel for every carried object** (not only the bomb): the binary's carried position is the carrier's plus the offset, fraction included, so it is the more faithful; no test or key moved. If the author wants the whole-pixel carry back for the other objects, `keepSubPixel` can be passed by the bomb's type alone, but (w3), (w5), (w10), (w11) need it for the bomb.
- The tests of T2 were not wrong except (w11)'s expected sounds (above).
- The recipe of the author (not blocking): the save `bombe-m14`.
