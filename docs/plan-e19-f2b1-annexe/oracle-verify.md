# Counter-check of the "oracle" discovery (E19.f2b1) -- adversarial, read-only

Repository `chantier/e19-suite` (HEAD moved 897504b -> 262f92a during the run, another agent committing tests; nothing built/run/exported/edited/staged
in the repo; only `git rev-parse` / `git status`). Binary = `ALUN_CD.EXE` (France), re-disassembled with my OWN lib (`vlib.py`, capstone), never with the
report's `bdis.py`/`lib`. Scripts (all in this folder): `vlib.py` (reader/disassembler), `xref.py`/`findaddr.py`/`grepins.py` (scans), `ctw.py`
(my CalcTextWidth, from my own reading of `0x8004771C` + jump table `0x80024098`), `corpus.py`/`kanji_scan.py`/`brace_scan.py`/`dbg394.py`/`real3.py`
(corpus), `comp.py` + `check_pixels.py` + `cmp_png.py` (my own compositor, states written BY HAND from the binary rules, glyph rectangles read from the
binary), `clut_check*.py` (palettes). I ran the report's `selfcheck.py` and `validate.py` only to test the claim "they pass" (both pass: 11 scenarios,
0 mismatches); I did not use their outputs as evidence. Tags: [bin] = re-read in the binary now; [calc] = my scripts; [hyp] = not proven.

NB for whoever reuses these: the Bash heredoc corrupts backslashes (known pitfall) -- my scripts were written with the Write tool.

## 1. Verdict per claim

