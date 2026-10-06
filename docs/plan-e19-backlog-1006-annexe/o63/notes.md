# O-E19-63: game-thread stall during map loads that keep the music, measured (2026-10-07)

Open point O-E19-63 of `docs/plan-e19-opcodes.md` (row "| O-E19-63 |", section 1.2w, D-E19-75; annex
`docs/plan-e19-backlog-1006-annexe/notes.md` "O-E19-63", `verify/verify.md` B2). Read-only for the repository:
nothing edited, staged, committed or built there. Every process started has exited (checked with `tasklist`).

## Answer first

- **Measured fact.** Every music-keeping map change costs exactly **one long frame**. No second frame goes over
  100 ms. The other frames of the window stay at the idle value of about 31 ms.
  - **Debug:** that one frame takes 148 to 301 ms (median of the per-sample maxima: 199 ms).
  - **Release:** it takes 125 to 257 ms (median: 163 ms).
  - The worst cases are the maps around the Inoa village 162: 164→162 and 168→162.
- **Against today's depth 3** (D-E19-75 margins for 44.1 kHz stereo 16-bit, 16 KiB buffers):
  - Near a loop seam the margin is **95.9 ms**. All 80 samples exceed it, in Debug and in Release. If the stall
    lands near a seam, the streamed music always starves.
  - Away from a seam, the margin depends on the queue phase and lies between **185.8 ms** (worst) and **278.6 ms**
    (best):
    - Debug: 28 of 40 samples exceed 185.8 ms and 1 of 40 exceeds 278.6 ms.
    - Release: 8 of 40 samples exceed 185.8 ms and none exceeds 278.6 ms.
  - So **depth 3 would starve during these loads**: often in Debug, sometimes in Release, and always near a seam.
- **Against depth 24** (about **2046 ms** at a seam, from the counter-check): the worst stall plus one frame stays
  far below it.
  - The worst measured stall plus one frame is about 332 ms in Debug (301 + 31). The worst two-frame top-up gap
    is 352 ms.
  - That leaves about 1.7 s of headroom. The criterion "max stall + 1 frame < 2046 ms" **passes** on every route
    measured.
- **Today nothing starves.** Alundra's BGM is still a resident looped clip (`PlayClip`), not a stream. This
  measurement is the premise for L-b (streamed BGM), as the plan intended.

## Method

### Harness
- The harness lives in `harness/`: `O63Harness.csproj` and `Program.cs`. It is a `WinExe` outside the repository.
- It references copies of the repository's own built assemblies by HintPath. There is no ProjectReference, so
  nothing in the repository is built.
- The two assembly sets were copied as follows:
  - `lib-debug/` is a copy of `Alundra.Tests/bin/Debug/net9.0-windows`.
  - `lib-release/` is a copy of `Alundra.Tests/bin/Release/net9.0-windows`.
- After the build, the whole set is copied next to the exe, with these exceptions:
  - The gameplay assembly goes to `gameplay\Alundra.dll`.
  - `runtimes\win-x64\native\{openal,SDL2}.dll` go to the exe's root. The harness has no deps.json, so without this
    the software audio output failed to load `openal`: the first smoke run had no audio and was discarded.

### Hosting, same as `CasaEngine.Launcher/Program.cs`
- Loggers: `DebugLogger` and `FileLogger("log.txt")` (here in the output directory), at `LogVerbosity.Trace`.
- `EngineEnvironment.ProjectPath` is the `alundra-project` folder of the main checkout. The project file is
  `alundra-project\AlundraGame.json`.
- The runtime context is `GameSettings.CreateRuntimeContext()`, with `UIViewRuntimeFactory = new MguiViewRuntimeFactory()`.
- The game object is `new CasaEngineGame(projectFile, runtimeContext: ...)`. The harness subclasses it to time the frames.
- The runtime config is the launcher's: TieredCompilation false, RollForward Major, WindowsDesktop.
- The gameplay assembly is loaded through `GameSettings.AssemblyManager.AssemblyLoader = Assembly.LoadFile(<exe>\gameplay\Alundra.dll)`.
  - This is the same `LoadFile` as the default loader, but on the copy.
  - So `alundra-project\Alundra.dll` was never opened or locked.
  - The Debug copy is byte-identical to it (SHA-1 below).
- Parameters are passed by environment variables, never on the command line. In non-FINAL builds,
  `CasaEngineGame.Initialize` reads `GetCommandLineArgs()[1]` and `[2]` as the project file and the content path.
