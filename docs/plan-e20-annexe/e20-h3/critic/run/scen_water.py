"""scen_water.py - critic: bomb / beans thrown into deep water (cells with slope byte 4 east of column 5)."""
from scen_lib import *
TILES = W.TILES; ROW = W.ROW
def cell(c, r): return TILES + 0x604 + r * ROW + c * 8
for item in (0x23, 0x1F):
    m, STATS = mk(item)
    for c in range(5, 40):
        for r in range(0, 60):
            m.poke16(cell(c, r), (m.peek(cell(c, r)) & 0xFF) | (0x08 << 8))
    log = run(m, 90, lambda t: (W.CIRCLE if t == 1 else (W.SQUARE if t == 42 else 0)))
    print('=== item %x thrown at t42 into slope-4 cells (col>=5)' % item)
    prev = None
    for r in log:
        if r['t'] < 60: continue
        key = [(i, e['st'], e['tanim']) for i, e in r['ents'].items()]
        if key != prev or r['jr']:
            print(' t%2d ents %s journal %s' % (r['t'], {i: (e['st'], e['tanim'], round(e['x'], 1), round(e['z'], 1)) for i, e in r['ents'].items()}, [(x[1], x[2] if x[1]=='sound' else x[2][:2]) for x in r['jr']]))
        prev = key
