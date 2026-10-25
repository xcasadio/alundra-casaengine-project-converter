import json
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json'))
sr = d['SpriteInfo']['SpriteRecords'][0]
a = sr['AnimSets'][0x3e]
print({k:(v if k!='PreloadedAnims' else '...') for k,v in a.items()})
print(len(sr['AnimSets']))
n40=[]
for i,a in enumerate(sr['AnimSets']):
    if a.get('Acceleration',0) & 0x40: n40.append(i)
print("count acceleration&0x40:", len(n40), [hex(x) for x in n40])
n40f=[i for i,a in enumerate(sr['AnimSets']) if a.get('Flags',0)&0x40]
print("count Flags&0x40:", len(n40f))
print(set(sr.keys()))
