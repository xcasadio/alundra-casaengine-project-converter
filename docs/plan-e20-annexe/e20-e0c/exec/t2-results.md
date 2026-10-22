# E20.e0c, T2 (phase B): results (2026-10-22 / 2026-10-23)

Base: HEAD `9e43e464` (T1), branch `chantier/theme1-porter-plateforme`. Start of the suite: 5 153 tests in Release, all green (T1). Binary `ALUN_CD.EXE` (France).

## What is delivered

- `Alundra/Ai/Objects/PickupAi.cs`: `A255` (`0x80061BD4`, a named stub in this phase) and `C255` (`0x8007C174`) for the DROPPED object (`+0x274 == 1`): the bounce on the ground, the countdown of the life (600 ticks, the mark `+0x1CC = 0x78` at 120), the wait for the end of the rise
  (state 0 to 1 when the force is no longer positive), the contact with the hero (three axes, size + 1), `GiveItem`, `SetGameFlag(+0x280)`, the sound of the byte 5 of the table, the jump from the state 1 to 3, then (state 3) `Unblock(self)` and the light destruction once no dialogue is open.
  The placed object (`+0x274 == 0`, phase D) and the chest (`+0x274 == 2`, E14) are named stubs: both handlers do nothing for them. A key object (byte 5 zero), never dropped, is a named stub before `GiveItem` (phase D). The state 2 (the fanfare) is phase D.
- `Alundra/Ai/NativeSpriteRegistry.cs`: `PickupAi.Register` in `CreateForProduction`: (A, 255) and (C, 255) for the 87 sprite types 31 to 118 but 72 (not the chest, type 30: D-E0C-2). The registry goes from 7 to 9 keys.
- `Alundra/Maps/WorldProxy.cs` (`RunEntityBlock`): `ModdedPosX/Y/Z = Pos + Mod` for the active entities that nothing blocks (the hero first), between the animation clock and the physics (T0 (d): `0x800383C0-0x800383E0`). The contact reads the positions of the end of the previous tick.
- Tests: `Alundra.Tests/Ai/Objects/PickupAiDropWorldTests.cs` (56 tests: (w1) to (w9)), `Alundra.Tests/Ai/Objects/PickupAiDropVectorTests.cs` (15 tests: the 140 usable dropped vectors of `vectors/c255.json`, the chains of the bounces, the real-code bounds of the threshold, the key object), `Alundra.Tests/Support/PickupDropRun.cs` (the world rig: the real map 14 with the real prefabs and the catalogue, the production registry, cleared of every entity but the hero, the object dropped by `LootDrop.SpawnContents`, one sample per tick at the heads of the passes `programs`, `counters`, `inventory`), `FakeDialogueDirector.cs`, and three additive members of `PickupHost` (`Dialogue`, `Destroyed`, `Unblocked`).

`Alundra/Entities/EntityScriptProxy.cs`, the engine, the converter and the data: no diff.

## Red before, green after

- (w1) to (w9): the 56 world tests were written first against the unchanged code (a first draft of the tests: 46 red, 10 green; `t2-red-names-first-draft.txt`). The tests were then corrected (the pixel grid, the closing mask of the box, the gone status, the gravity words) and the red was redone on the FINAL tests by a REAL mutation applied by script and undone byte for byte: `PickupAi.Register` removed from `NativeSpriteRegistry.CreateForProduction`
  (`scripts/t2_mutants.py apply noregister`): **48 red** (`t2-red-names.txt`): the 46 world tests and the 2 pins. The 10 that stay green are the cases "just outside the bound" of (w2) (`beyond: 1`), each paired with a positive case that is red.
- Green after the code: the 56 world tests, the 15 vector tests, the 2 pins.
- The vector tests were written after the code; their teeth are shown by the mutants below (the threshold equality and the key object were added after two mutants survived, see "Mutants").

## Tests that moved (against the predicted list of T0 (e), phase B: 2)

| predicted | now | change |
|---|---|---|
| `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn` | `...ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` | `6 + 1` becomes `6 + 1 + PickupAi.Keys.Count` (derived), plus a `TryGet` for each key and each of the 87 types, and the chest (30) and 72 refused |
| `WaveSwordLifeTests.V3_TheProductionRegistryHoldsSevenKeys_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` | `...V3_TheProductionRegistryHoldsNineKeys_...` | `3 + 3 + 1` becomes `3 + 3 + 1 + PickupAi.Keys.Count` (derived) |

