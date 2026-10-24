import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W

def show(res, slots=None, cols=None, first=1, last=None):
    cols = cols or ['status', 'type68', 'anim_t', 'anim_c', 'dir_t', 'fx', 'fy', 'fz', 'px', 'py', 'pz', 'adj_13C', 'ground_144', 'slope_18C', 'cnt1c', 'hitc', 'reset_AC', 'flags6C']
    for r in res['rows']:
        if r['tick'] < first or (last and r['tick'] > last):
            continue
        sl = slots or [s for s in r['slots'] if s != 0]
        line = []
        for s in sl:
            d = r['slots'].get(s)
            if d is None:
                line.append('slot%d: -' % s)
                continue
            parts = []
            for c in cols:
                v = d[c]
                if c in ('px', 'py', 'pz'):
                    v = '%.4f' % (v / 65536)
                elif c == 'flags6C':
                    v = '%X' % v
                parts.append('%s=%s' % (c, v))
            line.append('slot%d: %s' % (s, ' '.join(parts)))
        print('t%03d hold=%X rec=%X/%d | %s | %s' % (r['tick'], r['hold'], r['rec_ptr'], r['rec_cnt'], ' || '.join(line), ' '.join(r['journal'])))

if __name__ == '__main__':
    spec = dict(item=5, n=40, holds={1: W.SQUARE}, after=0)
    res = P.run(spec)
    print(res['meta'])
    show(res)
    m = res['m']
    print('uninit reads', len(m.uninit_reads))
