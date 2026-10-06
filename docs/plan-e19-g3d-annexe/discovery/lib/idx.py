"""idx.py: cached full disassembly + helpers.
usage: python idx.py callers <addr-hex>      -> all jal sites to addr, with enclosing function guess
       python idx.py mem <offset-hex>       -> all lw/sw/lh/sh/lb/sb/lbu/lhu with that immediate offset (any register)
"""
import sys, os, pickle
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
import lib
CACHE = os.path.join(H, "ins.pkl")


def load():
    if os.path.exists(CACHE):
        return pickle.load(open(CACHE, "rb"))
    ins = [(i.address, i.mnemonic, i.op_str) for i in lib.all_ins()]
    pickle.dump(ins, open(CACHE, "wb"))
    return ins


def func_start(ins_by_addr, addr):
    """walk back to a 'addiu sp, sp, -N' prologue"""
    a = addr
    while a > 0x80020000:
        t = ins_by_addr.get(a)
        if t and t[1] == "addiu" and t[2].startswith("$sp, $sp, -"):
            return a
        a -= 4
    return None


if __name__ == "__main__":
    ins = load()
    by = {a: (a, m, o) for a, m, o in ins}
    cmd = sys.argv[1]
    if cmd == "callers":
        t = int(sys.argv[2], 16)
        n = 0
        for a, m, o in ins:
            if m == "jal":
                try:
                    if int(o, 16) == t:
                        print("0x%08x in func@%s" % (a, hex(func_start(by, a) or 0)))
                        n += 1
                except Exception:
                    pass
        print(n, "callers")
    elif cmd == "mem":
        off = int(sys.argv[2], 16)
        n = 0
        for a, m, o in ins:
            if m in ("lw", "sw", "lh", "lhu", "sh", "lb", "lbu", "sb"):
                try:
                    rest = o.split(",", 1)[1].strip()
                    offs = rest.split("(")[0].strip()
                    if int(offs, 0) == off:
                        print("0x%08x %s %s  func@%s" % (a, m, o, hex(func_start(by, a) or 0)))
                        n += 1
                except Exception:
                    pass
        print(n, "refs")
