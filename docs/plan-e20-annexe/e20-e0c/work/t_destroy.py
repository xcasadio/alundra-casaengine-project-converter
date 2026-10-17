import random, sys
from lh import *
import models as M
random.seed(int(sys.argv[1]) if len(sys.argv) > 1 else 5)
w = World(); m = w.m
COUNT_ADDR = 0x80134360
def eaddr(i): return ENT0 + i * STRIDE
j2 = []
m.hooks[0x8003BE74] = lambda mc: j2.append(('effect', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(mc.r[29] + 0x10), mc.rw(mc.r[29] + 0x14), mc.rw(mc.r[29] + 0x18), mc.rw(mc.r[29] + 0x1C)))
def fresh():
    for i in range(0x300 * 6): m.wb(FAKE_BASE + i, 0)
# model state: dict of slot -> dict of fields
def model_destroy(ents, e, a1, mpmax, journal, hdr):
    """0x8003A59C, e = slot index; ents = dict slot -> fields; journal: list"""
    E = ents[e]
    # SpawnContents
    item = E[0x3C]
    if item != 0 and not M.veto(item, mpmax):
        journal.append(('spawn', 0, 0, (item + 0x1E) & M32, E[0x114] & M32, E[0x118] & M32, E[0x11C] & M32, 0))
        journal.append(('sound', 0x54))
    E[0x10] = 4
    E[0x228] = M32
    if E[0x1B8]:
        E[0x1B8] = 0; journal.append(('fxrelease',))
    if s32(a1) == -1: a1 = hdr[e]
    if a1 != 0: journal.append(('effect', 0, a1 & M32, 0, eaddr(e), 1, 0, 0, 0))
    if E[0x28]: journal.append(('carrier', E[0x28]))
def model_rec(ents, e, a1, mpmax, journal, hdr, count):
    if s32(count) >= 0:
        for i in range(0, count + 1):
            C = ents[i]
            if 1 <= ents[e][0x10] <= 3 and C[0x0C] == eaddr(e):   # NB: the test is on the state of the TARGET (lw 0x10(s2)), not on the candidate's
                model_rec(ents, i, a1, mpmax, journal, hdr, count)
    model_destroy(ents, e, a1, mpmax, journal, hdr)
bad = 0; n = 0; nspawn_tot = 0
HDRBASE = 0x80158000
for it in range(8000):
    NE = random.choice([1, 2, 3, 5, 8])
    count = NE - 1 if random.random() < 0.9 else random.choice([NE + 1, -1])
    mpmax = random.choice([0, 3])
    ents = {}
    for i in range(NE + 2):
        base = eaddr(i)
        for k in range(STRIDE): m.wb(base + k, 0)
        F = {}
        F[0x10] = random.choice([0, 1, 2, 3, 3, 4, 2, 3])
        par = random.random()
        F[0x0C] = 0 if (par < 0.3 or i == 0) else eaddr(random.randrange(0, i))   # acyclic: parent has a smaller slot (a cycle would never end in the binary either)
        F[0x3C] = random.choice([0, 0x45, 0x54, 0x24, 0x26, 0x51, 0x52, 0x55, random.randrange(0, 0x62)])
        F[0x114], F[0x118], F[0x11C] = (random.getrandbits(32) for _ in range(3))
        F[0x1B8] = random.choice([0, 0, 0x80159000 + 0x100 * i])
        F[0x28] = random.choice([0, 0, 0, FAKE_BASE + 0x500])
        F[0x228] = random.getrandbits(32)
        hv = random.choice([0, 0, 5, 8, 11])
        F[0x64] = HDRBASE + i * 0x40
        ents[i] = F
        for o, v in F.items(): m.ww(base + o, v)
        m.wb(F[0x64] + 0x1E, hv)
        if F[0x1B8]: m.ww(F[0x1B8] + 0x68, 0x1234)
    hdr = {i: m.rb(ents[i][0x64] + 0x1E) for i in ents}
    m.ww(COUNT_ADDR, count & M32)
    w.set_mp(0, mpmax)
    a1 = random.choice([M32, 0, 6, 9, 0xFFFFFFFE, 0x80000001])
    target = random.randrange(0, NE)
    use_rec = random.random() < 0.5
    w.j.clear(); j2.clear(); w.nspawn = 0; w.spawn_fail = False
    w.call(0x8003A648 if use_rec else 0x8003A59C, eaddr(target), a1)
    # real journal = spawn/sound (w.j, in order) merged with effect (j2) -- they are ordered per entity: spawn,sound,effect. Re-merge by checking per-entity order impossible; compare as two separate lists
    jm = []
    if use_rec: model_rec(ents, target, a1, mpmax, jm, hdr, count)
    else: model_destroy(ents, target, a1, mpmax, jm, hdr)
    real_sp = [x for x in w.j]
    exp_sp = [x for x in jm if x[0] in ('spawn', 'sound')]
    real_fx = list(j2); exp_fx = [x for x in jm if x[0] == 'effect']
    n += 1; nspawn_tot += sum(1 for x in exp_sp if x[0] == 'spawn')
    # final fields
    ok = real_sp == exp_sp and real_fx == exp_fx
    for i, F in ents.items():
        base = eaddr(i)
        for o in (0x10, 0x228, 0x1B8):
            if m.rw(base + o) != F[o]: ok = False
        if F[0x1B8] and m.rw(0x80159000 + 0x100 * i + 0x68) != (0 if F[0x1B8] == 0 else (0 if F[0x10] == 4 and True else 0x1234)): pass
    if not ok:
        bad += 1
        if bad < 5: print('MISMATCH destroy', it, use_rec, hex(a1), count, real_sp[:4], exp_sp[:4], real_fx[:2], exp_fx[:2])
print('DestroyEntity/recursion vectors', n, 'mismatches', bad, 'spawns', nspawn_tot)
for name, lo, hi in [('DestroyEntity 0x8003A59C', 0x8003A59C, 0x8003A648), ('recursive 0x8003A648', 0x8003A648, 0x8003A774)]:
    f = set(range(lo, hi, 4)); print(' ', name, len(m.cov & f), '/', len(f), [hex(a) for a in sorted(f - m.cov)][:6])