Exactly the 2 predicted tests, each renamed because "Seven" would be false. No other existing test moved: the refresh of `ModdedPos` moved none (T0 (e) said so: no reader). The suites are green.

## Suites

| suite | tests | failures |
|---|---|---|
| base (T1), Release, neutralised copy | 5 153 | 0 |
| after T2, Release, neutralised copy | 5 224 | 0 |
| after T2, Debug (last) | 5 224 | 0 |

5 224 = 5 153 + 71 new tests (56 + 15). `docs/hero-trace-389-*.txt` rewritten (line endings) by the suites: `git diff --ignore-cr-at-eol --quiet -- docs/` empty (rc 0), then restored. `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll`: identical.
`python -B tools/dll-structure/check_names.py --rules`: 0 finding, 1 warning (the one of the start: `TextBoxGpu.cs:266`).

## Mutants (real, by script, undone; `scripts/t2_mutants.py`, `scripts/t2_runmut.sh`, `t2-mutants.txt`)

21 mutants of the code of phase B, all killed: the registration removed; the threshold `<` for `<=`; the bounce without the reset to 0; the product by 13; the destruction at 1; the mark at 120 omitted; the no-rule test `-1` removed; the overlap `<=` on the hero side and on the object side; the pickup during a dialogue; the state 3 that does not wait for the
dialogue; the wait for the rise `>= 0`; the jump `+1` for `+2`; the sound omitted; the flag omitted; `GiveItem` omitted; `Unblock` omitted; the placed object that runs (`== 2` for `!= 1`); the refresh of `ModdedPos` removed; the registration for the chest; the key-object stub removed.
Two mutants SURVIVED the first set of tests and led to two tests: the threshold `<=` (the vectors of T0 never hit the equality; the REAL code was run on the bounds: `scripts/boundary_c255.py`, `replays/boundary_c255_out.txt`: a product equal to the threshold is lost, one above is kept) and the key-object stub.
`git diff` of the mutated files compared with the pristine copy after each: identical.

## Facts and deviations from the plan

1. **Gravity 3 in the world tests.** The maps 159 and 160 (Fairy cave, underwater, gravity 3, viscosity 256) do not run the entity block in a test: their entry keeps the gate of the gameplay closed (4 000 ticks, the object stays `Loaded`). (w1) at gravity 3 therefore runs in the world of the map 14 with the two words of those maps
   (`MapGravityRaw = 3`, `MapZViscosityRaw = 256`) on the dropped entity: the physics and C255 both read them from the entity. The chain of the 24 bounces of the maps 159 and 160 is read at the handler (`PickupAiDropVectorTests`).
2. **Bounds of the overlap in the world: the pixel.** A prefab keeps its position on the pixel grid (the root of the engine is in floats): an offset of one raw unit is lost. (w2) reads the bound at the pixel: `size + 1` (a whole number of pixels, out because the test is strict) is refused, one pixel inside it is taken, on X and Y from both sides and on Z from the hero's side. The raw-unit bounds
   (`size` and `size + 1`) are in the vectors of T0 (`c255.json` is random around them) and in the real-code bounds of the threshold. The Z axis from the object's side (the object under the ground the hero stands on) is not in the world tests: the physics lifts the object; it is in the vectors.
3. **The vectors left out**: 16 that start in the state 2 (phase D) and 10 of the objects `0x21` and `0x3D` (the vector forces the answer of `GiveItem` to 0, the real one answers 1 for objects without a life rule, and a dropped object that answers 1 has a message, which no drop produces). 140 vectors, 568 ticks, 26 pickups are compared word for word and event for event.
4. **A dropped object that `GiveItem` answers 1 for** (a message in the binary) is not ported in this phase: no object of the loot of the exported data answers 1 (T0 (g)); the guard is left to phase D, which writes the message path for the placed objects. A key object that is dropped (byte 5 zero) is a named stub for the same reason.
5. **(w9).** In both cases of slot (the object of a higher slot, of a lower slot) the program A of the object runs at the birth tick + 1 and the first C255 at + 2, on `IsOnGround` 0, the force positive, the life 600. This is T0 (b)/(c). The test of the higher slot carries the name of the gap of Q-E0C-8.
6. **The trailer** of the commits names the model of the session (`Claude Sonnet 5.5`), as T1 did; the task text said `Claude Opus 5.5`.