| # | Claim of the report | Verdict | My evidence |
|---|---|---|---|
| C1 | Clip = (32, min(prevY+4, 239), 258, h), h = 50 or 240-top when top+50 >= 240, inclusive end, computed BEFORE the slide moves Y (so from the previous pass) | CONFIRMED | `0x80046F10-0x80046FC4`: `lh 0xa(entry)`=5 + `lh 2(cfg)`=Y, -1, clamp 239 (`0x80046F30-3C`); h `0x80046F48-64`; x = `lhu 8(entry)`(16)+`lhu (cfg)`+env clip.x; w = `lh 0xc(entry)`(32)<<3 + 2; UpdateUiBoxesPosition is called later (`0x80046FF4`); `0x80085A70` stores x+w-1, y+h-1 (`0x80085AB4`, `0x80085AC8`). Entry 0 = {cfg 0x8009CFBC, +8 16, +0xA 5, +0xC 32, +0xE 6, init 0x80048304, upd 0x80046EF0}; cfg bytes 10 00 a8 00 24 00 07 00. |
| C2 | Slide-in Y sequence 240,236,231,...,173,168,168,168 over 18 passes; slide-out 168,168,172,177,...,235,240,240 then DialogClosed; clip tops/heights of S1 and S5 | CONFIRMED | Slide function `0x80047DD0` read myself: pos = start + (target-start)*step/total (C truncation), step 0..15, then 2 settle passes writing the target, then returns 1. Open `0x800451E8-0x800452D8` (240 -> cfg.Y, total 15, settle 2), close `0x80045F10-0x80045FCC` (cfg.Y -> 240). I recomputed all Y and clip tops/heights of S1/S5 by hand: all equal. |
| C3 | First box pass clips from 172 (cfg.Y = 168), not 239 | CONFIRMED | cfg is static 168; opening only COPIES cfg into the slide block (`0x800452B8-0x800452D8`), the first write of cfg.Y=240 is pass 1's UpdateUiBoxesPosition, after the clip. Release restores it (`0x80047038-0x80047050`). DLL: `AlundraDialogueBox.cs:246` sets `Y = ClosedY` at open, :350 `Y = OpenY` at release (checked). Caveat in 3.2. |
| C4 | Branches: slide -> UpdateUiBoxesPosition then RenderText; scroll pending -> ScrollText ONLY (no cursor); slide-out release pass draws nothing, not even the frame | CONFIRMED | `0x80046FCC-0x800470AC`; DialogClosed `0x8004501C` -> `0x80047CB0` `sh zero,(entry)`; dispatcher calls a callback only if `flags & 1` (`0x80048144`); frame cells `0x800481F8` skip inactive entries (`0x8004822C-30`) and are built after the callbacks (`0x8002BE5C` then `0x8002BE64`). (Visually equivalent to a frame at y=240: off-screen.) |
| C5 | Row x = 32 or 16+(288-w)/2 (signed halving), w = lineWidth[(bufX+i)%3] | CONFIRMED | RenderText `0x80045640-0x800456E8`, ScrollText `0x80045B0C-0x80045C48`; same formula in both. y = cfg.Y + 5 + 16 i (- offset in ScrollText). Inner "buffer loop" runs once per row (db handled through the index `0x80146F50`). |
| C6 | `\H` stores CalcTextWidth of the REST of the line (after `\H`) in lineWidth[(bufX+lineIndex)%3]; cleared only at opening and for the new bottom band at scroll end, after the rows are drawn | CONFIRMED | `0x800469F0-0x80046A5C` (cursor+1, call `0x8004771C` on `0x80149268+cursor`, band by `(bufX+lineIndex)%3`). Only 5 sites reference lineWidth (`0x8004530C` open, `0x800455E4`, `0x80045AB8`, `0x80045DD8` scroll-end clear after the draw loop, `0x80046A4C` the store): no other clearing. A page turn (`\A`) is a newline (scroll at the bottom), not a clear: no stale width possible. |
| C7 | CalcTextWidth rules (V re-read as glyph, B-G/T/Y skip 1, X skip 2, W glyph, digits skipped, A/N stop) | CONFIRMED | My own port `ctw.py` from `0x8004771C-0x800478B8` + jump table dump: groups `0-9` digits, `:;<=>?@HIJKLMOPQRSUV` re-read, `AN` stop, `BCDEFGTY` skip1, `W`, `X` skip2. Values: "Roue de la fortune !" 111 (x 104); S025 125 (x 97), trimmed 93 (x 113); map 394 s5 rest = 47 (x 136). All equal the report. |
| C8 | Cursor: tick+1 wrap 40 before image = tick/10; 9 passes of image 0 the first time then 10; u 0xB0..0xE0, v 0x38; (288, Y+32); tick reset only at opening; none on ScrollText passes | CONFIRMED | `0x80045814-0x8004592C` (`slti 0x28`, `*0x66666667` = /10), table `0x800A58CC+40 k` bytes +0xC/+0xD read: b0/c0/d0/e0, 38; x = cfg.X + 36*8 - 16, y = cfg.Y + 7*8 - 24; writers of tick: `0x800454F8` (open) and RenderText only; writers of cursorShown: `0x800454F0`, `0x80045A88`, `0x80046044`, `0x80046A90`. |
| C9 | Scroll offsets 2..16 computed AFTER the decrement; 0 while waiting; the ending pass draws the OLD rows then shifts; width of the new bottom band cleared; ClearImage of the band of the OLD top row | CONFIRMED | `0x80045988-0x80045E5C`: decrement `0x80045A90/98`, offset `(len-count)*16/len` `0x80045B1C-5C`, draw loop, DR_AREA link `0x80045D00-64`, `bnez count -> return` `0x80045D60`, shift `0x80045D68-DB0` (bufX' = (bufX+1)%3), width clear `0x80045DD8-EC`, `ClearImage(0x3C0, 0x120+16*((bufX'+2)%3), 0x40, 0x10)` at `0x80045E30` = band (bufX'+2)%3 = bufX_old = band of old row 0. |
| C10 | **H1**: ClearImage, LoadImage, DrawOTag share one FIFO; DrawOTag of the UI OT runs at the end of the same iteration, after ScrollText -> old row 0 is blank when the OT is drawn, so the descender row at y=172 is not drawn | CONFIRMED as a static derivation (call order + FIFO); NOT confirmed empirically (no emulator/capture) | See section 2. |
| C11 | Pixel references (rest pose x10, scroll offset 6 x7, ending x3, slide-out Y182 x7, centred x4, 255-cut x2) and "ending pass visible == next pass; CPU list differs on 14 pixels all on y=172"; "lag changes 55 pixels" | CONFIRMED (34/34 points, plus 6 full images) | My own compositor (`comp.py`): glyph rectangles read from the binary table, textures = the same exported PNGs. 34/34 sampled RGBA equal; `cmp_png.py`: rest, scroll6, scroll_ending_visible, scroll_ending_cpu_list, slideout182 and centred (cursor image 0) have 0 differing pixels against the report's PNGs; ending-pass CPU list differs from the next pass on 14 pixels, all on y=172; lagged clip vs current-Y clip differs on 55 pixels. |
| C12 | Annex `docs/plan-e19-f2b0-annexe/glyph_table.txt` equals the binary table | CONFIRMED | 256 rows vs `0x800993C4` (5 int32 x 256), 0 differences (my own parse). |
| C13 | `refcompose.py` implements the clip lag; pre-shift only because the caller passes the old rows; lacks ClearImage; centres by the whole row text, not the rest-of-line width | CONFIRMED | Read `f2b-disc/refcompose.py` (`prev_y` lag, `rows` argument, `width_of(text)` of the whole row, no band clearing). |
| C14 | Corpus: 24,431 non-empty map strings; widest line exactly 255 px, none wider (so the 255 cut never triggers on static text); 0 strings on the kanji path | CONFIRMED (independent method) | I did not retype the strings; I measured each line with my CalcTextWidth (the `\H` markers removed, as the typing pen ignores them): 24,431 strings, widest 255 (map_10 s7), 0 lines > 255. Kanji: conservative adjacent-byte-pair scan for 0x8140-0x84BE / 0x889F-0x9872 over all bytes: 0 hits (high bytes used: 9c ab b0 bb c7 c9 e0 e2 e7 e8 e9 ea ee ef f4 f9 fb). No `{`/`}` anywhere. |
| C15 | 823 `\H` in 511 strings; one not at the start of its line (394 s5); none with `\V`/`\X` after it; no raw LF | CONFIRMED | `corpus.py`: 823 / 511 / 1 (map_394 string 5, width before = 79) / 0 / 0. `\H` widths 27..254. |
| C16 | The three real f2a texts (389 S001/S002, 391 S019/S020/S022, 164 S003) carry no `\H` | CONFIRMED | `real3.py`. S025 of 472/473/474 = `\B\HRoue de la fortune !\N\HFlorin\W5Roulette` + 8 spaces (checked). |
| C17 | `validate.py`/`selfcheck.py` pass; extended model = model.run frame by frame on 11 scenarios | CONFIRMED (ran them; read the DrawnBox overrides: `_scroll_text` is a faithful copy of the model's plus a capture; `_interpreter` override matches D-E19-62/63 corrected) | Output: 11 scenarios 0 mismatches, "selfcheck OK". NOT re-derived by me: the f2a timeline itself (typing delays, `\A` pass numbers of S2/S3/S6); I only re-derived the draw-state rules and the slide/scroll/cursor arithmetic. |
| C18 | "Unproven" list (H1 empirical; colours from exported PNGs; MGUI/GPU specifics; other users of cfg 0x8009CFBC; kanji; `\V`/`\X` on `\H`; catch-up; name/choice boxes) | CONFIRMED as a fair list, with 3 of its items partly settled by me | See sections 3.1-3.3. |

## 2. H1 (the finding that changes the plan), re-derived

Facts [bin] (all re-read by me):
- `ClearImage 0x80084EFC` -> `jalr ops[2]` at `0x80084F68` with `a0 = ops[3]` (the fill), `a1` = rect (8 bytes copied), `a3` = colour.
  `LoadImage 0x80084F90` -> `jalr ops[2]` at `0x80084FD4` (`a0 = ops[8]`). `DrawOTag 0x800852CC` -> `jalr ops[2]` at `0x80085324` (`a0 = ops[6]`).
  `ops` = `*(0x800C812C)` = `0x800C80EC`, `ops[2]` = `0x80086B0C` (checked): the PsyQ command queue: ring of 64 (`0x800C8238` head / `0x800C823C` tail),
  executes at once only if the ring is empty and the DMA2 busy bit (`0x01000000`) is clear and `0x800C8140` is 0 (after waiting GPUSTAT ready
  `0x04000000`), otherwise enqueues (copying the 8-byte arg). `0x800C8135` (queue mode) is set to 1 by ResetGraph (`0x80084960`). Either path keeps call order.
- Iteration (`0x8002C3F4-0x8002C444`): `0x8002AC2C`, **RenderScene `0x8002BD60`** (callback dispatcher `0x80048054` at `0x8002BE5C` -> MsgBoxRender/ScrollText;
  then `0x80044C5C` which builds the frame cells and returns the UI OT head stored at `0x800DC098`, `0x8002BE70`), `0x8002BAEC(0)`, then
  **`0x80042798`**: `DrawSync(0)` (`0x800427C4`), the callback `*(0x8013FB7C)` = `0x8002B9D4` (ClearOTag of the other buffer, `0x800427E0`), VSync,
  `0x80042554`, and at the end `*(0x8013FB78)` = `0x8002BA4C` (`jalr` at `0x8004286C`). Both pointers are set by `0x80042658` called at `0x8002C1C8`
  (my correction: the report says "called from 0x80042798"; it is an indirect call from it, same effect).
- `0x8002BA4C` issues 6 `DrawOTag`: 5 scene OTs (`s0 + 0,0x10,8,4,0xC`), then the UI OT (`*(0x800DC098)`), then swaps the two OT buffers
  (`sw 0x1E24/0x1E20`). So the UI OT built by this iteration's RenderScene is drawn at the end of THIS iteration, after the ScrollText ClearImage.
- ScrollText on the ending pass: rows linked with the OLD bufX, then bufX', then the ClearImage of the band of the old row 0.
- ClearOTag `0x8008511C` links forward (slot 0 first); frame/cursor in slot 0, DR_AREA + bands in slot 2: no overlap issue.
=> in the GPU stream: DrawOTag(k-1) .. ClearImage(band of old row 0) .. DrawOTag(k). The old row-0 sprite (y 157..172, clip top 172) samples a
blank band: nothing at y=172. My compositor: the ending-pass image with the blank old row is identical to the next pass (0 pixels); the CPU-list
variant differs on 14 pixels, all on y=172.

Residual doubts [hyp]: (a) no capture/emulator (none found on this machine, search not exhaustive); (b) the GPU texture cache is not modelled
(2 KB; the UI OT is drawn after the world OTs and the frame cells, which fetch other texture pages: staleness very unlikely); (c) the alternate path of
`0x8002BA4C` when `*(0x800DC4E4) != 0` draws a single OT and NO UI OT (`0x8002BA54-0x8002BABC`); that byte is read at one place and never written
by any `lui 0x800E/-0x3B1C` access, so it is 0 in normal play, but I did not prove it for the whole game.

Other consequence (agrees with the report): no pre-shift rows are needed in the view; on the ending pass show the shifted rows at offset 0 (the DLL's
`ScrollPixels = 16` + already-shifted `Lines`, `AlundraDialogueBox.cs:494-501`, would jump one line for a frame otherwise).

## 3. Corrections and additions (things the report missed or stated loosely)

3.1 **Palette check (settles unproven item 2 partly).** The bands and the cursor use `*(0x80146E28 + 0x10)` = entry 8 of a table filled by
`0x80044B7C-0x80044B8C` with `GetClut(0x120, 0x1E0 + i)`, i = 0..15, the 16 x 16 palette block being the 512 bytes of `taki\screen\wind.cl`
(path string at `0x80023EFC`, loaded by `0x80044B48`, uploaded `0x80044BC4`): so palette #8 of WIND.CL (confirmed). Against the exported PNGs:
- the exported `font3.png` was built with FONT3.TIM's OWN CLUT, not palette #8: equal on 13 of 14 opaque indices, **index 4 differs** (export (82,90,57),
  palette #8 gives (74,82,57)); 44 texels of the sheet, only in glyph ids 4, 14, 15, 21..29 and `@` (64); no letter/digit. In S025, `\W5` = glyph 21 has 8
  such texels. So the "independent" T5 reference (built from the same PNG) cannot detect it. Low impact; export-side fidelity note, not for f2b1.
- frame colours (88,96,72) (152,152,112) (184,176,144) (72,64,56) (168,168,136) = WIND.CL palettes 0/1 indices 9, 11, 14, 8, 13; cursor (192,192,192) =
  palette 8 index 12 (both with the `v << 3` 5-to-8-bit expansion; the font export uses bit replication: just two exporter conventions).

3.2 **cfg 0x8009CFBC is shared** by 7 of the 13 descriptor entries (0, 2, 5, 7, 8, 9, 11, all with init `0x80048304`; entries 2/5/8/9/11 have 4 rows, entry 0 has 6).
Every writer of cfg X/Y found (`0x80047038`, `0x80050F48`, `0x800534xx`, `0x80056748`, `0x800590E0`) restores it from the slide origin `block+0x18/+0x1A`; the
open code only copies it. So "cfg.Y = 168 at opening" holds unless a slide is interrupted (not examined). No plan change; worth one line in the sketch.

3.3 **View/canvas [hyp]**: the frame is at y = 240 on pass 1 (and 240 on the last slide-out passes, up to 56 px below the 320 x 240 canvas at Y up to 240). If the
XAML root canvas is not clipped to its 320 x 240 and the window is not 4:3 (vertical letterbox), the frame would show below the canvas. T3/T5 should include a
state with Y >= 232 and a non-4:3 window (or `ClipToBounds` on the root canvas). Not tested by me (MGUI side).

3.4 Minor: `0x8002BA4C` is not called directly (see section 2); "CalcTextWidth `{`/`}` codes" never occur in the corpus (so the 256-row annex is enough
for `\H`); the report's draft D-E19-81 summary in the plan (rows 13..15 at Y 172; 12..15 at 177, 182, 187) matches my recomputation.

## 4. Open question for the author (only if you want one)

None is a genuine product decision: D-E19-81 already says "faithful to the binary", and by H1 the binary does not draw the y=172 row. What is needed is
a plan edit (detail 3 of D-E19-81 recorded as "not drawn, order ClearImage < DrawOTag", T5 pins (33,172) = frame colour (184,176,144) on the ending pass),
possibly with a one-line author acknowledgement because it amends a recorded decision. If an emulator capture is ever available, flip the one expected
pixel if it contradicts H1; the CPU-list variant exists for that.
