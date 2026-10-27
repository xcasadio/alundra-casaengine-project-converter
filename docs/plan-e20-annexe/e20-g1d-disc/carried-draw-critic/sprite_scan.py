import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle_h3bin'))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')
import h3_game as G
import world as W
res = []
for spr in range(0, 120):
    try:
        m, STATS = G.make(0x23, hero_px=(200, 200))
        p = G.spawn_global(m, spr, 230, 230)
        if not p:
            continue
        r = G.Run(m, lambda t: 0)
        r.step(2)
        W.run_pass(m, 0x800399B8)
        res.append((spr, m.peek(p+0x6C), m.peek(p+0xB4) & 0xFF, m.peeks(p+0x1F8)/65536, m.peeks(p+0x1F0)/65536, m.peeks(p+0x1F4)/65536, m.peeks(p+0x10)))
    except Exception as e:
        pass
for spr, fl, afl, h, sx, sy, st in res:
    print("sprite %3d flags %08X collidable=%s afl80=%s height %.1f box %.1fx%.1f st %d" % (spr, fl, bool(fl & 0x80), bool(afl & 0x80), h, sx, sy, st))
