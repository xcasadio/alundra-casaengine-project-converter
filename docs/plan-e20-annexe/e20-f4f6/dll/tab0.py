import struct
from mipsdis import _data, off, dis
def word(a): return struct.unpack('<I', _data[off(a):off(a)+4])[0]
TAB = {'A':0x800C4B34, 'C':0x800C4F34, 'D':0x800C5334, 'E':0x800C5734, 'F':0x800C5B34}
for slot, base in TAB.items():
    h = word(base)          # entry 0
    print('slot', slot, 'entry 0 ->', '%08X' % h)
    n = 0
    for i in dis(h, h+0x30):
        print('   %08X: %-6s %s' % (i.address, i.mnemonic, i.op_str))
        n += 1
        if i.mnemonic == 'jr':
            # delay slot
            continue
        if n > 8: break
