"""S9: every store to the control-flags word [0x800DC4B8] (lui 0x800E; sw ?, -0x3b48(r)), with the constant or the ori/andi mask when recoverable."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx
off = (-0x3b48) & 0xFFFF
def lui_ok(a, rs):
    for j in range(1, 10):
        w = mx.word(a - 4*j)
        if (w >> 26) == 15 and ((w >> 16) & 31) == rs and (w & 0xFFFF) == 0x800E:
            return True
        if (w >> 26) in (2, 3): return False
    return False
def desc_store(a, rt):
    """look back for the instruction that produced rt: ori/andi/addiu/or/and"""
    for j in range(1, 8):
        w = mx.word(a - 4*j)
        op = w >> 26
        if ((w >> 16) & 31) == rt and op in (13, 12, 9):
            nm = {13: 'ori', 12: 'andi', 9: 'addiu'}[op]
            return "%s 0x%x" % (nm, w & 0xFFFF if op != 9 else ((w & 0xFFFF) if not (w & 0x8000) else (w & 0xFFFF) - 0x10000))
        if ((w >> 11) & 31) == rt and op == 0:
            return "r-type %02x" % (w & 0x3f)
    return '?'
print("stores to [0x800DC4B8]:")
for (a, k, rt, rs) in mx.mem_ops(off, ('sw',)):
    if lui_ok(a, rs):
        if rt == 0:
            print("  %08x sw $zero              in %s" % (a, mx.name(a)))
        else:
            print("  %08x sw r%d (%s) in %s" % (a, rt, desc_store(a, rt), mx.name(a)))
