import sys, os, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
C4SPR = {34,37,107,111,112,113,117,137}
tot = collections.Counter()
for n in CHAIN:
    d = L.load_map(n); si = d['SpriteInfo']; recs = si['SpriteRecords']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        h = r['Header'] if r else {}
        if h.get('ProgramTick') in (4, 70) or h.get('ProgramDeactivate') == 20 or i in C4SPR:
            print('map', n, 'rec', k, 'sprite', i, 'C', h.get('ProgramTick'), 'E', h.get('ProgramDeactivate'))
            tot[n] += 1
print('hits by map', dict(tot))
# the sprite records page: is index i of the per-map page? check also all headers in the map's sprite record list (spawnable by script)
for n in CHAIN:
    d = L.load_map(n); si = d['SpriteInfo']; recs = si['SpriteRecords']
    for i, r in enumerate(recs):
        h = r['Header'] if r else {}
        if h and h.get('ProgramTick') in (4, 70):
            print('  map', n, 'record-table sprite', i, 'has C', h.get('ProgramTick'))
