"""Critic: print the class-C handler pointers 4, 23, 70, 73 and the calls each handler makes (read-only)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cdis as X

MASTER = 0x80098F4C
for slot in range(6):
    print("slot", "ABCDEF"[slot], hex(X.word(MASTER + 4 * slot)))
C = X.word(MASTER + 8)
for code in (4, 23, 70, 73):
    h = X.word(C + 4 * code)
    print(f"C{code}: table entry {C + 4*code:#x} -> {h:#x}")
    o = 0x800 + h - X.T_ADDR
    n = 0
    for i in X.md.disasm(X.D[o:o + 0x1000], h):
        if i.mnemonic in ("jal", "jalr"):
            print(f"   {i.address:#x} {i.mnemonic} {i.op_str}")
        if i.mnemonic == "jr" and i.op_str == "$ra":
            print(f"   end {i.address:#x}")
            break
        n += 1
