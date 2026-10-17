import json,sys
t=json.load(open('tools/dll-structure/table.json'))
want=sys.argv[1:]
for ty in t['types']:
    for w in want:
        if ty['old_name']==w or ty['old_file'].endswith('/'+w+'.cs') or ty['final']['file'].endswith('/'+w+'.cs') or ty['final']['name']==w:
            print(ty['old_file'],'|',ty['old_name'],'->',ty['final']['file'],'|',ty['final']['namespace'],'|',ty['final']['name'],'| split',ty['split'])
