"""cmp_endframe.py (h4c1 T0): the rows of the probe sampled at the END OF THE FRAME (after the validation of the animation switches, FrameSyncPasses) against the keys: row (AnimationDirection) and CurrentAnimationId.
usage: python -B cmp_endframe.py KEYDIR PROBEDIR"""
import json, os, sys
from collections import Counter
KEYDIR, OUT = sys.argv[1], sys.argv[2]
COLS = 'tick slot status px py pz fx fy fz adir dir anim_t anim_c ac flags adj ground slope c1d0 raw_w raw_d raw_h ev_w ev_d ev_h type'.split()
tot = Counter()
for name in ['earth1_flat_east', 'earth1_flat_south', 'earth1_flat_west', 'earth1_flat_north', 'earth1_flat_east_x200', 'earth1_deepwater_cols7_9']:
    key = json.load(open(os.path.join(KEYDIR, name + '.json')))
    kb = {(r['tick'], int(s)): e for r in key['rows'] for s, e in r['rocks'].items()}
    pb = {}
    for line in open(os.path.join(OUT, name + '.endframe.csv')):
        v = line.strip().split(',')
        if len(v) == len(COLS):
            d = dict(zip(COLS, [int(x) for x in v]))
            pb[(d['tick'], d['slot'])] = d
    diff = Counter()
    for k in set(pb) & set(kb):
        p, q = pb[k], kb[k]
        for a, b in (('adir', 'adir'), ('anim_c', 'anim_c'), ('anim_t', 'anim_t'), ('status', 'status'), ('px', 'px'), ('ac', 'reset_AC')):
            if p[a] != q[b]:
                diff[a] += 1
        if p['pz'] != q['pz'] - 1:
            diff['pz-1'] += 1
    print(name, 'pairs', len(set(pb) & set(kb)), 'only probe', len(set(pb) - set(kb)), 'only key', len(set(kb) - set(pb)), 'differences', dict(diff) or 'none')
    tot.update(diff)
print('total', dict(tot) or 'none')
