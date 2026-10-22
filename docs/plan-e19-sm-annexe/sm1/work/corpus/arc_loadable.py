"""Static necessary conditions for an ArcRun on a map (Alundra.Tests/AlundraArcSupport.cs ctor): the files the constructor reads must exist
(map folder, the map dialogue asset, Shared.dialogue, Etc.dialogue), and every PrefabAssetId of the map's Entities must be an asset of AssetInfos.json
with its file on disk (ArcPrefabAssets.Install), and every SpriteTableIndex must have a sprite record. Reads the repository only.
Usage: python -B arc_loadable.py MAP [MAP ...]"""
import json, glob, os, sys
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
BS = chr(92)
idx = json.load(open(ROOT + '/Maps/world-index.json', encoding='utf-8'))
infos = json.load(open(ROOT + '/AssetInfos.json', encoding='utf-8'))['asset_infos']
by_id = {}
for n in infos:
    by_id[n.get('id') or n.get('Id') or n.get('asset_id')] = n
recs = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))
print('AssetInfos entries', len(infos), 'keys of one:', sorted(infos[0].keys()))
for m in sys.argv[1:]:
    mp = int(m)
    rel = idx[str(mp)].replace(BS, '/')
    folder = os.path.dirname(ROOT + '/' + rel)
    world = os.path.basename(folder)
    zone = os.path.basename(os.path.dirname(folder))
    checks = {
        'zone/world': (zone, world),
        'tilemap': bool(glob.glob(folder + '/tilemap/*.tileMap')),
        'events': os.path.exists(folder + '/events/' + world + '.events.json'),
        'map dialogue': os.path.exists(folder + '/dialogues/' + world + '.dialogue'),
        'Shared.dialogue': os.path.exists(ROOT + '/Dialogues/Shared.dialogue'),
        'Etc.dialogue': os.path.exists(ROOT + '/Dialogues/Etc.dialogue'),
    }
    d = json.load(open(glob.glob(folder + '/tilemap/*.tileMap')[0], encoding='utf-8'))
    missing_prefab = []
    missing_record = []
    n = 0
    for layer in d['object_layers']:
        if layer['name'] != 'Entities':
            continue
        for o in layer['objects']:
            cp = o['custom_properties']
            n += 1
            pid = cp.get('PrefabAssetId')
            if pid not in by_id:
                missing_prefab.append((cp.get('Index'), pid))
            if pid not in recs:
                missing_record.append((cp.get('Index'), pid))
    print('map', mp, checks, 'entities', n, 'prefab ids missing from AssetInfos:', missing_prefab[:6], 'without sprite record:', missing_record[:6])
