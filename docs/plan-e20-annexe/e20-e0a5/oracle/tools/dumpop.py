import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
B = 0x80098FAC
for op in (0x2C, 0x2D, 0x2E, 0x89, 0x8A, 0x8B, 0x8C):
    print('opcode %02X -> %08X' % (op, lib.u32(B + 4 * op)))