- `ContentPath` is `<exe>\Content`. That folder is a copy of the engine content and is identical
  (`diff -rq`) to the launcher's.
- The Master bus is muted in memory through `AudioSystemComponent.IsMuted`, because the run happened at night. The
  project file is not written, and the software mixer and output keep running.
- The audio backend is `SoftwareAudioBackend (source: default)`, as the log line shows.

### Load trigger
- Each load is requested with `GameManager.SetWorldToLoad(<world path from Maps/world-index.json>)`. This is the
  call `AlundraWarpDirector.Advance` makes (`AlundraWarpDirector.cs:621`).
- The harness issues it at the start of its `Update`, so the load (`Clear`, `LoadCopy`, `LoadContent`, `BeginPlay`)
  runs in that same frame's `GameManager.UpdateWorld`.
- The fade, the arrival record and the departure sfx of a real warp are not reproduced. See the caveats.

### Timing
`Stopwatch` timestamps are taken around `base.Update` and `base.Draw`. Each frame records four values:
- `interval`: Update start to Update start, which is one `Game.Tick` (Update + Draw + Present).
- `update` and `draw`: the time spent in each.
- `fill gap`: Update end to Update end.
  - The audio component updates last (`ComponentOrder`).
  - `AudioService.Update` calls `MusicPlayer.Update`, which tops up every music voice (`AudioService.cs:1577`,
    `MusicPlayer.cs:261-330`).
  - So the fill gap is the time between two queue top-ups.
- `2-frame gap`: two consecutive fill gaps. It applies to a backend whose pending count is stale for one frame
  (MonoGame, `loop-engine/notes.md` 4.2). The software backend in use does not need it, so it is given only as an
  upper bound.

### Run
- Startup is on 389 (`FirstWorldLoaded`). The harness waits 480 frames, then runs the route twice. Each step waits
  240 frames before the next trigger.
- The window of a transition runs from its trigger frame to the next trigger.
- `GameManager.WorldChanged` was counted: 27 per run, which is 1 at startup plus 26 requested. So no map warped on
  its own.

## Routes (BGM data: `alundra-project/Maps/music-index.json`)
This is the map → raw track table that `loop-disc/notes.md` §2 (`a7scan.py`) used:
- track 25 = 389 and 390;
- track 7 = 20 maps, the overworld among them;
- track 16 = 137 maps, mostly Inoa.

The pairs below are connected by real portals. The `DestMapId` counts come from each map's `tilemap/*.tileMap`.

| track | pair | portals (`DestMapId` count) |
|---|---|---|
| 25 | 389 ↔ 390 (the intro ship) | 389→390 ×4, 390→389 ×4 |
| 7 | 2 ↔ 6 (Overworld 0,1 ↔ 1,1) | 2→6 ×2, 6→2 ×2 |
| 7 | 6 ↔ 7 (Overworld 1,1 ↔ 1,2) | 6→7 ×3, 7→6 ×3 |
| 16 | 162 ↔ 164 (Inoa village ↔ a house) | 162→164 ×2, 164→162 ×4 |
| 16 | 162 ↔ 168 (Inoa village ↔ a house) | 162→168 ×3, 168→162 ×2 |

- One pass is: 389→390→389, then 2 (setup), then 2→6→7→6→2, then 162 (setup), then 162→164→162→168→162, then 389 (setup).
- The setup steps change the track and are reported apart.
- The music state was read by reflection (`AlundraMusicPlayer.CurrentMapSoundIndex` and `_currentVoice`) at the
  trigger and again just before the next trigger. For **all 80 music-keeping samples**, the index and the voice
  handle are identical, so the same voice played through the load. On every setup step the index changed and the
  voice handle generation advanced.

## Configuration

| item | Debug | Release |
|---|---|---|
| CasaEngine.dll | `CasaEngine/bin/Debug`, SHA-1 `95b3c9c9…`, built 2026-10-07 00:43, JIT optimizer disabled | `CasaEngine/bin/Release`, SHA-1 `0ab34f95…`, built 01:09, optimized |
| Alundra.dll | SHA-1 `45f2c709…` = the deployed `alundra-project/Alundra.dll` | SHA-1 `e7e5ea0a…` = `Alundra/bin/Release` (**not** the deployed DLL), built 01:13 |
| harness | Debug | Release |

