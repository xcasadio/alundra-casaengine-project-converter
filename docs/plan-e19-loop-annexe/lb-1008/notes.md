# L-b + O-E19-66 discovery (data, converter, DLL half of the loop at the marker), 2026-10-08

Read-only in the repository. Parent `486fdf5` (`chantier/e19-suite2`), engine `f86a981b` (author's
`CasaEngine.Launcher/Program.cs` untouched), analyser `a958d86`. Repo status identical before and after
(`M CasaEngineMonogame`, analyser `?? AlundraTools/log.txt`, the empty file of 2026-10-03, mtime unchanged).
Everything built or run here is a physical copy (`git archive` of the analyser HEAD) in this folder.
F = fact (file:line, address or measurement), H = hypothesis.

## 1. State re-established against HEAD

- F: analyser `db69b82..a958d86` touches only `AlundraTools/AlundraTools/AlundraTools.csproj` and
  `FontGlyphTable.csv`; the extractor references only `AlundraEngine` (and `PsxSdk`), not that project
  (`AlundraDataExtractor.csproj`). So the extractor code is the oracle's base.
- F: BGM anchors unchanged: `Program.cs:153` record, `:177-249` `ExtractDataFromBgm`, `:185` `maxSeconds = 240`,
  `:256-322` `RenderBgmTrackUntilLoop` (append before test `:275-277`, break `:306-312`), single caller `:219`.
  `--verify-bgm` (`:611`) and `--render-bgm` (`:690`) do not call it.
- F: font3 anchors unchanged: `Program.cs:1130` saves `font3.FontBitmapTim` (FONT3.TIM decoded with its own CLUT 0,
  key (156,165,132) transparent, `Font3.cs:141-145`); `GenerateFontBitmapTim(..., Color[] pal)` ignores `pal`
  (`Font3.cs:88-102`). F: `Font3.Palettes` (WIND.CL via `ImageHelper.FromPsxColor(int)`, `ImageHelper.cs:12-21`) has no
  bit replication (`<<3` only, R/B read swapped and swapped back in `BitmapFromPsxBuff`), so it must NOT be reused for
  font3: palette 8 must be decoded with TimLoader's BGR555 replication (`TimLoader.cs:63-66`) to give (74,82,57).
- F: the extractor needs `EntityNames.csv` next to the exe or in the cwd (`EntityNames.cs:18-22`); the extractor csproj
  does not copy it (only `AlundraGame`/`AlundraTools` do). The repo's Release bin has a hand-placed copy, SHA-256
  `40d97114...772b`, equal to the tracked `AlundraTools/AlundraTools/EntityNames.csv`. A fresh build must copy it.
- F: `data-extracted/` == `Alundra Remake/remaster-data-extracted` (`diff -rq` empty, 4536 files each, 3 604 860 988 B).
- F: today's 45 WAVs in `data-extracted`, the remaster copy and `alundra-project/Musics` equal `TodaySHA256` of
  `docs/plan-e19-loop-annexe/data/oracle_sha256.tsv` (45/45); `bgm.json` `c1e9d685...4e641` (13 470 B) in both data
  copies; export `Musics/bgm-manifest.json` SHA-256 `80c3dd32...0381` (16 440 B); stale `Musics/bgm_044.wav` present.
- F: oracle WAVs (`loop-data/oracle/sound/bgm`) equal the versioned table 45/45 (SHA-256 and bytes), total
  1 343 140 980 B; oracle `bgm.json` `3d176f4a...6efaf`, 17 202 B (`check_state.out`).
- F: font3.png identical in the three places, SHA-256 `08824c3b...f9a7`, 9951 B.
- F: today's extractor (a958d86, Release, physical copy) reproduces `data-extracted/` byte for byte: `diff -rq` empty,
  4536 files, 3 604 860 988 B, 45 s, exit 0, console `rendered=45 silent=1 failed=0`, `sequencer=9`
  (`run-head/`, `diff_head_vs_de.out`).
- F: converter anchors unchanged: `SoundManifestReader.cs:12-25` (`BgmEntry`), `:85-110` (`ReadBgm`), only non-null
  bool helper `:180-181`; `AudioWriter.cs:43-47`, `:198-201` serialise every property; `FontWriter.cs:107-136` copies
  `ui/font3.png` byte for byte through `TextureAssetWriter.EnsureTexture` (`File.Copy`, `TextureAssetWriter.cs:49`;
  the `.texture` wrapper carries no pixel data).
- F: DLL anchors unchanged: `AlundraMusicPlayer.cs:145`, `:175`, `:190`, `:266-277` (B4), `:359-386` (StartVoice,
  `PlayClip`, `isLooped: true` `:384-385`), `:388-396`, `:402`, `:407`, `:412-422`, `:424-466`, `:471-475`. No other
  production file reads `_currentVoice`, `IsCurrentVoiceAlive` or `CurrentVoiceForTests`.
