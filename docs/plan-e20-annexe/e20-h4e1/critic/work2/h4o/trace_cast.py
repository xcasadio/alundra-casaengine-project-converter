"""trace_cast.py - the full cast of one tome in the emulated game: real MovePlayer, real driver, real SpawnWarpEntity over the installed global sprite page, real entity passes
(native programs A/C/D/E of the spell sprites), animation, physics and hit pass.  Effects / sound stubs are journalled.  Targets (optional) are real sprite instances
(global sprite 25, the transparent block) given a balance sheet and HP.

usage: python -B trace_cast.py ITEM [--mp N] [--staff] [--hold-circle FROM TO] [--targets SPEC] [--ticks N] [--out FILE.json]

Output: a summary on stdout and, with --out, the per-tick data as JSON."""
import json
import sys
import argparse
import spellworld as S
import world as W
from machine import s32, M32
import h4_models as M

HERO = M.HERO
STRIDE = M.STRIDE


def ent(i):
    return HERO + STRIDE * i


def ground_height_px(m):
    return 0


def spawn_target(m, x_px, y_px, z=0, sheet_index=None, hp=100, flags_extra=0):
    """a target: real SpawnWarpEntity of the global sprite 25, then the fields of an enemy: ClassA + Collidable, a balance sheet (real 0x80044550 of the index), HP"""
    e = m.call(0x80039F58, HERO, 0, 25, x_px << 16, y_px << 16, z, 0) if False else None
    return e


def snap_ents(m):
    out = {}
    for i in range(64):
        e = ent(i)
        st = m.peeks(e + 0x10)
        if st:
            out[i] = (st, m.peeks(e + 4), m.peeks(e + 0x68), m.peeks(e + 0x114), m.peeks(e + 0x118), m.peeks(e + 0x11C), m.peeks(e + 0x88), m.peeks(e + 0x90),
                      m.peeks(e + 0xB8), m.peeks(e + 0x14), m.peeks(e + 0x220), m.peeks(e + 0x6C))
    return out


def run(item, mp=4, mpmax=4, staff=False, ticks=600, hold=None, out=None, verbose=True, seed=0x12345678, hero_dir=None, extra_setup=None, target_spec=None):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=mpmax, seed=seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
    if extra_setup:
        extra_setup(m, STATS)
    # the dummy sound/effect journal is already installed by machine.py; a journal of sounds with ticks is in m.journal
    P = W.ent(0)
    if hero_dir is not None:
        m.poke(P + 0x8C, hero_dir)
    pad = W.Pad()
    births = {}                       # (slot, id) -> dict
    ents_by_key = {}
    timeline = []
    prev_keys = set()
    mp0 = m.peek(STATS + 4) & 0xFFFF
    last_item = 0
    cast_start = None
    for t in range(1, ticks + 1):
        hold_bits = 0
        if t == 1:
            hold_bits |= W.CIRCLE
        if hold and hold[0] <= t <= hold[1]:
            hold_bits |= W.CIRCLE
        m.tick_no = t
        W.tick(m, pad, hold_bits, check_pad=False)
        snap = snap_ents(m)
        keys = set()
        for i, v in snap.items():
            if i == 0:
                continue
            key = (i, v[1])
            keys.add(key)
            if key not in births:
                births[key] = dict(slot=i, id=v[1], type=v[2], birth=t, first=v, track=[], last_status=v[0])
            b = births[key]
            b['track'].append((t,) + v)
            b['last_seen'] = t
            b['last_status'] = v[0]
        hero = snap[0]
        timeline.append((t, hero[6], m.peeks(0x80127164), m.peeks(0x80127160), m.peeks(0x8012715C), len([k for k in keys])))
        last_item = m.peeks(0x80127164)
    mp1 = m.peek(STATS + 4) & 0xFFFF
    return m, STATS, births, timeline, mp0, mp1


def summarize(item, births, timeline, m, mp0, mp1, ticks):
    print('item %#x: MP %d -> %d' % (item, mp0, mp1))
    # hero state changes
    last = None
    for (t, anim, inuse, phase, cnt, n) in timeline:
        key = (anim, inuse, phase)
        if key != last:
            print('  tick %3d hero anim %#x item_in_use %#x phase %d counter %d active entities %d' % (t, anim, inuse, phase, cnt, n))
            last = key
    by_type = {}
    for k, b in births.items():
        by_type.setdefault(b['type'], []).append(b)
    for typ, lst in sorted(by_type.items()):
        lives = [b['last_seen'] - b['birth'] + 1 for b in lst]
        print('  type %d: %d spawned, births %d..%d, lifetimes %d..%d ticks (median %d)' % (typ, len(lst), min(b['birth'] for b in lst), max(b['birth'] for b in lst), min(lives), max(lives), sorted(lives)[len(lives) // 2]))
    sounds = [(j[0], j[2]) for j in m.journal if j[1] == 'sound' and j[2] != 0]
    if sounds:
        from collections import Counter
        c = Counter(s for _, s in sounds)
        print('  sounds (id: count):', dict(c))
    fx = [(j[0], j[2]) for j in m.journal if j[1] in ('effect_3BDD8', 'effect_3BE74')]
    if fx:
        from collections import Counter
        c = Counter((j[1], j[2][1], j[2][2]) for j in m.journal if j[1] in ('effect_3BDD8', 'effect_3BE74') or False)
        print('  effects (kind, id, anim): count', dict(Counter((j[1], j[2][1], j[2][2]) for j in m.journal if j[1] in ('effect_3BDD8', 'effect_3BE74'))))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('item', type=lambda s: int(s, 0))
    ap.add_argument('--mp', type=int, default=4)
    ap.add_argument('--ticks', type=int, default=600)
    ap.add_argument('--out')
    a = ap.parse_args()
    m, STATS, births, timeline, mp0, mp1 = run(a.item, mp=a.mp, mpmax=4, ticks=a.ticks)
    summarize(a.item, births, timeline, m, mp0, mp1, a.ticks)
    if a.out:
        json.dump(dict(item=a.item, timeline=timeline, births={('%d_%d' % k): dict(type=v['type'], birth=v['birth'], last=v['last_seen'], track=v['track']) for k, v in births.items()}), open(a.out, 'w'))
