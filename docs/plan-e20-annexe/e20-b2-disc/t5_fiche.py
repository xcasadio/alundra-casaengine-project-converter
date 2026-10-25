import json
P = r'D:/development/repo/alundra-casaengine-project-converter/'
bal = json.load(open(P + 'alundra-project/Data/balance.json', encoding='utf-8'))['EntriesBySpriteIndex']
dp = json.load(open(P + 'alundra-project/Data/item-drop-properties.json', encoding='utf-8'))
for item in (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16):
    e = bal[0x1E + item]
    atts = e['Attacks'] or []
    print('item', item, 'entry', 0x1E + item, 'category', dp[item][2] & 0x7F, 'dropf3', dp[item][2], 'AttackCount', e['AttackCount'], 'RecordSize', e['RecordSize'], 'MaxHp', e['MaxHp'], 'Level', e['BalanceLevel'],
          'resp', e['DamageResponses'])
    for i, a in enumerate(atts):
        print('    row', i, a)
print(list(bal[38].keys()))
