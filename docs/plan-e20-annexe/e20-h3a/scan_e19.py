import sys, json, glob, re, collections
sys.stdout.reconfigure(encoding='utf-8')
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
use = collections.defaultdict(set)
for f in glob.glob(ROOT + "/map_*.json"):
    mm = re.search(r'map_(\d+|alundra)\.json$', f.replace(chr(92), '/'))
    if not mm: continue
    mid = mm.group(1)
    d = json.load(open(f, encoding='utf-8-sig'))
    isg = (mid == 'alundra')
    for x in d['SpriteInfo']['SpriteRecords']:
        if not x: continue
        h = x['Header']; sid = h['Sector5Id']
        if h['ProgramDeactivate'] == 19:
            use[('global' if isg else 'map%s' % mid, sid)].add((h['BreakEffect'], h['MoreFlags'], h['SizeX'], h['SizeY'], h['SizeZ']))
for k in sorted(use, key=lambda t: (t[0] != 'global', t)):
    print(k, sorted(use[k]))
print('total sprite records with E=19:', len(use))
