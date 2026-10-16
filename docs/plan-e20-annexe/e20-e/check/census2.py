import json, glob, re, collections
tot=0; maps=0; dims=collections.Counter(); b9=collections.Counter(); nopile=0; with_pile=0; heights=collections.Counter()
both40=0
per={}
ng=0
for f in glob.glob("D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_*.json"):
    if not re.search(r"map_\d+\.json$",f): continue
    d=json.load(open(f))
    ng+=1
    m=d['Map']; dims[(m['Width'],m['Height'],len(m['MapTiles']))]+=1
    b9[d['Info']['BalanceLevel']]+=1
    c=0
    for t in m['MapTiles']:
        if t['Walkability']&2:
            c+=1
            heights[t['Height']]+=1
            if t['WallTilesOffset']==-1 or t['WallTilesOffset']==65535: nopile+=1
            else: with_pile+=1
            if t['Walkability']&0x40: both40+=1
    if c: maps+=1; tot+=c; per[f.split('map_')[-1]]=c
print("maps files",ng,"maps with cuttable",maps,"cells",tot)
print(dims); print(b9)
print("with pile",with_pile,"no pile",nopile,"both 0x40",both40)
print(sorted(heights.items())[:40])
top=sorted(per.items(), key=lambda kv:-kv[1])[:10]
print(top)
