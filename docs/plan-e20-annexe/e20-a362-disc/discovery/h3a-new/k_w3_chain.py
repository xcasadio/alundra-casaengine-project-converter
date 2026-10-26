"""k_w3_chain.py - h3a (w3) candidate key: two bombs born POSED (real SpawnWarpEntity), bomb 1 put to status 3 by the harness after tick K, bomb 2 11 px east of it, fuse kept at 240 (the native C62 is not ported by h3a),
the hero idle at (100, 100), the bombs at (HX + DX, HY + DY).   usage: k_w3_chain.py DX DY [K] [N]    Written for the a362-h3a discovery (2026-10-25)."""
import sys
import world as W
from scen_lib import *
from scen_zone import spawn
DX = float(sys.argv[1]); DY = float(sys.argv[2])
K = int(sys.argv[3]) if len(sys.argv) > 3 else 5
N = int(sys.argv[4]) if len(sys.argv) > 4 else 90
m, STATS = mk(None, indices=(1, 2, 253))
H = W.ent(0)
hx, hy = m.peeks(H + 0x114) / 65536, m.peeks(H + 0x118) / 65536
pad = W.Pad()
b1 = spawn(m, H, 0, 2, hx + DX, hy + DY, 0); m.poke(b1 + 0x274, 240)
b2 = spawn(m, H, 0, 2, hx + DX + 11, hy + DY, 0); m.poke(b2 + 0x274, 240)
print('hero (%.2f,%.2f) bomb1 (%.2f,%.2f) bomb2 (%.2f,%.2f)' % (hx, hy, m.peeks(b1 + 0x114) / 65536, m.peeks(b1 + 0x118) / 65536, m.peeks(b2 + 0x114) / 65536, m.peeks(b2 + 0x118) / 65536))
prev = None
for t in range(0, N + 1):
    m.tick_no = t
    j0 = len(m.journal)
    W.tick(m, pad, 0, check_pad=False)
    if t == K:
        m.poke(b1 + 0x10, 3)
    s = lambda b: (m.peeks(b + 0x10), m.peeks(b + 0x88), m.peeks(b + 0xAC), m.peeks(b + 0x220), 1 if m.peeks(b + 0x1D4) else 0)
    hs = (m.peeks(H + 0x14), m.peeks(H + 0x88), 1 if m.peek(H + 0x224) else 0)
    snd = [x[2] for x in m.journal[j0:] if x[1] == 'sound' and x[2] != 0]
    key = (s(b1), s(b2), hs, tuple(snd))
    if key != prev:
        print('k%+4d bomb1(st,anim,end,hitc,box)=%s bomb2=%s hero(hp,state,touching)=%s %s' % (t - K, s(b1), s(b2), hs, ('sounds %s' % snd) if snd else ''))
    prev = key
    if s(b1)[0] == 0 and s(b2)[0] == 0 and t > K:
        print('k%+4d both slots free' % (t - K)); break
