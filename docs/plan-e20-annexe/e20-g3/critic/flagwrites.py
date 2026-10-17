"""Critic: list every instruction that ORs/ANDs 0x80 into a value later stored to +0x6C, image-wide (read-only).
Heuristic: find 'sw rX, 0x6c(rY)' and print the 6 instructions before it when one of them is ori/andi/addiu with 0x80/-0x81
or an 'or'/'and' (register mask). Usage: python -B -I flagwrites.py"""
import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cdis as X

start = X.T_ADDR
size = struct.unpack_from("<I", X.D, 0x1C)[0]
code = X.D[0x800:0x800 + size]
X.md.skipdata = True
ins = list(X.md.disasm(code, start))
print("instructions", len(ins))
hits = 0
for k, i in enumerate(ins):
    if i.mnemonic == "sw" and i.op_str.split(", ")[1].startswith("0x6c("):
        win = ins[max(0, k - 6):k + 1]
        txt = " | ".join(f"{w.address:#x} {w.mnemonic} {w.op_str}" for w in win)
        imm = [w for w in win if (w.mnemonic in ("ori", "andi") and w.op_str.endswith(", 0x80")) or
               (w.mnemonic == "addiu" and w.op_str.endswith(", -0x81")) or
               (w.mnemonic == "ori" and int(w.op_str.split(", ")[-1], 16) & 0x80 if w.mnemonic == "ori" else False)]
        if imm:
            hits += 1
            print(txt)
print("hits", hits)
