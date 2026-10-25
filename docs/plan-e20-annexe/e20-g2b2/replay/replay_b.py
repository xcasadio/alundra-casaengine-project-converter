"""g2b2 T0 (b): the binary replay of the thrown objects over a hole / an exit / a full cell / a partial corner.
Reuses c4_scen.throw_run (REAL MovePlayer, REAL spawner, REAL C4/C23/E2, REAL physics; harness writes the pad, the cell field, the placement).
Run (next to work/): python -B -X utf8 replay_b.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'g2c1'))
import c4_scen as S, gl, ana
gl.CFIELDS.append(('or180', 0x180, 0))
S.OBJ['caisse36'] = ('map_392', 36, ())
S.OBJ['caisse35'] = ('map_383', 35, ())
S.OBJ['jarre41'] = ('map_70', 41, ())

CASES = {
    'hole': lambda cc, rr: dict(wk=0x04) if 6 <= cc <= 9 else None,
    'exit': lambda cc, rr: dict(gp=0x80) if 6 <= cc <= 9 else None,
    'full': None,
    'corner': lambda cc, rr: dict(wk=0x04) if (cc, rr) == (7, 5) else None,
    'cornerexit': lambda cc, rr: dict(gp=0x80) if (cc, rr) == (7, 5) else None,
}
WHO = {'C4': ['tonneau34', 'etoile107'], 'C23': ['caisse36', 'jarre41', 'caisse35']}


def run(name, case):
    m, c, rows = S.throw_run(name, cells=CASES[case], throw_hold=gl.RIGHT, n=130)
    chg = ana.changes(rows, c, ['status', 'anim_t', 'flags6C', 'or180', 'p184', 'slope18C', 'dir_t'], first=60)
    fx = [(r['tick'], x[:60]) for r in rows for x in r['journal'] if x.startswith('fx') and r['tick'] > 60]
    snd = [(r['tick'], x) for r in rows for x in r['journal'] if x.startswith('snd') and r['tick'] > 60 and x != 'snd(0)']
    dead = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 0, first=62)
    last = rows[-1]['c'][c]
    ticks = ['  t%d st=%d anim=%d flags=%X or180=%X and184=%X z=%.1f%s' % (r['tick'], r['c'][c]['status'], r['c'][c]['anim_t'], r['c'][c]['flags6C'], r['c'][c]['or180'], r['c'][c]['p184'], r['c'][c]['pz'] / 65536, (' ' + ';'.join(x[:40] for x in r['journal'] if x.startswith('fx'))) if any(x.startswith('fx') for x in r['journal']) else '') for r in rows if 81 <= r['tick'] <= 89]
    return dict(ticks=ticks, name=name, case=case, dead=dead, fx=fx[:3], snd=snd[:6], changes=chg, final=(last['status'], last['anim_t'], round(last['px'] / 65536, 1)))


if __name__ == '__main__':
    only = sys.argv[1:]
    for name in [n for g in WHO.values() for n in g]:
        if only and name not in only:
            continue
        for case in CASES:
            r = run(name, case)
            print('%-10s %-10s dead t%s fx %s snd %s final %s' % (r['name'], r['case'], r['dead'], r['fx'], r['snd'], r['final']))
            print('    changes:', ' | '.join(r['changes']))
            print(chr(10).join(r['ticks']))
