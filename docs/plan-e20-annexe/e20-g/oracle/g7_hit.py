"""g7_hit.py - S7: the thrown crate against a Beannoid (map 14 sprite 79, ClassA target, real record) by the REAL hit pass 0x80039300 (pworld unhooks the stub).
The Beannoid is placed by the harness (X = TX, Y = 100) with its handlers inert (pworld stubs); the hero throws moving east at t=61.
Run: python -B -I g7_hit.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K
import g4_throw as G
from machine import M32

TFIELDS = [('hp', 0x14, 1), ('hitc', 0x220, 1), ('touch', 0x224, 0), ('inv', 0x1CC, 1), ('inv1d0', 0x1D0, 1), ('c1c8', 0x1C8, 0), ('status', 0x10, 1)]


def run_one(tx, ty=100, throw_hold=gl.RIGHT, n=100):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]
    pe = K.ent(p)
    K.poke_entity(m, p, px=tx * 65536, py=ty * 65536)
    m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (ty * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, throw_hold | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    tinfo = {}

    def post(m_, t, r):
        r['tgt'] = gl.snapf(m_, pe, TFIELDS)
    rows = gl.run(m, nn, pads, crates=[c], extra=[p], post=post)
    return m, c, p, rows


if __name__ == '__main__':
    out = []
    for tx in (150, 165, 172, 178):
        m, c, p, rows = run_one(tx)
        s = G.throw_stats(rows, c, 60)
        thc = ana.changes([dict(tick=r['tick'], hero=r['tgt']) for r in rows], 'hero', ['hp', 'hitc', 'touch', 'inv'], first=55)
        cc = ana.changes(rows, c, ['hitc', 'status', 'anim_t', 'flags6C'], first=58)
        out.append('Beannoid at x=%d: target changes %s ; crate changes %s ; crate path %s' % (tx, thc, cc, s['path'][:14]))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g7-hit.txt'), 'w').write('\n'.join(out) + '\n')
