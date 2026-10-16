import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from summ import timeline
C.map14().record['Header']['Ubuff'][13] = 32
for name, item in (('fire', 16), ('ice', 14)):
    res = P.run(dict(item=item, n=64, holds={1: W.SQUARE}, after=0, targets=[(12, 12, 0)]))
    print('=====', name)
    for r in res['rows']:
        t = res['rows'][r['tick'] - 1]
        a = r['slots'].get(2)
        tg = r['slots'].get(1)
        ev = [j for j in r['journal'] if not j.startswith('stub')]
        if tg and (tg['c1d0'] in (25,) or tg['touch_slot'] is not None or any(e.startswith('fxattach') for e in ev)):
            print('t%d target c1d0=%d touch=%s | arrow anim=%s hitc=%s px=%.1f pz=%.2f | %s' % (r['tick'], tg['c1d0'], tg['touch_slot'], a and a['anim_t'], a and a['hitc'], a['px'] / 65536 if a else 0, a['pz'] / 65536 if a else 0, ' '.join(ev)))
    # hit ticks (c1d0 == 25 transitions)
    hits = [r['tick'] for r in res['rows'] if r['slots'].get(1) and r['slots'][1]['c1d0'] == 25]
    print('hit ticks (target +0x1D0 = 25):', hits)
