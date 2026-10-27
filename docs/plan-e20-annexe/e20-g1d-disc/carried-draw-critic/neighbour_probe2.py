import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import neighbour_probe as N
import h3_game as G
import world as W
# detailed look at the boxes used by the overlap test for sprite 25 next to the hero
m, STATS = G.make(0x23, hero_px=(200, 200))
for dy in (6, 10, 14, 18, 24, 30):
    m, STATS = G.make(0x23, hero_px=(200, 200))
    p = G.spawn_global(m, 25, 200, 200 + dy)
    r = G.Run(m, lambda t: (G.CIRCLE if t == 4 else 0))
    r.step(60)
    W.run_pass(m, 0x800399B8)
    H = G.HERO
    def box(e):
        return dict(x=m.peeks(e+0x114)/65536, y=m.peeks(e+0x118)/65536, bx=m.peeks(e+0x1D8)/65536, by=m.peeks(e+0x1DC)/65536, sx=m.peeks(e+0x1F0)/65536, sy=m.peeks(e+0x1F4)/65536,
                    zb=m.peeks(e+0x1E0)/65536, h=m.peeks(e+0x1F8)/65536, top=m.peeks(e+0x1C0)/65536, key=m.peek(e+0x1BC)&0xFFFFFFFF, idsv=m.peeks(e+0x1A8), fl=m.peek(e+0x6C), st=m.peeks(e+0x10))
    print("dy=%d hero %s" % (dy, box(H)))
    print("       neigh %s" % (box(p),))
    # the bomb
    for i in range(1, 40):
        q = W.ent(i)
        if m.peeks(q + 0x10) in (2, 3) and m.peeks(q + 0x68) == 2:
            print("       bomb  %s" % (box(q),)); break
