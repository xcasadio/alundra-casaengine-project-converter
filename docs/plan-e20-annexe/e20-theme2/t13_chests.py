import sys, json, re, glob, collections
P = r'D:/development/repo/alundra-casaengine-project-converter/'
ch = collections.defaultdict(set)
for fp in glob.glob(P + 'data-extracted/data/map_*.json'):
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si: continue
    ents = (si.get('Entities') or {}).get('Entities') or []
    for e in ents:
        if e and e.get('SpriteTableIndex') == 30 and e.get('Contents'):
            ch[e['Contents']].add(mid)
for k in sorted(ch):
    if k in (1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,25,26,27,28,31,32,35,36,37,38,39,40,41,43,44,45,46,47,48,49,50,51,52,53,54,55,56,59,80):
        print(k, hex(k), len(ch[k]), sorted(ch[k])[:10])
