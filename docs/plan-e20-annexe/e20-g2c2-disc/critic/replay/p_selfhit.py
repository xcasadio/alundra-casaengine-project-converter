"""p_selfhit.py (critic) - read-only: does the REAL hit pass (0x80039300, as in g7_hit.py) make the thrown / carried torch hit the hero that carries or throws it?
Logs per tick: torch status, anim_t, flags6C, +0x220 (HitCounter), +0x1C8 (current attack ptr), hero +0x224 (TouchingEntity), hero +0x1D0, and the journal lines of pass hits.
Run: python -B -I -X utf8 p_selfhit.py [moving|still|carry]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S
from machine import M32

C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10


def run(variant, n=110):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0})
    c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137)
    if variant == 'moving':
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (n - 61, 0)]
    elif variant == 'still':
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, 0 | gl.SQUARE), (n - 61, 0)]
    else:   # carry: lift and keep carrying
        seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 11, 0)]
    pads, nn = gl.pad_script(seq)
    log = []
    HERO = gl.HERO if hasattr(gl, 'HERO') else None

    def post(mm, t, r):
        e = K.ent(c)
        h = mm.peek(0x80127D30)
        log.append((t, mm.peek(e + 0x10), mm.peek(e + 0x88), hex(mm.peek(e + 0x6C)), mm.peek(e + 0x220), hex(mm.peek(e + 0x1C8)), hex(mm.peek(0x80127D30 + 0x224)), mm.peek(0x80127D30 + 0x1D0), mm.peek(0x80127D30 + 0x14)))
    rows = gl.run(m, nn, pads, crates=[c], post=post)
    out = ['variant %s: (tick, torch status, torch anim_t, torch flags, torch HitCounter, torch +0x1C8, hero +0x224, hero +0x1D0, hero hp)' % variant]
    prev = None
    for x in log:
        key = x[1:]
        if key != prev and x[0] >= 55:
            out.append(str(x))
        prev = key
    hits = [(r['tick'], j) for r in rows for j in r['journal'] if 'spark' in j or 'fx' in j]
    out.append('first journal fx/spark lines: %s' % (hits[:6],))
    return out


if __name__ == '__main__':
    for v in (sys.argv[1:] or ['moving', 'still', 'carry']):
        print('\n'.join(run(v)))
        print()