Shared by both configurations:
- Engine HEAD is `acbb1ae3`, committed 00:32. Its working tree differs only in the author's `CasaEngine.Launcher/Program.cs`.
- Parent HEAD is `5bb5abd`.
- Runtime: .NET 9.0.20, TieredCompilation false.
- GPU: AMD Radeon RX 9060 XT. Display: 3440×1440 at 100 Hz. Backbuffer 1280×960, VSync on, `IsFixedTimeStep` false.
- The window was active at the end of the runs.
- The idle frame takes about 31 ms in both configurations: Update about 0.2 to 0.4 ms and Draw about 0.5 to 0.7 ms.
  The rest is outside Update and Draw, in Present or vsync. This was not diagnosed (caveat 5).

## Numbers

### Samples
- Each configuration has two runs × two passes, so 4 samples per transition and 40 per configuration.
- "load" is the time from the trigger to `GameManager.WorldLoaded` (after `BeginPlay`).
- "max Tick" is the longest frame interval of the window. It is always the frame right after the trigger.

| transition | track | Debug load ms (min–max) | Debug max Tick ms (median / max) | Debug max 2-frame gap ms | Release load ms (min–max) | Release max Tick ms (median / max) | Release max 2-frame gap ms | Ticks > 100 ms |
|---|---|---|---|---|---|---|---|---|
| 389 → 390 | 25 | 131–148 | 179 / 187 | 220 | 93–128 | 148 / 169 | 201 | 1 |
| 390 → 389 | 25 | 117–123 | 162 / 167 | 198 | 95–100 | 139 / 143 | 174 | 1 |
| 2 → 6 | 7 | 150–153 | 191 / 193 | 226 | 106–113 | 150 / 151 | 183 | 1 |
| 6 → 7 | 7 | 181–188 | 223 / 227 | 258 | 137–144 | 176 / 183 | 214 | 1 |
| 7 → 6 | 7 | 145–156 | 189 / 195 | 227 | 104–115 | 146 / 155 | 187 | 1 |
| 6 → 2 | 7 | 166–172 | 213 / 223 | 256 | 120–123 | 166 / 175 | 208 | 1 |
| 162 → 164 | 16 | 169–175 | 206 / 212 | 243 | 136–139 | 171 / 176 | 209 | 1 |
| 164 → 162 | 16 | 217–225 | 262 / 264 | 295 | 167–219 | 232 / 257 | 288 | 1 |
| 162 → 168 | 16 | 113–117 | 150 / 157 | 189 | 88–95 | 125 / 130 | 160 | 1 |
| 168 → 162 | 16 | 204–263 | 255 / 301 | 352 | 160–164 | 200 / 201 | 232 | 1 |

### Stall summary
- **Frames above 100 ms:** exactly 1 per sample, in all 80 samples.
- **Other frames above 45 ms** in the windows: only two kinds.
  - 6→2 has a 46 to 47 ms frame 3 frames after the stall, in all 4 runs.
  - One 50.1 ms frame came 240 frames later (Debug 162→168), unrelated to the load.
- **Total stall:** that single frame, so the same as "max Tick".
- **Worst over all samples:**
  - Debug: load 263 ms, Tick 301 ms, fill gap 320 ms, 2-frame gap 352 ms.
  - Release: load 219 ms, Tick 257 ms, fill gap 257 ms, 2-frame gap 288 ms.
- **First pass against second pass:** the keep transitions differ by about ±10 %. The first track change of a run is
  much slower (Debug 389→2: 393 to 395 ms against 210 to 214 ms in pass 2), but it is not a keep transition.
- **Setup steps (track change), max Tick:**
  - Debug: 175 to 395 ms.
  - Release: 150 to 240 ms.
- **Old evidence:** the 1-second-resolution log gave "0–2 s" for 389↔390 (`loop-engine/notes.md` 4.3). The new
  figure of 117 to 187 ms is consistent with it and replaces it.

### Against the stream margins (16 KiB buffer = 92.88 ms at 44.1 kHz stereo 16-bit)

| depth | margin | Debug samples over | Release samples over | worst measured + 1 frame (Debug) |
|---|---|---|---|---|
| 3, at a seam | 95.9 ms | 40 / 40 | 40 / 40 | 332 ms → starves by about 240 ms |
| 3, worst phase | 185.8 ms | 28 / 40 | 8 / 40 | starves |
| 3, best phase | 278.6 ms | 1 / 40 | 0 / 40 | starves (Debug 168→162) |
| 24, at a seam | about 2046 ms | 0 | 0 | passes, headroom about 1.7 s |
| 24, worst / best phase | 2136 / 2229 ms | 0 | 0 | passes |

