"""q9_chain.py - placements of the liftable families (header C4, C23, C70) on the 30 maps of the story chain. READ-ONLY. Run: python -B -I -X utf8 q9_chain.py"""
import collections, json
R = r"D:/development/repo/alundra-casaengine-project-converter"
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
tot = collections.Counter()
for n in CHAIN:
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    c = collections.Counter()
    for e in si['Entities']['Entities']:
        if e is None or not e['IsEnabled']:
            continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None:
            continue
        h = r['Header']
        if h['ProgramTick'] in (4, 23, 70) and ((h['CanPickup'] >> 1) & 3):
            c['C%d' % h['ProgramTick']] += 1
    tot.update(c)
    if c:
        print('map', n, dict(c))
print('total on the 30 chain maps:', dict(tot))
