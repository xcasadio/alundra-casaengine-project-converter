"""scen_zone.py - critic: explosion hit zone measured with a REAL sprite target (global sprite 253, root: class A, 24x16x28, D32 reacts to attribute 4).
A bomb is spawned with the real SpawnWarpEntity at (BX,BY), fuse shortened to 2; the target at (BX+dx, BY+dy, TZ).  Reaction = target +0x88 leaves 0 (D32)."""
import sys
import spellworld as S
import world as W
from machine import s32, M32
from scen_lib import *

BX, BY = 300, 300
SPAWN = 0x80039F58

def spawn(m, creator, page, idx, x, y, z, d=0):
    sp = 0x801FFF00
    m.poke(sp + 0x10, int(y * 65536) & M32); m.poke(sp + 0x14, int(z * 65536) & M32); m.poke(sp + 0x18, d)
    return m.call(SPAWN, creator, page, idx, int(x * 65536) & M32) & M32

class Rigw:
    def __init__(self):
        self.m, self.STATS = mk(None, indices=(1, 2, 253))
        self.snap = (bytes(self.m.ram), bytes(self.m.init))
    def restore(self):
        self.m.ram[:] = self.snap[0]; self.m.init[:] = self.snap[1]; self.m.journal.clear()
        self.m.hi = self.m.lo = 0

def trial(rw, dx, dy, tz=0, nticks=64, bz=0, fuse=2, tgt=253):
    rw.restore()
    m = rw.m
    pad = W.Pad()
    b = spawn(m, H0, 0, 2, BX, BY, bz)
    t = spawn(m, H0, 0, tgt, BX + dx, BY + dy, tz)
    assert b and t
    m.poke(b + 0x274, fuse)
    first = None
    when = None
    for k in range(1, nticks + 1):
        m.tick_no = k
        W.tick(m, pad, 0, check_pad=False)
        if m.peeks(t + 0x88) != 0 and first is None:
            first = k
        if m.peeks(b + 0x10) == 3 and when is None:
            when = k
    return first, when

H0 = W.ent(0)
if __name__ == '__main__':
    rw = Rigw()
    print('baseline center:', trial(rw, 0, 0))
    print('X scan (dy=0):')
    res = {}
    for dx in list(range(-40, -30)) + list(range(30, 42)):
        res[dx] = trial(rw, dx, 0)
        print('  dx %4d -> first reaction tick %s (bomb status3 at tick %s)' % (dx, res[dx][0], res[dx][1]))
    print('Y scan (dx=0):')
    for dy in list(range(-26, -18)) + list(range(18, 28)):
        r = trial(rw, 0, dy)
        print('  dy %4d -> %s' % (dy, r))
