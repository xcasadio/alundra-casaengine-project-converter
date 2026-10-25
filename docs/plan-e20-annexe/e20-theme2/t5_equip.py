import sys, json, csv
P = r'D:/development/repo/alundra-casaengine-project-converter/'
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';')
    next(rd)
    for row in rd:
        names.append(row)
bal = json.load(open(P + 'alundra-project/Data/balance.json', encoding='utf-8'))['EntriesBySpriteIndex']
print('balance entries:', len(bal))
print('keys of entry 0x1E+51:', list(bal[0x1E + 51].keys()))
dp = json.load(open(P + 'alundra-project/Data/item-drop-properties.json', encoding='utf-8'))
for item in range(1, 60):
    f3 = dp[item][2]
    cat = f3 & 0x7F
    e = bal[0x1E + item]
    att = [(i, a['AttackType'], a['AttackAttribute'], a['Power'], a['UsesEquipmentPower']) for i, a in enumerate(e['Attacks'] or []) if a['AttackType'] or a['Power'] or a['AttackAttribute']]
    nm = names[30 + item][1]
    if cat or any(e['DamageResponses']) or att or e['RecordSize'] > 0 and (e['MaxHp'] or e['BalanceLevel'] != 255):
        print(item, hex(item), nm, 'cat', cat, 'MaxHp', e['MaxHp'], 'BalLevel', e['BalanceLevel'], 'resp', e['DamageResponses'], 'att', att[:6], 'size', e['RecordSize'])
