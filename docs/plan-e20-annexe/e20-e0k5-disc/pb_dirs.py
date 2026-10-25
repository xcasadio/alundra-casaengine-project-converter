"""pb_dirs.py - point-blank shot on the four sides: the hero walks into a wall until he stops (his flush position), then shoots (hunter arrow, fire ball, charged arrow) from the flush
position and from 2 px, 4 px and 6 px away from it. West wall = column 3 (x < 96 is col 0..3: wall col 3 = x 72..95, hero comes from the east), north wall = row 3 (y 48..63),
south wall = row 8 (y 128..143)."""
import os
import sys

HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'f4f6bin')
sys.path.insert(0, HERE)
import fw
import world as W

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
os.makedirs(OUT, exist_ok=True)
SQ = W.SQUARE
HOLD95 = {t: SQ for t in range(1, 96)}


def row_wall(r, wk=0x40, hb=0):
    def cells(col, row):
        return (hb, 0, wk, 0) if row == r else (0, 0, 0, 0)
    return cells


SIDES = {
    # name: (direction code, pad bit, cells, flush-search start, axis, sign of approach, hero start offsets list builder)
    'east': (24, W.RIGHT, fw.wall_col(5), (100, 100), 'x', +1),
    'west': (8, W.LEFT, fw.wall_col(2), (130, 100), 'x', -1),
    'north': (16, W.UP, row_wall(3), (100, 100), 'y', -1),
    'south': (0, W.DOWN, row_wall(8), (100, 100), 'y', +1),
}
SHOTS = [('hunter arrow (3)', 5, False, {3}, 60), ('William charged arrow (7)', 6, True, {7}, 150), ('fire ball normal (11)', 16, False, {11}, 60), ('ice ball normal (14)', 14, False, {14}, 60)]

lines = []
ONLY = os.environ.get('PB_ONLY')
for side, (direction, padbit, cells, start, axis, sign) in SIDES.items():
    if ONLY and side != ONLY:
        continue
    res = fw.run(dict(item=None, n=80, holds={t: padbit for t in range(1, 81)}, after=0, hero_px=start, direction=direction, cells=cells, targets=[], seed=0x12345678))
    last = res['rows'][-1]['slots'][0]
    flush = (last['px'] / 65536, last['py'] / 65536)
    lines.append('== side %s: hero walking %s stops at (%.3f, %.3f)' % (side, side, flush[0], flush[1]))
    for name, item, charged, types, n in SHOTS:
        for back in (0, 2, 4, 6):
            hx = flush[0] - (back * sign if axis == 'x' else 0)
            hy = flush[1] - (back * sign if axis == 'y' else 0)
            holds = dict(HOLD95) if charged else {1: SQ}
            r2 = fw.run(dict(item=item, n=n, holds=holds, after=0, hero_px=(int(hx), int(hy)), direction=direction, cells=cells, targets=[], seed=0x12345678))
            S = slot = None
            for r in r2['rows']:
                if charged and r['tick'] < 96:
                    continue
                for s, d in r['slots'].items():
                    if s != 0 and d['type68'] in types:
                        S, slot = r['tick'], s
                        break
                if S:
                    break
            if S is None:
                lines.append('  %-28s back=%d: no projectile' % (name, back))
                continue
            adj = st3 = None
            pos0 = None
            for r in r2['rows']:
                if r['tick'] < S or slot not in r['slots']:
                    continue
                d = r['slots'][slot]
                if r['tick'] == S:
                    pos0 = (d['px'] / 65536, d['py'] / 65536, d['fx'] / 65536, d['fy'] / 65536)
                if adj is None and d['adj_13C']:
                    adj = r['tick'] - S
                if st3 is None and d['status'] == 3:
                    st3 = r['tick'] - S
            lines.append('  %-28s back=%d hero=(%d,%d): end of S = (%.3f, %.3f) force=(%.2f, %.2f)  ForceAdjusted at S+%s, status 3 at S+%s' % (name, back, hx, hy, pos0[0], pos0[1], pos0[2], pos0[3], adj, st3))
    lines.append('')
open(os.path.join(OUT, 'pb_dirs_%s.txt' % (ONLY or 'all')), 'w').write('\n'.join(lines) + '\n')
print('\n'.join(lines))
