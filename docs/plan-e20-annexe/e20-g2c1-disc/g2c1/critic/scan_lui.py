import sys, os
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cdis import u32, T_ADDR, T_SIZE
want = int(sys.argv[1], 0)  # upper 16 bits value
for i in range(T_SIZE // 4):
    a = T_ADDR + 4 * i
    w = u32(a)
    if (w >> 26) == 0x0F and (w & 0xFFFF) == want:
        print('0x%08x lui r%d, 0x%x' % (a, (w >> 16) & 31, want))
