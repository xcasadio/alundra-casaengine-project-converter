# ADR-0025: Numeric text codes and \Y are positioned Yarn markers

- **Status**: Accepted
- **Date**: 2026-10-03
- **Source**: this chantier: `docs/plan-e19-opcodes.md` D-E19-48 (the author's answer to Q-F1, 2026-10-03) and section 1.2j.1 (E19.f0)

## Context

- A numeric text code `\<digits>` sets a temporary flag. In the France executable the text interpreter sets it when it reaches the code and moves on to the next character in the same step (`ALUN_CD.EXE` `0x80046B9C` to `0x80046CC4`, then `j 0x80046100`); `\Y` ends the render step (`0x800463B8`, `j 0x80046EBC`).
- ADR-0006 exported each numeric code as the command `<<flag n>>` before the line of its page and dropped `\Y`. The position of the code in the text was lost (`YarnTextEmitter.BuildPageRender`).
- Measured on the corpus (E19.f0 survey and value audit): 932 numeric codes in 627 pages and 155 of the 485 `.yarn` files, 87 before any glyph, 647 after the last one, 198 in the middle; 922 `\Y`, 921 of them right after a flag; 58 pages hold only flags.
- Yarn Spinner 3.2.1 keeps the exact position of a self-closing marker (11 182 `glyph` markers are proved by `YarnCorpusEquivalenceTests`).
- The typewriter of E19.f2 needs both positions: a flag fires when its glyph is drawn, and `\Y` sets the cadence.

## Decision

- A numeric code becomes `[flag id=N trimwhitespace=false/]` at its position in the text of its page (`N` in normalised decimal); a `\Y` becomes `[yield trimwhitespace=false/]` at its position. No `<<flag n>>` command is emitted any more; `<<falcon_update>>` is unchanged.
- At one position the source order is kept. The edge-space trim (D-E15-8) and the empty-page rule ignore these two markers: a space between the last visible unit and a flag is still removed, and a page holding only flags and yields is written as its markers in source order followed by `[empty/]`.
- A code above `int.MaxValue` is a conversion error (Yarn marker properties are integers).
- `report.json` counts `Yarn.FlagMarkers` and `Yarn.YieldMarkers` in place of `Yarn.FlagCommands`.
- The DLL sets the flag of every `flag` marker of a line when the line is shown, before forwarding it, in list order (behaviour unchanged, D-E12-4, until E19.f2); `yield` and unknown markers are ignored. The `flag` command handler stays for older exports.
- The delivery order is the DLL first, then the export (a new export read by an old DLL would set no text flag).
- The test oracle (`ReferenceTextDecoder`) is updated from this contract, never from the emitter.

## Consequences

- The visible text, the exported file set and the catalogue do not change: only the 155 `.yarn`, the 155 `.dialogue` and `report.json` differ from an ADR-0006 export.
- E19.f2 can fire each flag at its glyph and pace the typing on `yield` without a new conversion.
- An export at the new format needs the DLL built from this change; reverting means re-exporting with the previous converter while keeping the new DLL.
- Supersedes the numeric-code and `\Y` bullet of ADR-0006.
