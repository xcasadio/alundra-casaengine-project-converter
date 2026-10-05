# E19.f2b1 discovery: values written in advance (read-only)

Repository `chantier/e19-suite` @ `897504b` (nothing built, run, exported, edited, staged or committed in it; `git status` before/after: only the
submodule markers). Binary = `ALUN_CD.EXE` (France), re-read with capstone through `../oracle/bdis.py` (lib of `../../e19j-disc/lib.py`).
Tags: **[bin]** re-read in the binary now; **[code]** read in the repository; **[calc]** computed by the scratch scripts below; **[hyp]** not proven.
Everything is under `C:\...\scratchpad\f2b1-disc\`: `oracle\` (model, compositor, scripts), `values\` (this file + `S1..S6.csv`), `pixels\` (reference PNGs).

## 0. Headline

1. The f2a model extended with the binary's DRAWN state exists and is validated: `oracle/model_drawn.py` (`DrawnBox`, imports `e19f2-disc/model/model.py`
   unchanged). Its timeline equals `model.run` frame by frame on 11 scenarios (`validate.py`: 4 selftests, `\T`, cursor, `\N\A`, 4C/4D, 391, 389, 164 real
   texts) and the hand-derived numbers of `selfcheck.py` hold. D-E19-62/63 are applied with `corrected=True` (same as `AlundraTextBoxOracle`/`FBox`).
2. **Correction of D-E19-81 detail 3 (hypothesis H1, strong).** The "one-frame descender row at y = 172 on the scroll-ending pass" is probably NOT drawn by the
   binary: `ScrollText` issues `ClearImage` on the VRAM band of the OLD top row in the same pass, and the OT is drawn later (see 2.5). The visible picture of the
   ending pass is texel for texel the picture of the next pass (rows 1 and 2 at their post-shift positions, offset 0). This also removes the "jump a whole line
   for one frame" trap of `ScrollPixels = 16` + already-shifted `Lines`: on the ending pass the view just shows the shifted rows at offset 0.
3. The first pass of a box clips from 172 (cfg.Y = 168 left by the previous release), not from the DLL's `Y = 240` (`AlundraDialogueBox.cs:246`): invisible, but
   T1 must pin 172 (or exclude pass 1), never `min(240 + 4, 239) = 239`.
4. `oracle/refcompose.py`'s rules are right (clip lag via `prev_y`, rows passed by the caller) and my v2 (`refcompose2.py`) gives 0 differing pixels against it on 8
   states; both lack the ClearImage effect and v1's centring uses the whole row text instead of the `\H` rest-of-line width.
5. Corpus facts [calc]: 24,431 non-empty raw map strings typed to the end with the model: 0 errors (no kanji path), widest line exactly 255 px, none wider (the 255-px
   cut never triggers on static text); 823 `\H` in 511 strings, none with `\V`/`\X` after it, one not at the start of its line (map 394 string 5, see 5.4); no raw LF.

## 1. Rules of the drawn state, re-read in the binary [bin]

Callback slot 0, `MsgBoxRender 0x80046EF0`, table entry `0x800A731C` = `{0, cfg 0x8009CFBC, +8 = 16, +0xA = 5, +0xC = 32, +0xE = 6, init 0x80048304, update 0x80046EF0}`;
cfg bytes `10 00 a8 00 24 00 07 00` = (16, 168, 36, 7) (`rd` check). Order of one pass:

| Rule | Value | Address |
|---|---|---|
| 1 Clip (computed FIRST, from cfg.Y as left by the previous pass) | `top = min(cfg.Y + 5 - 1, 239)`; `h = 50`, or `240 - top` when `top + 50 >= 240`; `x = cfg.X + 16 = 32` (+ the draw-env clip origin, copied by `0x800859C8`), `w = 32*8 + 2 = 258`; `SetDrawArea 0x80085A70` makes x1 = x + w - 1, y1 = y + h - 1 (inclusive): rest = x 32..289, y 172..221 | `0x80046F10-0x80046FC4` (cfg.Y read `0x80046F20`, clamp `0x80046F30-0x80046F3C`, h `0x80046F48-0x80046F64`, call `0x80046FC0`); inclusive end `0x80085AB4`, `0x80085AC8` |
| 2 Branches | sliding (`g_dialog_flags 0x80152F08 & 3`): `UpdateUiBoxesPosition` then `RenderText`; slide done and bit 2: cfg restored from the slide origin (168), `DialogClosed`, nothing drawn; typed: `ProcessClose` then `RenderText`; scroll pending: `ScrollText` only; else interpreter then `RenderText` | `0x80046FCC-0x800470AC`, update call `0x80046FF4`, restore `0x80047038-0x80047050` |
| 3 Not drawn on the closing pass | `DialogClosed` zeroes the slot's entry flags (`sh zero` `0x80047CB8` via `0x80047CB0`, called `0x80045024`) and the dispatcher calls a callback only if `entry.flags & 1` (`0x80048144`); the frame cells are built by `0x800481F8` only for active entries (`0x8004822C-0x80048230`), after the callbacks (`0x8002BE5C` then `0x8002BE64`): no frame, no text | as listed |
| 4 Y used to draw | `UpdateUiBoxesPosition 0x80047DD0` writes cfg.Y (`0x80047EB0`) and the x0/y0 of every 8x8 cell (`0x80047F40-0x80047F44`) of the CURRENT double-buffer: that is why `settle = 2` (both buffers get the final Y); text y = cfg.Y + 5 + 16 i - offset; frame at (16, cfg.Y); cursor y = cfg.Y + 32. Slide block `0x80146FA8`: step 0, total 15, settle 2, start/target/origin | `0x80047DD0-0x80047F8C`; init `0x800451E8-0x800452D8`, close `0x80045F10-0x80045FCC` |
| 5 Row x | band of row i = `(bufX + i) % 3`; `w = lineWidth[band]` (`0x80149BE8`); `w != 0`: `x = cfg.X + (cfg.w*8 - w)/2` = `16 + (288 - w)/2` (signed halving); else `x = cfg.X + 16 = 32` | `RenderText 0x80045640-0x800456E8`, `ScrollText 0x80045B0C-0x80045C48` |
| 6 Row y, sprite | SPRT 255 x 16 (`w 0xFF`, `h 0x10`, `u 0`, `v 0x20 + 0x10 band`), opaque (`0x800842FC(p,0)`), raw texture (`0x80084324(p,1)`); texture = VRAM band (960, 288 + 16 band) | init `0x800453C0-0x800453F0` |
| 7 `\H` | at the `\H` step: cursor + 1, `CalcTextWidth(scriptBuffer + cursor)` stored in `lineWidth[(bufX + lineIndex) % 3]`, then back to the free codes | `0x800469F0-0x80046A5C` |
| 8 `CalcTextWidth 0x8004771C` | `{`: w[c + 0x50]; `}`: w[c + 0x90]; `\` + code through `0x80024098`: digits skipped (flag), `A` and `N` stop, `B..G`, `T`, `Y` skip the code char (width 0), `X` skips two, `W` = w[c - 0x20] (c < 0x41) or w[c - 0x27], everything else (`:`..`@`, `H`..`M`, `O`..`S`, `U`, `V`) re-reads the code char as a normal glyph; normal char = w[c] (table `0x800993C4`, 20 bytes, field 0); the model's `WIDTHS` = the annex field 0 on 256 codes [calc] | `0x8004771C-0x800478B8`; jump table dump `oracle/jt.py` |
| 9 Clearing of the widths | opening: the 3 widths zeroed; scroll end: the width of the new bottom band `(bufX' + lineIndex) % 3` zeroed AFTER the rows were drawn | open `0x80045380` (loop `0x80045440-0x80045464`); scroll end `0x80045DD8-0x80045DEC` |
| 10 ScrollText | waits while `count == len` (8) (start by `scrollMode` 8 / 2+press / 1 + 10-pass countdown / 4 + latch), then `count -= 1`; offset = `(len - count) * 16 / len` = 2, 4 .. 16 computed AFTER the decrement (0 while waiting); rows drawn with the OLD bufX; DR_AREA linked; when `count == 0`: pending 0, `bufX = (bufX + 1) % 3`, width cleared, `ClearImage(960, 288 + 16 * ((bufX' + 2) % 3), 64, 16)`: the band of the OLD top row; no cursor (no `RenderText`); the wait pass clears `cursorShown` | `0x80045988-0x80045E5C` (decrement `0x80045A90`, offset `0x80045B1C-0x80045B5C`, draw `0x80045AA0-0x80045CF8`, DR_AREA `0x80045D00-0x80045D64`, shift `0x80045D68-0x80045DB0`, clear `0x80045E30`) |
| 11 Cursor | only in `RenderText` when `cursorShown` (`0x80149CD0`): tick (`0x80149CD4`) + 1, wrap at 40, then image = tick / 10; `u = 0xB0 + 0x10 image`, `v = 0x38` read at `0x800A58CC + 40 image` (+0xC/+0xD; checked: b0, c0, d0, e0, all 38); 16 x 16 at `(cfg.X + 36*8 - 16, cfg.Y + 7*8 - 24)` = (288, Y + 32); tick reset to 0 only at opening (`0x800454F8`), not at the release; so the first cycle shows image 0 for 9 passes, the others 10 | `0x80045814-0x8004592C`, link into slot 0 `0x80045930-0x80045960` |
| 12 Slots / layers | slot 0 (head first): `DR_MODE`, frame cells (added by `0x800481F8` AFTER the callbacks, so drawn first), cursor; slot 2: `DR_AREA` (added last by `RenderText`/`ScrollText`, drawn first) then the three bands (added row 0, 1, 2, so drawn 2, 1, 0: no overlap); slot 3: full draw area; the frame and the cursor are never clipped | `0x800457A0-0x800457F4`, `0x80044C5C` |
| 13 Draw time | the UI OT is `DrawOTag`'d as the 6th call of `0x8002BA4C` (called at the end of the iteration by `0x80042798`, after `DrawSync(0)` `0x80084D68`); `ClearImage 0x80084EFC` (call `0x80084F68`), `LoadImage 0x80084F90` (`0x80084FD4`) and `DrawOTag 0x800852CC` (`0x80085324`) all enqueue through the same `ops[+8]` = `0x80086B0C` (ring of 64, head/tail `0x800C8238/0x800C823C`, FIFO, immediate when idle) | main loop `0x8002C3F4-0x8002C444` (iteration: debug input, RenderScene, Update(0), end of frame) |

Consequences used by the model: `drawn` is false on frame 0 of an opening (the box pass precedes the script tick), on the closing pass (`DialogClosed`) and whenever
the box is inactive; `Y` before the first pass is 168, not 240 (cfg static data; restored at each release `0x80047038`).

### 1.1 The extended record (per pass)
`DrawnBox.drawn` = `{kind RenderText|ScrollText, cfg_y_before, y, frame (16, y), clip (32, top, 258, h), offset, ending, rows_cpu[3], rows_visible[3], cursor}`; a
row = `{band, glyphs, width (lineWidth[band]), x, y}`; `cursor = {image, u 0xB0+0x10 image, v 0x38, x 288, y Y+32}`. `rows_cpu` is the CPU draw list (old rows
pre-shift on the ending pass), `rows_visible` has the old top row blanked on the ending pass (H1). Everything else is identical.

## 2. Value tables T1 can pin (generated, all passes in `values/S1..S6.csv`, markdown in `oracle/scenarios.md`)
Pass k = frame k of the model (opening opcode in tick N = 0; first box pass = 1; first glyph = 19). Columns of the CSV: pass; phase after the pass; drawn; kind; frame Y;
cfg Y before; clip x, top, w, h; offset; ending; per row text/x/y; cursor image/u/x/y. `corrected = true`.

### S1 open and slide-in (text "AB", no pad)
| pass | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| frame Y | 240 | 236 | 231 | 226 | 221 | 216 | 212 | 207 | 202 | 197 | 192 | 188 | 183 | 178 | 173 | 168 | 168 | 168 |
| clip top | 172 | 239 | 239 | 235 | 230 | 225 | 220 | 216 | 211 | 206 | 201 | 196 | 192 | 187 | 182 | 177 | 172 | 172 |
| clip h | 50 | 1 | 1 | 5 | 10 | 15 | 20 | 24 | 29 | 34 | 39 | 44 | 48 | 50 | 50 | 50 | 50 | 50 |

Rows empty on all 18 passes (bands cleared at the opening, interpreter idle during the slide); pass 19: row 0 "A" at x 32, y 173; pass 1's frame is at y 240 (off screen).
Pass 1 clip top is 172, from cfg.Y = 168 (not 239). Pass 18 is the slide-in "done" pass (RenderText, no write of Y).

### S2 typing a 2-line page ("Bonjour, Alundra !\N" + "deuxi" + 0xE8 + "me ligne", no pad)
Glyph every 4 passes from 19 (delay reset 4), the `\N` is a step of its own (pass 91): row 0 grows "B" 19, "Bo" 23, .. "Bonjour, Alundra !" 87; row 1 "d" 95 .. "deuxième ligne" 147; x 32, y 173 / 189
for every pass, clip 172..221 (Y 168 since pass 16); typing done 151; close trigger 511 (360 later). Full per-pass rows in `S2.csv`.

### S3 centred `\H` ("\B\HRoue de la fortune !\N\HFlorin\W5Roulette" + 8 trailing spaces, no pad)
Widths by `CalcTextWidth`: row 0 "Roue de la fortune !" = 111 px, x = 16 + (288 - 111)/2 = **104**; row 1 "Florin" + glyph 21 (14 px) + "Roulette" + 8 spaces (8 x 4) = **125**, x = **97**;
trimmed of the 8 spaces (what the Yarn gives after D-E15-8): 93, x = **113**. The x is final from the pass of the first glyph of the line (the `\H` is a free code of the
same step): pass 19 "R" x 104; pass 103 "F" x 97 (the `\N` step is pass 99). Row 0 stays at x 104, y 173 while row 1 is typed.
Also the mid-line quirk: map 394 string 5 `...\N` then `à la roulette\W2\999\Hum\T\W2\T\W2?\A...`: the width is that of the REST ("um", glyph 18, glyph 18, "?") = 47, so the band is at
x = 16 + (288 - 47)/2 = **136** with "à la roulette" typed from there [calc, `probe394.py`]. The only `\H` of 823 not at the start of its line.
A DLL view that scans tokens "from the centre token to the next new line / cursor / end" reproduces both.

### S4 scroll ("gypjq\Ndeux\Ntrois\Nquatre", scrollMode 3, no pad)
`\N` of line 3 at pass 83 arms the scroll; passes 84..92 wait (offset 0, no cursor, ScrollText); passes 93..100 are offsets 2, 4, 6, 8, 10, 12, 14, **16** (ending); next pass 101: bands shifted, offset 0.

| pass | 93 | 94 | 95 | 96 | 97 | 98 | 99 | 100 (ending) | 101 |
|---|---|---|---|---|---|---|---|---|---|
| offset | 2 | 4 | 6 | 8 | 10 | 12 | 14 | 16 | 0 |
| row 0 y | 171 | 169 | 167 | 165 | 163 | 161 | 159 | 157 (CPU list "gypjq", visible: blank) | 173 "deux" |
| row 1 y | 187 | 185 | 183 | 181 | 179 | 177 | 175 | **173** "deux" | 189 "trois" |
| row 2 y | 203 | 201 | 199 | 197 | 195 | 193 | 191 | **189** "trois" | 205 empty |

Clip 172..221 and frame Y 168 throughout; no cursor on any ScrollText pass; the new bottom band is empty and its `\H` width is 0 (x 32) until a new `\H` (`selfcheck.py`).
The visible picture of pass 100 equals that of pass 101 (checked on the composed images, `pixels.py`).

### S5 slide-out ("l1\Nl2\Ngypjq", pad held + pressed every pass, close at once)
Close trigger pass 31 (RenderText, Y 168); then, per pass from 31:

| pass | 31 | 32 | 33 | 34 | 35 | 36 | 37 | 38 | 39 | 40 | 41 | 42 | 43 | 44 | 45 | 46 | 47 | 48 | 49 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| frame Y | 168 | 168 | 172 | 177 | 182 | 187 | 192 | 196 | 201 | 206 | 211 | 216 | 220 | 225 | 230 | 235 | 240 | 240 | not drawn |
| clip top (prev Y + 4) | 172 | 172 | 172 | 176 | 181 | 186 | 191 | 196 | 200 | 205 | 210 | 215 | 220 | 224 | 229 | 234 | 239 | 239 | |
| clip h | 50 | 50 | 50 | 50 | 50 | 50 | 49 | 44 | 40 | 35 | 30 | 25 | 20 | 16 | 11 | 6 | 1 | 1 | |

The closing pass (49) restores cfg.Y = 168 and draws nothing (no frame). Row 2 ("gypjq", y = Y + 37) pixel rows kept by the lag: Y 172 -> rows 0..12 (13..15 cut), Y 177 -> 0..11 (12..15 cut),
Y 182 -> 0..11, Y 187 -> 0..11 (12..15 cut); from Y 192 the cut is the screen's. Rows 0 and 1 are never cut. The frame and the cursor are never clipped (y 168 + the frame's own shape).
(The summary of the earlier discovery saying "descenders only" was wrong; the verification C2 is right: row 12 is the baseline row of most glyphs.)

