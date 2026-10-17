import random, sys
import exelib as e
from lh import *
T = RANDTAB
def row(code): return [e.u8(T + (code & 0x7F) * 16 + i) for i in range(16)]
def model_choose(code, s):
    """returns (item, newstate, draws)"""
    draws = 0
    c = s32(code)
    while True:
        if c >= 0x100: return 0, s, draws          # slti signed
        if not (c & 0x80):
            return (c if c < 0x62 else 0), s, draws  # slti signed: a negative value passes through
        s = lcg(s); draws += 1
        c = e.u8(T + (c & 0x7F) * 16 + (s >> 28))
w = World()
random.seed(1)
bad = 0; n = 0
codes = list(range(0, 0x120)) + [0x7FFFFFFF, 0xFFFFFFFF, 0x1000, 0x180]
for code in codes:
    for k in range(40):
        s = random.getrandbits(32)
        w.set_rng(s)
        r = w.call(0x80032968, code) & M32
        ms = model_choose(code, s)
        n += 1
        if (r, w.get_rng()) != (ms[0], ms[1]):
            bad += 1
            if bad < 10: print('MISMATCH', hex(code), hex(s), hex(r), hex(w.get_rng()), ms)
print('vectors', n, 'mismatches', bad)
# coverage of 0x80032968..0x800329fc
fn = set(range(0x80032968, 0x80032A00, 4))
cov = w.m.cov & fn
print('instructions in function', len(fn), 'covered', len(cov), 'missing', [hex(a) for a in sorted(fn - cov)])
