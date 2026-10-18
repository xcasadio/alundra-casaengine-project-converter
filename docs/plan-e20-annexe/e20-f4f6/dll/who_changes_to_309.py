"""Which maps carry a 'Change map' (0x53) instruction whose destination is map 309 (bytes 53 35 01: v1 + v2<<8 = 309), by a linear walk with the opcode size table.
Read-only.  python -B who_changes_to_309.py"""
import glob, json, re, sys
sys.path.insert(0, '.')
import m  # copy of docs/plan-e19-h2-annexe/tools/m.py (SZ table parsed from EventOpcodeSizeTable.cs)

R = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/'
hits = []
for p in glob.glob(R + 'map_*.json'):
    mm = re.search(r'map_(\d+)\.json$', p)
    if not mm:
        continue
    mid = int(mm.group(1))
    d = json.load(open(p, encoding='utf-8'))
    ec = d['SpriteInfo']['EventCodes']
    C = ec['Codes']
    boundary = sum(2 * len(ec[k]) for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'))
    pc = boundary
    while pc < len(C):
        op = C[pc]
        sz = m.SZ.get(op) or 1
        if op == 0x53 and pc + 2 < len(C) and (C[pc + 1] | (C[pc + 2] << 8)) == 309:
            hits.append((mid, pc, C[pc:pc + 8]))
        pc += sz
print('Change-map to 309:', hits)