### S6 cursor ("a\Ab\Ac", presses at passes 60 and 120)
`\A` at pass 23: cursor image 0 (u 0xB0) at (288, 200) for passes 23..31 (9 passes), image 1 (0xC0) 32..41, 2 (0xD0) 42..51, 3 (0xE0) 52..59; release pass 60: no cursor;
the second `\A` (pass 68) continues the phase: image 3 at 68, 69, then image 0 70..79, 1 80..89, 2 90..99, 3 100..109, 0 110..119; none on ScrollText passes.

### Real texts (f2a oracle) [calc, `real_texts.py`]
389 S001, 391 S019/S020/S022, 164 S003: no `\H`; no scroll except 389 S002 (`\A` + 3 `\N`): scroll ending passes 147 and 163 with the pad held every pass; typing done 164. Their rows at typing
done are in the script output (x 32, y 173/189/205).

## 3. Pixel expectations (x1, clear (100,149,237)) (`oracle/pixels.md`, PNGs in `pixels/`)
Compositor v2 (`refcompose2.py`): glyph rectangles read from `docs/plan-e19-f2b0-annexe/glyph_table.txt` and asserted equal to the binary table at import (256 rows);
frame/font/wind PNGs from `alundra-project/UI/Textures`; clip inclusive. Classes are checked by the script: ink = 1 of the 14 opaque font3 colours (disjoint from the 7 frame
colours and 4 cursor colours). The frame's top rows 0..2 and bottom rows 54..55 are mostly transparent (ornate border; rows 52..53 partly), so the background shows there.

