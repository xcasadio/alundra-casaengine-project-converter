"""c_olen_lid.py - critic: Olen's bomb against the lid in the oracle (flat field, floor z=0).  The lid (global 243) at (LX, LY), the bomb (global 2) born at (BX, BY) at z = Z0 facing north (0x10), the native C62 neutralized
(+0x78 = 0: the map program C[5] replaces it), `Set anim 2` once at tick T4, `Set anim 1` at tick T5 (the explosion), the fuse never used.  Prints the bomb's x,y,z, the lid's status and TouchingEntity (+0x224).
usage: c_olen_lid.py LX LY BX BY Z0 T4 T5 [N]"""
import sys
import spellworld as S
import world as W
from machine import s32, M32
from scen_lib import *
from scen_zone import spawn
LX, LY, BX, BY, Z0 = [float(a) for a in sys.argv[1:6]]
T4, T5 = int(sys.argv[6]), int(sys.argv[7])
N = int(sys.argv[8]) if len(sys.argv) > 8 else T5 + 80
m, STATS = mk(None, indices=(1, 2, 243, 253))
H = W.ent(0)
pad = W.Pad()
lid = spawn(m, H, 0, 243, LX, LY, 0)
b = spawn(m, H, 0, 2, BX, BY, Z0, 0x10)
assert lid and b
m.poke(b + 0x78, 0)
print('lid', hex(lid), 'flags %#x' % m.peek(lid + 0x6C), 'pos (%.2f,%.2f,%.2f)' % (m.peeks(lid + 0x114) / 65536, m.peeks(lid + 0x118) / 65536, m.peeks(lid + 0x11C) / 65536))
prev = None
for t in range(1, N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    if t == T4: m.poke(b + 0x88, 2)
    if t == T5: m.poke(b + 0x88, 1)
    jr = [(x[1], x[2] if x[1] == 'sound' else tuple(x[2][:2])) for x in m.journal[j0:] if x[1] in ('sound', 'effect_3BDD8', 'effect_3BE74') and not (x[1] == 'sound' and x[2] == 0)]
    st = (round(m.peeks(b + 0x114) / 65536, 2), round(m.peeks(b + 0x118) / 65536, 2), round(m.peeks(b + 0x11C) / 65536, 2), m.peeks(b + 0x10), m.peeks(b + 0x90), m.peeks(b + 0x1D4) != 0, m.peek(lid + 0x224) != 0, m.peeks(lid + 0x10), m.peeks(lid + 0x1D0))
    key = (st, tuple(map(str, jr)))
    if key != prev:
        print('t%3d bomb x %.2f y %.2f z %.2f status %d canim %d box %s | lid touching %s status %d 1d0 %d %s' % ((t,) + st + (jr if jr else '',)))
    prev = key
