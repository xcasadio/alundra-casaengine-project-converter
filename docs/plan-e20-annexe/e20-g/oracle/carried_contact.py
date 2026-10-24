"""carried_contact.py - a CARRIED crate (anim 0, no attack box) walked into a Class-A target by the REAL hit pass: does it register a hit (target +0x224, crate +0x220)? Run: python -B -I carried_contact.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import k0lib as K
from machine import M32

def run(tx):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    pl, _ = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    p = pl[0]; pe = K.ent(p)
    K.poke_entity(m, p, px=tx * 65536, py=100 * 65536)
    m.poke(pe + 0x1D8, (tx * 65536 + m.peeks(pe + 0x1E4)) & M32); m.poke(pe + 0x1DC, (100 * 65536 + m.peeks(pe + 0x1E8)) & M32)
    T = m.call(0x800370C4, pe)
    m.poke(pe + 0x138, T); m.poke(pe + 0x11C, T + 1); m.poke(pe + 0x1E0, T + 1)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (80, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)
    ev = []
    def post(m_, t, r):
        if m_.peek(pe + 0x224) != 0:
            ev.append(('target.+0x224 set', t, hex(m_.peek(pe + 0x224))))
        if m_.peek(K.ent(c) + 0x220) != 0:
            ev.append(('crate.+0x220', t, m_.peek(K.ent(c) + 0x220)))
        r['crate_1d4'] = m_.peek(K.ent(c) + 0x1D4); r['crate_1c8'] = m_.peek(K.ent(c) + 0x1C8)
    rows = gl.run(m, nn, pads, crates=[c], extra=[p], post=post)
    xs = [(r['tick'], round(r['hero']['px'] / 65536, 1), round(r['c'][c]['px'] / 65536, 1)) for r in rows if r['tick'] in (11, 40, 60, 80, 90)]
    return ev[:6], xs, sorted(set((r['crate_1d4'], r['crate_1c8']) for r in rows if r['tick'] > 12))[:4]

if __name__ == '__main__':
    for tx in (165, 200):
        print('target at x=%d' % tx, run(tx))
