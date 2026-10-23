"""progs_g5.py - for every placement of the 4 families (census_g5.json), dump the scripted programs it runs (slots A, C, D, E, F; index & 0x7f) with opcode names,
using the repo's decode_events.py (sizes from EventOpcodeSizeTable.cs). Read-only.
Run: python -B -X utf8 progs_g5.py [FAMILY] > progs_g5.txt ; also writes opcode_hist_g5.json (opcode histogram by family)"""
import os, sys, json, collections, re
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
HERE = os.path.dirname(os.path.abspath(__file__))
cen = json.load(open(os.path.join(HERE, 'census_g5.json')))
want = sys.argv[1] if len(sys.argv) > 1 else None
SLOTS = (('A', 'A'), ('C', 'C'), ('D', 'D'), ('E', 'E'), ('F', 'F'))
hist = collections.defaultdict(collections.Counter)
docs = {}
done = set()


def prog_range(doc, slot, idx):
    for s, i, st, en in de.all_programs(doc):
        if s == slot and i == idx:
            return st, en
    return None


for r in cen['rows']:
    if want and r['fam'] != want:
        continue
    m = r['map']
    if m not in docs:
        docs[m] = de.load_map(m)
    doc = docs[m]
    if doc is None:
        print('map', m, 'no events file'); continue
    for slot, key in SLOTS:
        v = r[key]
        if not v:
            continue
        idx = v & 0x7f
        tag = (r['fam'], m, slot, idx)
        if tag in done:
            continue
        done.add(tag)
        pr = prog_range(doc, slot, idx)
        print('== %s map %d rec %d %s[%d] (raw %d, bit7=%d)' % (r['fam'], m, r['rec'], slot, idx, v, 1 if v & 0x80 else 0), 'range', pr)
        if not pr:
            print('   (no program: index past the table)')
            continue
        for pc, op, vv, bad in de.sweep(doc, pr[0], pr[1]):
            nm = de.SIZES.get(op, (0, '?'))[1]
            hist[r['fam']][op] += 1
            print('   @%d 0x%02X %-34s %s%s' % (pc, op, nm[:34], list(vv[1:]) if vv else '', ' BAD' if bad else ''))
            if bad:
                break
json.dump({f: {('0x%02X' % k): v for k, v in sorted(c.items())} for f, c in hist.items()}, open(os.path.join(HERE, 'opcode_hist_g5.json'), 'w'), indent=1)
