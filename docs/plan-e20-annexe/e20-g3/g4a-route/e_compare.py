"""e_compare.py - E20.g4a T0 (e) and (d): the DLL's wall north of the door (probe E_Calage, e.txt) against the binary's table (critic/out/w3_door.txt) and the law.
Usage: python -B -I e_compare.py <w3_door.txt> <e.txt>"""
import re, sys
from fractions import Fraction as F
w3 = {}
for l in open(sys.argv[1], encoding='utf-8'):
    m = re.match(r'\s+y0 (\d+\.\d+) -> \((\d+), (\d+\.\d+)\)', l)
    if m:
        w3[F(m.group(1))] = F(m.group(3))
dll = {}
for l in open(sys.argv[2], encoding='utf-8'):
    m = re.match(r'E k=(\d) y0=(\d+\.\d+) -> y=(\d+\.\d+) slides=(\d+)', l)
    if m:
        dll.setdefault(int(m.group(1)), {})[F(m.group(2))] = (F(m.group(3)), int(m.group(4)))
out = ['(e) PosY at the first tick x <= 690, hero started at (720, y0), Left held (the pilot lets go in a poll image), y0 = 286.0000 .. 295.0000 by 0.0625 (145 starts)',
       'binary = critic/out/w3_door.txt ; law = 295 + ((y0 - 295) mod 0.5) ; slides law = (PosY - y0) / 0.5']
for k in sorted(dll):
    d = dll[k]
    eq_bin = sum(1 for y0 in w3 if y0 in d and d[y0][0] == w3[y0])
    eq_law = sum(1 for y0 in d if d[y0][0] == 295 + ((y0 - 295) % F(1, 2)))
    eq_sl = sum(1 for y0 in d if d[y0][1] == (d[y0][0] - y0) / F(1, 2))
    out.append('k=%d : %d starts ; PosY equal to the binary for %d of %d ; equal to the law for %d ; slides = (PosY - y0)/0.5 for %d ; slides min %d max %d' % (
        k, len(d), eq_bin, len(w3), eq_law, eq_sl, min(v[1] for v in d.values()), max(v[1] for v in d.values())))
    bad = [(float(y0), float(d[y0][0]), float(w3[y0]), d[y0][1]) for y0 in w3 if y0 in d and d[y0][0] != w3[y0]]
    for b in bad[:10]:
        out.append('   DIFF y0=%.4f dll=%.7f binary=%.7f slides=%d' % b)
print('\n'.join(out))
open('e_compare.txt', 'w').write('\n'.join(out) + '\n')
