"""S7: readers of the hero target-animation (+0x88, abs 0x80127DB8) and of hero +0x1CC (abs 0x80127EFC), +0x224 (0x80127F54), +0x1D0 (0x80127F00), and any compare with 0x31/0x3A/0x1C/0x3B/0x39 nearby."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx
def near_consts(a, span=12):
    vals = []
    for j in range(-2, span):
        w = mx.word(a + 4*j)
        op = w >> 26
        if op in (9, 13) and ((w >> 21) & 31) == 0:   # addiu/ori rt,$zero,imm
            vals.append((j, (w & 0xFFFF)))
        if op == 10 or op == 11:  # slti/sltiu
            vals.append((j, ('slti', w & 0xFFFF)))
    return vals
for label, imm in (("hero+0x88 (0x7db8)", 0x7db8), ("hero+0x1CC (0x7efc)", 0x7efc), ("hero+0x1D0 (0x7f00)", 0x7f00), ("hero+0x224 (0x7f54)", 0x7f54)):
    print("== reads of", label)
    for (a, k, rt, rs) in mx.mem_ops(imm, kinds=('lw','lh','lhu','lb','lbu')):
        # require lui 0x8012 recent
        ok = False
        for j in range(1, 9):
            w = mx.word(a - 4*j)
            if (w >> 26) == 15 and ((w >> 16) & 31) == rs and (w & 0xFFFF) == 0x8012:
                ok = True; break
            if (w >> 26) in (2, 3): break
        if ok:
            cs = [c for c in near_consts(a) if c[1] in (0x31, 0x3a, 0x1c, 0x3b, 0x39, 0x3e, 0x3c, 0x4e) or isinstance(c[1], tuple)]
            print("  %08x in %s ; consts near: %s" % (a, mx.name(a), cs))
