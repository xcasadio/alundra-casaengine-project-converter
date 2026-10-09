"""dump_dis.py - D2: disassembly (address, instruction word, decoded) of the hit pass and its callees.
usage: python -B dump_dis.py [outdir]   writes out/dis_hits.txt"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from lib import dis, u32


def block(out, title, a, n):
    out.write('\n; ===== %s  (0x%08X, %d words)\n' % (title, a, n))
    for i in dis(a, n):
        out.write('%08x %08x %s %s\n' % (i.address, u32(i.address), i.mnemonic, i.op_str))


def main(outdir):
    with open(os.path.join(outdir, 'dis_hits.txt'), 'w', encoding='ascii') as f:
        f.write('; ALUN_CD.EXE (France), text at 0x80020000 = file offset 0x800. words are little-endian MIPS-I.\n')
        block(f, 'UpdateBalanceRecords 0x80039300 (hit pass)', 0x80039300, (0x80039188 - 0x80039188 + 0x80039780 - 0x80039300) // 4)
        block(f, 'spark routine 0x80039188', 0x80039188, (0x80039300 - 0x80039188) // 4)
        block(f, 'ComputeNewHp 0x8003A374', 0x8003A374, (0x8003A5A0 - 0x8003A374) // 4)
        block(f, 'ResolveBalanceTarget 0x8004464C', 0x8004464C, (0x80044830 - 0x8004464C) // 4)
        block(f, 'balance level / record select 0x80044520', 0x80044520, 60)
        block(f, 'lists 0x800384F4', 0x800384F4, 110)
        block(f, 'hit box 0x80038064', 0x80038064, 36)
        block(f, 'counters 0x80038998', 0x80038998, 42)
        block(f, 'EffectCreate 0x8003BDD8 head', 0x8003BDD8, 40)
        block(f, 'EffectAttach 0x8003BE74 head', 0x8003BE74, 40)
    print('ok')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
