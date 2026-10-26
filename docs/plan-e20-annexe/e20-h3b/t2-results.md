# E20.h3b T2: the tests of the world (w1) to (w11), red before T3 (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `578aed1f` (T1). One commit: the tests and this file. **No code of `Alundra/` changed.** `CasaEngineMonogame`, `alundra-datas-analyser`, `tools/`, `data-extracted/` and `alundra-project/` untouched.

## What was written

- `Alundra.Tests/Ai/Items/BombHandsRun.cs`: the bench. The real world of map 14 built by `ArcRun` (real prefabs, real hero controller), the cells of the scenario installed by `SyntheticField`, every entity of the map but the hero removed, the native handlers ON (the registry of production decides; the bench registers nothing), the hero put at the pixel of the scenario, the bomb given (`Give(35)`) and equipped (`SetItem(35)`), the boots `0x1B` given for the swim. The pad of a tick is the key's `hold(t)`: a lone Circle or Square goes through `ArcRun.Press`, the rest through `HoldDirections`; one tick a frame (so the per-frame step of the generator is the oracle's). `seeded` poses the generator at the oracle's seed `0x12345678` at the first tick. Every tick is sampled at its `inventory` pass (the end of the tick): the hero, every bomb (numbered in the order of the births), the sounds, the effects (also scanned right after the frame: what the entities `Emit` is applied by the stage at the end of the frame, and the key's tick of an effect is the tick of the code that asked for it), the centre of the hit.
- `Alundra.Tests/Ai/Items/BombHandsWorldTests.cs`: 26 test cases (11 scenarios), on the registry and the tables of production. Every value is typed from its key or read from the key's file at run time (w8, w10 and w11 read `explode_in_hands.txt`, `k_throw_explode.txt` and `bomb_hurt_while_carrying_bomb.txt`; w10 first checks that its typed data says what the file says). A tick of a key without a row holds the columns of the row before it (the keys print the changes only).

| Test | Key | Cases |
|---|---|---|
| `W1_` | `bomb_timeline.txt`, `hero_hit.txt`, `sparks/spark_values_pf.txt` | 1 (tick by tick 1 to 300, no tolerance: status, fuse, hero state, link, item, Z, the seven sparks with their forces, the wound) |
| `W2_` | `bomb_limit3.txt` (critic) | 1 (the rows of the key to 323, the start of 351) |
| `W3_` (x2) | `throw_dirs.txt`, `k_throw_explode.txt`, `bomb_air_throw.txt` | 4 directions + 1 in the air |
| `W4_` (x2) | `bomb_wall.txt`, `bomb_wall2.txt` | 1 bounce + 2 feet of a wall (columns 10 and 11) |
| `W5_` | `bomb_locked.txt` | 1 |
| `W6a_`, `W6b_` | `bomb_water.txt`; `water.txt` (critic) | 1 + 1 |
| `W7_` | `states_sample.txt` (critic R1) | 2 (Circle at 4 and at 14 after a stroke at 2) |
| `W8_` | `explode_in_hands.txt` | 1 (ticks 240 to 320, tick by tick, no tolerance) |
| `W9_` | the items in use of `bomb_timeline.txt` | 5 (Start at 2, 100, 243: refused; 244, 245: opens) |
| `W10_` | `k_throw_explode.txt` | 4 directions (the south one is hurt at 273 with hp 5 and the state `0x31`; no tolerance) |
| `W11_` | `bomb_hurt_while_carrying_bomb.txt` | 1 (ticks 1 to 386, tick by tick, no tolerance) |

## Red before T3

`dotnet test --filter "FullyQualifiedName~BombHandsWorldTests"` in Release (with the neutralised copy): **26 / 26 red** at `578aed1f` + the tests (the Circle starts nothing: the entry `0x23` is the silent stub, and (C, 62) is not registered). Every red is a difference with the key (no exception). The whole suite in Release, by the partitions of T1 (`Ai.` 1241, `Hero.Hero*` 1299, `Hero.` apart 550, `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` 2208, everything else 2187): **7485 = 7459 + 26 tests, 26 red (all of `BombHandsWorldTests`), 0 other red**. No existing test moved, none was rewritten. `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt` (restored by `git checkout`; `git diff --ignore-cr-at-eol --quiet -- docs/` clean). The Debug build came last.

## Check that the tests CAN go green (throwaway wiring, not committed)

To make sure the keys are reachable and the tests are not wrong, a throwaway wiring of the phase B was applied to the working tree, run, and **reverted by `git checkout -- Alundra/` (`git diff` empty after)**: the three call sites of `PlayerManager` (`ItemStartForTests ... : ItemStartStub` -> `item == 0x23 ? BombAi.Start(player)`, the same for `Drive` and, in `CancelItemMode`, `BombAi.Cancel(player)`), and the registration of (C, 62) next to D30 and E12 in `BombAi.Register`. Results:

