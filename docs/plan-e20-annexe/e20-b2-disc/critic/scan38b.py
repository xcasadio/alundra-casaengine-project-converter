import json, glob, re, os
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
rows = []
for f in glob.glob(D + "/map_*.json"):
    m = re.fullmatch(r"map_(\d+)\.json", os.path.basename(f))
    if not m:
        continue
    mi = int(m.group(1))
    d = json.load(open(f, encoding="utf-8"))
    si = d['SpriteInfo']
    E = si['Entities']['Entities']
    ST = si['SpriteTable']
    for i, e in enumerate(E):
        if e is None:
            continue
        if e['SpriteTableIndex'] == 38:
            rows.append((mi, i, ST[38] if 38 < len(ST) else 'oob', e['XPos'], e['YPos']))
loc = [r for r in rows if r[2] != -1]
glob_ = [r for r in rows if r[2] == -1]
print('type-38 placements with SpriteTable[38] == -1 (global page, item 8 pickup):', sorted((r[0], r[1]) for r in glob_))
print('type-38 placements with a map-local SpriteTable[38]:', len(loc), 'maps', sorted(set(r[0] for r in loc))[:60])
