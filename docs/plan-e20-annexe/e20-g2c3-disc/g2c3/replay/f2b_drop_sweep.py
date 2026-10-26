"""f2b_drop_sweep.py - read-only replay: the torch thrown flat, a Goutte d'eau (map 87 sprite 135) placed at x in a list, same height 0; reports for each x whether +0x130 of the torch ever names the drop,
the byte +0x274 at the end, and whether a flame (type 0xC) is born.  Run: python -B -I -X utf8 f2b_drop_sweep.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'work', 'orc')); sys.path.insert(0, HERE)
import gl
import k0lib as K
import c4_scen as S
C70, E20, E0 = 0x8007B7B0, 0x8007F6C8, 0x8007ED10

def run(x, y=100, n=140):
    S.widen('map_86', 137)
    m = gl.world(keep={C70, E20, E0})
    c = gl.add_crate(m, 130, 100, mapname='map_86', sprite=137)
    S.widen('map_87', 135)
    d = gl.add_crate(m, x, y, mapname='map_87', sprite=135, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    named = []; b274 = []; flame = []
    de = K.ent(d)
    def post(mm, t, r):
        e = K.ent(c)
        if mm.peek(e + 0x130) == de: named.append(t)
        b274.append((t, mm.peek(e + 0x274) & 0xFF))
        if any(mm.peek(K.ent(s_) + 0x68) == 0xC for s_ in K.active(mm)): flame.append(t)
    rows = gl.run(m, nn, pads, crates=[c], post=post)
    e = K.ent(c)
    first274 = next((t for t, v in b274 if v), None)
    return 'drop at x=%d: +0x130 names the drop at ticks %s; byte 0x274 first non-zero at %s; flame ticks %s' % (x, (named[:3] + ['...'] if len(named) > 3 else named), first274, (str(flame[0]) + '..' + str(flame[-1])) if flame else 'none')

if __name__ == '__main__':
    for x in (140, 160, 170, 178, 184, 190, 200, 210):
        print(run(x))
