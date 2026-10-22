# ADR-0076: The XA tracks are converted to WAV by the converter and played by the DLL

- **Status**: Accepted
- **Date**: 2026-10-22
- **Source**: this chantier: the author's answers of 2026-10-22 to O-E19-36 (`docs/plan-e19-opcodes.md`, section 3, decision D-E19-118) and to Q-H4-1 (`docs/plan-e20-heros.md`, section 2, decision D-E20-53)

## Context

- The opcode `0xB9 [v]` starts the XA track `v` (a 13-entry table at `0x800C4818` in `ALUN_CD.EXE`, France); it has 42 sites in 26 maps, none on the story chain. `0xBA` is the matching "track playing" predicate. Track 11 follows each received object (the fanfare, inferred from the scripts' context); tracks 7 to 10 are event themes (O-E19-36).
- The tomes' spells start XA tracks too (Q-H4-1). E20.h4a plans a logged stub for them.
- The extracted `ARAN_XA.XA` lost its audio: the tracks can only be decoded from the raw disc image `Alundra (France).bin` (O-E19-36).
- Today the port plays no XA track: `0xB9` is skipped and `0xBA` returns 0 (D-E11-5).
- Asked whether the XA tracks should be heard, the author answered that the converter must convert them to WAV so that they can be played.

## Decision

- The XA tracks are heard in the port.
- The converter decodes them from the raw disc image and exports each track as a WAV file, with the other exported audio.
- The DLL plays them wherever the original starts one: `0xB9`, the tomes' spells, and any other caller the slice finds.
- One slice carries the work, E19.sm7 (`docs/plan-e19-opcodes.md`, section 1.4), after the DLL restructuring. Until it lands, the callers keep a logged no-op (E20.h4a's stub, E20.e0c's fanfare).

## Consequences

- The converter needs the raw disc image as an input, not only the extracted files. The slice names that input, the WAV naming and the place of the files in the export, and documents them under `docs/formats/`.
- A full export plus its manifest proof validates the converter change, like any converter change.
- The engine may need a one-shot streamed player for a WAV that is not background music; the slice checks the existing audio first, and an engine gap is reported, not worked around.
- What `0xBA` returns while a track plays is settled in the slice's plan with the author. In the binary it reports "busy", so the "you got" box waits for the end of the fanfare; today it returns 0 (D-E11-5).
- The export grows by the size of the decoded tracks.
