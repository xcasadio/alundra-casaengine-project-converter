import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W

def lcg(s): return (s * 0x7D2B89DD + 0xE06A02E7) & 0xFFFFFFFF
def steps(a, b, mx=60):
    s = a
    for k in range(mx + 1):
        if s == b: return k
        s = lcg(s)
    return None

def rng_report(name, item, n, holds, **kw):
    res = P.run(dict(item=item, n=n, holds=holds, after=0, **kw))
    prev = res['rng0']
    out = []
    for r in res['rows']:
        if r['rng'] != prev:
            out.append((r['tick'], steps(prev, r['rng'])))
        prev = r['rng']
    print(name, 'rng draws per tick (tick, steps):', out)

if __name__ == '__main__':
    rng_report('hunter arrow', 5, 30, {1: W.SQUARE})
    rng_report('fire rod', 16, 70, {1: W.SQUARE})
    rng_report('ice rod', 14, 70, {1: W.SQUARE})
    rng_report('steel charged (hold to 95)', 2, 150, {t: W.SQUARE for t in range(1, 96)})
    rng_report('william charged (hold to 95)', 6, 130, {t: W.SQUARE for t in range(1, 96)})
