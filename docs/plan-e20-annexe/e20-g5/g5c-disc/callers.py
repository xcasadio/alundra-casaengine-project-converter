"""callers.py <addr>... - every jal/j to the addresses, plus lui/addiu pointer loads of the addresses. Read-only on ALUN_CD.EXE."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import all_ins
ins = all_ins()
for t in [int(x, 16) for x in sys.argv[1:]]:
    s = "0x%x" % t
    print("== callers of", s)
    for i in ins:
        if i.mnemonic in ("jal", "j") and i.op_str == s:
            print("  %08x %s %s" % (i.address, i.mnemonic, i.op_str))
