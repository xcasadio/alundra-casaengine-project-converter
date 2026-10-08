# BGM loop points (D-E19-70, O-E19-62): read-only discovery, 2026-10-05

Nothing was built, run (extractor, converter, game), exported, committed or edited in the repository. Only Python
models over `ALUN_CD.EXE`, `SOUND.BIN`, `data-extracted/sound/bgm*` and `alundra-project/Maps/*.json` (read only), plus
`ilspycmd` on the MonoGame 3.8.5.1 DLL from the NuGet cache. Scripts and outputs are in this folder (section 8).

## Summary (answer first)

- **Binary rule.** The BGM sequencer is Sony libsnd's.
  - Controller 99 = 20 (0x14, the loop start) stores the read position after the delta that follows it
    (`0x8008CAB0`-`0x8008CAD0`).
  - The loop count is latched once by controller 98 (`0x8008C96C`) or by controller 6 (`0x8008CD08`). Alundra uses
    127 on every looping track: CC 98 on 36 tracks, CC 6 on tracks 45 and 46.
  - Controller 99 = 30 (0x1E) with a count of 0x7F or more jumps back with a delay of 0 (`0x8008CB3C`-`0x8008CB50`).
    The event at the loop position runs in the jump frame, at the same tick. Nothing else is reset at the jump:
    tempo, channel volume, pan, program, bend, held notes, voices and reverb all carry over.
  - There is one loop pointer per sequence, so loops cannot nest. Every looping track has exactly one start
    marker, one count and one end marker.
  - A track without markers reaches its end-of-track meta. `PlaySeq(seq, 1, 1)` (`0x80049B60`) gave it a play
    count of 1 (`0x8008F10C`), so it **plays once and stops**: voices are keyed off (`0x8008D7DC`-`0x8008D8B4`) and
    the sequence is reset (`0x8008E594` -> `0x8008F2E8`).
  - Counts: **38 tracks loop** (1-25, 27-35, 39, 41, 45, 46) and **8 play once** (26, 36, 37, 38, 40, 42, 43, 44).
- **Per-track values.** They are in `loops.tsv`, computed with the timing model that predicts every `bgm.json`
  `Frames` exactly.
  - Track 25:
    - Intro: frames [0, 1358), tick 11510, 22.63 s.
    - Iteration 1: [1358, 7272).
    - First jump J1 = 7272 = today's `Frames` - 1.
    - Second jump J2 = 13186.
    - Loop period: 5914 frames = 98.57 s. It is not 7273: 7273 frames (121.2 s) is the port's whole-file period.
  - Sample offsets use 44100 Hz (not 48 kHz), with 735 samples per frame: `SpuMixerSoundPlaybackBackend.cs:18`,
    `Program.cs:255`.
- **The rendered WAV must change. Loop points alone are not enough.**
  1. On **10 tracks** (1, 7, 10, 15, 18, 21, 25, 31, 32, 41), the first loop iteration (the only one the WAV holds)
     sounds different from every later iteration of the original. At the jump the sequence keeps the loop end's
     channel volume and pan, and its held notes, instead of the state it had when it first entered the loop from
     the intro.
     - Track 7 (overworld, 20 maps) after the first pass: channel 8 is at volume 0 (48 notes muted), and channel 4
       is at 21 instead of 100 (197 notes).
     - Track 25: channel 9 is at 100 instead of 110 (14 notes), channel 5 is panned 15 instead of 64 (6 notes), and
       key 60 is held across the jump.
  2. On 26 tracks, frame quantisation moves events by ±1 frame between iterations. A loop region cut from
     iteration 1 is therefore not sample-equal to what follows the jump.
  3. The naive seam [LS, J1) cuts the voices that sound at the jump and plays the intro's tails instead. On 30 of
     the 37 measurable tracks, the step at the naive seam is more than twice the step at the true continuation.
  4. Track 14's WAV does not even hold one loop. Its first jump comes at 242.5 s, past the extractor's 240 s cap
     (`Program.cs:192`).
- **Recommended data change: option B, the steady-state render.** The extractor keeps rendering after the first
  jump, up to the second one.
  - The file holds frames [0, J2): the intro, then iteration 1, then iteration 2.
  - `bgm.json` gains `LoopStartFrame` = J1 and `LoopStartSample` = J1 × 735. The loop end is the end of the file.
  - The model shows the state at J1 equals the state at J2 on all 38 tracks, so iteration 2 is the steady state.
  - It needs the 240 s cap raised to at least 454 s (track 14: J2 = 27204 frames).
  - The jitter makes a baked crossfade of about one frame (735 samples) at the loop end advisable. It blends toward
    the samples just before J1, which are the end of iteration 1 and match the end of iteration 2.
  - Cost: the BGM WAVs grow from 667.9 MB to 1279.7 MB per copy.
  - Option C, render past the jump by W frames plus a tail margin and loop [LS+W', J1+W'), is smaller. It needs a
    per-track divergence analysis inside the extractor.
- **One-shot tracks.** Write `Looping = false` for them. Optionally render the release tail: 26, 36, 38, 42 and 43
  end at RMS 2400-6000 today.
