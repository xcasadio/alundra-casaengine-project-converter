"""c0_scan.py - critic's own scans of the whole text segment.
  jal TARGET        : callers (jal/j) of an address
  off OFFSET [store|load|all] : load/store instructions whose immediate == OFFSET (any base register) -> candidate accesses to the field
  lui-pair VALUE    : lui/addiu|lw|sw pairs that build an absolute address (hi<<16 + lo) == VALUE (linear, same register within 6 insns)
  words VALUE       : aligned 32-bit words in the whole image equal to VALUE (data pointers)
Output is plain text."""
import sys
from cmips import *

OPS = {32: "lb", 33: "lh", 35: "lw", 36: "lbu", 37: "lhu", 40: "sb", 41: "sh", 43: "sw", 34: "lwl", 38: "lwr", 42: "swl", 46: "swr"}


def insns():
    a = BASE
    while a < IMG_END - 3:
        yield a, img_u32(a)
        a += 4


def jal(target):
    out = []
    for a, i in insns():
        if (i >> 26) in (2, 3) and ((a & 0xF0000000) | ((i & 0x3FFFFFF) << 2)) == target:
            out.append((a, "jal" if i >> 26 == 3 else "j"))
    return out


def off(offset, kind="all"):
    out = []
    for a, i in insns():
        op = i >> 26
        if op in OPS and s16(i & 0xFFFF) == offset:
            isst = op in (40, 41, 43, 42, 46)
            if kind == "all" or (kind == "store" and isst) or (kind == "load" and not isst):
                out.append((a, OPS[op], (i >> 21) & 31, (i >> 16) & 31))
    return out


def lui_pairs(value):
    hi = (value >> 16) & 0xFFFF
    lo = value & 0xFFFF
    slo = s16(lo)
    if slo < 0:
        hi2 = (hi + 1) & 0xFFFF
    else:
        hi2 = hi
    out = []
    ins = list(insns())
    for k, (a, i) in enumerate(ins):
        if (i >> 26) == 15 and (i & 0xFFFF) == hi2:
            r = (i >> 16) & 31
            for j in range(k + 1, min(k + 12, len(ins))):
                a2, i2 = ins[j]
                op2 = i2 >> 26
                if op2 in (35, 32, 33, 36, 37, 40, 41, 43, 9, 8) and ((i2 >> 21) & 31) == r and s16(i2 & 0xFFFF) == slo:
                    out.append((a, a2, OPS.get(op2, "addiu")))
                # lui rewritten
                if op2 == 15 and ((i2 >> 16) & 31) == r:
                    break
    return out


def words(v):
    out = []
    a = BASE
    while a < IMG_END - 3:
        if img_u32(a) == v:
            out.append(a)
        a += 4
    return out


if __name__ == "__main__":
    c = sys.argv[1]
    if c == "jal":
        for a, k in jal(int(sys.argv[2], 16)):
            print("%08x %s" % (a, k))
    elif c == "off":
        for a, n, rs, rt in off(int(sys.argv[2], 0), sys.argv[3] if len(sys.argv) > 3 else "all"):
            print("%08x %-3s base=r%d reg=r%d" % (a, n, rs, rt))
    elif c == "lui":
        for a, a2, k in lui_pairs(int(sys.argv[2], 16)):
            print("%08x %08x %s" % (a, a2, k))
    elif c == "words":
        for a in words(int(sys.argv[2], 16)):
            print("%08x" % a)
