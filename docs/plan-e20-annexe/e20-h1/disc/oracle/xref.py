"""jal / j / lui+addiu|ori|lw|sw|lh.. cross references over the whole text of ALUN_CD.EXE. Read-only.
Usage: python -B xref.py jal ADDR | data ADDR  (data = any lui/imm pair resolving to ADDR, or an exact word equal to ADDR in the image)"""
import sys, struct
from bdis import *
N = (len(DATA) - 0x800) // 4
def word(i): return struct.unpack_from("<I", DATA, 0x800 + 4*i)[0]
def jal(target):
    out = []
    for i in range(N):
        w = word(i)
        if (w >> 26) in (2, 3):
            t = (((TADDR + 4*i + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
            if t == target: out.append((TADDR + 4*i, 'jal' if (w>>26)==3 else 'j'))
    return out
def data(target):
    out = []
    # lui rt, hi ; later any op with imm lo and base rt, within 24 insns, same register not overwritten (approx)
    hi = (target + 0x8000) >> 16 & 0xFFFF
    lo = target & 0xFFFF
    slo = lo - 0x10000 if lo & 0x8000 else lo
    for i in range(N):
        w = word(i)
        if (w >> 26) == 0xF and (w & 0xFFFF) == hi:
            rt = (w >> 16) & 31
            for j in range(i+1, min(N, i+24)):
                v = word(j)
                op = v >> 26
                if (v >> 21) & 31 == rt and (v & 0xFFFF) == lo and op in (0x08,0x09,0x0D,0x20,0x21,0x23,0x24,0x25,0x28,0x29,0x2B):
                    out.append((TADDR+4*j, TADDR+4*i)); break
                if op == 0xF and (v >> 16) & 31 == rt: break
    return out
if __name__ == "__main__":
    kind, a = sys.argv[1], int(sys.argv[2], 16)
    if kind == 'jal':
        for x in jal(a): print(f"{x[0]:08x} {x[1]}")
    else:
        for x in data(a): print(f"use {x[0]:08x}  lui {x[1]:08x}")
        for i in range(N):
            if word(i) == a: print(f"word {TADDR+4*i:08x}")
