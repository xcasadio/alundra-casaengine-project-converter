# L-b value audit: option B (render to the second loop jump), 2026-10-05

Scope: slice L-b (data + converter + DLL) of `docs/plan-e19-opcodes.md` §1.2w, decisions D-E19-70, D-E19-72, D-E19-73.
Nothing tracked was edited, nothing committed, nothing written into `data-extracted/`, `alundra-project/` or
`D:\development\repo\Alundra Remake\`. The analyser extractor (submodule `db69b82`) was built in Release (git-ignored
`bin/`/`obj/`), snapshotted into `extractor-bin/` (SHA-256 in `extractor_bin_sha256.txt`) and run only into this
folder. Repository status before/after: `git_status_before.txt`, `git_status_after.txt` (analyser: same ignored set,
`AlundraTools/log.txt` still the empty file of 2026-10-03; engine: only the author's `CasaEngine.Launcher/Program.cs`;
parent HEAD moved `a046568` -> `5c26993` during the audit, by another session, `docs/plan-e19-opcodes.md` only).

## Summary (answer first)

- **Render rule (measured on 45 tracks, oracle built).** Fresh sound system per track (as X2). Per frame:
  `AdvanceSoundFrame`, then `RenderSamples(735)`. A backward `SeqPosition` with sequence flag bit 0 still set is a
  **loop jump**; with bit 0 clear it is the **end of track** (analyser `SoundManager.cs:1558-1562` then `:1302-1306`,
  binary `0x8008D77C`-`0x8008D8B4`, `0x8008E594`/`0x8008E5B4`).
  - Looping (38 tracks): keep the first-jump frame J1 (iteration 2's first frame) and continue; at the second jump J2
    **do not keep** that frame (iteration 3's first frame). File = frames `[0, J2)`, `Frames = J2`,
    `LoopStartFrame = J1`, `LoopStartSample = J1 x 735` (stereo sample frames at 44 100 Hz; data byte offset
    `J1 x 2940`). Today `RenderBgmTrackUntilLoop` appends a frame before testing it (`Program.cs:275-277`, test
    `:306-312`, `frame++; break`), so today's file is `[0, J1]` and today's `Frames = J1 + 1` (37 tracks; 14 capped at
    14400). Under B the loop region `[J1, J2)` stands for every later pass; the wrap plays sample `J2·735-1` then
    sample `J1·735`.
  - Cap `maxSeconds` 240 -> **480** (28 800 frames; largest J2 = 27 204 = 453.4 s, track 14). A first jump without a
    second one before the cap, or neither jump nor end before the cap, is a **failure** (not exported), so the
    console's `failed=0` proves coverage. Measured: no silence-grace stop can fire (longest silent run before J2:
    117 frames, track 22; the grace is 300).
  - One-shots (26, 36, 37, 38, 40, 42, 43): **recommend the release tail**: after the end-of-track frame E (kept, as
    today), keep rendering until 60 frames in a row are silent (no |s| > 64), then keep up to the last audible frame
    (`Frames = max(E+1, lastAudible+1)`). Measured tails: 113, 104, 14, 111, 2, 108, 92 frames (0.03-1.9 s). Today 26,
    36, 38, 42, 43 are cut while still loud (last-frame RMS 4290, 4046, 3158, 6008, 2424). After the tail only 1-2 LSB
    remain for 30 s. Track 44 stays silent and refused, unchanged.
- **Seam rule: bake a 735-sample linear crossfade** of the file's last frame toward the 735 samples just before J1,
  `out[J2·735-735+x] = round((in[J2·735-735+x]·(735-(x+1)) + in[J1·735-735+x]·(x+1)) / 735)`, x = 0..734, per
  channel (735 odd: no rounding ties). The file's last sample then **equals** sample `J1·735-1`, so the wrap is the
  junction the render itself made at J1.
  - Without it the wrap step `|S[J1·735] - S[J2·735-1]|` exceeds the track's own 99th-percentile step on 9 tracks where
    the original has no such step (1: 8089 vs p99 3087; 10: 3059 vs 1008; 11: 5553 vs 1170; also 13, 15, 16, 17, 20,
    21). With it the wrap step is exactly the natural J1 step on 38/38 (median 0.12 x p99 instead of 0.22).
  - Why it is needed: the end of iteration 2 does **not** equal the end of iteration 1 (refutes `loop-disc` §3.3
    "converge before their last frame on every track"): last-frame difference RMS >= the signal RMS on 18 tracks; the
    last frame is identical only on 2, 12, 14, 22 (there the crossfade is a byte no-op).
  - True continuation (render past J2): the audio after J2 equals the audio after J1 for the whole next pass only on
    2, 12, 14; above a 64 threshold it parts after 2205 (22), 2773 (34), 2948 (31), 25 799 (23), 63 945 (6), 79-142
    samples (9, 39, 45, 46) and at once on the other 29. Cause: the original's own +-1-frame quantisation (the lengths of passes 2-7
    vary by one frame on 32 tracks, constant only on 2, 6, 12, 14, 23, 28); the original never repeats sample-exactly either. Musically: 500 ms
    loudness windows of passes 3-7 stay within 3.2 dB of pass 2 on 34 of 38 tracks; **exceptions** 10 (19.5 dB, some
    passes have a quiet passage near +40 s and +48 s: pass 2 is quiet like passes 5 and 7, passes 3, 4, 6 are loud), 30
    (13.0 dB, pass 3 only), 31 (5.3 dB, passes 5-7), 21 (4.1 dB, pass 7). Fact in the analyser render; cause not
    established (hypothesis: SPU voice allocation history); not fixable by a seam rule.
- **Oracle**: 45 WAVs + `bgm.json` in `oracle/sound/` (`oracle_sha256.tsv`, `loops_final.tsv`). Valid by four checks:
  (1) the harness in "today" mode, with the real `RenderBgmTrackUntilLoop` and with its own loop, reproduces
  `data-extracted/sound/bgm/` and `bgm.json` byte for byte; (2) an independent Python derivation (frames cut from a
  separate long render, crossfade and tail rule recomputed, header written by Python) equals the oracle on 45/45;
  (3) the harness without crossfade equals the **unchanged** `--render-bgm --frames <Frames>` (one process per track)
  on 45/45; (4) every X2 file's data is a byte prefix of the new file (45/45). J1, J2, P3 and the one-shot end frames
  agree with `loop-disc/loops.tsv` on 45/45. Two oracle runs are identical.
- **Sizes**: BGM WAVs 699 960 120 B (700.0 MB, 667.5 MiB) -> **1 343 140 980 B (1343.1 MB, 1280.9 MiB)**, x1.919,
  +643 180 860 B per copy (three copies: `data-extracted/`, the remaster folder, `alundra-project/Musics`). Largest
  `bgm_014.wav` 79 979 804 B. `bgm.json` 13 470 -> 17 202 B, SHA-256 `c1e9d685…4e641` -> `3d176f4a…6efaf`.
- **Predicted diffs.** Re-extraction: **46 `M`** (45 WAVs + `sound/bgm.json`), nothing else (`predicted_extract_changes.txt`;
  a full extraction with today's code reproduces `data-extracted/` byte for byte, 4536 files). Export: **47 `M`**
  (45 `Musics/*.wav`, `Musics/bgm-manifest.json`, `report.json`; `predicted_export_changes.txt`, `export_values.tsv`);
  `AssetInfos.json` unchanged; report counters unchanged; `Metrics.OutputSizeBytes` 1 287 720 524 -> 1 930 905 296
  (+643 184 772, plus or minus the few bytes the previous `report.json` differs by).
- **Converter**: three nullable properties appended to `BgmEntry` after `AssetId`; `ReadBgm` reads them (plus a
  `GetNullableBoolean` helper); `AudioWriter` needs no code change (it serialises every property). **DLL**: plays
  through `AudioService.Music` with a cached `SoundAsset` per index (`IsStreaming`, `IsLooped = looping ?? true`,
  `LoopStartSample = loop_start_sample ?? 0`, Music bus, volume 1) instead of `PlayClip(..., isLooped: true)`
  (`AlundraMusicPlayer.cs:384-385`); needs the engine slice first (`SoundAsset.LoopStartSample`, seek in
  `WavStreamReader`, `MusicPlayer.FillQueue` seeking to it, a per-update buffer cap).
- **Tests that move**: 88 music `PlayCalls` assertion lines (MusicPlayer 22, BgmFadeDirector 39 + 1 comment, WarpDeparture
  27); 11 `CurrentVoiceForTests` and 38 `IsCurrentVoiceAlive` test references; 37 `provider.Register(..., FakeAudioClip("bgm…"))`
  sites; one semantic flip (`AlundraBgmFadeDirectorTests.cs:448`, track 42 is a one-shot); converter
  `BgmFreshSoundSystemTests` T-D (45 cases), T-E (1) and the golden theory (45) go red and must be re-pinned.
- **Pitch guard** console line becomes `sequencer=18` (today 9): all hits are track 19, inside its loop body, played twice.

## 1. Render rule

### 1.1 Facts the rule rests on

- Loop jump keeps flag bit 0: CC 99 = 30 with count 127 only rewrites the read position and the delay
  (binary `0x8008CB48` `sw zero,0x88(s0)`, `0x8008CB50` `sw v0,4(s0)`; analyser `SoundManager.cs:1878-1907`).
- End of track clears bit 0 and sets 4 (binary `0x8008D83C`-`0x8008D864`: `ori 0x200`, `ori 4`; key-off `jal 0x80093EF4` at
  `0x8008D898` and `0x8008D8A4`), then the frame driver resets and stores 0 in the flags (`0x8008E594` `andi v0,v0,4`,
  `0x8008E5B4` `sw zero,0x90(v0)`). Analyser: `SoundManager.cs:1558-1572` and `:1302-1306`. So after
  `AdvanceSoundFrame`, `(Flags & 1) == 0` means the sequence stopped. `0x80093EF4` loops over the voices of the sequence
  and calls `0x80091134` per voice (disassembly `80093ef4`-`80093f88`, analyser `FUN_80093ef4` `:2870-2886`); that it
  is a release and not a cut is shown only by the analyser's render (decaying tails), not traced to SPU registers.
- Harness measurement (`long/events.tsv`, `long7/events.tsv`): every looping track jumps with bit 0 set, every one-shot
  ends with `Flags = 0`; J1, J2 (and J3..J7) per track; no other backward move.

### 1.2 The rule, as it would be written in `ExtractDataFromBgm`/`RenderBgmTrackUntilLoop` (`Program.cs:177-322`)

1. `maxSeconds = 480` (`:185`).
2. Per frame (`:273-315`): `AdvanceSoundFrame`; read `SeqPosition`, `Flags`; `RenderSamples(735)`;
   `backward = pos < previous`.
   - `backward && (Flags & 1) != 0 && J1 >= 0` -> `J2 = frame`, **break before appending**.
   - append; silence grace as today (300 frames).
   - `backward && (Flags & 1) != 0` -> `J1 = frame` (kept), continue.
   - `backward && (Flags & 1) == 0` -> `End = frame` (kept), then the release tail (1.3).
3. After the loop: `J1 >= 0 && J2 < 0` -> failure; no jump and no end before the cap -> failure (`DeleteStaleBgm`,
   `failed++`, as `:238-243`).
4. Looping: crossfade (section 2). Then the statistics (`PeakLeft/Right`, `RmsLeft/Right`, `FirstAudibleFrame`) in one
   pass over the **written** samples (the record describes the file).
5. Record (`:153`, `:236`): today's ten fields unchanged in name, order and meaning (`LoopDetected` = a backward
   `SeqPosition` was seen: true on all 45, so only track 14 moves, false -> true), then appended `bool Looping`,
   `int? LoopStartFrame` (J1, `null` for a one-shot), `int? LoopStartSample` (J1 x 735, `null` for a one-shot).
   Largest value 10 695 720 (track 14): fits an int.

Expected console (harness, same code path): `BGM 44: rendered SILENT (123 frames, peak 0/0) - not exported`,
`Extracted BGM: rendered=45 silent=1 failed=0 (of 46)`, `pitch guard hits: sequencer=18 sfx=0 exporter=0`. BGM
phase about 55 s (today about 31 s).

### 1.3 One-shots: tail measured (`tails.tsv`)

| idx | end-of-track frame E | today Frames (E+1) | last frame RMS today (cut) | release frames kept | new Frames | new bytes |
|---|---|---|---|---|---|---|
| 26 | 1106 | 1107 | 4290.0 | 113 | 1220 | 3586844 |
| 36 | 496 | 497 | 4046.4 | 104 | 601 | 1766984 |
| 37 | 605 | 606 | 40.1 | 14 | 620 | 1822844 |
| 38 | 419 | 420 | 3157.5 | 111 | 531 | 1561184 |
| 40 | 540 | 541 | 18.5 | 2 | 543 | 1596464 |
| 42 | 663 | 664 | 6008.0 | 108 | 772 | 2269724 |
| 43 | 807 | 808 | 2423.5 | 92 | 900 | 2646044 |

- The first frame after E is louder than E on 26/36/42 (peaks 11974, 12716, 22065): the release starts there.
- "First silent frame" and "last audible frame + 1" differ by 1-4 frames on 36, 38, 40, 42 (the level hovers near 64),
  hence the 60-frame window then trim rather than "stop at the first silent frame".
- From about 200 frames after E only +-1-2 LSB remain (max 2 at +200, 1 at +600, still non-zero at +1799): digital zero is
  never reached, so a "until zero" rule would run to the cap.
- Recommendation: take the tail (faithful: the original keys off with a release at the end of track; the files would
  otherwise end abruptly at RMS 2400-6000 once played unlooped). Side effect for the DLL: a one-shot's stream stays
  alive for the tail (up to 1.9 s past the original's end of track), during which a new `PlaySequence` is ignored
  (B4) whereas the original would restart it. Short window; reachable one-shots are 42 (22 `0xA7` sites) and 36
  (Cave-138), per `loop-disc/a7scan.json` (not re-run).
- Without the tail: one-shot WAVs and their `Frames` stay byte-identical to today; the extraction diff drops to 39 `M`.

## 2. Seam rule

### 2.1 Measures (`seam_b.tsv`, `equiv.tsv`, `iters7.tsv`; renders `long/` to J3+60 and `long7/` to J7+60)

Steps are `max(|dL|, |dR|)` between consecutive stereo samples; p99/max are over the B file `[0, J2)`.

| idx | LS | J1 = LoopStartFrame | LoopStartSample | J2 = Frames | P2 / P3 | today Frames | seam step no xf / p99 / max | seam step xf735 (= J1 step) | true step at J2 | it2-end vs it1-end, last frame RMS err / signal | samples until \|true - loop\| > 64 | worst 500 ms window it3..7 vs it2 (dB) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 714 | 7965 | 5854275 | 15215 | 7250 / 7251 | 7966 | 8089 / 3087 / 13839 | 749 | 2063 | 6505.4 / 4580.2 | 0 | 2.0 |
| 2 | 114 | 3992 | 2934120 | 7870 | 3878 / 3878 | 3993 | 307 / 1131 / 8589 | 307 | 307 | 0.0 / 1766.0 | never | 0.0 |
| 3 | 18 | 2313 | 1700055 | 4607 | 2294 / 2294 | 2314 | 279 / 2882 / 16489 | 289 | 467 | 875.7 / 3059.6 | 0 | 1.8 |
| 4 | 29 | 5874 | 4317390 | 11718 | 5844 / 5845 | 5875 | 2042 / 3380 / 17537 | 5542 | 3808 | 4679.1 / 4175.0 | 0 | 1.3 |
| 5 | 35 | 8019 | 5893965 | 16002 | 7983 / 7984 | 8020 | 640 / 2963 / 13051 | 866 | 1096 | 5572.2 / 5373.9 | 0 | 1.5 |
| 6 | 35 | 5725 | 4207875 | 11415 | 5690 / 5690 | 5726 | 1 / 1161 / 4119 | 0 | 0 | 0.8 / 76.4 | 63945 | 0.7 |
| 7 | 83 | 10638 | 7818930 | 21192 | 10554 / 10554 | 10639 | 308 / 2051 / 9325 | 258 | 254 | 224.7 / 1243.8 | 0 | 1.8 |
| 8 | 29 | 5290 | 3888150 | 10551 | 5261 / 5261 | 5291 | 46 / 2398.2 / 7531 | 138 | 125 | 160.6 / 2727.3 | 0 | 0.8 |
| 9 | 35 | 6402 | 4705470 | 12768 | 6366 / 6367 | 6403 | 22 / 1923 / 10994 | 19 | 19 | 5.8 / 46.6 | 140 | 1.2 |
| 10 | 63 | 5955 | 4376925 | 11848 | 5893 / 5893 | 5956 | 3059 / 1008 / 4244 | 610 | 216 | 3309.5 / 1803.3 | 0 | 19.5 |
| 11 | 204 | 5455 | 4009425 | 10707 | 5252 / 5251 | 5456 | 5553 / 1170 / 6622 | 275 | 169 | 2306.6 / 1469.5 | 0 | 3.1 |
| 12 | 126 | 3454 | 2538690 | 6782 | 3328 / 3328 | 3455 | 137 / 1340 / 4051 | 137 | 137 | 0.0 / 817.5 | never | 0.0 |
| 13 | 127 | 6567 | 4826745 | 13007 | 6440 / 6440 | 6568 | 702 / 595 / 4350 | 13 | 64 | 346.4 / 241.3 | 0 | 2.5 |
| 14 | 1899 | 14552 | 10695720 | 27204 | 12652 / 12652 | 14400 | 1347 / 2417 / 13314 | 1347 | 1347 | 0.0 / 2617.8 | never | 0.0 |
| 15 | 87 | 7644 | 5618340 | 15202 | 7558 / 7557 | 7645 | 3234 / 1722 / 9687 | 4203 | 832 | 3234.3 / 3229.3 | 0 | 2.6 |
| 16 | 441 | 8178 | 6010830 | 15914 | 7736 / 7737 | 8179 | 1641 / 1614 / 9327 | 164 | 429 | 960.3 / 707.7 | 0 | 1.7 |
| 17 | 63 | 7949 | 5842515 | 15834 | 7885 / 7885 | 7950 | 3598 / 2034 / 12623 | 405 | 486 | 1681.4 / 3578.0 | 0 | 1.9 |
| 18 | 35 | 5117 | 3760995 | 10199 | 5082 / 5081 | 5118 | 60 / 1301 / 10248 | 0 | 0 | 91.2 / 59.9 | 1 | 1.4 |
| 19 | 104 | 3835 | 2818725 | 7566 | 3731 / 3730 | 3836 | 515 / 2198 / 9257 | 1612 | 946 | 2348.8 / 2296.7 | 0 | 1.0 |
| 20 | 47 | 5330 | 3917550 | 10614 | 5284 / 5283 | 5331 | 2690 / 1531 / 5699 | 1268 | 1578 | 3500.7 / 2249.0 | 0 | 1.3 |
| 21 | 35 | 6754 | 4964190 | 13474 | 6720 / 6719 | 6755 | 1711 / 1689 / 8322 | 446 | 1337 | 2451.4 / 1997.4 | 0 | 4.1 |
| 22 | 35 | 7071 | 5197185 | 14107 | 7036 / 7035 | 7072 | 1519 / 1348 / 6133 | 1519 | 1519 | 0.0 / 2090.5 | 2205 | 2.9 |
| 23 | 44 | 7404 | 5441940 | 14764 | 7360 / 7360 | 7405 | 45 / 2547 / 12279 | 45 | 45 | 0.5 / 51.5 | 25799 | 1.9 |
| 24 | 599 | 4599 | 3380265 | 8598 | 3999 / 4000 | 4600 | 670 / 2779 / 13993 | 301 | 847 | 532.5 / 676.5 | 0 | 3.2 |
| 25 | 1358 | 7272 | 5344920 | 13186 | 5914 / 5913 | 7273 | 705 / 1583 / 7226 | 219 | 160 | 802.7 / 699.9 | 0 | 2.1 |
| 27 | 487 | 7263 | 5338305 | 14039 | 6776 / 6775 | 7264 | 244 / 2784 / 19593 | 31 | 29 | 156.4 / 848.5 | 0 | 1.9 |
| 28 | 148 | 5871 | 4315185 | 11594 | 5723 / 5723 | 5872 | 121 / 1429 / 4865 | 0 | 22 | 171.0 / 478.5 | 0 | 1.2 |
| 29 | 103 | 5282 | 3882270 | 10462 | 5180 / 5179 | 5283 | 2519 / 3078 / 18081 | 254 | 2759 | 1976.6 / 1743.1 | 0 | 1.7 |
| 30 | 483 | 10215 | 7508025 | 19947 | 9732 / 9731 | 10216 | 145 / 2141 / 11611 | 404 | 457 | 1097.1 / 875.7 | 0 | 13.0 |
| 31 | 441 | 6000 | 4410000 | 11559 | 5559 / 5559 | 6001 | 13 / 2306 / 18240 | 11 | 0 | 6.5 / 22.5 | 2948 | 5.3 |
| 32 | 168 | 6220 | 4571700 | 12273 | 6053 / 6052 | 6221 | 333 / 1534 / 6222 | 243 | 0 | 293.2 / 165.8 | 0 | 1.7 |
| 33 | 50 | 5900 | 4336500 | 11750 | 5850 / 5850 | 5901 | 565 / 1180 / 5613 | 617 | 202 | 4016.4 / 2907.9 | 0 | 1.3 |
| 34 | 109 | 3746 | 2753310 | 7383 | 3637 / 3636 | 3747 | 11 / 1167 / 5786 | 3 | 0 | 11.0 / 22.0 | 2773 | 3.7 |
| 35 | 6938 | 13744 | 10101840 | 20549 | 6805 / 6806 | 13745 | 358 / 1572 / 10517 | 316 | 401 | 84.8 / 792.1 | 0 | 1.6 |
| 39 | 35 | 2255 | 1657425 | 4474 | 2219 / 2220 | 2256 | 57 / 1261 / 5490 | 2 | 12 | 36.9 / 37.3 | 79 | 1.4 |
| 41 | 35 | 2943 | 2163105 | 5853 | 2910 / 2909 | 2944 | 53 / 591 / 2078 | 0 | 0 | 192.2 / 125.5 | 1 | 1.9 |
| 45 | 42 | 380 | 279300 | 718 | 338 / 339 | 381 | 398 / 4144 / 11613 | 384 | 388 | 10.2 / 1702.9 | 142 | 0.4 |
| 46 | 42 | 380 | 279300 | 718 | 338 / 339 | 381 | 398 / 4105 / 9841 | 384 | 388 | 10.2 / 1702.9 | 142 | 0.4 |

Reading it:
- **No crossfade**: wrap step > p99 on 10 tracks (1, 10, 11, 13, 15, 16, 17, 20, 21, 22); on 22 it is its own natural
  step (ends identical), so 9 tracks get a step the original does not have at J2 (true step at J2 below p99 on all 9
  except 20, 1578 ~ p99 1531). > 2 x p99 on 1, 10, 11. Never above the track's own maximum step.
- **Crossfade 735**: wrap step = `S[J1·735] - S[J1·735-1]` on 38/38 (a step the file already contains at the first-pass
  junction). Above p99 on 4 (5542), 15 (4203), 22 (1519, no-op): the natural attack at the loop entry. The original's own
  step at J2 is above p99 on 4, 20, 22.
- The crossfade alters only the last 735 stereo samples (checked: changed samples lie in `[J2·735-735, J2·735)` on every
  track); it is a byte no-op on 2, 12, 14, 22; it changes at most 138 samples on 23 (by 1 LSB). Largest change: |delta|
  13 495 (track 4), RMS 3580 (track 1) over the 16.7 ms.
- **Longer K** gains nothing at the wrap (the step is already the natural J1 step for any K) and alters more of the end;
  K = 735 matches the original's one-frame quantisation. Recommend K = 735.
- **True continuation**: see the summary. `post_first_gt0/16/64/256` and RMS errors per track in `seam_b.tsv`.
- **Steady state over six passes** (`iters7.tsv`): pass lengths vary by one frame on 32 of 38 tracks (constant on 2, 6, 12,
  14, 23, 28; `periods_it2_to_it7`); passes 3-7 are sample-identical to pass 2 on 2, 12, 14, and on 6 from pass 3 on
  (pass 2 differs by 0.7 dB at most). Track 10: windows near +2400 and +2880 frames, passes 1..7 at
  -38.1/-45.1/-38.1/-38.1/-45.1/-38.1/-45.0 dB (left), so no single pass is "the" steady state there.

### 2.2 Recommendation

Seam rule = the 735-sample crossfade above, applied after the render, before the statistics, only when `Looping`. It is
deterministic integer arithmetic (C# `(num ± 367) / 735` with truncation equals round-to-nearest; Python identical,
checked 45/45).

## 3. Oracle and data

### 3.1 Files

- `oracle/sound/bgm/*.wav` (45), `oracle/sound/bgm.json`; per track `oracle_sha256.tsv` (SHA-256, bytes, Frames, Looping,
  LoopStartFrame, LoopStartSample, J1, J2, EndOfTrackFrame, today's Frames/bytes/SHA-256, the four checks, crossfade
  extent, today's data SHA-256 and the new file's prefix SHA-256); `loops_final.tsv` (LS, J1, J2, J3, P1-P3, loop
  sample bounds, tail frames, seam measures, users).
- `run-b-raw/` = same rule without crossfade (equals `renderbgm/*_csharp_render.wav` from the unchanged CLI);
  `run-b-notail/` = crossfade, no tail; `run-x2-real/`, `run-x2-mine/` = today's rule (equal to `data-extracted/`).

### 3.2 New `bgm.json` fields (oracle, appended after `FirstAudibleFrame`)

`"Looping": true, "LoopStartFrame": J1, "LoopStartSample": J1*735` on 38 tracks; `"Looping": false, "LoopStartFrame":
null, "LoopStartSample": null` on 26, 36, 37, 38, 40, 42, 43. Fields that move in today's ten: `Frames`,
`DurationSeconds`, `RmsLeft`, `RmsRight` on 45; `PeakLeft` on 16, `PeakRight` on 15; `LoopDetected` on 1 (14);
`FirstAudibleFrame` on none.

### 3.3 Predicted re-extraction diff (`predicted_extract_changes.txt`, `extract_values.tsv`)

46 lines: `M sound/bgm.json` and `M sound/bgm/bgm_NNN.wav` for 1-43, 45, 46. No `A`, no `D`. Nothing else: a full
extraction with the db69b82 Release (`full-today-run/`, 46 s, exit 0, `sequencer=9`) equals `data-extracted/` by
`diff -rq` (4536 files, 0 differences), and the change is confined to `ExtractDataFromBgm` (last phase, `Program.cs:150`),
which writes only `sound/bgm/*.wav` and `sound/bgm.json`.

## 4. Converter and DLL

### 4.1 Converter (precedent ADR-0003)

- `Readers/SoundManifestReader.cs:12-25` `BgmEntry`: append after `AssetId`: `bool? Looping`, `int? LoopStartFrame`,
  `int? LoopStartSample` (null = absent from an older `bgm.json`, or no loop for a one-shot; `Looping` tells which).
- `ReadBgm` (`:85-110`): `Looping = GetNullableBoolean(element, "Looping")` (new helper: `True`/`False` -> value, else
  null), `LoopStartFrame/LoopStartSample = GetNullableInt32(...)` (`:170-173`).
- `Writers/AudioWriter.cs`: no code change (it serialises every `BgmEntry` property, `:43-47`, `:198-201`); the class doc
  `:28-33` may mention the loop fields.
- Manifest: `"asset_id"`, then `"looping"`, `"loop_start_frame"`, `"loop_start_sample"` (CRLF, indented,
  `predicted/bgm-manifest.json`, 20 352 B, SHA-1 `8f302c1a90fed03313c54b5755fb525487989048`, SHA-256 `3e2db0dc…ebce5`;
  today 16 440 B, SHA-1 `509e8c68…73d1`). The model reproduces today's manifest byte for byte from today's `bgm.json`.
  From an old `bgm.json` the three fields are written `null` (`predicted/bgm-manifest.from-old-bgm-json.json`).

### 4.2 Predicted export diff (`predicted_export_changes.txt`, `export_values.tsv`)

47 `M`: 45 `Musics/bgm_NNN.wav` (1-43, 45, 46), `Musics/bgm-manifest.json`, `report.json`. `AssetInfos.json` unchanged
(same 45 files, names and ids). Report: counters unchanged (`Audio.Bgm` 45, `Audio.WavCopied` 1041, `Verify.*`);
`Metrics.OutputSizeBytes` 1 287 720 524 -> 1 930 905 296, `OutputSizeMegabytes` 1228.07 -> 1841.45, `OutputFileCount`
23 744 unchanged; timings. `Musics/bgm_044.wav` (stale since X2) unchanged and unreferenced. Today's 45
`Musics/*.wav` equal `data-extracted/sound/bgm/*.wav` (checked).

### 4.3 DLL (`Alundra/Scripts/AlundraMusicPlayer.cs`)

- `BgmManifestEntry` (`:471-475`): add `[JsonPropertyName("looping")] bool? Looping`,
  `[JsonPropertyName("loop_start_sample")] long? LoopStartSample`.
- `LoadBgmManifest` (`:424-466`) builds, once per attach, `Dictionary<int, SoundAsset>` replacing
  `_assetIdBySoundIndex` (`:145`): `new SoundAsset { AudioFileAssetId = id, IsStreaming = true, IsLooped = Looping ?? true,
  LoopStartSample = LoopStartSample ?? 0, BusName = AudioBusNames.Music, Volume = AudioVoiceParameters.MaxVolume }`
  (ADR-0003 fallback: missing fields keep today's whole-file loop, one warning).
- `StartVoice` (`:359-386`): `_currentTrack = _audioService.Music.Play(asset, 0f, owner: this)` instead of
  `ClipProvider.GetClip` + `PlayClip(clip, Music, new AudioVoiceParameters(MaxVolume, 0, 0, isLooped: true), owner: this)`
  (`:371-385`). Owner stays `this` (D-C-5). No fade-in (D-C-3).
- `_currentVoice` (`:175`, `AudioVoiceHandle`) -> `MusicTrackHandle _currentTrack`; `StopCurrentVoice` (`:388-396`) ->
  `Music.Stop(_currentTrack)`; `IsCurrentVoiceAlive` (`:402`) -> `Music.IsAlive(_currentTrack)`; `CurrentVoiceForTests`
  (`:407`) -> `MusicTrackHandle`; `ResetForTests` (`:412-422`). Class doc `:96-105` (route B, D-C-1) superseded: parent ADR.
- Unchanged: B4 (`:266-277`), the fade director on the Master bus (`AlundraBgmFadeDirector.cs:256-258`).
- Prerequisite engine slice (not L-b): `SoundAsset.LoopStartSample` (`SoundAsset.cs`, `.sound` field `loop_start_sample`,
  default 0), `WavStreamReader` seek to a sample frame (`WavStreamReader.cs:90-97` has only `Rewind`), `MusicPlayer.FillQueue`
  seeking there instead of `Rewind()` (`MusicPlayer.cs:273-300`), a per-update buffer cap (the DLL fake answers 0 pending
  buffers, `Alundra.Tests/FakeAudioBackend.cs:189`, so an uncapped looped `FillQueue` would never return), an engine ADR.
  Streaming margin stays 3 x 16 384 B = about 279 ms at 44.1 kHz stereo (`MusicPlayer.cs:23-26`).

### 4.4 DLL tests that move (file:line)

- Music `PlayCalls` (music voices become streaming voices, `PlayClip` is no longer called):
  - `AlundraMusicPlayerTests.cs` 22: 211, 215, 216, 221, 235, 262, 266, 274, 304, 314, 323, 332, 333, 378, 379, 380,
    384, 453, 490, 494, 495, 533.
  - `AlundraBgmFadeDirectorTests.cs` 39 (+ comment 558): 130, 137, 161, 168, 190, 195, 212, 224, 243, 248, 274, 294,
    300, 336, 383, 419, 424, 431, 438, 446, 447, 448, 457, 517, 523, 528, 529, 550, 551, 575, 582, 587, 610, 616,
    620, 640, 651, 655, 686.
  - `AlundraWarpDepartureTests.cs` 27: 493, 502, 508, 520, 521, 562, 567, 568, 599, 609, 616, 624, 625, 658, 671, 699,
    709, 751, 762, 767, 778, 779, 809, 820, 826, 834, 835.
  - Not moving (SFX only): `AlundraSoundPlayerTests.cs` (25), `AlundraSoundPlayerStereoVolumeTests.cs` (11),
    `AlundraAnimationSoundTests.cs:417`, `AlundraWorldProxyAudioInstallationTests.cs:268-269`.
- Semantic flip: `AlundraBgmFadeDirectorTests.cs:448` asserts track 42 looped; 42 is a one-shot (its fixture must carry
  `looping: false`, the assertion becomes "not looped").
- `CurrentVoiceForTests` (type changes), 11: `AlundraBgmFadeDirectorTests.cs` 244, 249, 449;
  `AlundraMusicPlayerTests.cs` 219, 236, 268, 276, 381, 421; `AlundraWarpDepartureTests.cs` 700, 712. Of these, the
  `service.GetVoiceBus(...)` sites (BgmFade 449, MusicPlayer 220, 381, 421) need the track's voice or bus from the
  engine (or the recorded streaming voice).
- `IsCurrentVoiceAlive` (type unchanged, implementation changes), 38: `AlundraBgmFadeDirectorTests.cs` 138, 169, 384,
  418, 423, 430, 437, 445, 455, 522, 552, 576, 581, 588, 611, 615, 621, 645, 650, 656; `AlundraMusicPlayerTests.cs`
  275, 312, 315, 324, 334, 420; `AlundraWarpDepartureTests.cs` 522, 569, 608, 614, 626, 669, 711, 769, 780, 819, 824, 836.
- Fake: `FakeAudioClipProvider.OpenStream` (`FakeAudioBackend.cs:335`, returns null) must serve a seekable WAV for music;
  37 `provider.Register(..., new FakeAudioClip("bgm…"))` sites become stream registrations (BgmFade 121, 151, 181, 206,
  234, 258, 311, 371, 372, 506, 507, 540, 565, 601, 631; MusicPlayer 198, 255, 290, 291, 292, 370, 400, 435, 473, 474,
  512; Warp 483, 484, 545, 589, 590, 649, 690, 741, 742, 799, 800); a recorder of `CreateStreamingVoice` (`:160-181`)
  and of the opened asset ids replaces `PlayCalls` and the clip-name checks.
- Real-manifest lookups (`AlundraMusicPlayerTests.cs:188-198`, `AlundraWarpDepartureTests.cs:891-899`): unchanged.

### 4.5 Converter tests that move

`BgmFreshSoundSystemTests.cs` (X2): T-D `Frames_PerTrack_MatchTheAnnex` (`:113-128`) red on 45 (all `Frames` change,
one-shots through the tail); T-E `Track1_IsByteIdenticalToTheAnnex` (`:130-143`) red; golden
`Track_IsByteIdenticalToTheFreshSoundSystemRender` (`:145-159`) red on 45. They must be re-pointed to an L-b annex
(T-E as a prefix check: SHA-256 of the data bytes `[44, 44 + 7966·2940)` of `bgm_001.wav` =
`TodayDataSHA256` of track 1 in `oracle_sha256.tsv`). T-A (`:50-63`), T-B (`:65-95`), T-C (`:97-111`) stay green (X2 data is a
byte prefix, `FirstAudibleFrame` unchanged, 45 entries). Fixture tests `SoundManifestReaderTests.cs:81-114` and
`AudioWriterTests.cs:239-250`: unchanged (old fixtures read null).

## 5. Red-first tests with values

Converter, real data (early return without `data-extracted/`, as `BgmFreshSoundSystemTests`):
- **L-A** `[Theory]` 38 rows `(idx, J1, J2)` from the table in 2.1: `Looping == true`, `LoopStartFrame == J1`,
  `LoopStartSample == J1*735`, `Frames == J2` (e.g. 25: 7272, 5 344 920, 13 186; 14: 14 552, 10 695 720, 27 204;
  1: 7965, 5 854 275, 15 215; 45/46: 380, 279 300, 718). Red today: no `Looping` property, `Frames` = J1+1 (14: 14400).
- **L-B** `[Theory]` 7 one-shots: `Looping == false`, `LoopStartFrame`/`LoopStartSample` null, `Frames` 26:1220, 36:601,
  37:620, 38:531, 40:543, 42:772, 43:900 (today 1107, 497, 606, 420, 541, 664, 808).
- **L-C** WAV length = 44 + Frames x 2940 for all 45 (`Bytes` column; 25: 38 766 884, today 21 382 664).
- **L-D seam** on the real WAV, 38 looping tracks: last stereo frame == stereo frame at `LoopStartSample - 1`
  (25: both (-732, -123); raw render: last (192, 89)). Red without the crossfade on 33 of 38 (equal by chance only on
  2, 12, 14, 22, 23).
- **L-E guard** (green before and after): data prefix `[0, X2Frames·2940)` SHA-256 = `TodayDataSHA256`.
- **Golden**: 45 SHA-256 of `oracle_sha256.tsv`; `bgm.json` SHA-256 `3d176f4a2df0dc7ad0d8f1a2b88fed1e919d4e4b4aab173c6028192ca0b6efaf`.

Converter, fixtures: `ReadBgm` reads (25: true / 7272 / 5 344 920; 42: false / null / null; absent: null / null / null);
`AudioWriter` writes `looping`, `loop_start_frame`, `loop_start_sample` after `asset_id` with those values and nulls.

DLL (after the engine slice):
- **D-1** fixture manifest `{25: looping true, loop_start_sample 5344920}`, `PlayMapMusic(389)` then a frame close: one
  streaming music track, asset `IsStreaming`, `IsLooped`, `LoopStartSample == 5344920`, Music bus, volume 1, no
  `PlayClip`. Red today (one `PlayClip`, whole-file loop).
- **D-2** fixture `{42: looping false, loop_start_sample null}`, `0xA7 42,0` then `0xA5`: not looped. Red today
  (`AlundraBgmFadeDirectorTests.cs:448`).
- **D-3** manifest without the fields: looped, loop start 0, one warning.
- **D-4** real export, index 25: the started asset's `LoopStartSample == 5344920`.
- **D-5** B4 unchanged: a live track is not restarted.

## 6. Facts and hypotheses

- Facts (measured or read): every value in the tables; the binary addresses above (disassembly `loop-disc-verify/snd_dis.txt`,
  re-read for `0x8008CB48/50`, `0x8008D83C-0x8008D8A4`, `0x8008E594/5B4`, `0x80093EF4`).
- Track 14 follows the analyser (J1 14 552, J2 27 204); the binary gives 14 551 / 27 202 (`loop-disc-verify` correction 1,
  author's call, inaudible).
- Hypotheses: the pass-to-pass variations of 10, 21, 30, 31 come from voice allocation; the release after end of track
  matches the binary's `0x80091134` (only the analyser's port is exercised); 60 Hz sequencer tick on PAL (open since
  `loop-disc` §1.4).

## 7. Scripts and outputs (this folder)

| file | role |
|---|---|
| `harness/` (`Program.cs`, `harness.csproj`) | reflection harness: `long` renders past the jumps; `oracle` with rules `x2-real`, `x2-mine`, `b` (`--xf`, `--tail audible60`, `--cap`) |
| `extractor-bin/`, `extractor_bin_sha256.txt` | Release snapshot of the analyser extractor at `db69b82` |
| `long/`, `long7/` | renders to J3+60 (1.9 GB) and to J7+60 (4.3 GB), `events.tsv` |
| `oracle/`, `run-b-raw/`, `run-b-notail/`, `run-x2-real/`, `run-x2-mine/`, `renderbgm/` | oracle and cross-check runs (logs `run-*.log`; the repeat runs `run-b-tail`, `run-x2-mine2`, `run-x2-mine3` were byte-identical to `oracle`/`run-x2-mine` by `diff -rq` and their WAVs were removed) |
| `full-today-run/`, `full-today/` | full extraction with today's code (equals `data-extracted/`) |
| `seam_b.py` -> `seam_b.tsv`, `tails.tsv` | seam, continuation and tail measures |
| `equiv.py`, `equiv_coarse.py` -> `equiv.tsv`, `equiv_coarse_w6.tsv`, `equiv_coarse_w30.tsv` | loudness-envelope comparisons |
| `iters7.py` -> `iters7.tsv` | six steady-state passes |
| `oracle_check.py` -> `oracle_sha256.tsv`, `loops_final.tsv` | the four oracle checks |
| `predict_lb.py` -> `predicted_extract_changes.txt`, `extract_values.tsv`, `predicted_export_changes.txt`, `export_values.tsv`, `predicted/` | predicted diffs |
| `mk_tables.py` -> `tables.md` | tables of this note |
