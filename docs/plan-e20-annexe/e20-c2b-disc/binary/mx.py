"""Whole-image scanners for ALUN_CD.EXE (France). Run with python -I (this file adds its own directory to sys.path).
Image = 0x80020000 .. 0x800CA800 (file offset = 0x800 + addr - 0x80020000)."""
import sys, os, bisect, struct
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import bx

TEXT0 = 0x80020000
TEXT1 = 0x800CA800
N = (TEXT1 - TEXT0) // 4
W = [bx.u32(TEXT0 + 4 * i) for i in range(N)]


def word(a):
    return W[(a - TEXT0) >> 2]


FUNCS = []
for _i in range(N):
    _w = W[_i]
    if (_w >> 16) == 0x27BD and (_w & 0x8000) and (_w & 0xFFFF) != 0:
        FUNCS.append(TEXT0 + 4 * _i)


def func_of(a):
    i = bisect.bisect_right(FUNCS, a) - 1
    return FUNCS[i] if i >= 0 else None


def jal_target(w):
    return ((w & 0x03FFFFFF) << 2) | 0x80000000


def callsites(target, ops=(3,)):
    """addresses of jal (or j if ops includes 2) to target"""
    out = []
    for i in range(N):
        w = W[i]
        if (w >> 26) in ops and jal_target(w) == target:
            out.append(TEXT0 + 4 * i)
    return out


def data_refs(value):
    """addresses of 32-bit aligned words equal to value (pointer tables)"""
    return [TEXT0 + 4 * i for i in range(N) if W[i] == value]


def mem_ops(offset, kinds=('sw', 'sh', 'sb', 'lw', 'lh', 'lhu', 'lb', 'lbu')):
    """all load/store instructions with immediate == offset (any base register). returns [(addr, kind, rt, rs)]"""
    opc = {0x2B: 'sw', 0x29: 'sh', 0x28: 'sb', 0x23: 'lw', 0x21: 'lh', 0x25: 'lhu', 0x20: 'lb', 0x24: 'lbu'}
    out = []
    for i in range(N):
        w = W[i]
        op = w >> 26
        if op in opc and (w & 0xFFFF) == (offset & 0xFFFF) and opc[op] in kinds:
            out.append((TEXT0 + 4 * i, opc[op], (w >> 16) & 31, (w >> 21) & 31))
    return out


def const_before(a, reg, depth=12):
    """look back from address a for 'addiu reg,$zero,imm' / 'ori reg,$zero,imm' (same basic block, rough). returns imm or None"""
    k = a - 4
    steps = 0
    while steps < depth:
        w = word(k)
        op = w >> 26
        rt = (w >> 16) & 31
        rs = (w >> 21) & 31
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        if op in (2, 3, 4, 5, 6, 7, 1) or (op == 0 and (w & 0x3F) in (8, 9)):
            return None
        if rt == reg and op in (9, 13) and rs == 0:
            return simm if op == 9 else imm
        if rt == reg and op in (0x23, 0x21, 0x25, 0x20, 0x24, 15, 9, 13):
            return None
        k -= 4
        steps += 1
    return None


def pr(a, b, labels=None):
    for ad, w, m, o in bx.dis(a, b):
        lab = ''
        if labels and ad in labels:
            lab = '   ; ' + labels[ad]
        print("%08x %08x %s %s%s" % (ad, w, m, o, lab))


def name(a):
    f = func_of(a)
    return "%08x(+%x)" % (f, a - f) if f else "?"
