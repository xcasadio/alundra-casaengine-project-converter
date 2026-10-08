# E19.v discovery annex (measurement only)

Measured at commit `420cc6499350faafa02ad861ceecd49ccf8a5cfb` (branch `chantier/e19-suite3`), no production change.

Command (throwaway test, source kept in `ZzE19vArrivalCensusTests.cs.txt`, removed from `Alundra.Tests/` after the run):

    dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release -v q -nologo
    dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --blame-hang-timeout 300s --filter "FullyQualifiedName~ZzE19vArrivalCensusTests"

Files:

- `census-portals.tsv`: one row per portal of the 483 maps of `Maps/world-index.json` (3316 portals). `ground_px` is the 4-corner max ground of the
  destination map under the arrival; `above_px = ZLevel*16 - ground_px`; `dest_on_chain` = destination is one of `AlundraStoryChainOpcodeAudit.ChainMaps`.
- `census-summary.txt`: totals derived from the TSV.
- `arrival-476.txt`: the arrival record written by `0x53 @688` of map 390 (A1c setup), and the ground under it on map 476.
- `dynamic-traces.txt`: 60-frame traces (frame 0 = after the ArcRun constructor) of the 5 largest `above` arrivals (no chain destination has above > 0, so
  the choice is over all maps, distinct destination maps), one on-the-ground chain control, and the 476 record.
- `ZzE19vArrivalCensusTests.cs.txt`: the test that produced them.

Approximations:

- Census: the hero is taken at the arrival tile centre of the portal (`(DestTileX*24+12, DestTileY*16+8)`, i.e. the hero standing at the portal's X1/Y1
  corner); the real arrival adds the hero's offset inside the portal rectangle. Box fixture 21x15 with local (0.5, 0.5), the one of the arcs' hero; ground
  from `AlundraCellsCollisionField.TrySampleGround` through `AlundraTerrainProbe.SampleGroundCorner`, as `ClampToGround`. The destination internal map is
  `AlundraGameState.MapIdToInternalMapIndexTable` (identity at New Game). Portals are parsed with `AlundraWorldProxy.BuildPortals`.
- `above` is measured BEFORE `ClampToGround` (which only raises PosZ); `above < 0` means the clamp lifts the hero, not that it is in the air.
- Dynamic traces: `RealController` + `Prefabs` (animated hero), no flags, no input, direction `CardinalDirectionTable[ArrivalDirectionIndex]`.
  `ArcRun`'s constructor asserts the arrival animation is 0x36 and zeroes the hero's TargetAnimationId; the traces pass 0x36 to `ArcArrival`
  and then write the wanted animation (0x36, or 0xD for the 476 record) into `TargetAnimationId` before frame 1. For the 476 record, the "0x36 ends"
  SUMMARY lines are meaningless (the animation is not 0x36).
- The map's own event programs run in the traces (no flags set), which is why some traces have `PlayerControlFlags` bits set.
