import json, re, sys
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
mid = int(sys.argv[1]); tab = sys.argv[2]; idx = int(sys.argv[3])
d = json.load(open(P + 'data-extracted/data/map_%d.json' % mid, encoding='utf-8'))
ec = d['SpriteInfo']['EventCodes']; codes = ec['Codes']
starts = set()
for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
    for o in ec[k]:
        if 0 < o < len(codes): starts.add(o)
starts = sorted(starts)
s = ec['EventCodes%sTable' % tab][idx]
end = next((x for x in starts if x > s), len(codes))
print('map', mid, tab, idx, 'start', s, 'end', end)
pc = s
while pc < end:
    op = codes[pc]; sz = sizes.get(op, (0, '?'))[0]
    if sz <= 0: break
    print('  %5d %02X %-38s %s' % (pc, op, sizes[op][1][:38], codes[pc + 1: pc + sz])); pc += sz
