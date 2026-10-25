import json, re
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
import csv
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';'); next(rd)
    for row in rd: names.append(row)
for mid in (6, 52, 80):
    d = json.load(open(P + 'data-extracted/data/map_%d.json' % mid, encoding='utf-8'))
    si = d['SpriteInfo']; ents = si['Entities']['Entities']; ec = si['EventCodes']; codes = ec['Codes']
    starts = {}
    for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for i, o in enumerate(ec[k]):
            if 0 < o < len(codes): starts.setdefault(o, []).append('%s[%d]' % (k[10], i))
    order = sorted(starts)
    print('== map', mid)
    for n, s in enumerate(order):
        end = order[n + 1] if n + 1 < len(order) else len(codes)
        pc = s
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            a = codes[pc + 1: pc + sz]
            if op in (0x8A, 0x8B) and a[0] == 38:
                e = ents[38]
                glob = not (e['SpriteDirection'] & 0x80)
                nm = names[e['SpriteTableIndex']][1] if glob and e['SpriteTableIndex'] < len(names) else '(map-local sprite %d)' % e['SpriteTableIndex']
                print('  prog', starts[s], 'pc', pc, hex(op), 'record id 38 -> SpriteTableIndex', e['SpriteTableIndex'], 'dir', e['SpriteDirection'], nm)
            pc += sz
