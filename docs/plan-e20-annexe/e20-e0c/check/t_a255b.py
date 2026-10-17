import random, h2
from h2 import *
rng = random.Random(41)
m = mk()
E = ENT0 + 9*STRIDE
CH = ENT0 + 3*STRIDE
J = []
m.hooks[0x8005A9E0] = lambda mc: J.append(('cd', mc.r[4] & M32))
m.hooks[0x8003A774] = lambda mc: J.append(('light', mc.r[4] & M32))
BP = 0x801EBA40; BT = 0x801EB344

def put(a, d):
    for i in range(STRIDE): m.wb(a + i, 0)
    for o, v in d.items(): m.ww(a + o, v)

def model(e, ch, bankw):
    e = dict(e); ch = dict(ch); j = []
    g = lambda d, o: d.get(o, 0)
    if g(e, 0x68) == 0x1E:
        if g(e, 0x3C) == 0: e[0x88] = 1
        return e, j
    k = g(e, 0x274)
    if k == 1: return e, j
    if k == 2:
        if g(ch, 0x88) == 1:
            if g(e, 0x288) != 0: j.append(('cd', 0xB))
            return e, j
        e[0x10] = 1
        return e, j
    e[0x6C] = g(e, 0x6C) & ~0x80 & M32
    f = g(e, 0x40)
    if f != 0:
        lo = f & 0xFFFF
        base = BP if lo & 0x8000 else BT
        addr = base + (((lo >> 3) & 0xFFC))
        w = bankw.get(addr, 0)
        sh = f - (((f + (31 if f < 0 else 0)) >> 5) << 5) if f < 0x80000000 else None
        sh = (f & 31)
        if not (w & (1 << sh)):
            j.append(('light', E)); return e, j
    e[0x27C] = 0; e[0x280] = M32; e[0x284] = 0; e[0x278] = f
    return e, j

bad = 0
for it in range(15000):
    e = {0x68: rng.choice([0x1E, 0x1F, 0x45 + 0x1E, 0x24 + 0x1E]), 0x3C: rng.choice([0, 0, 5]), 0x274: rng.choice([0, 0, 1, 2, 3]),
         0x6C: rng.getrandbits(32), 0x40: rng.choice([0, 0, rng.randrange(0, 0x800), 0x8000 | rng.randrange(0, 0x800), 0x20, 0x8020]),
         0x27C: rng.getrandbits(32), 0x280: CH if rng.random() < .5 else rng.getrandbits(32), 0x284: rng.getrandbits(32), 0x278: rng.getrandbits(32),
         0x288: rng.choice([0, 1]), 0x88: rng.getrandbits(32), 0x10: rng.choice([1, 2, 3])}
    if e[0x274] == 2: e[0x280] = CH
    ch = {0x88: rng.choice([0, 1, 1, 2])}
    bankw = {}
    f = e[0x40] & 0xFFFF
    if f:
        base = BP if f & 0x8000 else BT
        bankw[base + (((f >> 3) & 0xFFC))] = rng.choice([0, 0xFFFFFFFF, 1 << (f & 31), ~(1 << (f & 31)) & M32, rng.getrandbits(32)])
    put(E, e); put(CH, ch)
    for a, v in bankw.items(): m.ww(a, v)
    J.clear()
    m.tracing = True; m.call(0x80061BD4, E); m.tracing = False
    real = {o: m.rw(E + o) for o in (0x10, 0x6C, 0x88, 0x278, 0x27C, 0x280, 0x284)}
    me, mj = model(e, ch, bankw)
    mod = {o: me.get(o, 0) for o in real}
    # normalise J addresses
    jr = [(t[0], (E if False else t[1])) for t in J]
    jm = [(t[0], t[1]) for t in mj]
    if real != mod or jr != jm:
        bad += 1
        if bad < 5: print('DIFF', {hex(k): hex(v) for k, v in e.items()}, real, mod, jr, jm)
print('A255 vectors 15000 diffs', bad)
ws = list(range(0x80061BD4, 0x80061D14, 4)); print('cov', len([a for a in ws if a in m.cov]), len(ws), [hex(a) for a in ws if a not in m.cov])
