"""c_olen_flight.py - critic: Olen's bomb flight, approximated in the oracle.  A bomb (global 2) born at (200,200) z=Z0 facing the direction DIR (north = 0x10), the harness
holds the TARGET ANIMATION at 2 on every tick (the C[5] script of the map 362 does `Set anim 2` and never resets it; the native C62 would reset it to 0 at the landing) and the fuse of C62 at 240.
Prints x, y, z, state each tick when something changes.  usage: c_olen_flight.py Z0 DIR [N]"""
import sys
import spellworld as S
import world as W
from machine import s32, M32
from scen_lib import *
from scen_zone import spawn
Z0 = float(sys.argv[1]); D = int(sys.argv[2], 0)
N = int(sys.argv[3]) if len(sys.argv) > 3 else 140
m, STATS = mk(None, indices=(1, 2, 253))
H = W.ent(0)
pad = W.Pad()
b = spawn(m, H, 0, 2, 200, 200, Z0, D)
assert b
print('bomb', hex(b), 'dir param', hex(D), 'flags %#x' % m.peek(b + 0x6C), 'target dir +0x98?', m.peeks(b + 0x98), 'dir +0x9C', m.peeks(b + 0x9C))
prev = None
for t in range(1, N + 1):
    m.tick_no = t
    W.tick(m, pad, 0, check_pad=False)
    m.poke(b + 0x274, 240)
    m.poke(b + 0x88, 2)
    st = (round(m.peeks(b + 0x114) / 65536, 2), round(m.peeks(b + 0x118) / 65536, 2), round(m.peeks(b + 0x11C) / 65536, 2), m.peeks(b + 0x10), m.peeks(b + 0x90), m.peeks(b + 0x13C), m.peeks(b + 0x144))
    if st != prev:
        print('t%3d x %.2f y %.2f z %.2f status %d canim %d forceAdj %d gnd %#x' % ((t,) + st))
    prev = st
