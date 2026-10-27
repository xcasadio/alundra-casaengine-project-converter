# E20.h4c2 T1 (phase A) - results (annex)

Run at the HEAD `4c7d7a47` (branch `chantier/theme2-attaques-objets`, 2026-10-27), after E20.e0a6 (`aa63f92e`). Plan: `docs/plan-e20-heros.md` section 1.31.4 (revision 3.2, READY), T1; D-H4C2-1 to D-H4C2-5. Two commits: the tests with a compiling skeleton
(`e92ce64e`), then the code. Nothing under `CasaEngineMonogame/`, `alundra-datas-analyser/`, `alundra-project/` (but the Debug DLL deposit, which git ignores), `data-extracted/`, `tools/` was changed. The phase A is INERT in production: `EarthTome2Ai` has no caller outside
the tests (the entries `0x2C` of the three seams of the item in use in `PlayerManager` and the call in `NativeSpriteRegistry.CreateForProduction` are the phase B, T2); the camera kick is written by nobody in production (only E13 of the tome writes it).

## 0. (s2) replayed first (D-H4C2-5 (1))

The probe `t0/probes/H4c2T0ProbeTests.cs.txt` was copied into `Alundra.Tests/Probes/` for the run and removed (not committed); 15 tests green; `t0/probes/cmp_probe.py` on its rows: **`s2 HOLDS`** (and s1, s3 to s12): the x, y, Z, terrain word, status, slot of the 15 births of `mid`, `edge`, `water`
(14 of `s9a`, `hb16`, `hb17`) equal the keys. The supplementary comparison of the rocks with E13 against the keys: `mid`, `edge`, `water`: field differences `{}`. The output is `s2-replay/cmp_probe.out`. E20.e0a6 fixed the cause (13 births of 15 lost the fraction of the pixel).

## 1. What was written