- F: test touch points unchanged: `PlayCalls` 22/40/27 lines, `CurrentVoiceForTests` 6/3/2, `IsCurrentVoiceAlive` 6/20/12,
  `FakeAudioClip("bgm` registrations 11/15/11 in MusicPlayer/BgmFade/WarpDeparture tests; 9 + 16 + 15 = 40 `[Fact]`
  methods; flip `AlundraBgmFadeDirectorTests.cs:448` (`bgm_042` asserted looped). `FakeAudioBackend.cs:189`
  pending = 0, `:335` `OpenStream` null, `:160` `CreateStreamingVoice`.
- F: engine at `f86a981b`: no loop point (`SoundAsset.cs` has no `LoopStartSample`; `WavStreamReader.cs:91` `Rewind`
  only; `MusicPlayer.cs:373` and `StreamingWorker.cs:263` rewind); `MusicPlayer.Play(SoundAsset, float, object)` `:75`,
  `Stop` `:150`, `IsAlive` `:226`; `DefaultQueuedBufferTarget = 3` `:28`. `AudioService` uses the worker only for the
  Software and MonoGame backends (`AudioService.cs:97`): the DLL tests (fake backend) run inline, the game runs the
  worker.
- F: parent ADRs end at 0049; engine at 0071 (G3c). D: 460 GB free, C: 475 GB free. Existing backups in
  `Alundra Remake/`: six folders of 3.60-3.61 GB (`*.bak-e19g0`, `*.bak-e19g0b`, `*.bak-x2`, `*.bak-2026-10-03`).

## 2. Prototype of L-b1 (scratch only: `src-lb/`, `proto_extractor.patch`, `run-lb/`, `out-lb/`)

The rule of `docs/plan-e19-loop-annexe/data/notes.md` 1.2 written directly into `RenderBgmTrackUntilLoop` (record
gains `bool Looping, int? LoopStartFrame, int? LoopStartSample`; cap 480 s; J1 kept, J2 dropped; failure when J1
without J2 or nothing before the cap; release tail 60 silent frames then trim to the last audible frame; crossfade 735
before the statistics), plus `Font3.DecodeFontTimWithWindClut(folder, 8)` (WIND.CL entry 8 with TimLoader's
replication; colour 0x0000 transparent keeping FONT3.TIM's own index-0 RGB) saved at `Program.cs:1130`.

- F: console `BGM 44: rendered SILENT (123 frames, peak 0/0)`, `rendered=45 silent=1 failed=0 (of 46)`,
  `sequencer=18`; 71 s (76 s on the repeat); output 4536 files, 4 248 045 580 B.
- F: 45/45 WAVs equal the versioned oracle table; `bgm.json` byte-equal to the oracle (`3d176f4a...6efaf`).
- F: `diff -rq` against `data-extracted/` = 47 lines: the 46 of `predicted_extract_changes.txt` + `ui/font3.png`.
- F: font3.png: exactly 44 texels change, (82,90,57,255) -> (74,82,57,255), cells 4:1, 14:1, 15:1, 21:8, 22-25:2,
  26-29:5, 64:5; transparent texels 54 517 before and after with the same RGB; 15 distinct colours before and after;
  new SHA-256 `381c63efe4f663b0f0fec8e9da6f3a81751eec23ce60eeb742ba5df8f86a6fb5`, 9951 B. The 44 (x,y) are in
  `font3_texels.out`.
- F: a second run (`out-lb2/`) is identical to the first (`diff -rq` empty): the GDI+ PNG and the BGM are deterministic.
- H: the converter manifest bytes (20 352 B, SHA-1 `8f302c1a...7048`) rest on the 10-05 Python model (verified twice
  there), not on a converter run; not re-run here.

## 3. Split proposal

### L-b1 extractor + re-extraction + mirror (+ O-E19-66 extractor half)
- Files: analyser `AlundraTools/AlundraDataExtractor/Program.cs`, `AlundraTools/AlundraEngine/Text/Font3.cs` (new
  decode method; `Palettes` untouched); parent `alundra-casaengine-project-converter.Tests/BgmFreshSoundSystemTests.cs`
  (T-D, T-E, golden re-pointed to `docs/plan-e19-loop-annexe/data/oracle_sha256.tsv`: `Frames`, `SHA256`; T-E becomes the
  prefix check `TodayDataSHA256` of track 1 over `[44, 44 + 7966 x 2940)`), a new `BgmLoopAtMarkerTests.cs` (L-A..L-E,
  `bgm.json` SHA-256), a new font3 test next to `FontWriterTests.cs` (44 texels), the submodule pointer, the plan,
  `docs/formats/font.md`. Annex: the versioned `oracle_sha256.tsv` already holds every value.
- Red first on today's `data-extracted/`: L-A (38: Looping, J1, J1 x 735, Frames = J2), L-B (7 one-shots: false/null,
  Frames 1220/601/620/531/543/772/900), L-C (45: WAV length 44 + Frames x 2940), L-D (38: last stereo frame == frame
  LoopStartSample - 1; red on 33), golden (45 + `bgm.json`), F-1 (44 texels (74,82,57,255), 0 at (82,90,57,255)).
  Green before and after: L-E prefix (45), T-A, T-B (86), T-C.
