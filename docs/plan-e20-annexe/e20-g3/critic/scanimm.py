"""Critic: image-wide scan for instructions whose operand string contains a given text (read-only).
Usage: python -B -I scanimm.py MNEMONIC_REGEX OPERAND_SUBSTRING [range_start range_end]"""
import sys, os, struct, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cdis as X

size = struct.unpack_from("<I", X.D, 0x1C)[0]
X.md.skipdata = True
mre = re.compile(sys.argv[1])
sub = sys.argv[2]
lo = int(sys.argv[3], 16) if len(sys.argv) > 3 else 0
hi = int(sys.argv[4], 16) if len(sys.argv) > 4 else 0xFFFFFFFF
n = 0
for i in X.md.disasm(X.D[0x800:0x800 + size], X.T_ADDR):
    if lo <= i.address < hi and mre.fullmatch(i.mnemonic) and sub in i.op_str:
        print(f"{i.address:#010x}  {i.mnemonic} {i.op_str}")
        n += 1
print("count", n)
