import sys
from capstone import *
import cx_exe as X
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 | CS_MODE_LITTLE_ENDIAN)
def dis(start, maxins=400, stopafter_jr=True):
    out = []
    a = start
    n = 0
    seen_jr = None
    while n < maxins:
        w = X.D[X.off(a):X.off(a)+4]
        ins = list(md.disasm(w, a))
        if not ins:
            out.append("%08x: %s  .word %08x" % (a, w.hex(), int.from_bytes(w,'little')))
        else:
            i = ins[0]
            out.append("%08x: %s  %-8s %s" % (a, w[::-1].hex(), i.mnemonic, i.op_str))
            if i.mnemonic == 'jr' and i.op_str == '$ra':
                seen_jr = a
        a += 4; n += 1
        if seen_jr is not None and a > seen_jr + 4:
            break
    return "\n".join(out)
if __name__ == "__main__":
    for tok in sys.argv[1:]:
        if tok.startswith("op"):
            op = int(tok[2:], 16)
            h = X.handler(op)
            print("=== opcode %02X handler %08x ===" % (op, h))
            print(dis(h))
        else:
            h = int(tok, 16)
            print("=== addr %08x ===" % h)
            print(dis(h))
