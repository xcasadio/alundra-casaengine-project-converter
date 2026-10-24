"""scan3.py - independent cross-check from data-extracted/data/map_*.json (raw extraction): 0x82/0x83/0x84 with 0x23/0x1F, chests, ground pickups (global sprites 61 = I31 and 65 = I35), bomb sprite 2 / beans sprite 1 placed on maps, pots 153."""
import sys, json, glob, os, re, collections
sys.stdout.reconfigure(encoding='utf-8')
import m as M
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
res = collections.defaultdict(list)
n = 0
for f in sorted(glob.glob(ROOT + "/map_*.json"), key=lambda p: int(re.search(r'map_(\d+)', p).group(1)) if re.search(r'map_(\d+)', p) else -1):
    mm = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    n += 1
    si = d['SpriteInfo']
    ec = si['EventCodes']
    C = ec['Codes']; T = {k: ec['EventCodes%sTable' % k] for k in 'ABCDEF'}
    boundary = sum(2 * len(t) for t in T.values())
    starts = sorted(set(o for t in T.values() for o in t if o))
    def lab(pc):
        best = None
        for k, t in T.items():
            for i, off in enumerate(t):
                if off and off <= pc and (best is None or off > best[0]): best = (off, k, i)
        return best[1:] if best else None
    pc = boundary
    while pc < len(C):
        op = C[pc]; sz = M.SZ.get(op) or 1
        a = C[pc + 1:pc + sz]
        if op in (0x82, 0x83, 0x84) and a and a[0] in (0x23, 0x1F):
            res['op%02X' % op].append((mid, pc, hex(a[0]), lab(pc)))
        pc += sz
    ents = si['Entities']['Entities']
    for i, e in enumerate(ents):
        if not e or not e.get('IsEnabled'): continue
        t = e['SpriteTableIndex']; dd = e['SpriteDirection']; page = 'M' if dd & 0x80 else 'G'
        c = e.get('Contents') or 0
        if c in (0x23, 0x1F): res['contents'].append((mid, i, page, t, hex(c)))
        if page == 'G' and t in (61, 65): res['ground_pickup'].append((mid, i, t, 'I31 beans' if t == 61 else 'I35 bomb'))
        if page == 'G' and t in (1, 2): res['placed_global_%d' % t].append((mid, i, e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex']))
print('maps read', n)
for k in sorted(res):
    print('==', k, len(res[k]))
    for x in res[k]: print('  ', x)
