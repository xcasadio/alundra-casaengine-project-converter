import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

COLS = ['status', 'anim_t', 'anim_c', 'dir_t', 'fx', 'fy', 'fz', 'px', 'py', 'pz', 'adj_13C', 'ground_144', 'slope_18C', 'hitc', 'touch_slot', 'reset_AC', 'flags6C', 'c1d0', 'c1cc', 'hp', 'atk_attr', 'atk_pow', 'box1d4']


def fmtv(c, v):
    if c in ('px', 'py', 'pz'):
        return '%.3f' % (v / 65536)
    if c == 'flags6C':
        return '%X' % v
    if c == 'box1d4':
        return '1' if v else '0'
    return str(v)


def timeline(res, slot, every=0, quiet=('px', 'py', 'pz', 'cnt1c', 'fx', 'fy', 'fz', 'b0', 'hbx', 'hby', 'hbz', 'bw', 'bd', 'bh', 'terr_138'), cols=COLS, maxrows=400):
    rows = res['rows']
    prev = None
    out = []
    for r in rows:
        d = r['slots'].get(slot)
        key = None if d is None else tuple((k, v) for k, v in d.items() if k not in quiet and k != 'atk1c8' and k != 'touch' and k != 'prog228' and k != 'parent')
        jj = [j for j in r['journal'] if not j.startswith('stubC')]
        show = (key != prev) or (every and r['tick'] % every == 0) or (bool(jj) and d is not None)
        if show:
            if d is None:
                out.append('t%03d slot%d absent  %s' % (r['tick'], slot, ' '.join(jj)))
            else:
                out.append('t%03d %s | %s' % (r['tick'], ' '.join('%s=%s' % (c, fmtv(c, d[c])) for c in cols), ' '.join(jj)))
        prev = key
    if len(out) > maxrows:
        out = out[:maxrows // 2] + ['...'] + out[-maxrows // 2:]
    return '\n'.join(out)


def traj(res, slot, first, last, cols=('px', 'py', 'pz', 'fx', 'fy', 'fz', 'cnt1c', 'b0')):
    out = []
    for r in res['rows']:
        if first <= r['tick'] <= last:
            d = r['slots'].get(slot)
            if d is None:
                out.append('t%03d absent' % r['tick'])
            else:
                out.append('t%03d %s' % (r['tick'], ' '.join('%s=%s' % (c, fmtv(c, d[c])) for c in cols)))
    return '\n'.join(out)
