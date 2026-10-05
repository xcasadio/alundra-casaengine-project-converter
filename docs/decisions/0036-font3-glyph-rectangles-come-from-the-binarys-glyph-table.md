# ADR-0036: The font3 glyph rectangles come from the glyph table of the binary

- **Status**: Proposed (decision taken by the session while planning E19.f2b0, to be confirmed by the author)
- **Date**: 2026-10-05
- **Source**: `docs/plan-e19-opcodes.md`, section "E19.f2b0" (discovery of E19.f2b, 2026-10-05); value annex `docs/plan-e19-f2b0-annexe/` (the table at `0x800993C4` of `ALUN_CD.EXE`, dumped off the binary). Complements ADR-0009 (which characters get a cell) and the advance table of the E12.b slice (`FontCharWidths.csv`).

## Context

- [binary] `RenderTextBitmap` (`0x800478C4`) copies, for each typed character, `w x h` texels of FONT3.TIM from `(srcX, srcY)` to `(pen, yoff)` and advances the pen by `w`. The five ints come from a 256-entry table of 20 bytes at `0x800993C4`, indexed by the raw code. Every `yoff` is 0 and no entry has a zero width or height. The decompiled `g_fontCharWidthTable` equals it on its 1280 ints; `FontCharWidths.csv` publishes only the first int of each entry (the advance).
- `FontWriter` wrote for each glyph the 16x16 grid cell of its source record (`ui/font3.json`) and the advance of the table. Of the 145 `char` lines of `UI/font3.fnt`, 129 named another window than the binary's; 16 glyphs were drawn differently (opaque texels not equal on an unclipped stamp): raw codes 30, 31, 39, 44, 45, 49, 58, 121, 122, 123, 127, 156, 171, 176, 187 and 233. 238 of the 256 rows of `font3-charset.json` carried a cell that is not the binary's rectangle.

## Decision

- The analyser publishes the whole table as `FontGlyphTable.csv` (`code;width;height;srcX;srcY;yOffset`, 256 rows), dumped once from `g_fontCharWidthTable`; the converter links it like `FontCharWidths.csv` and reads it with `FontGlyphTableCatalogReader`.
- `FontWriter` takes the rectangle of each glyph (`x`, `y`, `width`, `height` of the `char` lines and of `font3-charset.json`) from the table by its raw code, and `yoffset` from the table's `yOffset`. `xadvance` and the choice of characters (ADR-0009) do not change. With the CSV or a row missing, the rectangle of the source record is used and a warning is reported; the check of the source records against the grid stays.
- The tests compare the export with the annex (the binary's table dumped independently), never with the analyser's CSV or the converter's own output.

## Consequences

- `UI/font3.fnt` and `UI/font3-charset.json` change at the next full export; every screen drawn in font3 (inventory, sub-inventory, save screen, dialogue box) shows the glyphs of the binary: commas, dashes, "1", colons, quotation marks, "oe" and the degree sign at their place, no stray pixels of the neighbouring cell after "y", "z" and "e acute".
- The DLL and the engine do not change. The measures of `AlundraFont3GlyphTests` (advances, wrapping) do not move (checked with a font rewritten with the table's rectangles).
- The exact place of the pixels under MGUI is only proven with the box view of E19.f2b1 (test on a real GPU).
- Revert: the analyser pointer, the commits of the converter, then a full export in place.
