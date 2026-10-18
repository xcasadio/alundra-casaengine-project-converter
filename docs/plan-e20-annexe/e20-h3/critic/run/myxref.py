"""myxref.py - critic's own caller scan: every jal/j to a target, every lui+addiu/ori/lw/sw pair that builds a given address. usage: python -B myxref.py 0x8004E5C4 [more targets...]"""
import sys, struct
import lib
N = (len(lib.DATA) - lib.OFF) // 4
W = [struct.unpack_from('<I', lib.DATA, lib.OFF + 4*i)[0] for i in range(N)]
def addr(i): return lib.BASE + 4*i
def jal_callers(t):
    out = []
    for i, w in enumerate(W):
        if (w >> 26) in (2, 3) and (((addr(i) + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)) == t:
            out.append((addr(i), 'jal' if (w >> 26) == 3 else 'j'))
    return out
def func_start(a):
    # nearest preceding 'addiu sp, sp, -N' prologue
    i = (a - lib.BASE) // 4
    while i > 0:
        w = W[i]
        if (w >> 16) == 0x27BD and (w & 0x8000): return addr(i)
        i -= 1
    return None
def data_refs(t):
    """lui rX,hi ; (addiu|ori|lw|lh|lb|lbu|lhu|sw|sh|sb) rY, lo(rX) with hi<<16 + sext(lo) == t within 8 instructions"""
    out = []
    hi = (t + 0x8000) >> 16 & 0xFFFF
    lo = t - (hi << 16)
    for i, w in enumerate(W):
        if (w >> 26) == 0xF and (w & 0xFFFF) == hi:
            rt = (w >> 16) & 31
            for j in range(i+1, min(i+10, N)):
                x = W[j]
                op = x >> 26
                if op in (9, 0xD, 0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B) and ((x >> 21) & 31) == rt:
                    imm = x & 0xFFFF
                    simm = imm - 0x10000 if imm & 0x8000 else imm
                    if op == 0xD: v = (hi << 16) | imm
                    else: v = (hi << 16) + simm
                    if (v & 0xFFFFFFFF) == t: out.append((addr(j), hex(op)))
                if ((x >> 16) & 31) == rt and op in (0xF, 9, 0x23, 0x20, 0x24, 0x21, 0x25) : break
    return out
for a in sys.argv[1:]:
    t = int(a, 16)
    print('== %s' % a)
    for (c, k) in jal_callers(t):
        fs = func_start(c)
        print('  %s at %08x in func ~%s' % (k, c, hex(fs) if fs else '?'))
    for (c, k) in data_refs(t):
        fs = func_start(c)
        print('  data-ref op%s at %08x in func ~%s' % (k, c, hex(fs) if fs else '?'))
