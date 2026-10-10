"""f1-check: run the binary's sort driver 0x800399B8 after each tick of C2-steel and journal the hero's +0x1BC against the formula."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import ext
import probe_ext as PE
import world as W

HERO = ext.HERO


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def formula(m):
    y = m.peeks(HERO + 0x118)
    z = m.peeks(HERO + 0x11C)
    idsv = m.peeks(HERO + 0x1A8)
    return (((y + (idsv << 16)) & 0xFFFF0000) + s16(z >> 16)) & 0xFFFFFFFF


rows = []


def after(m, t):
    W.run_pass(m, 0x800399B8)
    k = m.peek(HERO + 0x1BC)
    f = formula(m)
    rows.append((t, m.peeks(HERO + 0x88) if False else m.peek(HERO + 0x88), m.peeks(HERO + 0x1A8), m.peeks(HERO + 0x118), m.peeks(HERO + 0x11C), k, f))


spec = PE.spec_of('C2-steel')
res = ext.run_ext(spec, after_tick=after)
bad = [r for r in rows if r[5] != r[6]]
with open(os.path.join(HERE, 'keyprobe.txt'), 'w') as fh:
    for r in rows:
        fh.write('t%3d anim88=%X idsv=%d y=%d z=%d key=%08X formula=%08X %s\n' % (r[0], r[1], r[2], r[3], r[4], r[5], r[6], 'OK' if r[5] == r[6] else 'DIFF'))
print('ticks', len(rows), 'mismatches', len(bad))
print('distinct idsv', sorted(set(r[2] for r in rows)))
for r in rows[:3] + rows[30:34] + rows[88:93]:
    print(r)
