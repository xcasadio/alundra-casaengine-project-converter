# ADR-0004: Background music follows the executable's sequence state

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: this chantier: `docs/plan-bgm-demarrage-binaire.md` (facts B1-B18 read in `ALUN_CD.EXE`, decisions D5-D8,
  task T2.1 and its validation note), approved by the author on 2026-09-25

## Context

The DLL's background-music port (plans E11.c and E11.b) was written from the decompilation. Reading the French
executable showed that it does not match the original:

- In `ALUN_CD.EXE`, `PlaySeq` (`0x8008f188`) starts background music from one place only: `StopAllSound`
  (`0x80049af4`). `StopAllSound` returns at once when `g_currentMapSoundIndex` is negative.
- Loading a sequence never plays it. The sequencer only advances a sequence whose play bit is set, and opening a
  sequence never sets that bit.
- `InitializeBgm` stops the sequence and rewinds it. It does not restart it.
- A map entry loads the track and sets `g_resetSoundFlag`. The frame function consumes that flag through
  `StopAllSound`, after the frame's scripts.
- Opcode `0xA7` with a zero stop-all operand loads the track without playing it. The corpus plays such a track later
  with `0xA5`.
- The warp departure (`HandleMapSoundEffects`, called after the frame loop) never loads music. It arms the fade, or
  stops the music.
- The DLL did something else: it played on load, restarted on `0xA5`, `0xA6 0` and the fade's swap tick, played
  track 1 on the 21 maps whose index is `-1`, and loaded the destination track at warp departure.

## Decision

- `AlundraMusicPlayer` holds the executable's state: the raw current map sound index (never remapped, so `-1` stays
  `-1`), a loaded track or none, and the reset flag. A live voice means the sequence is playing.
- `StopSequence` ports `InitializeBgm`: it stops the voice and keeps the track loaded. `PlaySequence` ports
  `PlaySeq`: it leaves a live voice alone and starts a loaded, silent track from the top. `RestartIfActive` is removed.
- `AlundraBgmFadeDirector.StopAllSound` does nothing when the index is negative. Otherwise it disarms an armed fade,
  restores the master volume and calls `PlaySequence`. `LoadBgm` always clears the reset flag. `LoadBgm(0)` and the
  fade's swap tick stop the music.
- A map entry loads the track and sets the flag. `AlundraWorldProxy.Update` consumes the flag at the frame close,
  before the anti-duplicate flush.
- `0xA7 n>0` loads without playing and clears the flag. `0xA7 0` unloads. The runner calls `StopAllSound` only when
  `n > 0` and the stop-all operand is set.
- A warp departure records a request. It is evaluated at the frame close, after the flag: a different index with a
  silent warp sound (`seq_num == -1` and `max_voices == 0`) arms the fade directly; with an audible sound it calls
  `LoadBgm(0)`. The destination track plays at arrival.

## Consequences

- Faithful to the original: the 21 maps with index `-1` are silent, and a fade stops the music until `0xA5` or the
  next map entry. The corpus's canonical block (fade, silent load, wait, `0xA5`) starts the new track at `0xA5`.
- The map-entry music now starts at the end of the first frame instead of at world install. A script's `0xA6` in that
  frame cancels it.
- Declared deviations: loads are instant, so there is no CD streaming latency and `0xA8` stays 0; the side effects of
  a pending `StopAllSound` are not reproduced when `0xA7` shares the entry frame (P8); an armed `StopAllSound` stops
  the sound effect voices but does not cut the notes of a playing track (P5).
- Tests that drove the singletons must now run a frame close before they assert on music, and every class that
  installs a world or departs through a warp resets both session singletons.
- Deferred: the sound-effect half of the warp departure (a silent-but-playable warp sound, 379, still plays); the BGM
  calls of two AI routines and of `0x8004a9e0`; the flag-conditioned override table.
