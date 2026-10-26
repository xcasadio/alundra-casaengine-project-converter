"""f2_drop.py - read-only replay (copy of the g2c oracle machine): the torch thrown flat into a Goutte d'eau (map 87 sprite 135, type 391 = 0x187), REAL C70 and E20.
Question: does E20's contact rule (+0x130 of type 0x187 -> byte +0x274 = 1) suppress the flame?  Three variants: 'nodrop' (reference: the flame is born), 'drop' (the drop at x=150 in the path),
'dropfar' (the drop at x=300, out of the path).  The drop is a placed record whose handlers are inert (only its body, Collidable, matters).
Run: python -B -I -X utf8 f2_drop.py [nodrop|drop|dropfar]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S

C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10


def run(variant, n=200):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0})
    c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137)
    d = None
    if variant != 'nodrop':
        S.widen('map_87', 135)
        d = gl.add_crate(m, 150 if variant == 'drop' else 300, 100, mapname='map_87', sprite=135, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    track = {}

    def post(mm, t, r):
        e = K.ent(c)
        track[t] = (mm.peek(e + 0x10), mm.peek(e + 0x88), hex(mm.peek(e + 0x6C)), mm.peek(e + 0x130), mm.peek(e + 0x274) & 0xFF, mm.peek(e + 0xAC), round(mm.peeks(e + 0x114) / 65536, 1))

    rows = gl.run(m, nn, pads, crates=[c], post=post)
    out = ['variant %s (drop slot %s, drop entity %s, drop type %s)' % (variant, d, hex(K.ent(d)) if d is not None else None, hex(m.peek(K.ent(d) + 0x68)) if d is not None else None)]
    prev = None
    for t in sorted(track):
        if t < 60:
            continue
        v = track[t]
        if v != prev:
            out.append('t%d: status=%d anim_t=%d flags=%s xcoll=%s b274=%d AC=%d x=%.1f' % ((t,) + v))
            prev = v
    flame = [s_ for s_ in K.active(m) if m.peek(K.ent(s_) + 0x68) == 0xC]
    out.append('flame slots at the end: %s' % flame)
    out.append('journal: %s' % [(r['tick'], r['journal']) for r in rows if r['tick'] > 60 and [x for x in r['journal'] if not x.startswith('stub')]][:8])
    return out


if __name__ == '__main__':
    for v in (sys.argv[1:] or ['nodrop', 'drop', 'dropfar']):
        print('\n'.join(run(v)))
        print()
