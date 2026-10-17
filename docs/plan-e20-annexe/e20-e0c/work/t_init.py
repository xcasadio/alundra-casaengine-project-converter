import random
from lh import *
import models as M
random.seed(11)
w = World(); m = w.m
REC = 0x80160000; HDR = 0x80160400; GBANK = 0x801EB344; TBANK = 0x801EBA40
bad = 0; n = 0
codes = [0, 0, 0x24, 0x61, 0x62, 0x7F, 0x80, 0x81, 0x82, 0x83, 0xF6, 0xF8, 0xFA, 0xFB, 0xFC, 0xFF, 0xFD]
flags_pool = [0, 1, 5, 31, 32, 0x7FF, 0x800, 0x7FFF, 0x8000, 0x8001, 0x801F, 0x87FF, 0x8800, 0xFFFF, 1720, 1665, 1351, 0x8000 | 1720]
for it in range(20000):
    base = ENT0 + 7 * STRIDE
    for i in range(0x294): m.wb(base + i, 0)
    has = random.random() < 0.7
    f10 = random.choice(flags_pool) if random.random() < 0.6 else random.getrandbits(16)
    c12 = random.choice(codes); hc = random.choice(codes)
    for i in range(0x20): m.wb(REC + i, 0); m.wb(HDR + i, 0)
    m.wh(REC + 0x10, f10); m.wb(REC + 0x12, c12); m.wb(REC + 0x13, random.getrandbits(8)); m.wb(HDR + 0x1F, hc)
    m.ww(base + 0x44, REC if has else 0); m.ww(base + 0x64, HDR)
    m.ww(base + 0x40, 0xDEADBEEF); m.ww(base + 0x3C, 0xDEADBEEF)
    gb = [random.getrandbits(32) if random.random() < 0.5 else 0 for _ in range(64)]
    tb = [random.getrandbits(32) if random.random() < 0.5 else 0 for _ in range(64)]
    # make the banks hit the flag we use often: set the bit with 50 percent probability
    for ff in ((f10,) if (f10 & 0x7FFF) < 0x800 else ()):
        bank, name = (tb, 'T') if ff & 0x8000 else (gb, 'G')
        idx = ((ff >> 3) & 0xFFC) // 4
        if random.random() < 0.5: bank[idx] |= (1 << (ff & 31))
        else: bank[idx] &= ~(1 << (ff & 31)) & M32
    for i in range(64):
        m.ww(GBANK + 4 * i, gb[i]); m.ww(TBANK + 4 * i, tb[i])
    st = random.getrandbits(32); w.set_rng(st)
    w.call(0x80032A40, base)
    exp = M.init_contents(dict(f10=f10, c12=c12) if has else None, hc, gb, tb, st)
    got = (m.rw(base + 0x3C), m.rw(base + 0x40), w.get_rng())
    n += 1
    if got != (exp[0], exp[1], exp[2]):
        bad += 1
        if bad < 8: print('MISMATCH IC', has, hex(f10), hex(c12), hex(hc), [hex(x) for x in got], [hex(x) for x in exp])
print('InitializeContents vectors', n, 'mismatches', bad)
fn = set(range(0x80032A40, 0x80032B28, 4))
print(' coverage', len(m.cov & fn), '/', len(fn), 'missing', [hex(a) for a in sorted(fn - m.cov)])
