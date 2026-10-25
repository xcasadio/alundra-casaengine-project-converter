import sys, json, re, glob, collections
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
chest = collections.defaultdict(set); pick = collections.defaultdict(set)
op82 = collections.defaultdict(set); op83 = collections.defaultdict(set); op84 = collections.defaultdict(set)
sprtypes = collections.defaultdict(set)
for fp in glob.glob(P + 'data-extracted/data/map_*.json'):
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si: continue
    for e in (si.get('Entities') or {}).get('Entities') or []:
        if not e: continue
        t = e.get('SpriteTableIndex')
        sprtypes[t].add(mid)
        if t == 30 and e.get('Contents'): chest[e['Contents']].add(mid)
        if t is not None and 31 <= t <= 116: pick[t-30].add(mid)
    ec = si.get('EventCodes')
    if not isinstance(ec, dict): continue
    codes = ec.get('Codes') or []
    starts = sorted({o for k in ec if k.endswith('Table') for o in ec[k] if 0 < o < len(codes)})
    for i, s in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(codes)
        pc = s
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            if op == 0x82: op82[codes[pc+1]].add(mid)
            if op == 0x83: op83[codes[pc+1]].add(mid)
            if op == 0x84: op84[codes[pc+1]].add(mid)
            pc += sz
json.dump({'sprtypes': {str(k): sorted(v) for k, v in sprtypes.items() if k is not None}}, open('sprtypes.json', 'w'))
print('item: chest_maps / placed_pickup_maps / 0x82 / 0x83 / 0x84')
for it in range(1, 98):
    c = len(chest.get(it, ())); p = len(pick.get(it, ())); a = len(op82.get(it, ())); b = len(op83.get(it, ())); u = len(op84.get(it, ()))
    print(it, hex(it), c, p, a, b, u, '' if (c or p or a or b or u) else '<-- NONE')
