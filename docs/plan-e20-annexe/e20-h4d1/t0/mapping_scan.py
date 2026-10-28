"""mapping_scan.py - E20.h4d1 T0 (c): the distinct (address, size) pairs read and written by the vectors of the fire-2 driver 0x80035C64 (60 + the 1 cancel vector) and of E28 0x8007FB58 (203), with the number of vectors that read / write each.
usage: python -I mapping_scan.py <fire2 vec file> <cancel vec file> <e28 vec file>"""
import json, sys
from collections import Counter
HERO = 0x80127D30; STR = 0x294
def fmt(a):
    if HERO <= a < HERO + 64 * STR:
        i, o = divmod(a - HERO, STR); return 'slot[%d]+0x%X' % (i, o)
    return hex(a)
def scan(path, label, base_of=None):
    d = json.load(open(path))['vectors']
    rd, wr, vals = Counter(), Counter(), {}
    for v in d:
        for a, n, val in v['reads']:
            rd[(a, n)] += 1; vals.setdefault((a, n), set()).add(val)
        for a, n, val in v['writes']:
            wr[(a, n)] += 1
    print('==', label, len(d), 'vectors')
    print(' reads:')
    for (a, n), c in sorted(rd.items()):
        print('   %-18s size %d  read by %3d vectors  distinct values %d' % (fmt(a), n, c, len(vals[(a, n)])))
    print(' writes:')
    for (a, n), c in sorted(wr.items()):
        print('   %-18s size %d  written by %3d vectors' % (fmt(a), n, c))
scan(sys.argv[1], 'fire2 driver 0x80035C64')
scan(sys.argv[2], 'cancel 0x80034E08 (critic extra)')
scan(sys.argv[3], 'E28 0x8007FB58')
