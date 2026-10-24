import sys, os
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
m = int(sys.argv[1]); slots = sys.argv[2:]
doc = de.load_map(m)
print('Tables:', {s: doc['EventCodes%sTable' % s] for s in 'ABCDEF'})
for slot, i, s, e in de.all_programs(doc):
    if slots and ('%s%d' % (slot, i)) not in slots:
        continue
    print('== map %d %s[%d] @%d..%d' % (m, slot, i, s, e))
    for pc, op, v, bad in de.sweep(doc, s, e):
        nm = de.SIZES.get(op, (0, '?'))[1]
        print('   @%d 0x%02X %-34s %s%s' % (pc, op, nm, list(v[1:]) if v else '', ' BAD' if bad else ''))
        if bad:
            break