| With that wiring | |
|---|---|
| Green tick by tick, exactly | **W2** (the limit of three), **W4** the bounce (the unique bounce, the spark at 51, the half turn, the landing at 74, the sound 24 at 75), **W7** (both), **W8** (the whole explosion in the hands to 320, hero and two bombs), **W9** (5 / 5) |
| Green but for the cause 2 below | W1 (every status, fuse, state, link, item, Z, the seven sparks AND their forces: the draws are the key's), W5, W6a, W6b, W3 (4 + air), W4 foot of a wall (2), W10, W11 |

The two causes the throwaway wiring brings to light (they are T3's to resolve; none is a fault of the keys or of the tests):

1. **The bomb joins the tick of its birth** (`JoinsBirthTick`, E20.e0a3b: an entity born by `MovePlayer` runs in the very tick). The key's bomb is `status 1` at the tick of the Circle and `2` at the next (its load program runs one tick late: the fuse starts to count at the tick 3), so with the engine's rule the whole timeline of (w1) is one tick early (status 3 at 242, sound 46 at 243, the wound at 260). Posing `bomb.JoinsBirthTick = false` right after the birth in `BombAi.Start` (one line, throwaway test) removed the whole shift and made W1, W2 and W8 exact.
2. **The bomb loses the sub-pixel of its position** (the carried bomb, and so the thrown one, is at the pixel of the hero: x = 206.0 where the key says 206.09375, 135.5 where it says 136.328125; every other column is the key's). The offsets of (w3) and (w10) are the hero's fraction away (34.50 -> 33.67), and the EAST throw of (w10), at rest 34.42 px from the hero instead of 35.25, falls inside the zone and hurts the hero (hp 5 at the tick 273) where the key says "indemne". The fraction is dropped between `SpawnEntityBySpriteIndex(hero.PosX ...)` and the carried position (the root keeps whole pixels: `ResolveLogicalPosition`). W5, W6a, W3, W4 (feet) and W11 differ from their keys by that alone (the F1 positions and the raw words of the smoke at `13500416` instead of `13506560`).

Other findings of the experiment, no action in T2:
- The centre of the hit of the wound (the seam `HitSparksForTests`) is in the DLL's Z (the binary's minus one unit, ADR-0026): `(6553599, 6553599, 360447)` where the key's creation argument is `360448`. W1 expects `360447`.
- The DLL does not play the sound 11 at the landing of the hero's jump (the key `bomb_air_throw` plays it at 91): it is the hero's, not the bomb's. W3 (air) compares the sounds but 11.
- The Emit-ted smoke (effect 12) and the effect 6 are applied at the end of the frame: the bench attributes them to the tick that asked for them (the scan after the frame).

## Deviations from the plan, and what is NOT compared

1. **(w2)**: the bomb born at the start of 351 is compared on the fuse (240), the sound 44, the hero and the count; not on its status. The critic's key prints that bomb in `status 2` at once (the oracle's recycled slots), unlike the first three births (`status 1`); the plan's list ("le quatrième départ refusé tant que la première case n'est pas libre") does not need it.
2. **(w3)** in the air: the sound 11 is left out (above).
3. **(w6b)**: only the critic's `water.txt` (hero at 100, Circle 1, Square 42) is a test. The reading's `bomb_thrown_in_water.txt` (hero put at 178, walking 3 ticks) is not: the oracle's hero walks 3.0 px there (181.0 at the tick 50) and the DLL's 6.1 (184.1), a difference of the hero's walk from a posed position, not of the bomb.
4. **(w8)** and **(w11)** do not compare the invulnerability and blink counters of the wound (`+0x1D0`, `+0x1CC`, in the key's rows): the DLL names them differently and they are the wound's (E20.c2b, already pinned). They compare everything else of every row.
5. **(w1)**: the hit sparks are compared by the centre the seam hands over (one set of three) and by the count of the effects 9 made at the tick 260 (3), not by the words of the effects after their first update; the drawn values of those three sparks (the only line of `bomb_timeline` that the per-frame step changes) are the wound's, not C62's, and are not compared.
6. **(w5)** poses the lock (`GameState.PlayerControlFlags = ForcedSequence`, the word `0x20`) at the head of the tick 61, as the key's `poke` between the ticks 60 and 61. **(w9)** presses Start in the run that ends at that tick (five runs) and reads `InventoryDirector.Instance.IsActive`.
7. The Z of the bomb is compared where the key prints it (32.0 carried, 31.5 and 30.5 let go, 0.0 at the explosion, 63.5 / 36.0 / 27.5 in the flight): the same numbers here as in the key (the key's rest is 0, the DLL's rest is 0).

## Flags for the main session

- T3 cannot make (w1), (w2), (w8) and (w11) green without resolving the two causes above (the birth tick; the sub-pixel). Neither is in the plan's T3 list ("les entrées `0x23` et l'inscription"). Whether the birth tick is a one-line posing in `BombAi.Start` and the sub-pixel a position write after the birth (or a root that keeps the fraction) is for the executor of T3 to establish; if the second touches the engine (`CasaEngineMonogame/`) or the spawn of all entities, it is a fork of the plan.
- (w10) pins Q-H3B-1 (default A): the south throw hurts the hero who stays; the east throw must not (it does while the sub-pixel is lost).
