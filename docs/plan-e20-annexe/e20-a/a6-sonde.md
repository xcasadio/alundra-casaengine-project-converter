# E20.a A6: probe of the first frames in the real game

Date: 2026-10-09. Method of E19.v2: the real game (`CasaEngineGame` on `alundra-project/AlundraGame.json`, which loads the Debug-deployed `alundra-project/Alundra.dll`, byte-identical
to `Alundra/bin/Debug/net9.0-windows/Alundra.dll` at the time) run by a throwaway harness kept outside the repository (scratchpad `probe-src`, `probe-run`). It logs the hero each frame
for 220 frames: target and current animation, `IsOnGround`, `HeroAirborne`, `PosZ`, the controller's ground contact, the control flags.

| Case | Result |
|---|---|
| **New Game** (map 389) | the control is locked from frame 1 (`PlayerControlFlags` 0x4); target and current animation `0x36` (LoadingMap) for the first frames, then `0x0` (Idle) when its chain ends; PosZ 80 from frame 2; no other animation in 220 frames, so no jump pose (0x2B, 0x2C, 0x2D) and Alundra is still |
| **Arrival** (a pending arrival record as `0x52`/`0x53` leave it, the same map requested, the map's own program locking the control at its first tick), arrival animation `0x36` | locked (0x4) from the first frame of the new hero; `0x36` then `0x0`; no jump pose |
| same, arrival animation `0x0` (Idle) | `0x0` for 200 frames, locked from the first frame |
| same, arrival animation `0x1` (Moving) | `0x1` for 200 frames, locked from the first frame (the arrival animation is kept while locked, as the binary leaves it) |

The first `MovePlayer` of a map now runs after the map events of the first tick, so it sees the lock (the V2 mechanism of E19.v2 is replaced by the binary's order). The case of
O-E19-80 (a fresh pawn on an arrival, Idle or Moving, reading a ground contact of false in the common tail) did not show: the lock is in place before the hero's first tick.
**The floor rule (`PosZ > floor`) is therefore not applied to the common tail.** Open: an arrival NOT followed by a lock (a map whose entry program does not lock) was not probed.
