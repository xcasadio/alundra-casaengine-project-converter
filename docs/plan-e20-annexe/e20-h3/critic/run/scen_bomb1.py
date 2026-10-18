import sys
from scen_lib import *
m, STATS = mk(0x23)
log = run(m, 330, lambda t: W.CIRCLE if t == 1 else 0)
prev = None
for r in log:
    key = [(i, e['st'], e['tanim'], e['canim'], e['end']) for i, e in r['ents'].items()] + [r['hero']['tanim'], r['link'], r['item']]
    jr = r['jr']
    if key != prev or jr:
        print('t%3d' % r['t'], 'hero tanim', r['hero']['tanim'], 'link %x item %x' % (r['link'], r['item']),
              {i: (e['st'], e['tanim'], e['canim'], e['fuse'], round(e['z'], 2), e['end']) for i, e in r['ents'].items()}, jr)
    prev = key
