# Loop at the sequence marker (section 1.2w): design annex

Plan: `docs/plan-e19-opcodes.md`, section 1.2w, decisions D-E19-70, D-E19-72 to D-E19-76 and D-E19-102 (2026-10-08: the session does the
engine part L-a, then the data, converter and DLL part L-b).

Versioned on 2026-10-08 from the session scratchpad, where it was prepared on 2026-10-05 (it had not been versioned then):

- `disc/`: the read-only discovery of the binary loop markers (the libsnd driver stores the position at controller 99 = 20 and jumps
  back at controller 99 = 30, `0x8008CA40`; 38 looping tracks, 8 one-shot; the stable state from the second pass on), and its fresh
  counter-check `disc/verify/verify.md`.
- `engine/`: the L-a design (`SoundAsset.LoopStartSample`, seeking the WAV stream reader to a frame, `MusicPlayer.FillQueue` and the
  streaming worker looping to it, a cap of buffers per update, test values from `fillsim.py`), and its counter-check `engine/verify/`.
  Written against the engine of 2026-10-05: re-check every anchor against the current engine before planning.
- `data/`: the L-b value audit and oracle (option B: render to the second jump J2, `LoopStartFrame` = J1, `LoopStartSample` = J1 × 735,
  a one-frame linear crossfade, cap 480 s, release tails of the 7 one-shot tracks; predicted changes of the extraction and the export;
  the oracle's SHA-256 table `oracle_sha256.tsv`), and its counter-check `data/verify/`. The oracle WAV files themselves (about 16 GB with
  the working copies) stay in the session scratchpad (`loop-data/oracle/`) and are not versioned.
- The scripts read the game data, the binary and the scratchpad through absolute paths of the author's machine: adapt them first.
- `la-1008/`: the discovery of 2026-10-08 for L-a against the engine at `f86a981b` (rules, values written in advance by `sim.py`,
  closed file list, depth table) and its counter-check `la-1008/verify/` (the values replayed as real xUnit tests in a physical copy, all
  equal; mutations; corrections M1 to M7).
- `lb-1008/`: the discovery of 2026-10-08 for L-b and O-E19-66 (the extractor prototype `proto_extractor.patch` reproduces the oracle byte
  for byte and gives the font3 fix) and its counter-check `lb-1008/verify/` (corrections M1 to M8: the font3 decode belongs in
  `AlundraDataExtractor/Program.cs`, not in the decompiled `AlundraEngine`; five reference images and three GPU tests hold the old
  index-4 colour; the export size target is re-based).
