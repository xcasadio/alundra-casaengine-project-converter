"""cast_rich.py - h4c2 discovery: the cast of the earth-2 tome (0x2C) as the binary plays it (slot initialisation of the map load, per-frame step of the generator), with per-rock rows:
birth tick and position, the draws (r1, r2) recovered from the generator, Z per tick, +0x13C (ForceAdjusted), +0x144 (on ground), +0xAC (end of animation), status, target animation, flags,
the tick of the first pass of E13 (the camera kick), the tick of the status 4, the tick the slot is free.
usage: python -B cast_rich.py [--pf] [--seed N] [--hero-x PX] [--hero-y PX] [--out FILE] [--ticks N]"""
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
HERO = W.ent(0)

ap = argparse.ArgumentParser()
ap.add_argument('--pf', action='store_true')
ap.add_argument('--no-slotinit', action='store_true')
ap.add_argument('--seed', type=lambda s: int(s, 0), default=0x12345678)
ap.add_argument('--ticks', type=int, default=400)
ap.add_argument('--hero-x', type=int)
ap.add_argument('--hero-y', type=int)
ap.add_argument('--out')
ap.add_argument('--water-east', action='store_true')
a = ap.parse_args()

orig_make = S.make


def make(*args, **kw):
    m, STATS, placed = orig_make(*args, **kw)
    if not a.no_slotinit:
        for i in range(64):
            m.poke(S.ENT0 + S.STRIDE * i, i)
    if a.hero_x is not None:
        m.poke(S.ENT0 + 0x114, a.hero_x << 16)
    if a.hero_y is not None:
        m.poke(S.ENT0 + 0x118, a.hero_y << 16)
    return m, STATS, placed


S.make = make
if '--water-east' in sys.argv:
    _bw = W.build_world
    def build_world(m, gravity=128, visc=4096, cells=None):
        return _bw(m, gravity=gravity, visc=visc, cells=lambda c, r: (0, 0, 0, 8) if c >= 7 else (0, 0, 0, 0))
    W.build_world = build_world
orig_tick = W.tick
per_tick = []


def tick(m, pad, hold, skip=(), check_pad=True):
    rng0 = m.peek(RNG) & M32
    orig_tick(m, pad, hold, skip=skip, check_pad=check_pad)
    rng1 = m.peek(RNG) & M32
    kick = m.peeks(KICK)
    m.poke(KICK, 0)
    if a.pf:
        m.poke(RNG, (rng1 * MUL + INC) & M32)
    ents = {}
    for i in range(1, 64):
        e = W.ent(i)
        st = m.peeks(e + 0x10)
        if st:
            ents[i] = dict(st=st, id=m.peeks(e + 4), type=m.peeks(e + 0x68), x=m.peeks(e + 0x114), y=m.peeks(e + 0x118), z=m.peeks(e + 0x11C), fz=m.peeks(e + 0xB8),
                           ta=m.peeks(e + 0x88), ca=m.peeks(e + 0x90), td=m.peeks(e + 0x8C), ae=m.peeks(e + 0xAC), fadj=m.peeks(e + 0x13C), gnd=m.peeks(e + 0x144),
                           b20=m.peeks(e + 0x20), cnt=m.peeks(e + 0x1C), fl=m.peek(e + 0x6C), t138=m.peeks(e + 0x138))
    per_tick.append(dict(t=len(per_tick) + 1, rng0=rng0, rng1=rng1, kick=kick, hero_ta=m.peeks(HERO + 0x88), hero_z=m.peeks(HERO + 0x11C), ents=ents))


W.tick = tick
m, STATS, births, timeline, mp0, mp1 = T.run(0x2C, mp=4, mpmax=4, ticks=a.ticks, seed=a.seed)

# rows per rock
rows = []
by_key = {}
for d in per_tick:
    for i, e in d['ents'].items():
        by_key.setdefault((i, e['id']), []).append((d['t'], e))
for (slot, eid), tr in sorted(by_key.items(), key=lambda kv: kv[1][0][0]):
    if tr[0][1]['type'] != 17:
        continue
    b = tr[0][0]
    first = tr[0][1]
    def first_tick(pred):
        return next((t for t, e in tr if pred(e)), None)
    row = dict(slot=slot, id=eid, birth=b, x0=first['x'] >> 16, y0=first['y'] >> 16, z0=first['z'] / 65536.0, st_at_birth=first['st'],
               st2=first_tick(lambda e: e['st'] == 2), st3=first_tick(lambda e: e['st'] == 3), st4=first_tick(lambda e: e['st'] == 4),
               ta1=first_tick(lambda e: e['ta'] == 1), animend=first_tick(lambda e: e['ae'] == 1), fadj=first_tick(lambda e: e['fadj'] != 0), ground=first_tick(lambda e: e['gnd'] != 0),
               fl_first=first['fl'] & 0xFFFFFFFF, fl_last=tr[-1][1]['fl'] & 0xFFFFFFFF, x_last=tr[-1][1]['x'] >> 16, y_last=tr[-1][1]['y'] >> 16, z_last=tr[-1][1]['z'] / 65536.0,
               dir=first['td'], t138=first['t138'], last_seen=tr[-1][0])
    # draws: the two draws of the birth tick are the words rng0(b)*MUL+INC and its successor
    rng0 = per_tick[b - 1]['rng0']
    w1 = (rng0 * MUL + INC) & M32
    w2 = (w1 * MUL + INC) & M32
    row['r1'] = (w1 * 32) >> 32
    row['r2'] = (w2 * 32) >> 32
    rows.append(row)
print('rocks:', len(rows))
for r in rows:
    print('birth=%3d slot=%d id=%2d r1=%2d r2=%2d pos0=(%d,%d) z0=%.0f st@birth=%d st3=%s fadj=%s ground=%s ta1=%s animend=%s st4=%s last=%s dir=%d t138=%d' % (
        r['birth'], r['slot'], r['id'], r['r1'], r['r2'], r['x0'], r['y0'], r['z0'], r['st_at_birth'], r['st3'], r['fadj'], r['ground'], r['ta1'], r['animend'], r['st4'], r['last_seen'], r['dir'], r['t138']))
kicks = [(d['t'], d['kick']) for d in per_tick if d['kick']]
print('kicks:', kicks)
if a.out:
    json.dump(dict(pf=a.pf, seed=a.seed, rows=rows, kicks=kicks, per_tick=[dict(t=d['t'], rng0=d['rng0'], rng1=d['rng1'], kick=d['kick'], hero_ta=d['hero_ta']) for d in per_tick]), open(a.out, 'w'))
