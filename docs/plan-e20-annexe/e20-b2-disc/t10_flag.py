import glob, json, re, sys
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
lo, hi = int(sys.argv[1]), int(sys.argv[2])
ops_of_flag = {0x05, 0x06, 0x07, 0x08, 0x35, 0x36, 0x0B, 0x0C}
files = sorted(glob.glob(P + 'data-extracted/data/map_*.json'))
for fp in files:
    mm = re.search(r'map_(\d+)\.json$', fp)
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(fp, encoding='utf-8'))
    si = d.get('SpriteInfo')
    if not si or not isinstance(si.get('EventCodes'), dict): continue
    ec = si['EventCodes']; codes = ec.get('Codes') or []
    starts = {}
    for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for i, o in enumerate(ec.get(k) or []):
            if 0 < o < len(codes): starts.setdefault(o, []).append('%s[%d]' % (k[10], i))
    order = sorted(starts)
    for n, s in enumerate(order):
        end = order[n + 1] if n + 1 < len(order) else len(codes)
        pc = s
        while pc < end and pc < len(codes):
            op = codes[pc]; sz = sizes.get(op, (0, ''))[0]
            if sz <= 0: break
            a = codes[pc + 1: pc + sz]
            if len(a) >= 2 and a[0] == lo and a[1] == hi:
                print('map', mid, starts[s], 'pc', pc, '%02X' % op, sizes[op][1], a)
            pc += sz
