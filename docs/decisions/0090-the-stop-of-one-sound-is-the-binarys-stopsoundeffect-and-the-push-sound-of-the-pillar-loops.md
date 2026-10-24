# ADR-0090: The stop of one sound is the binary's StopSoundEffect (opcodes 0xBE and 0x75, the end of C76) and the push sound of the pillar loops

- **Status**: Accepted
- **Date**: 2026-10-24
- **Source**: this chantier: `docs/plan-e20-heros.md` section 1.24.6, sub-section E20.g5c (revision 1, D-E20-67, D-G5C-1 to D-G5C-8); reading of `ALUN_CD.EXE` (France): `StopSoundEffect` `0x80049634` with its voice finder `0x80049714`, the handlers `0x80041C6C` (0xBE) and `0x8003FA9C` (0x75), replayed on the MIPS machine (`docs/plan-e20-annexe/e20-g5/g5c-disc/`, `stop_oracle.py`, `handlers_oracle.py`); measured results in `docs/plan-e20-annexe/e20-g5/g5c/README.md`

## Context

- The binary has seven calls to `StopSoundEffect` (`0x80049634`): the opcodes `0xBE` and `0x75`, the end of C76 (the pillar, id 25), C39 and C40 (id `0x55`), C49 (two, id `0x62`). The decompilation defines the function but renders the seven by `PlaySoundEffect`; the DLL carried that error (`0xBE`, `0x75` and the end of C76 replayed the sound).
- `StopSoundEffect(id)` does nothing for the id 0; otherwise, if the bank is not loading, every voice of the REQUESTED id is key-offed and freed at once, whichever object started it. It reads neither the countdown that blocks `Play` nor the anti-duplicate table nor the fade machine.
- 371 of the 392 `0xBE` sites (119 maps, 12 on the story chain) target a looping sound. The replay was refused by the polyphony cap (`MaxVoices` 1), so the scrape of stone (id 25) never stopped.
- ADR-0086 said the push sound of the pillar is short; it loops (id 25 is `repeat: true`, `max_voices: 1`).

## Decision

- `ISoundPlayer.StopSfx(int sfxId)` is a default member that does nothing (the 25 implementers are not broken); `SoundPlayer` and the test recorders override it (D-G5C-1).
- `SoundPlayer.StopSfx` has the semantics of the binary: every live voice of the requested id (the redirection by `RefSfxId` included, both tones of the id 85) is stopped and forgotten; it is not gated by the armed state; the per-frame anti-duplicate table is neither read nor written; nothing for the id 0, an unknown id or a finished voice (D-G5C-2).
- `0xBE` calls `StopSfx((b2 << 8) | b1)` and `0x75` calls `StopSfx(b1)`; `0xBD` and `0x12` stay plays. `0x75` has no site in the data; it is ported with `0xBE` for the same defect (D-G5C-3). The labels of `EventOpcodeSizeTable.cs` come from the decompilation and stay (D-G5C-7).
- The pillar calls `StopSfx(25)` once when the contact ends after a push, in place of its named stub (`StopSoundStubCallsForTests` is removed). This amends ADR-0086 (D-G5C-8): its stub is plugged and its sound is not short.
- The stop is net (`AudioService.Stop`); the faithful release of about 0.2 s is the engine gap G1, recorded in `docs/plan-e20-annexe/e20-g5/g5c/engine-gaps.md` (D-G5C-4).
- One scrape at a time (cap 1), as in the original: a second object that asks for it moves silently, and the stop of the first cuts the sound of the other (D-G5C-5).
- The stops of the native handlers C39, C40 and C49 stay with the theme of the enemy AI, which will port those handlers; the API serves them (D-G5C-6).

## Consequences

- The scrape of the statues, blocks, buttons and pillars ends with the move (map 26 pillar: the voice dies at the release, frame 103; the button of Torla on map 426: the `0xBE` of the frame 215 ends it).
- Two existing tests moved (renamed, by derivation): the `0xBE` and `0x75` dispatch tests now assert the stop list and an empty play list. The two pillar test files read the stop from the recorder; no expected value changed.
- The sound is cut net, not faded (gap G1); the mixer loop window is a known gap (G2), out of this slice.
- Revert: `git revert` of the code commit; the sound loops again and the pillar gets its stub back.
