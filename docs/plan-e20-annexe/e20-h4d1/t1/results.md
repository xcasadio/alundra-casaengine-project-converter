# E20.h4d1 T1 - results (phase A, tests first, inert in production)

Branch `chantier/theme2-attaques-objets`, 2026-10-28. Plan: `docs/plan-e20-heros.md` section 1.31.5 T1 and section 1.31 (R-EL-1 to R-EL-7). Starting point: T0 (`ed4565bc`). Two commits: the tests with a compiling skeleton (`0be8ffda`), then the code.

## 1. What was written

| Plan test | Test | File |
|---|---|---|
| (e1) E28 against the vectors | `FireTome2AiTests.E1` (203 vectors: the 202 of the discovery and the seed `0xA468A517` of the faithful key; effects, generator, destruction) | `Alundra.Tests/Ai/Items/FireTome2AiTests.cs` |
| (e2) full reservoir | `E2` (8 draws, 0 effect, the destruction) | same |
| (e3) `BreakEffect = 8` | `E3` in the REAL world of map 14: the attached effect 8 on the entity that has it, none on the real sprite 21 (its header has `BreakEffect` 0, asserted) | same |
| (e4) exactly 8 draws | `E4` (four seeds) | same |
| (e5) registration `(4, 28)` for the type 21 alone | `E5` on a registry of its own (`TryGet(4, 28, 0x115)` false) | same |
| (e6) the tables by hand | `E6` (`OffsetXList[4k]`, `OffsetYList[4k]` as literals; the radii recomputed by hand from the LCG) | same |
| (b1), (b2), (b3) the buffer | `CameraSwayBufferTests.B1`, `B2`, `B3` | `Alundra.Tests/Cameras/CameraSwayBufferTests.cs` |
| (d1) the pilot against the 61 vectors | `D1` (60 vectors of the pilot, replayed field by field: answer, phase, animation, item, pointer, block, buffer, births, statuses and blockers; plus the cancellation vector of the critic) | `FireTome2AiTests.cs` |
| (d2) phase >= 3 or negative | `D2` | same |
| (d3) refused birth | `D3` (and `D3b` for the wait for the animation `0x33` and the arguments of the birth) | same |
| (d4) capture before the write of `Flag` | `D4` (preset block and zero block, counter 32) | same |
| (d5) not recycled = alive | `D5` (`FlagToDestroy`, `Normal`, `Deactivated`, `Loaded` are alive; `Destroyed` or `Slot < 0` ends the burning; phase 2) | same |
| (d6) the steps | `D6` (six counters) | same |
| (c1) the cancellation | `C1` (type 21 at the statuses 0 to 4, the types 20, 22, 23, 24, 17, and the entity of the map of index 21 = type `0x115` intact; the buffer restored with no guard) | same |
| (c2) the guard in phase 1 | `C2` (slope 4, animations 0x31/0x34/0, item `0x30`, `0x2B` (neutral) and 0) | same |
| (w1) the whole cast | `FireTome2WorldTests.W1` against `fire2_cast_pf` (200 ticks) | `Alundra.Tests/Ai/Items/FireTome2WorldTests.cs` |
| (w2) hurt at 60 | `W2` (1, 2 and 3 ticks a frame) | same |
| (w3) hurt at 20 | `W3` (`fire2_hurt20`, `_zerobuf`, `_stale`) | same |
| (w4) the preset with zero limits | `W4` (`fire2_cast_preset_limit0_pf`, nine words from the tick 155, Reach included) and `W4b` (`fire2_cast_preset_block`, five words) | same |
| (w5) hurt at 170 | `W5` | same |
| (w6) the targets | `W6` (`fire2_targets_pf`, `fire2_targets_edge_pf`: the hit ticks of every target, the hero never touched, the generator) and `W6b` (the pairs of the edge and the growth of the box) | same |
| (w7) 2 and 3 ticks a frame | `W7`, `W7b` | same |

Support: `FireTome2Run` (the bench, R-EL-4), `FireTome2Key` (the faithful keys and the target keys), `FireTome2Compare`, `FireTome2Vectors`; `AnnexFiles.FireTome2Annex`; `CutBench.CutHost.CameraSway` (null by default; the pilot tests put a fresh `CameraSway`).

Code: `Alundra/Ai/Items/FireTome2Ai.cs` (new: `Driver`, `Cancel`, `ColumnEnds` = E28, `Register`, `Keys`, `SpriteTypes`), `CameraSway.cs` (`Buffer`, `CaptureToBuffer`, `RestoreFromBuffer`, `ResetForTests` clears the buffer; `OnMapLoad` does not touch it), `EntityScriptProxy.cs` (`ItemSpawnedEntity`), `NativeEffects.cs` (`FireColumnBlastEffect`, `CreateFireColumnBlast`). Nothing is wired: `PlayerManager` and `NativeSpriteRegistry` are untouched (the phase B is T2); `check_names.py --rules`: 0 finding.

## 2. Red before, green after

Against the skeleton (bodies throw `NotImplementedException`, the buffer is not cleared by `ResetForTests`): 40 tests, 39 red, 1 green (`E5`, the registration, which the skeleton already carries: a witness). `red_before_the_code.txt` lists the red ones. After the code: 40 of 40 green, Release and Debug. The suite: `suite.summary` (7 825 tests, 0 failure in Release and in Debug; 7 785 of T0 + 40; no existing test moved, the predicted list for the phase A being empty: the five sums of the keys of the registry are raised by the phase B only).

## 3. Findings and deviations (none is a stop of the plan)

1. **Tests corrected after the test commit** (they were wrong about the bench, not about the code; they are in the code commit): (a) the newborn of the test host (`CutHost`) is a bare proxy with `Slot` -1 and `Status` `Destroyed`, hence "changed" for the identity test of D-H4D1-3: the pilot tests give it a slot and a status of the living at its birth (`OnBirth`), as the plan's mapping says ("`Slot` posé pour « vivant »"); (b) `W2` at 2 and 3 ticks a frame compared the POSITIONS of the eight effects with the key's: the state of the generator at the tick 60 depends on the cadence (the step at the end of the frame), so the positions are compared at one tick a frame only (the plan's (w7) asks for the count of effects and draws); (c) `W4b` (`fire2_cast_preset_block`, non-zero limits): the DLL runs the step of the shake from the tick 1, so the buffer taken at the tick 34 holds the DLL's moved offsets and reach: the buffer is compared by its five words (R-EL-7), the nine words only for the key with zero limits (`W4`).
2. The pointer of the column that is **null at the phase 1** (not in the 60 vectors: a phase 1 without a pointer is unreachable, the birth writes it) is treated as "changed" (the burning ends); the plan is silent. Noted, not tested.
3. E28's explosions are queued on the effect stage (`EffectStage.Emit`) like the other native effects of the DLL, the draws being made at once: a full reservoir keeps the eight draws (`E2`). The plan's D-H4D1-4 says "un créateur nommé de `NativeEffects` qui appelle `CreatedForTests`, sur le modèle de `CreateSprintBounce`", which this is.
4. The 8 effects of the key are compared at the tick 154 in the world (`W1`) by position, effect, animation and Z = the binary's Z (2); they come out in the order of the key.
5. `CameraSwayBufferTests.B3` also checks the shared instance (`CameraSway.Instance.ResetForTests` clears the buffer), since `ArcRun.ResetAll` relies on it.
