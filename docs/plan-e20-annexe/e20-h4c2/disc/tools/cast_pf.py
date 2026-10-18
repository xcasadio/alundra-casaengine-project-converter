"""cast_pf.py - h4c2 discovery: the cast of one tome (default 0x2C, earth 2) in the emulated game with
  --slotinit : the map-load slot initialisation of the binary (0x8003B24C-0x8003B2CC: +0 of every one of the 64 slots = its own index), which the oracle's setup leaves at 0;
  --pf       : the per-frame step of the generator (0x8002BD0C-0x8002BD34: s <- s * 0x7D2B89DD + 0xE06A02E7 at [0x80098708]), added AFTER the passes of every tick;
  --kick     : [0x800DD874] (the camera kick) read at the end of every tick and cleared, as the camera step 0x8002CE98 does.
Output (JSON): per tick rng0 / rng1 / kick / hero fields, and the birth + track of every entity.
usage: python -B cast_pf.py ITEM [--slotinit] [--pf] [--seed N] [--hero-x PX --hero-y PX] [--out FILE] [--ticks N]"""
import sys
import json
import argparse
import spellworld as S
import trace_cast as T
import world as W
from machine import s32, M32

RNG = 0x80098708
KICK = 0x800DD874
MUL, INC = 0x7D2B89DD, 0xE06A02E7


def step(s):
    return (s * MUL + INC) & M32


ap = argparse.ArgumentParser()
ap.add_argument('item', type=lambda s: int(s, 0))
ap.add_argument('--slotinit', action='store_true')
ap.add_argument('--pf', action='store_true')
ap.add_argument('--seed', type=lambda s: int(s, 0), default=0x12345678)
ap.add_argument('--ticks', type=int, default=400)
ap.add_argument('--mp', type=int, default=4)
ap.add_argument('--out')
ap.add_argument('--hero-x', type=int)
ap.add_argument('--hero-y', type=int)
a = ap.parse_args()

orig_make = S.make


def make(*args, **kw):
    m, STATS, placed = orig_make(*args, **kw)
    if a.slotinit:
        for i in range(64):
            m.poke(S.ENT0 + S.STRIDE * i, i)
    if a.hero_x is not None:
        m.poke(S.ENT0 + 0x114, a.hero_x << 16)
    if a.hero_y is not None:
        m.poke(S.ENT0 + 0x118, a.hero_y << 16)
    return m, STATS, placed


S.make = make

# monkey-patch W.tick so that the per-frame step and the kick sampling happen in T.run's loop
orig_tick = W.tick
per_tick = []


def tick(m, pad, hold, skip=(), check_pad=True):
    rng0 = m.peek(RNG) & M32
    orig_tick(m, pad, hold, skip=skip, check_pad=check_pad)
    rng1 = m.peek(RNG) & M32
    kick = m.peeks(KICK)
    m.poke(KICK, 0)                      # the camera step clears it (0x8002CE98)
    if a.pf:
        m.poke(RNG, step(rng1))
    per_tick.append(dict(rng0=rng0, rng1=rng1, kick=kick))


W.tick = tick
m, STATS, births, timeline, mp0, mp1 = T.run(a.item, mp=a.mp, mpmax=4, ticks=a.ticks, seed=a.seed)
T.summarize(a.item, births, timeline, m, mp0, mp1, a.ticks)
kicks = [(i + 1, d['kick']) for i, d in enumerate(per_tick) if d['kick']]
print('camera kick written (tick, value):', kicks)
if a.out:
    json.dump(dict(item=a.item, seed=a.seed, pf=a.pf, slotinit=a.slotinit, per_tick=per_tick, timeline=timeline,
                   births={('%d_%d' % k): dict(type=v['type'], birth=v['birth'], last=v['last_seen'], track=v['track']) for k, v in births.items()}), open(a.out, 'w'))