- **Derived, not measured:** the deepest queue these routes need at a seam is about 352 ms, the Debug 2-frame gap.
  Depth 24 covers about 6 times that.
- **Derived, not measured:** depth 6 would cover these routes, since (6 − 2) × 92.88 + 3.04 = 374.6 ms. It would not
  cover the unmeasured heavier loads listed below.

## Facts against hypotheses

### FACT: measured here
- One stall frame per music-keeping load, with the values in the tables above.
- The same voice keeps playing across the load.
- The default audio backend is `SoftwareAudioBackend`.
- The idle frame takes about 31 ms on this machine.

### FACT: read in the code, not exercised by a stream
The voice queue is topped up only on the game thread:
- `AudioSystemComponent.Update` calls `AudioService.Update`.
- `AudioService.Update` calls `MusicPlayer.Update` (`AudioService.cs:1577`).
- `MusicPlayer.Update` calls `FillQueueFromRing` or `FillQueue` (`MusicPlayer.cs:261-330`).
- The S2 worker only fills the ring: `StreamingWorker`, with `_queuedBufferTarget * 2` slots (`MusicPlayer.cs:134`).

So a game-thread stall longer than the queued audio starves the voice. **Not verified with a streamed track
playing:** L-b does not exist yet, so no audible gap was measured.

### HYP: not measured
- **Cold disk cache.** The export was written at 01:02 to 01:15 and the smoke runs read it, so the OS cache was
  warm. A first visit after boot may be longer.
- **Other loads.** Track 21 and track 30 maps, and larger maps than 162, may stall longer.
- **476→478.** The old Debug log showed more than 1 s for this load. It changes the track, so it does not matter
  unless a stream keeps playing through it.
- **Shipped build.** A FINAL or shipped build with less logging would load faster.

## Caveats
1. **Fade and arrival are skipped.** The loads were triggered directly with `SetWorldToLoad`, without the warp
   director's fade, arrival record and departure sfx. The load itself is the same engine path. The departure sfx
   load and the arrival placement are not included, so they would add a little.
2. **Logging.** Trace-level logging to a file with AutoFlush is on, as in the author's launcher (Debug and Release).
   It inflates loads compared with a quieter build.
3. **Release DLL.** The Release Alundra.dll is not the deployed one. It is the Release build present in
   `Alundra/bin/Release`, which has the same timestamps as `Alundra.Tests/bin/Release`.
4. **Muted bus.** The Master bus was muted in memory. The mixer and the output still run, so the game-thread cost
   is unchanged (HYP).
5. **Unexplained frame pacing.** On a 100 Hz display, about 30 ms of each frame is spent outside Update and Draw.
   This was not investigated. A machine with a 16.7 ms frame would see each stall frame shorter by up to about
   14 ms, and the "+1 frame" term would shrink too.
6. **Inactive sleep.** The first pair of runs (`out-*-sleep20`) kept MonoGame's `InactiveSleepTime` of 20 ms. The
   second pair (`out-debug`, `out-release`) set it to zero.
   - The second pair logged an active window at the end.
   - The idle frames are identical in both pairs (31.5 ms Debug, 30.9 to 31.0 ms Release). So the 20 ms sleep did
     not fire in the first pair either. This is inferred, not logged.
   - Both pairs are pooled.

## Files (this folder)
- `harness/`: `O63Harness.csproj` and `Program.cs`.
  - Build commands: `dotnet build -c Debug` and `dotnet build -c Release`.
  - Environment variables: `O63_PROJECT`, `O63_OUT`, `O63_PASSES`, `O63_SETTLE`, `O63_MUTE`, `O63_NOSLEEP`.
- `lib-debug/` and `lib-release/`: copies of the repository's built assemblies.
- Per-run folders: `out-debug-sleep20/`, `out-debug/`, `out-release-sleep20/` and `out-release/`.
  - Each one holds `frames.csv`, `transitions.csv`, `run.txt` and `log.txt`.
  - Each one has a `summary.md` written by `analyse.py`.
- `out-smoke/`: a one-pass Debug run with 120-frame settling, consistent with the others and not pooled.
- `analyse.py`: the per-run table.
- `combine.py` and `combined.md`: the pooled table above.