- Procedure (precedent X2-R2): fresh Release build from the analyser commit + copy `EntityNames.csv`; run outside the
  repo into `Alundra Remake/remaster-data-extracted-lb`; console as section 2; `diff -rq` vs `data-extracted/` = exactly
  47 lines; 45 WAV + `bgm.json` + `font3.png` at the SHA-256 above; backups `data-extracted.bak-lb` (`robocopy /E`) and
  old remaster renamed `remaster-data-extracted.bak-lb`; new folder renamed; `robocopy /MIR` from PowerShell; final
  `diff -rq` empty (4536 files). Disk: about +4.25 GB (new) +3.6 GB (backup) +0.64 GB (data-extracted growth) on D:.
- Rollback: analyser commit reverted (pointer), rename the remaster folders back and mirror again, or rerun a958d86
  (deterministic, 45 s, proven today).

### L-b2 converter + export (manifest proof)
- Files: `Readers/SoundManifestReader.cs` (3 nullable properties after `AssetId`, `GetNullableBoolean`, `ReadBgm`),
  `Writers/AudioWriter.cs` (class doc only), `SoundManifestReaderTests.cs`, `AudioWriterTests.cs` (fixtures: 25
  true/7272/5 344 920, 42 false/null/null, absent null), `docs/formats/audio-manifests.md` (3 fields, new meaning of
  `frames`, `loop_detected` true on one-shots, example of track 1: 15215 / 253.583... / 7965 / 5 854 275), plan.
- Export in place: manifest before/after = 48 `M` (the 47 of `predicted_export_changes.txt` + `UI/Textures/font3.png`);
  `bgm-manifest.json` SHA-1 `8f302c1a...`, `AssetInfos.json` unchanged, report counters unchanged,
  `OutputSizeBytes` 1 930 905 296 (font3.png keeps 9951 B); double export identical except `report.json`.
- Between L-b2 and L-b3 the DLL still loops the whole (now doubled) file and loops one-shots forever: transient.
- Rollback: revert the converter commit; re-export from restored data, manifest equal to "before" except `report.json`.

### L-b3 DLL (needs L-a merged into the E19 stack)
- Needs from L-a: `SoundAsset.LoopStartSample` (sample frames, default 0); `MusicPlayer` seeking there at end of data
  in BOTH the inline `FillQueue` and the worker channel; a per-call buffer bound (the DLL fake answers 0 pending); the
  depth chosen from O-E19-63 (24 holds, 3 does not).
- Files: `Alundra/Scripts/AlundraMusicPlayer.cs` (manifest fields `looping`, `loop_start_sample`; cached `SoundAsset`
  per index, `IsStreaming`, `IsLooped = looping ?? true`, `LoopStartSample ?? 0`, Music bus, volume 1;
  `Music.Play/Stop/IsAlive`; `MusicTrackHandle`; class doc), `Alundra.Tests/FakeAudioBackend.cs` (`OpenStream` serves a
  registered WAV; pending count = submitted buffers for streaming voices; recorder of streaming voices),
  `AlundraMusicPlayerTests.cs`, `AlundraBgmFadeDirectorTests.cs`, `AlundraWarpDepartureTests.cs`, a parent ADR (0050
  or next free; supersedes route B, D-C-1), plan.
- Red first D-1..D-5 (notes.md 5); 40 existing methods rewritten from `PlayCalls` to the stream recorder; flip `:448`.
- Rollback: revert the DLL commit, redeploy the DLL.

## 4. Risks

- During a one-shot's release tail (up to 1.9 s past the end of track) B4 ignores a new `0xA5` that the original
  would honour (reachable: 42, 22 `0xA7` sites; 36, Cave-138; from `loop-disc/a7scan.json`, not re-run).
- One-shots 36 and 42 stop looping in game (faithful, audible change). Track 10/30 pass-to-pass loudness (notes 2.1).
- `IsAlive` is a slot check released at the next `Music.Update` (verify hint), B4 timing in tests.
- Font3: only the text bands and the cursor are proven to use entry 8; `wind.png` uses `<<3` without replication
  (H: a 1-2 LSB convention gap between the two pages; out of scope).
- Extraction from a fresh build without `EntityNames.csv` aborts (seen here).
