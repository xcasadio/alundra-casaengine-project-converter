"""E19.f4c2 F4C2-0: the prediction. Writes, next to this file (every path relative to it),

  states.json         the view-model state the mapping of E19.f4c2 must write for each pinned image (the speaker part: name box, portrait with its Brightness = Rgb / 128;
                      the text part as the shipped AlundraTextBoxViewModel.Apply writes it, kept for the order points);
  classes.tsv         the comparison class of each pinned image, with its Rgb, the quad of its portrait and its reference file;
  refs/*.png          the reference images (320 k + 2 ox) x (240 k + 2 oy), composed by f4c_ref.py from the binary's value model (portrait sampled at the centre of the
                      screen pixels at the screen resolution, then modulated like the PS1: texel x Rgb / 128, saturated);
  names-digests.tsv   the SHA-1 of the RGB bytes of the 320 x 240 image of the name box alone (60 names x the three positions x 64, 303 and 150).

Comparison classes (written here, read by Alundra.Tests/UI/AlundraSpeakerPixelTests.cs):
  exact   Rgb = 128 (the portrait at rest) or no portrait: every pixel equal to the reference.
  tint8   Rgb != 128 (every pass of the opening and of the return, the rest pass of the close trigger T included: Rgb 127): every pixel OUTSIDE the quad of the portrait equal to the
          reference; inside the quad each channel within 8/255 of the reference, the rows and columns on a tie of the centre rule ((2 i + 1) * src = 2 dst * integer) excluded.

Run: python gen_cases.py   (needs numpy, Pillow and the exported ../../alundra-project; no other input than the versioned annexes)
"""
import hashlib
import json
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
import f4c_ref as C
from assets import sprite_pixels, portraits
import model as M

CURSORS = ['wind_150', 'wind_173', 'wind_201', 'wind_228']
BANK = {p['bank']: p for p in portraits()}
N0 = 3          # the opcode of the first dialogue runs at frame 3: frame = N + rel + 3


def vm_of(rec, portrait_row=None):
    """The view-model state the mapping of E19.f4c2 writes for one frame record."""
    d = rec['drawn']
    anything = d is not None or rec['name'] is not None or rec['portrait'] is not None
    case = dict(root='Visible' if anything else 'Collapsed', textBox=d is not None)
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
        case['portrait'] = dict(src=portrait_row['id'], tx=float(p['x'] - 8), ty=float(p['y'] - 116), sx=p['w'] / 48.0, sy=p['h'] / float(H), brightness=p['rgb'] / 128.0)
    return case


def main():
    scen = {}
    scen['S1'] = (C.run([(0x0D, C.entity(0x104, True), b'AB', True, None)], frames=60), 4)
    scen['S8'] = (C.run([(0x0D, C.entity(0x17A, True, h=72), b'AB', True, None)], frames=60), 122)
    scen['S5'] = (C.run([(0x0D, C.entity(0x104, True), b'AB', True, None), (0x0D, C.entity(0x104, True), b'CD', True, None)], frames=110), 4)
    scen['S3'] = (C.run([(0x0D, C.entity(0x10C, False), b'AB', True, None)], frames=60), 4)
    scen['S2'] = (C.run([(0x0D, C.entity(0xFF, True), b'AB', True, None)], frames=60), 4)

    # (scenario, [rel...], [(k, ox, oy)...]): the pinned images of the plan (F4C2-0)
    plan = [
        ('S1', [1, 2, 8, 13, 16, 21, 22, 25, 36, 37, 38, 39, 40], [(1, 0, 0)]),
        ('S1', [2, 8, 16, 21, 22, 36], [(2, 0, 0), (3, 0, 0)]),
        ('S1', [1, 2, 3, 37, 38], [(2, 97, 41), (3, 97, 41)]),
        ('S8', [2, 8, 16, 22, 25, 37], [(1, 0, 0)]),
        ('S8', [16], [(2, 0, 0)]),
        ('S5', [37, 38, 39, 40, 41, 42], [(1, 0, 0)]),
        ('S2', [2, 16], [(1, 0, 0)]),
        ('S3', [2, 16, 22], [(1, 0, 0)]),
    ]
    px = {}
    os.makedirs(os.path.join(HERE, 'refs'), exist_ok=True)
    states, rows = {}, []
    for name, rels, views in plan:
        recs, bank = scen[name]
        row = BANK[bank]
        if bank not in px:
            px[bank] = sprite_pixels(row['id'])
        for rel in rels:
            rec = recs[rel + N0]
            for k, ox, oy in views:
                ref = '%s_N%d_x%d%s' % (name, rel, k, '_off' if ox or oy else '')
                C.save(C.compose(rec, px[bank], k=k, ox=ox, oy=oy), os.path.join(HERE, 'refs', ref + '.png'))
                p = rec['portrait']
                rgb = None if p is None else p['rgb']
                klass = 'exact' if rgb is None or rgb == 128 else 'tint8'
                quad = '-' if p is None or p['w'] == 0 or p['h'] == 0 else '%d,%d,%d,%d' % (p['x'], p['y'], p['w'], p['h'])
                state = vm_of(rec, row)
                state.update(scenario=name, frame=rel + N0, rel=rel, k=k, ox=ox, oy=oy)
                states[ref] = state
                rows.append((ref, name, rel + N0, rel, k, ox, oy, '-' if rgb is None else rgb, klass, quad, ref + '.png'))
    json.dump(states, open(os.path.join(HERE, 'states.json'), 'w', encoding='utf-8', newline='\n'), indent=1)
    with open(os.path.join(HERE, 'classes.tsv'), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('name\tscenario\tframe\trel\tk\tox\toy\trgb\tclass\tquad\treference\n')
        for r in rows:
            fh.write('\t'.join(str(v) for v in r) + '\n')

    # the 60 names at three positions: the name box alone (frame + text), nothing else on the screen
    digests = []
    widths = json.load(open(os.path.join(HERE, '..', 'plan-e19-f4-annexe', 'names_widths.json'), encoding='utf-8'))
    for nid in sorted(C.NAMES):
        w = widths['0x%x' % nid]['w']
        for x in (64, 303, 150):
            tx = x + (112 - w) // 2          # C trunc: 112 - w > 0 for every name (widths 19..56)
            cw = max(0, min(258, 320 - (x + 16)))
            rec = dict(drawn=None, portrait=None, name=dict(frame=(x, 140), text=(tx, 148), clip=(x + 16, 148, cw, 34)), name_id=nid)
            img = C.compose(rec, None)
            digests.append((nid, C.NAMES[nid].decode('cp1252'), w, x, tx, hashlib.sha1(img.tobytes()).hexdigest()))
    with open(os.path.join(HERE, 'names-digests.tsv'), 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('id\tname\twidth\tx\ttext_x\tsha1_rgb_320x240\n')
        for d in digests:
            fh.write('0x%x\t%s\t%d\t%d\t%d\t%s\n' % d)
    print(len(rows), 'pinned images,', len(digests), 'name images; glyph table source:', C.LIB_SOURCE)


if __name__ == '__main__':
    main()
