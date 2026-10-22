"""Independent re-disassembly (capstone) of the handlers 0x6A-0x6D and of the RunScript call/advance sequence.
Read-only on ALUN_CD.EXE (path in lib.py). Output: evidence/redisasm.txt (written by redirecting stdout)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
TABLE = 0x80098FAC
for op in (0x6A, 0x6B, 0x6C, 0x6D, 0x6E):
    h = lib.u32(TABLE + 4 * op)
    print("table[%#04x] @ %#010x -> handler %#010x" % (op, TABLE + 4 * op, h))
print()
for op, n in ((0x6A, 11), (0x6B, 17), (0x6C, 17), (0x6D, 17)):
    h = lib.u32(TABLE + 4 * op)
    print("; opcode %#04x handler %#010x (%d words)" % (op, h, n))
    for i in lib.dis(h, n):
        print("  %#010x  %-8s %s" % (i.address, i.mnemonic, i.op_str))
print()
print("; RunScript dispatch 0x800422B0..0x80042348")
for i in lib.dis(0x800422B0, 0x348 // 4 - 0x2B0 // 4 + 1 if False else 36):
    print("  %#010x  %-8s %s" % (i.address, i.mnemonic, i.op_str))
