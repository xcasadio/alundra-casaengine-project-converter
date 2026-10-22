import collections, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, cfg2, memo_static as M
progs, sites = M.programs_from_tsv()
sig_of = {}
shapes = collections.OrderedDict()
for (m, s, p) in progs:
    base, d = C.load(m)
    codes = d['Codes']; entry = d[C.TABLE_KEYS[s]][p]
    seen, marks, ru, extra = cfg2.reach_walkprogram(codes, entry)
    # signature: instruction bytes in pc order relative to entry
    sig = []
    for pc in sorted(seen):
        op, size, ops = C.decode_one(codes, pc)
        sig.append((pc - entry, op, tuple(ops)))
    sig = tuple(sig)
    shapes.setdefault(sig, []).append((m, s, p, entry))
print('programs:', len(progs), 'distinct byte-exact shapes:', len(shapes))
for i, (sig, members) in enumerate(shapes.items()):
    nsites = sum(1 for (mm, ss, pp, pc, op) in sites if (mm, ss, pp) in {(a, b, c) for a, b, c, e in members})
    ops = collections.Counter(o for rel, o, ops_ in sig if 0x6A <= o <= 0x6D)
    print('shape %d: %d programs, %d sites, 6A-6D ops %s : %s' % (i, len(members), nsites, {hex(k): v for k, v in sorted(ops.items())}, ' '.join('%s[%d]' % (a, c) if False else '%d:%s[%d]@%d' % (a, b, c, e) for a, b, c, e in members)))
