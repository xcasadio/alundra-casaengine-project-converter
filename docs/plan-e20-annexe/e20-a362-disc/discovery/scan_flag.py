import sys, json, glob, re
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/a362-h3a/h3critic')
from my_maps import SIZES, DATA, programs, decode
want = int(sys.argv[1])
lo, hi = want & 0xFF, want >> 8
for f in sorted(glob.glob(DATA + 'map_*.json'), key=lambda p: int(re.search(r'map_(\d+)', p).group(1)) if re.search(r'map_(\d+)\.json', p) else -1):
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
                if len(raw) >= 3 and raw[1] == lo and raw[2] == hi and op not in (0x30,0x31,0x34,0x35,0x36):
                    print('map %d %s[%d] @%d %02x %s %s' % (mid, nm, pi, p, op, name, list(raw)))