| File | Content |
|---|---|
| `Alundra/Ai/Items/EarthTome2Ai.cs` (new) | `SpriteTypes = {17}`, `ImpactCode = 13`, `Keys`, `TableBytes` (the 64 bytes of `0x80023504`) and `Table` (u16 tick, s16 p), `Driver(hero)` (the three phases; two draws before each birth, refused or not; `ShouldDestroyAtBirth`, the one-argument destroy, `ForceZ = 0xFFF80000`), `ShouldDestroyAtBirth(heroPosZ, terrain)` (pure: `unchecked(heroPosZ + 1 + 0x1000000) < terrain`), `ImpactDeactivate(entity)` = E13 with the guard of the ground (D-H4C2-3, DEVIATION from the binary, written in the class comment), `Cancel(hero)` = `0x80034BDC`, `Register(registry)` (E13 for the type 17 alone) |
| `Alundra/Maps/IEntityWorldContext.cs` | `void SetCameraKick(int scrollY)`, a default member that does nothing |
| `Alundra/Maps/WorldProxy.cs` | `SetCameraKick` (ASSIGNS at the index of the tick in progress, padded with zeros), `_cameraKickOfTick`, `_cameraKickTick` (set at the head of `RunEntityBlock`, 0 after the tick loop of `Update`), the list passed to `UpdateCameraFollow` and **emptied after it** (not at the head of the frame), `CameraKickOfTickForTests` |
| `Alundra/Cameras/CameraMath.cs`, `CameraDirector.cs` | the optional final parameter `IReadOnlyList<int>? cameraKickOfTick` of `AdvanceCameraSmoothing` and `UpdateCameraFollow`: the kick of the index `firstFullTick + tick` is added to the sway offset Y (render `Y -= kick`) at each full tick; the snap tick has none; during a warp transition `CameraDirector` passes none |
| `Alundra.Tests/Ai/Items/EarthTome2AiTests.cs` | (t1), (t2) x2, (t3), (t4), (t5), (t6), (e1) to (e7), (c1), (r1): 16 tests |
| `Alundra.Tests/Ai/Items/EarthTome2WorldTests.cs` | (w1), (w2), (w3) x2, (w3b) x2, (w4) x3, (w5), (w6) x2, (w8) x2, (s10b) x3: 17 test cases |
| `Alundra.Tests/Ai/Items/EarthTome2Run.cs`, `EarthTome2Compare.cs`, `EarthTome2Key.cs`, `EarthTome2Vectors.cs` | the bench (`HitArena` + the synthetic field, `ticksPerFrame` ticks per frame, the three seams of h0a, E13 registered on the runner's registry only if its key is absent), the field-by-field comparison, the key / cancel-key loaders, the loaders of the 609 cases of the driver and of the h4b vectors read by address |
| `Alundra.Tests/Cameras/CameraKickTests.cs` | (k1): the rest gaps d = 0, 1, 2, 15, 16, -1, the accumulation, the 3-tick frames (6 kick patterns against the binary's formula), the snap frame, short / long lists, the bounds, no list; the 100 vectors of `vec_camera_8002CDA0.json`: 22 "binary only" skipped by their flag, **78 compared** (the scroll and the nine words of the sway block after the step): 11 test cases |
| `Alundra.Tests/Maps/WorldProxyCameraKickTests.cs` | (k2) the driven world (the index of the tick, two E13 at the same tick give 2, the list empty after the camera step); (k3) a world with no driver (the kick at the index 0 applied by the camera step of the same image, a 2-tick image, a warp transition, no camera): 5 tests |
| `Alundra.Tests/Support/AnnexFiles.cs`, `CutBench.cs` | `EarthTome2Annex`, `H4bVector`; `CutHost.RefuseCall` (refuses the call whose index the function accepts) and `CutHost.CameraKicks` / `SetCameraKick` |
| `t1/` | `dump_table.py` (reads the table from the France executable, SHA-1 checked), `table_80023504.hex` / `.txt`, `camera_kick_model.py` (the independent model of the binary's camera step that the expected sequences of (k1) come from), `s2-replay/`, `red_before_the_code.txt`, this file |

The expected values: the table of the executable; the 604 cases of `disc/keys/driver_2c_cases.json` whose terrain under the newborn is representable by a field (`lo`; the 5 cases `eq` (2) and `lt` (3) are excluded by their name and covered by (t4) and (w8)); the 40 vectors of E13 (20 first
passes replayed ON THE GROUND, 20 second passes); the keys `critic/keys/cast_2c_pf_mid_x600y480` (w1), `rockfix/cast_2c_pf_rockfix` (w2, w6, s10b), `disc/keys/cancel_2c_pf_t60`, `_t100` and `rockfix/cancel_2c_pf_t170_rockfix` with their sound journals (w4), `rockfix/cast_2c_pf_plateau_hb16_rockfix` /
`_hb17_rockfix` (w8, s10b), `disc/keys/targets_2c_pf_inv0` (w5); the 78 camera vectors.

## 2. Red before, green after

- Tests commit `e92ce64e` against the skeleton (every function inert, `Register` registers nothing, empty table, the kick member and the optional parameter ignored): **52 of 59 test cases red** (`red_before_the_code.txt`), **7 green witnesses** that are green before and after by design: `E4` (a cancelled
  rock gives neither kick nor flags), `K1_NoKick...`, `K1_WithoutAListOrWithZeros...`, the 3-tick pattern (0, 0, 0), `K3_DuringAWarpTransition...`, `K3_AWorldWithNoCamera...`, `W6_AnEntityMarkedWithGravity...` (f6a, the witness of the rocks that do not drown).
- With the code: **59 of 59 green**, in Release (copy neutralised, `-p:CustomAfterMicrosoftCommonTargets=skipcopy.targets`) and in Debug.
- **No existing test moved**: the starting suite of 7 721 tests (E20.e0a6 included) is 7 721 + 59 = **7 780 tests, zero failure**, in Release (A `Ai.Projectiles` 332, B the other `Ai`, `Arcs`, `Audio`, `Cameras`, `Cells`, `Combat`, `Dialogues` 2 065, C `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` 3 341, D the rest 2 042) and in Debug (the same four chunks, same counts,
  built last). The five sums of keys of the production registry are unchanged (the phase B raises them). The traces `docs/hero-trace-389-*.txt` were rewritten by the suites (end of lines only: `git diff --ignore-cr-at-eol --quiet -- docs/` true) and restored.
- `python -B tools/dll-structure/check_names.py --rules`: 0 finding (the one warning is old: `TextBoxGpu.cs`).
- The mutants are T3's (at least 32); not run here.

## 3. What the world tests found (the bench vs the keys)

(w1), (w2), the water band (w6), the 3 interruptions (w4) and both plateaus (w8) are **equal to the keys, every tick, every field** (status, animation asked, Z + 1, end of animation, flags, ForceAdjusted, ground contact, X and Y, the kicks, the sounds, the generator at the head of each tick and its
steps, the hero, the item, the magic) except the points below. The rock of the edge (id 11) is exact on each axis (bound 0, `S10b`).

- **(s10b), measured**: the rock 8 against the wall of the plateau, at its landing (tick 146): DLL x 10 289 152, y 4 968 832; key (corrected game) x 10 289 152, y 4 719 956: **difference (0, 248 876) raw units (3.8 px)**, the same at the byte 16 and at the byte 17; the largest difference over its life is the same 248 876; the bound of (s10a) is 715 680 on each axis. The rock 11 (the
  edge): DLL (655 360, 9 204 160) = key, exactly.
- **A rock destroyed on the spot** (the byte 17: the rocks 9, 13, 14, born at 130, 194, 226): the key's row at the birth tick reads `ground = 1` (the word of the slot template, the rock is destroyed before its first physics pass), the DLL's `IsOnGround` is 0. Only that field of that row differs;
  nothing reads it afterwards (the slot is freed). The comparison skips the ground contact of a row whose status is 4 at the birth tick (`EarthTome2Compare.Rocks`). Deviation from the plan text "sans tolérance", disposed here and to be confirmed by the main session.
- **(w2)**: the plan says the kick and the sound of the edge rock move from 163 to 195. At 163 the kick and one sound 45 are still there, those of the interior rock born at 130 (130 + 33): the test counts them (one sound 45 at 163, one at 195, a kick of 2 at both).
- **(w3b)** runs with the hero at x = 40 (not 100): at 3 ticks per frame the draws no longer put a rock outside the grid with the hero at x = 100 (the set would be empty, the test vacuous); close to the west edge several rocks are clamped in the air at both rates. In the air the animation asked of a
  rock is the fall's (2), never the impact's 1: the test says "not 1".
- **(w5)** (non-blocking) is green: six blocks of the global sprite 25 spawned at the key's pixels, `Flags |= ClassA` (the class the rocks hit), 5 000 HP; the touch of a target is the tick at which its cooldown reads 25. **The key's tick is the DLL's plus 1**: the oracle's harness records a touch the tick
  after the hit pass wrote it, as (s8) states for the nine draws (the key's own `rng` shows them at 44, 69, 94, ... for the first hits recorded at 45, 70, 95, ...). The test subtracts 1 from the key.
- **The camera vectors**: the 78 compared vectors reproduce the binary's scroll and the nine words of the sway block, kicks of -2, 2 and 7 included (25 compared vectors have a non-zero kick Y, 53 none; 35 are snaps, none has both a kick and a snap: the loss of the kick at the snap is carried by the pure test
  `K1_ASnapFrame...`); the 22 vectors with a kick X (ids 3, 4, 8, 12, 16, 24, 32, 34, 39, 40, 44, 45, 47, 48, 57, 64, 69, 72, 74, 83, 87, 91) are skipped by their flag, as D-H4C2-5 (2) says. **O-H4C2-1 stays open**: the writers of `[0x800DD870]` in the binary and its member in the DLL.
- **(t2)** has two parts: the 604 cases replayed on the direct driver (answer, phase, animation, generator, item, births with the Z of the case minus 1, destruction, `ForceZ`, the entities of the case after the guard's cancellation), and the 37 distinct (phase, counter, animation) combinations with the slope 0 through
  the real tick (`TomeBench`) for the counter (saturated) and the release by the wrapper. A "pool" entry of the cases is the stub's entity prepared for every accepted birth: the unused ones stay at (status 1, force 0), which the test asserts.
- **Not decided here**: nothing was changed in the engine, the converter or the data; `Alundra/Ai/Items/EarthTome2Ai.cs` registers nothing by itself; no ADR (T3); the production entries and (p1) to (p4) are T2.
