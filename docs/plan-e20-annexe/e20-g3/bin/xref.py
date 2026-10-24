"""xref.py - cross references in ALUN_CD.EXE (France), read-only.
Usage: python -B xref.py ADDR [ADDR...]   (hex virtual addresses)
Prints: every jal/j to ADDR, every raw 32-bit word equal to ADDR (function-pointer tables), and every lui/addiu|ori pair that materialises ADDR
(same register, within 8 instructions)."""
import sys
import struct
import capstone
import exelib

D = exelib.DATA
T = exelib.T_ADDR
N = exelib.T_SIZE


def words():
    for a in range(T, T + N, 4):
        yield a, struct.unpack_from('<I', D, exelib.off(a))[0]


def main(targets):
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
    md.skipdata = True
    code = D[exelib.off(T):exelib.off(T) + N]
    ins = list(md.disasm(code, T))
    byaddr = {i.address: k for k, i in enumerate(ins)}
    for tgt in targets:
        print('== %#010x' % tgt)
        for i in ins:
            if i.mnemonic in ('jal', 'j') and i.op_str.startswith('0x') and int(i.op_str, 16) == tgt:
                print('  %s %#010x  %s %s' % ('call' if i.mnemonic == 'jal' else 'jump', i.address, i.mnemonic, i.op_str))
        for a, w in words():
            if w == tgt:
                print('  word %#010x' % a)
        hi = (tgt + 0x8000) >> 16
        lo = tgt & 0xFFFF
        for k, i in enumerate(ins):
            if i.mnemonic == 'lui':
                ops = [x.strip() for x in i.op_str.split(',')]
                try:
                    v = int(ops[1], 0)
                except ValueError:
                    continue
                if v == hi:
                    for j in range(k + 1, min(k + 9, len(ins))):
                        n = ins[j]
                        if n.mnemonic in ('addiu', 'ori'):
                            o2 = [x.strip() for x in n.op_str.split(',')]
                            try:
                                imm = int(o2[2], 0)
                            except ValueError:
                                continue
                            if (imm & 0xFFFF) == lo and o2[1] == ops[0]:
                                print('  lui/%s %#010x..%#010x' % (n.mnemonic, i.address, n.address))
                                break


if __name__ == '__main__':
    main([int(x, 16) for x in sys.argv[1:]])
