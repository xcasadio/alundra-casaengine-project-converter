# Verification of the BGM loop-point discovery (`loop-disc/`), read-only, 2026-10-05

Nothing was built, run (extractor, converter, game), exported, committed or edited in the repository. Repo HEAD is
`6d933e4` (4 commits past `7d23f89`; engine pointer `a6efd2a9`, no audio file changed, every cited line still valid).

Scripts in this folder (none imports `v_seq.py` or `loopmodel.py`):
- `seqsim.py`: independent driver written from the disassembly (`0x8008BCC4`, `0x8008BDD0`, `0x8008C1B8`,
  `0x8008C918`, `0x8008CA40`, `0x8008CC70`, `0x8008D568`, `0x8008D8D0`), with two per-frame variants:
  `bin` (the binary loop `0x8008BD84`-`0x8008BDAC`) and `ana` (the analyser `SoundManager.cs:1342-1362`).
- `census.py`: a linear walk of every SEQ, plus a column-by-column diff against `loop-disc/loops.tsv`.
- `iters.py`, `onenote.py`, `counts.py`: iteration 1/2/3 comparison, aligned by stream index.
- `diverge.py`: where the two drivers part, and the marker/loop-event frame gap.
- `wavchk.py`, `todayseam.py`: WAV sizes, tails, the naive seam, today's whole-file seam.
- `snd_dis.txt`: disassembly of `0x8008A000`-`0x80098000`.

