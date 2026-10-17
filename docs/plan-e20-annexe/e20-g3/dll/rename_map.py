import json,sys
t=json.load(open('D:/development/repo/alundra-casaengine-project-converter/tools/dll-structure/table.json',encoding='utf-8'))
rows=[]
for x in t['types']:
    rows.append((x['old_name'],x['final']['name'],x['old_file'],x['final']['file']))
q=sys.argv[1:]
for r in rows:
    if not q or any(s.lower() in (r[0]+r[1]+r[2]+r[3]).lower() for s in q):
        print('\t'.join(r))
