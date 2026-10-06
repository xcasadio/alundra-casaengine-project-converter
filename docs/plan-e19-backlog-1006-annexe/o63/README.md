# Open point O-E19-63: music-keeping map loads measured (2026-10-07, night)

Plan: `docs/plan-e19-opcodes.md`, row O-E19-63 and section 1.2w.

- `notes.md`: method, configuration and caveats; `combined.md`: the per-transition table (load time, longest frame, longest gap
  between two music-queue top-ups) for Debug and Release, 80 samples, and the comparison with the stream margins.
- `harness/`: the out-of-repository console harness (`O63Harness.csproj.txt`, `Program.cs.txt`, renamed so no build picks them up):
  it hosts the game on the real export like the launcher, references the built assemblies by HintPath, triggers loads with
  `GameManager.SetWorldToLoad`, mutes the Master bus in memory, and times every `Game.Tick`. `analyse.py` and `combine.py` produce the
  table from the run outputs (the outputs themselves were not versioned).
- Not measured: a cold disk cache, the tracks 21 and 30, 476 → 478, the warp director's fade and arrival placement.
