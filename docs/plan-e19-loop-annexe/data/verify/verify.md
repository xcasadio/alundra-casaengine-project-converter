# Verification of the L-b value audit (option B), read-only, 2026-10-05

Target: `../loop-data/notes.md` and the report built on it (D-E19-70, D-E19-72, D-E19-73; plan §1.2w, §1.2v).
Nothing was built, run (extractor, converter, game, tests) or written outside this folder. Repo status is the same
before and after: parent `5c26993` clean except the submodule markers; analyser `db69b82` only `?? AlundraTools/log.txt`;
engine only the author's `M CasaEngine.Launcher/Program.cs`.

## Method (own code, this folder)

- `vaudio.py` -> `vaudio.json`, `vaudio.out`; `vsummary.py` -> `vsummary.out`. For the 45 written tracks it reads:
  today's `data-extracted/sound` (X2), the oracle (`loop-data/oracle/sound`), the **unchanged** extractor CLI renders
  (`loop-data/renderbgm/*_csharp_render.wav`, format matches `Program.cs:757,764`) and the long renders
  (`loop-data/long/`). J1/J2/end frames come from `loop-disc-verify/seqsim.out` (an independent Python sequencer, `ana`
  variant), not from the harness. Own crossfade (vectorised, own rounding), own stats (C#-style `(double)long / total`),
  own WAV header parse, own seam/continuation/silence/tail measures.
- `viters.py` -> `viters.out`: periods and loudness windows over passes 2-7 (`long7/`), two metrics.
- `vcause.py` -> `vcause.out`: loop region [J1,J2) vs a one-pass-later region [J2,J3) against the true continuation.
- `vmanifest.py` -> `vmanifest.out`, `my-bgm-manifest.json`: own model of `AudioWriter.WriteManifest`.
- `vtests.py` -> `vtests.out`: every claimed test line list compared with the files.
- `disasm.py` -> `disasm.out` (capstone on `ALUN_CD.EXE` France, load address from the header).
- `fulltoday_diff.out`: `diff -rq loop-data/full-today data-extracted` (exit 0, 4536 files each side).

## Verdicts

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Loop jump keeps flag bit 0, only rewrites position and delay | CONFIRMED | `0x8008CB3C` `jal 0x8008D8D0` (delta read, result dropped), `0x8008CB44` `lw v0,0xC(s0)`, `0x8008CB48` `sw zero,0x88(s0)`, `0x8008CB50` `sw v0,4(s0)`; no flag write. Analyser `SoundManager.cs:1904-1906`. (The handler also stores 0x1E in `+16`, `0x8008CAE0`.) |
| 2 | End of track clears bit 0, sets 0x200 and 4, keys off via `0x80093EF4`, then the frame driver stores 0 | CONFIRMED | `0x8008D7F0` `and -2`, `0x8008D80C` `and -9`, `0x8008D828` `and -3`, `0x8008D844` `ori 0x200`, `0x8008D860` `ori 4`, `jal 0x80093EF4` at `0x8008D898` (only when `+3C != 0xFF`) and `0x8008D8A4`; `0x8008E594` `andi 4` -> `jal 0x8008F2E8` -> `0x8008E5B4` `sw zero,0x90`. `0x80093EF4` loops the voices and calls `0x80091134(0)` on matching keys. Analyser `:1558-1572`, `:1302-1306`; `FUN_8008f2e8` (`:653-686`) resets `SeqPosition` to the start, so the end of track is seen as "backward with bit 0 clear". |
| 3 | Today's frame boundary: append before test (`Program.cs:275-277`, `:306-312`), file `[0,J1]`, `Frames = J1+1` | CONFIRMED | Code read; `Frames_today == J1+1` on 37/38 looping tracks (14 capped at 14400). |
| 4 | Option B file `[0,J2)`, `Frames = J2`, `LoopStartFrame = J1`, `LoopStartSample = J1x735` | CONFIRMED | Oracle `bgm.json` vs seqsim: 38/38; `LoopStartSample == LoopStartFrame*735` on 38; one-shots `false/null/null` on 7. |
| 5 | Cap 480 s; largest J2 27 204 (track 14); longest silent run before J2 117 frames (track 22) | CONFIRMED | Own: max J2 27 204; max silent run 117 on 22 (one-shots: 45 frames before E at most). |
| 6 | One-shot tail values: kept 113/104/14/111/2/108/92, new Frames 1220/601/620/531/543/772/900, today 1107/497/606/420/541/664/808 | CONFIRMED | Own tail rule on `long/` gives the same 7 values; oracle WAVs equal the CLI render of those lengths. |
| 7 | 26, 36, 38, 42, 43 cut loud today (last-frame RMS 2424-6008) | CONFIRMED | 4290.0, 4046.4, 3157.5, 6008.0, 2423.5. |
| 8 | "After the tail only 1-2 LSB remain" | REFUTED as worded (minor) | Last kept frame peaks 65-75 LSB; the dropped audio peaks 51-63 LSB in the next 60 frames, 3-5 LSB in frames 60-200, 1 LSB beyond. The notes' wording ("from about 200 frames after E") is the right one. |
| 9 | Without the tail one-shot files stay byte-identical, diff 39 lines | CONFIRMED | `run-b-notail` one-shot WAVs `cmp`-equal to `data-extracted` (7/7); 38 WAVs + `bgm.json` = 39. |
| 10 | Console: `BGM 44 ... SILENT (123 frames ...)`, `rendered=45 silent=1 failed=0 (of 46)`, `sequencer=18`; today 9, all on track 19 | CONFIRMED from logs only (not re-run) | `run-oracle.log` ("BGM 19: ... hits 18"), `run-x2-mine3.log` ("BGM 19: ... hits 9"), `run-x2-real.log`/`full-today-run` (9). Track 44 end at frame 122 (seqsim) fits 123 frames. "Because the loop body plays twice" is consistent (18 = 2x9) but not shown. |
| 11 | Crossfade formula, integer, no ties; last sample equals sample `J1*735-1` | CONFIRMED | Own implementation reproduces the oracle from the unchanged CLI render on 38/38; last sample equals the pre-J1 sample on 38/38 (25: (-732,-123) both; raw (192,89)). |
| 12 | Pass-2 end equals pass-1 end only on 2, 12, 14, 22 (crossfade byte no-op there); difference >= signal on 18 tracks; prior "converge" claim wrong at sample level | CONFIRMED | Identical last frame: [2, 12, 14, 22]; err RMS >= signal RMS on 18 (1, 4, 5, 10, 11, 13, 15, 16, 18, 19, 20, 21, 25, 29, 30, 32, 33, 41). Crossfade changes 0 samples on those 4, 138 samples by 1 LSB on 23, max \|delta\| 13 495 on 4. |
| 13 | No-crossfade wrap step > p99 on 9 tracks "where the original has no such jump" (1: 8089/3087, 10: 3059/1008, 11: 5553/1170, 13, 15, 16, 17, 20, 21) | CONFIRMED values; count 8 strictly | > p99 on 10 tracks (those 9 + 22, where it is the natural step). On **20** the original's own step at J2 (1578) also exceeds p99 (1531), so "no such jump" holds on 8. |
| 14 | With crossfade the wrap step equals the natural J1 step on 38/38; median 0.12 vs 0.22 of p99 | CONFIRMED | 38/38 (it follows from the last weight being 1); medians 0.117 / 0.222. Above p99 with crossfade: 4, 15, 22. |
| 15 | True continuation: whole pass only on 2, 12, 14; 2205 (22), 2773 (34), 2948 (31), 25 799 (23), 63 945 (6); 79-142 on 9, 39, 45, 46; "at once on the other 29" | Values CONFIRMED; count REFUTED | Other tracks = **26** (24 at sample 0, 18 and 41 at sample 1); 3+5+4+29 would be 41 tracks. |
| 16 | Cause = the original's one-frame jitter; pass lengths vary on 32 tracks | Count CONFIRMED; cause UNCONFIRMED | Periods vary on 32 (constant on 2, 6, 12, 14, 23, 28). But 28 has 6 equal periods and still parts at sample 0; looping one pass later ([J2,J3)) still parts within 1 sample on 24 tracks, including 3, 7, 10, 17, 33 where P2 == P3. Passes differ below the frame level, not only by length. Side result: no later loop window would make B sample-faithful. |
| 17 | Passes 3-7 within 3.2 dB of pass 2 on 34/38; four exceptions 10 (19.5), 30 (13.0), 31 (5.3), 21 (4.1) | Values CONFIRMED; count REFUTED | Same metric (500 ms, -50 dBFS gate): **33/38**, a fifth exception **34 at 3.7 dB** (also in `loop-data/iters7.tsv` and the notes' own table). With 1 s windows gated at -40 dBFS only 30 (10.0 dB) and 10 (5.8 dB) exceed 3.2 dB: 21, 31, 34 depend on the metric; 10 and 30 do not. |
| 18 | Track 10: passes 2, 5, 7 quiet, 3, 4, 6 loud, B loops the quiet one | CONFIRMED | Left dB at +2400 frames, passes 1..7: -38.1/-45.1/-38.1/-38.1/-45.1/-38.1/-45.0; at +2880: -39.0/-54.8/-39.0/-39.0/-57.3/-39.0/-54.8. Cause (voice allocation) UNCONFIRMED, as labelled. |
| 19 | Oracle check 1: today mode rebuilds `data-extracted` | CONFIRMED | `run-x2-real` and `run-x2-mine` `bgm/` and `bgm.json` equal `data-extracted/sound` byte for byte. |
| 20 | Oracle check 2: independent rebuild, same SHA-256 45/45 | CONFIRMED (own rebuild) | Own derivation: unchanged CLI render + own crossfade (looping) or CLI render as is (one-shots) equals the oracle on 45/45; own SHA-256 equal to `oracle_sha256.tsv` on 45/45. |
| 21 | Oracle check 3: harness without crossfade equals unchanged `--render-bgm --frames N` | CONFIRMED | `cmp` of `run-b-raw` vs `renderbgm` 45/45; CLI frame counts equal the oracle `Frames`. |
| 22 | Oracle check 4: X2 data is a byte prefix of the new file | CONFIRMED | 45/45. |
| 23 | J1, J2, P3, end frames equal `loops.tsv` 45/45; track 14 analyser 14552/27204, binary 14551/27202 | CONFIRMED | `long/events.tsv` vs `loops.tsv`: 0 mismatches; seqsim `bin` gives 14551/27202. "Inaudible" is a judgement (not measured). |
| 24 | Sizes 699 960 120 -> 1 343 140 980 B, x1.919, largest `bgm_014.wav` 79 979 804 B | CONFIRMED | Own sums; every oracle WAV is 44 + Frames x 2940, valid PCM 16-bit stereo 44.1 kHz header. |
| 25 | `bgm.json`: 3 fields appended; Frames/Duration/RMS move on 45, Peak 16/15, LoopDetected only 14, FirstAudibleFrame none; SHA-256 `3d176f4a...6efaf` (today `c1e9d685...4e641`), 17 202 B | CONFIRMED | Own field diff and hashes; own stats recomputation equals the oracle on 45/45. |
| 26 | Re-extraction diff 46 `M`; full extraction with today's code equals `data-extracted` (4536 files) | CONFIRMED (list and premise) | `predicted_extract_changes.txt` = `bgm.json` + 45 WAVs; `diff -rq full-today data-extracted` exit 0, 4536 files. The snapshot `extractor-bin` was not rebuilt by me; its behaviour matches db69b82's output. |
| 27 | Export diff 47 `M`; manifest 20 352 B SHA-1 `8f302c1a...7048`; `AssetInfos.json` and counters unchanged; `OutputSizeBytes` 1 287 720 524 -> 1 930 905 296 | CONFIRMED | Own manifest model reproduces today's manifest (16 440 B) and gives the predicted bytes and SHA-1; `AssetInfos.json` carries no size (`id`, `name`, `file_name`, `asset_type`); `MeasureOutput` (`ConversionReport.cs:56-76`) sums file sizes: 1 287 720 524 + 643 180 860 + 3 912 = 1 930 905 296 (1841.45 MiB); today's 45 `Musics/*.wav` equal `data-extracted` (45/45 `cmp`). |
| 28 | Converter: 3 nullable fields after `AssetId`, `GetNullableBoolean`, `AudioWriter` unchanged | CONFIRMED | `SoundManifestReader.cs:12-25`, `:85-110`, `:165-189` (only non-null bool helper exists); `AudioWriter.cs:43-47,198-201` serialise every property, nulls kept. |
| 29 | DLL changes and line refs (`:145`, `:175`, `:359-386`, `:388-396`, `:402`, `:407`, `:412-422`, `:424-466`, `:471-475`, B4 `:266-277`), fade on Master `AlundraBgmFadeDirector.cs:256-258` | CONFIRMED | Read. `MusicPlayer.Play(SoundAsset, float, object owner)` exists (`MusicPlayer.cs:60`), `IsAlive`, `Stop` exist; `SoundAsset` default bus is Sfx (`SoundAsset.cs:20`), so Music must be set. |
| 30 | Engine slice needed (`LoopStartSample`, reader seek, `FillQueue` seek, per-update cap) | CONFIRMED | `SoundAsset.cs` has no loop point; `WavStreamReader.cs:90-97` only `Rewind`; `MusicPlayer.cs:273-300` loops while pending < 3 with no cap; DLL fake `FakeAudioBackend.cs:189` returns 0 pending, `:335` `OpenStream` null. |
| 31 | DLL tests: 88 `PlayCalls` lines (22/39+1 comment/27), 11 `CurrentVoiceForTests` (4 with `GetVoiceBus`), 38 `IsCurrentVoiceAlive`, 37 bgm `Register` sites, flip at `AlundraBgmFadeDirectorTests.cs:448`; SFX files do not move | CONFIRMED | Every claimed line list equals the files (`vtests.out`); no other test file uses the two accessors; `:448` asserts `IsLooped` for `bgm_042`, a one-shot (seqsim end 663). Warp tests use a fake SFX player, so their `PlayCalls` are music only. `AlundraAnimationSoundTests.cs:417` and `AlundraWorldProxyAudioInstallationTests.cs:268-269` are SFX. |
| 32 | Converter tests: T-D (45), T-E, golden (45) red; T-A, T-B, T-C green; fixture tests unchanged | CONFIRMED with the tail | `BgmFreshSoundSystemTests.cs:50-159`. Without the tail, T-D and golden are red on 38 only (one-shots unchanged). `SoundManifestReaderTests.cs:81-114` and `AudioWriterTests.cs:69-75` assert properties, not bytes. |
| 33 | Red-first values (25: 7272 / 5 344 920 / 13 186; 14: 14 552 / 10 695 720 / 27 204; 1: 7965 / 5 854 275 / 15 215; 45: 380 / 279 300 / 718; WAV 25: 38 766 884 vs 21 382 664; seam L-D red on 33/38) | CONFIRMED | Own values; raw last sample equals the pre-J1 sample on 2, 12, 14, 22, 23 (33 red). |

Sample tracks recomputed in full: 1, 7, 14, 25, 32, 35, 45 and one-shot 42 (in fact all 45).

## Corrections to carry

1. True continuation: "at once on the other **26**" (24 at sample 0; 18 and 41 at sample 1), not 29.
2. Loudness: "within 3.2 dB on **33** of 38"; a fifth exception, **34 (3.7 dB)**. Only 10 and 30 stay exceptions under a 1 s / -40 dBFS metric.
3. Seam without crossfade: the "no such jump in the original" set is 8 tracks; on 20 the original's own J2 step (1578) is also above p99 (1531).
4. Divergence cause: not established as period jitter (track 28 has constant periods and parts at once; a [J2,J3) loop parts within 1 sample on 24 tracks). Keep the facts, drop the cause.
5. Tail residue: the file ends at about 65-75 LSB peak; the next 60 frames peak at 51-63 LSB; 1-2 LSB only from about 200 frames after E.
6. Converter test red counts (T-D, golden) are 45 only if the tail is taken; 38 without.

## Missed side effects and hints (not blockers)

- `Music.IsAlive(track)` is a slot check (`MusicPlayer.cs:197`, `:344-360`); the slot is released only at the next
  `MusicPlayer.Update` when the voice died elsewhere (`:246-249`). Today `IsCurrentVoiceAlive` reads
  `AudioService.IsAlive(voice)` directly (`AlundraMusicPlayer.cs:402`). After an external voice stop (for example
  `AudioSystemComponent.cs:81` `Service.StopAll()`), B4 would see "alive" until the next update. No DLL path found that
  stops the music voice outside `StopCurrentVoice`; impact UNCONFIRMED.
- With the DLL fake (0 pending buffers, `FakeAudioBackend.cs:189`), a non-looped stream is read to its end inside
  `Play` and released at the first `Music.Update`; a looped one never leaves `FillQueue` without the cap. D-2 and the B4
  tests must not depend on `AudioService.Update` timing, or the fake must report pending buffers.
- `LoopDetected` stays true on the 7 one-shots (the end of track is a backward move): consumers must read `Looping`.
- One-shots are looped forever by today's DLL (`isLooped: true` for every track, `AlundraMusicPlayer.cs:384`); B
  makes them play once, like the original. Reachable one-shots (42, 36 per `loop-disc/a7scan.json`, not re-run) change
  audibly in game.
- Two oracle runs identical: only their `oracle_b.tsv` could be compared (equal); the repeat WAVs were deleted.

## Facts vs hypotheses

- Facts: every value above, recomputed from the produced files, the binary and the code.
- Hypotheses kept as such: voice-allocation cause of the 10/30/31/21 variations; the release behaviour of
  `0x80091134` (only the analyser's port is exercised); track 14's one-frame difference being inaudible; the pitch-guard
  hits being inside track 19's loop body.
