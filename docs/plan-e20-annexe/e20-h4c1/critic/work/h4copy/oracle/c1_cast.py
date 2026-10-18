"""c1_cast.py (h4c1 discovery, own script on a COPY of the e20-h4 harness): the earth-1 cast (item 0x2B) with an option to write the slot index word (+0) of the 64 entities as 0x8003B24C does
(the h4 harness leaves them 0, so the count [0x80134360] is not raised at birth and a newborn does not take part in its birth tick).
usage: python -B c1_cast.py [--slotidx] [--ticks N] [--out FILE.json] [--seed S] [--dir D] [--mp N] [--staff]"""
import argparse
import json
import sys
import spellworld as S
import world as W
import h4_models as M

ap = argparse.ArgumentParser()
ap.add_argument('--slotidx', action='store_true')
ap.add_argument('--ticks', type=int, default=140)
ap.add_argument('--out')
ap.add_argument('--seed', type=lambda s: int(s, 0), default=0x12345678)
ap.add_argument('--dir', type=int, default=None)
ap.add_argument('--item', type=lambda s: int(s, 0), default=0x2B)
a = ap.parse_args()

m, STATS, placed = S.make(item=a.item, mp=4, mpmax=4, seed=a.seed, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
if a.slotidx:
    for i in range(64):
        m.poke(S.ENT0 + S.STRIDE * i, i)
if a.dir is not None:
    m.poke(W.ent(0) + 0x8C, a.dir)
pad = W.Pad()
F = ['status', 'id', 'type', 'x', 'y', 'z', 'fz', 'tdir', 'tanim', 'canim', 'row98', 'animend', 'hitc', 'cnt1c', 'flags', 'b274']
def snap(i):
    e = S.ent(i)
    g = m.peeks
    return dict(status=g(e + 0x10), id=g(e + 4), type=g(e + 0x68), x=g(e + 0x114), y=g(e + 0x118), z=g(e + 0x11C), fz=g(e + 0xB8), tdir=g(e + 0x8C), tanim=g(e + 0x88),
                canim=g(e + 0x90), row98=g(e + 0x98), animend=g(e + 0xAC), hitc=g(e + 0x220), cnt1c=g(e + 0x1C), flags=m.peek(e + 0x6C), b274=g(e + 0x274), slot=i)
births = {}
hero_track = []
for t in range(1, a.ticks + 1):
    hold = W.CIRCLE if t == 1 else 0
    m.tick_no = t
    W.tick(m, pad, hold, check_pad=False)
    h = snap(0)
    hero_track.append((t, h['tanim'], m.peeks(0x80127164), m.peeks(0x80127160), m.peeks(0x8012715C), m.peeks(0x80134360)))
    for i in range(1, 64):
        s = snap(i)
        if s['status'] == 0:
            continue
        k = '%d_%d' % (i, s['id'])
        b = births.setdefault(k, dict(slot=i, id=s['id'], type=s['type'], birth=t, track=[]))
        b['track'].append(dict(t=t, **s))
        b['last'] = t
sounds = [(j[0], j[2]) for j in m.journal if j[1] == 'sound' and j[2] != 0]
out = dict(slotidx=a.slotidx, hero=hero_track, births=births, sounds=sounds, rng=m.peek(0x80098708))
if a.out:
    json.dump(out, open(a.out, 'w'))
n = len(births)
print('slotidx', a.slotidx, 'births', n, 'first birth', min(b['birth'] for b in births.values()), 'last birth', max(b['birth'] for b in births.values()))
lives = [b['last'] - b['birth'] + 1 for b in births.values()]
print('lifetimes', min(lives), max(lives))
print('hero timeline (t, anim, item, phase, counter, count) changes:')
last = None
for r in hero_track:
    key = (r[1], r[2], r[3])
    if key != last:
        print('  ', r)
        last = key
from collections import Counter
print('sounds', dict(Counter(s for _, s in sounds)))
print('rng after', hex(out['rng'] & 0xFFFFFFFF))
