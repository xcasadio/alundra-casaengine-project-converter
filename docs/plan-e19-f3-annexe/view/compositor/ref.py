"""The INDEPENDENT reference compositor of the E19.f3b discovery: pure Python (numpy + PIL), reading only the exported PNGs / .fnt / sprite JSON of the generated project and the
binary's geometry (values.json: frame x, label x, cursor x and image per pass). It shares no code with the C# side (machine, view model, XAML, MGUI, GPU)."""
import json
import os
import re
import sys

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True

PROJ = r'D:\development\repo\alundra-casaengine-project-converter\alundra-project'
BG = (100, 149, 237)
FRAME_CHOICE = '829f31a7-39d1-5fc5-b436-befd59b50985'      # the baked 128 x 32 sprite of the choice box
FRAME_TEXT = '973a9208-c867-57fe-bee3-cf30237221ef'        # g_uiBoxesInventoryDescriptionBackground, the text box frame
CURSORS = ['wind_150', 'wind_173', 'wind_201', 'wind_228']  # u 0xB0 + 0x10 i, v 0x38 (binary-notes 7)


class Assets:
    def __init__(self):
        d = json.load(open(os.path.join(PROJ, 'AssetInfos.json'), encoding='utf-8'))
        self.by_id = {}
        self.by_name = {}
        for e in d['asset_infos']:
            self.by_id[e['id']] = e
            self.by_name[e['name']] = e['id']  # the last writer wins, as the engine's catalogue
        self._png = {}
        self.fnt = parse_fnt(os.path.join(PROJ, 'UI', 'font3.fnt'))
        self.page = self.png(os.path.join('UI', 'Textures', 'font3.png'))

    def png(self, rel):
        if rel not in self._png:
            self._png[rel] = np.array(Image.open(os.path.join(PROJ, rel)).convert('RGBA'))
        return self._png[rel]

    def sprite(self, name_or_id):
        sid = name_or_id if name_or_id in self.by_id else self.by_name[name_or_id]
        sp = json.load(open(os.path.join(PROJ, self.by_id[sid]['file_name']), encoding='utf-8'))
        loc = sp['location']
        sheet = json.load(open(os.path.join(PROJ, self.by_id[sp['sprite_sheet_asset_id']]['file_name']), encoding='utf-8'))
        png = self.by_id[sheet['texture_asset_id']]['file_name']
        return self.png(png), (loc['x'], loc['y'], loc['w'], loc['h'])


def parse_fnt(path):
    glyphs = {}
    for line in open(path, encoding='utf-8'):
        if line.startswith('char '):
            d = {k: int(v) for k, v in re.findall(r'(\w+)=(-?\d+)', line)}
            glyphs[d['id']] = d
    return glyphs


class Canvas:
    def __init__(self, w=320, h=240):
        self.a = np.empty((h, w, 3), dtype=np.uint8)
        self.a[:, :] = BG

    def blit(self, img, rect, dx, dy, clip=None, band=None):
        x0, y0, w, h = rect
        src = img[y0:y0 + h, x0:x0 + w]
        H, W, _ = self.a.shape
        X = np.broadcast_to(np.arange(w)[None, :] + dx, (h, w))
        Y = np.broadcast_to(np.arange(h)[:, None] + dy, (h, w))
        ok = (X >= 0) & (X < W) & (Y >= 0) & (Y < H) & (src[..., 3] > 0)
        if clip is not None:
            cx, cy, cw, ch = clip
            ok &= (X >= cx) & (X < cx + cw) & (Y >= cy) & (Y < cy + ch)
        if band is not None:
            bx, bw = band
            ok &= (X >= bx) & (X < bx + bw)
        self.a[Y[ok], X[ok]] = src[..., :3][ok]

    def text(self, assets, s, x, y, band_w=None, clip=None):
        pen = x
        for ch in s:
            g = assets.fnt.get(ord(ch))
            if g is None:
                continue  # a character absent from the .fnt draws nothing and advances nothing
            self.blit(assets.page, (g['x'], g['y'], g['width'], g['height']), pen + g['xoffset'], y + g['yoffset'], clip=clip,
                      band=(x, band_w) if band_w is not None else None)
            pen += g['xadvance']
        return pen - x

    def scaled(self, k):
        return np.repeat(np.repeat(self.a, k, axis=0), k, axis=1)


def compose_choice(c, assets, row, labels=('OUI', 'NON')):
    """The choice box of one pass, from a row of the binary's tables (frame_x, label0_x, label1_x, cursor_x, cursor_img): frame cells, then the cursor, then the two labels
    (OT slot 5 then slot 6), y 144 / 136 / 152, no clip."""
    if not row.get('drawn'):
        return
    img, rect = assets.sprite(FRAME_CHOICE)
    c.blit(img, rect, row['frame_x'], 144)
    img, rect = assets.sprite(CURSORS[row['cursor_img']])
    c.blit(img, rect, row['cursor_x'], 136)
    c.text(assets, labels[1], row['label1_x'], 152)  # the binary draws NON then OUI (head insertion); they never overlap
    c.text(assets, labels[0], row['label0_x'], 152)


def compose_textbox(c, assets, vm):
    """The text box from its view model values (the f2b1c method): frame, cursor, then the three rows clipped to (32, clipTop, 258, clipHeight), each cut at 255 from its x."""
    if vm['root'] != 'Visible':
        return
    img, rect = assets.sprite(FRAME_TEXT)
    c.blit(img, rect, 16, vm['frameTop'])
    cur = vm['cursor']
    if cur['vis'] == 'Visible' and cur['src']:
        img, rect = assets.sprite(cur['src'])
        c.blit(img, rect, 288, cur['top'])
    clip = (32, vm['clipTop'], 258, vm['clipHeight'])
    for key in ('row0', 'row1', 'row2'):
        r = vm[key]
        if r['text']:
            c.text(assets, r['text'], 32 + r['left'], vm['clipTop'] + r['top'], band_w=255, clip=clip)


TEXTBOX_STATES = {
    'rest': dict(root='Visible', frameTop=168, clipTop=172, clipHeight=50,
                 row0=dict(text='Bonjour, Alundra !', left=0, top=1), row1=dict(text='deuxième ligne', left=0, top=17), row2=dict(text='', left=0, top=33),
                 cursor=dict(src='wind_150', top=200, vis='Visible')),
    'mrow': dict(root='Visible', frameTop=168, clipTop=172, clipHeight=50,
                 row0=dict(text=chr(26) * 18, left=0, top=1), row1=dict(text='NON OUI', left=0, top=17), row2=dict(text='', left=0, top=33),
                 cursor=dict(src='wind_150', top=200, vis='Visible')),
}


def load_rgb(path):
    return np.array(Image.open(path).convert('RGB'))


def diff(got, want):
    """(count of texels that differ in R, G or B, first one as text)."""
    if got.shape != want.shape:
        return -1, 'shape %s vs %s' % (got.shape, want.shape)
    bad = np.any(got != want, axis=2)
    n = int(bad.sum())
    if n == 0:
        return 0, ''
    ys, xs = np.nonzero(bad)
    y, x = int(ys[0]), int(xs[0])
    return n, 'first at (%d, %d) got %s want %s' % (x, y, tuple(int(v) for v in got[y, x]), tuple(int(v) for v in want[y, x]))
