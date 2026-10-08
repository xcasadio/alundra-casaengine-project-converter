# Fresh counter-check of the L-b + O-E19-66 discovery (2026-10-08): working notes

Read-only in the repository (parent `486fdf5`, engine `f86a981b` with the author's `CasaEngine.Launcher/Program.cs`, analyser `a958d86`
with its untracked `AlundraTools/log.txt`); `git status` identical before and after. Everything built or run lives in this folder.
F = fact (file:line, address or measurement), H = hypothesis.

## What I ran

| step | how | output |
|---|---|---|
| source copy | `git archive a958d86 AlundraTools Directory.Build.props` into `src/`, copied to `mylb/`, `patch -p1 --binary < ../proto_extractor.patch` | `mylb/` equals the discovery's `src-lb/` (diff -rq empty, bin/obj aside); `src/` equals `src-head/` |
| build | `dotnet build -c Release AlundraDataExtractor.csproj` in `mylb/` | `build-mylb.log`, 0 errors |
| run | `run_mylb.sh` (cwd `run/`, `EntityNames.csv` copied from the archive, SHA-256 `40d97114...772b` = tracked file) | `run/extract_stdout.txt`, `run/done.txt` (exit 0, 101 s with a concurrent disassembly), `out/` |
| diffs | `diff -rq out data-extracted`, `diff -rq out ../out-lb`, `diff -rq data-extracted "Alundra Remake/remaster-data-extracted"` | `diff_mine_vs_de.out` (47 lines), `diff_mine_vs_theirs.out` (empty), `diff_de_vs_remaster.out` (empty) |
| data values | `vdata.py` | `vdata.out` |
| copies, oracle, report | `vcopies.py` | `vcopies.out` |
| font3 | `vfont3.py` (own TIM/CLUT parser, no C#) | `vfont3.out` |
| binary | `vclut.py` (capstone on ALUN_CD.EXE France) | `vclut.out` |
| reference images | `vrefimg.py` (all 114 PNGs under `docs/`, `Alundra.Tests/`, converter tests) | `vrefimg_all.out` |

## Facts

- F: analyser `db69b82..a958d86` = `AlundraTools/AlundraTools/AlundraTools.csproj` (+9) and `FontGlyphTable.csv` (+257) only;
  `AlundraDataExtractor.csproj` references `AlundraEngine` only and copies no `EntityNames.csv`; `EntityNames.cs:18-22` looks in the cwd then
  `AppContext.BaseDirectory`; loaded by `GameEngine.cs:87` and `Program.cs:1361`.
- F: Program.cs anchors as stated: record `:153`, `ExtractDataFromBgm` `:177`, `maxSeconds = 240` `:185`, caller `:219`, render `:256-322`,
  font3 save `:1130`. `Font3.cs:88-102` ignores `pal`; `:141-145` decodes FONT3.TIM with its own CLUT and key (156,165,132).
  `ImageHelper.FromPsxColor(int)` `:12-21` = plain `<<3`; `TimLoader.cs:63-66` = bit replication; with a key, TimLoader's alpha is key-only.
- F: my build of the prototype: console `BGM 44: rendered SILENT (123 frames, peak 0/0) - not exported`,
  `Extracted BGM: rendered=45 silent=1 failed=0 (of 46)`, `pitch guard hits: sequencer=18 sfx=0 exporter=0`; 4536 files, 4 248 045 580 B;
  identical to the discovery's `out-lb/`; vs `data-extracted/`: the 46 lines of `predicted_extract_changes.txt` + `ui/font3.png`.
- F: 45/45 WAVs equal `oracle_sha256.tsv` (SHA-256 and bytes, 1 343 140 980 B); `bgm.json` SHA-256 `3d176f4a...6efaf`, 17 202 B, byte-equal to
  the oracle file; fields appended `Looping`, `LoopStartFrame`, `LoopStartSample`; `LoopDetected` changes on 14 only; `FirstAudibleFrame` on none;
  max `LoopStartSample` 10 695 720.
- F: one-shots (Frames, EndOfTrackFrame): 26 (1220, 1106), 36 (601, 496), 37 (620, 605), 38 (531, 419), 40 (543, 540), 42 (772, 663),
  43 (900, 807); longest tail after E: 113 frames (track 26) = 1.883 s.
- F: L-C green on 45; L-D green on 38/38 on the new files; L-E prefix green on 45 (new files) and on today's files.
- F: L-D on today's `data-extracted/` with the table's `LoopStartSample`: red on **38/38** (no track green by chance).
- F: the oracle WAVs in `loop-data/oracle/sound/bgm` equal the table 45/45; the 45 export WAVs equal `TodaySHA256`; `data-extracted/` equals
  the remaster folder.
- F: FONT3.TIM: 4 bpp, 256 x 256, one 16-colour CLUT. CLUT 0 vs WIND.CL entry 8: equal except index 0 (0x4293 vs 0x0000) and index 4
  (0x1D6A -> (82,90,57) vs 0x1D49 -> (74,82,57), replicated). Index 4 = 44 texels, cells 4, 14, 15, 21 (8), 22-25 (2), 26-29 (5), 64 (5); index 8
  unused. No WIND.CL entry equals FONT3's CLUT 0 on the used indices. Today's `font3.png` equals my CLUT-0 decode (RGBA, 0 differences); the
  prototype PNG (theirs and mine, SHA-256 `381c63ef...6fb5`, 9951 B) equals my entry-8 decode (0 differences).
- F (binary): `0x80044B48` loads `taki\screen\wind.cl` (string `0x80023EFC`, 0x200 bytes to `0x800CC058` via `jal 0x8008160C`);
  `0x80044B74-0x80044B9C`: `*(0x80146E28 + 2i) = GetClut(0x120, 0x1E0 + i)`, i = 0..15 (`jal 0x800840E8`); upload `0x80044BC4` (`jal 0x80084F90`,
  rect (0x120, 0x1E0, 16, 16)). Direct reader of entry 8 (`+0x10`): `0x80050424` only; 19 other sites read the table base with a computed index.
- F (engine f86a981b): no `LoopStartSample`; rewinds only at `MusicPlayer.cs:373`, `StreamingWorker.cs:263`; `AudioService.cs:97` uses the
  worker only for Software/MonoGame backends; `IsAlive` `:226` = slot lookup, slot released in `Update` when finishing and 0 pending (`:292`).
  **The inline `FillQueue` already has a per-call bound**: `for (submitted = 0; submitted < _queuedBufferTarget && ...)` (`MusicPlayer.cs:354-358`).
- F (DLL tests): `[Fact]` 9/16/15; `PlayCalls` lines 22/40/27 (BgmFade `:558` is a comment); `CurrentVoiceForTests` 6/3/2; `IsCurrentVoiceAlive`
  6/20/12; `FakeAudioClip("bgm` 11/15/11; `GetVoiceBus` 3/1/0; `:448` asserts `bgm_042` looped. Fake: `CreateStreamingVoice` `:160`,
  `GetPendingBufferCount => 0` `:189`, `OpenStream => null` `:335`.
- F (converter tests): T-D 45 rows (`tracks.tsv` `NewFrames`), T-E 1 and golden 45 (`plan-e19-x2-annexe/oracle_sha256.tsv` `SHA256`) = 91.
- F (reference images): five versioned PNGs contain font3 index-4 texels (82,90,57): `docs/plan-e19-f2b1-annexe/pixels/centred.png` (8),
  `centred_x2.png` (32), `docs/plan-e19-f3-annexe/view/pixels/overlap_mrow_V1_t19_x1.png` (90), `_x2.png` (360),
  `overlap_mrow_V2_t22_x1.png` (90); none contains (74,82,57). They are compared texel for texel with a GPU render of the exported
  `UI/Textures/font3.png` (`TextBoxGpuHarness.cs:276-290`, project root from `FindProjectRoot()`):
  `AlundraTextBoxPixelTests.ACentredLine_IsTheVersionedImage_AtX1AndX2` (`:229-240`, `[glyph id=21]`),
  `AlundraChoicePixelTests.TheChoice_OverAFullFirstRow_IsTheVersionedImage_AtX1AndX2_AndTheOrderPoints` (`:294-299`, underlay 18 x glyph 0x1A)
  and `TheInverseOrderOfTheWindows_DiffersFromTheVersionedImages` (`:303-319`, a 338-345 count window). 82 is not a `<<3` value, so every
  (82,90,57) texel in them is font3 index 4.
- F (export): `report.json` (2026-10-07 01:02) `OutputSizeBytes` 1 311 900 066, `OutputFileCount` 24 106 (the 10-05 base was 1 287 720 524 /
  23 744); export `bgm-manifest.json` SHA-1 `509e8c68...73d1` (the model's base, unchanged); converter sources unchanged since `1c1e0d4`.
  `font3.texture` holds no pixel data; `FontWriter.cs:108` is the only converter reader of `ui/font3.png`.
- F (DLL today): music is a whole clip, `AssetContentManagerAudioClipProvider.GetClip` (`:22-45`) caches each clip handle until disposal.
- F: no map's `music-index.json` value is a one-shot (26, 36, 37, 38, 40, 42, 43); `a7scan.json` lists 0xA7 sites for 36 (1) and 42 (22) only (read,
  not re-run).
- F: author rule (memory `ne-pas-corriger-decompilation`, 2026-10-03): do not modify `alundra-datas-analyser/AlundraTools/AlundraEngine`.
- F: D: 459 GB free, C: 451 GB free.

## Hypotheses

- H: converter manifest bytes after L-b2 (20 352 B, SHA-1 `8f302c1a...`) rest on the 10-05 model (`data/verify/my-bgm-manifest.json`), not on a run.
- H: font3 contexts other than the text bands and the cursor may use other table entries (computed-index readers not followed).
- H: the release tail matches the binary's key-off release (`0x80091134`), from the analyser render only.
