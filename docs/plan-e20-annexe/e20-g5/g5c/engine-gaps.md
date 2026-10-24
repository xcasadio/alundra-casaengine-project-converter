# E20.g5c - engine gaps (read-only record, nothing worked around)

Plan: `docs/plan-e20-heros.md` section 1.24.6, E20.g5c (D-G5C-4, Q-G5C-4). The engine (`CasaEngineMonogame/`) is only read by this slice; the stop of one sound is built on
`AudioService.Stop(handle)` (net stop). The two gaps below are recorded here, not fixed.

## G1 - no key-off with a release per tone

The original key-offs a voice and lets it die on the ADSR release of its tone (`ADSR2 & 0x1F`, bit 5 = exponential mode). Measured from `SOUND.BIN`
(`docs/plan-e20-annexe/e20-g5/g5c-disc/vab_release_all_out.txt`, report section 2): the release is 0.006 to 0.74 s depending on the id (0.186 s for the scrape 25, 0.743 s for the id 85,
0.046 to 0.093 s for 353, 0.372 s for 349).

- What is missing: the release time of each tone is neither in `sfx.json` nor in `Sounds/sfx-manifest.json`, so even `AudioService.StopWithFade` has no duration to use.
- What the DLL does meanwhile (D-G5C-4): `SoundPlayer.StopSfx` stops each voice at once (`AudioService.Stop`). The scrape of the statues, blocks, buttons and pillars ends
  net at the end of the move; the original lets it fade over about 0.2 s.
- What a faithful fix needs (one more slice, not started): the extractor exports the release time per tone, the converter writes it in the manifest, the DLL calls `StopWithFade`
  with it. Asked to the author as Q-G5C-QA1 (non blocking, default: the net stop).

## G2 - the mixer loops over the whole clip (already known, out of g5c)

`StereoVoiceMixer.WriteFrames` wraps to sample 0 over the whole clip, without the `loop_start` / `loop_end` window of the tone. This changes the character of a loop, not its stop;
g5c does not depend on it and does not touch it.
