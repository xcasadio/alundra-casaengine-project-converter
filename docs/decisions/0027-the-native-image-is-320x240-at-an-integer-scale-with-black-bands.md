# ADR-0027: The native image is 320x240 at an integer scale with black bands

- **Status**: Accepted
- **Date**: 2026-10-03 (decisions D-E19-47 and D-E19-60 of the author, 2026-10-03)
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2q E19.s (rules S-R1 to S-R6), D-E19-47, D-E19-60. Engine side: engine ADR-0048 (`CasaEngineMonogame/docs/decisions/0048-virtual-resolution-with-integer-fit-and-black-bands.md`).

## Context

- The original's screen is 320x240: `ALUN_CD.EXE` (France) calls `SetDefDrawEnv` (`0x80042504`) and `SetDefDispEnv` (`0x8004251C`) with `0x140` x `0xF0` in `0x800424AC`, and sizes both full-screen TILEs `0x140` x `0xF0` (`0x800429C8`-`0x800429EC`). No 236 appears in the draw code.
- The 236 of the decompilation (`StaticVariables.ScreenHeight = 236; //224`) is wrong against the binary: the same decompilation writes 236 into the TILE at `0x8013FB98` where the binary writes `0xF0`. It entered this repository with `5c7fdad` (converter, `AlundraDisplay.NativeHeight`) and `1507afc` (the DLL's zoom, `944 / 236 = 4`), which made the default window 1280x944.
- The image followed the window badly: the engine's view and camera kept the size they had when the world loaded, because on DesktopGL a user resize only raises `ClientSizeChanged`; and the DLL set the zoom once per world from the window height alone, so a window wider than 320:236 showed more than 320 world units (the halo of the Lars and Melzas scene, map 476, ended smaller than the window).
- The four full-screen screens (HUD, inventory, sub-inventory, save) took `max(1, width / 320)` once, when their window was built.

## Decision

- The native image is 320x240 everywhere: converter (`AlundraDisplay.NativeHeight` 240, default window 1280x960, camera zoom placeholder 4), DLL, documents. The binary wins over the decompilation.
- The image is enlarged at a whole factor only, `k = max(1, floor(min(L / 320, H / 240)))`, centered, the rest of the window black bands, and recalculated in real time when the window changes (D-E19-47, D-E19-60). Every pixel keeps the same size.
- The rule lives in the engine (engine ADR-0048). The converter only declares the project setting `VirtualResolution` (`Width` 320, `Height` 240, `Mode` `IntegerFit`) in `AlundraGame.json`; the window size written there is the starting state.
- The DLL no longer poses the camera zoom (`CameraDisplayHeight` and `ComputeCameraZoom` are removed; `AlundraCameraDirector` keeps `PixelSnap`). `CameraVisibleWidth` and `CameraVisibleHeight` stay 320 and 240 (clamp, backdrops).
- The four screens redo their window and integer scale from the engine's new `OnScreenBoundsChanged` callback; they compute exactly what their `OnWindowLoaded` computes (the view is `320 k` wide, so the scale is `k`).
- The window always opens at the project size (1280x960); nothing is remembered between runs (S-R6).

## Consequences

- The visible world is exactly 320x240 units whatever the window, so the backdrops, the cellular layers and the fades (world-space) fill the whole image; the four rows lost by the old 236 come back; the halo of the 476 covers the whole image.
- The old note about a 1.7 % aspect difference with a CRT no longer applies: 320x240 is exactly 4:3 at square pixels.
- Bands are wider than an exact fit would give (1920x1080 gives x4 and bands of 320 and 60 pixels); accepted by the author.
- The engine's temporary dialogue box keeps window-pixel sizes inside the view until E19.f replaces it.
- A DLL from before E19.s stays compatible with an export from E19.s (the setting is read only by the engine); rolling back is a re-export with the previous converter.
