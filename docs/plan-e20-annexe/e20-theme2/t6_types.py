import sys, json, csv
P = r'D:/development/repo/alundra-casaengine-project-converter/'
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';')
    next(rd)
    for row in rd:
        names.append(row)
d = json.load(open(P + 'data-extracted/data/map_alundra.json', encoding='utf-8'))
sr = d['SpriteInfo']['SpriteRecords']
bal = json.load(open(P + 'alundra-project/Data/balance.json', encoding='utf-8'))['EntriesBySpriteIndex']
want = [1, 2, 3, 4, 7, 11, 12, 14, 15, 16, 17, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30]
for t in want:
    h = sr[t]['Header']
    a = sr[t]['AnimSets']
    b = bal[t]
    att = [(i, x['AttackType'], x['AttackAttribute'], x['Power']) for i, x in enumerate(b.get('Attacks') or []) if x['AttackType'] or x['Power']]
    print(t, hex(t), names[t][1], '| Load/Tick/Touch/Deact/Interact', h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract'],
          '| MoreFlags', hex(h['MoreFlags']), 'CanPickup', hex(h['CanPickup']), 'Break', h['BreakEffect'], 'Contents', h['Contents'],
          '| anims', len(a), [(x['Speed'], x['Sfx']) for x in a[:2]], '| attacks', att[:3])
