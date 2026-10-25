import json
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json'))
sr = d['SpriteInfo']['SpriteRecords'][0]
for st in (0x31,0x1c,0x3a,0x3b):
    a=sr['AnimSets'][st]
    pa=a['PreloadedAnims'][0]
    print(hex(st), {k:v for k,v in pa.items() if k!='Frames'})
    for f in pa['Frames']:
        print("   ", f)
