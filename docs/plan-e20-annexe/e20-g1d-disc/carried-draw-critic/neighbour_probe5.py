"""READ-ONLY probe (copy of the h3 oracle). Mechanism proof: a PERSISTENT collidable neighbour (global sprite 25, flags 0x6080) whose body HEIGHT field +0x1F8 is set to 16 px (harness write, as a crate or a pot has) before each run of the REAL
sort driver 0x800399B8. Question: does the hero's key get raised above the NON-collidable carried bomb's (bomb drawn behind the hero in the original)?"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle_h3bin'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W

def case(dy, dx=0, height=0x0FFFFF, nticks=70):
    m, STATS = G.make(0x23, hero_px=(200, 200))
    p = G.spawn_global(m, 25, 200 + dx, 200 + dy)
    r = G.Run(m, lambda t: (G.CIRCLE if t == 4 else 0))
    rows = []
    def post(m, t):
        if height is not None:
            m.poke(p + 0x1F8, height)
        W.run_pass(m, 0x800399B8)
        bomb = None
        for i in range(1, 40):
            q = W.ent(i)
            if m.peeks(q + 0x10) in (2, 3) and m.peeks(q + 0x68) == 2 and m.peek(q + 0x28) != 0:
                bomb = q; break
        if bomb is None: return
        rows.append((t, m.peek(G.HERO + 0x1BC) & 0xFFFFFFFF, m.peek(bomb + 0x1BC) & 0xFFFFFFFF, m.peek(p + 0x1BC) & 0xFFFFFFFF, m.peeks(bomb + 0x11C) / 65536))
    r.step(nticks, post=post)
    behind = [t for t, h, b, c, z in rows if b < h]
    front = [t for t, h, b, c, z in rows if b > h]
    print("neighbour sprite 25 at hero%+d,%+d height %s: ticks %d, bomb in front %d, BEHIND %d (first/last behind tick: %s/%s)" % (
        dx, dy, 'real 32px' if height is None else '%.0fpx' % (height / 65536 + 1 / 65536), len(rows), len(front), len(behind), behind[:1], behind[-1:]))
    for t, h, b, c, z in rows[-1:]:
        print("      last: hero key %08X bomb key %08X neighbour key %08X bomb z %.1f" % (h, b, c, z))
case(8, height=None)
case(8)
case(14)
case(-8)
case(16)
case(22)
