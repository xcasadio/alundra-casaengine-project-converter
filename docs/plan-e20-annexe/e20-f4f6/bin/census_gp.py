"""Census of the cell ground-property byte over all maps: +0x18C = min over landed corners of (cell & 0xE00) >> 9, i.e. (GroundProperty & 0xE) >> 1.
Also the walkability bits 0x02 (cuttable), 0x8004 (DestroyOnVramFlags test uses +0x180 & 0x8004 on the OR of the corner cell words).
"""
import json, os, collections
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
gp = collections.Counter()
gp_maps = collections.defaultdict(set)
wk = collections.Counter()
wk_maps = collections.defaultdict(set)
for n in range(0, 483):
    p = os.path.join(D, 'map_%d.json' % n)
    if not os.path.exists(p):
        continue
    d = json.load(open(p, encoding='utf-8-sig'))
    mt = (d.get('Map') or {}).get('MapTiles') or []
    for t in mt:
        if not t:
            continue
        g = t['GroundProperty']
        gp[g] += 1
        gp_maps[g].add(n)
        w = t['Walkability']
        wk[w] += 1
        wk_maps[w].add(n)
print('GroundProperty value -> cells, maps (+0x18C value = (gp & 0xE) >> 1)')
for g in sorted(gp):
    print('  gp=0x%02X  18C=%d  cells=%d  maps=%d  e.g. %s' % (g, (g & 0xE) >> 1, gp[g], len(gp_maps[g]), sorted(gp_maps[g])[:8]))
print('Walkability value -> cells, maps')
for w in sorted(wk):
    print('  wk=0x%04X cells=%d maps=%d e.g. %s' % (w, wk[w], len(wk_maps[w]), sorted(wk_maps[w])[:8]))
