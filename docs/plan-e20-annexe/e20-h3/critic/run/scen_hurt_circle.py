"""scen_hurt_circle.py - critic: Circle pressed one tick after the hero was hit by his own explosion (state 49): a second bomb is born in hand INSIDE the active box of the first and is detonated by D30."""
from scen_lib import *
m, STATS = mk(0x23)
def hold(t):
    if t == 1: return W.CIRCLE
    if t == 262: return W.CIRCLE
    return 0
log = run(m, 300, hold)
for r in log:
    if 258 <= r['t'] <= 290:
        print('t%3d hero tgt %2d hp %2d item %x link %x sounds %s ents %s' % (r['t'], r['hero']['tanim'], r['hero']['hp'], r['item'], r['link'], [x[2] for x in r['jr'] if x[1]=='sound'], {i:(e['sp'],e['st'],e['fuse'],e['tanim']) for i,e in r['ents'].items()}))
