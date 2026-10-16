import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
# find sw/sh/sb (store) with a given 16-bit immediate offset
def find(imm, ops=(0x2B,0x29,0x28)):
    out = []
    n = (len(lib.DATA) - lib.OFF) // 4
    for i in range(n):
        w = struct.unpack_from('<I', lib.DATA, lib.OFF + 4 * i)[0]
        if (w >> 26) in ops and (w & 0xFFFF) == imm:
            out.append((lib.BASE + 4 * i, w))
    return out
if __name__ == '__main__':
    imm = int(sys.argv[1], 16)
    ops = (0x2B,0x29,0x28) if len(sys.argv) < 3 else tuple(int(x,16) for x in sys.argv[2].split(','))
    for a, w in find(imm, ops):
        ins = lib.dis(a, 1)[0]
        print('%08X  %s %s' % (a, ins.mnemonic, ins.op_str))
