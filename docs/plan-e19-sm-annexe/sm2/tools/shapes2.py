import collections, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M
progs, sites = M.programs_from_tsv()
shapes = collections.OrderedDict()
for (m, s, p) in progs:
    base, d = C.load(m)
    codes = d['Codes']; entry = d[C.TABLE_KEYS[s]][p]
    seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
    sig = tuple((pc - entry, op) for pc, op in sorted(seen.items()))
    shapes.setdefault(sig, []).append((m, s, p, entry))
print('programs:', len(progs), 'opcode-structure shapes:', len(shapes))
site_by_prog = collections.Counter((m, s, p) for m, s, p, pc, op in sites)
for i, (sig, members) in enumerate(shapes.items()):
    n = sum(site_by_prog[(a, b, c)] for a, b, c, e in members)
    print('shape %d: %d programs, %d sites: %s' % (i, len(members), n, ', '.join('%d:%s[%d]' % (a, b, c) for a, b, c, e in members)))
    print('     ops:', ' '.join('%02X' % o for rel, o in sig))
    # thresholds of readers per member
    for a, b, c, e in members:
        base, d = C.load(a)
        codes = d['Codes']
        thr = []
        for pc in sorted(cfg2.reach_walkprogram(codes, e)[0]):
            if codes[pc] in (0x6B, 0x6C, 0x6D):
                thr.append('%02X@%d:%d' % (codes[pc], pc, codes[pc+1] | (codes[pc+2] << 8)))
        print('       %d:%s[%d] thresholds %s' % (a, b, c, thr))
