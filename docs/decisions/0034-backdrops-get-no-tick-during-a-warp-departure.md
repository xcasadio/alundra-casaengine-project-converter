# ADR-0034: The backdrops get no tick during a warp departure, like the binary

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.m4" (decision D-E19-67, taken by the session in AUTO mode under the author's rule "the binary decides"; open item O-E19-55, audit of 2026-10-03). It refines the push contract of decision D-E9b-2 of `docs/plan-e9b-backdrops-moteur.md` (line 378).

## Context

- The binary `ALUN_CD.EXE` (France) leaves its frame loop as soon as a departure is armed and runs a transition loop (`0x8002C490`-`0x8002C4C0`) that calls only the pad update, the effect tick, the sound streaming, the end of frame and an empty function: never `RenderScene` nor `Update`. The backdrop driver (`0x8005B670`) is reached only from `RenderScene` (`0x8002BE18`), so nothing of the backdrops advances during the departure (cadence, automatic scroll, cells, `rand()` draws, wave counter, palette program). The screen shows a copy of the last drawn frame under the fade tile.
- The DLL pushed the tick count to the engine services on every frame, from the arming frame F0 to the frame that ends the fade, F15: 16 backdrop ticks too many per departure (rain of map 391 falling during the departure to map 416, sea of maps 389 and 416, waves of maps 476 and 478), plus the matching extra `rand()` draws and, since E19.m3, 16 extra ticks of the global wave counter.
- A frame pushed with 0 ticks changes no state in `ScrollingLayerService` or `CellularLayerService` (verified in the engine code and by the tests of E19.m4); the arrival is already aligned (the arrival map's backdrop first ticks in its first `RenderScene`, at the same tick as the arrival fade's first step).

## Decision

- `AlundraWorldProxy.Update` pushes the backdrop frame with 0 ticks while `AlundraWarpDirector.Instance.IsTransitionInProgress` (the predicate that already gates the camera sway), and with `ticksThisFrame` otherwise. The frame is always pushed, so the camera target and the scroll stay current.
- No guard on the gameplay freeze (`gameplayBlocked`): under a dialogue or the inventory the binary's `RenderScene` keeps running, so the backdrops keep moving.
- Engine untouched.

## Consequences

- Exact at one tick per frame; on a catch-up frame that arms a departure at its k-th tick, the port can be up to 3 ticks short.
- The rain of map 391 stops during the departure to map 416; the session `rand()` stream (ADR-0032) no longer advances during departures.
- The rest of the scene (camera follow, entity animation, NPC physics, animated tiles, HUD, dialogue, music fade) still advances under the departure fade while the binary shows a frozen image: open item O-E19-57, not decided here.
