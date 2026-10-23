"""scan130.py - every store (sw) and load (lw) with displacement 0x130 in the text of ALUN_CD.EXE (France), with the enclosing function start (the nearest preceding
'addiu $sp,$sp,-N' prologue) - the writers and readers of Entity +0x130 (XCollisionEntity) among all registers (a heuristic: the displacement alone does not prove the base is an
entity). Read-only."""
import sys, os, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'work'))
import exelib
lo, hi = 0x80020000, 0x80020000 + exelib.T_SIZE
funcs = []
for a in range(lo, hi, 4):
    w = exelib.u32(a)
    if (w >> 16) == 0x27BD and (w & 0x8000):      # addiu sp,sp,-N
        funcs.append(a)
import bisect
def fn(a):
    i = bisect.bisect_right(funcs, a) - 1
    return funcs[i] if i >= 0 else 0
res = {'sw': [], 'lw': []}
for a in range(lo, hi, 4):
    w = exelib.u32(a)
    op = w >> 26
    if (w & 0xFFFF) == 0x130 and op in (0x2B, 0x23):
        res['sw' if op == 0x2B else 'lw'].append(a)
for k in ('sw', 'lw'):
    print(k, len(res[k]))
    byf = {}
    for a in res[k]:
        byf.setdefault(fn(a), []).append(a)
    for f, aa in sorted(byf.items()):
        print('  func 0x%08X: %s' % (f, ' '.join('%08x' % x for x in aa)))
