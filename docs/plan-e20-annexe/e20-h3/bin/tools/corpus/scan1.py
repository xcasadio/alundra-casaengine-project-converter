"""scan1.py - corpus scan (READ-ONLY on alundra-project/ and data-extracted/): where does the game give the bomb (0x23) and the beans (0x1F), where are the objects that react to the bomb, where is the 'Pot magique' (0x199)."""
import sys, json, glob, os, re, collections
sys.stdout.reconfigure(encoding='utf-8')
import m as M

maps = {}
for f in glob.glob(M.MAPS + "/*/*/events/*.events.json"):
    base = os.path.basename(os.path.dirname(os.path.dirname(f.replace("\\", "/"))))
    mm = re.search(r"-(\d+)$", base)
    if mm:
        maps[int(mm.group(1))] = base
print('maps in the project:', len(maps))

def program_spans(mp):
    """(table letter, index, start offset) -> end offset (next higher start or end of codes)"""
    starts = sorted(set(off for t in mp.T.values() for off in t if off))
    return starts

def label_of(mp, pc):
    best = None
    for k, t in mp.T.items():
        for i, off in enumerate(t):
            if off and off <= pc and (best is None or off > best[0]):
                best = (off, k, i)
    return best

def scan_ops(mp):
    C = mp.C
    boundary = sum(2 * len(t) for t in mp.T.values())
    pc = boundary
    out = []
    while pc < len(C):
        op = C[pc]
        sz = M.SZ.get(op) or 1
        out.append((pc, op, list(C[pc + 1:pc + sz])))
        pc += sz
    return out

res = collections.defaultdict(list)
for mid in sorted(maps):
    try:
        mp = M.Map(mid)
    except Exception as ex:
        print('skip', mid, ex); continue
    ops = scan_ops(mp)
    for pc, op, a in ops:
        if op == 0x82 and a and a[0] in (0x23, 0x1F):
            res['give'].append((mid, pc, a[0], label_of(mp, pc)))
        if op == 0x83 and a and a[0] in (0x23, 0x1F):
            res['has'].append((mid, pc, a[0], a, label_of(mp, pc)))
        if op == 0x84 and a and a[0] in (0x23, 0x1F):
            res['use'].append((mid, pc, a[0], label_of(mp, pc)))
        if op == 0x87 and len(a) >= 2 and a[1] == 3:
            res['touch_bomb'].append((mid, pc, a, label_of(mp, pc)))
        if op == 0x95 and len(a) >= 9 and a[8] == 3:
            res['rect_bomb'].append((mid, pc, a, label_of(mp, pc)))
    # placed entities
    for r in mp.records:
        t = r.get('SpriteTableIndex'); d = r.get('SpriteDirection') or 0
        c = r.get('Contents') or 0
        if r.get('IsEnabled') and c in (0x23, 0x1F):
            res['chest'].append((mid, r.get('Index'), t, d, c, r.get('EntityName')))
        if r.get('IsEnabled') and t == 153 and (d & 0x80):
            res['pot'].append((mid, r.get('Index'), r.get('EntityName'), r.get('XPos'), r.get('YPos')))
for k, v in res.items():
    print('==', k, len(v))
    for x in v[:400]:
        print('  ', x)
json.dump({k: [list(map(str, x)) for x in v] for k, v in res.items()}, open('scan1_out.json', 'w'), ensure_ascii=False, indent=0)
