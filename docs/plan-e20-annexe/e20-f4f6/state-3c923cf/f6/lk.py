import json,sys
t=json.load(open('tools/dll-structure/table.json'))
for q in sys.argv[1:]:
    for e in t['types']:
        if q.lower() in e['old_name'].lower() or q.lower() in e['old_file'].lower():
            print(e['old_file'],'|',e['old_name'],'->',e['final']['file'],'|',e['final']['name'],'|',e['final']['namespace'])
