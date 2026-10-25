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
        pc = s
        while pc < end:
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            if op in (0xB7, 0xB8):   # check target anim / current anim
                a = codes[pc+1] if op else 0
                res[(hex(op), 'v1=%d' % codes[pc+1], 'v2=%d' % codes[pc+2])].add(mid)
            pc += sz
for k in sorted(res):
    if k[1] in ('v1=129',) or True:
        pass
# print only those relating to hero (group 129) and anims in the item-mode ranges 0x1f..0x34
for k in sorted(res):
    v1 = int(k[1][3:]); v2 = int(k[2][3:])
    if v1 == 129 and 0x1D <= v2 <= 0x35:
        print(k, sorted(res[k]))
print('all (op, grp) for B7/B8:', sorted({(k[0], k[1]) for k in res}))
