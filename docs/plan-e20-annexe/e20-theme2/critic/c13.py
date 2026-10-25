import sys, json, re, glob, collections
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
tab = [lib.u8(0x80098FA4 + i) for i in range(16)]
print('table 0x80098FA4:', tab)
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
c87 = collections.defaultdict(set); n87 = collections.Counter(); c95 = collections.defaultdict(set); n95 = collections.Counter()
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
            if op == 0x87: c87[codes[pc+2]].add(mid); n87[codes[pc+2]] += 1
            if op == 0x95: c95[codes[pc+9]].add(mid); n95[codes[pc+9]] += 1
            pc += sz
print('0x87:', {k: (n87[k], len(c87[k])) for k in sorted(c87)})
print('0x95:', {k: (n95[k], len(c95[k])) for k in sorted(c95)})
