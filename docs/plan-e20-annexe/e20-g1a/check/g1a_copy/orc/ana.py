"""ana.py - compact summaries of gl.run rows (change points, apex, landing)"""
import gl


def changes(rows, who, fields, first=1, last=10 ** 9):
    """who = 'hero' or crate slot; returns list of 'tNNN field a->b' for every change of the given fields"""
    out = []
    prev = None
    for r in rows:
        d = r['hero'] if who == 'hero' else r['c'].get(who)
        if d is None:
            continue
        cur = tuple(d[f] for f in fields)
        if prev is not None and cur != prev and first <= r['tick'] <= last:
            out.append('t%d %s' % (r['tick'], ' '.join('%s:%s>%s' % (f, gl.fmt(f, a), gl.fmt(f, b)) for f, a, b in zip(fields, prev, cur) if a != b)))
        prev = cur
    return out


def series(rows, who, field, first=1, last=10 ** 9, scale=65536.0, nd=2):
    return [round((r['hero'] if who == 'hero' else r['c'][who])[field] / scale, nd) for r in rows if first <= r['tick'] <= last]


def apex(rows, who, field='pz', first=1, last=10 ** 9):
    best = None
    for r in rows:
        if first <= r['tick'] <= last:
            v = (r['hero'] if who == 'hero' else r['c'][who])[field]
            if best is None or v > best[1]:
                best = (r['tick'], v)
    return best[0], best[1] / 65536.0


def first_tick(rows, pred, first=1):
    for r in rows:
        if r['tick'] >= first and pred(r):
            return r['tick']
    return None
