"""hole_c1c.py - side check: the age counter +0x1C of a FREE slot below the highest used slot keeps counting (counters pass 0x80038998 touches every slot index <= [0x80134360]).
A sprite born in such a hole starts with that stale count, not with 0.  Real code; nothing assumed."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pworld as P, world as W, c1world as C
from machine import M32
ENT0, STRIDE = W.ENT0, W.STRIDE
def ent(i): return ENT0 + STRIDE * i
m, meta = P.setup(item=None, hero_px=(100, 100), direction=24)
# a filler in slot 3 (status 5, inert), slots 1 and 2 free (zero, index, -1)
for i in (1, 2, 3):
    m.zero(ent(i), STRIDE); m.poke(ent(i), i); m.poke(ent(i) + 0x48, M32)
m.poke(ent(3) + 0x10, 5)
S = 11
def mp(mm):
    if mm.tick_no == S:
        sp = (mm.r[29] - 0x800) & ~7
        mm.poke(sp + 0x10, 100 << 16); mm.poke(sp + 0x14, 100 << 16); mm.poke(sp + 0x18, 0x18)
        p = C.side_call(mm, 0x80039F58, (ent(0), 0, 27, 100 << 16), sp)
        print('spawn at tick', S, 'slot', (p - ENT0) // STRIDE, ' +0x1C at creation =', mm.peek(p + 0x1C))
    mm.r[2] = 0
m.hooks[0x80031B50] = mp
pad = W.Pad()
for t in range(1, 16):
    m.tick_no = t
    pad.update(0); W.write_pad(m, pad)
    for a in W.PASSES:
        m.call(a, limit=5_000_000)
        if a == 0x80038998:
            print('tick %2d after counters: free slot1 +0x1C=%d  slot2 +0x1C=%d slot1 status=%d' % (t, m.peek(ent(1) + 0x1C), m.peek(ent(2) + 0x1C), m.peek(ent(1) + 0x10)))
