# E20.A362 T0b: the smoke of the load (2026-10-26)

Second commit of T0, after the table of `t0a.md` (commit `26a15de4`). A throwaway test (never committed, deleted after the run; Release, `skipcopy.targets`, `dotnet test --filter`,
`--blame-hang-timeout 60s`, one test, 1 s) built `ArcRun` on the map 362 with `ArcSpec("E20A362-smoke", "Coal Mine _ Olen's Dream", "Coal Mine _ Olen's Dream-362", flags [248], ...,
RealController: true, Prefabs: true, Arrival: new ArcArrival(204 << 16, 144 << 16, 2 << 20, GameState.ResetAnimationId, 16))` (D-A362-3), ran 6 frames without talking to Olen
and printed statics only. **It read no tick of the scene**: no trace, no pc, no flag, no frame of an event. Output: `t0b-smoke.txt`.

What it shows (all statics):
- The world loads with the real controller and the prefabs; the arrival gives the hero (204, 144, 32), tile (8, 9, 2), on the ground, direction 16 (north), animation 0.
- The map's dialogue asset has 13 lines; the nodes `M362_S003`, `M362_S004`, `M362_S005` (dialogues 131, 132, 133: the local text ids `0x83`, `0x84`, `0x85`, node `M{map}_S{id & 0x7f:000}`) are
  present with 1, 3 and 1 lines; the line keys carry the prefix `line:`.
- The records 0 to 4 are present (sprites 243 (the lid, already at (212, 100, 32): the shift of `C[3]` is done in these frames), 26, 266, 265, 264 (Olen at (204, 120, 32))), the records 5 to 10
  are absent.
- No error other than the unresolved sprites (the headless montage's known noise, filtered by `ArcChecks.AssertNoUnexpectedError`); four warnings, all the headless montage's own (no
  `dialogue_etc` asset, no scrolling layer service, no navigation grid, no camera 2d).

Nothing disagrees with `t0a.md`. The table was written before this run.
