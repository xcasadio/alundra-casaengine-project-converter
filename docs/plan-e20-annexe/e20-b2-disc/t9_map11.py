import json, re
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
d = json.load(open(P + 'data-extracted/data/map_11.json', encoding='utf-8'))
print({k: (v if not isinstance(v, (list, dict)) else '...') for k, v in d.items()})
si = d['SpriteInfo']; ents = si['Entities']['Entities']
print('record 24:', ents[24])
for i, e in enumerate(ents):
    if e and e.get('SpriteTableIndex') == 38 and not (e['SpriteDirection'] & 0x80):
        print('global-38 record', i, e)
# programs: who mentions record 24 or item 8
ec = si['EventCodes']; codes = ec['Codes']
starts = {}
for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
    for i, o in enumerate(ec[k]):
        if 0 < o < len(codes): starts.setdefault(o, []).append('%s[%d]' % (k[10], i))
order = sorted(starts)
for n, s in enumerate(order):
    end = order[n + 1] if n + 1 < len(order) else len(codes)
    pc = s
    while pc < end:
        op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
        if sz <= 0: break
        a = codes[pc + 1: pc + sz]
        if op in (0x2D, 0x2E, 0x8A, 0x8B, 0x82) and a and a[0] == 24:
            print('prog', starts[s], 'pc', pc, hex(op), sizes[op][1], a)
        pc += sz
# the records' owner programs: the entity's own program indexes
e = ents[24]
print({k: v for k, v in e.items() if k.startswith('EventCodes')})
