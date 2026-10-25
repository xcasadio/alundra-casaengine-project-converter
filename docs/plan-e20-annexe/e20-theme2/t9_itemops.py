import sys, json, re, glob, collections, csv
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
names = []
with open(P + 'alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv', encoding='utf-8-sig') as f:
    rd = csv.reader(f, delimiter=';')
    next(rd)
    for row in rd:
        names.append(row)
def iname(i):
    return names[30 + i][1] if 0 < i < 98 else str(i)

cnt = collections.defaultdict(lambda: collections.defaultdict(set))  # op -> operand tuple -> set(map)
total_prog = 0
bad = 0
files = sorted(glob.glob(P + 'data-extracted/data/map_*.json'), key=lambda s: int(re.search(r'map_(\d+)\.json', s).group(1)) if re.search(r'map_(\d+)\.json', s) else -1)
for fp in files:
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm:
        continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si or not isinstance(si.get('EventCodes'), dict):
        continue
    ec = si['EventCodes']
    codes = ec.get('Codes') or []
    starts = set()
    for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for o in ec.get(k) or []:
            if 0 < o < len(codes):
                starts.add(o)
    starts = sorted(starts)
    for i, s in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(codes)
        pc = s
        total_prog += 1
        while pc < end:
            if pc >= len(codes):
                break
            op = codes[pc]
            sz = sizes.get(op, (0, ''))[0]
            if sz <= 0:
                bad += 1
                break
            if op in (0x82, 0x83, 0x84, 0xC0):
                operands = tuple(codes[pc + 1: pc + sz])
                cnt[op][operands].add(mid)
            pc += sz
print('programs', total_prog, 'undecodable stops', bad)
print('--- 0x82 (give item or pickup by id): operand -> maps count')
by82 = collections.defaultdict(set)
for ops_, maps in cnt[0x82].items():
    by82[ops_[0]] |= maps
for k in sorted(by82):
    print(k, hex(k), iname(k), len(by82[k]), sorted(by82[k])[:8])
print('--- 0x83 (if number of item >= value)')
by83 = collections.defaultdict(set)
for ops_, maps in cnt[0x83].items():
    by83[ops_[0]] |= maps
for k in sorted(by83):
    print(k, hex(k), iname(k), len(by83[k]), sorted(by83[k])[:8])
print('--- 0x84 (use item)')
by84 = collections.defaultdict(set)
for ops_, maps in cnt[0x84].items():
    by84[ops_[0]] |= maps
for k in sorted(by84):
    print(k, hex(k), iname(k), len(by84[k]), sorted(by84[k])[:8])
print('--- 0xC0 (set weapon slot)')
for ops_, maps in cnt[0xC0].items():
    print(ops_, sorted(maps))
