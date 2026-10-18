import sys
sys.path.insert(0, '.')
from bdis import *
a = int(sys.argv[1], 16); n = int(sys.argv[2])
for i in range(n):
    print(f"{a+4*i:08x}  [{i}] {u32(a+4*i):08x}")
