"""Reference compositor of the E19.f4c discovery (pure Python, numpy + PIL): the text box screen WITH the speaker name box and portrait, one pass at a time.

Independent of the C# side (machine, view model, XAML, MGUI, GPU): it reads only
  - the binary's value model (docs/plan-e19-f4-annexe/model/f4_model*.py, validated against the real binary code by the f4 discovery) lock-stepped with the f2b1 text box
    drawn-state model (docs/plan-e19-f2b1-annexe/scripts/model_drawn.py),
  - the exported PNGs / sprites of alundra-project (the same caveat as f2b1 and f3b: the textures are the exports, not VRAM),
  - the binary's glyph table (via refcompose2) for the text of the rows AND of the name.
The compositing order is the binary's ordering table: slots 0 and 2 (text box frame, cursor, rows), slot 3 (portrait), slot 5 (name frame), slot 6 (name text, cut by its DR_AREA).
"""
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'f4model'))
sys.path.insert(0, os.path.join(HERE, 'f2b1scripts'))
sys.path.insert(0, HERE)

import numpy as np
from PIL import Image

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
    d = json.load(open(os.path.join(HERE, 'names_widths.json'), encoding='utf-8'))
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


def sample(px, w, h, rule):
    H, W = px.shape[:2]
    if rule == 'psx':      # floor(i * src / dst): texel under the pixel's top-left corner [hyp, PSX rasteriser]
        ys = (np.arange(h) * H) // h
        xs = (np.arange(w) * W) // w
    else:                  # 'center': nearest sampling at pixel centres, what a GPU point sampler does
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


def compose(rec, portrait_px=None, rule='center', tint=False, layers=('box', 'portrait', 'nameframe', 'nametext')):
    """One frame record of run() -> a 240 x 320 x 3 uint8 image."""
    canvas = np.empty((240, 320, 3), dtype=np.uint8)
    canvas[:, :] = BG
    if 'box' in layers and rec['drawn'] is not None:
        img, _ = R.compose(rec['drawn'])
        canvas[:, :] = np.array(img.convert('RGB'))
    p = rec['portrait']
    if 'portrait' in layers and p is not None and p['w'] > 0 and p['h'] > 0 and portrait_px is not None:
        s = sample(portrait_px, p['w'], p['h'], rule)
        if tint:
            s = psx_modulate(s, p['rgb'])
        blit_rgba(canvas, s, p['x'], p['y'])
    n = rec['name']
    if n is not None:
        if 'nameframe' in layers:
            blit_rgba(canvas, NAMEFRAME, n['frame'][0], n['frame'][1])
        if 'nametext' in layers:
            glyphs = list(NAMES[rec['name_id']])
            band, _ = R.band_image(glyphs)
            arr = np.array(band.crop((0, 0, 255, 16)))
            blit_rgba(canvas, arr, n['text'][0], n['text'][1], clip=(n['clip'][0], n['clip'][1], n['clip'][2], n['clip'][3]))
    return canvas


def save(canvas, path, k=1):
    arr = np.repeat(np.repeat(canvas, k, axis=0), k, axis=1) if k > 1 else canvas
    Image.fromarray(arr).save(path)


if __name__ == '__main__':
    bank = {p['bank']: p for p in portraits()}
    jess = bank[4]
    px = sprite_pixels(jess['id'])
    recs = run([(0x0D, entity(0x104, True), b'AB', True, None)], frames=60)
    os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
    for f in (4, 5, 11, 16, 19, 24, 25, 28, 39, 40, 42):
        r = recs[f]
        save(compose(r, px), os.path.join(HERE, 'out', 'S1_N%+d.png' % r['rel']))
        print(f, 'N%+d' % r['rel'], r['phase'], 'box y', None if r['drawn'] is None else r['drawn']['y'], 'name', None if r['name'] is None else r['name']['frame'][0],
              'portrait', None if r['portrait'] is None else (r['portrait']['x'], r['portrait']['y'], r['portrait']['w'], r['portrait']['h'], r['portrait']['rgb']))
