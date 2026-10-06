"""Reference compositor of E19.f4c (pure Python, numpy + PIL): the text box screen WITH the speaker name box and portrait, one pass at a time.

Independent of the C# side (machine, view model, XAML, MGUI, GPU): it reads only
  - the binary's value model (docs/plan-e19-f4-annexe/model/f4_model*.py, validated against the real binary code by the f4 discovery) lock-stepped with the f2b1 text box
    drawn-state model (docs/plan-e19-f2b1-annexe/scripts/model_drawn.py),
  - the exported PNGs / sprites of alundra-project (the same caveat as f2b1 and f3b: the textures are the exports, not VRAM),
  - the binary's glyph table (via refcompose2, which asserts it against the executable when the discovery's `lib` is importable) for the text of the rows AND of the name.
The compositing order is the binary's ordering table: slots 0 and 2 (text box frame, cursor, rows), slot 3 (portrait), slot 5 (name frame), slot 6 (name text, cut by its DR_AREA).

Every path is relative to this file. The two f2b1/f4 sources are imported from their own annex folders; the f2b1 `refcompose2` needs a module named `lib` that exposes the
executable (a discovery tool kept outside the repository): when it is not importable, a stand-in built from the versioned glyph table (docs/plan-e19-f2b0-annexe/glyph_table.txt)
is installed, so that its import-time check against the executable (done once by the f2b0 slice) is the only thing skipped.

E19.f4c2 (D-E19-100, D-E19-101): the portrait is sampled at the CENTRE of the screen pixels (what the GPU does, floor((i + 0.5) * src / dst)) at the screen resolution
(`k` x the native size), then modulated like the PS1 does (`psx_modulate`: 5-bit texel x rgb / 128, saturated). The PS1 own texel rule (floor(i * src / dst)) is NOT a reference.
"""
import os
import struct
import sys
import types

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(DOCS, 'plan-e19-f4-annexe', 'model'))
sys.path.insert(0, os.path.join(DOCS, 'plan-e19-f2b1-annexe', 'scripts'))
sys.path.insert(0, HERE)

import numpy as np
from PIL import Image


def _install_lib_stand_in():
    try:
        import lib  # the discovery's executable reader, when the machine has it (a namespace package of another tool is not it)
        if hasattr(lib, 'DATA'):
            return 'binary'
        del sys.modules['lib']
    except ImportError:
        pass
    table = {}
    for ln in open(os.path.join(DOCS, 'plan-e19-f2b0-annexe', 'glyph_table.txt'), encoding='utf-8').read().splitlines()[1:]:
        p = ln.split()
        if len(p) == 6:
            table[int(p[0], 16)] = tuple(int(x) for x in p[1:])
    base, off, at = 0x80020000, 0x800, 0x800993C4
    data = bytearray(off + (at - base) + 20 * 256)
    for c in range(256):
        struct.pack_into('<5i', data, at - base + off + 20 * c, *table[c])
    lib = types.ModuleType('lib')
    lib.DATA, lib.BASE, lib.OFF = bytes(data), base, off
    sys.modules['lib'] = lib
    return 'glyph-table stand-in'


LIB_SOURCE = _install_lib_stand_in()

import model as M
import f4_model as F
import f4_model_h as FH
from model_drawn import DrawnBox
import refcompose2 as R
from assets import sprite_pixels, portraits, PROJ

NAMEFRAME = np.array(Image.open(os.path.join(PROJ, 'UI', 'Textures', 'g_textTilesConfiguration.png')).convert('RGBA'))
BG = R.BACKGROUND[:3]


def load_names():
    import json
    d = json.load(open(os.path.join(DOCS, 'plan-e19-f4-annexe', 'names_widths.json'), encoding='utf-8'))
    return {int(k, 16): v['name'].encode('cp1252') for k, v in d.items()}


NAMES = load_names()


class F4D(DrawnBox):
    """The f2b1 drawn text box + the f4 name box and the (height-aware) portrait."""

    def __init__(self, etc=NAMES):
        super().__init__()
        self.nb = F.NameBox(etc)
        self.pt = FH.PortraitH()
        self.cam = (0, 0)
        self.speaker = None
        self.attempts = []

    def op_dialog(self, kind, entity, matched, text, mode, name_id=None):
        if kind == 0x0D or matched:
            if entity['flags'] & F.FLAG_HAS_PORTRAIT:
                if self.pt.open(entity, self.cam, entity.get('h', 56)):
                    self.speaker = entity
            nid = name_id if kind == 0xC4 else entity['id']
            self.nb.open(nid)
        ok = self.try_open(text, mode)
        self.attempts.append((self.frame, kind, ok))
        return ok

    def _process_close(self, pressed):
        before = len(self.notes)
        super()._process_close(pressed)
        if 'close-trigger' in self.notes[before:]:
            self.nb.close()
            if self.pt.state != 0 and self.speaker is not None:
                self.pt.close(self.speaker, self.cam)


def entity(id, portrait, x=200, y=150, z=0, h=56):
    return dict(id=id, flags=F.FLAG_HAS_PORTRAIT if portrait else 0, x=x, y=y, z=z, h=h)


