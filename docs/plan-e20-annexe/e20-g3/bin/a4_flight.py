"""a4_flight.py - disc-e20g3 A4: is a thrown liftable still in the collidable list in flight?  C23 family (crate, map 392 sprite 36) versus C4 family (stone, map 22 sprite 34), real C23 / C4 / E2, flat world.
Sequence as g18: lift at tick 11, the gesture lasts ~36 ticks, throw east at tick 61. Prints, for ticks 58..90, flags, anim, +0x28 and list membership of the object."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
gl = L.gl
OUT = []
def P(s):
    OUT.append(s); print(s, flush=True)
for name, mapname, sprite, keep in (('C23 crate (map_392 s36)', 'map_392', 36, None), ('C4 stone (map_22 s34)', 'map_22', 34, {0x80066984})):
    m = gl.world(keep=keep)
    c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
    ce = L.K.ent(c)
    def post(m_, t, r):
        n = m_.peek(0x80127D2C)
        r['member'] = any(m_.peek(0x80127C28 + 4 * k) == ce for k in range(n))
    pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (40, 0)])
    rows = gl.run(m, n, pads, crates=[c], post=post)
    P('== ' + name)
    for r in rows:
        if r['tick'] in (11, 12, 60, 61, 62, 66, 70, 76, 82, 83, 84, 85, 86, 87, 90):
            d = r['c'][c]
            P('  t%3d x=%7.2f z=%6.2f status=%d anim=%d t24=%d +0x28=%s flags=%X in collidable list=%s' % (r['tick'], d['px'] / 65536, d['pz'] / 65536, d['status'], d['anim_t'], d['t24'], hex(d['carrier28']) if d['carrier28'] else '0', d['flags6C'], r['member']))
open(os.path.join(L.OUT, 'a4_flight.txt'), 'w').write('\n'.join(OUT) + '\n')
