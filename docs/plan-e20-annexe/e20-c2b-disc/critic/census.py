import json, glob, re, os
tot={1:0,2:0,3:0}; maps=[]; nfiles=0
for f in sorted(glob.glob('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_*.json')):
    m=re.match(r'map_(\d+)\.json$', os.path.basename(f))
    if not m: continue
    nfiles+=1
    d=json.load(open(f))
    tiles=d['Map']['MapTiles']
    c={1:0,2:0,3:0}
    for t in tiles:
        w=(t['Walkability'] | (t['GroundProperty']<<8)) & 0x180
        n=w>>7
        if n: c[n]+=1
    if sum(c.values()):
        maps.append((int(m.group(1)),c))
        for k in c: tot[k]+=c[k]
print("files",nfiles,"maps with hazard",len(maps),tot)
print([x[0] for x in maps])
