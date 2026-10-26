"""k_w1_posed.py - h3a (w1) candidate keys: a bomb born POSED by the real SpawnWarpEntity (as the DLL test will: SpawnEntityBySpriteIndex, no hands), put to status 3 by the harness after tick K
(K = the key's tick 0), the hero idle at (100, 100) in the synthetic field.  Variants: the bomb at (DX, DY) from the hero.  Columns per tick: bomb status / target anim / current anim / end flag / z,
hero touching / hp / state / x,y / invulnerability counters, journal sounds and effects.   usage: k_w1_posed.py DX DY [K] [NTICKS]
Written for the a362-h3a discovery (2026-10-25).  The harness writes: the bomb's position (SpawnWarpEntity), its status (+0x10 = 3 once, after tick K)."""
import sys
import spellworld as S
import world as W
from machine import s32, M32
from scen_lib import *
from scen_zone import spawn

DX = float(sys.argv[1]); DY = float(sys.argv[2])
K = int(sys.argv[3]) if len(sys.argv) > 3 else 5
N = int(sys.argv[4]) if len(sys.argv) > 4 else 90
m, STATS = mk(None, indices=(1, 2, 253))
H = W.ent(0)
import os
if os.environ.get('EMPTY_INV'):
    for i in range(128): m.poke(STATS + 0x12 + 4 * i, 0)
    m.poke(0x80127164, 0)
hx, hy = m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536
pad = W.Pad()
b = spawn(m, H, 0, 2, hx + DX, hy + DY, 0)
assert b
m.poke(b + 0x274, 240)                       # the harness: the fuse of C62 kept high so that the native C62 (not ported by h3a) does not end it before the poke
print('hero at (%.2f,%.2f) ; bomb slot at %#x born at (%.2f,%.2f) z %.2f ; fuse word %d' % (hx, hy, b, m.peeks(b + 0x114) / 65536, m.peeks(b + 0x118) / 65536, m.peeks(b + 0x11C) / 65536, m.peeks(b + 0x274)))
prev = None
for t in range(0, N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    if t == K:
        m.poke(b + 0x10, 3)                      # the harness: status 3 (Deactivated) at the end of tick K
    bs = (m.peeks(b + 0x10), m.peeks(b + 0x88), m.peeks(b + 0x90), m.peeks(b + 0xAC), round(m.peeks(b + 0x11C) / 65536, 2), m.peeks(b + 0x1D4))
    hs = (m.peeks(H + 0x14), m.peeks(H + 0x88), m.peek(H + 0x224), m.peeks(H + 0x1CC), m.peeks(H + 0x1D0))
    jr = [(x[1], x[2] if x[1] == 'sound' else tuple(x[2][:2])) for x in m.journal[j0:] if x[1] in ('sound', 'effect_3BDD8') and not (x[1] == 'sound' and x[2] == 0)]
    key = (bs, hs, tuple(jr))
    if key != prev:
        print('k%+4d bomb(st,tanim,canim,end,z,box)=%s hero(hp,state,touching,1cc,1d0)=%s x=%.2f y=%.2f %s' % (t - K, bs, (hs[0], hex(hs[1]), hex(hs[2]), hs[3], hs[4]), m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536, jr if jr else ''))
    prev = key
    if bs[0] == 0 and t > K:
        print('k%+4d bomb slot free' % (t - K)); break
