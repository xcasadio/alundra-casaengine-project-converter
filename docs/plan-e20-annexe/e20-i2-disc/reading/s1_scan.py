"""Scan the whole text of ALUN_CD.EXE for load/store instructions with a given displacement (raw word decode).
usage: python -I s1_scan.py 0x188 [0xcc ...]   -> prints address, op, rt, base, disp, nearest function start (addiu sp,sp,-N)"""
import sys, struct, bisect
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2')
import bx

START, END = 0x80020000, 0x800CA800
OPS = {0x20: 'lb', 0x21: 'lh', 0x22: 'lwl', 0x23: 'lw', 0x24: 'lbu', 0x25: 'lhu', 0x26: 'lwr',
       0x28: 'sb', 0x29: 'sh', 0x2A: 'swl', 0x2B: 'sw', 0x2E: 'swr'}
REG = ['zero', 'at', 'v0', 'v1', 'a0', 'a1', 'a2', 'a3', 't0', 't1', 't2', 't3', 't4', 't5', 't6', 't7',
       's0', 's1', 's2', 's3', 's4', 's5', 's6', 's7', 't8', 't9', 'k0', 'k1', 'gp', 'sp', 'fp', 'ra']
want = [int(a, 0) for a in sys.argv[1:]]
funcs = []
hits = []
for a in range(START, END, 4):
    w = bx.u32(a)
    op = w >> 26
    if op == 0x09 and ((w >> 21) & 31) == 29 and ((w >> 16) & 31) == 29 and (w & 0x8000):
        funcs.append(a)
    if op in OPS:
        disp = w & 0xFFFF
        if disp & 0x8000:
            disp -= 0x10000
        if disp in want:
            hits.append((a, OPS[op], REG[(w >> 16) & 31], REG[(w >> 21) & 31], disp))
for a, op, rt, base, disp in hits:
    i = bisect.bisect_right(funcs, a) - 1
    f = funcs[i] if i >= 0 else 0
    print('%08x  %-4s %-3s 0x%x(%s)   fn~%08x' % (a, op, rt, disp, base, f))