def run(dialogues, frames=60, cam=(40, 20), n0=3, pad=M.pad_every_frame):
    """Same loop as the annex scenarios.run(): per frame the pass of the box, the name, the portrait, THEN the script phase. Returns one record per frame."""
    box = F4D()
    box.cam = cam
    out = []
    i = 0
    for f in range(frames):
        box.frame = f
        box.sfx, box.flags_set, box.notes = [], [], []
        held, pressed = pad(f, box)
        box.render(held, pressed)
        nd = box.nb.pass_()
        pd = box.pt.pass_()
        ev = []
        if i < len(dialogues) and f >= n0:
            kind, ent, text, matched, nid = dialogues[i]
            ok = box.op_dialog(kind, ent, matched, text, 1, nid)
            ev.append('opcode %s -> %s' % (hex(kind), 'opened' if ok else 'retry (0)'))
            if ok:
                i += 1
        ev += list(box.notes)
        out.append(dict(frame=f, rel=f - n0, drawn=box.drawn, name=nd, portrait=pd, name_id=box.nb.id, phase=box.phase(), events=ev,
                        speaker=box.speaker))
    return out


# ---------------------------------------------------------------------------------------------------------------- compositing
def psx_modulate(px, rgb):
    """5-bit model of the PSX texture modulation [hyp, public GPU documentation, no hardware here]: out5 = min(31, t5 * rgb // 128)."""
    t5 = px[..., :3].astype(np.int32) >> 3
    o5 = np.minimum(31, (t5 * rgb) // 128)
    o8 = (o5 << 3) | (o5 >> 2)
    res = px.copy()
    res[..., :3] = o8.astype(np.uint8)
    return res


def sample(px, w, h):
    """The centre rule: nearest sampling at pixel centres, what a GPU point sampler does (floor((i + 0.5) * src / dst), D-E19-101)."""
    H, W = px.shape[:2]
    ys = np.floor((np.arange(h) + 0.5) * H / h).astype(int)
    xs = np.floor((np.arange(w) + 0.5) * W / w).astype(int)
    return px[np.ix_(ys, xs)]


def blit_rgba(canvas, img, dx, dy, clip=None):
    """Opaque-or-transparent blit (alpha > 0 draws the texel's RGB, like the binary's opaque prims and the f2b1/f3b compositors)."""
    h, w = img.shape[:2]
    H, W = canvas.shape[:2]
    X = np.broadcast_to(np.arange(w)[None, :] + dx, (h, w))
    Y = np.broadcast_to(np.arange(h)[:, None] + dy, (h, w))
    ok = (X >= 0) & (X < W) & (Y >= 0) & (Y < H) & (img[..., 3] > 0)
    if clip is not None:
        cx, cy, cw, ch = clip
        ok &= (X >= cx) & (X < cx + cw) & (Y >= cy) & (Y < cy + ch)
    canvas[Y[ok], X[ok]] = img[..., :3][ok]


def up(arr, k):
    return np.repeat(np.repeat(arr, k, axis=0), k, axis=1) if k > 1 else arr


def compose(rec, portrait_px=None, k=1, ox=0, oy=0, layers=('box', 'portrait', 'nameframe', 'nametext')):
    """One frame record of run() -> a (240 k + 2 oy) x (320 k + 2 ox) x 3 uint8 image: the screen at the integer scale k (every layer point-sampled, the portrait sampled
    at the centre of the screen pixels then modulated), in a view offset by (ox, oy) inside a target of the background colour (the view offset of a window that is not 4:3)."""
    base = np.empty((240, 320, 3), dtype=np.uint8)
    base[:, :] = BG
    if 'box' in layers and rec['drawn'] is not None:
        img, _ = R.compose(rec['drawn'])
        base[:, :] = np.array(img.convert('RGB'))
    canvas = up(base, k).copy()
    p = rec['portrait']
    if 'portrait' in layers and p is not None and p['w'] > 0 and p['h'] > 0 and portrait_px is not None:
        s = sample(portrait_px, p['w'] * k, p['h'] * k)
        if p['rgb'] != 128:     # 128 is the neutral colour of the PS1: the texel as it is (the plan: the portrait at rest is exact)
            s = psx_modulate(s, p['rgb'])
        blit_rgba(canvas, s, p['x'] * k, p['y'] * k)
    n = rec['name']
    if n is not None:
        if 'nameframe' in layers:
            blit_rgba(canvas, up(NAMEFRAME, k), n['frame'][0] * k, n['frame'][1] * k)
        if 'nametext' in layers:
            glyphs = list(NAMES[rec['name_id']])
            band, _ = R.band_image(glyphs)
            arr = np.array(band.crop((0, 0, 255, 16)))
            c = n['clip']
            blit_rgba(canvas, up(arr, k), n['text'][0] * k, n['text'][1] * k, clip=(c[0] * k, c[1] * k, c[2] * k, c[3] * k))
    if ox or oy:
        target = np.empty((240 * k + 2 * oy, 320 * k + 2 * ox, 3), dtype=np.uint8)
        target[:, :] = BG
        target[oy:oy + 240 * k, ox:ox + 320 * k] = canvas
        canvas = target
    return canvas


def save(canvas, path):
    Image.fromarray(canvas).save(path)


if __name__ == '__main__':
    bank = {p['bank']: p for p in portraits()}
    jess = bank[4]
    px = sprite_pixels(jess['id'])
    recs = run([(0x0D, entity(0x104, True), b'AB', True, None)], frames=60)
    outdir = os.path.join(HERE, 'refs')
    os.makedirs(outdir, exist_ok=True)
    for f in (4, 5, 11, 16, 19, 24, 25, 28, 39, 40, 42):
        r = recs[f]
        print(f, 'N%+d' % r['rel'], r['phase'], 'box y', None if r['drawn'] is None else r['drawn']['y'], 'name', None if r['name'] is None else r['name']['frame'][0],
              'portrait', None if r['portrait'] is None else (r['portrait']['x'], r['portrait']['y'], r['portrait']['w'], r['portrait']['h'], r['portrait']['rgb']))
