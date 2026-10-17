#!/usr/bin/env python3
"""critic-complet c5: linear dump (opcode, operands) of the B programs of the given maps, with the sizes of EventOpcodeSizeTable.cs, via the
DLL discovery's decode_events.py (copied in ./dllcopy, unchanged). Read-only, stdout. Usage: c5_dump_prog.py MAP SLOT [SLOT...]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'dllcopy'))
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'dllcopy'))
import decode_events as de
m = int(sys.argv[1]); slots = sys.argv[2:]
doc = de.load_map(m)
for slot, i, s, e in de.all_programs(doc):
    if slots and ('%s%d' % (slot, i)) not in slots:
        continue
    print('== map %d %s[%d] @%d..%d' % (m, slot, i, s, e))
    for pc, op, v, bad in de.sweep(doc, s, e):
        print('   @%d 0x%02X %s%s' % (pc, op, list(v), ' BAD' if bad else ''))
        if bad:
            break
