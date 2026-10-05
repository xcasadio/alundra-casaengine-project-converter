# ADR-0038: The trailing spaces of a centred last line are kept, guarded by empty

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: this chantier: `docs/plan-e19-opcodes.md` D-E19-78 (the author's answer to Q2 of E19.f2b, 2026-10-05) and D-E19-84 (session technical choice); evidence in `docs/plan-e19-f2b1-annexe/s025-notes.md` and `s025-verify.md`

## Context

- ADR-0006 (D-E15-8) removes the spaces at the edges of a page, as Yarn does at compile time. ADR-0025 made the walk transparent to the `flag` and `yield` markers: a space between the last visible unit and a trailing flag is still removed.
- The France executable counts every space of a centred line in its width (`CalcTextWidth` at `0x8004771C`, 4 px per space in font3) and the text box types them as any character.
- Measured on the 823 `\H` of the corpus, four ways (raw strings with the binary rule, exported elements, exported `.yarn`, every compiled `.dialogue` through the DLL's own tokenizer) and on the original disc data (`DATAS.BIN`, `ETC_RES.R`): exactly three centred lines change width, the last line of S025 of maps 472, 473 and 474 (`Florin\W5Roulette` followed by 8 spaces): 125 px in the binary, 93 px exported, drawn at x 113 instead of 97.
- Yarn Spinner 3.2.1, probed with the project's own compiler, string table and line parser: the compiler removes the spaces that end a line, with or without a `#line` tag, and a non-breaking space too; it keeps the spaces that precede a self-closing marker (`empty`, `yield`, `flag`, any name). Keeping the spaces in the emitter alone therefore changes nothing.
- The DLL, the `Alundra.Tests` oracle and the converter's equivalence harness already read `empty` as a no-op.

## Decision

- When the last line of a page (the elements after its last `\N`, or the whole page) holds a `\H`, the emitter keeps the trailing spaces of the page, flag and yield markers included. If the page then ends on a text run that ends on a space, it appends `[empty trimwhitespace=false/]` at the end of the line. A trailing `flag` or `yield` marker already protects the spaces: no guard is added in that case. The leading-space trim and every other page are unchanged.
- This rule wins over the transparency of `flag` and `yield` of ADR-0025 for a centred last line (spaces before a trailing flag are then kept), and over D-E15-8 for the same lines.
- The independent test oracle (`ReferenceTextDecoder`) takes the same rule from this decision, never from the emitter (the rule of ADR-0025).
- The marker is `empty`, reused, not a new name (no new vocabulary, no new case in the test helpers).

## Consequences

- Export delta: exactly the `.yarn` and the `.dialogue` of the three pub maps (`line:M472_S025_p0`, `M473_S025_p0`, `M474_S025_p0`, 37 bytes longer each: 8 spaces and the marker); `program_base64` and every counter of `report.json` are unchanged.
- The page types 43 steps instead of 35, as in the binary (ADR-0029).
- Same family, not covered and left as named gaps (O-E19-67 of the plan): the other edge spaces D-E15-8 removes (164 pages lose trailing spaces, 4 a leading one), among them three inventory texts of the ETC table (`0x206`, `0x239`, `0x2A6`) that lose a trailing space outside the box. The decompilation removes trailing spaces in `CalculateTextWidthFromScript` (`TextDecoder.cs:967-970`), a deviation: it is not a width reference and is not corrected.
- Partly supersedes ADR-0006 (edge spaces of a page) and refines ADR-0025 (transparency of the two markers).
