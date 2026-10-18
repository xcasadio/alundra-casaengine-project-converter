"""trace_targets.py - a cast with real targets: stationary instances of the global sprite 25 (transparent block) given ClassA, a balance sheet (all responses 0x10 by default,
or a chosen real record) and a large HP.  The damage of the hit pass is resolved as the D handlers of the enemies do it: ComputeNewHp (REAL 0x8003A374) for every entity left with a TouchingEntity,
at the head of the next tick (before the events pass, 0x8003B388 order), then the target takes an invulnerability counter +0x1CC = INV when INV > 0.

usage: python -B trace_targets.py ITEM X Y [X Y ...] [--inv N] [--hp N] [--ticks N]   (X, Y in px, absolute; the hero is at 100,100)"""
import sys
import argparse
import spellworld as S
import world as W
from machine import s32, M32
import h4_models as M

HERO = M.HERO
STRIDE = M.STRIDE
SP = 0x801FFF00


def ent(i):
    return HERO + STRIDE * i


def spawn_global(m, sprite, x_px, y_px, z=0, direction=0):
    sp = SP
    m.poke(sp + 0x10, y_px << 16)
    m.poke(sp + 0x14, z)
    m.poke(sp + 0x18, direction)
    ptr = m.call(0x80039F58, HERO, 0, sprite, x_px << 16)
    return ptr & M32


def run(item, targets, inv=0, hp=1000, ticks=600, mp=4, seed=0x12345678, sheet=None, hold=None, hero_dir=None, verbose=False, staff_extra=()):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=4, seed=seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25), extra_items=staff_extra)
    P = ent(0)
    if hero_dir is not None:
        m.poke(P + 0x8C, hero_dir)
    tptrs = []
    for (x, y) in targets:
        p = spawn_global(m, 25, x, y)
        assert p, 'target spawn failed'
        m.poke(p + 0x6C, m.peek(p + 0x6C) | 1)             # ClassA
        m.poke(p + 0x14, hp)
        if sheet is not None:
            m.poke(p + 0x1C4, m.call(0x80044550, sheet) & M32)
        tptrs.append(p)
    m.journal.clear()
    pad = W.Pad()
    hits = {p: [] for p in tptrs}
    hp0 = {p: m.peeks(p + 0x14) for p in tptrs}
    touch_log = []
    hero_hp0 = m.peeks(P + 0x14)
    for t in range(1, ticks + 1):
        # head of the tick: the D handlers of the targets resolve the touch of the previous hit pass
        for p in tptrs:
            tc = m.peek(p + 0x224)
            if tc:
                typ = m.peeks(tc + 0x68)
                before = m.peeks(p + 0x14)
                m.call(0x8003A374, p)
                after = m.peeks(p + 0x14)
                hits[p].append((t, typ, before - after, m.peeks(tc + 0x114) >> 16, m.peeks(tc + 0x118) >> 16))
                if inv:
                    m.poke(p + 0x1CC, inv)
        hold_bits = W.CIRCLE if t == 1 else 0
        if hold and hold[0] <= t <= hold[1]:
            hold_bits |= W.CIRCLE
        m.tick_no = t
        W.tick(m, W.Pad() if False else pad, hold_bits, check_pad=False)
    res = {}
    for p in tptrs:
        res[p] = dict(x=m.peeks(p + 0x114) >> 16, y=m.peeks(p + 0x118) >> 16, hp=m.peeks(p + 0x14), hits=hits[p], push=(m.peeks(p + 0xCC), m.peeks(p + 0xD0)))
    return m, res, hp0, STATS


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('item', type=lambda s: int(s, 0))
    ap.add_argument('xy', nargs='*', type=int)
    ap.add_argument('--inv', type=int, default=0)
    ap.add_argument('--hp', type=int, default=1000)
    ap.add_argument('--ticks', type=int, default=600)
    ap.add_argument('--sheet', type=int)
    a = ap.parse_args()
    targets = list(zip(a.xy[0::2], a.xy[1::2]))
    m, res, hp0, STATS = run(a.item, targets, inv=a.inv, hp=a.hp, ticks=a.ticks, sheet=a.sheet)
    for p, r in res.items():
        tot = sum(h[2] for h in r['hits'])
        from collections import Counter
        c = Counter((h[1], h[2]) for h in r['hits'])
        print('target at (%d,%d): HP %d -> %d, %d damage events, attackers by (type, damage): %s' % (r['x'], r['y'], a.hp, r['hp'], len(r['hits']), dict(c)))
