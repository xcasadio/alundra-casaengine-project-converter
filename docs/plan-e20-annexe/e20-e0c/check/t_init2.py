import random, h2
from h2 import *
import exelib
rng = random.Random(11)
TAB = [exelib.u8(RANDTAB + i) for i in range(128*16)]
m = mk()
REC = 0x80150000; HDR = 0x80150200
BANK_PERM = 0x801EBA40; BANK_TMP = 0x801EB344
def choose_model(code, s):
    if code & 0x80000000: code -= 1<<32
    while code < 0x100:
        if code & 0x80:
            s = lcg(s); code = TAB[(code & 0x7F)*16 + (s >> 28)]
        else:
            return (code if code < 0x62 else 0), s
    return 0, s
def run(norec, f, recbyte, hdrbyte, bankwords, s):
    e = ENT0 + 4*STRIDE
    for i in range(0x2A0): m.wb(e+i, 0)
    m.ww(e+0x44, 0 if norec else REC); m.ww(e+0x64, HDR)
    m.wb(HDR+0x1F, hdrbyte); m.wh(REC+0x10, f); m.wb(REC+0x12, recbyte)
    for k, v in bankwords.items(): m.ww(k, v)
    m.ww(RNG, s)
    m.ww(e+0x3C, 0xDEAD); m.ww(e+0x40, 0xBEEF)
    m.tracing = True
    m.call(0x80032A40, e)
    m.tracing = False
    return m.rw(e+0x3C), m.rw(e+0x40), m.rw(RNG)
def model(norec, f, recbyte, hdrbyte, bankwords, s):
    if norec:
        f40 = 0; code = hdrbyte
    else:
        if (f & 0x7FFF) >= 0x800: f = 0
        f40 = f
        code = None
        if f != 0:
            base = BANK_PERM if f & 0x8000 else BANK_TMP
            w = bankwords.get(base + (((f >> 5) & 0x3FF)*4), None)
            assert w is not None
            if w & (1 << (f & 31)): code = 0
        if code is None:
            code = recbyte if recbyte != 0 else hdrbyte
    it, s2 = choose_model(code, s)
    return it, f40, s2
bad = 0; n = 0
for it in range(20000):
    norec = rng.random() < 0.2
    f = rng.choice([0, 0, rng.randrange(0, 0x800), 0x8000 | rng.randrange(0, 0x800), rng.getrandbits(16), 0x7FF, 0x800, 0x8800, 0xFFFF, 0x1F, 0x20])
    recbyte = rng.choice([0, 0, 0x82, 0xFB, 0x45, rng.getrandbits(8)])
    hdr = rng.choice([0, 0xFC, 0x82, 0x24, rng.getrandbits(8)])
    # bank words: set the word concerned randomly, nothing else needed; but the model needs its address
    ff = f if (f & 0x7FFF) < 0x800 else 0
    bw = {}
    if ff:
        base = BANK_PERM if ff & 0x8000 else BANK_TMP
        bw[base + (((ff >> 5) & 0x3FF)*4)] = rng.choice([0, 0xFFFFFFFF, 1 << (ff & 31), ~(1 << (ff & 31)) & M32, rng.getrandbits(32)])
    s = rng.getrandbits(32)
    a = run(norec, f, recbyte, hdr, bw, s)
    # model needs bank words for the case f!=0 only
    b = model(norec, f, recbyte, hdr, bw, s)
    n += 1
    if a != b:
        bad += 1
        if bad < 6: print('DIFF', norec, hex(f), hex(recbyte), hex(hdr), bw, [hex(x) for x in a], [hex(x) for x in b])
print('vectors', n, 'diffs', bad)
ws = list(range(0x80032A40, 0x80032B28, 4)); hit = [a for a in ws if a in m.cov]
print('cov', len(hit), len(ws), [hex(a) for a in ws if a not in m.cov])
