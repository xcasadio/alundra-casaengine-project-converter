"""S2: census of code that writes the hero's Hp (+0x14), TargetAnimationId (+0x88), TargetDirection (+0x8C), +0x1CC, +0x1D0, +0x224.
Hero base 0x80127D30. Two patterns: (a) absolute: lui r,0x8012 ... sw x,(0x7d30+off)(r); (b) register: addiu/ori rY,rX,0x7d30 then sw x,off(rY) in the same function (rough: register not tracked past redefinition)."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx

HERO = 0x80127D30
OFFS = {0x14: 'Hp', 0x18: 'HpMax', 0x88: 'TargetAnim', 0x8C: 'TargetDir', 0x1CC: 'Invuln', 0x1D0: 'HitLock', 0x224: 'Touching', 0x20: 'Frozen20', 0x2C: 'Carried2C'}
opc = {0x2B: 'sw', 0x29: 'sh', 0x28: 'sb'}

def hits(kinds=('sw', 'sh', 'sb')):
    out = []
    # (a) absolute pattern: store with imm == 0x7d30+off and base register previously loaded with lui 0x8012 in the same block (rough)
    for off, nm in OFFS.items():
        imm = (0x7D30 + off) & 0xFFFF
        if 0x7D30 + off >= 0x8000:
            # lui 0x8013 + negative imm
            continue
        for (a, k, rt, rs) in mx.mem_ops(imm, kinds):
            # look back <= 10 words for lui rs,0x8012 (op 15)
            ok = False
            for j in range(1, 11):
                w = mx.word(a - 4 * j)
                if (w >> 26) == 15 and ((w >> 16) & 31) == rs and (w & 0xFFFF) == 0x8012:
                    ok = True
                    break
                if (w >> 26) in (2, 3):
                    break
            if ok:
                out.append((a, k, nm, 'abs', rt, rs))
    return out

def reg_pattern():
    out = []
    # functions that contain 'addiu rY, rX, 0x7d30' preceded by lui rX,0x8012
    sites = []
    for i in range(mx.N):
        w = mx.W[i]
        if (w >> 26) == 9 and (w & 0xFFFF) == 0x7D30:
            rs = (w >> 21) & 31
            rt = (w >> 16) & 31
            # lui before?
            for j in range(1, 8):
                v = mx.W[i - j]
                if (v >> 26) == 15 and ((v >> 16) & 31) == rs and (v & 0xFFFF) == 0x8012:
                    sites.append((mx.TEXT0 + 4 * i, rt))
                    break
    return sites

if __name__ == '__main__':
    print("== (a) absolute-address stores to hero fields ==")
    for (a, k, nm, p, rt, rs) in sorted(hits()):
        print("%08x %s %-10s in %s" % (a, k, nm, mx.name(a)))
    print("== (b) functions materialising &hero (0x80127D30) and stores through that register ==")
    sites = reg_pattern()
    funcs = {}
    for a, rt in sites:
        funcs.setdefault(mx.func_of(a), []).append((a, rt))
    for f, lst in sorted(funcs.items()):
        # end of function = next func start
        idx = mx.FUNCS.index(f)
        end = mx.FUNCS[idx + 1] if idx + 1 < len(mx.FUNCS) else mx.TEXT1
        regs = {rt for a, rt in lst}
        found = []
        for a in range(f, end, 4):
            w = mx.word(a)
            op = w >> 26
            if op in opc and ((w >> 21) & 31) in regs:
                off = w & 0xFFFF
                if off in OFFS:
                    found.append((a, opc[op], OFFS[off]))
        if found:
            print("func %08x: &hero at %s ; %s" % (f, ["%08x" % a for a, rt in lst], found))
