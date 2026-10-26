"""census2.py - independent census (critic): for every map, every sprite record whose header holds C70, E20, C71, A19 or D34
(any slot/code pair), the index and the placements. Also any record with codes listed. Run: python -B -I -X utf8 census2.py"""
import json, glob, re, collections, os
D = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
HERE = os.path.dirname(os.path.abspath(__file__))
WATCH = {('ProgramTick', 70): 'C70', ('ProgramDeactivate', 20): 'E20', ('ProgramTick', 71): 'C71', ('ProgramLoad', 19): 'A19', ('ProgramTouch', 34): 'D34'}
out = []
hold = collections.defaultdict(set)  # name -> set of (map, idx)
place = collections.defaultdict(lambda: collections.Counter())
placeall = collections.Counter()
files = sorted((p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1)))
nrec = 0
for f in files:
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    recs = si['SpriteRecords']
    st = si['SpriteTable']
    held = {}
    for i, r in enumerate(recs):
        if r is None:
            continue
        h = r['Header']
        nrec += 1
        tags = [nm for (k, v), nm in WATCH.items() if h.get(k) == v]
        if tags:
            held[i] = tags
            for t in tags:
                hold[t].add((n, i))
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None:
            continue
        i = e['SpriteTableIndex']
        if i in held:
            for t in held[i]:
                place[t][(n, i, bool(e['IsEnabled']))] += 1
# report
for t in ('C70', 'E20', 'C71', 'A19', 'D34'):
    idxs = collections.Counter(i for (n, i) in hold[t])
    out.append('%s: held by %d (map,index) pairs; indices %s' % (t, len(hold[t]), dict(idxs)))
    en = collections.Counter()
    dis = collections.Counter()
    for (n, i, e), c in place[t].items():
        (en if e else dis)[n] += c
    out.append('   enabled placements per map: %s  total=%d' % (dict(sorted(en.items())), sum(en.values())))
    out.append('   DISABLED placements per map: %s  total=%d' % (dict(sorted(dis.items())), sum(dis.values())))
txt = '\n'.join(out)
print(txt)
open(os.path.join(HERE, 'out', 'census2.txt'), 'w', encoding='utf-8').write(txt + '\n')
