import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import exe
base = int(sys.argv[1], 16)
n = int(sys.argv[2])
for i in range(n):
    print("idx 0x%02X -> %08X" % (i, exe.u32(base + 4 * i)))
