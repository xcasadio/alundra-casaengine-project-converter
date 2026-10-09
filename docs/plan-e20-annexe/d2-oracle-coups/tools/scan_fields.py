"""scan_fields.py - D2: every store (sw/sh/sb) in the EXE whose displacement is one of the entity fields the hit pass reads (static scan).
usage: python -B scan_fields.py"""
import os, sys, re
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import lib
want = {0x1d8, 0x1dc, 0x1e0, 0x1e4, 0x1e8, 0x1ec, 0x1f0, 0x1f4, 0x1f8, 0x1d4, 0x1c8, 0x1fc, 0x200, 0x204, 0x208, 0x20c, 0x210, 0x214, 0x218, 0x21c}
for i in lib.all_ins():
    if i.mnemonic in ('sw', 'sh', 'sb'):
        m = re.match(r'\$(\w+), (-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)', i.op_str)
        if m:
            d = int(m.group(2), 0)
            if d in want and 0x80030000 <= i.address < 0x80050000:
                print('%08x %s %s' % (i.address, i.mnemonic, i.op_str))