Rest pose (pass 151 of "Bonjour, Alundra !\Ndeuxième ligne\A", cursor image 0; row 0 pen end 105, row 1 82):
(16,168) bg (100,149,237) frame corner; (17,170) (88,96,72); (160,196) (152,152,112); (31,175) (184,176,144) frame left of the text; (32,175) (41,49,16) first ink of "B"; (32,174) (184,176,144);
(136,175) (99,107,74) the "!"; (32,196) (41,49,16) first ink of "d"; (288,200) (168,168,136) cursor corner; (295,207) (192,192,192) cursor.

Scroll offset 6 (pass 95; rows y 167/183/199): (34,172) ink (clip top kept); (45,171) (88,96,72) frame (ink above the clip cut); (52,170) bg (cut); (33,182) ink; (36,185) ink; (49,201) ink; (20,171) (184,176,144).

Scroll ending (pass 100): (33,172) (184,176,144) frame under H1 (the CPU list would draw ink (41,49,16) there: 14 pixels differ, all on y = 172); (36,175) ink; (49,191) ink.

Slide-out Y 182 (pass 35, clip 181..230 from Y 177; row 2 "gypjq" at y 219): (33,230) ink kept (pixel row 11); (34,231) (184,176,144) cut (a clip from the current Y would keep it); (32,232) (88,96,72) cut;
(33,234) (72,64,56) cut; (33,189) ink (row 0); (20,183) (184,176,144) frame at the current Y; (20,181) bg. The lag changes 55 pixels of this pass.

