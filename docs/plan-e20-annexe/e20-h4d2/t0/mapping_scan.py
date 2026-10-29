"""mapping_scan.py - E20.h4d2 T0 (b): the distinct (address, size) pairs read and written by the vectors of the fire-1 driver 0x80035A84, of the cancellation 0x80034D2C, of E3 0x8007EDA0 and of C63 0x8007AC60, with the number of vectors that read / write each
(and, for the entity words, which part of the vectors are `binary_only`).  Pure reading of the four vector files.
usage: python -I mapping_scan.py <driver vec> <cancel vec> <e3 vec> <c63 vec>"""
import json
import sys
from collections import Counter

HERO = 0x80127D30
STR = 0x294


def fmt(a):
    if HERO <= a < HERO + 64 * STR:
        i, o = divmod(a - HERO, STR)
        return 'slot[%d]+0x%X' % (i, o)
    return hex(a)


def entity_offset(a):
    """the offset inside the entity, whatever the slot (the vectors place the entities at different slots / fake addresses): None when the address is not in the slot table or in a pool the vectors use"""
    if HERO <= a < HERO + 64 * STR:
        return (a - HERO) % STR
    return None


def scan(path, label):
    d = json.load(open(path))['vectors']
    rd, wr, vals = Counter(), Counter(), {}
    off_rd, off_wr, bo = Counter(), Counter(), 0
    for v in d:
        bo += 1 if v.get('binary_only') else 0
        for a, n, val in v['reads']:
            rd[(a, n)] += 1
            vals.setdefault((a, n), set()).add(val)
            o = entity_offset(a)
            if o is not None:
                off_rd[(o, n)] += 1
        for a, n, val in v['writes']:
            wr[(a, n)] += 1
            o = entity_offset(a)
            if o is not None:
                off_wr[(o, n)] += 1
    print('==', label, len(d), 'vectors,', bo, 'binary_only')
    print(' non-entity addresses read:')
    for (a, n), c in sorted(rd.items()):
        if entity_offset(a) is None:
            print('   %-18s size %d  read by %4d vectors  distinct values %d' % (fmt(a), n, c, len(vals[(a, n)])))
    print(' non-entity addresses written:')
    for (a, n), c in sorted(wr.items()):
        if entity_offset(a) is None:
            print('   %-18s size %d  written by %4d vectors' % (fmt(a), n, c))
    print(' entity offsets read (slot table, any slot): offset size  number of (vector, word) reads')
    for (o, n), c in sorted(off_rd.items()):
        print('   +0x%-4X size %d  %d' % (o, n, c))
    print(' entity offsets written:')
    for (o, n), c in sorted(off_wr.items()):
        print('   +0x%-4X size %d  %d' % (o, n, c))


scan(sys.argv[1], 'fire-1 driver 0x80035A84')
scan(sys.argv[2], 'cancel 0x80034D2C')
scan(sys.argv[3], 'E3 0x8007EDA0')
scan(sys.argv[4], 'C63 0x8007AC60')
