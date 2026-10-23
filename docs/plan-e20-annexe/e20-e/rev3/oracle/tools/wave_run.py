"""wave_run.py - bin agent of the E20.f3 discovery.  Sword wave (global sprites 27-29, C61 / E10) released on level ground, per-tick table.

Real code of ALUN_CD.EXE (France) on the E20.c1 / f-projectiles machine (pworld.py, unchanged copy).  Nothing here knows an expected value.
usage: python -B wave_run.py <name>   (names below)
"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C

OUT = os.path.join(HERE, '..', 'out')
DIRS = {'east': 24, 'north': 16, 'west': 8, 'south': 0}
SWORD = {'steel': 2, 'demon': 3, 'holy': 4}
RELEASE = 96                      # Square held t1..t95, released t96 (the earliest release is S+D+60 = 1+21+60 = 82; 96 is the f-projectiles choice)


def px(v):
    return v / 65536.0


def row_fmt(r, s):
    d = r['slots'].get(s)
    if d is None:
        return 't%03d slot%d absent' % (r['tick'], s)
    return ('t%03d st=%d anim_t=%d dir_t=%d dir_c=%d adir=%d pos=(%.5f,%.5f,rawZ=%d) force=(%d,%d,%d) hitbox=(%.3f,%.3f,%.3f) size=(%d,%d,%d) '
            'c1c=%d rstAC=%d fl=%X atk=%s/%s adj=%d gnd=%d terr=%d hitc=%d c1d0=%d box=%d') % (
        r['tick'], d['status'], d['anim_t'], d['dir_t'], d['dir_c'], d['adir'], px(d['px']), px(d['py']), d['pz'], d['fx'], d['fy'], d['fz'],
        px(d['hbx']), px(d['hby']), px(d['hbz']), d['bw'], d['bd'], d['bh'], d['cnt1c'], d['reset_AC'], d['flags6C'], d['atk_attr'], d['atk_pow'],
        d['adj_13C'], d['ground_144'], d['terr_138'], d['hitc'], d['c1d0'], 1 if d['box1d4'] else 0)


def run_wave(name, sword='steel', direction='east', n=160, targets=(), cells=None, hero_px=(100, 100), interp=False, extra_holds=None, init=None, tall=False):
    item = SWORD[sword]
    holds = {t: W.SQUARE for t in range(1, RELEASE)}
    if extra_holds:
        holds.update(extra_holds)
    spec = dict(item=item, n=n, holds=holds, after=0, hero_px=hero_px, direction=DIRS[direction], cells=cells, targets=list(targets))
    if init:
        spec['init'] = init
    if interp:
        import interp2
        spec['ticker'] = interp2.tick2
    if tall:
        C.map14().record['Header']['Ubuff'][13] = 32      # target body SizeZ 32 (f-projectiles' patch); default is the real SizeZ 8
    res = P.run(spec)
    return res


def dump(res, name, slots, first=0, last=10 ** 9, journal=True):
    lines = ['# %s' % name, '# spec: ' + json.dumps({k: (v if not callable(v) else 'fn') for k, v in res['spec'].items() if k not in ('holds', 'init', 'ticker', 'cells')}, default=str)]
    prev_hero = None
    for r in res['rows']:
        if r['tick'] < first or r['tick'] > last:
            continue
        for s in slots:
            lines.append(row_fmt(r, s))
        j = [x for x in r['journal'] if not x.startswith('stubC')]
        if journal and j:
            lines.append('      journal: ' + ' '.join(j))
        lines.append('      rng=%08X' % (r['rng'] & 0xFFFFFFFF))
    return '\n'.join(lines)


def hero_summary(res):
    out = []
    last = None
    for r in res['rows']:
        h = r['slots'][0]
        k = (h['anim_t'], h['anim_c'], h['adir'], h['dir_t'])
        if k != last:
            out.append('t%03d hero anim_t=%d anim_c=%d adir(+0x98)=%d dir_t(+0x8C)=%d rec=%X cnt=%d charge=%d' % (r['tick'], h['anim_t'], h['anim_c'], h['adir'], h['dir_t'], r['rec_ptr'], r['rec_cnt'], r['charge']))
            last = k
    return '\n'.join(out)


if __name__ == '__main__':
    which = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    if which == 'dirs':
        for sword in ('steel', 'demon', 'holy'):
            for d in ('east', 'north', 'west', 'south'):
                if sword != 'steel' and d != 'east':
                    continue
                res = run_wave('wave_%s_%s' % (sword, d), sword=sword, direction=d, n=150)
                txt = hero_summary(res) + '\n' + dump(res, 'wave %s %s level ground, hero (100,100)' % (sword, d), [1], first=114, last=146)
                open(os.path.join(OUT, 'wave_%s_%s.txt' % (sword, d)), 'w').write(txt + '\n')
                print('wrote', sword, d)
