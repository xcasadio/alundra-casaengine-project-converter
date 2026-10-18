import sys, re
import lib
from my_dscan import handlers, md
slot = int(sys.argv[1]) if len(sys.argv) > 1 else 3
hs = handlers(slot, 128)
addrs = sorted(hs)
out = []
for k, a in enumerate(addrs):
    end = addrs[k+1] if k+1 < len(addrs) else a + 0x200
    ins = []
    for x in range(a, end, 4):
        w = lib.u32(x); r = list(md.disasm(w.to_bytes(4,'little'), x))
        ins.append('%08x %-6s %s' % (x, r[0].mnemonic if r else '.word', r[0].op_str if r else '%08x' % w))
    out.append((hs[a], a, ins))
for codes, a, ins in out:
    txt = '\n'.join(ins)
    if '0x1c8' in txt or '0x224' in txt:
        print('=== slot%d code %s at %08x (%d ins)' % (slot, codes, a, len(ins)))
        if len(ins) <= int(sys.argv[2]) if len(sys.argv) > 2 else 40:
            print(txt)
