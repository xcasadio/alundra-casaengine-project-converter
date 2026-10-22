import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, tilemap2 as T, records as R
ai = json.load(open(C.PROJECT + '/AssetInfos.json', encoding='utf-8'))['asset_infos']
ids = {a.get('id') or a.get('Id'): a for a in ai}
print('AssetInfos entries:', len(ai), 'first keys:', list(ai[0].keys()))
bad = 0
for m in sorted(R.by_map):
    for lname, o in T.objects(m):
        if lname != 'Entities': continue
        cp = o['custom_properties']
        raw = int(cp[R.KEYS['C']])
        if ('C', raw & 0x7F) not in R.by_map[m]: continue
        pid = cp.get('PrefabAssetId')
        a = ids.get(pid)
        ok = a is not None
        path = a.get('file_name') or a.get('FileName') or a.get('path') if a else None
        exists = False
        if a:
            for k, v in a.items():
                if isinstance(v, str) and v.endswith(('.entity', '.json', '.prefab')):
                    exists = os.path.exists(os.path.join(C.PROJECT, v.replace(chr(92), '/')))
                    path = v
        if not ok or not exists:
            bad += 1
        print('map %d rec %s prefab %s in AssetInfos=%s file=%s exists=%s' % (m, cp['Index'], (pid or '')[:8], ok, path, exists))
print('bad:', bad)
