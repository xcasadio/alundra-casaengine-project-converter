"""disrange.py - disassemble ranges of the binary: python -B disrange.py 0x8003FFD4 0x80040048 [more start end pairs]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib


def main(args):
    for i in range(0, len(args), 2):
        a, b = int(args[i], 16), int(args[i + 1], 16)
        for ins in lib.dis(a, (b - a) // 4):
            w = lib.u32(ins.address)
            print('%08X  %08x  %-8s %s' % (ins.address, w, ins.mnemonic, ins.op_str))
        print()


if __name__ == '__main__':
    main(sys.argv[1:])
