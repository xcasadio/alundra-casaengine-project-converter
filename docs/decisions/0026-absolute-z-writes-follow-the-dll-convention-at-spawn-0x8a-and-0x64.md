# ADR-0026: Absolute writes of Z follow the DLL convention (binary minus 1) at spawn, 0x8A and 0x64

- **Status**: Accepted (its first exception, the literal target of 0x22, is replaced by ADR-0041)
- **Date**: 2026-10-03 (decision D-E19-59 of the author, 2026-10-03, O-E19-45)
- **Source**: this chantier: `docs/plan-e19-opcodes.md` section 1.2n.1b E19.h1b1 (rules H1b1-R1 to H1b1-R5), D-E19-59, O-E19-45. Binary: `ALUN_CD.EXE` (France) at `0x80039EA0`-`0x80039F04` (spawn), `0x8003F654`-`0x8003F670` (0x64), `0x80040308`-`0x80040320` (0x8A).

## Context

- The binary keeps `PosZ` one unit above the height the DLL keeps: its ground is `T + 1` where `T` is the terrain height, so `z << 16 == T` puts an entity at rest exactly. The spawn writes `PosZ = z - ModZ + 1` and then floors it (`if !(T + 1 < PosZ) PosZ = T + 1`, comparing `PosZ`, not `ModdedPosZ`, at `0x80039EF0`); `0x64` and `0x8A` write `PosZ = (z << 16) + 1` with neither `ModZ` nor a floor; `0x65`, `0x89` and `0x8B` add an offset without a `+ 1`.
- The DLL already keeps `PosZ` equal to the binary's minus 1 everywhere else (a landing is at `T`). Three writes kept the `+ 1`: the spawn factory, `0x8A` and `0x64`.
- An entity with a controller loses that unit at the first pull of the root at the head of a frame (the root is a float; whole pixels survive the round trip, one unit does not). Placed on another entity it then crosses its support. That is why the spawn evaluated one support without reach (`EvaluateEntitySupport(..., immediateAtSpawn: true)`, O-E19-15). An entity without a controller kept the `+ 1` for good (14 sites: Rancune on map 476, Sara on 41 and 47, the key triggers of 145, 229 and 398).
- Census: 544 `0x64` sites in 155 maps, 684 `0x8A` sites in 69 maps. The only visible effect found is the floating platform of maps 83 and 410 (`0x64 @1235`, then `0x21 @1246`): its first climb ends at 48 px after 32 ticks like the original, instead of 49 px after 33.

## Decision

- The spawn writes `PosZ = (z << 16) - ModZ`. When the caller hands over a collision field (`AlundraWorldProxy` does, at map load and in `SpawnEntityByRecordId`), the spawn raises `PosZ` to `T` when `PosZ <= T`; the comparison is on `PosZ`, as in the binary. `T` is the maximum of the four corners of the record's footprint by `AlundraTerrainProbe.SampleTerrainHeightCorner`, the sampling of `ComputeTerrainHeight`. Without a field (the intro harness, bare tests) no floor is applied, not even at 0.
- `0x8A` and `0x64` write `PosZ = z << 16`, with neither `ModZ` nor a floor. `0x65`, `0x89` and `0x8B` do not change.
- The two production calls of `EvaluateEntitySupport(..., immediateAtSpawn: true)` are removed (O-E19-15 is closed in production). The parameter stays for the intro harness and its tests.
- All of it goes in one commit: converting `0x8A` and `0x64` alone would leave an entity placed on a platform born from the factory one unit inside its box, without the support.

## Consequences

- The pixel and `TileZ` do not change: the binary's Z always equals 1 modulo 256, so binary minus 1 and the old DLL value fall in the same pixel and the same `TileZ`. Pins move by exactly one unit (`PosZ`), and A6 moves from `7340032` to `9437184` (the transparent block born from `0x2D @265` is no longer glued on the sailor 4: it stays at its spawn height, the terrain `T` of 144 px, like the binary).
- Two exceptions stay: the literal target of `0x22` (it waits for a record height, not a `PosZ` write) and a hero who arrives above the ground (adoption keeps its own rule).
- The terrain floor lifts 431 records of the corpus (54 on maps used by tests); most already reached the terrain at the first tick. 87 entities without gravity whose `OffsetZ` is above 0 now stay higher by at most `OffsetZ`, as in the binary (save books +2 px on maps 178, 179 and 185, bouncy pots +12 px on 15 and 17, jump ramps, lamp switches); 6 stem-bomb lids with gravity fall by `OffsetZ` after the spawn; 4 controller-less lasers stay raised. These are for the author's recipe.
- The key triggers 172 rec4 and 179 rec4 are not glued on their support today (rec6 spawns after them, in layer order, and the spawn-time snapshot holds only earlier records); they keep their height, minus one unit.
- Rolling back is a revert of the commit (DLL only, no export).
