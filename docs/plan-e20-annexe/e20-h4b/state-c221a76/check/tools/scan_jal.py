"""scan_jal.py - list the call sites (jal) of given targets in a range. usage: scan_jal.py TARGET START END"""
import sys, struct, exelib
t = int(sys.argv[1], 16); lo = int(sys.argv[2], 16); hi = int(sys.argv[3], 16)
for a in range(lo, hi, 4):
    w = exelib.u32(a)
    if (w >> 26) == 3 and (((a + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)) == t:
        print('%08x' % a)
