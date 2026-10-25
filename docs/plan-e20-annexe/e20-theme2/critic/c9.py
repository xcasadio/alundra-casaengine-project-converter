import sys, json, re, glob, collections
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
sp = collections.defaultdict(set)
for fp in glob.glob(P + 'data-extracted/data/map_*.json'):
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si or not isinstance(si.get('EventCodes'), dict): continue
    ec = si['EventCodes']; codes = ec.get('Codes') or []
    starts = sorted({o for k in ec if k.endswith('Table') for o in ec[k] if 0 < o < len(codes)})
    for i, s in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(codes)
        pc = s
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            if op in (0x8A, 0x8B):
                t = codes[pc+1]
                sp[(op, t)].add(mid)
            pc += sz
print('0x8A/0x8B spawn by global sprite index (0..30 = projectiles/objects, 31..116 = item pickups):')
for (op, t) in sorted(sp, key=lambda x: (x[1], x[0])):
    if t <= 30 or 31 <= t <= 120:
        print('op %02X type %d (item %d) maps %s' % (op, t, t-30, sorted(sp[(op,t)])[:14]))
