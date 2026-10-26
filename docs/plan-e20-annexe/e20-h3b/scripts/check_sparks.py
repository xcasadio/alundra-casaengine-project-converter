"""check_sparks.py - E20.h3b T0 (c): checks the slot words printed by k_h3b_sparks.py against the draw formula of C62 (h3_models.py c62):
st1 = the generator before the first draw, st2 = step(st1); force X = hi(st1 * 0x30001) + 0xFFFE8000, force Y = hi(st2 * 0x20001) + 0xFFFF0000 (slot +0x54 and +0x58).
The tick's rng_after is st2 (two draws, nothing else draws in the tick), st1 is recovered with the modular inverse of the multiplier. with the argument 'pf' the printed generator is AFTER the per-frame step (pf_run.py steps inside the tick function), which is undone first.
usage: python -B check_sparks.py <k_h3b_sparks output> [pf]"""
import re, sys
M = 0xFFFFFFFF
A, C = 0x7D2B89DD, 0xE06A02E7
inv = pow(A, -1, 1 << 32)
n = bad = 0
for line in open(sys.argv[1]):
    m = re.match(r't\s*(\d+) rng_after ([0-9a-f]{8}) +calls (\d+).*slot \{(.*)\}', line)
    if not m or m.group(1) == '260': continue
    t, st2 = int(m.group(1)), int(m.group(2), 16)
    if len(sys.argv) > 2 and sys.argv[2] == 'pf': st2 = ((st2 - C) * inv) & M
    st1 = ((st2 - C) * inv) & M
    fx = (((st1 * 0x30001) >> 32) + 0xFFFE8000) & M
    fy = (((st2 * 0x20001) >> 32) + 0xFFFF0000) & M
    sl = dict(re.findall(r"'(0x[0-9a-f]+)': '(0x[0-9a-f]+)'", m.group(4)))
    ok = int(sl.get('0x54', '0'), 16) == fx and int(sl.get('0x58', '0'), 16) == fy
    n += 1; bad += (not ok)
    print('t%3d st1 %08x st2 %08x  force X %08x Y %08x  slot %s %s  %s' % (t, st1, st2, fx, fy, sl.get('0x54'), sl.get('0x58'), 'OK' if ok else 'DIFFERS'))
print('sparks checked %d, differences %d' % (n, bad))
