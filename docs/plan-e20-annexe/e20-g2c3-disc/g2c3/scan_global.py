"""scan_global.py - who reads/writes the word at 0x8013DA20 (C71 state 4 stores 1 there).  A MIPS access to 0x8013DA20 is `lui r,0x8014 ; xx rt,-0x25e0(r)`.
Run: python -B -I -X utf8 scan_global.py [hexaddr]"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bx
target = int(sys.argv[1], 16) if len(sys.argv) > 1 else 0x8013DA20
hi = (target + 0x8000) >> 16
lo = (target & 0xFFFF)
LS = {0x20: 'lb', 0x21: 'lh', 0x23: 'lw', 0x24: 'lbu', 0x25: 'lhu', 0x28: 'sb', 0x29: 'sh', 0x2B: 'sw', 0x22: 'lwl', 0x26: 'lwr'}
n = (len(bx.data) - 0x800) // 4
base = 0x80020000
found = []
for i in range(40, n):
    a = base + 4 * i
    w = bx.u32(a)
    op = w >> 26
    if op in LS and (w & 0xFFFF) == lo:
        rs = (w >> 21) & 31
        ok = False
        for k in range(1, 40):
            pw = bx.u32(a - 4 * k)
            if pw >> 26 == 0x0F and ((pw >> 16) & 31) == rs:
                ok = ((pw & 0xFFFF) == hi)
                break
        if ok:
            found.append((a, LS[op], w))
print('target 0x%08X: %d accesses' % (target, len(found)))
for a, m, w in found:
    print('%08x %s' % (a, m))
