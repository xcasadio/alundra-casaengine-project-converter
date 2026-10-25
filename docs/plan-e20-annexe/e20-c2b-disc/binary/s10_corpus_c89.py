"""S10: which map sprite records use the native handlers that touch the hero state (C89 @0x80061EB8 writes hero state 0x31; C48 @0x80074D00 writes hero 1CC=0x78)?
Letter->header field mapping is deduced from the chest (A255/C255/F255)."""
import json, glob, os, re, sys
DATA = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
fields = ['ProgramLoad', 'ProgramTick', 'ProgramTouch', 'ProgramDeactivate', 'ProgramInteract']
res = {}
files = sorted(glob.glob(DATA + '/map_*.json'), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1)) if re.search(r'map_(\d+)\.json', p) else 99999)
for p in files:
    m = re.search(r'map_(\d+)\.json$', p)
    if not m:
        continue
    mid = int(m.group(1))
    try:
        d = json.load(open(p))
    except Exception as e:
        continue
    si = d.get('SpriteInfo') or {}
    for idx, rec in enumerate(si.get('SpriteRecords') or []):
        h = (rec or {}).get('Header') or {}
        for f in fields:
            v = h.get(f)
            if v is None: continue
            res.setdefault((f, v), []).append((mid, idx))
# deduce: find which field holds 255 for the chest
for f in fields:
    print(f, "has 255:", len(res.get((f, 255), [])), "records; 89:", sorted(set(res.get((f, 89), [])))[:8], "; 48:", sorted(set(res.get((f, 48), [])))[:8])
