"""targets_pf.py - h4c2 discovery: trace_targets (a cast with real targets) with the map-load slot initialisation and, optionally, the per-frame step of the generator.
usage: python -B targets_pf.py ITEM [--slotinit] [--pf] [--inv N] [--targets "X Y X Y ..."] [--out FILE]"""
import sys
import json
import argparse
import spellworld as S
import trace_targets as TT
import world as W
from machine import M32
from collections import Counter

RNG = 0x80098708
MUL, INC = 0x7D2B89DD, 0xE06A02E7

ap = argparse.ArgumentParser()
ap.add_argument('item', type=lambda s: int(s, 0))
ap.add_argument('--slotinit', action='store_true')
ap.add_argument('--pf', action='store_true')
ap.add_argument('--inv', type=int, default=0)
ap.add_argument('--hp', type=int, default=5000)
ap.add_argument('--ticks', type=int, default=400)
ap.add_argument('--targets', default='140 100 100 140 180 100 100 60 60 100 140 140')
ap.add_argument('--out')
a = ap.parse_args()

orig_make = S.make


def make(*args, **kw):
    m, STATS, placed = orig_make(*args, **kw)
    if a.slotinit:
        for i in range(64):
            m.poke(S.ENT0 + S.STRIDE * i, i)
    return m, STATS, placed


S.make = make
orig_tick = W.tick
rng_log = []


def tick(m, pad, hold, skip=(), check_pad=True):
    rng0 = m.peek(RNG) & M32
    orig_tick(m, pad, hold, skip=skip, check_pad=check_pad)
    rng1 = m.peek(RNG) & M32
    if a.pf:
        m.poke(RNG, (rng1 * MUL + INC) & M32)
    rng_log.append((rng0, rng1))


W.tick = tick
xy = [int(v) for v in a.targets.split()]
targets = list(zip(xy[0::2], xy[1::2]))
m, res, hp0, STATS = TT.run(a.item, targets, inv=a.inv, hp=a.hp, ticks=a.ticks)
out = []
for p, r in res.items():
    tot = sum(h[2] for h in r['hits'])
    c = Counter((h[1], h[2]) for h in r['hits'])
    first = r['hits'][0][0] if r['hits'] else None
    print('target (%d,%d): hits %d dmg %d first tick %s by %s' % (r['x'], r['y'], len(r['hits']), tot, first, dict(c)))
    out.append(dict(x=r['x'], y=r['y'], hits=r['hits'], hp=r['hp']))
if a.out:
    json.dump(dict(item=a.item, inv=a.inv, pf=a.pf, slotinit=a.slotinit, targets=out, rng=rng_log), open(a.out, 'w'))
