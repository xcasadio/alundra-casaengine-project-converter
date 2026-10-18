"""fire2_targets.py - h4d1 discovery (read-only): the fire-2 cast against stationary targets, a COPY-based variant of trace_targets.py that (1) can add the frame-end generator step (--pf) and
(2) logs, tick by tick, the ticks at which the REAL hit pass writes a hit on each target (the target's +0x1D0 counter set to 0x19), the tick of its resolution, and the draws of the tick.
Targets are what trace_targets makes: the global sprite 25 (transparent block) given ClassA, the balance record 'all responses 0x10' of the harness, a large HP, no invulnerability.

usage: python -B fire2_targets.py [--pf] [--ticks N] X Y [X Y ...]   (px, hero at 100,100) ; --out FILE.json"""
import sys
import os
import json
import argparse
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4oracle'))
import spellworld as S
import world as W
from machine import s32, M32
import trace_targets as T

HERO = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
MULT, ADD = 0x7D2B89DD, 0xE06A02E7


def ent(i):
    return HERO + STRIDE * i


def run(item, targets, ticks=200, pf=False, seed=0x12345678, hp=5000):
    m, STATS, placed = S.make(item=item, mp=4, mpmax=4, seed=seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
    tptrs = []
    for (x, y) in targets:
        p = T.spawn_global(m, 25, x, y)
        assert p, 'target spawn failed'
        m.poke(p + 0x6C, m.peek(p + 0x6C) | 1)             # ClassA
        m.poke(p + 0x14, hp)
        tptrs.append(p)
    m.journal.clear()
    pad = W.Pad()
    log = []
    jn = 0
    prev_1d0 = {p: 0 for p in tptrs}
    for t in range(1, ticks + 1):
        # head of the tick: resolve the touch of the previous hit pass exactly as trace_targets does (ComputeNewHp = the D handler of the enemies)
        resolved = []
        for p in tptrs:
            tc = m.peek(p + 0x224)
            if tc:
                typ = m.peeks(tc + 0x68)
                before = m.peeks(p + 0x14)
                m.call(0x8003A374, p)
                after = m.peeks(p + 0x14)
                resolved.append((tptrs.index(p), typ, before - after))
        rng0 = m.peek(RNG)
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
        rng1 = m.peek(RNG)
        if pf:
            m.poke(RNG, (rng1 * MULT + ADD) & M32)
        hits = []
        for k, p in enumerate(tptrs):
            c = m.peeks(p + 0x1D0)
            if c == 0x19 and prev_1d0[p] != 0x19:
                hits.append(k)
            prev_1d0[p] = c
        ev = [(j[1], list(j[2]) if isinstance(j[2], (list, tuple)) else j[2]) for j in m.journal[jn:] if j[0] == t and j[1] != 'sound' and not j[1].startswith('pass_')]
        jn = len(m.journal)
        box = None
        e1 = ent(1)
        if m.peeks(e1 + 0x10) and m.peeks(e1 + 0x68) == 21:
            box = [m.peeks(e1 + 0x1D4 + 4 * i) for i in range(6)]
        log.append(dict(t=t, rng0=rng0, rng1=rng1, hit_targets=hits, resolved=resolved, hp=[m.peeks(p + 0x14) for p in tptrs], effects=ev, sprite21_box=box))
    return log


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('xy', nargs='*', type=int)
    ap.add_argument('--pf', action='store_true')
    ap.add_argument('--ticks', type=int, default=200)
    ap.add_argument('--out')
    a = ap.parse_args()
    targets = list(zip(a.xy[0::2], a.xy[1::2]))
    log = run(0x30, targets, ticks=a.ticks, pf=a.pf)
    if a.out:
        json.dump(dict(options=vars(a), targets=targets, log=log), open(a.out, 'w'))
    for k, tg in enumerate(targets):
        ticks = [r['t'] for r in log if k in r['hit_targets']]
        dmg = [(r['t'], d) for r in log for (kk, typ, d) in r['resolved'] if kk == k]
        print('target %d at %s : hit-pass writes at ticks %s ; resolutions (tick, damage) %s ; final HP %d' % (k, tg, ticks, dmg, log[-1]['hp'][k]))
    # box growth on the sprite 21
    prevbox = None
    for r in log:
        if r['sprite21_box'] != prevbox and r['sprite21_box'] is not None:
            print('  t=%d sprite21 words +0x1D4..: %s' % (r['t'], r['sprite21_box']))
            prevbox = r['sprite21_box']
    efx = [(r['t'], len([e for e in r['effects'] if e[0].startswith('effect')])) for r in log if any(e[0].startswith('effect') for e in r['effects'])]
    print('  effect creations per tick (stub journal):', efx)
    print('  ticks with draws:', [(r['t'], '%#x->%#x' % (r['rng0'], r['rng1'])) for r in log if r['rng0'] != r['rng1']])
