import json,sys
R="D:/development/repo/alundra-casaengine-project-converter"
d=json.load(open(R+'/data-extracted/data/map_%s.json'%sys.argv[1],encoding='utf-8-sig'))
print(list(d.keys()))
for k,v in d.items():
    if isinstance(v,dict): print(k, list(v.keys())[:30])
