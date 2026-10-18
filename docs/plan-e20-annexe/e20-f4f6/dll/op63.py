import struct
from mipsdis import _data, off, dis
def word(a): return struct.unpack('<I', _data[off(a):off(a)+4])[0]
for op in (0x63, 0x62, 0x1A):
    h = word(0x80098FAC + 4*op)
    print('opcode %02X handler %08X' % (op, h))
    for i in dis(h, h+0x70):
        print('  %08X: %-8s %s' % (i.address, i.mnemonic, i.op_str))
        if i.mnemonic == 'jr': 
            break
