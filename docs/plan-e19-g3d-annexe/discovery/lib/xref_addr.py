"""xref_addr.py <addr_hex> : list instructions that load/store the given absolute address through lui+offset (same register)."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import lib
sys.stdout.reconfigure(encoding="utf-8")
target = int(sys.argv[1], 16)
ins = lib.all_ins()
hi = (target + 0x8000) >> 16 & 0xFFFF
lo = target & 0xFFFF
if lo >= 0x8000: lo -= 0x10000
lui_at = {}
out = []
for k, i in enumerate(ins):
    if i.mnemonic == "lui":
        reg, imm = i.op_str.split(",")
        lui_at[reg.strip()] = (int(imm.strip(), 16), k)
    elif i.mnemonic in ("lw", "sw", "lb", "lbu", "sb", "lh", "lhu", "sh", "addiu"):
        try:
            parts = i.op_str.replace(")", "").split(",")
            if i.mnemonic == "addiu":
                dst, src, off = [p.strip() for p in parts]
                reg = src; offv = int(off, 0)
            else:
                rt, rest = parts[0].strip(), ",".join(parts[1:])
                off, reg = rest.split("(")
                offv = int(off.strip() or "0", 0); reg = reg.strip()
        except Exception:
            continue
        if reg in lui_at and lui_at[reg][0] == hi and k - lui_at[reg][1] < 40 and offv == lo:
            out.append((i.address, i.mnemonic, i.op_str))
for a, m, o in out:
    print("0x%08x %s %s" % (a, m, o))
print(len(out), "refs")
