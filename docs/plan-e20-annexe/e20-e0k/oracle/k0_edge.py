"""k0_edge.py - E20.e0k3 T0: a walking entity pushed continuously against the east edge of the map (the clamp of the physics pass 0x800366FC): the bound is +0xFC = 0x4E00000 - ((offX + sizeX) << 16)
for X (and 0x3C00000 - (...) for Y; low bound +0x108 = -(offX << 16)).  Same entity as k0_walker.py: the Beannoid of map 14 (sprite 79, ClassA, Gravity, 20 x 14 x 8, no rebound handler: the handlers are inert
stubs), open field (no cells), animation 7 (walk) with direction chosen by the harness at tick 1.
Keys: edge-push-east (direction 24, 1.875 px per tick, from x = 1225), edge-push-diag (direction 20 = north-east: X pushed against the east bound while Y keeps progressing toward the north),
edge-exact (the walk speed overridden to 256 = exactly 3 px per tick, 196 608 raw, started 3 px short of the bound plus nothing: the step of tick 1 arrives EXACTLY on the bound: neither clamped nor flagged, tick 2 is
over the bound by one step: clamped and flagged).  Per tick: px, py and adj_13C (ForceAdjusted, +0x13C).
Run: python -B -I k0_edge.py   (writes out/edge-*.txt and out/edge-*.json, prints the summary)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
import k0_walker as WK
from k0_arrows import OUT

COLS = ['status', 'anim_t', 'anim_c', 'fx_C4', 'fnx_E4', 'px', 'py', 'pz', 'ground_144', 'adj_13C']
TICKS = 14
BOUND_X = 0x4E00000   # minus ((offX + sizeX) << 16) of the Beannoid, read below from the record


def bound_x_raw(m, s):
    e = K.ent(s)
    return m.peeks(e + 0xFC)


if __name__ == '__main__':
    lines = []
    cases = (
        ('edge-push-east', dict(direction=24, pos=(1225, 100)), 'Beannoid (anim 7, 1.875 px per tick) pushed east from x = 1225 against the east edge'),
        ('edge-push-diag', dict(direction=20, pos=(1225, 100)), 'Beannoid (anim 7) pushed north-east (direction 20) from (1225, 100) against the east edge'),
        ('edge-exact', dict(direction=24, pos=(1225, 100), speed_override={7: 256}), 'Beannoid (anim 7, speed 256 = exactly 3 px per tick) pushed east, start chosen below'),
    )
    for name, kw, title in cases:
        kw = dict(kw)
        if name == 'edge-exact':
            # find the bound with a throwaway build of the world, then start exactly one step (3 px) short of it
            m0, s0, init0, rows0 = WK.walker(cells=None, anim=7, n=1, **dict(kw))
            b = bound_x_raw(m0, s0)
            kw['pos'] = ((b >> 16) - 3, 100)
            assert b & 0xFFFF == 0, b
        m, s, init, rows = WK.walker(cells=None, anim=7, n=TICKS, **dict(kw))
        m2, s2, init2, rows2 = WK.walker(cells=None, anim=7, n=TICKS, ticker=interp2.tick2, **dict(kw))
        d = K.same_rows(rows, rows2)
        e = K.ent(s)
        bx, bx_lo, by, by_lo = (m.peeks(e + o) for o in (0xFC, 0x108, 0x100, 0x10C))
        txt = K.table(rows, s, COLS, 1, TICKS, 1, title='== %s: %s; bounds raw: X [%d ; %d] Y [%d ; %d]; interp diff %s\nstart state: %s' % (
            name, title, bx_lo, bx, by_lo, by, d, ' '.join('%s=%s' % (c, K.fmt(c, init[c])) for c in COLS if c in init)))
        open(os.path.join(OUT, name + '.txt'), 'w').write(txt + '\n')
        K.dump_json(os.path.join(OUT, name + '.json'), rows, dict(name=name, slot=s, init=init, bound_x_high=bx, bound_x_low=bx_lo, bound_y_high=by, bound_y_low=by_lo, **{k: v for k, v in kw.items() if k != 'speed_override'}))
        lines.append('%s: bounds raw X [%d ; %d] Y [%d ; %d] (X high = %.4f px); interp diff %s' % (name, bx_lo, bx, by_lo, by, bx / 65536, d))
        prev = init
        for r in rows:
            c = r['slots'][s]
            lines.append('  tick %2d: px %d (%.4f) py %d (%.4f) dx %d adj_13C %d' % (r['tick'], c['px'], c['px'] / 65536, c['py'], c['py'] / 65536, c['px'] - prev['px'], c['adj_13C']))
            prev = c
    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'edge-summary.txt'), 'w').write(txt + '\n')
    print(txt)
