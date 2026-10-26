"""dump_prog.py MAP SLOT... (e.g. 72 A140 C140): linear dump of exported event programs using the repo's decode_events.py (read-only)."""
import os, sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
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
