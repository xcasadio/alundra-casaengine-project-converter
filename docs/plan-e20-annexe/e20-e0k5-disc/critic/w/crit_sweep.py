"""pb_sweep.py - point-blank sweep on the f4f6 oracle (copy in t2-proj/f4f6bin, run through runpy_cap.py, never in place).

For each projectile (hunter arrow, William arrow, charged arrow, fire/ice ball, normal and charged) and for the hero standing at x0 = 100..119 facing east with a wall
column 5 (x >= 120) ahead: the binary's position at the end of the birth tick S, the step the projectile asks at S (ForceX at S), the position an unchecked step would give
(x0 + step), the tick (relative to S) of the first ForceAdjusted (+0x13C) and of the first status 3 (Deactivated), and the position when status 3 begins.
Output: t2-proj/out/pb_sweep_east.txt
"""
import os
import sys

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'f4f6bin')
sys.path.insert(0, HERE)
import fw
import world as W

SQ = W.SQUARE
HOLD95 = {t: SQ for t in range(1, 96)}
OUTDIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out_sweep')
os.makedirs(OUTDIR, exist_ok=True)

# name, item, charged, expected types, n ticks, birth tick lower bound
CONFIGS = [
    ('hunter arrow (type 3)', 5, False, {3}, 60),
    ('William normal arrow (type 4)', 6, False, {4}, 60),
    ('William charged arrow (type 7)', 6, True, {7}, 150),
    ('fire ball normal (type 11)', 16, False, {11}, 60),
    ('fire ball charged (type 12)', 16, True, {12}, 150),
    ('ice ball normal (type 14)', 14, False, {14}, 60),
    ('ice ball charged (type 15)', 14, True, {15}, 150),
]


def one(item, charged, types, n, x0, y0=100, direction=24, cells=None):
    holds = dict(HOLD95) if charged else {1: SQ}
    res = fw.run(dict(item=item, n=n, holds=holds, after=0, hero_px=(x0, y0), direction=direction, cells=cells, targets=[], seed=0x12345678))
    S = slot = None
    rows = res['rows']
    for r in rows:
        if charged and r['tick'] < 96:
            continue
        for s, d in r['slots'].items():
            if s != 0 and d['type68'] in types:
                S, slot = r['tick'], s
                break
        if S:
            break
    if S is None:
        return None
    info = dict(S=S, slot=slot)
    adj = st3 = None
    for r in rows:
        if r['tick'] < S or slot not in r['slots']:
            continue
        d = r['slots'][slot]
        if r['tick'] == S:
            info['xS'] = d['px'] / 65536
            info['yS'] = d['py'] / 65536
            info['fxS'] = d['fx'] / 65536
            info['fyS'] = d['fy'] / 65536
            info['adjS'] = d['adj_13C']
        if adj is None and d['adj_13C']:
            adj = r['tick'] - S
            info['x_at_adj'] = d['px'] / 65536
            info['y_at_adj'] = d['py'] / 65536
        if st3 is None and d['status'] == 3:
            st3 = r['tick'] - S
            info['x_at_st3'] = d['px'] / 65536
            info['y_at_st3'] = d['py'] / 65536
    info['adj'] = adj
    info['st3'] = st3
    return info



def main():
    lines=[]
    for col,xs in ((5,(104,106,107,108,109)),(6,(128,130,131,132,133))):
        for name, item, charged, types, n in CONFIGS:
            lines.append('== %s wall col %d'%(name,col))
            for x0 in xs:
                i = one(item, charged, types, n, x0, cells=fw.wall_col(col))
                if i is None:
                    lines.append('  %3d none'%x0); continue
                lines.append('  x0=%d S=%d fxS=%d xS_raw=%d adj@%s st3@%s'%(x0,i['S'],int(i['fxS']*65536),int(round(i['xS']*65536)),i['adj'],i['st3']))
    open(os.path.join(OUTDIR,'crit_sweep.txt'),'w').write('\n'.join(lines)+'\n')
    print('\n'.join(lines))
main()
