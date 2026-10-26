import sys, json, glob, re
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/a362-h3a/h3critic')
from my_maps import SIZES, DATA, programs, decode
targets = {int(a) for a in sys.argv[1:]}
for f in sorted(glob.glob(DATA + 'map_*.json')):
    mm = re.search(r'map_(\d+)\.json$', f)
    if not mm: continue
    mid = int(mm.group(1))
    try: d = json.load(open(f, encoding='utf-8-sig'))
    except Exception: continue
    if 'SpriteInfo' not in d: continue
    codes, tabs, offs = programs(d)
    for nm, t in tabs.items():
        for pi, off in enumerate(t):
            if off <= 0 or off >= len(codes): continue
            nxt = [o for o in offs if o > off]; end = nxt[0] if nxt else len(codes)
            for (p, op, raw, name) in decode(codes, off, end):
                if raw is None: break
                if op == 0x53 and len(raw) >= 3:
                    tgt = raw[1] | (raw[2] << 8)
                    if tgt in targets:
                        print('map %d %s[%d] @%d -> map %d  raw %s' % (mid, nm, pi, p, tgt, list(raw)))
    # portals
    try:
        for po in d['Info'].get('Portals', []) or []:
            if isinstance(po, dict):
                for k, v in po.items():
                    pass
    except Exception: pass
