"""scan6.py - which sprite records (global page and every map page) use the native codes of the bomb and the beans: (C,62) (D,30) (E,12) (C,73) (E,21) and (A,x)/(F,x) for the same sprites; also the codes 0 (default) of sprite 1 and 2."""
import sys, json, glob, re, collections
sys.stdout.reconfigure(encoding='utf-8')
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
WANT = {('C', 62), ('D', 30), ('E', 12), ('C', 73), ('E', 21)}
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
        for slot, key in (('A', 'ProgramLoad'), ('C', 'ProgramTick'), ('D', 'ProgramTouch'), ('E', 'ProgramDeactivate'), ('F', 'ProgramInteract')):
            if (slot, h[key]) in WANT:
                use[(slot, h[key])].add(('global' if isg else 'map%s' % mid, sid))
for k in sorted(use):
    v = sorted(use[k])
    print(k, 'records:', len(v), 'distinct sprite ids:', sorted(set(s for _, s in v)), 'pages:', sorted(set(p for p, _ in v))[:6])
