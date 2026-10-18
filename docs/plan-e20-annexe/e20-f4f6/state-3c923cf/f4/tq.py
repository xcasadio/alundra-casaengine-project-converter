import json, sys
t = json.load(open('D:/development/repo/alundra-casaengine-project-converter/tools/dll-structure/table.json', encoding='utf-8'))
for q in sys.argv[1:]:
    found = False
    for e in t['types']:
        if q.lower() in e['old_name'].lower() or q.lower() in e['old_file'].lower():
            found = True
            print(f"{e['old_name']} | old_file={e['old_file']} | final={e['final']['file']} | ns={e['final']['namespace']} | name={e['final']['name']} | split={e['split']} | main={e['main']}")
    if not found:
        print('NOT IN TABLE:', q)
