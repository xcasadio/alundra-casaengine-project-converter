"""k_limit3_hero.py - scen_limit3.py (critic key bomb_limit3.txt) with the hero columns: is the hero ever hurt over the 640 ticks of the key?  Written for the a362-h3a discovery (2026-10-25)."""
from scen_lib import *
m, STATS = mk(0x23)
CIRCLES = [1, 71, 141, 211, 281, 351, 421, 491]
def hold(t):
    h = 0
    if t in CIRCLES: h |= W.CIRCLE
    if (t - 42) in CIRCLES: h |= W.SQUARE
    return h
log = run(m, 640, hold)
prev = None
for r in log:
    h = r['hero']
    key = (h['hp'], h['tanim'] in (0x31, 0x3A, 0x1C))
    if key != prev:
        print('t%3d hero hp %d state %#x x %.2f y %.2f' % (r['t'], h['hp'], h['tanim'], h['x'], h['y'])); prev = key
print('min hp over the key:', min(r['hero']['hp'] for r in log), ' hurt states seen:', sorted({hex(r['hero']['tanim']) for r in log if r['hero']['tanim'] in (0x31, 0x3A, 0x1C)}))
print('hero x range %.2f..%.2f' % (min(r['hero']['x'] for r in log), max(r['hero']['x'] for r in log)))
