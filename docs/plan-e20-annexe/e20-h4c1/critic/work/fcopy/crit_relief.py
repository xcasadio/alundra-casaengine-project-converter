"""crit_relief.py (critic of h4c1, own script on a COPY of the other agent's harness): relief scenarios the report's key set does not have.
The earth-1 cast over (a) a gp=2 slope band, (b) a 16 px step, (c) a 32 px step, columns 7..9; prints for every rock whose landing cell is in the band the end of its life."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ['x', os.path.join(HERE, '_crit_out')]
exec(open(os.path.join(HERE, 'c1_make_keys.py')).read().split("scen = []")[0])


def life(res):
    live = {}
    done = []
    for r in res['rows']:
        t = r['tick']
        cur = {s: e for s, e in r['slots'].items() if s != 0 and e['type68'] == 16}
        for s, e in cur.items():
            if s in live and live[s]['rows'][-1]['status'] == 4:
                done.append(live.pop(s))
            if s not in live:
                live[s] = dict(slot=s, birth=t, rows=[], ticks=[])
            live[s]['rows'].append(e)
            live[s]['ticks'].append(t)
        for s in list(live):
            if s not in cur:
                done.append(live.pop(s))
    done += list(live.values())
    return sorted(done, key=lambda b: (b['birth'], b['slot']))


def report(name, cells):
    res, path = run(name, cells=cells)
    print('==', name)
    for b in life(res):
        rows, ticks = b['rows'], b['ticks']
        st3 = next((t for t, r in zip(ticks, rows) if r['status'] == 3), None)
        st4 = next((t for t, r in zip(ticks, rows) if r['status'] == 4), None)
        first = rows[0]
        last = rows[-1]
        moved_after_e3 = None
        if st3 is not None:
            r3 = next(r for t, r in zip(ticks, rows) if t == st3)
            moved_after_e3 = ((last['px'] - r3['px']) / 65536.0, (last['py'] - r3['py']) / 65536.0)
        slopes = sorted(set(r['slope_18C'] for r in rows))
        print('birth %3d slot %2d dir %2d  E3 %s st4 %s  final (%.1f,%.1f) pz_end %.2f slope18C %s  moved after E3 %s  life %d' % (
            b['birth'], b['slot'], first['dir_t'], st3, st4, last['px'] / 65536.0, last['py'] / 65536.0, last['pz'] / 65536.0, slopes, moved_after_e3, len(ticks)))
    return res


if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'all'
    report('crit_flat', None)
    report('crit_slope_gp2_cols7_9', cells_cols(7, 9, (0, 0, 0, 0x02)))
    report('crit_step16_cols7_9', cells_cols(7, 9, (1, 0, 0, 0)))
    report('crit_step32_cols7_9', cells_cols(7, 9, (2, 0, 0, 0)))
