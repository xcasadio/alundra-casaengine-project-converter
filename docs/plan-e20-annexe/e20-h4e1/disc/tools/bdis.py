"""bdis.py - read-only disassembly of a range of ALUN_CD.EXE (France): python -B bdis.py START COUNT"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
a = int(sys.argv[1], 0); n = int(sys.argv[2], 0)
for i in lib.dis(a, n):
    print('%08x  %-8s %s' % (i.address, i.mnemonic, i.op_str))
