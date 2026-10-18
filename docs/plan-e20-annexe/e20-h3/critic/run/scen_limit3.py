"""scen_limit3.py - critic: try to chain bombs: Circle, wait, Square (throw), repeat every 70 ticks; log which Circle is accepted (spawn sound 44) or refused (sound 3)."""
from scen_lib import *
m, STATS = mk(0x23)
CIRCLES = [1, 71, 141, 211, 281, 351, 421, 491]
def hold(t):
    h = 0
    if t in CIRCLES: h |= W.CIRCLE
    if (t - 42) in CIRCLES: h |= W.SQUARE        # throw 42 ticks after the start
    return h
log = run(m, 640, hold)
for r in log:
    t = r['t']
    snds = [x[2] for x in r['jr'] if x[1] == 'sound'] if False else [x[2] for x in r['jr'] if x[1] == 'sound']
    if t in CIRCLES:
        pass
    ev = []
    for x in r['jr']:
        if x[1] == 'sound' and x[2] in (3, 44, 46):
            ev.append(x[2])
    bombs = [(i, e['st'], e['fuse']) for i, e in r['ents'].items() if e['sp'] == 2]
    if ev or t in CIRCLES or (t - 42) in CIRCLES:
        print('t%3d' % t, 'hold %02x' % (r['hold'] & 0xFF), 'sounds', ev, 'bombs(slot,status,fuse)', bombs, 'hero tanim', r['hero']['tanim'], 'item %x' % r['item'])
