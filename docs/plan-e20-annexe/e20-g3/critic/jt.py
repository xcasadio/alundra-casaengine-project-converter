"""critic: MovePlayer's state jump table 0x80022D3C (index = hero +0x88, 0x50 entries); print entries and the sites that WRITE +0x88 = 0x20..0x24, 0x27
(li v0, N followed within 3 instructions by sw v0, 0x88(...) or 0x7db8(...)). Read-only. Usage: python -B -I jt.py"""
import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cdis as X

T = 0x80022D3C
ent = {k: X.word(T + 4 * k) for k in range(0x50)}
for k in list(range(0x1E, 0x2A)):
    same = [j for j in range(0x50) if ent[j] == ent[k]]
    print(f"state {k:#04x} ({k}) -> {ent[k]:#x}   shared with {[hex(j) for j in same if j != k][:10]}")
size = struct.unpack_from("<I", X.D, 0x1C)[0]
X.md.skipdata = True
ins = list(X.md.disasm(X.D[0x800:0x800 + size], X.T_ADDR))
want = {0x20, 0x21, 0x22, 0x23, 0x24, 0x27}
for i, a in enumerate(ins):
    if a.mnemonic == "addiu" and a.op_str.endswith("$zero, " + "0x%x" % 0) is False:
        pass
    if a.mnemonic == "addiu" and "$zero, 0x" in a.op_str:
        try:
            v = int(a.op_str.split(", ")[-1], 16)
        except ValueError:
            continue
        if v in want:
            reg = a.op_str.split(",")[0]
            for b in ins[i:i + 4]:
                if b.mnemonic == "sw" and b.op_str.startswith(reg + ",") and (b.op_str.endswith("0x88($s0)") or "0x7db8(" in b.op_str or "0x88(" in b.op_str):
                    print(f"  {a.address:#x} {a.mnemonic} {a.op_str} ; {b.address:#x} {b.mnemonic} {b.op_str}")
