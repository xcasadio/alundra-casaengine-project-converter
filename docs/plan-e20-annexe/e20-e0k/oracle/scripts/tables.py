import sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import exe
print('slide jump table 0x80023734:')
for i in range(32):
    print(i, hex(exe.u32(0x80023734+4*i)))
print('drift tables 0x80028B94 / 0x80028BD4 (16 entries):')
print([exe.s32(0x80028B94+4*i) for i in range(16)])
print([exe.s32(0x80028BD4+4*i) for i in range(16)])
print('cos', [exe.s16(0x80023654+2*i) for i in range(32)])
print('sin', [exe.s16(0x80023694+2*i) for i in range(32)])
