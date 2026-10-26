import json, glob, re, collections
R="D:/development/repo/alundra-casaengine-project-converter"
D=R+'/data-extracted/data'
files=sorted((p for p in glob.glob(D+'/map_*.json') if re.search(r'map_\d+\.json$',p)), key=lambda p:int(re.search(r'map_(\d+)\.json',p).group(1)))
print(len(files),'maps')
types=collections.defaultdict(set)   # (idx) per map with ProgramLoad 19 or Tick 71
tot=collections.Counter()
for f in files:
    n=int(re.search(r'map_(\d+)\.json',f).group(1))
    d=json.load(open(f,encoding='utf-8-sig'))
    si=d['SpriteInfo']
    recs=si['SpriteRecords']
    # which table indexes have load 19 or tick 71
    idxs={}
    for i,r in enumerate(recs):
        if r is None: continue
        h=r['Header']
        if h['ProgramLoad']==19 or h['ProgramTick']==71:
            idxs[i]=(h['ProgramLoad'],h['ProgramTick'],h['Sector5Id'],h['MoreFlags'],h['CanPickup'])
    if not idxs: continue
    ents=si['Entities']['Entities']
    cnt=collections.Counter(); en=collections.Counter()
    for k,e in enumerate(ents):
        if e is None: continue
        if e['SpriteTableIndex'] in idxs:
            cnt[e['SpriteTableIndex']]+=1
            if e['IsEnabled']: en[e['SpriteTableIndex']]+=1
    print('map',n,'records',idxs,'placements',dict(cnt),'enabled',dict(en))
    # SpriteTable maps index->?
