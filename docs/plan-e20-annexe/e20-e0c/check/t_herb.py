import random, h2
from h2 import *
rng = random.Random(33)
m = mk()
HERO = ENT0
PLAYER_PTR = 0x80176318; PLAYER = 0x80141000
INVPTR = 0x8017631C; INV = 0x80142000; INVN = 0x8017638C
TBL = 0x800B9FE8
FXBASE = 0x80150000
m.ww(PLAYER_PTR, PLAYER); m.ww(INVPTR, INV); m.ww(INVN, 0x62)
J = []
def fx(mc):
    sp = mc.r[29]
    J.append(('fx', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    a = FXBASE
    for i in range(0x100): mc.wb(a + i, 0)
    mc.r[2] = a
m.hooks[0x8003BDD8] = fx
bad = 0
for it in range(4000):
    item = 0x24
    mx = rng.choice([0, 1, 5, 10, 99, -1 & 0xFFFF])
    cnt = rng.choice([0, 1, 4, 5, 9, 10, 98, 99, 100])
    hpmax = rng.choice([1, 4, 8, 20, 40, 50, rng.randrange(1, 100)]); hp = rng.randrange(0, hpmax + 1)
    m.wh(TBL + 10 * item + 6, mx)
    m.wh(INV + 4 * item + 2, cnt)
    for i in range(0x294): m.wb(HERO + i, 0)
    m.ww(HERO + 0x14, hp); m.ww(HERO + 0x18, hpmax)
    z = rng.getrandbits(30)
    m.ww(HERO + 0x11C, z)
    J.clear()
    m.tracing = True
    r = m.call(0x80033F00, HERO, item)
    m.tracing = False
    real_cnt = m.rhs(INV + 4 * item + 2); real_hp = s32(m.rw(HERO + 0x14))
    # model (DLL's AddOneItemIfUnlocked semantics, written from the disassembly of 0x8004E530):
    smx = mx - 0x10000 if mx & 0x8000 else mx
    scnt = cnt - 0x10000 if cnt & 0x8000 else cnt
    if scnt == smx: ncnt = scnt; entered = False
    else: ncnt = scnt + 1; entered = ncnt > scnt
    mhp = hp; fxs = []
    if not entered:
        d = max(5, (hpmax + (3 if hpmax < 0 else 0)) >> 2)
        mhp = min(hpmax, hp + d)
        fxs = [('fx', 0, 0xE, 0, 0, 0, (z + 0x100000) & M32)]
    ok = real_cnt == ncnt and real_hp == mhp and [t for t in J] == fxs and (r & M32) == 0
    if not ok:
        bad += 1
        if bad < 5: print('DIFF', mx, cnt, hp, hpmax, 'real', real_cnt, real_hp, J, 'mod', ncnt, mhp, fxs, r)
print('herb vectors 4000 diffs', bad)
ws = list(range(0x80033EC8, 0x80033EFC, 4)) + list(range(0x80032E50, 0x80032EEC, 4)) + list(range(0x80033F48, 0x80033F6C, 4))
print('cov', len([a for a in ws if a in m.cov]), len(ws), [hex(a) for a in ws if a not in m.cov])
