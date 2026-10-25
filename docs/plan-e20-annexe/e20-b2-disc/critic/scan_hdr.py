import json, glob, re, os
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
hits = []
glob_hits = []
seen_global = {}
for f in glob.glob(D + "/map_*.json"):
    m = re.fullmatch(r"map_(\d+)\.json", os.path.basename(f))
    if not m:
        continue
    mi = int(m.group(1))
    d = json.load(open(f, encoding="utf-8"))
    si = d['SpriteInfo']
    for idx, rec in enumerate(si['SpriteRecords']):
        if not rec:
            continue
        c = rec['Header'].get('Contents', 0) & 0xFF
        if c == 8:
            hits.append((mi, idx))
        elif c and c < 0x62:
            seen_global.setdefault(c, []).append((mi, idx))
print('sprite headers (local pages) with Contents low byte == 8:', hits)
print('distinct direct-item Contents values seen in sprite headers:', sorted(seen_global))
