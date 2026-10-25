import sys, json, csv
P = r'D:/development/repo/alundra-casaengine-project-converter/'
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';')
    next(rd)
    for row in rd:
        names.append(row)
ip = json.load(open(P + 'alundra-project/Data/items-properties.json', encoding='utf-8'))
dp = json.load(open(P + 'alundra-project/Data/item-drop-properties.json', encoding='utf-8'))
print('globalsprite idx -> name (0..30):')
for i in range(0, 31):
    print(i, hex(i), names[i][1])
print()
print('id hex name | slot flag prio max icon | drop f1 snd f3 f4')
for i in range(1, 98):
    n = names[30 + i][1] if 30 + i < len(names) else '?'
    d = dp[i] if i < len(dp) else None
    print(i, hex(i), n, '|', ip[i], '|', d)
