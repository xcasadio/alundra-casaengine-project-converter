# E20.h3b T1, phase A: C62, the start, the driver and the cancellation of the bomb (2026-10-26)

Branch `chantier/theme2-attaques-objets`, HEAD of departure `f266940b` (T0). Two commits: the tests with a compiling skeleton (`c23400be`, every test red), then the code (this commit). **No production path calls the new functions**: nothing is registered, no table entry `0x23` exists yet (phase B, T3).

## What was written

- `Alundra/Ai/Items/BombAi.cs`: `Start(hero)` (`0x8003453C`), `Drive(hero)` (`0x800352C4`), `Cancel(hero)` (`0x80034B54`), `Tick(bomb, context, readCorners)` (C62, `0x8007A978`), the fuse accessors `Fuse` / `SetFuse` (`+0x274` on `EntityScriptProxy.Bytes`), the constant `TickCode = 62`. **`BombAi.Keys` and `BombAi.Register` are NOT touched**: C62 joins them at T3 (the inscription is the phase B; registering it now would put it in `CreateForProduction`, which `BombAi.Register` feeds, and the six benches of h3a would go red at T1). The T0 choice (C62 in `BombAi.Keys`, so the five sums follow with no edit) stands for T3.
- `Alundra/Hero/PlayerManager.cs`: `CancelItemMode` is `internal` instead of `private` (one word). The driver on the slope 4 calls the dispatcher `0x800350C0` (the cancellation of the item in use, then the item freed), which is `CancelItemMode`; it goes through the seam `ItemCancelForTests`, so T3 can wire `BombAi.Cancel` into the table without touching the driver.
- Tests (`Alundra.Tests/Ai/Items/`): `BombHandsTests.cs` (`H1_*` the start, `H2_*` the driver, `H3_*` the cancellation), `BombTickTests.cs` (`H4_*` C62), the bench `BombHandsBench.cs`; `BombVector.cs` gains `SpawnReturn`, `Return` and a null `self`; `Support/CutBench.cs` records the single-argument `DestroyEntity` (`CutHost.DestroyedAlone`, it was a no-op).

## Oracle: the vectors replayed

The 25 vectors of each function of `docs/plan-e20-annexe/e20-h3/critic/results/goldens_critic.json` (identical to the T0 replay), field by field: `H1_Start_TheTwentyFiveVectors...` (18 refusals with the sound 3, 3 null births with no sound, 4 starts), `H2_Drive_TheTwentyFiveVectors...` (24 answers 1, one 0), `H3_Cancel_TheTwentyFiveVectors...` (2 cancellations with the effect 12 and the one-argument destruction, 23 untouched), `H4_C62_TheTwentyFiveVectors...` (16 holes destroyed, 3 sparks with their two draws and the generator word, 3 sounds `0x18`, 2 releases of the carrier). The spark forces and the generator word after the call are compared with the words the real code wrote.

Hand cases next to the vectors (each negative case beside a positive one): the three refusals and the item in use (even the bomb itself), the slopes other than 4, the limit of three for every status (a destroyed bomb counts until its slot is recycled, other types do not), a null birth, the fuse `0xF0` after the birth, the links and the offset, the lift animation 5 / `0x0B`, the driver on every slope, the cancellation and its D1 (`+0x24`, not `+0x20`), the fuse (hole before the decrement, 241 ticks to the status 3), the spark condition for fuses from -24 to 70 (the negative multiples of 8 too, never 0), a spark not obtained (full reservoir, no stage) draws nothing, the carried bomb with each code of throw (flags untouched), the single bounce in the air, the explosion where it stands at the foot of a wall on the ground, the sound `0x18`.

## Red before, green after

54 tests (named in the two files, `H1_` to `H4_`): **54 red** at `c23400be` (the skeleton throws `NotImplementedException`), **54 green** after the code. `dotnet test --filter "BombHandsTests|BombTickTests"`: 54 / 54 in Release, and the namespace `Alundra.Tests.Ai.Items.` (174 tests) green in Debug.

## Existing tests moved: none

Release with the neutralised copy (`-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`, build and test), `--no-build --blame-hang-timeout 60s`, partitions disjoint by whole namespace (the driver is `run_part.sh` of g2c3, in the scratchpad; the whole suite does not fit one 10 minute call):

| Partition | Tests | Red |
|---|---|---|
| `Ai.` | 1215 | 0 |
| `Hero.Hero*` | 1299 | 0 |
| `Hero.` apart from `Hero.Hero*` | 550 | 0 |
| `Arcs. Audio. Cameras. Cells. Combat. Dialogues. Entities. Events.` | 2208 | 0 |
| everything else | 2187 | 0 |
| total | **7459** (= 7405 at the HEAD of T0 + the 54 new) | **0** |

No existing test moved or was rewritten (the registry is untouched: the five sums, the three `BombAiTests` rows, `LogicalZWorldTests` Z4 and Z7, `HeroItemUseTests.A4_`, the benches of h3a and the preset list move at T3 as the T0 list says). `python -B tools/dll-structure/check_names.py --rules`: 0 finding (1 pre-existing warning). The suites rewrote the line ends of the four `docs/hero-trace-389-*.txt`: `git diff --ignore-cr-at-eol --quiet -- docs/` was clean, then they were restored by `git checkout`. The Debug build came last.

## Decisions (local, inside the plan)

1. **C62 is not registered at T1** (see above). The test of the production registry that would say so is not written (it would have to be rewritten at T3).
2. **The signatures** are `Start(hero)`, `Drive(hero)`, `Cancel(hero)` (the item is implicit: `0x23`); T3 wraps them in the `(hero, item)` shapes of the seams of h0a.
3. **The effects' Z**: the spark and the smoke are created at the DLL's `PosZ + 1` (the binary's frame, ADR-0026), as `BallAi` and `HeroHeals` do; the spark is at `+ 0x80000`. The bounce spark of a wall in the air is created by `CreateNow` too, with no draw (the vectors have no bounce; T2 (w4) compares it). `C4Tick`'s bounce uses `PosZ + 0x80000` without the `+ 1`: a latent difference of the other slice, not touched.
4. **The spark policy of T0 (c)**: `EffectStage.CreateNow` (immediate, returns the slot) and two draws only when it returns a slot; the smoke (effect 12) goes through `Emit` (no draw, no handle needed).
5. **A seam in production code**: `Tick(..., readCorners)` takes the four corner words as `LiftableAi.C4Tick` does, default `CornerTileWords.Read`.
6. **Without a world** `Cancel` clears the hero's link and nothing else; `Start` returns 1 (no sound for the count, since there is no list to count). These two cases have no binary counterpart (the binary has one world); no production path reaches them.

## Flagged, not done

- The vectors of the driver (`drv_bomb`) never reach the slope 4: the branch that cancels is covered by hand tests (`H2_Drive_ABombCarriedIntoTheDeepWater4_...`) and by the pilot of T2 (w5)/(w6a).
- Mutants: left to T4 (the plan's list of 16).
