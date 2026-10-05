# E19.f2b0 annex: the font3 glyph table of the binary

`glyph_table.txt` is the glyph table of `ALUN_CD.EXE` (France) at `0x800993C4`: 256 entries of 20 bytes, five
little-endian int32 each, `{w, h, srcX, srcY, yoff}`, indexed by the raw game code (column `idx`, hexadecimal).
`RenderTextBitmap` (`0x800478C4`) copies `w x h` texels of FONT3.TIM from `(srcX, srcY)` to `(pen, yoff)` and
advances the pen by `w`.

It was dumped straight off the binary by a read-only script of the E19.f2b discovery (2026-10-05), independently
of the decompilation. The decompiled `g_fontCharWidthTable` (`StaticVariables.cs:9484`) equals it on all 1280
ints. Every `yoff` is 0 and no entry has a zero width or height.

E19.f2b0 tests compare the exported `UI/font3.fnt` with this file, so it must not be regenerated from the
converter's own output.
