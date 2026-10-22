"""Own disassembly of the four handlers (capstone, from the binary) and the table check: the handler address of each opcode is read from the table 0x80098FAC + 4 * op.
Usage: python -B disasm4.py OUTFILE"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib  # noqa: E402

out = open(sys.argv[1], 'w', encoding='utf-8', newline='\n')
for op in (0x3C, 0x3D, 0x60, 0x61):
    h = lib.u32(0x80098FAC + 4 * op)
    out.write('== opcode 0x%02X handler 0x%08X (table entry at 0x%08X)\n' % (op, h, 0x80098FAC + 4 * op))
    # disassemble until the first 'jr $ra' and its delay slot
    a = h
    done = False
    n = 0
    while not done and n < 200:
        ins = lib.dis(a, 1)[0]
        out.write('0x%08x  %-7s %s\n' % (ins.address, ins.mnemonic, ins.op_str))
        if ins.mnemonic == 'jr' and ins.op_str == '$ra':
            nxt = lib.dis(a + 4, 1)[0]
            out.write('0x%08x  %-7s %s\n' % (nxt.address, nxt.mnemonic, nxt.op_str))
            done = True
        a += 4
        n += 1
    out.write('\n')
out.close()