- **Playback.** MonoGame 3.8.5.1 DesktopGL ignores `SoundEffect` loop points (`PlatformInitializePcm` drops
  `loopStart`/`loopLength`, decompiled), and the engine's resident path can only loop the whole clip.
  - The engine already streams music (`MusicPlayer`, ADR-0001), and its loop is "rewind to sample 0"
    (`MusicPlayer.cs` `FillQueue`, `WavStreamReader.Rewind`).
  - Minimal engine change:
    - `SoundAsset.LoopStartSample`, an optional `.sound` field that defaults to 0, which is today's behaviour.
    - `WavStreamReader` seeks to a sample frame.
    - `MusicPlayer.FillQueue` seeks to the loop start instead of rewinding.
    - The buffers submitted per update are bounded, as ADR-0039 did for the stereo voices.
    - An engine ADR records it. `IAudioBackend` does not change.
  - Minimal DLL change: `AlundraMusicPlayer` plays through `AudioService.Music` with an in-memory `SoundAsset`
    (asset id, `IsStreaming`, `IsLooped` = the manifest's looping flag, `LoopStartSample`), instead of `PlayClip`
    with `isLooped: true` (`AlundraMusicPlayer.cs:384`). This supersedes D-C-1 (route B) of `plan-e11c-musique.md`.
  - Converter: append the new fields to `BgmEntry` and `SoundManifestReader.ReadBgm`, following the precedent of
    the nullable sound-effect fields (ADR-0003).
- **Chain effect today.** Every map with music replays its intro at every loop and hits a seam click.
  - The longest intros:
    - Track 35 (Fairy cave and Meia Dream, maps 158, 161, 440-442, 468): 115.6 s of intro every 229 s.
    - Track 14 (Arena Melzas 2, map 471): 31.65 s of intro, and its loop is truncated.
    - Track 25 (maps 389 and 390): 22.6 s.
    - Track 27 (Kline's Nightmare): 8.1 s.
    - Track 30 (48 maps): 8.05 s.
    - Track 16 (137 maps): 7.35 s.
    - Track 31 (13 maps): 7.35 s.
  - Two one-shots loop when they should play once: 42 (22 boss and dream sites, `0xA7 42,0`) and 36 (Cave-138).
  - After the fix, 10 tracks will also sound different from today in their later loops. The port has only ever
    played iteration 1.
- **Order.** Do this together with X2 (D-E19-69: a clean sound system per track), in the same extractor slice, so
  there is only one re-extraction, mirror and full export.

## 1. The binary rule, complete (ALUN_CD.EXE France, capstone via `../psxdis.py`, `../e19m0-disc/dd.py`)

### 1.1 Dispatch and the sequence state

- **Controller dispatch.** `0x8008C1B8` reads the controller and its value. Controllers up to 0x79 jump through the
  table at `0x8002A83C`:
  - Controller 6 goes to `0x8008C264`, which calls `0x8008CC70` (data entry).
  - Controller 98 goes to `0x8008C3EC`, which calls `0x8008C918`.
  - Controller 99 goes to `0x8008C40C`, which calls `0x8008CA40`.
  - Controller 100 goes to `0x8008C42C`, which calls `0x8008CB88`.
  - These are table words at `0x8002A854`, `0x8002A9C4`, `0x8002A9C8` and `0x8002A9CC`.
- **Sequence state.** The state lives at `*(0x801F6CE8 + 4·seq) + 0xAC·track` (the stride is computed at
  `0x8008CA4C`-`0x8008CA8C`). Its fields:

  | offset | content |
  |---|---|
  | `+0x04` | read pointer |
  | `+0x08` | start pointer |
  | `+0x0C` | loop pointer |
  | `+0x10` | loop active |
  | `+0x16` | last NRPN MSB |
  | `+0x27` | loop start seen |
  | `+0x28` | loop count |
  | `+0x46` | play count (`l_count`) |
  | `+0x48` | times played |
  | `+0x88` | delay |
  | `+0x90` | flags |

### 1.2 The loop markers

- **CC 99 = 0x14 (loop start)**, `0x8008CAB0`-`0x8008CAD0`:
  - It sets `+0x16 = 0x14` and `+0x27 = 1`.
  - It reads the next delta (`jal 0x8008D8D0`) into `+0x88`.
  - It sets `+0x0C = +0x04`, which is the position of the **next event**, after that delta.
  - So the loop starts at the event that follows the marker. When that delta is not zero, this event runs one
    frame later than the marker: tracks 11, 13, 15, 27, 28, 29 and 31.
- **Loop-count latch.** It is the same logic in two handlers:
  - CC 98: `0x8008C96C`-`0x8008C994`.
  - CC 6 (data entry): `0x8008CD08`-`0x8008CD2C`.
  - When `+0x27 == 1` and `+0x10 == 0`, it sets `+0x28 = value` and `+0x10 = 1`.
  - It latches once. Later runs of the same event inside the loop are ignored, because `+0x10` stays at 1.
  - The decompilation has both (`SoundManager.cs:1833-1837` and `:1954-1960`).
  - Alundra uses CC 98 = 127 on the 36 tracks 1-25, 27-35, 39 and 41. It uses CC 6 = 127 on tracks 45 and 46. A
    model that only knew CC 98 (the first `v_loop.py`) took 45 and 46 for one-shots.
- **CC 99 = 0x1E (loop end)**, `0x8008CAD4`-`0x8008CB50`. It sets `+0x16 = 0x1E`, then depends on the count:
  - Count 0: `+0x10 = 0`, the delay is the next delta, and there is no jump.
  - Count from 1 to 0x7E: the count is decremented and the delay is the next delta. If the count is still not 0, it
    jumps (`+0x04 = +0x0C`); otherwise `+0x10 = 0`.
  - Count 0x7F or more: the next delta is read and discarded, then `sw zero, 0x88(s0)` (`0x8008CB48`) and
    `+0x04 = +0x0C` (`0x8008CB50`). The count is never decremented, so the loop is infinite.
  - In the per-frame loop `0x8008BCC4` (decompiled at `SoundManager.cs:1321-1384`, modelled by `v_seq.advance`), a
    delay of 0 means the loop-position event runs **in the same frame and at the same tick** as the 0x1E event.
  - The decompilation (`SoundManager.cs:1878-1907`) matches the binary.

### 1.3 State at the jump

- Only the position and the delay change. Nothing touches:
  - the tempo (`+0x70`, `+0x8C`);
  - the channel programs, volumes (CC 7), pans (CC 10) and bends;
  - the running status;
  - the playing voices (there is no key-off);
  - the reverb.
- So **the state at the jump is the state at the end of the loop body**, not the state when the loop was first
  entered. `loopmodel.py` and `seam.py` measure, per track, which channel state differs and for how long:
  - Tempo differs on 16 tracks. The ritardando at the loop end is undone by the loop body's first tempo event
    within 0 to 2 frames.
  - Volume and pan differ on 10 tracks, for up to 10387 frames (track 7, channel 8).
  - Bend differs on 8 tracks. That has no effect on new notes in the port: `0xE0` calls `FUN_8008D4C0`, then
    `FUN_80093030`, which bends only the sounding voices (`SoundManager.cs:1462-1474`, `:3119-3137`, `:3187-3232`).
    The note-on reads channel volume and pan (`:4278`, `:3113`) but no bend.
  - Notes are held across the jump on 5 tracks (10, 21, 25, 31, 41).
  - At the second jump the state equals the first jump's on all 38 tracks. **The steady state starts at J1.**

### 1.4 Other cases

- **Nesting.** There is one `+0x0C` per sequence, so a second 0x14 would overwrite it and loops cannot nest.
  `cccensus.py`, a linear walk of every file to its end-of-track with the binary's decoding, finds exactly one
  99=20, one count and one 99=30 per looping track, and none in the 8 one-shots.
  - The only other controllers used are CC 7, CC 10, pitch bend and program change.
  - There is one stray CC 99 on track 18, channel 8, with a value that is neither 20 nor 30. It is harmless.
- **End of track (meta 0x2F)**, `0x8008D77C`-`0x8008D8B4`. It increments `+0x48`, then:
  - If `+0x46 == 0`, it restarts at `+0x08` (`+0x0C` is untouched).
  - Else, if `+0x48 < +0x46`, it restarts and sets `+0x0C = +0x08`.
  - Else it stops: it clears flags 1, 8 and 2, sets 0x200 and 4, sets `+0x0C = +0x08`, keys off with
    `jal 0x80093EF4` (a release, per `ding-verify` claim 9), and sets the delay from `+0x70`.
  - The per-frame driver (`0x8008E3D8`) then sees flag 4 (`0x8008E594`), calls `0x8008F2E8`, and stores 0 in the
    flags (`0x8008E5B4`). `0x8008F2E8` rewinds `+0x04`/`+0x0C` to `+0x08` and clears `+0x27`/`+0x28`/`+0x10`
    (`0x8008F3A8`-`0x8008F418`).
  - BGM is started only by `StopAllSound` (`0x80049AF4`) with `PlaySeq(seq, 1, 1)` (`0x80049B58`-`0x80049B60`).
    That goes to `0x8008F188`, then `0x8008F088`, which stores `+0x46 = 1` (`0x8008F10C`) and `+0x48 = 0`
    (`0x8008F13C`).
  - So a BGM without a loop marker **plays once and stops**. Its notes release, and the sequence stays loaded,
    rewound. The next `PlaySeq` would play it again from the start.
  - None of the 38 looping tracks ever reaches its end-of-track, because the jump comes first.
- **Not established (side question).** The model and the extractor tick at 60 Hz. `0x801F6CE0` = 0x3C is used in
  the tempo formula; it is written at `0x8008E010` and `0x8008ED24`-`0x8008EDE8` (the tick-mode switch). Whether
  the PAL binary ticks the sequencer at 50 Hz is not settled here. It does not change the design, because the loop
  points are consistent with the rendered WAV either way.

## 2. Per-track values (`loops.tsv` has every column; `table.md` is this table)

Definitions:
- **LS**: the frame where the loop-position event first runs. The intro is [0, LS).
- **J1 and J2**: the frames of the first and second jumps. Iteration 2 starts at J1 and iteration 3 at J2.
- **Today's `Frames`** = J1 + 1. The extractor keeps the jump frame (`Program.cs:304-308`: `frame++; break`).
- **Samples**: frame × 735 at 44100 Hz, stereo, 16-bit.
- **"iter 1 differs"**: at least one note plays under a different channel volume or pan, or a note is held across
  the jump (section 1.3).

| idx | kind | LS frame (tick) | intro s | J1 = loop start (sample) | J2 = file end | loop frames (P2) | loop s | today Frames | iter 1 differs | used by (map entry / 0xA7) |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | loop | 714 (10075) | 11.900 | 7965 (5854275) | 15215 | 7250 | 120.83 | 7966 | **yes** | none |
| 2 | loop | 114 (1435) | 1.900 | 3992 (2934120) | 7870 | 3878 | 64.63 | 3993 | no | 2 A7 sites |
| 3 | loop | 18 (475) | 0.300 | 2313 (1700055) | 4607 | 2294 | 38.23 | 2314 | no | 2 maps (391,392) |
| 4 | loop | 29 (475) | 0.483 | 5874 (4317390) | 11718 | 5844 | 97.40 | 5875 | no | 2 maps (321,323); 5 A7 sites |
| 5 | loop | 35 (475) | 0.583 | 8019 (5893965) | 16002 | 7983 | 133.05 | 8020 | no | 1 maps (322); 15 A7 sites |
| 6 | loop | 35 (475) | 0.583 | 5725 (4207875) | 11415 | 5690 | 94.83 | 5726 | no | 12 maps |
| 7 | loop | 83 (1430) | 1.383 | 10638 (7818930) | 21192 | 10554 | 175.90 | 10639 | **yes** | 20 maps |
| 8 | loop | 29 (475) | 0.483 | 5290 (3888150) | 10551 | 5261 | 87.68 | 5291 | no | 1 maps (293) |
| 9 | loop | 35 (475) | 0.583 | 6402 (4705470) | 12768 | 6366 | 106.10 | 6403 | no | 23 maps |
| 10 | loop | 63 (710) | 1.050 | 5955 (4376925) | 11848 | 5893 | 98.22 | 5956 | **yes** | 7 maps; 6 A7 sites |
| 11 | loop | 204 (1675) | 3.400 | 5455 (4009425) | 10707 | 5252 | 87.53 | 5456 | no | 7 maps |
| 12 | loop | 126 (950) | 2.100 | 3454 (2538690) | 6782 | 3328 | 55.47 | 3455 | no | 5 maps; 1 A7 sites |
| 13 | loop | 127 (955) | 2.117 | 6567 (4826745) | 13007 | 6440 | 107.33 | 6568 | no | 3 maps (17,439,451) |
| 14 | loop | 1899 (18475) | 31.650 | 14552 (10695720) | 27204 | 12652 | 210.87 | 14400 (capped, `LoopDetected` false) | no | 1 maps (471) |
| 15 | loop | 87 (955) | 1.450 | 7644 (5618340) | 15202 | 7558 | 125.97 | 7645 | **yes** | 1 maps (326) |
| 16 | loop | 441 (6715) | 7.350 | 8178 (6010830) | 15914 | 7736 | 128.93 | 8179 | no | 137 maps; 1 A7 sites |
| 17 | loop | 63 (715) | 1.050 | 7949 (5842515) | 15834 | 7885 | 131.42 | 7950 | no | 11 maps |
| 18 | loop | 35 (475) | 0.583 | 5117 (3760995) | 10199 | 5082 | 84.70 | 5118 | **yes** | 9 maps |
| 19 | loop | 104 (2395) | 1.733 | 3835 (2818725) | 7566 | 3731 | 62.18 | 3836 | no | 1 maps (153) |
| 20 | loop | 47 (710) | 0.783 | 5330 (3917550) | 10614 | 5284 | 88.07 | 5331 | no | 2 maps (324,325) |
| 21 | loop | 35 (475) | 0.583 | 6754 (4964190) | 13474 | 6720 | 112.00 | 6755 | **yes** | 56 maps |
| 22 | loop | 35 (475) | 0.583 | 7071 (5197185) | 14107 | 7036 | 117.27 | 7072 | no | 18 maps |
| 23 | loop | 44 (715) | 0.733 | 7404 (5441940) | 14764 | 7360 | 122.67 | 7405 | no | 18 maps |
| 24 | loop | 599 (8635) | 9.983 | 4599 (3380265) | 8598 | 3999 | 66.65 | 4600 | no | none |
| 25 | loop | 1358 (11510) | 22.633 | 7272 (5344920) | 13186 | 5914 | 98.57 | 7273 | **yes** | 2 maps (389,390) |
| 26 | one-shot (end frame 1106) | - | - | - | 1107 (+ release) | - | - | 1107 | - | none |
| 27 | loop | 487 (4315) | 8.117 | 7263 (5338305) | 14039 | 6776 | 112.93 | 7264 | no | 7 maps |
| 28 | loop | 148 (1435) | 2.467 | 5871 (4315185) | 11594 | 5723 | 95.38 | 5872 | no | 2 maps (159,160) |
| 29 | loop | 103 (1435) | 1.717 | 5282 (3882270) | 10462 | 5180 | 86.33 | 5283 | no | 10 maps |
| 30 | loop | 483 (5270) | 8.050 | 10215 (7508025) | 19947 | 9732 | 162.20 | 10216 | no | 48 maps |
| 31 | loop | 441 (3355) | 7.350 | 6000 (4410000) | 11559 | 5559 | 92.65 | 6001 | **yes** | 13 maps |
| 32 | loop | 168 (1435) | 2.800 | 6220 (4571700) | 12273 | 6053 | 100.88 | 6221 | **yes** | 7 maps |
| 33 | loop | 50 (712) | 0.833 | 5900 (4336500) | 11750 | 5850 | 97.50 | 5901 | no | 1 maps (327) |
| 34 | loop | 109 (1195) | 1.817 | 3746 (2753310) | 7383 | 3637 | 60.62 | 3747 | no | 7 maps |
| 35 | loop | 6938 (50870) | 115.633 | 13744 (10101840) | 20549 | 6805 | 113.42 | 13745 | no | 6 maps |
| 36 | one-shot (end frame 496) | - | - | - | 497 (+ release) | - | - | 497 | - | 1 A7 site (Cave-138) |
| 37 | one-shot (end frame 605) | - | - | - | 606 (+ release) | - | - | 606 | - | none |
| 38 | one-shot (end frame 419) | - | - | - | 420 (+ release) | - | - | 420 | - | none |
| 39 | loop | 35 (475) | 0.583 | 2255 (1657425) | 4474 | 2219 | 36.98 | 2256 | no | 1 maps (477) |
| 40 | one-shot (end frame 540) | - | - | - | 541 (+ release) | - | - | 541 | - | none |
| 41 | loop | 35 (475) | 0.583 | 2943 (2163105) | 5853 | 2910 | 48.50 | 2944 | **yes** | none |
| 42 | one-shot (end frame 663) | - | - | - | 664 (+ release) | - | - | 664 | - | 22 A7 sites |
| 43 | one-shot (end frame 807) | - | - | - | 808 (+ release) | - | - | 808 | - | none |
| 44 | one-shot (end frame 122) | - | - | - | 123 (+ release) | - | - | 123 | - | none |
| 45 | loop | 42 (959) | 0.700 | 380 (279300) | 718 | 338 | 5.63 | 381 | no | 3 A7 sites (map 476's table value 45 means "stop", not this track) |
| 46 | loop | 42 (959) | 0.700 | 380 (279300) | 718 | 338 | 5.63 | 381 | no | none |

Notes on the table:

- **"Used by"** comes from `a7scan.py`:
  - Map entries come from `Maps/music-index.json`. The value -1 is kept apart, because those 21 maps are silent in
    the original (ADR-0004, `0x80049B04`).
  - 0xA7 sites come from a branch-following walk of every reachable instruction of the 483 event files, reusing
    `e16-0/q2-opcodes/scan_opcodes_v2.py`.
  - The figures are distinct maps.
  - Tracks 1, 24, 26, 37, 38, 40, 41, 43, 44 and 46 are not reachable from map tables or `0xA7`.
- **Model check.** J1 + 1 equals `bgm.json` `Frames` on all 37 tracks the extractor detected as looping. The
  8 one-shots match end + 1.
- **Periods per iteration** (P1, P2, P3) vary by ±1 frame on 26 tracks (for example track 25: 5914, 5914, 5913).
  That is the original's own frame quantisation. The model gives the per-event shifts.

## 3. The extractor today, and what must change

### 3.1 Today

`alundra-datas-analyser` `51b77d8`, `AlundraTools/AlundraDataExtractor/Program.cs`:

- `ExtractDataFromBgm` (`:177-245`) runs with `maxSeconds = 240` (`:192`). For each index it calls
  `LoadMapSequence(i, 1)`, then `RenderBgmTrackUntilLoop` (`:216`), then `WriteStereoWav` at 44100 Hz (`:232`).
- The record (`:153`, written at `:233`) has these fields: `SoundIndex`, `File`, `Frames`, `DurationSeconds`
  (= Frames / 60), `LoopDetected`, `PeakLeft`, `PeakRight`, `RmsLeft`, `RmsRight`, `FirstAudibleFrame`.
- `RenderBgmTrackUntilLoop` (`:253-319`) works one frame at a time:
  - It calls `AdvanceSoundFrame` (`:272`), renders 735 samples (`:273`) and appends them.
  - It stops when 300 frames are silent (`:297`).
  - It also stops when `SeqPosition` goes backwards (`:304-308`). It sets `LoopDetected = true`, then
    `frame++; break`, so the jump frame is kept.
  - The backward test also fires on an **end-of-track**, because `0x8008F2E8` rewinds in the same frame. So the 8
    one-shots are written with `LoopDetected: true`, and `LoopDetected` does not tell loop from end.
- No loop position is exported. The converter copies the WAV and the fields as they are (`AudioWriter.cs:66-100`,
  `SoundManifestReader.cs:85-109`). The DLL loops the whole clip (`AlundraMusicPlayer.cs:384`).

### 3.2 Does the audio from the loop start equal the audio after the jump? No (evidence)

1. **Channel state.** On 10 tracks the first iteration does not sound like the later ones (section 1.3, `audible.py`,
   `seam.py` column `iter1_differs_from_steady_state`):
   - Track 1: channel 4 plays 106 notes at volume 110 against 80 later; key 45 is held across the jump.
   - Track 7: channel 4 plays 197 notes at volume 100 against 21; channel 8 plays 48 notes at 100 against 0
     (muted).
   - Track 10: key 52 is held across the jump.
   - Track 15: channel 5 plays 12 notes at 100 against 110.
   - Track 18: channel 2 plays 16 notes at 100 against 2.
   - Track 21: key 40 is held across the jump.
   - Track 25: the pan of channel 5 (6 notes) and the volume of channel 9 (14 notes) differ, and key 60 is held.
   - Track 31: key 55 is held across the jump.
   - Track 32: channel 6 plays 11 notes at 100 against 110.
   - Track 41: key 72 is held across the jump and never released.

   Whether a held note is still audible depends on its envelope; only a render shows it.
2. **Quantisation jitter.** Event frames relative to the iteration start differ by ±1 frame (+2 on 41, -2 on 14)
   between iterations on 26 tracks. `seamexact.py` with L = 180 frames:
   - No track has an event-exact naive seam [LS, J1).
   - Only 3 tracks (2, 12, 14) are event-exact at the steady-state seam [J1, J2).
3. **WAV measurement.** The WAV holds exactly one post-jump frame, J1.
   - `corr.py` correlates frame J1 with frames LS+d. The best match is LS+1 on 24 (0.97/0.98) and 16, LS+0 on 1 and
     27 (0.88-0.96), and nowhere above 0.5 on 25 and 30.
   - `rms(frameJ1 - frameLS) / rms(frameJ1)` has a median of 1.06.
   - The step at the naive seam (`S[LS·735] - S[J1·735-1]`) is more than twice the natural step
     (`S[J1·735] - S[J1·735-1]`) on 30 of the 37 measurable tracks. Examples: track 4, 13564 against 5803;
     track 29, 4167 against 20.
   - Caveat: for LS below about 40 frames, frame LS still carries the previous track's residue until X2 is done
     (O-E19-61).
4. **Track 14.** J1 = 14552 is beyond the cap of 14400, so the WAV holds no full loop.

### 3.3 Options for the data

- **A. Loop points only: loop [LS, J1) of today's WAV.**
  - Changes: no re-render (beyond X2), and the jump frame is dropped from the loop.
  - Rejected:
    - It is unfaithful on the 10 tracks of section 3.2.1.
    - It gives a seam discontinuity on 30 of 37 tracks.
    - Track 14 stays truncated.
- **B. Steady-state render (recommended): the file is [0, J2) and the loop is [J1, J2).**
  - Played from 0, it gives the intro, iteration 1, then iteration 2 for ever. That is the original's order. The
    state at J2 equals the state at J1 on all 38 tracks.
  - The seam J2 -> J1 joins two equivalent ends: iteration 2's end and iteration 1's end, which converge before
    their last frame on every track. Event-exact seams: 3 of 38 tracks at L = 180, 10 of 38 at L = 6.
  - Because of the remaining ±1-frame jitter, bake a **short linear crossfade over the last K samples before J2
    toward the K samples before J1**, with K = 735 (one frame):
    `S'[J2·735-K+x] = (1-x/K)·S[J2·735-K+x] + (x/K)·S[J1·735-K+x]`. The file then ends at the value just before
    `S[J1·735]`, and the jump is continuous.
  - Before deciding K, the premise check is a clean `--render-bgm --bgm-index 25 --frames 13250`, comparing
    frames [J1, J1+60) with [J2, J2+60) and the seam step (section 5).
  - Size (`sizes.py`): from 667.9 MB to 1279.7 MB (x1.92) per copy. There are three copies on disk: `data-extracted`,
    the remaster folder and `alundra-project`. The largest J2 is 27204 frames (453.4 s, track 14).
  - Under streaming playback, runtime memory does not grow.
- **C. Compact steady state: the file is [0, J1+W'), with W' = W + a tail margin.**
  - The loop is [LS+W', J1+W'). It is a rotation of the steady state.
  - It is smaller: for most tracks W is 0-4500 frames, but 10387 for track 7.
  - The extractor must compute W per track: the first rewrite of each differing channel state, the release of
    notes held at the jump, and a margin for release and reverb.
  - It is more code and more risk. Use it only if the author refuses B's size.

### 3.4 Minimal extractor change (B), `Program.cs`

1. Raise `maxSeconds` (`:192`) to at least 454; 600 is suggested.
2. In `RenderBgmTrackUntilLoop`, separate a loop jump from an end-of-track:
   - `(sequenceStates[seqId].Flags & 1) == 0` after `AdvanceSoundFrame` means the sequence stopped: `0x8008F2E8`
     cleared the flags, as in the binary `0x8008E5B4`.
   - A backward `SeqPosition` with flag 1 still set is a loop jump.
3. **First loop jump.** Record `loopStartFrame = frame` (this is J1) and continue. Keep the frame: it is iteration
   2's first frame.
4. **Second loop jump.** Stop and drop that frame's 735 samples (iteration 3's first frame, equivalent to J1), so
   `Frames` = J2. Then bake the K-sample crossfade.
5. **End-of-track (one-shot).** `Looping = false`. Optionally keep rendering until the release is silent, for
   example while peak > 64, up to a cap. Today 26, 36, 38, 42 and 43 end at RMS 2400-6000 (`oneshot_tail.py`), so
   their release is cut.
6. **`BgmExportRecord`.** Append fields at the end, keeping the existing order (the ADR-0003 precedent):
   - `Looping` (bool);
   - `LoopStartFrame` (int, -1 when not looping);
   - `LoopStartSample` (int, LoopStartFrame × 735, or -1).

   Optional fields, for documentation and tests:
   - `SeqLoopStartFrame` (LS);
   - `LoopEndFrame` (= `Frames`).

   The largest sample offset is 27204 × 735 = 19 994 940, which fits an int.
7. **Other modes.** `--verify-bgm` (`:607-684`) and `--render-bgm` (`:686-760`) keep their own loops.
   `--render-bgm` with `--frames` is already able to render past J2 for the premise check.
8. **Do it with X2.** X2 builds a clean sound system per track (D-E19-69, `ding/notes.md` section 6). Do both in the
   same slice so there is one re-extraction, mirror and full export. Mirror `sound/bgm/*` and `sound/bgm.json`, and
   check the direction of each difference (memory: the 09-19 remaster text is not decoded).

## 4. Playback side

### 4.1 Converter

- `Writers/AudioWriter.cs:66-100` copies `bgm_NNN.wav`, registers it in the catalog (`Ids.For("sound/bgm/<file>")`)
  and writes `Musics/bgm-manifest.json` in snake_case from `BgmEntry` (`Readers/SoundManifestReader.cs:12-25`).
  `ReadBgm` maps each field explicitly (`:85-109`).
- **Change.**
  - Add `Looping`, `LoopStartFrame` and `LoopStartSample` (nullable, so that an older `bgm.json` reads as "unknown")
    to `BgmEntry`, and add the matching `GetNullable*` reads in `ReadBgm`.
  - The manifest then carries `looping`, `loop_start_frame` and `loop_start_sample`.
  - No `.sound` documents are needed (section 4.3).
  - The WAVs keep their names and asset ids.

### 4.2 Engine, facts

- **`AudioService.PlayClip`** (`AudioService.cs:64`) plays a resident `SoundEffect`. For a music voice
  `IsLooped = true` becomes `SoundEffectInstance.IsLooped` (`MonoGameAudioBackend.cs:436-440`), which loops the
  whole buffer.
- **MonoGame 3.8.5.1 DesktopGL** has `SoundEffect(byte[], offset, count, rate, channels, loopStart, loopLength)`,
  but `PlatformInitializePcm` ignores `loopStart` and `loopLength`. It only binds the data buffer (decompiled with
  `ilspycmd`, `mg/SoundEffect.cs:542-554`). There is no other loop-point API.
- **Streaming exists.** `MusicPlayer` (`Streaming/MusicPlayer.cs`, owned by `AudioService.Music`, `:32`, `:41`,
  updated from `AudioService.Update`) feeds a `DynamicSoundEffectInstance` from `WavStreamReader`. Its loop is
  `Reader.Rewind()` to the first data byte (`MusicPlayer.cs` `FillQueue`, `WavStreamReader.cs:91-97`). Buffers
  queued back to back are gapless.
- **`FillQueue` has no cap per call.** It loops while the backend reports fewer than 3 pending buffers. Under a
  fake that always answers 0 (the DLL's `Alundra.Tests/FakeAudioBackend.cs:189`), a looped track never leaves the
  loop. This is the "a route A test would freeze the suite" point of `plan-e11c-musique.md` §2.2.
  `StereoVoiceMixer` already solved it with `MaxBuffersPerUpdate = 3` (ADR-0039).
- **`SoundAsset`** (`SoundAsset.cs`, `.sound` fields in `docs/engine/audio-system.md:60-85`) has `IsLooped` and
  `IsStreaming`, and no loop point. It can be built in code (`new SoundAsset { ... }`, `ObjectBase()` only draws an
  id).
- **ADRs.** ADR-0001 (streaming music in the engine), ADR-0002 (the `.sound` metadata list) and ADR-0039 (software
  stereo voices; it says "Not done: loop points").
- **Engine rules** (`CasaEngineMonogame/AGENTS.md`):
  - Asset formats stay stable: an additive optional field with a default plus a note, no silent change (§8).
  - Interfaces only at real backend boundaries, so `IAudioBackend` should not change (§7).
  - No allocation in `Update` or streaming (hot-path list).
  - An ADR for any format, public API or backend decision (§10).
  - Tests in `CasaEngine.Tests` (§6).

### 4.3 Design options (playback)

- **E1. Streaming with a loop start (recommended).**
  - Engine:
    1. `SoundAsset.LoopStartSample` (long or int, default 0), serialized as the optional field `loop_start_sample`.
    2. `WavStreamReader.SeekToSampleFrame(long)`: data offset plus frame × `BlockAlign`; it validates the bounds.
    3. `MusicPlayer.FillQueue`: at the end of data, seek to `LoopStartSample` instead of `Rewind()`.
    4. A per-call cap on buffers (a `MaxBuffersPerUpdate` like ADR-0039).
    5. Optionally a `LoopEndSample`. It is not needed with B, where the file ends at the loop end.

    Plus an engine ADR (amending ADR-0002's metadata list), `docs/engine/audio-system.md`, and optionally the
    inspector field.
  - DLL (`AlundraMusicPlayer`):
    - `LoadBgmManifest` keeps `(assetId, looping, loopStartSample)` per index.
    - `StartVoice` plays `_audioService.Music.Play(new SoundAsset { AudioFileAssetId, IsStreaming = true,
      IsLooped = looping, LoopStartSample, BusName = Music, Volume = 1 }, 0f, owner: this)` and keeps the
      `MusicTrackHandle`.
    - `StopCurrentVoice` calls `Music.Stop(handle)`.
    - `IsCurrentVoiceAlive` calls `Music.IsAlive(handle)`.
  - Unchanged: B4 (a live voice is left alone), D-C-5 and D-C-6 (session ownership), and the fade director (it acts
    on the Master bus, `AlundraBgmFadeDirector.cs:258`).
  - Bonus: route A's memory is 49 KB against 20.4 MiB per track resident (`plan-e11c-musique.md` §2.2). That
    matters more once B doubles the files.
  - Cost: the DLL music tests watch `backend.PlayCalls`. There are about 90 sites: `AlundraMusicPlayerTests` 22,
    `AlundraBgmFadeDirectorTests` 40, `AlundraWarpDepartureTests` 27, `AlundraWorldProxyAudioInstallationTests` 2,
    plus the arrival and new-game tests. They must watch the streaming voice instead, and the fake clip provider
    needs a real `OpenStream` (today `=> null`, `FakeAudioBackend.cs:335`).
  - D-C-1 (route B) of `plan-e11c-musique.md` is superseded: a parent ADR.
- **E2. Resident clip on a software voice with a loop start** (an `AudioService.PlayClipLooped`, fed like
  `StereoVoiceMixer`).
  - It needs stereo samples kept by `MonoGameAudioClip`; today only mono is kept (ADR-0039).
  - Memory doubles (about 37-76 MB per track after B, twice).
  - The fake still sees a streaming voice, so the DLL tests change anyway. Not recommended.
- **E3. Two clips, intro then loop.** It is not gapless: the switch happens at a game frame, and `SoundEffectInstance`
  cannot queue. Rejected.
- **E4. Keep `PlayClip` with a whole-file loop.** This is today's defect. Rejected by D-E19-70.

## 5. Tests and values computable in advance

### 5.1 Pinned values (model, `loops.tsv`)

- **Track 25.**
  - Intro LS = 1358 (tick 11510, 22.633 s).
  - J1 = 7272: `LoopStartFrame`, `LoopStartSample` = 5 344 920.
  - J2 = 13186: `Frames` after B, so the file holds 13186 × 735 = 9 691 710 samples (37.0 MB).
  - Loop P2 = 5914 frames (98.57 s). Today's `Frames` = 7273 = J1 + 1.
- **Track 1.** LS 714, J1 7965, J2 15215.
- **Track 14.** LS 1899, J1 14552, J2 27204. Today it is capped at 14400 with `LoopDetected` false.
- **Tracks 45 and 46** (the CC 6 latch). LS 42, J1 380, J2 718.
- **One-shots.** Loop -1 for all; end frames 26:1106, 36:496, 37:605, 38:419, 40:540, 42:663, 43:807, 44:122.
- **The table rule.** Today's `Frames` - 1 = the new `LoopStartFrame` on the 37 tracks that already loop. That is
  a cross-check that needs no model.

### 5.2 Tests

- **Analyser.** It has no test project (`plan-extraction-bgm.md` §2.1). The acceptance is the run's own output plus
  real-data tests on the parent.
- **Converter, real data** (skipped when `data-extracted/` is missing; the pattern is
  `SpriteBankReaderAnimSetsTests.cs`, an early return). A new `BgmLoopPointsTests.cs`, a theory over the 46 rows:
  - looping tracks: `bgm.json` `LoopStartFrame` = J1, `Frames` = J2, and `LoopStartSample` = J1 × 735;
  - WAV length = `Frames` × 735 × 4 + 44;
  - one-shots: `Looping` false;
  - seam continuity on the real WAV: `|S[J2·735-1] - S[J1·735]|` no larger than the 99th percentile of the
    track's own sample-to-sample steps. This one only goes green after the crossfade.
  - Red today: `LoopStartFrame` does not exist, `Frames` = J1 + 1, and track 14 is capped.
- **Converter, fixtures.**
  - `SoundManifestReaderTests` (`:87-114`): the new nullable fields are read; when absent they are null.
  - `AudioWriterTests` (`:239-250`): they pass through to `bgm-manifest.json`.
- **Engine (`CasaEngine.Tests`).**
  - `WavStreamReader.SeekToSampleFrame` on an in-memory WAV.
  - `MusicPlayer` with `LoopStartSample = k` on a synthetic ramp WAV of N frames: the submitted bytes are
    `[0..N)`, then `[k..N)`, then `[k..N)`.
  - `.sound` round-trip of `loop_start_sample`, with the default 0.
  - `FillQueue` stops with `ReportsZeroPendingBuffers = true`. That fake flag already exists in
    `CasaEngine.Tests/Audio/FakeAudioBackend.cs:214-223`.
- **DLL.**
  - Entry 389 with a fixture manifest (asset, `looping: true`, `loop_start_sample: 5344920`) gives exactly one
    music track on the Music bus, with that loop start, looped, at full volume.
  - A one-shot index (for example 42 through `0xA7 42,0` then `0xA5`) is not looped.
  - B4: a live track is not restarted.
  - Fades act on the Master bus, unchanged.
- **Mutations.**
  - Stop at the first jump again: `Frames` = J1 + 1, the theory goes red.
  - Use LS as the loop start: red on `LoopStartFrame`.
  - Revert the engine seek to `Rewind()`: the engine test goes red.
  - `isLooped: true` for every track: the one-shot DLL test goes red.
  - Remove the crossfade: the seam test goes red, if the premise check shows a discontinuity.
- **Premise check before editing** (main session, later):
  - Run `AlundraDataExtractor --render-bgm "<Alundra (France)_extracted>" <scratch>/r25 --bgm-index 25
    --frames 13250`.
  - Expect a first jump at 7272 and a second at 13186.
  - Compare frames [7272, 7332) with [13186, 13246), and measure the seam step.
  - Do the same for 7 (J1 10638, J2 21192), where the steady state differs most.

### 5.3 What changes in the export

- The BGM WAVs of the 38 looping tracks grow to J2 frames.
- Their first frames also change through X2.
- The one-shot WAVs gain a release tail if that option is taken.
- `bgm.json` and `bgm-manifest.json` gain the loop fields.
- `bgm_014.wav` goes from 14400 frames to 27204.
- `bgm_044.wav` is an X2 matter: today it holds track 43's residue (`ding-verify` claim 22).
- The 996 sound-effect WAVs and `sfx.json` must not change.

## 6. Chain effect (which maps loop audibly differently)

### 6.1 Today against the original

Every looping track used in the game replays [0, LS) and iteration 1 at each loop. The seam J1 -> 0 cuts the
sounding voices and lands on the intro.

- **Long intros replayed**, intro / original loop / port period:

  | track | maps | intro | original loop | port period | note |
  |---|---|---|---|---|---|
  | 35 | Fairy cave and Meia Dream (158, 161, 440, 441, 442, 468) | 115.6 s | 113.4 s | 229 s | |
  | 14 | Arena Melzas 2 (471) | 31.65 s | 210.9 s | 240 s | the WAV stops 2.5 s before the jump |
  | 25 | Ship Klark (389, 390) | 22.6 s | 98.6 s | 121.2 s | |
  | 27 | Kline's Nightmare (123-128, 466) | 8.1 s | | | |
  | 30 | Coal Mine and Olen's Dream, Elane Nightmare and others (48 maps) | 8.05 s | | | |
  | 16 | Inoa and many others (137 maps) | 7.35 s | | | |
  | 31 | Coastal Cave and others (13 maps) | 7.35 s | | | |
  | 11 | Torla (7 maps) | 3.4 s | | | |
  | 32 | Nestus' Dream (7 maps) | 2.8 s | | | |
  | 28 | Fairy cave underwater (159, 160) | 2.47 s | | | |
  | 12 and 13 | | 2.1 s | | | |
  | 2 | 0xA7 at 163 and 260 | 1.9 s | | | |
  | 34, 19, 29, 15, 7, 10, 17 | | 1.0-1.8 s | | | |

- **Short intros.** All other tracks have an intro of 0.3-0.83 s. It is mostly the silent pre-roll (after X2) before
  the first note, which inserts a gap of about 0.5 s at every loop. Before X2 it is the previous track's residue.
- **One-shots looped instead of played once.**
  - 42 (22 boss and dream maps, `0xA7 42,0` then `0xA5`).
  - 36 (Cave-138, `0xA7 36,0`).
  - Hypothesis, not traced frame by frame: after `0xA5` the corpus waits 240 + 240 + 200 frames before `0xA7 0,0`.
    That is more than 42's 664 frames, so the port would restart the jingle for about 16 frames before the stop.
  - 26, 37, 38, 40, 43 and 44 are reachable from neither the map table nor `0xA7`.

### 6.2 After the fix against today

The later loops of 10 tracks will sound different from anything the port has played:

- 7 (overworld, 20 maps);
- 21 (56 maps);
- 25 (389, 390);
- 31 (13 maps);
- 32 (7 maps);
- 10 (Inoa, 7 maps and 6 `0xA7` sites);
- 15 (Arena Ronan, 326);
- 18 (Tarn's Manor and others, 9 maps);
- 1 and 41 (not reachable).

## 7. Open points, not established

- Whether the notes held across the jump (10, 21, 25, 31, 41) are still audible at the jump. That depends on their
  ADSR, and only a render shows it.
- The size of the steady-state seam discontinuity. It needs the render of section 5.2's premise check.
- Whether the original's sequencer ticks at 60 Hz on PAL (`0x801F6CE0`, section 1.4).
- Whether new notes in the binary inherit the channel bend. The analyser port says no. The binary's key-on path was
  not read here.

## 8. Scripts and outputs (this folder)

| file | role |
|---|---|
| `loopmodel.py` | Timing model `v_seq` (`../recipe-1005/ding-verify`) plus full CC 99/98/6 loop semantics, the end-of-track, the state and held-note snapshots, and the event log. |
| `seam.py` | Writes `loops.tsv` and `seam_details.jsonl`; the WAV seam measures. |
| `audible.py` | Iteration 1 against the steady state, per channel. |
| `seamexact.py` | Event-exactness of seams A, B and B3. |
| `dbg45.py` | Event-exactness check for track 45. |
| `corr.py` | Correlation of the jump frame J1 with frames LS+d. |
| `cccensus.py` | Controller and marker census. |
| `a7scan.py` | Map and `0xA7` usage, output `a7scan.json`. |
| `oneshot_tail.py` | Last frames of the one-shot WAVs. |
| `sizes.py` | Size impact of the options. |
| `gen_table.py` | Builds `table.md` from `loops.tsv`. |
| `mg/SoundEffect.cs` | The decompiled MonoGame `SoundEffect`. |
| `model_out.jsonl`, `seam_out.txt` | Raw outputs. |
