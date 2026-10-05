"""f2b1 extension of the tick-exact text box model (e19f2-disc/model/model.py, imported unchanged): every box pass also
yields the binary's DRAWN state (what MsgBoxRender 0x80046EF0 puts in the local ordering table).

Everything added here is read from ALUN_CD.EXE (France); addresses in the comments.

Per pass (callback slot 0, entry {cfg 0x8009CFBC (16,168,36,7), +8 = 16, +0xA = 5, +0xC = 32, +0xE = 6}):
  1. clip DR_AREA, computed FIRST from cfg.Y as left by the PREVIOUS pass (0x80046F10-0x80046FC4):
        top = min(cfg.Y + 5 - 1, 239)          h = 50, or 240 - top when top + 50 >= 240
        x = cfg.X + 16 = 32                    w = 32 * 8 + 2 = 258      (SetDrawArea 0x80085A70: inclusive x1 = x + w - 1)
  2. sliding (dialog_flags & 3): UpdateUiBoxesPosition 0x80047DD0 writes cfg.X/Y and every frame cell; then RenderText;
     slide-out done: cfg restored from the slide origin (168), DialogClosed 0x8004501C (entry flag cleared by 0x80047CB0
     -> neither the frame cells (0x800481F8 tests entry.flags & 1) nor the text are drawn that pass);
  3. typing done: ProcessClose then RenderText;  scroll pending: ScrollText ONLY (no RenderText -> no cursor);
     else interpreter then RenderText.
  RenderText 0x800455B4: three bands (SPRT 255 x 16) at x = 32 or cfg.X + (288 - lineWidth[band]) / 2, y = cfg.Y + 5 + 16 i,
     band of row i = (bufX + i) % 3; the cursor (16 x 16 at (cfg.X + 36 * 8 - 16, cfg.Y + 7 * 8 - 24) = (288, Y + 32), u = 0xB0 +
     0x10 * (tick / 10), v = 0x38) only when cursorShown (tick + 1 wrapping at 40 BEFORE the image is chosen).
  ScrollText 0x80045988: same bands at y - offset, offset = (8 - count) * 16 / 8 AFTER the countdown decrement (0 while it
     waits); on the pass where count reaches 0 the rows are drawn with the OLD bufX, then bufX + 1, lineWidth of the new
     bottom band cleared (0x80045DEC) and ClearImage of its VRAM band (960, 288 + 16 * ((bufX' + 2) % 3), 64, 16)
     (0x80045E30): that band is the band of the OLD top row.
  GPU order: the ClearImage is queued during RenderScene (0x8002BD60) and DrawOTag runs at the end of the iteration
     (0x80042798 -> DrawSync(0) 0x80084D68 -> 0x8002BA4C DrawOTag), the libgpu queue (0x80086B0C) is FIFO, so the cleared
     band is what the OT draws: rows_visible blanks the old top row on that pass (rows_cpu keeps the CPU draw list).
"""
import os
import sys

SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'e19f2-disc', 'model'))
import model as M  # noqa: E402  (read-only reference model)
from model import BOX_X, BOX_Y, BOX_Y_OUT, SCROLL_LEN, _tdiv, glyph_char  # noqa: E402

CLIP_X = BOX_X + 16          # entry +8 = 16
CLIP_W = 32 * 8 + 2          # entry +0xC = 32
CLIP_HMAX = 6 * 8 + 2        # entry +0xE = 6
ROW_Y0 = 5                   # entry +0xA
BAND_W = 255                 # SPRT w = 0xFF (0x800453C0)
FRAME_W_PX = 36 * 8          # cfg.w * 8
CURSOR_X = BOX_X + 36 * 8 - 16
CURSOR_DY = 7 * 8 - 24


def clip_for(cfg_y):
    top = min(cfg_y + ROW_Y0 - 1, 239)
    h = CLIP_HMAX if top + CLIP_HMAX < 240 else 240 - top
    return (CLIP_X, top, CLIP_W, h)


