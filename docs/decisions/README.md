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
| ADR-0006 | All Alundra text is authored as Yarn and the raw text tables are no longer exported | Accepted; partly superseded by ADR-0025 (numeric codes and `\Y`); `\X` bullet superseded by ADR-0007 | 2026-09-27 |
| ADR-0007 | The falcon update command keeps the state it replaces, and each `\X` reads on the original's side of the update | Accepted | 2026-09-27 |
| ADR-0008 | The dialogue box uses font3 and draws each glyph marker as the matching font3 character | Accepted | 2026-09-28 |
| ADR-0009 | font3 cells map to Unicode through their CP1252 byte, for proven characters only | Accepted | 2026-09-28 |
| ADR-0010 | Game flags stay in the DLL, and Yarn reads and writes them as `$flag_n` and `$tmp_flag_n` | Accepted; D-E16-16 range superseded by ADR-0011 | 2026-09-28 |
| ADR-0011 | Yarn flag variables cover only the original's 64-word banks (ids 0 to 2047) | Accepted | 2026-09-28 |
| ADR-0012 | The Alundra save game holds the original's saved state and is validated field by field before it is applied | Accepted | 2026-09-28 |
| ADR-0013 | A save is loaded through the warp departure path and applied at the arrival map's entry; the recipe keys exist only in Debug builds | Accepted | 2026-09-28 |
| ADR-0014 | The save book reproduces the original's memory-card save screens over four file slots | Accepted | 2026-09-29 |
| ADR-0015 | The event interpreter ports the missing opcodes from the binary, with a production loop guard and the E4.d detour for scripted walks | Accepted | 2026-09-29 |
| ADR-0016 | A blocked step on the cell field advances to contact, and ForceAdjusted keeps its shortfall rule | Accepted; D-E19-10 superseded by ADR-0017 | 2026-09-29 |
| ADR-0017 | ForceAdjusted rises only on a tick without progress, like the binary | Accepted | 2026-10-01 |
| ADR-0018 | The NPC animation lag is kept, arcs load real prefabs, and the engine gets an exact animation clock | Accepted; D-E19-16 refined by ADR-0019 | 2026-10-01 |
| ADR-0019 | Animation ends become exact on a logical clock while sprites stay in real time, and loops are counted | Accepted | 2026-10-01 |
| ADR-0020 | Opcode 0x24 is ported with a census of its waits, and opcodes 0x40/0x41 are ported in full | Accepted | 2026-10-01 |
| ADR-0021 | Entity contacts, scripted jumps and the hero's water and ice rules follow the binary, and test saves feed the recipes | Accepted | 2026-10-02 |
| ADR-0022 | Native-destruction obstacles stay solid, the walk detour waits on entities, and overlaps stay faithful | Accepted | 2026-10-02 |
| ADR-0023 | The hero jumps, falls and lands like the binary, with the take-off sound, and dialogue fidelity joins E19.f | Accepted; partly superseded by ADR-0028 (the "take-off only" sound scope) | 2026-10-02 |
| ADR-0024 | Destroyed entities are recycled through the engine like the binary | Accepted | 2026-10-03 |
| ADR-0025 | Numeric text codes and \Y are positioned Yarn markers | Accepted | 2026-10-03 |
| ADR-0026 | Absolute writes of Z follow the DLL convention (binary minus 1) at spawn, 0x8A and 0x64 | Accepted | 2026-10-03 |
| ADR-0027 | The native image is 320x240 at an integer scale with black bands | Accepted | 2026-10-03 |
| ADR-0028 | Every animation switch plays its sound, appearances included | Accepted | 2026-10-03 |
| ADR-0030 | The extractor writes a per-texel alpha code, effect sheets, dialogue portraits and the odd last column | Accepted | 2026-10-03 |
