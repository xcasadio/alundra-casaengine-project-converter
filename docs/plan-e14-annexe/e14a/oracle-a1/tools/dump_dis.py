"""dump_dis.py - D1: disassembly (address, instruction word, decoded instruction) of the pieces the notes cite.  writes out/dis_passes.txt, out/dis_pad.txt,
out/dis_moveplayer.txt, out/callgraph_moveplayer.txt"""
import os
import subprocess
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib
from lib import dis, u32
import callgraph


def block(out, title, a, n):
    out.write('\n; ===== %s  (0x%08X, %d words)\n' % (title, a, n))
    for i in dis(a, n):
        out.write('%08x %08x %s %s\n' % (i.address, u32(i.address), i.mnemonic, i.op_str))


def main(outdir):
    with open(os.path.join(outdir, 'dis_passes.txt'), 'w', encoding='ascii') as f:
        f.write('; ALUN_CD.EXE (France), text at 0x80020000 = file offset 0x800.  words are little-endian MIPS-I.\n')
        block(f, 'tick function 0x8003B388 (entry to the call of the passes)', 0x8003B388, 40)
        block(f, 'pass 1: 0x80038634 entity slots (complete)', 0x80038634, 41)
        block(f, 'pass 2: 0x800386D0 events (entry, MovePlayer call at 0x800386EC, entity loop head)', 0x800386D0, 24)
        block(f, 'pass 2 tail: 0x800388B4 (second loop head)', 0x800388B4, 12)
        block(f, 'pass 3: 0x80038998 counters (head)', 0x80038998, 42)
        block(f, 'pass 4: 0x800384F4 lists (head)', 0x800384F4, 28)
        block(f, 'pass 5: 0x80038E18 animation (complete)', 0x80038E18, 42)
        block(f, 'pass 5 callee: 0x80038AB4 UpdateAnimation (complete)', 0x80038AB4, 216)
        block(f, 'pass 6: 0x80038364 physics (head)', 0x80038364, 24)
        block(f, 'pass 7: 0x80038E84 effects (head, stubbed in D1)', 0x80038E84, 16)
        block(f, 'pass 8: 0x80039300 hits (head, stubbed in D1)', 0x80039300, 16)
    with open(os.path.join(outdir, 'dis_pad.txt'), 'w', encoding='ascii') as f:
        block(f, 'pad routine 0x8002E250 (a0 = pad struct, a1 = new state)', 0x8002E250, 43)
        block(f, 'pad refresh 0x8002E38C (BIOS read, pad 0 then pad 1)', 0x8002E38C, 15)
        block(f, 'pad init 0x8002E30C / 0x8002E34C', 0x8002E30C, 32)
    with open(os.path.join(outdir, 'dis_moveplayer.txt'), 'w', encoding='ascii') as f:
        a = 0x80031B50
        for i in dis(a, (0x80032958 - a) // 4):
            f.write('%08x %08x %s %s\n' % (i.address, u32(i.address), i.mnemonic, i.op_str))
    root = 0x80031B50
    seen = {}
    todo = [root]
    while todo:
        fn = todo.pop()
        if fn in seen:
            continue
        a, e = callgraph.func_extent(fn)
        cs = callgraph.callees(a, e) if e else []
        seen[fn] = (e, cs)
        todo += cs
    with open(os.path.join(outdir, 'callgraph_moveplayer.txt'), 'w', encoding='ascii') as f:
        f.write('; static call graph (jal targets) reachable from MovePlayer 0x80031B50; extents estimated by scanning to jr $ra\n')
        for fn in sorted(seen):
            e, cs = seen[fn]
            f.write('%08x - %s  calls %s\n' % (fn, ('%08x' % e) if e else '?', ' '.join('%08x' % c for c in sorted(set(cs)))))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
