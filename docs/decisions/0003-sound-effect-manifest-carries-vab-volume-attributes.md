# ADR-0003: The sound effect manifest carries the VAB volume and pan attributes

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: this chantier: `docs/plan-audio-mix-exact-muet.md` (decision D2, proposal P9, tasks T2.3 and T3.3),
  branch `chantier/audio-mix-exact`. Analyser commit `b79b45a` (extractor). Engine counterpart: CasaEngine ADR-0039
  (software stereo voices).

## Context

- The author decided on 2026-09-25 that a sound effect voice must start, and be remixed by opcodes `0xAB`/`0xBF`,
  with the left and right volumes the original writes into the SPU, per tone.
- In `ALUN_CD.EXE` (France), those volumes are computed from the VAB master volume (`VabHdr.Mvol`), the program
  volume and pan (`ProgAtr.Mvol`/`Mpan`) and the tone volume and pan (`VagAtr.Vol`/`Pan`). The key-on path is
  `FUN_80090C58`, the remix path `0x80049794`. The binary checks are recorded in the plan (T0.2).
- Until now `sound/sfx.json` (extractor) and `Sounds/sfx-manifest.json` (converter) carried none of these bytes. The
  converter's reader turned a missing number into 0 (`SoundManifestReader.GetInt32`), and the writer serialises
  every property.
- A real 0 exists in the data: 15 tones have a volume of 0 in the original (for example tone 1 of sound 162).

## Decision

- `sfx.json` gains, per record, `VabMasterVolume`, `ProgramVolume` and `ProgramPan` and, per tone, `Volume` and
  `Pan`. The fields are appended at the end of each object, so the existing fields keep their place.
- The values are those of the record whose samples were actually exported, after the `RefSfxId` chain
  (`SoundBin.DecodeSfxTones` with its program attributes). They are never read from the unresolved record.
- Absence is represented by `null`, end to end, never by 0:
  - `null` for a record the extractor could not resolve (invalid, map VAB not resolvable, sequence-triggered);
  - a resolved record without tones keeps the real values of its program;
  - the converter reads missing or `null` values as `null` (`int?`) and writes them as `null` in
    `Sounds/sfx-manifest.json` (`vab_master_volume`, `program_volume`, `program_pan`, `volume`, `pan`), in the same
    form as the existing `skip_reason: null`.
- A consumer that finds `null` on a record it plays must fall back to its previous behaviour (for the Alundra DLL:
  unit volume, centred), and say so once in its log.

## Consequences

- The DLL can reproduce the original's per-tone stereo volumes from the manifest alone.
- A manifest produced from an older `sfx.json` stays usable: its fields read as `null`, never as a silent 0.
- Every exported record now carries three more record fields and two more tone fields; `sfx-manifest.json` changes
  once at the first export with the new `sfx.json`, then stays stable.
- On 2026-09-25 the values measured are uniform at the VAB and program level (127, 127, 64 on every record). Only
  the tone volume and pan vary, but the format keeps all five, since the original reads all five.
