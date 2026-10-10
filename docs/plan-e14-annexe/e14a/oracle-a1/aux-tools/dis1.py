"""aux/dis1.py - disassemble N instructions of ALUN_CD.EXE (France) at an address (capstone).   usage: python -B aux/dis1.py 8003B388 70   (run from the oracle folder)"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools'))
import lib

a = int(sys.argv[1], 16)
n = int(sys.argv[2])
for i in lib.dis(a, n):
    print('%08X  %-8s %s' % (i.address, i.mnemonic, i.op_str))
