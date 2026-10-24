"""scan2.py - bomb-related corpus, per placed record (READ-ONLY on alundra-project/)."""
import sys, json, glob, os, re, collections
sys.stdout.reconfigure(encoding='utf-8')
import m as M

def I(r, k):
    try: return int(r.get(k) or 0)
    except Exception: return 0

maps = {}
for f in glob.glob(M.MAPS + "/*/*/events/*.events.json"):
    base = os.path.basename(os.path.dirname(os.path.dirname(f.replace("\\", "/"))))
    mm = re.search(r"-(\d+)$", base)
    if mm: maps[int(mm.group(1))] = base

def prog_ops(mp, letter, idx):
    off = mp.T[letter][idx] if idx < len(mp.T[letter]) else 0
    if not off: return []
    starts = sorted(set(o for t in mp.T.values() for o in t if o))
    nxt = [s for s in starts if s > off]
    end = nxt[0] if nxt else len(mp.C)
    C = mp.C; pc = off; out = []
    while pc < end and pc < len(C):
        op = C[pc]; sz = M.SZ.get(op) or 1
        out.append((pc, op, list(C[pc + 1:pc + sz])))
        pc += sz
    return out

out = collections.defaultdict(list)
pots = collections.Counter(); pot_maps = collections.defaultdict(list)
chests = collections.defaultdict(list)
for mid in sorted(maps):
    mp = M.Map(mid)
    for r in mp.records:
        if not I(r, 'IsEnabled'): continue
        t = I(r, 'SpriteTableIndex'); d = I(r, 'SpriteDirection'); page = 'M' if d & 0x80 else 'G'
        name = r.get('EntityName')
        ci = I(r, 'Contents')
        if ci in (0x23, 0x1F):
            chests[ci].append((mid, I(r, 'Index'), page, t, name))
        if page == 'M' and t == 153:
            pot_maps[mid].append((I(r, 'Index'), name, I(r, 'XPos'), I(r, 'YPos'), I(r, 'Height')))
        for letter, key in (('A', 'EventCodesA_LoadIndex'), ('C', 'EventCodesC_TickIndex'), ('D', 'EventCodesD_TouchIndex'), ('E', 'EventCodesE_DeactivateIndex'), ('F', 'EventCodesF_InteractIndex')):
            idx = I(r, key)
            if idx < 0x80: continue
            idx -= 0x80
            for pc, op, a in prog_ops(mp, letter, idx):
                if op == 0x87 and len(a) >= 2 and a[1] == 3:
                    out['touch_bomb'].append((mid, I(r, 'Index'), page, t, name, letter, idx, (I(r, 'XPos'), I(r, 'YPos'), I(r, 'Height'))))
                if op == 0x95 and len(a) >= 9 and a[8] == 3:
                    out['rect_bomb'].append((mid, I(r, 'Index'), page, t, name, letter, idx, a[:5]))
                if op in (0x82,) and a and a[0] in (0x23, 0x1F):
                    out['give'].append((mid, I(r, 'Index'), page, t, name, letter, idx, a[0]))
def dedupe(l):
    seen = set(); o = []
    for x in l:
        k = tuple(map(str, x))
        if k not in seen: seen.add(k); o.append(x)
    return o
for k in ('touch_bomb', 'rect_bomb', 'give'):
    print('==', k, len(out[k]))
    for x in dedupe(out[k]): print('  ', x)
print('== chests with Contents 0x23/0x1F:', {hex(k): v for k, v in chests.items()})
print('== map-page sprite 153 placements by map:', {k: len(v) for k, v in sorted(pot_maps.items())})
names = collections.Counter(n for v in pot_maps.values() for (_, n, _, _, _) in v)
print('names of sprite 153:', dict(names))
json.dump(dict(out), open('scan2_out.json', 'w'), ensure_ascii=False, default=str)
