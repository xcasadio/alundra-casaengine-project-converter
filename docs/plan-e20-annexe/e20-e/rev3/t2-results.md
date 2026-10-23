# E20.e, T2 (world tests, red before T3): measured results (2026-10-23)

Start HEAD `ddd81aff` (phase A), branch `chantier/theme1-porter-plateforme`. T2 adds tests and oracle scenarios only; no production code.

## The oracle (two scenarios added, the rest byte-identical)

`oracle/cut_m14.py` gained two scenarios, run from a copy: `beside` (basic sword, hero (744, 696) facing EAST, away from the bushes of the cells 26 to 29, rows 40 to 44) and `idle` (no press, hero (732, 696) facing west, the place where `sword_none` cuts 4 bushes).
The six scenarios of T0 are equal (JSON `==`) to the ones versioned at T0, the seeds too. `beside`: 0 cuts, the swing state 0x10 at tick 1 and 0 at tick 22, sounds 29 and 0 only. `idle`: 0 cuts, the state 0 throughout. Both generator sequences equal `nograss`'s
(`rng_by_tick` begins 2472470177, 1020151012, 2812641211: no draw outside a cut and a spark). The files `oracle/out/cut_m14.json` and `.txt` are regenerated (LF), `SHA1SUMS.txt` updated.

## The tests (`Alundra.Tests/Cells/TileEffectCutWorldTests.cs`, 10 tests; `Alundra.Tests/ExportGuards/TileCutEffectIdExportGuardTests.cs`, 3 cases)

Arena: `HitArena` on the real map 14 (hero on the ground of the cells there, `heroPixelZ` 16; 0 far from the grass), no target, every other entity removed (`FlagToDestroy` then `RecycleDestroyedEntities()`), `NativeHandlersEnabled = true`, a sound recorder
injected, `NativeEffects.CreatedForTests` for the effect, the generator set to the oracle's seed at the head of the tick 1. Everything expected is READ from `cut_m14.json` (the tick, the cell, the effect id, X, Y, Z, the sounds 31 and 84, the generator word at the head of every tick, the cells,
the birth and the Z of the object); nothing is taken from the DLL.

| test | scenario | what it asserts (all from the oracle) |
|---|---|---|
| W1 | `sword_none` | 4 cuts at ticks 2, 6, 8, 8: effects (global effect 2, X, Y, Z), sounds 31, cells (bit 0x02 and stack gone, the others untouched), head word of every tick, one overlay reconstruction per frame that cut a stacked cell |
| W2 | `wave_none` | 6 cuts (hero 2, 8; wave 123 x2, 127 x2): the same, over 150 ticks, with the 3 hold sparks that draw 6 words |
| W3 | `nograss`, `beside` | 0 cuts, no effect, no sound 31/84, head words equal, no reconstruction, the cuttable cells unchanged (green twins) |
| W4 | `idle` | 0 cuts at the place where W1 cuts; the hero stays in state 0 (green twin) |
| W5 | `sword_none` twice | the bushes are cut in the first visit; a new world has them back (bit, stack) |
| W6 sword, W6 wave | `sword_obj`, `wave_obj` | the object is born at the cut cell (type 100, 114; X, Y), sounds 84 then 31 at the tick of the cut, the head words (2 draws for the object), the Z of the object at every tick of the oracle (DLL Z = binary Z - 1, ADR-0026) |
| W7 | `nostack_none` | the cell (27, 45) without a stack: effect, loot, sound, and 0 reconstruction at its tick |
| W8 | `sword_none` | the real exported property `TileCutEffectId = "2"` (no `BalanceLevel`), the world's index 2, the emitted effect is the global 2 |
| guard | maps 10, 14, 16 | the exported `.tileMap` carries `TileCutEffectId = "2"` and no `BalanceLevel` |

Red before T3 (names in `t2-red-names.txt`): W1, W2, W5, W6 (x2), W7, W8 = 7 red; green: W3 (x2), W4, the guard (x3) = 6. The red ones fail on the first assertion about what the cut produces (no effect, no sound, no cell cut); the green ones are the "nothing is cut" twins, each next to a red positive case
on the same place, and the guard of the export (E20.eR is done).

## Harness findings (not defects of the DLL)

1. The hero's counter `+0x1C` (`EntityScriptProxy.FrameCounter`) sets the phase of the hold sparks (every 4 ticks, the sound 428 or 429 by bit 2). The arena's hero has run 10 settling ticks and reads 10 at the head of the tick 1, so its sparks fall at the ticks 83, 87, 91, 95; the oracle's
   fall at 85, 89, 93 (sounds 428, 429, 428), a counter of 96 at the tick 85. The charged scenarios set the counter to 12 at the head of the tick 1 (`OracleHeroFrameCounterAtTick1`); that aligns the phase and nothing else. Without it the head words diverge from the tick 84 (the sparks draw 2 words each).
2. The effect is created by the stage at the end of the frame (ADR-0077), the sound at once: the tests compare the effects and the sounds as two lists, as the plan says (S-E1), not interleaved as the oracle's journal.
3. Measured with the two calls of T3 wired in a throwaway working tree (reverted for the red run): the 10 world tests are green, so the expected values are reachable by the port; the object's Z at the ticks of the oracle matches for the wave too (no one-tick gap of Q-E0C-8 shows at the object's Z).

## Deviation from the plan

The tests of the cut by the hero and the wave are in a new file `TileEffectCutWorldTests.cs` (class `TileEffectCutWorldTests`, in the collection of the music singleton like the other arena tests) and not in `TileEffectWorldTests.cs`: that class has no collection and no
reset of the static seeds, and the arena tests need both. `TileEffectWorldTests.cs` keeps its six T1 tests.
