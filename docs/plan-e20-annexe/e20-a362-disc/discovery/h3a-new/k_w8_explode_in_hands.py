"""k_w8_explode_in_hands.py - h3b (w8) candidate: scen_hurt_circle.py (Circle at t1 then at t262, one tick after the hero was hit by his own explosion) prolonged to the end of the second bomb,
printing only the ticks where something of the hero or of the bombs changes.  NO D-H-7 setting: the unmodified oracle.  Written for the a362-h3a discovery (2026-10-25)."""
from scen_lib import *
m, STATS = mk(0x23)
def hold(t):
    return W.CIRCLE if t in (1, 262) else 0
log = run(m, 340, hold)
prev = None
for r in log:
    if r['t'] < 240: continue
    h = r['hero']
    key = (h['hp'], h['tanim'], r['link'], r['item'], tuple((i, e['st'], e['tanim'], e['end']) for i, e in sorted(r['ents'].items())), tuple(x[2] for x in r['jr'] if x[1] == 'sound' and x[2] != 0))
    if key != prev:
        print('t%3d hero hp %2d state %#04x x %.2f link %#x item %#x | %s | sounds %s' % (r['t'], h['hp'], h['tanim'], h['x'], r['link'], r['item'], {i: (e['st'], e['tanim'], e['end']) for i, e in r['ents'].items()}, [x[2] for x in r['jr'] if x[1] == 'sound' and x[2] != 0]))
    prev = key
