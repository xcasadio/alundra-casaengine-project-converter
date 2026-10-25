"""For maps 6, 52, 80 (and 7, 11): the 0x8A/0x8B spawns of a record whose SpriteTableIndex is 38 WITH its SpriteDirection, and what the map's own sprite table says for index 38."""
import json, re, sys
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
for mid in (6, 7, 11, 52, 80):
    d = json.load(open(P + 'data-extracted/data/map_%d.json' % mid, encoding='utf-8'))
    si = d['SpriteInfo']
    ents = si['Entities']['Entities']
    ec = si['EventCodes']; codes = ec['Codes']
    starts = {}
    for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for i, o in enumerate(ec[k]):
            if 0 < o < len(codes):
                starts.setdefault(o, []).append('%s[%d]' % (k[10], i))
    order = sorted(starts)
    print('== map', mid)
    for n, s in enumerate(order):
        end = order[n + 1] if n + 1 < len(order) else len(codes)
        pc = s
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            a = codes[pc + 1: pc + sz]
            if op in (0x8A, 0x8B):
                e = ents[a[0]] if a[0] < len(ents) else None
                if e and e.get('SpriteTableIndex') == 38:
                    print('  prog', starts[s], 'pc', pc, hex(op), 'record', a[0], 'SpriteTableIndex', e['SpriteTableIndex'], 'SpriteDirection', e['SpriteDirection'], '=> %s' % ('MAP-LOCAL sprite (type 0x%X)' % (0x100 + e['SpriteTableIndex']) if e['SpriteDirection'] & 0x80 else 'GLOBAL sprite 38 (pickup of item 8)'))
            pc += sz
