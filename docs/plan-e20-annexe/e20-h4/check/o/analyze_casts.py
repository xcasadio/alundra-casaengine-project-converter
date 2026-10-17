"""analyze_casts.py - per-entity table of one cast (births, impact, end, landing offset from the hero) from trace_cast.run()."""
import sys
import trace_cast as T


def table(item, ticks=800, seed=0x12345678, hero_dir=None, **kw):
    m, STATS, births, timeline, mp0, mp1 = T.run(item, mp=4, mpmax=4, ticks=ticks, seed=seed, hero_dir=hero_dir, **kw)
    hx, hy = 100 * 65536, 100 * 65536
    rows = []
    for k, b in sorted(births.items(), key=lambda kv: (kv[1]['birth'], kv[0])):
        tr = b['track']
        first = tr[0]
        t_imp = next((t[0] for t in tr if t[1] == 3), None)
        last = tr[-1]
        zmax = max(t[6] for t in tr)
        xs = [t[4] for t in tr]
        ys = [t[5] for t in tr]
        # tr tuple: (t, status, id, type, x, y, z, tanim, canim, forcez, hp, hitc, flags)
        rows.append(dict(type=b['type'], birth=b['birth'], impact=t_imp, last=b['last_seen'], dir=None,
                         x0=(first[4] - hx) // 65536, y0=(first[5] - hy) // 65536, z0=first[6] / 65536,
                         x1=(last[4] - hx) // 65536, y1=(last[5] - hy) // 65536, zmax=zmax / 65536,
                         xmin=(min(xs) - hx) // 65536, xmax=(max(xs) - hx) // 65536, ymin=(min(ys) - hy) // 65536, ymax=(max(ys) - hy) // 65536))
    return rows, m, timeline


if __name__ == '__main__':
    item = int(sys.argv[1], 0)
    rows, m, tl = table(item)
    for r in rows:
        print('type %2d birth %3d impact %s last %3d  start (%4d,%4d) z %.1f -> end (%4d,%4d) zmax %.1f  x[%d..%d] y[%d..%d]' % (
            r['type'], r['birth'], r['impact'], r['last'], r['x0'], r['y0'], r['z0'], r['x1'], r['y1'], r['zmax'], r['xmin'], r['xmax'], r['ymin'], r['ymax']))
