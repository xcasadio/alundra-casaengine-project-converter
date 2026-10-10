# ADR-0051: The game streams its music with the loop point of the sequence

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: `docs/plan-e19-opcodes.md`, "E19.L-b3" (section 1.2w.2) with D-E19-102 and D-E19-106, and the evidence in `docs/plan-e19-loop-annexe/` (`data/notes.md` sections 4.3, 4.4 and 5; `lb-1008/notes.md`; `lb-1008/verify/verify.md`). Engine counterparts: CasaEngine ADR-0074 (a streamed music track loops back to its loop-start sample; numbered ADR-0072 until the engine merge of 2026-10-10) and ADR-0073 (the default music queue is 24 buffers deep). Data side: ADR-0003 (manifest fallback precedent).

## Context

- Until now `AlundraMusicPlayer` played each track with `AudioService.PlayClip` on the Music bus, looped over the whole exported file (route B, D-C-1 of `docs/plan-e11c-musique.md`). The file was rendered up to the first loop marker, and a one-shot track (the sequences that never loop) looped forever too.
- Since E19.L-b1 and L-b2 the exported `Musics/*.wav` hold the intro and the first pass up to the end of the loop (the tail of a one-shot included), and `Musics/bgm-manifest.json` carries three more fields per track: `looping`, `loop_start_frame` and `loop_start_sample` (the sample frame of the sequence's loop marker, 735 samples per sequencer frame; null for the seven one-shots 26, 36, 37, 38, 40, 42 and 43).
- Since E19.L-a the engine's music player (`AudioService.Music`) streams a `SoundAsset` and, for a looped asset, seeks back to `SoundAsset.LoopStartSample` at the end of the data, in the inline path and in the streaming worker, with a queue 24 buffers deep.

## Decision

- **The music is a stream, not a clip.** `AlundraMusicPlayer` builds one `SoundAsset` per manifest track at each attach (`IsStreaming`, Music bus, volume 1, name `bgm_NNN`) and plays it with `AudioService.Music.Play(asset, 0, owner: this)`; `Music.Stop` and `Music.IsAlive` replace the stop and the liveness check of the voice. The owner stays the player (D-C-5), no fade-in (D-C-3). This supersedes route B (D-C-1).
- **The loop point comes from the manifest.** `IsLooped = looping ?? true` and `LoopStartSample = loop_start_sample ?? 0` for a looping track; a track the manifest says does not loop is not looped (tracks 36 and 42 now stop after their tail, as in the original).
- **Missing fields fall back, with one warning.** A manifest without the `looping` field (an export from before the loop points) keeps the former behaviour for the tracks that lack it: the whole file loops from sample 0. The player logs ONE warning per attach, not per track, naming the manifest (the ADR-0003 precedent: an older export keeps working).
- **B4 is unchanged.** `PlaySequence` leaves a live track alone. The track is alive until the music player's next update has seen its stream end, so during the release tail of a one-shot (up to 1.9 s past the end of the track; tracks 36 and 42 are reachable) a new `0xA5` of the same track is ignored, where the original restarts the sequence. **Accepted deviation** (D-E19-106, decided by the author on 2026-10-08); the oracle `bgm.json` is not affected.
- Test seams: `CurrentVoiceForTests` is now the `MusicTrackHandle`, and `CurrentAssetForTests` returns the started `SoundAsset`. `FakeAudioBackend` records the streaming voices (`StreamVoices`), counts the buffers submitted to a streaming voice (`DrainStream` plays them) and its clip provider serves registered wavs from `OpenStream`.

## Consequences

- The track comes back to the loop marker without a cut, and a long file is no longer loaded whole into memory (`bgm_014` is 80 MB).
- The music tests now read the stream recorder where they read the whole-clip recorder (`PlayCalls`); the sound effects keep `PlayClip` and `PlayCalls`. The corpus test that asserted track 42 looped now asserts it does not.
- A manifest from before L-b2 plays, but loops the whole file and logs one warning per attach; the game needs the L-b2 export (and the L-b1 data) to have the loop points.
- The master fade machine acts on the Master bus and is unaffected (`AlundraBgmFadeDirector`).
- Open for the author's recipe (LB-4): a looping track (25, the room of map 389) comes back to the marker with no audible cut; tracks 36 and 42 end after their tail; a map change that keeps the music does not cut it.