class DrawnBox(M.TextBox):
    """model.TextBox + the drawn state. corrected=True applies D-E19-62 / D-E19-63 (what the DLL reproduces)."""

    def __init__(self, corrected=True):
        super().__init__()
        self.corrected = corrected
        self.y = BOX_Y            # cfg.Y of the static data (bytes 10 00 a8 00 24 00 07 00 at 0x8009CFBC) = 168
        self.drawn = None         # set by RenderText / ScrollText of the pass, None when nothing is drawn
        self._pass_cfg_before = None
        self._pass_clip = None

    # ---- the two corrections of the author (same as Alundra.Tests/AlundraTextBoxOracle.cs with corrected = true)
    def op4C(self, v):
        self.text_flags = v
        if self.corrected:
            self.auto_adv = 0                       # D-E19-62

    def _interpreter(self, held, pressed):
        if self.text_flags & 8:
            if pressed:
                self.cursor_shown = 0
                self.text_flags &= ~8
                if (not self.corrected) or self.line_index == 2:
                    self.scroll_mode |= 8           # D-E19-63
                self.notes.append('A-release')
                self._newline()
            return
        super()._interpreter(held, pressed)

    # ---- one pass
    def render(self, held, pressed):
        self.scroll_px = 0
        self.drawn = None
        if self.dialog_flags & 4:
            self._msgbox_render(held, pressed)

    def _msgbox_render(self, held, pressed):
        self._pass_cfg_before = self.y
        self._pass_clip = clip_for(self.y)          # 0x80046F10-0x80046FC4, BEFORE UpdateUiBoxesPosition
        super()._msgbox_render(held, pressed)

    def _render_text(self):
        super()._render_text()
        self._capture('RenderText', offset=0, cursor=True, ending=False)

    def _scroll_text(self, pressed):                # copy of model.TextBox._scroll_text + the capture
        if self.scroll_count == SCROLL_LEN:
            start = False
            if self.scroll_mode & 8:
                start = True
                self.scroll_mode &= ~8
            if self.scroll_mode & 2 and pressed:
                start = True
            if self.scroll_mode & 1:
                self.scroll_wait -= 1
                if self.scroll_wait == 0:
                    start = True
            if self.scroll_mode & 4 and self.auto_adv2 == 1:
                start = True
                self.auto_adv2 = 0
            if not start:
                self.cursor_shown = 0
                self.cursor_img = None
                self.scroll_px = 0
                self._capture('ScrollText', offset=0, cursor=False, ending=False)
                return
        self.scroll_count -= 1
        self.scroll_px = (SCROLL_LEN - self.scroll_count) * 16 // SCROLL_LEN
        self.cursor_img = None
        self._capture('ScrollText', offset=self.scroll_px, cursor=False, ending=(self.scroll_count == 0))   # draw BEFORE the shift
        if self.scroll_count == 0:
            self.scroll_pending = 0
            self.buf_x = (self.buf_x + 1) % 3
            band = (self.buf_x + self.line_index) % 3
            self.line_width[band] = 0
            self.bands[band] = []
            self.notes.append('scroll-done')

    # ---- observation
    def _row(self, i, offset):
        band = (self.buf_x + i) % 3
        w = self.line_width[band]
        x = BOX_X + 16 if w == 0 else BOX_X + _tdiv(FRAME_W_PX - w, 2)   # 0x80045640-0x800456B8
        return dict(row=i, band=band, glyphs=list(self.bands[band]), width=w, x=x, y=self.y + ROW_Y0 + 16 * i - offset)

    def _capture(self, kind, offset, cursor, ending):
        rows = [self._row(i, offset) for i in range(3)]
        vis = [dict(r) for r in rows]
        if ending:
            vis[0]['glyphs'] = []                   # ClearImage of the old top band, executed before DrawOTag
        cur = None
        if cursor and self.cursor_shown and self.cursor_img is not None:
            cur = dict(image=self.cursor_img, u=0xB0 + 0x10 * self.cursor_img, v=0x38, x=CURSOR_X, y=self.y + CURSOR_DY, tick=self.cursor_tick)
        self.drawn = dict(
            kind=kind,
            cfg_y_before=self._pass_cfg_before, y=self.y, frame=(BOX_X, self.y),
            clip=self._pass_clip, offset=offset, ending=ending,
            rows_cpu=rows, rows_visible=vis, cursor=cur)

    def drawn_rows_text(self, which='rows_visible'):
        return [''.join(glyph_char(g) for g in r['glyphs']) for r in self.drawn[which]]


def run_drawn(script, pad, frames=3000, corrected=True, stop_when_closed=True):
    """Same loop as model.run (frame f: box.render(pad(f)), then one script tick) with a DrawnBox. Returns the per-pass records."""
    box = DrawnBox(corrected)
    ctx = M.Ctx(box)
    gen = script(ctx)
    alive = True
    out = []
    for f in range(0, frames):
        box.frame = f
        ctx.frame = f
        box.sfx, box.flags_set, box.notes = [], [], []
        held, pressed = pad(f, box)
        glyphs_before = len(box.glyph_log)
        was_active = box.is_active()
        box.render(held, pressed)
        drawn = box.drawn
        if alive:
            try:
                next(gen)
            except StopIteration:
                alive = False
        out.append(dict(frame=f, phase=box.phase(), active_before=was_active, drawn=drawn, notes=list(box.notes),
                        glyphs=len(box.glyph_log) - glyphs_before, pad=(held, pressed), y=box.y,
                        text_flags=box.text_flags, cursor_shown=box.cursor_shown, scroll_px=box.scroll_px, cursor_img=box.cursor_img,
                        rows=box.visible_rows()))
        if not alive and not box.is_active():
            break
    return out, ctx, box
