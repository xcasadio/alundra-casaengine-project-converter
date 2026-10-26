import os, sys, collections, glob, re
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
ROOT = de.ROOT
maps = sorted({int(re.search(r'-(\d+)\.events\.json$', f.replace(chr(92), '/')).group(1)) for f in glob.glob(ROOT + '/alundra-project/Maps/*/*/events/*.events.json')})
WATCH = {(2, 70), (4, 20), (3, 34), (2, 71), (0, 19)}
tot = collections.Counter()
any41 = collections.Counter()
for m in maps:
    doc = de.load_map(m)
    for slot, i, s, e in de.all_programs(doc):
        for pc, op, v, bad in de.sweep(doc, s, e):
            if bad: break
            if op in (0x41, 0x40):
                key = (op, v[1], v[2]) if len(v) > 2 else (op,)
                any41[key] += 1
                if op == 0x41 and (v[1], v[2]) in WATCH:
                    tot[(m, slot, i, tuple(v))] += 1
print('maps', len(maps))
print('0x41 with watched (slot,code):', dict(tot))
print('all 0x41 codes (slot,code) distinct:', sorted((k[1], k[2]) for k in any41 if k[0] == 0x41)[:80])
