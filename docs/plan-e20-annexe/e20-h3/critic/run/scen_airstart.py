from scen_lib import *
for item in (0x23, 0x1F):
    m, STATS = mk(item)
    def hold(t):
        if t == 1: return W.CROSS
        if t == 12: return W.CIRCLE
        return 0
    log = run(m, 90, hold)
    prev = None
    print('=== item %x: jump at t1, Circle at t12' % item)
    for r in log:
        h = r['hero']
        key = (h['tanim'], h['gnd'], r['link'], r['item'])
        if key != prev or r['jr']:
            print(' t%2d hero tanim %2d canim %2d gnd %d z %.1f link %x item %x | ents %s %s' % (r['t'], h['tanim'], h['canim'], h['gnd'], h['z'], r['link'], r['item'],
                  {i: (e['st'], e['tanim'], round(e['z'], 1)) for i, e in r['ents'].items()}, [(x[1], x[2]) for x in r['jr'] if x[1] == 'sound']))
        prev = key
        if r['t'] > 60: break
