import random, sys, h2
from h2 import *
import exelib
rng = random.Random(7)
TAB = [exelib.u8(RANDTAB + i) for i in range(128*16)]
ITEM4 = [exelib.u8(ITEMTAB + 8*i + 4) for i in range(0x62)]
PLAYER_PTR = 0x80176318; PLAYER = 0x80141000
FAKE = 0x80150000
m = mk()
# item table name pointers etc. irrelevant. Player struct for MpMax
m.ww(PLAYER_PTR, PLAYER)
J = []
nspawn = [0]; fail = [False]
def spawn(mc):
    sp = mc.r[29]
    J.append(('spawn', mc.r[4]&M32, mc.r[5]&M32, mc.r[6]&M32, mc.r[7]&M32, mc.rw(sp+0x10), mc.rw(sp+0x14), mc.rw(sp+0x18)))
    if fail[0]: mc.r[2] = 0; return
    a = FAKE + nspawn[0]*0x300; nspawn[0] += 1
    for i in range(0x300): mc.wb(a+i, 0)
    mc.ww(a+0x6C, 0xFFFFFFFF)
    mc.r[2] = a
def snd(mc): J.append(('sound', mc.r[4]&M32))
m.hooks[0x80039F58] = spawn; m.hooks[0x800490FC] = snd
def choose_model(code, s):
    steps = 0
    while code < 0x100 if code < 0x80000000 else True:
        if code & 0x80:
            s = lcg(s); steps += 1
            code = TAB[(code & 0x7F)*16 + (s >> 28)]
        else:
            return (code if code < 0x62 else 0), s
    return 0, s
def veto(item, mpmax): return item in (0x26,0x51,0x52) and mpmax == 0
def run(kind, code_or_item, pos, flag, mpmax, rngs, failspawn):
    global J
    J = []; fail[0] = failspawn; nspawn[0] = 0
    m.wh(PLAYER+6, mpmax & 0xFFFF)
    m.ww(RNG, rngs)
    e = ENT0 + 5*STRIDE
    m.ww(e+0x3C, code_or_item); m.ww(e+0x40, flag)
    m.ww(e+0x114, pos[0]); m.ww(e+0x118, pos[1]); m.ww(e+0x11C, pos[2])
    m.tracing = True
    if kind == 'rl': r = m.call(0x80032C7C, code_or_item, *pos)
    else: r = m.call(0x80032B90, e)
    m.tracing = False
    ent = bytes(m.ram[(FAKE&0x1FFFFFFF):(FAKE&0x1FFFFFFF)+0x300]) if nspawn[0] else None
    return r, J, m.rw(RNG), ent
def model(kind, code_or_item, pos, flag, mpmax, rngs, failspawn):
    J2 = []
    s = rngs
    if kind == 'rl':
        item, s = choose_model(code_or_item, s)
    else:
        item = code_or_item
    if item == 0: return 0, J2, s, None
    if veto(item, mpmax): return 0, J2, s, None
    if kind == 'sc': pos = pos  # from container
    J2.append(('spawn', 0, 0, item+0x1E, pos[0]&M32, pos[1]&M32, pos[2]&M32, 0))
    if failspawn: return 0, J2, s, None
    ent = bytearray(0x300)
    ent[0x6C:0x70] = (0xFFFFFFFF & ~0x80).to_bytes(4,'little')
    ent[0xB8:0xBC] = (0xA0000).to_bytes(4,'little')
    ent[0x274:0x278] = (1).to_bytes(4,'little')
    ent[0x278:0x27C] = ((600 if ITEM4[item] else -1) & M32).to_bytes(4,'little')
    ent[0x27C:0x280] = (0).to_bytes(4,'little')
    ent[0x280:0x284] = ((flag if kind=='sc' else 0) & M32).to_bytes(4,'little')
    ent[0x284:0x288] = (0xA0000).to_bytes(4,'little')
    J2.append(('sound', 0x54))
    return 1, J2, s, bytes(ent)
bad = 0; n = 0
codes = list(range(0x100)) + [0x100, 0x1FF, 0xFFFFFFFF]
for it in range(12000):
    kind = rng.choice(['rl','sc'])
    if kind == 'rl': c = rng.choice(codes)
    else: c = rng.choice(list(range(0x62)) + [0x26,0x51,0x52]*5 + [0x24,0x45,0x54]*3 + [0])
    pos = tuple(rng.choice([0, 1, 0x100000, 0xFFFFFFFF, rng.getrandbits(32)]) for _ in range(3))
    flag = rng.choice([0, 1, 0x8005, rng.getrandbits(16), 0x7FF])
    mp = rng.choice([0, 0, 1, 5, 0x8000])
    st = rng.getrandbits(32); fs = rng.random() < 0.1
    a = run(kind, c, pos, flag, mp, st, fs)
    b = model(kind, c, pos, flag, mp, st, fs)
    # compare return (low bit), journal, rng, ent
    ok = (a[0]&M32) == b[0] and a[1] == b[1] and a[2] == b[2] and a[3] == b[3]
    n += 1
    if not ok:
        bad += 1
        if bad < 6: print('DIFF', kind, hex(c), pos, flag, mp, fs, a[0], b[0], a[1], b[1], hex(a[2]), hex(b[2]), a[3] and a[3][0x270:0x290].hex(), b[3] and b[3][0x270:0x290].hex())
print('vectors', n, 'diffs', bad)
import capstone
def cov(lo, hi):
    ws = [a for a in range(lo, hi, 4)]
    hit = [a for a in ws if a in m.cov]
    return len(hit), len(ws), [hex(a) for a in ws if a not in m.cov]
print('cov SpawnRandomLoot', cov(0x80032C7C, 0x80032D74))
print('cov SpawnContents', cov(0x80032B90, 0x80032C7C))
print('cov Choose', cov(0x80032968, 0x80032A00))
print('cov veto', cov(0x80032A00, 0x80032A40))
