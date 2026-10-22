"""Read-only static check that each sm3 map is loadable by an ArcRun in Prefabs mode: folder, world, events, tilemap exist; every PrefabAssetId of the Entities
layer resolves in AssetInfos.json; the warps/entry tile candidates."""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import corpus
P = corpus.PROJECT
infos = {a['id'].lower() if isinstance(a, dict) and 'id' in a else None: a for a in json.load(open(P + '/AssetInfos.json', encoding='utf-8'))['asset_infos']}
print('asset infos:', len(infos))
for mid in (54,159,160,258,298,421,423,424,425,426,440):
    rel = corpus._index[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    folder = P + '/' + os.path.dirname(rel)
    ok = {n: os.path.exists(f) for n, f in (('world', folder + '/' + base + '.world'), ('events', folder + '/events/' + base + '.events.json'), ('tileMap', folder + '/tilemap/' + base + '.tileMap'), ('dialogue', folder + '/dialogues/' + base + '.dialogue'))}
    d = json.load(open(folder + '/tilemap/' + base + '.tileMap', encoding='utf-8'))
    ents = [o for l in d['object_layers'] if l['name'] == 'Entities' for o in l['objects']]
    missing = sorted({o['custom_properties'].get('PrefabAssetId') for o in ents if o['custom_properties'].get('PrefabAssetId') and o['custom_properties']['PrefabAssetId'].lower() not in infos})
    noprefab = [o['custom_properties']['Index'] for o in ents if not o['custom_properties'].get('PrefabAssetId')]
    print(mid, base, ok, 'entities', len(ents), 'missing prefab ids', missing, 'records without prefab', noprefab, 'dir', os.path.dirname(rel))
