"""cancel_pf.py - h4c2 discovery: the interruption of the earth-2 tome (interrupt_check.py) with the map-load slot initialisation and the per-frame step of the generator:
the hero animation is set to 0x31 (the hurt state) before the passes of tick T_HIT, the driver's guard (0x800351B8) cancels (0x800350C0 -> 0x80034BDC) and the rocks in the air
go to status 3 with the target animation 1.  Rows per rock: status, target animation, z, +0xAC, flags, and the camera kick of each tick.
usage: python -B cancel_pf.py [T_HIT] [--out FILE]"""
import sys
import json
import spellworld as S
import world as W
from machine import M32

RNG = 0x80098708
KICK = 0x800DD874
MUL, INC = 0x7D2B89DD, 0xE06A02E7
P = W.ent(0)
t_hit = int(sys.argv[1]) if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else 100
out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else None

m, STATS, placed = S.make(item=0x2C, mp=4, mpmax=4, indices=(1, 16, 17, 19, 20, 21, 22, 23, 24))
for i in range(64):
    m.poke(S.ENT0 + S.STRIDE * i, i)
if '--fix' in sys.argv:
    import fixpatch
    fixpatch.apply(m)
pad = W.Pad()
rows = []
for t in range(1, t_hit + 130):
    if t == t_hit:
        m.poke(P + 0x88, 0x31)
    m.tick_no = t
    W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
    kick = m.peeks(KICK)
    m.poke(KICK, 0)
    m.poke(RNG, (m.peek(RNG) * MUL + INC) & M32)
    if t >= t_hit - 1:
        ents = []
        for i in range(1, 64):
            e = W.ent(i)
            st = m.peeks(e + 0x10)
            if st:
                ents.append(dict(slot=i, id=m.peeks(e + 4), st=st, type=m.peeks(e + 0x68), ta=m.peeks(e + 0x88), z=m.peeks(e + 0x11C) / 65536.0, ae=m.peeks(e + 0xAC), fl=m.peek(e + 0x6C), b20=m.peeks(e + 0x20)))
        rows.append(dict(t=t, item=m.peeks(0x80127164), phase=m.peeks(0x80127160), hero_ta=m.peeks(P + 0x88), kick=kick, ents=ents))
snd=[(t,k,v) for (t,k,v) in m.journal if k=='sound' and t>=t_hit-2 and t<=t_hit+3]
print('sound journal around the cancel:', snd)
print('all sounds from t_hit-2 to end:', [(t,v) for (t,k,v) in m.journal if k=='sound' and t>=t_hit-2])
for r in rows[:4] + rows[-1:]:
    print('t=%d item_in_use=%#x hero_ta=%#x kick=%d ents=%s' % (r['t'], r['item'], r['hero_ta'], r['kick'], [(e['id'], e['st'], e['ta'], round(e['z']), e['ae'], hex(e['fl'] & 0xFFFFFFFF)) for e in r['ents']]))
kt = [(r['t'], r['kick']) for r in rows if r['kick']]
print('kicks after the cancel:', kt)
dead = {}
for r in rows:
    for e in r['ents']:
        dead[e['id']] = r['t']
print('last tick seen per rock id:', dead)
if out:
    json.dump(rows, open(out, 'w'))
