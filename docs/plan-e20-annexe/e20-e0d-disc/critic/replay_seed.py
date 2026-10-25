import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mips import *
STATS = 0x80180000
def run(entry, mpmax, mp, effect_ok=True, rng=0x12345678):
    m = Mem(); log = []; cpu = CPU(m)
    m.w32(0x80176318, STATS); m.w16(STATS + 4, mp); m.w16(STATS + 6, mpmax)
    m.w32(0x80127d30 + 0x114, 100); m.w32(0x80127d30 + 0x118, 200); m.w32(0x80127d30 + 0x11c, 300)
    m.w32(0x80098708, rng)
    cpu.stubs[0x800490FC] = lambda c: (log.append(('sfx', hex(c.r[4]))), 0)[1]
    cnt = {'n': 0}
    def eff(c):
        sp = c.r[29]
        log.append(('eff', c.r[4], c.r[5], c.r[6], c.r[7], m.r32(sp + 0x10), hex(m.r32(sp + 0x14))))
        cnt['n'] += 1
        return (0x80190000 + 0x100 * cnt['n']) if effect_ok else 0
    cpu.stubs[0x8003BDD8] = eff
    cpu.call(entry, (0x80127d30,))
    s16 = lambda x: x - 65536 if x & 0x8000 else x
    return s16(m.r16(STATS + 4)), s16(m.r16(STATS + 6)), log, m.r32(0x80098708), m
for mpmax in range(0, 6):
    for mp in (0, min(mpmax, 1), mpmax):
        a, b, log, rng, m = run(0x80033E40 if False else 0x8003382C, mpmax, mp)
        effs = [l for l in log if l[0] == 'eff']
        print('MpMax', mpmax, 'Mp', mp, '->', 'Mp', a, 'MpMax', b, 'sfx', [l for l in log if l[0] == 'sfx'], 'effects', len(effs), 'anim set', set(l[3] for l in effs), 'effect id', set(l[2] for l in effs))
# rng steps
_, _, log, rng, m = run(0x8003382C, 0, 0)
A, C = 0x7d2b89dd, 0xe06a02e7
s = 0x12345678
for k in range(8): s = (s * A + C) & M32
print('rng after', hex(rng), 'expected after 8 LCG steps', hex(s))
_, _, log, rng, m = run(0x8003382C, 0, 0, effect_ok=False)
print('effects refused: rng', hex(rng), 'unchanged' if rng == 0x12345678 else 'changed', [l for l in log if l[0]=='sfx'])
# vessel entry for reference sound
