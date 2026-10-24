import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bdis import DATA, BASE, OFF
def u32(a): return struct.unpack_from('<I', DATA, a-BASE+OFF)[0]
for i in range(32): print(i, hex(u32(0x80023734+4*i)))
print('tbl 800236d4', [struct.unpack_from('<i', DATA, 0x800236d4-BASE+OFF+4*k)[0] for k in range(24)])
