"""dumpall.py MAP SLOT_INDEX... (e.g. 426 C14 C13): full linear dump of exported programs with opcode names and operands (like dump_prog.py, but prints the DLL's size-table names). Read-only."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import decode_events as de
m = int(sys.argv[1]); want = sys.argv[2:]
doc = de.load_map(m)
for slot, i, s, e in de.all_programs(doc):
    if want and ('%s%d' % (slot, i)) not in want:
        continue
    print('== map %d %s[%d] @%d..%d' % (m, slot, i, s, e))
    for pc, op, v, bad in de.sweep(doc, s, e):
        print('   @%d 0x%02X %-36s %s%s' % (pc, op, de.SIZES.get(op, (0, '?'))[1][:36], list(v[1:]) if v else '', ' BAD' if bad else ''))
        if bad:
            break