Centred S025 (pass 195): (104,175) ink / (103,175) (184,176,144); (97,191) ink / (96,191) (152,152,112). 255-px cut (synthetic 25 x "m"): (286,179) ink, (287,179) (168,168,136) not ink.
x2 references are nearest-neighbour upscales (HYPOTHESIS, only the GPU test proves it).

## 4. refcompose.py check
Implements: clip from `prev_y` (lag), clamps, frame then cursor unclipped, bands at `y - scroll`, 255-wide crop. Does not: derive rows (the caller must pass the OLD rows on the ending pass),
the ClearImage effect, or the rest-of-line `\H` width (it centres by the whole row text). v2 = v1 on 8 states (rest, scroll 2/6/16, slide-out Y 172/177/182/187): 0 pixels differ.

## 5. What remains unproven
1. **H1 (GPU order)**: proven by call order and the shared FIFO, not by an emulator or a capture. If wrong, the ending pass draws old row 0 at y 172 for one frame (14 pixels in the sample); T5 can pin either.
   Texture-cache staleness after a GPU fill is not modelled (negligible: the UI OT is drawn last, after thousands of texel fetches).
2. Colours: references come from the exported PNGs, not VRAM/CLUTs; "CLUT #8 of WIND.CL" and the frame PNG being texel-exact with the 36 x 7 cells of the cfg are not re-verified.
3. MGUI/FSS output of font3 under padding, line padding, vertical alignment, wrap (O-E19-64): only T5 tells; x2/x3 = nearest upscale unproven.
4. Other users of cfg `0x8009CFBC` (`0x80050D4C`, `0x80050F50`, `0x800534F8` write/read it, probably the inventory/choice code): not traced; pass 1's clip 172 assumes cfg.Y = 168 at the opening.
5. The kanji (2-byte) path of the box (model refuses `0x8140-0x84BE`, `0x889F-0x9872` instead of guessing); 0 corpus strings hit it.
6. Runtime substitutions (`\V`, `\X`) on a `\H` line: the binary measures the raw text; none in the corpus.
7. Catch-up frames (several logic ticks, one render): the binary draws once per pass; the DLL view would show only the last pass.
8. The corrected oracle (D-E19-62/63) departs from the binary by author decision; scenarios S1..S6 avoid the departures (`\A` on lines 1 and 2 behave the same in both).
9. Name box (f4) and choice box (f3) drawn states: not covered.

## 6. Open question for the author
- **D-E19-81 detail 3** ("one-frame descender row at y = 172 at the end of a scroll"): by H1 the binary does not show it. Confirm that faithful = not drawn (then the view shows the shifted rows at offset 0 on the ending pass, no pre-shift rows needed, T5 pins (33,172) = frame colour)?
  Recommendation: yes; the work is not blocked (the CPU-list variant is generated too: `scroll_ending_cpu_list.png`).

## 7. Files
`oracle/model_drawn.py` (extension), `validate.py`, `selfcheck.py`, `scenarios.py` (+ `scenarios.md`, `scenarios.json`), `refcompose2.py`, `pixels.py` (+ `pixels.md`), `corpus_widths.py`, `scan_raw.py`, `scan_lf.py`,
`probe394.py`, `real_texts.py`, `bdis.py`/`rd.py`/`xref*.py`/`jt.py` (binary helpers), `dumps/` (disassembly of MsgBoxRender, RenderText, ScrollText, init, CalcTextWidth, RenderScene, OT link); `values/S1..S6.csv`; `pixels/*.png`.
