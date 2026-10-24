import sys, glob, os, re, collections
sys.stdout.reconfigure(encoding='utf-8')
import m as M
cnt = collections.Counter(); maps = {}
for f in glob.glob(M.MAPS + "/*/*/events/*.events.json"):
    base = os.path.basename(os.path.dirname(os.path.dirname(f.replace(chr(92), '/'))))
    mm = re.search(r"-(\d+)$", base)
    if not mm: continue
    mid = int(mm.group(1))
    try: mp = M.Map(mid)
    except Exception: continue
    if not mp.cells: continue
    w = mp.cells['walkability']; g = mp.cells['ground_property']
    n_pit = sum(1 for a, b in zip(w, g) if (a & 1) and (b & 0x10))
    n_gp = sum(1 for b in g if b & 0x10)
    n_w = sum(1 for a in w if a & 1)
    n_grass = sum(1 for a in w if a & 2)
    cnt['maps'] += 1; cnt['cells_wk1'] += n_w; cnt['cells_gp10'] += n_gp; cnt['cells_both'] += n_pit; cnt['cells_wk2_grass'] += n_grass
    if n_pit: maps[mid] = n_pit
print(dict(cnt))
print('maps with cells (wk&1 and gp&0x10):', len(maps), sorted(maps.items())[:20])
vals = collections.Counter()
for mid in list(maps)[:50]:
    mp = M.Map(mid)
    for a, b in zip(mp.cells['walkability'], mp.cells['ground_property']):
        if (a & 1) and (b & 0x10): vals[(a, b)] += 1
print(vals.most_common(8))
