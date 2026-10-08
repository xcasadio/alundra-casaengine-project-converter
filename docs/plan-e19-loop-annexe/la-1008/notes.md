# L-a (engine half of the loop at the sequence marker), discovery against engine f86a981b, 2026-10-08

Read-only. Nothing built, run, edited, staged or committed in the repository or its submodules. Engine checkout
`chantier/e19-suite2` HEAD `f86a981b` (only the author's `CasaEngine.Launcher/Program.cs` is modified; not read).
Scripts in this folder: `sim.py` / `sim.out` (own model of the current C#), `fillsim_20261005.py` (copy of the
2026-10-05 model, for comparison only). Labels: **F** = read in code or computed, **H** = hypothesis.

Decisions in force: D-E19-70, D-E19-72..76, D-E19-102 (the session does L-a then L-b; depth set from O-E19-63).

## 1. Facts at f86a981b (what changed since the 2026-10-05 design)

- F1 `SoundAsset` grew (priority, variations, spatial, bindings) but still has no loop point: `IsLooped` :62,
  `IsStreaming` :76, `Load` :227-256. Its number reader `ReadNumber` returns a **float** (:348-362): reusing it for
  the loop start would lose odd values above 2^24 (16 777 217 -> 16 777 216, `sim.out` G). Alundra's values fit
  (largest loop start 10 695 720, track 14), but the rule must read an integer token.
- F2 `EditorAssetJsonSerializer.SaveSoundAsset` (:791-888) writes the six base keys, then every newer key only when
  it differs from its default (`priority` :801-804, variations, spatial, bindings). Dispatch :101-102. A field not
  written there is lost at the first editor save.
- F3 `SoundAssetInspectorPanel.CreatePreviewCopy` (:318-330) is `private static` and copies 7 fields; a streamed
  preview goes through it (:275-282). `CasaEngine.Editor` has `InternalsVisibleTo("CasaEngine.Tests")`
  (`InternalsVisibleTo.Tests.cs:3`) and `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs` exists. The
  inspector's `NumericField` is float-based (:678): an inspector row could not hold large loop starts exactly.
- F4 `WavStreamReader`: seekable stream required (:39-42); `DataOffset` = stream position after the `data` header
  (:174), not 44; `fmt ` > 16 bytes skipped (:161-163); unknown chunks skipped with odd-size padding (:245-259);
  `DataLength` rounded to whole blocks (:193); `Read` stops at the end of data and returns 0 there (:60-88);
  `Rewind` (:91-97) is the only repositioning. Data offsets: 16-byte `fmt ` = 44; 18-byte `fmt ` (the layout of
  `MusicPlayerTests.CreateStreamingAsset`, :26) = 46; 18 + `LIST` 6 = 60; demo `racing_game_music_1.wav`
  (18 + `fact` 4) = 58, 6 190 583 frames at 22 050 Hz (own parse). A naive `44 + 600*4` lands on frame 596 at offset
  60, and 2 bytes early (left/right swapped) at offset 46.
- F5 **Two loop sites**, both `Rewind()`: `MusicPlayer.FillQueue` :373 (inline mode and the synchronous first fill of
  `Play` in every mode, :129) and `StreamingWorker.Channel.Service` :263 (the production path: `AudioService.cs:98`
  uses the worker for the Software and MonoGame backends). `Acquire(reader, isLooped, name)` :60 ->
  `Channel.Reset(reader, isLooped, name)` :205-214 carry no start. `Play` copies only `IsLooped` (:123).
- F6 The per-call bound already exists on both consumer paths: `FillQueue` :358-360 and `FillQueueFromRing`
  :325-327 (at most `target` submits per call). No `MusicPlayerTests` fact pins it (only the stereo mixer has one,
  `AudioServiceStereoVoiceTests.cs:207`).
- F7 Queue: `DefaultBufferSizeInBytes` 16384 (:25), `DefaultQueuedBufferTarget` 3 (:28); worker ring =
  `2 x target` slots of one buffer each (:134). Software mixer: 16 KiB = 8192 samples = 2 chunks
  (`SoftwareMixer.cs:862-863`, `DefaultChunkSamples` 4096 :54); per-voice queue cap 128 chunks (:74) -> max depth 64
  at 16 KiB; pool 256 initial chunks. `StereoVoiceMixer.cs:35` documents its own target 3 "like
  `MusicPlayer.DefaultQueuedBufferTarget`".
- F8 `GetPosition` (:244-255) is the reader position (inline) or the last handed block (worker): it leads the audible
  position by up to the queue.
- F9 Demo: `AudioDemo.cs` keys in use: Space, L, F, S, B, P, C, PageUp/Down, arrows, M, N, R, T, D, G, V, J, H, O,
  K, I (the 2026-10-05 proposal's O and H are taken). `DemosGame` globals: arrows, Enter, Escape, F1, Add,
  Subtract. Free letters: A, E, Q, U, W, X, Y, Z. The demo-browser merge changed only 2 lines of `AudioDemo.cs`
  (`8d8dbc7a`, `DemoKeyboard.Read`).
- F10 Docs: `docs/engine/audio-system.md` §3 (:268-350; additive-key table :321-331; "a streamed asset ignores
  variations and priority" :340), §5 (:402-424; "La boucle rembobine le lecteur" :414, "lit et rembobine" :420),
  §9 (:635-683), §11 (:702-740). ADR-0056 says "`SoundAsset` is not changed" (S2 scope) and the worker "reads and
  rewinds"; ADR-0002:14 lists the V1 metadata.
- F11 Engine ADR numbers: highest committed on any local branch is **0071** (`chantier/e19-suite2`);
  `main`/`origin/main`/`demo-browser` 0070; every other branch and worktree <= 0070; nothing untracked in
  `docs/decisions`. **Next free: 0072** (re-scan before the commit).
- F12 DLL: no use of the engine `MusicPlayer`, `DefaultQueuedBufferTarget` or `LoopStartSample` in `Alundra/`; the
  DLL fake returns `GetPendingBufferCount => 0` (`Alundra.Tests/FakeAudioBackend.cs:189`) and `OpenStream => null`
  (:335). L-a moves no DLL test.

## 2. Rules (L-a)

R1 `SoundAsset.LoopStartSample` (`long`, sample frames = one sample per channel counted from the first audio byte,
default 0; setter clamps a negative value to 0). `Load` reads optional `loop_start_sample`: an integer token that fits
`long` is taken; any other token type warns once naming the asset and the key and keeps 0 (the `ReadNumber` warning
pattern, never its float). Meaningful only for a looped, streamed sound.
R2 `SaveSoundAsset` writes `loop_start_sample` only when it is not 0, right after `is_streaming`. Every existing
`.sound` re-saves with exactly its keys.
R3 `CreatePreviewCopy` becomes `internal` and copies `LoopStartSample`. No inspector row in L-a (float field;
documented limit).
R4 `WavStreamReader.SeekToSample(long frame)`: throws `ObjectDisposedException` when disposed and
`ArgumentOutOfRangeException` unless `0 <= frame < DataLength / BlockAlign`; seeks to
`DataOffset + frame * BlockAlign`, sets `Position` to `frame * BlockAlign`; no allocation. `Rewind()` =
`SeekToSample(0)` (kept, same behaviour: a reader always holds >= 1 block, :182-185). Optional
`WavFormatInfo.SampleFrameCount => DataLength / BlockAlign`.
R5 `MusicPlayer.Play`: after `OpenReader`, when `IsLooped` and `LoopStartSample >= frame count`, write one throttled
warning (built in `Play`, never in `Update`) and use 0; store the value in `Track.LoopStartSample` (reset in
`ReleaseTrack`). The asset is not read after `Play` (cutscene contract: the handle is released after `Play`).
R6 Both loop sites seek: `FillQueue` :373 -> `SeekToSample(track.LoopStartSample)`; `Acquire(reader, isLooped,
loopStartSample, name)` -> `Channel.Reset(...)` stores it; `Service` :263 -> `SeekToSample(_loopStartSample)`. The
"second read 0 -> finishing" guards stay.
R7 Reads still stop at the end of data: the seam submits one short buffer (no fill across the seam).
R8 Non-goals: no loop end, no engine crossfade (baked by the extractor, D-E19-73), no loop point for resident
sounds (MonoGame ignores regions), no `IAudioBackend` change, no DLL/converter/data change (L-b).
R9 (task T2) `DefaultQueuedBufferTarget` 3 -> **24**; constant and class docs rewritten for 44.1 kHz; the stereo
mixer keeps its own 3 (doc cross-reference fixed).

## 3. Values (from `sim.out`, written before any code)

Ramp R1000: 1000 stereo frames at 44 100 Hz, frame f = (L = f, R = 10000 + f), loop start 600, players built with an
explicit target 3 so the depth task does not move them.
- L1 inline `new MusicPlayer(service, 16384, 3)`: after `Play` 3 buffers **4000 / 1600 / 1600 B** = [0..999]
  [600..999] [600..999]; after `ConsumeBuffers(voice,3)` + `Update`: 6 buffers, last three 1600 B [600..999];
  frame(i) = i for i < 1000, else 600 + (i - 1000) mod 400 over 3000 frames. Red today: [0..999] x 3, 4000 B each.
- L1w worker twin `new MusicPlayer(service, true, 16384, 3)` (internal ctor) + fake backend: identical values;
  buffers 4-6 come from the worker (a worker still rewinding gives 4000 B at buffer 4). Update until 6 buffers or
  2 s, then dispose the player.
- L2 / L2w 1024 B buffers: after `Play` [0..255] [256..511] [512..767]; after two rounds 9 buffers
  1024, 1024, 1024, 928, 1024, 576, 1024, 576, 1024 (2056 frames). In L2w the worker crosses the seam itself.
- L3 loop start 0: 3 x 4000 B, then 6 x 4000 B (unchanged bytes). L4 not looped + 600: 1 buffer 4000 B, finishing,
  track ends after `ConsumeBuffers(1)` + `Update`. L5 loop start 1000 and 5 000 000: [0..999] x 3, one warning.
- L6 R1000h (`fmt ` 18 + `LIST` 6, data offset 60) through the player: L1 values; naive offset gives 596 at index
  1000. L7 allocation: 65 536 looped frames, loop start 32 768, 200 updates, 0 B. L8 loop start 999 (last valid
  frame): 4000 / 4 / 4 B, then +3 x 4 B (documents the short-region limit).
- B2 zero-pending fake, looped, loop start 600, recording off: submitted bytes 7200 after `Play`, 12 000 after one
  `Update` (watchdog `Task.Wait(10 s)` because removing the bound would spin).
- Reader: W1 R1000h `SeekToSample(600)`: `DataOffset` 60, `Position` 2400, next 16 B = (600,10600)..(603,10603),
  `Position` 2416; W2 `SeekToSample(999)`: 4 B then 0, `IsEndOfStream`; W3 -1 and 1000 throw; W4 `SeekToSample(0)`
  equals `Rewind`; W5 mono 1000 frames: `Position` 1200, next sample 600; W6 no allocation; W7 18-byte `fmt `
  (offset 46): first frame after `SeekToSample(600)` is (600,10600), naive gives (10599,600).
- Asset/serializer: S1 5 344 920 read as 5344920L; S2 missing -> 0; S3 -5 -> 0; S4 round trip 5 344 920; S5 new
  asset has no key, 5 344 920 writes it; S6 16 777 217 read exactly; S7 `"600"`, 600.5, `true` -> 0 plus one
  warning; P1 `CreatePreviewCopy` keeps 600. A demo `.sound` carrying the key enters the existing theories
  `Serialize_OfADemoSoundFile_KeepsExactlyItsOriginalKeys` and `Loader_DemoSoundFiles_HaveTheVariationDefaults`.
- Depth T2: with `SampleCount` 106 496 (26 buffers), `Play` 24, `ConsumeBuffers(2)` 22, `Update` 24.

## 4. Depth (D-E19-75, O-E19-63)

| target | best | worst | at a seam (track 6, 134-frame seam buffer) | fill across the seam |
|---|---|---|---|---|
| 3 | 278.6 ms | 185.8 ms | 95.9 ms | 185.8 ms |
| 6 | 557.3 | 464.4 | 374.6 | 464.4 |
| 24 | 2229.1 | 2136.2 | 2046.4 | 2136.2 |

Measured worst (O-E19-63): Debug 2-frame top-up gap 351.5 ms. Depth 6 clears the measured routes by 23 ms only and
not the unmeasured ones (cold cache, tracks 21/30, 476 -> 478). Depth 24 leaves about 1.7 s. Filling across the seam
buys 90 ms at depth 24 for two more code paths and new values: not recommended. Cost of 24: 384 KiB of queue plus a
768 KiB worker ring per concurrent track (channels are pooled, never freed); 384 KiB read synchronously in `Play`;
`GetPosition` leads by up to 2.2 s; 48 of 128 mixer chunks per voice (F7).

## 5. Tests that move

- Loop point task: none (loop start 0 is the same bytes; new demo file adds theory rows only).
- Depth task: `MusicPlayerTests.Play_StartsAStreamAndQueuesAhead` (:58-72, asserts 3 -> 16 with today's file),
  `Update_TopsTheQueueBackUpAsBuffersAreConsumed` (:88-100, 1/3 -> 14/14), the `SampleCount` constant and comment
  (:9-11), the comment of `GetPosition_FollowsTheDecoder` (:297). Tests already written against the constant
  (`MusicPlayerWorkerTests` :191, :273; `SoftwareAudioBackendStreamingCountTests` :118) keep their source; H: they
  pass at 24 (48 chunks, pool 256).

## 6. Closed file list

Engine runtime: `CasaEngine/Framework/Audio/SoundAsset.cs`, `.../Streaming/WavStreamReader.cs`,
`.../Streaming/WavFormatInfo.cs`, `.../Streaming/MusicPlayer.cs`, `.../Streaming/StreamingWorker.cs`,
`.../Streaming/StereoVoiceMixer.cs` (T2 doc line only). Editor services: `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs`.
Editor: `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs`. Tests: `CasaEngine.Tests/Audio/WavStreamReaderTests.cs`,
new `CasaEngine.Tests/Audio/MusicPlayerLoopStartTests.cs` (ramp helper inside it, not in `WavBuilder.cs`),
`SoundAssetTests.cs`, `SoundAssetEditorSerializationTests.cs`, `CasaEngine.Tests/Editor/SoundAssetInspectorPanelTests.cs`,
`MusicPlayerTests.cs` (T2). Demo: `CasaEngine.Demos/Demos/AudioDemo.cs`, new `Content/Audio/loop_point_demo.wav`
(529 260 B, generated: 44.1 kHz stereo, 132 300 frames, `fmt ` 18 + `LIST`, offset 60; 1 s 440 Hz then four
660 Hz beeps) and `loop_point_demo.sound` (`loop_start_sample` 44100), `Content/AssetInfos.json`,
`Content/Content.mgcb`. Docs: `docs/engine/audio-system.md` §3, §5, §9, §11; new `docs/decisions/0072-*.md`;
`docs/decisions/README.md`; new `ai-agent/tasks/e19la-music-loop-start-tasks.md`; `ai-agent/README.md`.
Parent: `docs/plan-e19-opcodes.md` §1.2w and the engine pointer.

## 7. Risks

- Silent drops (serializer, preview copy) and the float read: pinned by S4, S5, S6, P1.
- Forgetting the worker site: only L1w/L2w catch it (the production path).
- A loop region shorter than one buffer submits short buffers and shrinks the margin (L8); Alundra's shortest region
  is 248 430 frames (5.6 s).
- Depth 24 is an engine-wide default: memory and position lead above; the MonoGame count is one frame stale (covered
  by the 2-frame figure).
- Conflicts: `AudioDemo.cs`, `audio-system.md`, `docs/decisions/README.md`; ADR number re-scan.
- For L-b, not L-a: the DLL fake's 0 pending makes each `Update` submit 24 buffers (bounded, about 384 KiB copied per
  frame in DLL tests); `docs/formats/audio-manifests.md` belongs to L-b.

## 8. Author questions

1. Depth 24 as the engine default (recommended), or a per-project setting, or the minimum 6?
2. Keep the short seam buffer (recommended) or fill across the seam?
3. Demo: commit a generated 529 KB WAV (recommended) or an in-memory provider in the demo?
4. Resident sounds ignore `loop_start_sample` (recommended), or map it to `WithLoopRegion` under the software backend?
