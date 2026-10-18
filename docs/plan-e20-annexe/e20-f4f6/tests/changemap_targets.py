# Read-only: linear decode of every map's event Codes with the opcode size table, to list the 0x53 (ChangeMap) instructions and where they warp (internal map index).
# Validation: the runner's own comment says the corpus has 329 occurrences of 0x53; this script must find 329 or the decode is not trustworthy.
import json, glob, os, re, sys, collections
DATA = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
SIZES = json.load(open('C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-weapons/test-means/sizes.json'))
sizes = {int(k): v for k, v in SIZES.items()}
found = []
total = 0
bad_maps = 0
for p in sorted(glob.glob(DATA + '/map_*.json')):
    m = re.match(r'map_(\d+)\.json$', os.path.basename(p))
    if not m:
        continue
    mid = int(m.group(1))
    d = json.load(open(p, encoding='utf-8'))
    ec = d['SpriteInfo']['EventCodes']
    codes = ec['Codes']
    starts = [x for t in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable') for x in ec[t] if x]
    if not starts:
        continue
    pc = min(starts)
    ok = True
    while 0 <= pc < len(codes):
        op = codes[pc]
        sz = sizes.get(op)
        if sz is None or sz == 0:
            sz = 1
        if op == 0x53 and pc + 7 < len(codes):
            idx = (codes[pc + 2] << 8) | codes[pc + 1]
            found.append((mid, pc, idx, codes[pc + 3], codes[pc + 4], codes[pc + 5], codes[pc + 6]))
            total += 1
        pc += sz
print('0x53 occurrences found by the linear decode:', total, '(runner comment: 329)')
tg = collections.Counter(f[2] for f in found)
print('targets 308/309/310:', {k: tg.get(k, 0) for k in (308, 309, 310)})
for f in found:
    if f[2] in (308, 309, 310):
        print('map', f[0], 'pc', f[1], '-> internal index', f[2], 'tile', f[3:6], 'effect', f[6])
