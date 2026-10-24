import json,sys
R="D:/development/repo/alundra-casaengine-project-converter"
m=sys.argv[1]; ids=set(int(x) for x in sys.argv[2:])
d=json.load(open(R+'/data-extracted/data/map_%s.json'%m,encoding='utf-8-sig'))
s=d['Strings']
print(type(s), (list(s.keys()) if isinstance(s,dict) else len(s)))
L = s if isinstance(s,list) else (s.get('Strings') or s.get('Entries') or list(s.values())[0])
for i,t in enumerate(L):
    if not ids or i in ids or (i+128) in ids or (i+0x80) in ids:
        print(i, repr(t)[:300])