## Verdicts

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Dispatch through the table `0x8002A83C`: CC 6 → `0x8008CC70`, 98 → `0x8008C918`, 99 → `0x8008CA40` | CONFIRMED | Words `0x8002A854`=`8008C264`, `0x8002A9C4`=`8008C3EC`, `0x8002A9C8`=`8008C40C`. Those stubs `jal` the three handlers. Controllers above 0x79 go to the delta-only path (`0x8008C230`-`0x8008C238`). |
| 2 | State `*(0x801F6CE8+4·seq)+0xAC·track`; fields `+04/+08/+0C/+10/+27/+28/+46` | CONFIRMED | The stride is 43·4 (`0x8008CA54`-`0x8008CA88`). `+08` is the restart source at the end of track (`0x8008D790`). `+46` is read with `lh` (`0x8008D780`) and `+48` is incremented (`0x8008D78C`). |
| 3 | Loop start 0x14 (`0x8008CAB0`-`0x8008CAD0`): `+27`=1, delta → `+88`, `+0C` = position after that delta | CONFIRMED | It also sets `+16`=0x14 (`0x8008CAB0`). |
| 4 | Count latched once, by CC 98 (`0x8008C96C`) or CC 6 (`0x8008CD08`), when `+27==1 && +10==0`; tracks 45 and 46 use CC 6 | CONFIRMED | Both paths are in the binary and in the analyser (`SoundManager.cs:1833`, `:1954`). Census: 36 tracks latch with CC 98 = 127, tracks 45 and 46 with CC 6 = 127. On later passes, CC 6 on 45 and 46 only reads its delta, because `+2A` != 2 (`0x8008CF68`-`0x8008CF70`). |
| 5 | Loop end 0x1E: count 0 means no jump; count below 0x7F decrements and jumps while not 0; count 0x7F or more means delay 0 (`0x8008CB48`), jump (`0x8008CB50`), no decrement | CONFIRMED | Disassembly `0x8008CAD4`-`0x8008CB50`. With a delay of 0, the inner loop `0x8008BD8C`-`0x8008BD94` runs the loop-position event in the same frame. |
| 6 | Nothing else resets at the jump; no nesting; exactly one start marker, one count and one end marker per looping track | CONFIRMED | The handler writes only `+16`, `+28`, `+88` and `+04`. Running status `+11` and channel `+12` are untouched. The census finds exactly one 99=20, one 99=30 and one latch per looping track, and none in the 8 one-shots. Track 18's stray 99=3 is harmless: it has no CC 6. |
| 7 | No marker: PlaySeq(seq,1,1) gives l_count 1 (`0x8008F10C`), so the track plays once; key-off `0x80093EF4`; reset `0x8008E594`→`0x8008F2E8` | CONFIRMED | The other writers of `+46` are `0x8008DAB4`, the next-sequence chain, which `+3C` = 0xFF disables (the only `+3C` writes, `0x8008DE20` and `0x8008EF74`, store 0xFF), and `0x8008DD10`, whose function has no `jal` caller. The second PlaySeq caller, `0x80049428`, uses another table (`0x800A81C8`). "Release" relies on `0x80091134`, not re-traced here. |
| 8 | 38 tracks loop (1-25, 27-35, 39, 41, 45, 46) and 8 play once (26, 36, 37, 38, 40, 42, 43, 44) | CONFIRMED | `seqsim.out` |
| 9 | 44100 Hz, 735 samples per frame | CONFIRMED | `SpuMixerSoundPlaybackBackend.cs:18`, `Program.cs:255`; every WAV is PCM 16-bit stereo at 44100 Hz with a 44-byte header. |
| 10 | "The timing model predicts today's `Frames` exactly", presented as the binary's | CONFIRMED for the extractor; REFUTED as "the binary's" on track 14 | Both of my variants give `Frames` = J1+1 on all 37 tracks with `LoopDetected` and end+1 on all 8 one-shots. But `v_seq`/`loopmodel` copy the analyser's stale-tempo early return (`if (tempo < delay) return;` inside the do-loop, `SoundManager.cs:1344`, loop `:1342-1362`). The binary loops straight back to the `jal` (`0x8008BDA8` → `0x8008BD84`). The two part on **track 14 only**, at frame 11613: the tempo rises to T=147, and the analyser delays the note at SEQ offset 42704 by one frame. Binary: J1 14551, J2 27202, P 12652/12651/12651. Analyser (and so the extractor): 14552, 27204, 12653/12652/12652. |
| 11 | Per-track values (`loops.tsv`) | CONFIRMED (corrected for 14) | LS, LS tick, J1, J2, P1-P3 and the end frames, recomputed for all 46 tracks: they match `loops.tsv` exactly, except track 14 in the binary variant (row 10). Spot values: 1 (714 / 7965 / 15215), 24 (599 / 4599 / 8598), 25 (1358, tick 11510 / 7272 / 13186), 32 (168 / 6220 / 12273), 43 (end 807), 44 (end 122), 45 and 46 (42 / 380 / 718), 35 (6938 / 13744 / 20549). |
| 12 | The loop-position event runs one frame after the marker on 11, 13, 15, 27, 28, 29, 31 | CONFIRMED | `diverge.out`: exactly those 7. |
| 13 | The first pass differs from every later pass on 10 tracks (1, 7, 10, 15, 18, 21, 25, 31, 32, 41) | CONFIRMED, under its own rule | Volume or pan differences on notes keyed before the restoring CC: 1 (ch4, 106 notes), 7 (ch4, 197; ch8, 48), 15 (ch5, 12), 18 (ch2, 16), 25 (ch9, 14; ch5 pan, 6), 32 (ch6, 11). Notes still sounding at J1 that were not sounding at the loop entry: 1 (12,45), 10 (7,52), 21 (7,40), 25 (5,60), 31 (6,55), 41 (2,72). The union is the report's 10. The rule leaves out notes keyed in the same frame as the restoring CC; that holds because CC 7 and CC 10 rewrite the sounding voices through `0x8009410C`. Counting them would add 1-3 notes on 6, 9, 13, 14, 16, 17, 19 and 34. The note-on does read the channel volume (`0x800935A8`, `lh 0x4e`). |
| 14 | §1.3: "notes held across the jump on 5 tracks (10, 21, 25, 31, 41)" and "volume and pan differ on 10 tracks" | REFUTED (internal) | Held notes: 6 tracks, including track 1 (the report's own §3.2.1 lists it). Track 1's key 45 is struck again at +1834, before the note-off at +1860. At the state level, volume or pan differs on 23 tracks. Notes are affected on only 6. |
| 15 | The state at J1 equals the state at J2 on all 38 tracks | CONFIRMED | Channel CCs, programs, bends, tempo and the set of sounding notes are equal at J1 and J2. The note attributes of passes 2 and 3 are identical. The only difference is the non-latched CC 98 / CC 6 bookkeeping. |
| 16 | "Jitter: ±1 frame on 26 tracks" | REFUTED (count; not decision-changing) | Aligned by stream index, events shift between passes 1 and 2 on **36 of 38** tracks (2 and 12 are exact); passes 2 and 3 likewise. Periods vary on 29 tracks. Cause: `loopmodel.iteration_offsets` cuts passes by frame window, so the pre-jump events of the jump frame misalign the lists. It reports 0 for 11, 15, 27 and 29, although P1≠P2 there. Consequence: the original itself never repeats sample-exactly. |
| 17 | Naive seam [LS,J1): step more than twice the true step on 30 of 37; track 14 holds no full loop | CONFIRMED | `wavchk.out`: 30 of 37 (4: 13564 vs 5803; 29: 4167 vs 20). Track 14 is capped at 14400 frames with `LoopDetected` false. |
| 18 | Option B: file [0,J2), loop [J1,J2); cap at least 454 s; tell loop from end by `Flags&1` | CONFIRMED in principle | Rows 15 and 10. `Flags` = 0 after the reset in the same frame: binary `0x8008E594`-`0x8008E5B4`, analyser `SoundManager.cs:1302-1306`. Largest J2: 27204 frames in the extractor (453.4 s). |
| 19 | Size 667.9 MB → 1279.7 MB per copy | CONFIRMED, units are MiB | 700.3 MB → 1341.9 MB in decimal units. |
| 20 | One-shots 26, 36, 38, 42 and 43 end loud | CONFIRMED | Last-frame RMS: 4290, 4047, 3158, 6007, 2423. Tracks 37, 40 and 44: 40, 19, 5. |
| 21 | MonoGame DesktopGL 3.8.5.1 ignores loopStart/loopLength | CONFIRMED (the decompile itself not redone) | `loop-disc/mg/SoundEffect.cs:542-554`; `Directory.Packages.props:18` = 3.8.5.1. |
| 22 | Engine facts: `FillQueue` calls `Rewind()` with no per-call cap; a fake that reports 0 pending buffers spins forever; `MaxBuffersPerUpdate` exists in ADR-0039's mixer; `SoundAsset` has no loop point | CONFIRMED | `MusicPlayer.cs:273-300`; `WavStreamReader.cs:91-97` (the stream must be seekable, `:40-43`, and the real provider returns a `FileStream`, `AssetContentManagerAudioClipProvider.cs:58-87`); `StereoVoiceMixer.cs:42,159`; `CasaEngine.Tests/Audio/FakeAudioBackend.cs:218-223`; DLL fake `:189` (0 pending) and `:335` (`OpenStream` returns null); `SoundAsset.cs` (in-code construction only draws a Guid). |
| 23 | DLL, converter and extractor line references | CONFIRMED | `AlundraMusicPlayer.cs:359-386,384-385,424-475`; `SoundManifestReader.cs:12-25,85-109`; `AudioWriter.cs:66-100`; `Program.cs:153,177,192,216,232-233,253,255,272-273,297,304-308`. 91 `PlayCalls` occurrences across the 4 named test files. |
| 24 | Map usage (35: 158, 161, 440-442, 468; 14: 471; 25: 389, 390; 27: 7 maps; 30: 48; 16: 137; 31: 13) | CONFIRMED | Recounted from `alundra-project/Maps/music-index.json`. The -1 maps stay silent through the DLL's B2 guard (`AlundraBgmFadeDirector.cs:160`), so track 1 is unused. |
| 25 | 0xA7 sites (42: 22 maps; 36: Cave-138; unreachable tracks) | UNCONFIRMED (not re-run) | Read only from `loop-disc/a7scan.json`; the walker was not run again. |
| 26 | "Every map with music replays its intro at each loop, **with a click at the seam**" | Intro CONFIRMED; click OVERSTATED | Today's whole-file seam step (last sample → sample 0) exceeds the track's 99th-percentile step on 27 of the 38 looping tracks. It is below that on 10, 21, 24, 28, 29, 31, 32, 35, 41, 45 and 46 (track 35: 635 vs 1330). |
| 27 | Track 42 replays about 16 frames before its stop | UNCONFIRMED (marked as a hypothesis) | Not traced. |
| 28 | `plan-e11c-musique.md:43-48` is wrong; D-C-1 is route B | CONFIRMED | Doc read. |
| 29 | Option C (W values) | UNCONFIRMED | Not checked. |
| 30 | Tempo differs at the jump on 16 tracks | CONFIRMED | 4, 5, 6, 8, 9, 10, 14, 18, 21, 22, 23, 25, 28, 31, 39, 41. |
| 31 | Open points: held-note audibility, seam size after B, PAL tick, bend inheritance | Stay open | Bend: `0x80093030` loops over the voices through `0x80092E04` (per-voice pitch store `0x801F779C`) and keeps no per-channel bend. That leans towards "not inherited", but it is not proven. Side fact for PAL: the extractor itself uses 50 fps for PAL elsewhere (`Program.cs:145`) but 60 for BGM. |

## Corrections to carry
1. **Track 14:** the original's values are J1 14551, J2 27202, P2 12651. The extractor (analyser) gives 14552 and 27204. Pins
   and `LoopStartSample` must come from the extractor (10 695 720 = 14552 × 735) unless the analyser's stale-tempo check is
   ruled a decompilation defect. That is an author call, given the rule "ne pas corriger la décompilation". It is inaudible.
2. **Counting fixes:** jitter is on 36 tracks, not 26; held notes cross the jump on 6 tracks, not 5; volume or pan
   differs at the state level on 23 tracks, and notes are affected on 6. The "10 tracks" verdict stands. The sizes are MiB.
3. **The "click at every seam" claim** holds on about 27 of 38 tracks, not on all of them.

## Missed side effects and risks
- **Streaming starvation (E1).** `AudioService` builds `MusicPlayer` with its defaults (`AudioService.cs:32`): 3 × 16384 B
  (`MusicPlayer.cs:23-26`).
  - At 44.1 kHz, 16-bit stereo, that is about 279 ms of margin. The class doc assumes 22 kHz and 185 ms per buffer.
  - A game-thread stall longer than that underruns the voice and leaves an audible gap. A map load while the same track
    keeps playing (B4 / B10) is exactly such a stall. The doc names the "world load" case itself.
  - The resident clip used today cannot starve.
  - Measure warp stalls, or size the queue for this use. Magnitude UNCONFIRMED.
- **X2 oracle.** D-X-5 as amended keeps track 1's byte identity as X2's oracle. With B in the same slice, `bgm_001.wav` grows
  from 7966 to 15215 frames and its tail is crossfaded. The oracle must become "the prefix [0, 7966·735) is byte-identical".
- **One-shot liveness.** The binary clears the flags at the end of track (`0x8008E5B4`) while the release still rings.
  - If the WAV carries the release tail, the DLL voice stays "alive" (B4) longer than the original's sequence.
  - A PlaySeq inside that window restarts the track in the original and is ignored by the port. The window is short.
- **Test accessors.** `CurrentVoiceForTests` and `IsCurrentVoiceAlive` (49 test references) also change type with E1. That
  is on top of the `PlayCalls` sites.
- **Author question, not raised by the report.** In the original's steady state, channel volumes are left at their
  loop-end fade.
  - Track 7: ch8 fades from 100 to 0 over +10387..+10548 and stays at 0 for the next 10387 frames, so 48 notes per loop
    are silent. ch4 ends at 21.
  - That is the binary's behaviour, and B reproduces it as D-E19-70 asks ("comme l'original").
  - The rule "un bug avéré du jeu original se corrige" could apply. Faithful B versus the first-pass sound on 1, 7, 15, 18,
    25 and 32 is the author's call.
