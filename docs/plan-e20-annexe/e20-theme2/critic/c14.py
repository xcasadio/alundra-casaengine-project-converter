import sys, json, re, glob, collections
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
names = {}
import csv
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';'); next(rd)
    rows = list(rd)
for i in range(1, 90):
    if 30 + i < len(rows): names[i] = rows[30 + i][1]
chest = collections.defaultdict(set); o82 = collections.defaultdict(set); spawn = collections.defaultdict(set); placed = collections.defaultdict(set)
for fp in glob.glob(P + 'data-extracted/data/map_*.json'):
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm: continue
    mid = int(mm.group(1))
    if mid == 0: continue
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si: continue
    for e in (si.get('Entities') or {}).get('Entities') or []:
        if e and e.get('SpriteTableIndex') == 30 and e.get('Contents'): chest[e['Contents']].add(mid)
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
            if op == 0x82: o82[codes[pc+1]].add(mid)
            if op in (0x8A, 0x8B) and codes[pc+1] >= 31: spawn[codes[pc+1]-30].add(mid)
            pc += sz
print('items whose ONLY non-test source (map != 0) is a chest (no 0x82, no script spawn):')
for it in range(1, 90):
    if chest.get(it) and not o82.get(it) and not spawn.get(it):
        print(' ', it, names.get(it), sorted(chest[it])[:8], len(chest[it]), 'maps')
print('items with chest on a real map AND also 0x82/spawn (not chest-dependent): count', sum(1 for it in range(1,90) if chest.get(it) and (o82.get(it) or spawn.get(it))))
print('items with no source at all outside map 0 (no chest, no 0x82, no spawn):', [(it, names.get(it)) for it in range(1, 87) if not chest.get(it) and not o82.get(it) and not spawn.get(it)])
