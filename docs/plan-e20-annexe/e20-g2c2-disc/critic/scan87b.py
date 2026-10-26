import os, sys, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
for m in (86,87,88,89,90,91,92,93,94,441,442,158):
    doc = de.load_map(m)
    if doc is None:
        print(m,'none'); continue
    cnt = collections.Counter(); progs = collections.defaultdict(set)
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if bad: break
            if op == 0x87:
                cnt[tuple(v[1:3])] += 1; progs[tuple(v[1:3])].add('%s%d' % (slot, i))
    print(m, dict(cnt), {k: sorted(v) for k, v in progs.items() if k[1] == 6})
