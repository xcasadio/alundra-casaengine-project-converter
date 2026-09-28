# ADR-0009: font3 cells map to Unicode through their CP1252 byte, for proven characters only

- **Status**: Accepted
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e15-yarn.md`, E15.e (D-E15-14 to D-E15-16, the author's answers of 2026-09-28 after the in-game recipe showed wrong accent glyphs in the font3 dialogue box)

## Context

- The converter's `FontWriter` wrote `UI/font3.fnt` with each atlas cell keyed on the Unicode character given by a CP850 table (a port of the analyser's `TextDecoder.ConvertCp850ToLatin1`), resolving collisions in favour of the CP850 entry.
- The original game draws an escaped character `}c` from atlas cell `0x90 + c` and `{c` from cell `0x50 + c` (`alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs`, glyph selection and the escape table's comment): `}Y` is cell `0xE9`, "é" in CP1252/Latin-1, not CP850's 130. The atlas confirms it: cell 130 holds a comma-like glyph and cell 233 holds "é"; cell 151 holds a dashed glyph and cell 249 holds "ù"; the same holds for every accented letter of the text.
- The extractor already turns the escape pairs into UTF-8 (`TextDecoder.DecodeString`, called by `GameMap.cs` and `EtcRes.cs`), so the text is correct; only the cell-to-character table was wrong. The corpus uses 17 non-ASCII characters (é, à, è, ê, ç, î, ô, â, œ, û, ù, Ç, °, ï, «, », É); all but "°" were drawn from a wrong cell and "œ" had no glyph at all.
- The author asked that nothing be invented: only proven associations are kept.

## Decision

- The texts stay as extracted; no conversion is added at run time.
- `FontWriter` keys font3 cells as follows: codes below 128 keep their own value (as before, glyph markers 16 to 29 included); above 127, only the 17 characters of the text are associated, each with the cell of its CP1252 byte (é 0xE9, à 0xE0, è 0xE8, ê 0xEA, ç 0xE7, î 0xEE, ô 0xF4, â 0xE2, œ 0x9C, û 0xFB, ù 0xF9, Ç 0xC7, ° 0xB0, ï 0xEF, « 0xAB, » 0xBB, É 0xC9). Every other cell above 127 has no character: it is absent from `font3.fnt` and listed in `font3-charset.json` without a code point.
- The CP850 table and its collision handling are removed; each glyph keeps its own proportional advance (`FontCharWidths.csv`).

## Consequences

- Accented text renders with the original's glyphs in the dialogue box and the inventory, both in font3.
- A character the text does not use yet has no glyph until its cell is proven; a new character in the data would render as missing, which the corpus tests and the recipe would reveal.
- `font3-charset.json` changes shape for unmapped cells (no code point); nothing reads it at run time.
