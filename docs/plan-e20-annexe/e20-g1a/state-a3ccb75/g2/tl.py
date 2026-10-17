import json,sys
t=json.load(open('tools/dll-structure/table.json',encoding='utf-8'))
q=sys.argv[1:]
for ty in t['types']:
    f=ty['final']
    if any(x.lower() in ty['old_file'].lower() or x==ty['old_name'] for x in q):
        print(ty['old_file'],'|',ty['old_name'],'->',f['file'],'|',f['namespace']+'.'+f['name'],'| split' if ty['split'] else '')
