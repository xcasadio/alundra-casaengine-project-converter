"""s4_drift_tables.py - dump of the slide tables of 0x800366FC read from ALUN_CD.EXE (France): X at 0x80028B94, Y at 0x80028BD4 (16 signed words each), and the 32-direction tables 0x80023654/0x80023694."""
import sys, struct
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2')
import bx


def s32(a):
    return struct.unpack_from('<i', bx.data, bx.off(a))[0]


print('idx  tabX(0x80028B94)  tabY(0x80028BD4)')
for i in range(16):
    print('%2d  %9d (%#x)  %9d (%#x)' % (i, s32(0x80028B94 + 4 * i), s32(0x80028B94 + 4 * i) & 0xffffffff, s32(0x80028BD4 + 4 * i), s32(0x80028BD4 + 4 * i) & 0xffffffff))
print('direction tables 0x80023654 (X) / 0x80023694 (Y), 32 entries:')
print([struct.unpack_from('<h', bx.data, bx.off(0x80023654 + 2 * i))[0] for i in range(32)])
print([struct.unpack_from('<h', bx.data, bx.off(0x80023694 + 2 * i))[0] for i in range(32)])
