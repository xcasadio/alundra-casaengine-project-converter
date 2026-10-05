"""Reference compositor v2 for the f2b1 pixel test (independent of the DLL and of MGUI, read-only on the repository).

It composes ONE drawn state of model_drawn.DrawnBox (the per-pass record of the binary's drawing) into a 320 x 240 image, in the order
of the binary's local ordering table: slot 0 = frame cells then cursor (never clipped), slot 2 = DR_AREA clip then the three bands
(SPRT 255 x 16), slot 3 = the draw area restored (nothing else is drawn here). Glyph rectangles come from the E19.f2b0 annex
(docs/plan-e19-f2b0-annexe/glyph_table.txt), cross-checked against the binary (ALUN_CD.EXE 0x800993C4) at import.
Textures are the exports under alundra-project/UI/Textures (frame, font3, wind) - caveat of the verification: the frame and cursor
references come from the exported PNGs, not from VRAM.
"""
import os
import struct
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SP = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(SP, 'e19j-disc'))
import lib  # noqa: E402

REPO = r"D:\development\repo\alundra-casaengine-project-converter"
ROOT = os.path.join(REPO, 'alundra-project')
ANNEX = os.path.join(REPO, 'docs', 'plan-e19-f2b0-annexe', 'glyph_table.txt')
TABLE = 0x800993C4
BACKGROUND = (100, 149, 237, 255)
SCREEN = (320, 240)

FRAME = Image.open(os.path.join(ROOT, 'UI', 'Textures', 'g_uiBoxesInventoryDescriptionBackground.png')).convert('RGBA')
FONT = Image.open(os.path.join(ROOT, 'UI', 'Textures', 'font3.png')).convert('RGBA')
WIND = Image.open(os.path.join(ROOT, 'UI', 'Textures', 'wind.png')).convert('RGBA')


def _annex():
    t = {}
    for ln in open(ANNEX, encoding='utf-8').read().splitlines()[1:]:
        p = ln.split()
        if len(p) == 6:
            t[int(p[0], 16)] = tuple(int(x) for x in p[1:])
    return t


GLYPH = _annex()
for _c in range(256):
    _b = struct.unpack_from('<5i', lib.DATA, TABLE + 20 * _c - lib.BASE + lib.OFF)
    assert GLYPH[_c] == _b, ('annex differs from the binary at', _c, GLYPH[_c], _b)


def band_image(glyph_ids):
    """The 256 x 16 band as RenderTextBitmap (0x800478C4) leaves it: w x h texels copied from (srcX, srcY) to (pen, yoff) while pen + k < 256."""
    band = Image.new('RGBA', (256, 16), (0, 0, 0, 0))
    fp, bp = FONT.load(), band.load()
    pen = 0
    for g in glyph_ids:
        w, h, sx, sy, yo = GLYPH[g]
        for r in range(h):
            for k in range(w):
                if pen + k >= 256:
                    break
                y = yo + r
                if 0 <= y < 16:
                    bp[pen + k, y] = fp[sx + k, sy + r]
        pen += w
    return band, pen


def compose(state, which='rows_visible', with_cursor=True):
    """state = DrawnBox.drawn of a pass (a dict). Returns (image 320x240, info)."""
    big = Image.new('RGBA', (320, 400), BACKGROUND)
    y = state['y']
    big.alpha_composite(FRAME, (state['frame'][0], y))                      # slot 0: the 36 x 7 cells of the frame (288 x 56)
    cur = state['cursor']
    if with_cursor and cur is not None:                                      # slot 0, after the frame: the wait cursor
        big.alpha_composite(WIND.crop((cur['u'], cur['v'], cur['u'] + 16, cur['v'] + 16)), (cur['x'], cur['y']))
    layer = Image.new('RGBA', (320, 400), (0, 0, 0, 0))                      # slot 2: the three bands
    for r in state[which]:
        if not r['glyphs']:
            continue
        band, _ = band_image(r['glyphs'])
        layer.alpha_composite(band.crop((0, 0, 255, 16)), (r['x'], r['y'])) if -16 < r['y'] < 400 else None
    cx, ct, cw, ch = state['clip']                                           # DR_AREA: inclusive x1 = x + w - 1, y1 = y + h - 1
    mask = Image.new('L', (320, 400), 0)
    mask.paste(255, (cx, ct, cx + cw, ct + ch))
    alpha = layer.getchannel('A')
    clipped_alpha = Image.new('L', (320, 400), 0)
    clipped_alpha.paste(alpha, (0, 0), mask)
    layer.putalpha(clipped_alpha)
    big.alpha_composite(layer)
    return big.crop((0, 0, 320, 240)), dict(clip=(cx, ct, cx + cw - 1, ct + ch - 1))
