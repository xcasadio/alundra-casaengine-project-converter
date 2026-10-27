"""Reads, from ALUN_CD.EXE (France), the data the earth-2 driver 0x800354D0 uses: the table 0x80023504 (tick u16, p s16, until the sentinel 0xFFFF) and the 32-direction tables X 0x80023654 / Y 0x80023694 (s16),
and computes the position of each rock of a given pair of draws.  Read-only."""
import struct
import sys
from cmips import IMG, BASE, TEXT_OFF


def rd(addr, n):
    o = addr - BASE + TEXT_OFF
    return IMG[o:o + n]


def u16(a):
    return struct.unpack('<H', rd(a, 2))[0]


def s16(a):
    return struct.unpack('<h', rd(a, 2))[0]


ents = []
a = 0x80023504
while True:
    t = u16(a)
    if t == 0xFFFF:
        ents.append(('sentinel', t, s16(a + 2)))
        break
    ents.append((len(ents), t, s16(a + 2)))
    a += 4
print('table 0x80023504 (index, tick, p):')
for e in ents:
    print('  ', e)
X = [s16(0x80023654 + 2 * i) for i in range(32)]
Y = [s16(0x80023694 + 2 * i) for i in range(32)]
print('X[32]', X)
print('Y[32]', Y)
# radius in px for r1=0 or 8 etc at p = 512 and 7680
for p in (512, 7680):
    print('p=%d: X radius max %.1f px, Y radius max %.1f px' % (p, max(abs(v) for v in X) * (p + 0xA00) / 65536.0, max(abs(v) for v in Y) * (p + 0xA00) / 65536.0))
