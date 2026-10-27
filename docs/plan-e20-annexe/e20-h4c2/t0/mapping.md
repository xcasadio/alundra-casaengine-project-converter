# E20.h4c2 - T0 (b): the bytes read by the vectors of E13 and of the camera, and the members of the DLL that carry them (rule of D-H4B1-3)

Read at the starting HEAD `45a1b50a` (2026-10-27). The vectors: `docs/plan-e20-annexe/e20-h4b/bin/vec/vec_e13_8007F30C.json` (40), `vec_camera_8002CDA0.json` (100), `vec_sway_8002C894.json` (80); the reads and writes below are the DISTINCT
(address, size) pairs found in them by `t0/mapping_scan.py` (output `t0/mapping_scan.txt`). All words are 4 bytes. Entity bytes are given as offsets from the first argument of E13 (the entity).

## E13 `0x8007F30C` (40 vectors; 20 are first passes, 20 are second passes)

| Binary | Role | Read / written | Member of the DLL |
|---|---|---|---|
| `+0x6C` | the flags | r (20 vectors), w (20) | `EntityScriptProxy.Flags` (`uint`, `EntityScriptProxy.cs:161`) |
| `+0x88` | the animation asked | r (40), w (20) | `EntityScriptProxy.TargetAnimationId` (`uint`, `:163`) |
| `+0xAC` | the end of the animation held | r (20) | `EntityScriptProxy.ForceResetAnimationFlag` (`int`, `:172`) |
| `[0x800DD874]` | the camera kick Y, assigned 2 | w (20) | **no member today**: the member created by D-H4C2-2 (`IEntityWorldContext.SetCameraKick(int scrollY)`, the list by tick of `WorldProxy`); not a stop (the slice creates it) |
| callee `0x8003A59C` | `DestroyEntity(self, -1)`, stubbed by the vectors (`journal_destroy`) | - | `IScriptHost.DestroyEntity(entity, -1)` (`IScriptHost.cs:48`) |

Bytes that D-H4C2-3 adds to E13 (not read by the vectors): `+0x144` (the ground flag) -> `IsOnGround` (`:422`); `+0x10` (the status, put back to 2) -> `Status` (`:88`, `EntityStatus.Normal`); `+0x13C` -> `ForceAdjusted` (`:402`, NOT read by the guard).
The adapter of (e1) poses `IsOnGround` = 1 for the 20 vectors of the first pass (revision 3).

## Camera step `0x8002CDA0` up to `0x8002CF38` (100 vectors) and sway step `0x8002C894` (80 vectors)

| Binary | Role | Read / written | Member of the DLL |
|---|---|---|---|
| arguments `a1, a2, a3` (`a0` unused) | the followed point `PosX>>16`, `PosY>>16`, `PosZ>>16` of the hero | args | `lookAtRenderTarget` of `CameraMath.AdvanceCameraSmoothing` = `ComputeCameraLookAtRenderPosition(x, y, z)` (render X = x, render Y = -(y - z) + 16) |
| `[0x800E42B8]` | first call after a map load (`g_isCameraScrolling`) | r (100), w (35) | `needsSnap` (`bool`) of `AdvanceCameraSmoothing` |
| `[0x800E4328]`, `[0x800E432C]` | the scroll X, Y (integers, pixels) | r (65), w (100) | `previousSmoothedTarget` and the returned `Vector3` (render space); `ToOriginalScrollSpace` (`CameraMath.cs:301`): scrollX = render X - 160, scrollY = -render Y - 120 |
| `[0x800DD874]` | the camera kick Y | r (65), w (100: cleared) | **no member today**: the optional final parameter of `AdvanceCameraSmoothing` (D-H4C2-2), added to `swayOffsetY` (render Y -= kick), not applied on a snap |
| **`[0x800DD870]`** | **the camera kick X** | **r (65), w (100: cleared)** | **NO MEMBER and none planned**: it has no writer in the game (one `sw 0` clears it), D-H4C2-2 carries the Y kick only. **22 of the 100 vectors read it as +3 or -3** (ids 3, 4, 8, 12, 16, 24, 32, 34, 39, 40, 44, 45, 47, 48, 57, 64, 69, 72, 74, 83, 87, 91) and add it to the scroll X: **see the stop below** |
| `[0x800E4338]` .. `[0x800E4358]` | the sway block, 9 words: Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY | r, w | `CameraSway.Flag`, `LimitX`, `LimitY`, `SpeedX`, `SpeedY`, `OffsetX`, `OffsetY`, `ReachX`, `ReachY` in that order (`CameraSway.cs:33-41`) |
| `[0x801FFF00]` | the harness's stack word (written 100 times by the vectors; outside the callee's frame `0x801FFE00..0x801FFEFF` that the vectors exclude) | w | none: the harness's return-address slot, not an input or an output of the function |

## Stop of the plan raised by this table

"Un octet lu par un vecteur n'a pas de membre" is a stop of the slice (section 1.31.4, Arrets). The byte `[0x800DD870]` (the kick X) is read with a non-zero value by 22 vectors of `vec_camera_8002CDA0.json` and the DLL has no member to carry it
(and D-H4C2-2 does not add one: the X kick has no writer, so the DLL never needs it). The precedent of h4b1 (the vectors that vary a byte the DLL cannot carry are classified "binary only" and skipped by their flag, critique C-1) is NOT written in section 1.31.4:
the choice is the main session's. The other bytes all have a member (or the member that D-H4C2-2 creates).
