import json,sys
t=json.load(open('tools/dll-structure/table.json',encoding='utf-8'))
q=sys.argv[1:]
for ty in t['types']:
    for k in q:
        if k in ty['old_file'] or k==ty['old_name'] or k==ty['final']['name'] or k in ty['final']['file']:
            print(ty['old_file'],'|',ty['old_name'],'->',ty['final']['name'],'|',ty['final']['file'],'| split' if ty['split'] else '')
            break
