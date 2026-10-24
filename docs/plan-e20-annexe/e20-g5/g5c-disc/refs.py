"""refs.py <addr>... - every (lui hi ; addiu/lw/sw/lh/lhu/lb/lbu/sb/sh lo(...)) pair reaching a data address (hi/lo split, lui within 12 instructions before). Read-only."""
import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import all_ins
ins = all_ins()
for t in [int(x, 16) for x in sys.argv[1:]]:
    lo = t & 0xffff
    los = lo - 0x10000 if lo >= 0x8000 else lo
    hi = (t - los) >> 16
    print("== refs to 0x%x (hi 0x%x lo %d)" % (t, hi, los))
    for k, i in enumerate(ins):
        m = i.mnemonic
        if m not in ("addiu","lw","sw","lh","lhu","lb","lbu","sb","sh"): continue
        ops = i.op_str
        mm = re.search(r'(-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)$', ops)
        if m == "addiu":
            parts = ops.split(", ")
            if len(parts) != 3: continue
            try: v = int(parts[2], 16) if parts[2].lstrip('-').startswith('0x') else int(parts[2])
            except: continue
            base = parts[1].lstrip('$')
        else:
            if not mm: continue
            try: v = int(mm.group(1), 16) if mm.group(1).lstrip('-').startswith('0x') else int(mm.group(1))
            except: continue
            base = mm.group(2)
        if v != los: continue
        for j in range(max(0, k-12), k):
            if ins[j].mnemonic == "lui":
                p = ins[j].op_str.split(", ")
                if p[0].lstrip('$') == base and int(p[1], 16) == hi:
                    print("  %08x %s %s    (lui at %08x)" % (i.address, m, ops, ins[j].address))
                    break
