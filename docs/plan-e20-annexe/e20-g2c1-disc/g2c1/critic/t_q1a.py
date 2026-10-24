import sys, os, json, collections, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import evlib as L
cnt_dsl = collections.Counter(); cnt_grav = collections.Counter(); cnt_dvf = collections.Counter()
by_type = collections.Counter()
total = 0
files = sorted(glob.glob(L.ROOT + '/data-extracted/data/map_*.json'))
for f in files:
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d.get('SpriteInfo'); 
    if not si: continue
    recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        if i >= len(recs) or recs[i] is None: continue
        h = recs[i]['Header']
        fl = h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
        if fl & 0x100:
            cnt_grav['grav'] += 1
            if fl & 0x100000:
                cnt_dsl['dsl'] += 1; by_type[(i, 'DSL')] += 1
            if fl & 0x200000:
                cnt_dvf['dvf'] += 1; by_type[(i, 'DVF')] += 1
print(dict(cnt_grav), dict(cnt_dsl), dict(cnt_dvf))
for (i, k), n in sorted(by_type.items(), key=lambda x: -x[1])[:30]:
    print(i, k, n)
