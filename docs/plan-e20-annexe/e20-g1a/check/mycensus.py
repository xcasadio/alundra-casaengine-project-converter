import json, glob, re, collections
D = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
files = sorted([p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)], key=lambda p:int(re.search(r'map_(\d+)',p).group(1)))
bytype = collections.defaultdict(lambda: collections.Counter())
lift = collections.defaultdict(lambda: collections.Counter())
maps_with = collections.defaultdict(set)
tot=0; totmaps=set()
hdrs = {}
for f in files:
    n = int(re.search(r'map_(\d+)',f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if not e: continue
        i = e['SpriteTableIndex']
        sd = e['SpriteDirection']
        t = i | 0x100 if (sd & 0x80) else i
        r = recs[i] if i < len(recs) else None
        if not r: 
            bytype[t][('norec',)] += 1
            continue
        h = r['Header']
        kind = (h['CanPickup']>>1)&3
        sig = (kind, h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['MoreFlags'], h['CanPickup'], h['BreakEffect'], h['SizeX'],h['SizeY'],h['SizeZ'], h['Contents'])
        bytype[t][sig] += 1
        if kind and e.get('IsEnabled',1):
            lift[t][sig]+=1; maps_with[t].add(n); tot+=1; totmaps.add(n)
print('total enabled liftable placements', tot, 'on', len(totmaps), 'maps')
for t in sorted(lift):
    print(t, hex(t), 'liftable sigs:', dict(lift[t]), 'maps', len(maps_with[t]))
    # other (non-liftable) sigs with the same type code
    others = {s:c for s,c in bytype[t].items() if s[0] != 'norec' and s[0]==0}
    if others: print('   NON-LIFTABLE with same type:', others)
    if len(lift[t])>1: print('   multiple liftable sigs')
