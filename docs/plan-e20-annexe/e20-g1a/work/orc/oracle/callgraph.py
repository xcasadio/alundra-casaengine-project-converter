"""callgraph.py - static call graph (jal targets) reachable from a root, over ALUN_CD.EXE; also prints function extents by scanning to jr $ra.
usage: python -B callgraph.py 0x80031B50 [depth]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
from lib import dis, u32

def func_extent(a, maxn=3000):
    """walk linearly from a until a 'jr $ra' followed by its delay slot, ignoring branches that jump beyond (simple, may under-approximate)"""
    ins = dis(a, maxn)
    hi = a
    end = None
    for i, x in enumerate(ins):
        # track furthest forward branch target
        if x.mnemonic in ('b', 'beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'j'):
            t = x.op_str.split(',')[-1].strip()
            try:
                tv = int(t, 16)
                if tv > hi and x.mnemonic != 'j': hi = tv
                if x.mnemonic == 'j' and a < tv < a + 0x4000 and tv > hi: hi = tv
            except ValueError:
                pass
        if x.mnemonic == 'jr' and x.op_str.strip() == '$ra' and x.address >= hi:
            end = x.address + 4
            break
    return a, end

def callees(a, end):
    out = []
    for x in dis(a, (end - a) // 4):
        if x.mnemonic == 'jal':
            out.append(int(x.op_str, 16))
    return out

if __name__ == '__main__':
    root = int(sys.argv[1], 16)
    seen = {}
    todo = [root]
    while todo:
        f = todo.pop()
        if f in seen: continue
        a, e = func_extent(f)
        cs = callees(a, e) if e else []
        seen[f] = (e, cs)
        todo += cs
    for f in sorted(seen):
        e, cs = seen[f]
        print('%08x - %s  size %s  calls %s' % (f, ('%08x' % e) if e else '?', ('%d' % ((e - f)//4)) if e else '?', ' '.join('%08x' % c for c in sorted(set(cs)))))
