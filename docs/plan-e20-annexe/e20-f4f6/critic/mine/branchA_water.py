import sys, os
BIN = os.path.abspath('../bin'); sys.path.insert(0, BIN)
import fw, world as W
SQ = W.SQUARE
def run(slope, tick_abs):
    def f(m, t):
        # after tick `tick_abs`, force +0x18C of slot 1 (ball) = slope, as the end pass of a tick spent over water would leave it
        if t == tick_abs:
            m.poke(W.ENT0 + W.STRIDE * 1 + 0x18C, slope)
    res = fw.run(dict(item=16, n=40, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[], after_tick=[f]))
    return res
for slope in (4, 1, 2):
    res = run(slope, 27)       # S = 12 ; tick 28 = S+16 (counter 16, multiple of 8, explosion running since S+11)
    for r in res['rows']:
        if r['tick'] in (28, 29):
            s = r['slots'].get(1)
            print('slope', slope, 'tick', r['tick'], '(S+%d)' % (r['tick'] - 12), 'status', s['status'] if s else None, 'draws', r['draws'], [e[:60] for e in r['effects']])
