"""disassembly helper: python d.py <start-hex> <count-instructions> ; annotates jal targets with names.json if present"""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
NAMES = {}
p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "names.json")
if os.path.exists(p):
    try:
        NAMES = json.load(open(p))
    except Exception:
        NAMES = {}


def nm(a):
    for k in (hex(a), "0x%08x" % a):
        if k in NAMES:
            return ",".join(x[0] for x in NAMES[k])
    return ""


def show(start, n):
    for i in lib.dis(start, n):
        extra = ""
        if i.mnemonic in ("jal", "j"):
            try:
                t = int(i.op_str, 16)
                extra = "   ; " + nm(t)
            except Exception:
                pass
        print("%08x: %-7s %s%s" % (i.address, i.mnemonic, i.op_str, extra))


if __name__ == "__main__":
    show(int(sys.argv[1], 16), int(sys.argv[2]))
