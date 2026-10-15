import glob, json, os, re, collections
ROOT = "D:/development/repo/alundra-casaengine-project-converter"
files = glob.glob(ROOT + "/alundra-project/Maps/*/*/tilemap/*.tileMap")
print(len(files), 'tilemaps')
tot = collections.Counter(); per = {}
for f in files:
    tm = json.load(open(f, encoding='utf-8'))
    cp = tm.get('custom_properties', {})
    if 'AlundraCells' not in cp: continue
    cells = json.loads(cp['AlundraCells'])
    sl = cells['slope']
    mid = int(re.search(r'-(\d+)$', os.path.basename(os.path.dirname(os.path.dirname(f)))).group(1))
    c = collections.Counter()
    for v in sl:
        if v & 0xC0:
            c['drift_any'] += 1
            c['ground04' if v & 4 else 'noground04'] += 1
            c['air08' if v & 8 else 'noair08'] += 1
            c['dir%d' % ((v >> 4) & 3)] += 1
            if v & 0x80: c['flag80'] += 1
    if c['drift_any']:
        per[mid] = c
        tot.update(c)
print(dict(tot))
for mid in sorted(per):
    c = per[mid]
    print(mid, dict(c))
