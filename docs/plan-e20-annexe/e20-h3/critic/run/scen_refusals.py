from scen_lib import *
m, STATS = mk(0x23)
def hold(t):
    if t in (1, 10, 60, 61): return W.CIRCLE if t != 61 else 0
    if t == 62: return W.CIRCLE
    if t == 80: return W.TRIANGLE
    return 0
log = run(m, 100, hold)
for r in log:
    snd = [x[2] for x in r['jr'] if x[1] == 'sound']
    if r['t'] in (1, 10, 60, 62, 80) or snd:
        print('t%3d hold %02x sounds %s bombs %s item %x link %x hero tanim %d' % (r['t'], r['hold'] & 0xFF, snd, [(i, e['st'], e['fuse']) for i, e in r['ents'].items()], r['item'], r['link'], r['hero']['tanim']))
