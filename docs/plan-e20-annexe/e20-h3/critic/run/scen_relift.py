"""scen_relift.py - critic: can the hero lift a bomb or a bean that lies in front of him (Square)?  Entity spawned 14 px east of the hero (hero faces east, x=100)."""
import sys
from scen_lib import *
from scen_zone import spawn
for kind, label in ((2, 'bomb'), (1, 'beans')):
    m, STATS = mk(None, indices=(1, 2))
    e = spawn(m, HERO, 0, kind, 100 + 14, 100, 0)
    if kind == 2: m.poke(e + 0x274, 4000)
    log = run(m, 60, lambda t: W.SQUARE if t in (3, ) else 0)
    print('=== %s: Square at t3' % label)
    prev = None
    for r in log[:30]:
        h = r['hero']
        key = (h['tanim'], r['link'])
        if key != prev:
            print(' t%2d hero tanim %2d link %x ent %s' % (r['t'], h['tanim'], r['link'], {i: (x['st'], x['tanim'], round(x['x'], 1), hex(x['fl'])) for i, x in r['ents'].items()}))
        prev = key
    print('  hero H+0x130 (touching entity) at t3:', hex(m.peek(HERO + 0x130)), ' bomb/beans flags', hex(m.peek(e + 0x6C)))
