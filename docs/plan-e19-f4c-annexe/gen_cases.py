"""Cases for the GPU probe (probe/rt/Probe.exe) and their independent references (f4c_ref.py)."""
import json
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
from PIL import Image
import f4c_ref as C
from assets import sprite_pixels, portraits
import model as M

CURSORS = ['wind_150', 'wind_173', 'wind_201', 'wind_228']
BANK = {p['bank']: p for p in portraits()}


def vm_of(rec, portrait_row=None):
    """The probe view model state the PROPOSED mapping would write for one frame record (the shipped AlundraTextBoxViewModel.Apply for the text part)."""
    d = rec['drawn']
    anything = d is not None or rec['name'] is not None or rec['portrait'] is not None
    case = dict(root='Visible' if anything else 'Collapsed')
    if d is not None:
        clip = d['clip']
        rows = []
        for r in d['rows_visible']:
            text = ''.join(M.glyph_char(g) for g in r['glyphs'])
            rows.append(dict(text=text, left=r['x'] - 32, top=r['y'] - clip[1]))
        cur = d['cursor']
        case.update(frameTop=d['y'], clipTop=clip[1], clipHeight=clip[3], rows=rows,
                    cursor=None if cur is None else dict(src=CURSORS[cur['image']], top=d['y'] + 32))
    n = rec['name']
    if n is not None:
        case['nameBox'] = dict(left=n['frame'][0], textLeft=n['text'][0], text=C.NAMES[rec['name_id']].decode('cp1252'))
    p = rec['portrait']
    if p is not None and portrait_row is not None:
        H = portrait_row['h']
        case['portrait'] = dict(src=portrait_row['id'], tx=float(p['x'] - 8), ty=float(p['y'] - 116), sx=p['w'] / 48.0, sy=p['h'] / float(H))
    return case


def build(name, recs, frames, bank, ks=(1,), extra=None):
    row = BANK[bank]
    out = []
    for f in frames:
        rec = recs[f]
        for k in ks:
            c = vm_of(rec, row)
            c.update(name='%s_f%02d_x%d' % (name, f, k), k=k)
            if extra:
                c.update(extra)
            out.append(c)
    return out


def main():
    cases = []
    refs = {}      # case name -> (record, bank)

    # S1: name + portrait (Jess, bank 4, 48 x 56), every frame at x1, a few at x2 and x3
    s1 = C.run([(0x0D, C.entity(0x104, True), b'AB', True, None)], frames=60)
    for f in range(3, 45):
        cases += build('S1', s1, [f], 4)
        refs['S1_f%02d_x1' % f] = (s1[f], 4)
    for f in (19, 24, 25, 11, 5, 39):
        cases += build('S1', s1, [f], 4, ks=(2, 3))
        refs['S1_f%02d_x2' % f] = (s1[f], 4)
        refs['S1_f%02d_x3' % f] = (s1[f], 4)

    # S8: a 48 x 72 portrait (Miming, bank 122), speaker at (200, 150, 0), text 'AB'; name id 0x17a
    s8 = C.run([(0x0D, C.entity(0x17A, True, h=72), b'AB', True, None)], frames=60)
    for f in (4, 5, 11, 19, 24, 25, 28, 40):
        cases += build('S8', s8, [f], 122)
        refs['S8_f%02d_x1' % f] = (s8[f], 122)

    # S5: two dialogues in a row, the second speaker's name and portrait start while the first box is still leaving
    s5 = C.run([(0x0D, C.entity(0x104, True), b'AB', True, None), (0x0D, C.entity(0x104, True), b'CD', True, None)], frames=110)
    for f in (40, 41, 42, 43, 44, 45):
        cases += build('S5', s5, [f], 4)
        refs['S5_f%02d_x1' % f] = (s5[f], 4)

    # S3: name only, S2: portrait only
    s3 = C.run([(0x0D, C.entity(0x10C, False), b'AB', True, None)], frames=60)
    for f in (5, 19, 25):
        cases += build('S3', s3, [f], 4)
        refs['S3_f%02d_x1' % f] = (s3[f], 4)
    s2 = C.run([(0x0D, C.entity(0xFF, True), b'AB', True, None)], frames=60)
    for f in (5, 19):
        cases += build('S2', s2, [f], 4)
        refs['S2_f%02d_x1' % f] = (s2[f], 4)

    # views that are not 4:3: the engine offsets the view (ADR-0054); frames where the name frame is beyond the right edge and the box is below the bottom
    for f in (4, 5, 6, 40, 41):
        for k in (2, 3):
            c = vm_of(s1[f], BANK[4])
            c.update(name='S1off_f%02d_x%d' % (f, k), k=k, ox=97, oy=41)
            cases.append(c)
    os.makedirs(os.path.join(HERE, 'out', 'ref'), exist_ok=True)
    json.dump(cases, open(os.path.join(HERE, 'out', 'cases.json'), 'w'), indent=1)
    px = {b: sprite_pixels(BANK[b]['id']) for b in (4, 122)}
    for name, (rec, bank) in refs.items():
        for rule in ('center', 'psx'):
            img = C.compose(rec, px[bank], rule=rule)
            C.save(img, os.path.join(HERE, 'out', 'ref', '%s_%s.png' % (name, rule)), 1)
    print(len(cases), 'cases;', len(refs), 'reference frames')
    json.dump({n: dict(frame=r['frame'], rel=r['rel'], portrait=r['portrait'], name=r['name'], box_y=None if r['drawn'] is None else r['drawn']['y']) for n, (r, b) in refs.items()},
              open(os.path.join(HERE, 'out', 'refs_meta.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
