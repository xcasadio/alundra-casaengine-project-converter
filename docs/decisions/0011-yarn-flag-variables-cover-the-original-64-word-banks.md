# ADR-0011: Yarn flag variables cover only the original's 64-word banks (ids 0 to 2047)

- **Status**: Accepted
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e16-etat-partie.md` §0.1 (D-E16-25, the author's answer of 2026-09-28 after the review that followed the E16.0 measurements) and §2 (E16.0, Q1 and Q3)

## Context

- ADR-0010 (D-E16-16) let `$flag_n` and `$tmp_flag_n` address any `n` from 0 to 32767, the range the DLL's two 1024-word banks can hold.
- E16.0 measured the original: `g_saveData` holds `GameFlags` on 64 words and `ClearTemporaryFlags` clears a 64-word `g_temporaryFlags` (`ALUN_CD.EXE` `0x800814A4`–`0x800814BC`, `0x8008159C`); no flag id of the corpus reaches 2048 in either bank. E16.c therefore saves `GameFlags` on 64 words and zeroes the whole in-memory bank before restoring them.
- With ADR-0010's range, a Yarn write to `$flag_n` with `n` ≥ 2048 would set a bit the save never writes, and a load would drop it without a message, against D-E16-18.

## Decision

- **D-E16-25** — `$flag_n` and `$tmp_flag_n` accept `n` from 0 to 2047 only, the ids of the original's two 64-word banks. Any larger `n` is refused like any other name (D-E16-18): logged once per name, no exception, no state change.
- This replaces the range of D-E16-16 in ADR-0010; the rest of ADR-0010 stands.

## Consequences

- Every flag a Yarn text can write is saved (`GameFlags`) or deliberately not saved (`TemporaryFlags`), exactly like the original's; nothing is lost silently at load.
- E16.f tests the bounds 0, 31, 32 and 2047 in both banks, and refuses 2048 and above.
- The DLL's in-memory banks keep their 1024 words; only the Yarn names are narrowed.
