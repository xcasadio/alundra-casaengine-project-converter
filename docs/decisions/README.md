# Architecture Decision Records

This folder records the architecture decisions of this repository: architecture, data formats, public APIs, backends, and the rules that govern the AI agents working on it.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`).
- Backfilled decisions keep a `Source` pointing at the original document. Existing audits are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| ADR-0001 | The game's XAML screens embed their XAML in the Alundra DLL | Superseded by ADR-0002 | 2026-09-21 |
| ADR-0002 | The game's screens are project assets bound to observable view models | Accepted | 2026-09-24 |
| ADR-0003 | The sound effect manifest carries the VAB volume and pan attributes | Accepted | 2026-09-25 |
| ADR-0004 | Background music follows the executable's sequence state | Accepted | 2026-09-25 |
| ADR-0005 | The inventory's opening portrait reaches the DLL through map_alundra.json and a one-id index | Accepted | 2026-09-26 |
| ADR-0006 | All Alundra text is authored as Yarn and the raw text tables are no longer exported | Accepted; `\X` bullet superseded by ADR-0007 | 2026-09-27 |
| ADR-0007 | The falcon update command keeps the state it replaces, and each `\X` reads on the original's side of the update | Accepted | 2026-09-27 |
| ADR-0008 | The dialogue box uses font3 and draws each glyph marker as the matching font3 character | Accepted | 2026-09-28 |
| ADR-0009 | font3 cells map to Unicode through their CP1252 byte, for proven characters only | Accepted | 2026-09-28 |
| ADR-0010 | Game flags stay in the DLL, and Yarn reads and writes them as `$flag_n` and `$tmp_flag_n` | Accepted; D-E16-16 range superseded by ADR-0011 | 2026-09-28 |
| ADR-0011 | Yarn flag variables cover only the original's 64-word banks (ids 0 to 2047) | Accepted | 2026-09-28 |
| ADR-0012 | The Alundra save game holds the original's saved state and is validated field by field before it is applied | Accepted | 2026-09-28 |
| ADR-0013 | A save is loaded through the warp departure path and applied at the arrival map's entry; the recipe keys exist only in Debug builds | Accepted | 2026-09-28 |
