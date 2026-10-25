"""S3: for each store to hero +0x88 outside MovePlayer, the constant stored (if recoverable), grouped by function."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx
import s2_herowrites as s2

opc = {0x2B: 'sw'}
sites = s2.reg_pattern()
funcs = {}
for a, rt in sites:
    funcs.setdefault(mx.func_of(a), []).append((a, rt))
rows = []
for f, lst in sorted(funcs.items()):
    idx = mx.FUNCS.index(f)
    end = mx.FUNCS[idx + 1] if idx + 1 < len(mx.FUNCS) else mx.TEXT1
    regs = {rt for a, rt in lst}
    for a in range(f, end, 4):
        w = mx.word(a)
        if (w >> 26) == 0x2B and ((w >> 21) & 31) in regs and (w & 0xFFFF) in (0x88, 0x8C, 0x14, 0x1CC):
            rt = (w >> 16) & 31
            c = mx.const_before(a, rt)
            rows.append((f, a, w & 0xFFFF, c))
for f, a, off, c in rows:
    print("func %08x store %08x +0x%x value %s" % (f, a, off, ('0x%x' % c) if c is not None else '?'))
# absolute ones
print('-- absolute --')
for (a, k, nm, p, rt, rs) in sorted(s2.hits()):
    if nm in ('TargetAnim', 'Hp', 'Invuln', 'TargetDir'):
        c = mx.const_before(a, rt)
        print("%08x %s %s value %s in %s" % (a, k, nm, ('0x%x' % c) if c is not None else '?', mx.name(a)))
