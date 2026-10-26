import json,sys
R="D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
g=json.load(open(R+'map_alundra.json',encoding='utf-8-sig'))
recs=g['SpriteInfo']['SpriteRecords']
print(len(recs), list(g['SpriteInfo'].keys()))
for sid in (243,2):
    x=recs[sid]
    h=x['Header']
    print('--- global', sid, {k:v for k,v in h.items()})
    print([k for k in x.keys()])
x=recs[2]
a=x['AnimSets']
print(type(a), len(a))
s=json.dumps(a[0] if isinstance(a,list) else a)[:3000]
print(s)
