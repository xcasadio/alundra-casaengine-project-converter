"""c1_pworld.py (h4c1 discovery, own script on a COPY of the e20-f4f6 harness): the earth-1 tome cast (item 0x2B) on the pworld machine, i.e. with the slot index word (+0) of the 64 entities
written as the world init 0x8003B24C does (the e20-h4 harness leaves them 0), E3 (0x8007EDA0) REAL and the global sprite 16 installed.  Nothing here knows an expected value.
usage: python -B c1_pworld.py [--n N] [--hero X Y] [--dir D] [--targets X,Y ...] [--kind K] [--json OUT]"""
import argparse
import json
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fw
sys.path.insert(0, os.path.join(HERE, 'src'))
import pworld as P
import world as W

E3 = 0x8007EDA0


def init_tome(item, mp=4, mpmax=4):
    def f(m):
        STATS = m.peek(0x80176318)
        m.call(0x8004E530, item)
        m.poke16(STATS + 6, mpmax)
        m.poke16(STATS + 4, mp)
        m.call(0x8004E484, 1)
        m.poke16(STATS + 0xC, item)
        m.uninit_reads.clear()
    return f


def cast(item=0x2B, n=140, hero=(100, 100), direction=24, targets=(), target_kind=None, cells=None, seed=0x12345678, extra_init=(), keep=None, circle_ticks=(1,), types=None):
    keep = set(P.REAL) | {E3} if keep is None else keep
    types = tuple(P.PROJECTILE_TYPES) + (16,) if types is None else types
    spec = dict(item=None, n=n, holds={t: W.CIRCLE for t in circle_ticks}, hero_px=hero, direction=direction, cells=cells, targets=list(targets), seed=seed,
                types=types, keep=keep, target_kind=target_kind, init=[init_tome(item)] + list(extra_init))
    return fw.run(spec)


def rocks(res, hero=(100, 100)):
    births = {}
    for r in res['rows']:
        for s, d in r['slots'].items():
            if d['type68'] == 16:
                key = (s, d['id'] if 'id' in d else None)
                b = births.setdefault((s, r['tick'] if (s not in births or births[s]['dead']) else 0), None)
    return births


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--n', type=int, default=140)
    ap.add_argument('--hero', type=int, nargs=2, default=[100, 100])
    ap.add_argument('--dir', type=int, default=24)
    ap.add_argument('--item', type=lambda s: int(s, 0), default=0x2B)
    ap.add_argument('--json')
    a = ap.parse_args()
    res = cast(item=a.item, n=a.n, hero=tuple(a.hero), direction=a.dir)
    m = res['m']
    rows = res['rows']
    hero_rows = [(r['tick'], r['slots'].get(0, {}).get('anim_t'), r['slots'].get(0, {}).get('status')) for r in rows]
    first16 = [(r['tick'], [s for s, d in r['slots'].items() if d['type68'] == 16]) for r in rows if any(d['type68'] == 16 for d in r['slots'].values())]
    print('ticks with sprite 16 alive:', first16[0][0], '..', first16[-1][0])
    print('sounds:', [(r['tick'], j) for r in rows for j in r['journal'] if j.startswith('snd')][:6], '...')
    if a.json:
        json.dump(dict(rows=rows), open(a.json, 'w'), default=str)
