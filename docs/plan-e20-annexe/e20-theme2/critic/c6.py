import sys, json, re, glob, collections
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
res = collections.defaultdict(set)
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
        pc = s; ctx_player = False
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            if op == 0x42: ctx_player = True
            if op == 0x43: ctx_player = False
            if op == 0x1A and ctx_player:
                res[('1A after 0x42', codes[pc+1])].add(mid)
            if op in (0x59, 0x5B):
                res[(hex(op)+' grp%d' % codes[pc+1], codes[pc+2])].add(mid)
            pc += sz
# print animations forced for group 129 plus the 1A after 42
for k in sorted(res, key=lambda x: (x[0], x[1])):
    if k[0].endswith('grp129') or k[0].startswith('1A'):
        print(k[0], hex(k[1]), sorted(res[k])[:12], len(res[k]))
