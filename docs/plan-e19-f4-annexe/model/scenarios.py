"""Value tables of the name box and the portrait for a few dialogue scenarios (model/f4_model.py, validated against the
real binary code by ../validate.py).  Writes ../values.md and ../values.json.

Frame numbering: N = the frame of the script phase in which the opcode runs (the text box opens in N; first pass N+1).
"""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import model as M
import f4_model as F
from names import load_names

NAMES = {k: v.encode('cp1252') for k, v in load_names().items()}
HERE = os.path.dirname(os.path.abspath(__file__))


def entity(id, portrait, x=200, y=150, z=0):
    return dict(id=id, flags=F.FLAG_HAS_PORTRAIT if portrait else 0, x=x, y=y, z=z)


def run(dialogues, frames=130, cam=(40, 20), pad=M.pad_every_frame, n0=3):
    """dialogues: list of (kind, entity, text, matched, name_id).  The script tries the first at frame n0 and each
    next one at the tick after the previous returned (retrying while the box is active)."""
    box = F.F4Box(NAMES)
    box.cam = cam
    rows = []
    i = 0
    for f in range(0, frames):
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
        rows.append(dict(frame=f, y=box.y, phase=box.phase(), nb=nd, pt=pd, nb_flags=box.nb.flags, pt_state=box.pt.state, events=ev))
    return rows


def key(r):
    nb = r['nb']; pt = r['pt']
    return (r['phase'], r['y'], None if nb is None else (nb['frame'], nb['text'], nb['clip']), None if pt is None else (pt['x'], pt['y'], pt['w'], pt['h'], pt['rgb']), tuple(r['events']))


def compress(rows, n0):
    """Table with identical consecutive rows collapsed."""
    out = ['| frames | rel | text box | name box (frame x / text x,y / clip) | portrait (x,y w x h rgb) | events |', '|---|---|---|---|---|---|']
    i = 0
    while i < len(rows):
        j = i
        while j + 1 < len(rows) and key(rows[j + 1]) == key(rows[i]) and not rows[j + 1]['events']:
            j += 1
        r = rows[i]
        nb = r['nb']; pt = r['pt']
        nbs = '-' if nb is None else '%d / %d,%d / %s' % (nb['frame'][0], nb['text'][0], nb['text'][1], ','.join(map(str, nb['clip'])))
        pts = '-' if pt is None else '%d,%d %dx%d %d' % (pt['x'], pt['y'], pt['w'], pt['h'], pt['rgb'])
        fr = str(r['frame']) if i == j else '%d..%d' % (r['frame'], rows[j]['frame'])
        rel = 'N%+d' % (r['frame'] - n0) if i == j else 'N%+d..N%+d' % (r['frame'] - n0, rows[j]['frame'] - n0)
        out.append('| %s | %s | %s y=%d | %s | %s | %s |' % (fr, rel, r['phase'], r['y'], nbs, pts, '; '.join(r['events'])))
        i = j + 1
    return '\n'.join(out)


SCEN = {
    'S1 name + portrait (Jess 0x104, entity at (200,150,0), camera (40,20); text "AB", Square pressed every frame)':
        dict(d=[(0x0D, entity(0x104, True), b'AB', True, None)], frames=60),
    'S2 portrait only (id 0xFF below 0x100, portrait flag)':
        dict(d=[(0x0D, entity(0xFF, True), b'AB', True, None)], frames=60),
    'S3 name only (Septimus 0x10C, no portrait flag)':
        dict(d=[(0x0D, entity(0x10C, False), b'AB', True, None)], frames=60),
    'S4 neither (id 0xFF, no flag): the text box alone':
        dict(d=[(0x0D, entity(0xFF, False), b'AB', True, None)], frames=60),
    'S5 two dialogues in a row, same speaker (the second opcode retries while the first box is up)':
        dict(d=[(0x0D, entity(0x104, True), b'AB', True, None), (0x0D, entity(0x104, True), b'CD', True, None)], frames=110),
    'S6 0xC4: explicit name 0x10C (Septimus) on a matched entity that has a portrait; the entity own id is 0x104':
        dict(d=[(0xC4, entity(0x104, True), b'AB', True, 0x10C)], frames=60),
    'S7 0x5C whose search finds nobody: no name, no portrait, the box opens':
        dict(d=[(0x5C, entity(0x104, True), b'AB', False, None)], frames=60),
}


def main():
    md = ['# E19.f4 value tables (model/f4_model.py + model/model.py, validated against the real binary code)', '',
          'Frame N = the script phase in which the opcode runs. Name box: cfg.x of the frame cells / text sprite x,y / draw-area clip (x,y,w,h). '
          'Portrait: top-left x,y, drawn size, vertex colour (all three channels). `-` = not drawn that frame.', '']
    js = {}
    for title, sc in SCEN.items():
        rows = run(sc['d'], frames=sc['frames'])
        n0 = 3
        md.append('## ' + title)
        md.append('')
        md.append(compress(rows, n0))
        md.append('')
        js[title] = [dict(frame=r['frame'], rel=r['frame'] - n0, phase=r['phase'], box_y=r['y'], name=r['nb'], portrait=r['pt'], events=r['events']) for r in rows]
    open(os.path.join(HERE, '..', 'values.md'), 'w', encoding='utf-8').write('\n'.join(md))
    json.dump(js, open(os.path.join(HERE, '..', 'values.json'), 'w'), indent=1)


if __name__ == '__main__':
    main()
