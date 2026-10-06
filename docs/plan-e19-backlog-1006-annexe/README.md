# E19 backlog annex of 2026-10-06 (night)

Plan: `docs/plan-e19-opcodes.md` (sections 1.2w, 1.2s, open points O-E19-9, O-E19-63 to O-E19-66, O-E19-70, O-E19-76, O-E19-77).

- `notes.md`: read-only discovery of the remaining E19 backlog: the loop at the sequence marker (section 1.2w: the engine half
  L-a is not done anywhere; L-b is designed and has an oracle), E19.d2c2 / D5b, E19.h2, the stale K2 header, O-E19-57, O-E19-63,
  O-E19-64, O-E19-66, O-E19-9, the engine gap report entries for O-E19-65 and O-E19-70.
- `verify/verify.md`: the fresh counter-check. **Where it disagrees with `notes.md`, the counter-check wins:**
  - D-E19-74 is an author decision (the engine half of the loop goes to S2 of `chantier/audio-modern`); S2 merged without it.
    Taking it onto the E19 stack changes an author decision: it is a question for the author, not a session choice.
  - the stream margin near a loop seam is about 96 ms (the seam short buffer takes one of the three slots), as D-E19-75 states;
    at depth 24 the seam case is about 2046 ms.
  - the E19.m backlog file is stale: E19.m5 closed most G1 items; the real remainder is G3, G4, G5, G7 and the late G1 items
    (M-20, M-25, M-32, M-42, M-44), plus M-16, M-17, SJ-12 (D5b) and the test half of M-45.
  - `docs/formats/audio-manifests.md` and the editor's `CreatePreviewCopy` are missing from the L-b and L-a lists.
- `font3pal.py`, `verify/pal.py`, `verify/idx4.py`: the font3 index 4 colour (O-E19-66); `scan_zero_loops.py`, `verify/scan0.py`:
  the 5 zero-duration loops (O-E19-9); `verify/seam.py`: the stream seam margin.
- The scripts read the game data through absolute paths of the author's machine: adapt them first.
