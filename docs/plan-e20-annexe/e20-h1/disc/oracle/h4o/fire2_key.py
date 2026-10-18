"""fire2_key.py - h4d1 discovery (read-only): the per-tick key of the fire-2 cast (item 0x30) in the emulated binary, a COPY of the e20-h4 harness (h4oracle/) run with `python -B`.

Differences with trace_cast.py (which stays the reference of the plan): it records, tick by tick, the 9-word shake block, the 9-word buffer, the pointer/identity words, the generator
(before the tick, after the passes, and, with --pf, after the binary's frame-end step 0x8002BD0C-0x8002BD34), the entities of the table and the stubbed effects/sounds of the tick.
It poses nothing else than the harness already does (hero (100,100), direction 0x18, flat synthetic field, seed 0x12345678, no other entity), except the options below.

options:
  --pf             add the frame-end step of the generator after every tick (s <- s * 0x7D2B89DD + 0xE06A02E7), AFTER the recording of the tick's own generator state
  --hurt T         poke the hero animation 0x31 right before tick T (the hurt state set by the damage entry), as interrupt_check.py does
  --block W0..W8   pre-set the shake block with 9 words before the cast (decimal or 0x..)
  --buf   W0..W8   pre-set the buffer 0x80127138 with 9 words before the cast (a stale buffer)
  --ticks N, --seed S, --out FILE
"""
import sys
import json
import argparse
import os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4oracle'))
import spellworld as S
import world as W
from machine import s32, M32

HERO = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
BLOCK = 0x800E4338
BUF = 0x80127138
MULT, ADD = 0x7D2B89DD, 0xE06A02E7


def ent(i):
    return HERO + STRIDE * i


def words(m, a, n=9):
    return [m.peeks(a + 4 * i) for i in range(n)]


def snap_entities(m):
    out = {}
    for i in range(1, 64):
        e = ent(i)
        st = m.peeks(e + 0x10)
        if st:
            out[i] = dict(status=st, id=m.peeks(e + 4), type=m.peeks(e + 0x68), flags=m.peek(e + 0x6C), x=m.peeks(e + 0x114), y=m.peeks(e + 0x118), z=m.peeks(e + 0x11C),
                          anim_t=m.peeks(e + 0x88), anim_c=m.peeks(e + 0x90), dir_t=m.peeks(e + 0x8C), animend=m.peeks(e + 0xAC), b4=m.peeks(e + 0xB4), c20=m.peeks(e + 0x20),
                          cur_atk=m.peek(e + 0x1C8), hitc=m.peeks(e + 0x220), box=words(m, e + 0x1D4, 6))
    return out


def run(item=0x30, ticks=200, seed=0x12345678, pf=False, hurt=None, block=None, buf=None, mp=4, hero_dir=None, targets=()):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=4, seed=seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
    P = ent(0)
    if hero_dir is not None:
        m.poke(P + 0x8C, hero_dir)
    if block:
        for i, v in enumerate(block):
            m.poke(BLOCK + 4 * i, v)
    if buf:
        for i, v in enumerate(buf):
            m.poke(BUF + 4 * i, v)
    pad = W.Pad()
    rows = []
    jn = 0
    for t in range(1, ticks + 1):
        if hurt is not None and t == hurt:
            m.poke(P + 0x88, 0x31)
        rng0 = m.peek(RNG)
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
        rng1 = m.peek(RNG)
        rng2 = rng1
        if pf:
            rng2 = (rng1 * MULT + ADD) & M32
            m.poke(RNG, rng2)
        ev = [(j[1], list(j[2]) if isinstance(j[2], (list, tuple)) else j[2]) for j in m.journal[jn:] if j[0] == t]
        jn = len(m.journal)
        rows.append(dict(t=t, hero_anim_t=m.peeks(P + 0x88), hero_anim_c=m.peeks(P + 0x90), inuse=m.peeks(0x80127164), phase=m.peeks(0x80127160), counter=m.peeks(0x8012715C),
                         mp=m.peek(STATS + 4) & 0xFFFF, block=words(m, BLOCK), buf=words(m, BUF), ptr=m.peek(0x80127124), ident=m.peeks(0x80127128), rng0=rng0, rng1=rng1, rng2=rng2,
                         ents=snap_entities(m), journal=ev))
    return rows


def fmt_args(name, a):
    if a is None:
        return name
    if isinstance(a, int):
        return '%s(%d)' % (name, a)
    s = [x if x < 0x80000000 else x - (1 << 32) for x in a]
    return '%s%s' % (name, s)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--pf', action='store_true')
    ap.add_argument('--hurt', type=int)
    ap.add_argument('--block', nargs=9, type=lambda s: int(s, 0))
    ap.add_argument('--buf', nargs=9, type=lambda s: int(s, 0))
    ap.add_argument('--ticks', type=int, default=200)
    ap.add_argument('--seed', type=lambda s: int(s, 0), default=0x12345678)
    ap.add_argument('--item', type=lambda s: int(s, 0), default=0x30)
    ap.add_argument('--out')
    a = ap.parse_args()
    rows = run(item=a.item, ticks=a.ticks, seed=a.seed, pf=a.pf, hurt=a.hurt, block=a.block, buf=a.buf)
    if a.out:
        json.dump(dict(options=vars(a), rows=rows), open(a.out, 'w'))
    prev = None
    for r in rows:
        key = (r['hero_anim_t'], r['inuse'], r['phase'], tuple(r['block']), tuple(r['buf']), tuple((i, e['status'], e['anim_c']) for i, e in r['ents'].items()), len(r['journal']), r['rng1'] != r['rng0'])
        if key != prev or r['journal']:
            print('t=%3d hero(anim_t %#x anim_c %#x) inuse %#x phase %d counter %d mp %d block %s buf %s' % (r['t'], r['hero_anim_t'], r['hero_anim_c'], r['inuse'], r['phase'], r['counter'], r['mp'], r['block'], r['buf']))
            print('       rng0 %#010x rng1 %#010x rng2 %#010x ents %s' % (r['rng0'], r['rng1'], r['rng2'], {i: (e['status'], e['id'], e['type'], e['flags'], e['x'] >> 16, e['y'] >> 16, e['z'], e['anim_t'], e['anim_c'], e['animend'], e['b4']) for i, e in r['ents'].items()}))
            for name, args in r['journal']:
                print('       journal', fmt_args(name, args))
        prev = key
