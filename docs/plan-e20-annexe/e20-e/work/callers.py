"""List jal callers of given targets. Usage: python -B callers.py ADDR [ADDR...]"""
import sys, struct
import exelib as e
targets = [int(a,16) for a in sys.argv[1:]]
for t in targets:
    w = (0x0C000000 | ((t >> 2) & 0x03FFFFFF))
    print(f"-- callers of {t:08x}")
    n = (e.T_SIZE)//4
    for i in range(n):
        a = e.T_ADDR + 4*i
        v = struct.unpack_from("<I", e.DATA, 0x800 + 4*i)[0]
        if v == w:
            print(f"  jal at {a:08x}")
