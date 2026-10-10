"""aux/findjal.py - list the call sites (jal / j) of an address in ALUN_CD.EXE (France).   usage: python -B aux/findjal.py 8003B388"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'tools'))
import lib

tgt = int(sys.argv[1], 16)
D = lib.DATA
for o in range(lib.OFF, len(D) - 3, 4):
    w = struct.unpack_from('<I', D, o)[0]
    if (w >> 26) in (2, 3) and (((w & 0x3FFFFFF) << 2) | 0x80000000) == tgt:
        print('%08X %s' % (lib.BASE + o - lib.OFF, 'jal' if (w >> 26) == 3 else 'j'))
